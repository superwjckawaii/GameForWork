using System.Text.Json;
using GameForWork.Core.Diagnostics;
using GameForWork.Core.Offline;
using GameForWork.Core.P1;
using GameForWork.Core.P1.World;
using GameForWork.Core.Persistence;
using Godot;

namespace GameForWork.GodotClient;

public partial class Main : Node
{
    private const ulong DefaultSeed = 20_260_827;
    private const double SimulationStepSeconds = 0.05;
    private const long SimulationStepMilliseconds = 50;
    private static readonly JsonSerializerOptions SaveJsonOptions = new() { WriteIndented = false };
    private SingleInstanceCoordinator? _singleInstance;
    private WindowController? _windowController;
    private JsonLineLogger? _logger;
    private SaveRepository? _saveRepository;
    private SettingsStore? _settingsStore;
    private P1GameSession? _session;
    private P2Dashboard? _dashboard;
    private HFlowContainer? _standardToolbar;
    private HBoxContainer? _miniToolbar;
    private HFlowContainer? _testHarness;
    private Button? _largeWindowButton;
    private Label? _noticeLabel;
    private ConfirmationDialog? _closeDialog;
    private CheckBox? _rememberCloseChoice;
    private string _savesRoot = string.Empty;
    private int _activeSlot = 1;
    private bool _battlePaused;
    private bool _quitting;
    private int _restoreRequested;
    private double _simulationAccumulator;
    private double _autoSaveAccumulator;

    public override void _Ready()
    {
        Engine.MaxFps = 60;
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
        _logger.Write(GameLogLevel.Information, "p1a.start", "application", "P1A application started.");
        _savesRoot = Path.Combine(userDirectory, "saves");
        TryInitializeSave(_activeSlot);
        BuildInterface();

        GetTree().AutoAcceptQuit = false;
        GetWindow().CloseRequested += OnCloseRequested;
        _windowController = new WindowController(GetWindow(), _settingsStore, TogglePause, OpenLogs, QuitApplication);
        _windowController.Initialize();
        if (_session is null && _windowController.IsMini)
        {
            _windowController.ToggleMode();
        }

        if (_largeWindowButton is not null)
        {
            _largeWindowButton.Visible = _windowController.CanUseLarge;
        }

        _singleInstance.StartListening(() => Interlocked.Exchange(ref _restoreRequested, 1));
        UpdateWindowModeInterface();
        UpdateTrayState();
    }

    public override void _Process(double delta)
    {
        if (Interlocked.Exchange(ref _restoreRequested, 0) == 1)
        {
            _windowController?.Restore();
        }

        _windowController?.TickSnapping(delta);
        UpdateWindowModeInterface();
        Engine.MaxFps = _windowController?.IsHiddenToTray == true
            ? 5
            : _windowController?.IsMini == true && !DisplayServer.WindowIsFocused() ? 30 : 60;
        if (_session is null)
        {
            _dashboard?.Tick(delta);
            return;
        }

        _simulationAccumulator += delta;
        _autoSaveAccumulator += delta;
        while (_simulationAccumulator >= SimulationStepSeconds)
        {
            _simulationAccumulator -= SimulationStepSeconds;
            try
            {
                if (_battlePaused)
                {
                    _session.AdvanceTownOnly(SimulationStepMilliseconds);
                }
                else
                {
                    _session.Advance(SimulationStepMilliseconds);
                }
            }
            catch (Exception exception)
            {
                ReportError("p1a.tick_failed", "P1A simulation tick failed.", exception);
                _battlePaused = true;
                break;
            }
        }

        _dashboard?.Tick(delta);

        if (_autoSaveAccumulator >= 10.0)
        {
            _autoSaveAccumulator = 0;
            SaveP1State(showNotice: false);
        }
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
        _logger?.Write(GameLogLevel.Information, "p1a.stop", "application", "P1A application stopped.");
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        _logger?.Dispose();
        _singleInstance?.Dispose();
    }

