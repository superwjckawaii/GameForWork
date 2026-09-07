using Godot;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Environment = System.Environment;
using GameForWork.GodotClient;
using GameForWork.Core.Ascendancies;
using GameForWork.Core.Campaign;
using GameForWork.Core.Campaign.Progression;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Characters;
using GameForWork.Core.Management;
using GameForWork.Core.Release;

// Diagnostic-only adapter: keep reflection and replay control out of production.
public partial class Baseline : Node
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly record struct Case(string Scenario, int Width, int Height, EffectDensity Density,
        int Round, double Warmup, double Capture);
    private readonly List<Case> _cases = [];
    private readonly List<object> _reports = [];
    private readonly List<double> _frames = [];
    private readonly List<object> _trace = [];
    private readonly List<object> _sequence = [];
    private Main _client = null!;
    private Dashboard _dashboard = null!;
    private WindowController _window = null!;
    private GameSession _session = null!;
    private MapRunResult _run = null!;
    private int _caseIndex, _videoFrame;
    private long _start, _lastFrame, _segmentStart, _segmentLength, _workingSet, _renderedStart;
    private double _uiPeak, _savePeak, _cpuStart;
    private int[] _gcStart = [];
    private bool _sampling, _recording, _capturing, _singleCase, _finished;
    private string _scenario = "";
    private double _captureStarted;
    private static string Output(string name) => ProjectSettings.GlobalizePath("res://" + name);
    private static T Field<T>(object instance, string name) =>
        (T)(instance.GetType().GetField(name, PrivateInstance)?.GetValue(instance)
            ?? throw new InvalidOperationException($"Missing diagnostic field {name}"));
    private static void SetField(object instance, string name, object value) =>
        (instance.GetType().GetField(name, PrivateInstance)
            ?? throw new InvalidOperationException($"Missing diagnostic field {name}")).SetValue(instance, value);

    public override void _Ready()
    {
        bool smoke = OS.GetCmdlineUserArgs().Contains("--baseline-smoke");
        bool exhaustive = OS.GetCmdlineUserArgs().Contains("--baseline-exhaustive");
        string? selectedCase = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--baseline-case="));
        foreach (string scenario in new[] { "normal", "dense" })
        {
            foreach (var size in new[] { (384, 216), (960, 640), (1920, 1280) })
            foreach (EffectDensity density in Enum.GetValues<EffectDensity>())
            {
                if (size.Item1 == 384 && density != EffectDensity.Low) continue;
                for (int round = 1; round <= (exhaustive ? 3 : 1); round++)
                    _cases.Add(new(scenario, size.Item1, size.Item2, density, round,
                        smoke ? 1 : exhaustive ? 30 : 5, smoke ? 2 : exhaustive ? 120 : 20));
            }
            if (!smoke && !exhaustive)
                for (int round = 1; round <= 3; round++)
                    _cases.Add(new(scenario, 1920, 1280, EffectDensity.High, round, 30, 120));
        }
        if (selectedCase is not null)
        {
            _caseIndex = int.Parse(selectedCase.Split('=')[1]);
            if (_caseIndex < 0 || _caseIndex >= _cases.Count) throw new ArgumentOutOfRangeException(nameof(selectedCase));
            _singleCase = true;
        }
        _client = new Main();
        AddChild(_client);
        _dashboard = Field<Dashboard>(_client, "_dashboard");
        _window = Field<WindowController>(_client, "_windowController");
        _window.SetAlwaysOnTop(false);
        GetWindow().GuiDisableInput = true;
        GetWindow().Title = "Visual baseline — non-interactive capture";
        File.WriteAllText(Output("environment.json"), JsonSerializer.Serialize(new {
            Engine = Engine.GetVersionInfo().ToString(), Adapter = RenderingServer.GetVideoAdapterName(),
            AdapterVendor = RenderingServer.GetVideoAdapterVendor(), Display = DisplayServer.GetName(),
            Dpi = DisplayServer.ScreenGetDpi(), Scale = DisplayServer.ScreenGetScale(),
            Processor = OS.GetProcessorName(), LogicalProcessors = Environment.ProcessorCount,
            OS = OS.GetDistributionName(), StartedUtc = DateTime.UtcNow,
            Mode = smoke ? "smoke" : exhaustive ? "exhaustive" : "representative",
            SaveRoot = Field<string>(_client, "_savesRoot"), Cases = _cases.Count, SingleCase = _singleCase
        }, new JsonSerializerOptions { WriteIndented = true }));
        ApplyCase();
    }

    private void ApplyCase()
    {
        Case c = _cases[_caseIndex];
        if (_scenario != c.Scenario)
        {
            _scenario = c.Scenario;
            _session = GameSession.CreateNew(new PlayerIdentity("Visual Baseline", CharacterGender.Androgynous,
                CharacterSkinTone.Fair, CharacterHairStyle.Cropped, BaseClass.Fighter), 20260907, false);
            _session = GameSession.Restore(_session.Capture() with { Campaign = CampaignState.CreateLegacyCompleted().Capture() });
            string buildId = "core.benchmark.breaker." + (_scenario == "dense" ? "endgame" : "entry");
            var definition = WarriorBenchmarkBuilds.All.Single(b => b.StableId == buildId);
            var build = (TeamBuild)typeof(ReleaseTargets).GetMethod("CreateCombatBuild", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, [definition])!;
            if (_scenario == "dense") _session.World.Hero.Progression.UnlockFinalBreakthrough();
            _session.World.Hero.Progression.MigrateToMinimumLevel(build.Sheet.Level);
            _session.World.Hero.UpdateBuild(build);
            var map = new MapItem("visual-baseline-" + _scenario, _scenario == "dense" ? 20 : 1).EnsureFormal(20260907);
            _run = new MapRunner(new MapAttemptResolver()).Run(map, _scenario == "dense" ? MapRoute.Abyss : MapRoute.Safe, build, 20260907);
            var timeline = _run.Attempts[0].Timeline!;
            var segment = timeline.Encounters.OrderByDescending(e =>
                timeline.SpatialFrames?.Where(f => f.AtMilliseconds >= e.StartMilliseconds &&
                    f.AtMilliseconds < e.StartMilliseconds + e.DurationMilliseconds).Select(f => f.Enemies.Count).DefaultIfEmpty().Max() ?? 0)
                .ThenByDescending(e => e.DurationMilliseconds).First();
            _segmentStart = segment.StartMilliseconds;
            _segmentLength = Math.Max(1, segment.DurationMilliseconds - 100);
            _session.World.Hero.StartMap(_run.Map, _run.Route, _run.DurationMilliseconds, _run);
            SetField(_client, "_session", _session);
            _dashboard.SetSession(_session);
            File.WriteAllText(Output("fixture-" + _scenario + ".json"), JsonSerializer.Serialize(new {
                Seed = 20260907, BuildId = buildId, Build = build, Map = _run.Map, Route = _run.Route,
                TimelineHash = timeline.FinalHash, SegmentStartMs = _segmentStart, SegmentLengthMs = _segmentLength,
                MaximumEnemies = timeline.SpatialFrames?.Max(f => f.Enemies.Count),
                Events = timeline.Events.Count, SpatialFrames = timeline.SpatialFrames?.Count
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        if (_window.IsMini != (c.Width == 384)) _window.ToggleMode();
        if (_window.IsLarge != (c.Width == 1920)) _window.ToggleLarge();
        typeof(Main).GetMethod("UpdateWindowModeInterface", PrivateInstance)!.Invoke(_client, null);
        GetWindow().Size = new Vector2I(c.Width, c.Height);
        GetWindow().Position = Vector2I.Zero;
        VisualPreferences.EffectDensity = c.Density;
        _sampling = false; _recording = false; _videoFrame = 0;
        _frames.Clear(); _trace.Clear(); _sequence.Clear();
        _start = _lastFrame = Stopwatch.GetTimestamp();
        GD.Print($"BASELINE_CASE {_caseIndex + 1}/{_cases.Count} {c}");
    }

    public override void _Process(double delta)
    {
        if (_run is null || _finished) return;
        Case c = _cases[_caseIndex];
        if (GetWindow().Size != new Vector2I(c.Width, c.Height) || _window.IsHiddenToTray || GetWindow().Mode == Window.ModeEnum.Minimized)
        {
            GD.PushError($"Baseline window changed during case {_caseIndex}; this sample is invalid.");
            _finished = true;
            GetTree().Quit(1);
            return;
        }
        if (_capturing) return;
        long now = Stopwatch.GetTimestamp();
        double frameMs = Stopwatch.GetElapsedTime(_lastFrame, now).TotalMilliseconds;
        _lastFrame = now;
        double elapsed = Stopwatch.GetElapsedTime(_start, now).TotalSeconds;
        _session.World.Hero.SaveRemainingMapTime(Math.Max(100, _run.DurationMilliseconds -
            (_segmentStart + (long)(elapsed * 1000) % _segmentLength)));
        if (_recording)
        {
            // Separate sequential images; capture overhead never enters timing samples.
            CaptureImage($"sequence-{_scenario}-{_videoFrame++:D3}.webp", sequence: true);
            return;
        }
        if (elapsed < c.Warmup) return;
        using var process = Process.GetCurrentProcess();
        if (!_sampling)
        {
            _sampling = true; _captureStarted = elapsed;
            _renderedStart = Engine.GetFramesDrawn();
            SetField(_client, "_peakSimulationMilliseconds", 0d);
            _cpuStart = process.TotalProcessorTime.TotalSeconds;
            _workingSet = process.WorkingSet64;
            _uiPeak = _savePeak = 0;
            _gcStart = Enumerable.Range(0, 3).Select(GC.CollectionCount).ToArray();
            return;
        }
        _frames.Add(frameMs);
        double simulation = Field<double>(_client, "_lastSimulationMilliseconds");
        double ui = _dashboard.LastRefreshMilliseconds;
        long save = Field<long>(_client, "_lastSaveMilliseconds");
        _uiPeak = Math.Max(_uiPeak, ui); _savePeak = Math.Max(_savePeak, save);
        _workingSet = Math.Max(_workingSet, process.WorkingSet64);
        _trace.Add(new { AtSeconds = elapsed, FrameMs = frameMs, SimulationMs = simulation, UiMs = ui, SaveMs = save,
            Gc = Enumerable.Range(0, 3).Select(GC.CollectionCount).ToArray() });
        if (elapsed - _captureStarted < c.Capture) return;
        var values = _frames.Order().ToArray();
        var root = Field<VBoxContainer>(_client, "_interfaceRoot");
        var worldView = Field<WorldView>(_dashboard, "_worldView");
        GD.Print($"LAYOUT root={root.Size} visible={root.IsVisibleInTree()} position={root.GlobalPosition} view={GetViewport().GetVisibleRect()} drawCalls={RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalDrawCallsInFrame)}");
        var report = new { CaseIndex = _caseIndex, c.Scenario, c.Width, c.Height, Density = c.Density.ToString(), c.Round, c.Warmup, c.Capture,
            Frames = values.Length, MeanMs = values.Average(), Quantile95Ms = values[(int)((values.Length - 1) * .95)],
            Quantile99Ms = values[(int)((values.Length - 1) * .99)], MaxMs = values[^1],
            LongFrames = values.Count(v => v > 40), SimulationPeakMs = Field<double>(_client, "_peakSimulationMilliseconds"), UiPeakMs = _uiPeak,
            SavePeakMs = _savePeak, WorkingSetPeakBytes = _workingSet,
            CpuPercent = (process.TotalProcessorTime.TotalSeconds - _cpuStart) / (elapsed - _captureStarted) / Environment.ProcessorCount * 100,
            GcCollections = Enumerable.Range(0, 3).Select(i => GC.CollectionCount(i) - _gcStart[i]).ToArray(),
            ActualWidth = GetWindow().Size.X, ActualHeight = GetWindow().Size.Y, FrameCap = Engine.MaxFps,
            LogicalCanvasWidth = root.Size.X, LogicalCanvasHeight = root.Size.Y,
            CombatViewVisible = worldView.IsVisibleInTree(), CombatViewWidth = worldView.Size.X,
            Focused = DisplayServer.WindowIsFocused(), RenderedFrames = Engine.GetFramesDrawn() - _renderedStart };
        _reports.Add(report);
        File.WriteAllText(Output($"frames-{_caseIndex:D2}.json"), JsonSerializer.Serialize(_trace));
        File.WriteAllText(Output("baseline.json"), JsonSerializer.Serialize(_reports, new JsonSerializerOptions { WriteIndented = true }));
        GD.Print(JsonSerializer.Serialize(report));
        CaptureImage($"case-{_caseIndex:D2}.webp", sequence: false);
    }

    private async void CaptureImage(string name, bool sequence)
    {
        _capturing = true;
        try
        {
            await WaitForDraw().WaitAsync(TimeSpan.FromSeconds(5));
            double capturedAt = Stopwatch.GetElapsedTime(_start).TotalSeconds;
            using var picture = GetViewport().GetTexture().GetImage();
            if (picture.SaveWebp(Output(name)) != Error.Ok) throw new IOException("Image capture failed.");
            _capturing = false;
            Case c = _cases[_caseIndex];
            if (sequence)
            {
                _sequence.Add(new { Frame = _videoFrame - 1, AtSeconds = capturedAt });
                if (_videoFrame == 30)
                {
                    File.WriteAllText(Output($"sequence-{_scenario}.json"), JsonSerializer.Serialize(_sequence));
                    NextCase();
                }
            }
            else if (c.Width == 1920 && c.Density == EffectDensity.High &&
                (c.Round == 3 || OS.GetCmdlineUserArgs().Contains("--baseline-smoke"))) _recording = true;
            else NextCase();
        }
        catch (Exception exception)
        {
            GD.PushError($"Baseline capture failed: {exception}");
            _finished = true;
            GetTree().Quit(1);
        }
        async Task WaitForDraw() => await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
    }

    private void NextCase()
    {
        if (_singleCase || ++_caseIndex == _cases.Count) { _finished = true; GD.Print("BASELINE_COMPLETE"); GetTree().Quit(); }
        else ApplyCase();
    }
}
