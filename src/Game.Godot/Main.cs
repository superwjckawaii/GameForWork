using System.Diagnostics;
using System.Text.Json;
using GameForWork.Core.Combat;
using GameForWork.Core.Diagnostics;
using GameForWork.Core.Offline;
using GameForWork.Core.Persistence;
using Godot;

namespace GameForWork.GodotClient;

public partial class Main : Node
{
    private const ulong DefaultSeed = 20_260_827;
    private readonly BattleEngine _engine = new();
    private readonly List<BattleCommand> _commands = [];
    private readonly List<BattleEvent> _events = [];
    private SingleInstanceCoordinator? _singleInstance;
    private WindowController? _windowController;
    private JsonLineLogger? _logger;
    private SaveRepository? _saveRepository;
    private SettingsStore? _settingsStore;
    private string _savesRoot = string.Empty;
    private int _activeSlot = 1;
    private BattleState _state = P0BattleFactory.Create(DefaultSeed);
    private ArenaView? _arena;
    private Label? _status;
    private RichTextLabel? _eventLog;
    private Label? _pauseLabel;
    private ConfirmationDialog? _closeDialog;
    private CheckBox? _rememberCloseChoice;
    private HBoxContainer? _standardToolbar;
    private HBoxContainer? _miniToolbar;
    private VBoxContainer? _sidePanel;
    private HFlowContainer? _testHarness;
    private double _tickAccumulator;
    private double _nonCombatSeconds;
    private bool _battlePaused;
    private bool _quitting;
    private int _restoreRequested;

    public override void _Ready()
    {
        _singleInstance = new SingleInstanceCoordinator();
        if (!_singleInstance.IsPrimary)
        {
            _singleInstance.NotifyPrimary();
            GetTree().Quit();
            return;
        }

        string userDirectory = ProjectSettings.GlobalizePath("user://");
        _logger = new JsonLineLogger(Path.Combine(userDirectory, "logs"));
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        _settingsStore = new SettingsStore(Path.Combine(userDirectory, "settings.json"));
        _logger.Write(GameLogLevel.Information, "p0.start", "application", "P0 application started.");

        _savesRoot = Path.Combine(userDirectory, "saves");
        TryInitializeSave(_activeSlot);
        BuildInterface();
        GetTree().AutoAcceptQuit = false;
        GetWindow().CloseRequested += OnCloseRequested;
        _windowController = new WindowController(GetWindow(), _settingsStore, TogglePause, OpenLogs, QuitApplication);
        _windowController.Initialize();
        _singleInstance.StartListening(() => Interlocked.Exchange(ref _restoreRequested, 1));
        UpdateInterface();
    }