    private void BuildInterface()
    {
        var root = new VBoxContainer();
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddThemeConstantOverride("separation", 6);
        AddChild(root);

        _standardToolbar = new HFlowContainer();
        root.AddChild(_standardToolbar);
        AddButton(_standardToolbar, "标准/迷你", ToggleWindowMode);
        _largeWindowButton = AddButton(_standardToolbar, "大窗口 1920×1280", ToggleLargeWindow);
        AddButton(_standardToolbar, "暂停战斗", TogglePause);
        AddButton(_standardToolbar, "保存", () => SaveP1State(showNotice: true));
        var slots = new OptionButton { TooltipText = "三个独立存档槽" };
        for (int slot = 1; slot <= 3; slot++)
        {
            slots.AddItem($"存档 {slot}", slot);
        }

        slots.ItemSelected += index => SwitchSaveSlot(slots.GetItemId((int)index));
        _standardToolbar.AddChild(slots);
        AddButton(_standardToolbar, "置顶", () => _windowController?.ToggleAlwaysOnTop());
        AddButton(_standardToolbar, "隐藏到托盘 (Tab)", () => _windowController?.HideToTray());
        int initialOpacity = _settingsStore?.Load().OpacityPercent ?? 100;
        var opacity = new HSlider
        {
            MinValue = 70,
            MaxValue = 100,
            Step = 5,
            Value = initialOpacity,
            CustomMinimumSize = new Vector2(100, 0),
            TooltipText = "窗口透明度 70%～100%",
        };
        opacity.ValueChanged += value => _windowController?.SetOpacity((int)value);
        _standardToolbar.AddChild(opacity);
        var snapToggle = new CheckButton
        {
            Text = "边缘吸附",
            ButtonPressed = _settingsStore?.Load().SnapEnabled ?? true,
            TooltipText = "停止拖动后才判断吸附；关闭后可自由拖动",
        };
        snapToggle.Toggled += enabled => _windowController?.SetSnapEnabled(enabled);
        _standardToolbar.AddChild(snapToggle);

        _miniToolbar = new HBoxContainer { Visible = false, Alignment = BoxContainer.AlignmentMode.Center };
        _miniToolbar.AddThemeConstantOverride("separation", 2);
        root.AddChild(_miniToolbar);
        AddMiniButton(_miniToolbar, "展开", "恢复标准窗口", 52, ToggleWindowMode);
        AddMiniButton(_miniToolbar, "暂停", "只暂停两队战斗；城镇继续生产", 52, TogglePause);
        AddMiniButton(_miniToolbar, "托盘", "隐藏到系统托盘", 52, () => _windowController?.HideToTray());
        AddDragButton(_miniToolbar);
        AddMiniButton(_miniToolbar, "×", "关闭", 36, OnCloseRequested);
        var miniOpacity = new HSlider
        {
            MinValue = 70,
            MaxValue = 100,
            Step = 5,
            Value = initialOpacity,
            TooltipText = "透明度",
            CustomMinimumSize = new Vector2(62, 28),
        };
        miniOpacity.ValueChanged += value => _windowController?.SetOpacity((int)value);
        _miniToolbar.AddChild(miniOpacity);

        _noticeLabel = new Label
        {
            Text = "P2 主线与构筑管理 · 20 Hz 确定性模拟 / 60 FPS 画面",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        root.AddChild(_noticeLabel);
        _dashboard = new P2Dashboard();
        _dashboard.Initialize(_session, CreateCharacter, OnSessionChanged, ShowNotice);
        root.AddChild(_dashboard);

        _testHarness = new HFlowContainer();
        root.AddChild(_testHarness);
        AddButton(_testHarness, "P2: 模拟48h", RunOfflineBenchmark);
        AddButton(_testHarness, "P2: 备份", CreateBackup);
        AddButton(_testHarness, "打开日志", OpenLogs);
        AddButton(_testHarness, "复制日志路径", CopyLogPath);
        AddButton(_testHarness, "重置关闭询问", ResetCloseChoice);

        _closeDialog = new ConfirmationDialog
        {
            Title = "关闭 GameForWork",
            DialogText = string.Empty,
            OkButtonText = "退出",
            CancelButtonText = "缩到托盘",
            MinSize = new Vector2I(460, 200),
        };
        var closeContent = new VBoxContainer
        {
            Position = new Vector2(20, 20),
            Size = new Vector2(420, 100),
        };
        closeContent.AddThemeConstantOverride("separation", 14);
        closeContent.AddChild(new Label
        {
            Text = "要退出程序，还是缩到托盘继续挂机？",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        _rememberCloseChoice = new CheckBox { Text = "记住本次选择" };
        closeContent.AddChild(_rememberCloseChoice);
        _closeDialog.AddChild(closeContent);
        _closeDialog.Confirmed += () => CompleteCloseChoice(closeToTray: false);
        _closeDialog.Canceled += () => CompleteCloseChoice(closeToTray: true);
        AddChild(_closeDialog);
    }

    private void CreateCharacter(PlayerIdentity identity)
    {
        _session = P1GameSession.CreateNew(identity, unchecked(DefaultSeed + (ulong)(_activeSlot - 1)));
        _dashboard?.SetSession(_session);
        SaveP1State(showNotice: false);
        ShowNotice($"{identity.Name} 已与古代门扉建立契约；第一幕“余烬营地”开始自动推进。");
    }

    private void OnSessionChanged()
    {
        SaveP1State(showNotice: false);
        UpdateTrayState();
    }

    private void TogglePause()
    {
        _battlePaused = !_battlePaused;
        ShowNotice(_battlePaused ? "战斗模拟已暂停；城镇生产继续。" : "两支队伍已继续战斗。");
        UpdateTrayState();
    }

    private void ToggleWindowMode()
    {
        _windowController?.ToggleMode();
        UpdateWindowModeInterface();
    }

    private void ToggleLargeWindow()
    {
        if (_windowController?.CanUseLarge != true)
        {
            ShowNotice("当前屏幕可用区域不足 1920×1280，大窗口选项不可用。");
            return;
        }

        _windowController.ToggleLarge();
        UpdateWindowModeInterface();
    }

    private void UpdateWindowModeInterface()
    {
        if (_windowController is null || _standardToolbar is null || _miniToolbar is null ||
            _testHarness is null || _dashboard is null || _noticeLabel is null)
        {
            return;
        }

        bool mini = _windowController.IsMini;
        _standardToolbar.Visible = !mini;
        _miniToolbar.Visible = mini;
        _testHarness.Visible = !mini;
        _noticeLabel.Visible = !mini;
        _dashboard.SetMiniMode(mini);
    }

    private void RunOfflineBenchmark()
    {
        if (_session is null)
        {
            ShowNotice("请先创建角色。");
            return;
        }

        try
        {
            _saveRepository?.CreateBackup();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            P1OfflineResult result = _session.AdvanceOffline(OfflineTime.MaximumMilliseconds);
            watch.Stop();
            SaveP1State(showNotice: false);
            ShowNotice(
                $"48h P1 结算完成：成功 {result.TotalMapsCompleted}，失败 {result.TotalMapsFailed}，" +
                $"耗时 {watch.ElapsedMilliseconds} ms，哈希 {result.FinalHash[..12]}…");
        }
        catch (Exception exception)
        {
            ReportError("p1a.offline_benchmark_failed", "P1A offline benchmark failed.", exception);
        }
    }

    private void SaveP1State(bool showNotice)
    {
        if (_session is null || _saveRepository is null)
        {
            if (showNotice)
            {
                ShowNotice("尚未创建角色，没有可保存的 P1 状态。");
            }

            return;
        }

        try
        {
            _saveRepository.SaveP1SessionJson(JsonSerializer.Serialize(_session.Capture(), SaveJsonOptions));
            if (showNotice)
            {
                ShowNotice($"P1 状态已保存（Schema {_saveRepository.GetSchemaVersion()}）。");
            }
        }
        catch (Exception exception)
        {
            ReportError("p1a.save_failed", "P1A state save failed.", exception);
        }
    }

    private void TryInitializeSave(int slot)
    {
        try
        {
            _saveRepository = new SaveRepository(_savesRoot, slot);
            _saveRepository.Initialize();
            _saveRepository.CreateBackup();
            string? json = _saveRepository.LoadP1SessionJson();
            _session = string.IsNullOrWhiteSpace(json)
                ? null
                : P1GameSession.Restore(
                    JsonSerializer.Deserialize<P1GameSessionSnapshot>(json, SaveJsonOptions) ??
                    throw new InvalidDataException("P1 save JSON was empty."));
            SettleOfflineOnOpen();
        }
        catch (Exception exception)
        {
            _saveRepository?.Dispose();
            _saveRepository = null;
            _session = null;
            ReportError("p1a.save_initialize_failed", "Save initialization failed; no replacement save was created.", exception);
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
        P1OfflineResult? result = _session?.AdvanceOffline(elapsed.EffectiveMilliseconds);
        string intervalId = $"p1a-startup-{_activeSlot}-{lastObservedUtcMs}-{nowUtcMs}";
        _saveRepository.TryCommitOfflineSession(
            intervalId,
            lastObservedUtcMs,
            nowUtcMs,
            result?.TotalMapsCompleted + result?.TotalMapsFailed ?? 0,
            result is null ? "{}" : JsonSerializer.Serialize(result, SaveJsonOptions));
        if (_session is not null)
        {
            _saveRepository.SaveP1SessionJson(JsonSerializer.Serialize(_session.Capture(), SaveJsonOptions));
        }

        _logger?.Write(
            elapsed.ClockMovedBackward ? GameLogLevel.Warning : GameLogLevel.Information,
            "p1a.offline_startup_settled",
            "offline",
            "P1A startup offline interval was committed before presentation.",
            new Dictionary<string, object?>
            {
                ["effective_ms"] = elapsed.EffectiveMilliseconds,
                ["clock_moved_backward"] = elapsed.ClockMovedBackward,
                ["clamped"] = elapsed.WasClamped,
                ["completed_maps"] = result?.TotalMapsCompleted ?? 0,
                ["failed_maps"] = result?.TotalMapsFailed ?? 0,
            });
    }

    private void SwitchSaveSlot(int slot)
    {
        if (slot == _activeSlot)
        {
            return;
        }

        SaveP1State(showNotice: false);
        _saveRepository?.Dispose();
        _saveRepository = null;
        _activeSlot = slot;
        TryInitializeSave(slot);
        _dashboard?.SetSession(_session);
        ShowNotice($"已切换到存档槽 {_activeSlot}。");
    }

    private void CreateBackup()
    {
        try
        {
            SaveP1State(showNotice: false);
            string path = _saveRepository?.CreateBackup() ?? "存档未初始化";
            ShowNotice($"备份已创建：{path}");
        }
        catch (Exception exception)
        {
            ReportError("p1a.backup_failed", "Backup failed.", exception);
        }
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
        _closeDialog!.PopupCentered(new Vector2I(460, 200));
    }

    private void CompleteCloseChoice(bool closeToTray)
    {
        if (_rememberCloseChoice!.ButtonPressed && _settingsStore is not null)
        {
            _settingsStore.Save(_settingsStore.Load() with { CloseToTray = closeToTray });
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
        SaveP1State(showNotice: false);
        _quitting = true;
        GetTree().Quit();
    }

    private void OpenLogs()
    {
        _ = OS.ShellOpen(Path.Combine(ProjectSettings.GlobalizePath("user://"), "logs"));
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

    private void UpdateTrayState()
    {
        TrayStatus status = _battlePaused
            ? TrayStatus.Paused
            : _session?.World.Teams.All(team => team.IsStopped || team.Queue.Count == 0 && team.ActiveMap is null) == true
                ? TrayStatus.Waiting
                : TrayStatus.Normal;
        _windowController?.SetTrayStatus(status);
    }

    private void ReportError(string eventId, string message, Exception exception)
    {
        _logger?.Write(GameLogLevel.Error, eventId, "p1a", message, exception: exception);
        _windowController?.SetTrayStatus(TrayStatus.Stopped);
        ShowNotice($"错误：{message} {exception.Message}");
    }

    private void ShowNotice(string message)
    {
        if (_noticeLabel is not null)
        {
            _noticeLabel.Text = message;
        }

        _logger?.Write(GameLogLevel.Information, "p1a.notice", "ui", message);
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
        _logger?.Write(
            GameLogLevel.Error,
            "application.unobserved_task",
            "application",
            "Unobserved task exception.",
            exception: eventArgs.Exception);
        eventArgs.SetObserved();
    }

    private static Button AddButton(Container parent, string text, Action action)
    {
        var button = new Button { Text = text };
        button.Pressed += action;
        parent.AddChild(button);
        return button;
    }

    private static void AddMiniButton(Container parent, string text, string tooltip, float width, Action action)
    {
        var button = new Button
        {
            Text = text,
            TooltipText = tooltip,
            CustomMinimumSize = new Vector2(width, 30),
        };
        button.AddThemeFontSizeOverride("font_size", 13);
        button.Pressed += action;
        parent.AddChild(button);
    }

    private static void AddDragButton(Container parent)
    {
        bool dragging = false;
        var button = new Button
        {
            Text = "拖",
            TooltipText = "拖动小窗",
            CustomMinimumSize = new Vector2(42, 30),
        };
        button.AddThemeFontSizeOverride("font_size", 13);
        button.ButtonDown += () => dragging = true;
        button.ButtonUp += () => dragging = false;
        button.GuiInput += inputEvent =>
        {
            if (!dragging || !Input.IsMouseButtonPressed(MouseButton.Left) ||
                inputEvent is not InputEventMouseMotion motion)
            {
                return;
            }

            float scale = Math.Max(1f, DisplayServer.ScreenGetScale(DisplayServer.WindowGetCurrentScreen()));
            var physicalDelta = new Vector2I(
                (int)Math.Round(motion.Relative.X * scale),
                (int)Math.Round(motion.Relative.Y * scale));
            DisplayServer.WindowSetPosition(DisplayServer.WindowGetPosition() + physicalDelta);
        };
        parent.AddChild(button);
    }
}