    public override void _Process(double delta)
    {
        _nonCombatSeconds += delta;
        if (Interlocked.Exchange(ref _restoreRequested, 0) == 1)
        {
            _windowController?.Restore();
        }

        _windowController?.TickSnapping();
        UpdateWindowModeInterface();
        if (!_battlePaused && !_state.IsFinished)
        {
            _tickAccumulator += delta;
            const double tickDuration = 1.0 / BattleState.TicksPerSecond;
            while (_tickAccumulator >= tickDuration && !_state.IsFinished)
            {
                _tickAccumulator -= tickDuration;
                StepSimulation();
            }
        }

        UpdateInterface();
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (inputEvent is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Tab } &&
            DisplayServer.WindowIsFocused())
        {
            _windowController?.HideToTray();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _ExitTree()
    {
        if (_singleInstance?.IsPrimary == true)
        {
            _windowController?.Dispose();
        }

        _saveRepository?.Dispose();
        _logger?.Write(GameLogLevel.Information, "p0.stop", "application", "P0 application stopped.");
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        _logger?.Dispose();
        _singleInstance?.Dispose();
    }

    private void BuildInterface()
    {
        var root = new VBoxContainer();
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddThemeConstantOverride("separation", 8);
        AddChild(root);

        _standardToolbar = new HBoxContainer();
        root.AddChild(_standardToolbar);
        AddButton(_standardToolbar, "标准/迷你", ToggleWindowMode);
        AddButton(_standardToolbar, "暂停/继续", TogglePause);
        AddButton(_standardToolbar, "单步", StepOnce);
        AddButton(_standardToolbar, "同种子重放", ReplaySameSeed);
        AddButton(_standardToolbar, "保存", SaveCurrentSnapshot);
        var slots = new OptionButton { TooltipText = "存档槽" };
        slots.AddItem("存档 1", 1);
        slots.AddItem("存档 2", 2);
        slots.AddItem("存档 3", 3);
        slots.ItemSelected += index => SwitchSaveSlot(slots.GetItemId((int)index));
        _standardToolbar.AddChild(slots);
        AddButton(_standardToolbar, "置顶", () => _windowController?.ToggleAlwaysOnTop());
        AddButton(_standardToolbar, "隐藏到托盘 (Tab)", () => _windowController?.HideToTray());

        int initialOpacity = _settingsStore?.Load().OpacityPercent ?? 100;
        var opacity = new HSlider { MinValue = 70, MaxValue = 100, Step = 5, Value = initialOpacity, CustomMinimumSize = new Vector2(120, 0) };
        opacity.ValueChanged += value => _windowController?.SetOpacity((int)value);
        _standardToolbar.AddChild(opacity);
        _standardToolbar.AddChild(new Label { Text = "透明度" });

        _miniToolbar = new HBoxContainer { Visible = false };
        root.AddChild(_miniToolbar);
        AddButton(_miniToolbar, "展开", ToggleWindowMode);
        AddButton(_miniToolbar, "暂停", TogglePause);
        AddButton(_miniToolbar, "托盘", () => _windowController?.HideToTray());
        AddDragButton(_miniToolbar);
        AddButton(_miniToolbar, "关闭", OnCloseRequested);
        var miniOpacity = new HSlider { MinValue = 70, MaxValue = 100, Step = 5, Value = initialOpacity, CustomMinimumSize = new Vector2(65, 0) };
        miniOpacity.ValueChanged += value => _windowController?.SetOpacity((int)value);
        _miniToolbar.AddChild(miniOpacity);

        var content = new HSplitContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        root.AddChild(content);
        _arena = new ArenaView { State = _state, CustomMinimumSize = new Vector2(440, 360), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        content.AddChild(_arena);

        _sidePanel = new VBoxContainer { CustomMinimumSize = new Vector2(330, 0) };
        content.AddChild(_sidePanel);
        _pauseLabel = new Label();
        _sidePanel.AddChild(_pauseLabel);
        _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _sidePanel.AddChild(_status);
        _sidePanel.AddChild(new Label { Text = "最近 10 条权威事件" });
        _eventLog = new RichTextLabel { FitContent = false, SizeFlagsVertical = Control.SizeFlags.ExpandFill, ScrollActive = true };
        _sidePanel.AddChild(_eventLog);

        _testHarness = new HFlowContainer();
        root.AddChild(_testHarness);
        AddButton(_testHarness, "P0: 模拟48h", RunOfflineBenchmark);
        AddButton(_testHarness, "P0: 快照/哈希", SaveCurrentSnapshot);
        AddButton(_testHarness, "P0: 备份", CreateBackup);
        AddButton(_testHarness, "P0: 损坏TEST存档并恢复", CorruptAndRecoverTestSave);
        AddButton(_testHarness, "P0: 触发日志错误", TriggerLogError);
        AddButton(_testHarness, "打开日志", OpenLogs);
        AddButton(_testHarness, "复制当前日志路径", CopyLogPath);
        AddButton(_testHarness, "重置关闭询问", ResetCloseChoice);

        _closeDialog = new ConfirmationDialog
        {
            Title = "关闭 GameForWork",
            DialogText = "退出程序，还是缩到托盘继续挂机？",
            OkButtonText = "退出",
            CancelButtonText = "缩到托盘",
        };
        _rememberCloseChoice = new CheckBox { Text = "记住本次选择" };
        _closeDialog.AddChild(_rememberCloseChoice);
        _closeDialog.Confirmed += () => CompleteCloseChoice(closeToTray: false);
        _closeDialog.Canceled += () => CompleteCloseChoice(closeToTray: true);
        AddChild(_closeDialog);
    }

    private static void AddButton(Container parent, string text, Action action)
    {
        var button = new Button { Text = text };
        button.Pressed += action;
        parent.AddChild(button);
    }

    private static void AddDragButton(Container parent)
    {
        var button = new Button { Text = "拖动" };
        button.GuiInput += inputEvent =>
        {
            if (inputEvent is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
            {
                DisplayServer.WindowStartDrag();
            }
        };
        parent.AddChild(button);
    }

    private void StepSimulation()
    {
        IReadOnlyList<BattleCommand> tickCommands = _engine.BuildAutomaticCommands(_state);
        _commands.AddRange(tickCommands);
        _events.AddRange(_engine.Step(_state, tickCommands));
        _arena?.QueueRedraw();
        if (_state.IsFinished)
        {
            _logger?.Write(
                GameLogLevel.Information,
                "battle.ended",
                "combat",
                "P0 battle ended.",
                new Dictionary<string, object?>
                {
                    ["seed"] = _state.Seed,
                    ["outcome"] = _state.Outcome.ToString(),
                    ["tick"] = _state.Tick,
                    ["hash"] = BattleStateCodec.Hash(_state),
                });
            UpdateTrayState();
        }
    }

    private void StepOnce()
    {
        if (!_state.IsFinished)
        {
            StepSimulation();
        }
    }

    private void ReplaySameSeed()
    {
        _state = P0BattleFactory.Create(_state.Seed);
        _commands.Clear();
        _events.Clear();
        _tickAccumulator = 0;
        if (_arena is not null)
        {
            _arena.State = _state;
            _arena.QueueRedraw();
        }
    }

    private void TogglePause()
    {
        _battlePaused = !_battlePaused;
        UpdateTrayState();
    }

    private void ToggleWindowMode()
    {
        _windowController?.ToggleMode();
        UpdateWindowModeInterface();
    }

    private void UpdateWindowModeInterface()
    {
        if (_windowController is null || _standardToolbar is null || _miniToolbar is null ||
            _sidePanel is null || _testHarness is null || _arena is null)
        {
            return;
        }

        bool mini = _windowController.IsMini;
        _standardToolbar.Visible = !mini;
        _sidePanel.Visible = !mini;
        _testHarness.Visible = !mini;
        _arena.CustomMinimumSize = mini ? Vector2.Zero : new Vector2(440, 360);
        Vector2 mouse = GetViewport().GetMousePosition();
        _miniToolbar.Visible = mini && mouse.Y <= 72;
    }

    private void RunOfflineBenchmark()
    {
        try
        {
            _saveRepository?.CreateBackup();
            var watch = Stopwatch.StartNew();
            OfflineResult result = new OfflineSimulator().Simulate(OfflineTime.MaximumMilliseconds, _state.Seed);
            watch.Stop();
            long endUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long startUtcMs = endUtcMs - OfflineTime.MaximumMilliseconds;
            string intervalId = $"p0-test-{_activeSlot}-{endUtcMs}";
            bool committed = _saveRepository?.TryCommitOfflineSession(
                intervalId,
                startUtcMs,
                endUtcMs,
                result.TotalBattles,
                JsonSerializer.Serialize(result)) ?? false;
            _logger?.Write(
                GameLogLevel.Information,
                "offline.benchmark",
                "offline",
                "48-hour offline simulation completed.",
                new Dictionary<string, object?>
                {
                    ["elapsed_ms"] = watch.ElapsedMilliseconds,
                    ["battles"] = result.TotalBattles,
                    ["last_hash"] = result.LastHash,
                    ["committed"] = committed,
                });
            ShowNotice($"48h 精确模拟完成并提交：{result.TotalBattles} 场，耗时 {watch.ElapsedMilliseconds} ms");
        }
        catch (Exception exception)
        {
            ReportError("offline.benchmark_failed", "48-hour offline simulation failed.", exception);
        }
    }

    private void SaveCurrentSnapshot()
    {
        try
        {
            _saveRepository?.SaveSnapshot(_state.Tick, BattleStateCodec.Serialize(_state));
            ShowNotice($"快照已提交。SHA-256: {BattleStateCodec.Hash(_state)}");
        }
        catch (Exception exception)
        {
            ReportError("save.snapshot_failed", "Snapshot save failed.", exception);
        }
    }

    private void CreateBackup()
    {
        try
        {
            string path = _saveRepository?.CreateBackup() ?? "存档未初始化";
            ShowNotice($"自动备份：{path}");
        }
        catch (Exception exception)
        {
            ReportError("save.backup_failed", "Backup failed.", exception);
        }
    }

    private void CorruptAndRecoverTestSave()
    {
        string root = Path.Combine(ProjectSettings.GlobalizePath("user://"), "test_harness", Guid.NewGuid().ToString("N"));
        try
        {
            string databasePath;
            using (var testRepository = new SaveRepository(root, 1))
            {
                testRepository.Initialize();
                testRepository.SaveSnapshot(0, BattleStateCodec.Serialize(P0BattleFactory.Create(1)));
                testRepository.CreateBackup();
                databasePath = testRepository.DatabasePath;
            }

            File.WriteAllBytes(databasePath, "intentionally corrupt TEST save"u8.ToArray());
            using (var recovered = new SaveRepository(root, 1))
            {
                recovered.Initialize();
                if (recovered.LoadLatestSnapshot() is null)
                {
                    throw new InvalidDataException("Recovered TEST save has no snapshot.");
                }
            }

            ShowNotice("TEST 存档损坏检测、recovery 保留和自动备份恢复均成功。");
        }
        catch (Exception exception)
        {
            ReportError("save.test_recovery_failed", "TEST save recovery failed.", exception);
        }
    }

    private void TriggerLogError()
    {
        try
        {
            throw new InvalidOperationException("Intentional P0 test error; no game state was changed.");
        }
        catch (Exception exception)
        {
            ReportError("diagnostics.intentional_error", "Intentional error test.", exception);
        }
    }

    private void CopyLogPath()
    {
        if (_logger is not null)
        {
            DisplayServer.ClipboardSet(_logger.CurrentLogPath);
            ShowNotice("当前日志路径已复制。");
        }
    }

    private void ResetCloseChoice()
    {
        if (_settingsStore is null)
        {
            return;
        }

        _settingsStore.Save(_settingsStore.Load() with { CloseToTray = null });
        ShowNotice("关闭行为已重置；下次点击关闭会重新询问。");
    }

    private void OpenLogs()
    {
        string path = Path.Combine(ProjectSettings.GlobalizePath("user://"), "logs");
        _ = OS.ShellOpen(path);
    }

    private void OnCloseRequested()
    {
        if (_quitting)
        {
            return;
        }

        GameSettings settings = _settingsStore?.Load() ?? new GameSettings();
        if (settings.CloseToTray.HasValue)
        {
            if (settings.CloseToTray.Value)
            {
                _windowController?.HideToTray();
            }
            else
            {
                QuitApplication();
            }

            return;
        }

        _rememberCloseChoice!.ButtonPressed = false;
        _closeDialog!.PopupCentered();
    }

    private void CompleteCloseChoice(bool closeToTray)
    {
        if (_rememberCloseChoice!.ButtonPressed && _settingsStore is not null)
        {
            GameSettings settings = _settingsStore.Load() with { CloseToTray = closeToTray };
            _settingsStore.Save(settings);
        }

        if (closeToTray)
        {
            _windowController?.HideToTray();
        }
        else
        {
            QuitApplication();
        }
    }

    private void QuitApplication()
    {
        try
        {
            _saveRepository?.SaveSnapshot(_state.Tick, BattleStateCodec.Serialize(_state));
        }
        catch (Exception exception)
        {
            _logger?.Write(GameLogLevel.Error, "save.exit_failed", "save", "Immediate exit save failed.", exception: exception);
        }

        _quitting = true;
        GetTree().Quit();
    }

    private void SwitchSaveSlot(int slot)
    {
        if (slot == _activeSlot)
        {
            return;
        }

        try
        {
            _saveRepository?.SaveSnapshot(_state.Tick, BattleStateCodec.Serialize(_state));
        }
        catch (Exception exception)
        {
            ReportError("save.switch_write_failed", "Current slot could not be saved before switching.", exception);
            return;
        }

        _saveRepository?.Dispose();
        _saveRepository = null;
        _activeSlot = slot;
        TryInitializeSave(slot);
        _commands.Clear();
        _events.Clear();
        if (_arena is not null)
        {
            _arena.State = _state;
            _arena.QueueRedraw();
        }

        ShowNotice($"已切换到存档槽 {_activeSlot}。");
    }

    private void TryInitializeSave(int slot)
    {
        try
        {
            _saveRepository = new SaveRepository(_savesRoot, slot);
            _saveRepository.Initialize();
            _saveRepository.CreateBackup();
            byte[]? snapshot = _saveRepository.LoadLatestSnapshot();
            _state = snapshot is null
                ? P0BattleFactory.Create(unchecked(DefaultSeed + (ulong)(slot - 1)))
                : BattleStateCodec.Deserialize(snapshot);
            SettleOfflineOnOpen();
        }
        catch (Exception exception)
        {
            _saveRepository?.Dispose();
            _saveRepository = null;
            ReportError("save.initialize_failed", "Save initialization failed; no new save was created.", exception);
        }
    }

    private void SettleOfflineOnOpen()
    {
        if (_saveRepository is null)
        {
            return;
        }

        long lastObservedUtcMs = _saveRepository.GetLastObservedUtcMs();
        long nowUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        OfflineElapsed elapsed = OfflineTime.Calculate(lastObservedUtcMs, nowUtcMs);
        OfflineResult result = new OfflineSimulator().Simulate(elapsed.EffectiveMilliseconds, _state.Seed);
        string intervalId = $"startup-{_activeSlot}-{lastObservedUtcMs}-{nowUtcMs}";
        _saveRepository.TryCommitOfflineSession(
            intervalId,
            lastObservedUtcMs,
            nowUtcMs,
            result.TotalBattles,
            JsonSerializer.Serialize(result));
        _logger?.Write(
            elapsed.ClockMovedBackward ? GameLogLevel.Warning : GameLogLevel.Information,
            "offline.startup_settled",
            "offline",
            "Startup offline interval was committed before presentation.",
            new Dictionary<string, object?>
            {
                ["effective_ms"] = elapsed.EffectiveMilliseconds,
                ["clock_moved_backward"] = elapsed.ClockMovedBackward,
                ["clamped"] = elapsed.WasClamped,
                ["battles"] = result.TotalBattles,
            });
    }

    private void ReportError(string eventId, string message, Exception exception)
    {
        _logger?.Write(GameLogLevel.Error, eventId, "p0", message, exception: exception);
        _windowController?.SetTrayStatus(TrayStatus.Stopped);
        ShowNotice($"错误：{message}\n{exception.Message}");
    }

    private void ShowNotice(string message)
    {
        _events.Add(new BattleEvent(_state.Tick, BattleEventKind.BattleEnded, Detail: message));
        UpdateInterface();
    }

    private void UpdateInterface()
    {
        if (_status is null || _eventLog is null || _pauseLabel is null)
        {
            return;
        }

        ActorState hero = _state.Actors[1];
        ActorState enemy = _state.Actors[2];
        _pauseLabel.Text = $"战斗：{(_battlePaused ? "已暂停" : "运行中")}  | 非战斗计时：{(long)_nonCombatSeconds}s";
        _status.Text =
            $"存档槽 {_activeSlot}  Tick {_state.Tick}/{BattleState.MaxTicks}  Seed {_state.Seed}\n" +
            $"结局：{_state.Outcome}\n" +
            $"Hero  pos=({hero.XRaw},{hero.YRaw}) hp={hero.Life}/{hero.MaxLife} cd={hero.CooldownRemainingTicks}\n" +
            $"Enemy pos=({enemy.XRaw},{enemy.YRaw}) hp={enemy.Life}/{enemy.MaxLife} cd={enemy.CooldownRemainingTicks}\n" +
            $"State SHA-256:\n{BattleStateCodec.Hash(_state)}\n" +
            $"日志：{_logger?.CurrentLogPath}";
        _eventLog.Text = string.Join(
            '\n',
            _events.TakeLast(10).Select(item =>
                $"[{item.Tick:000}] {item.Kind} A={item.ActorId} T={item.TargetActorId} V={item.Value} {(item.Success ? "OK" : "")} {item.Detail}"));
    }

    private void UpdateTrayState()
    {
        TrayStatus status = _battlePaused
            ? TrayStatus.Paused
            : _state.IsFinished
                ? TrayStatus.Waiting
                : TrayStatus.Normal;
        _windowController?.SetTrayStatus(status);
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs eventArgs)
    {
        if (eventArgs.ExceptionObject is Exception exception)
        {
            _logger?.Write(GameLogLevel.Error, "application.unhandled", "application", "Unhandled exception.", exception: exception);
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs eventArgs)
    {
        _logger?.Write(GameLogLevel.Error, "application.unobserved_task", "application", "Unobserved task exception.", exception: eventArgs.Exception);
        eventArgs.SetObserved();
    }
}
