using System.Runtime.InteropServices;
using GameForWork.Core.Persistence;
using Godot;

namespace GameForWork.GodotClient;

public enum TrayStatus
{
    Normal,
    Waiting,
    Stopped,
    Paused,
}

public sealed class WindowController : IDisposable
{
    private static readonly Vector2I StandardSize = new(960, 640);
    private static readonly Vector2I LargeSize = new(1920, 1280);
    private static readonly Vector2I MiniSize = new(384, 216);
    private const int SnapDistance = 12;
    private readonly Window _window;
    private readonly SettingsStore _settingsStore;
    private readonly Action _togglePause;
    private readonly Action _openLogs;
    private readonly Action _quit;
    private GameSettings _settings;
    private Vector2I _standardPosition;
    private Vector2I _lastObservedPosition;
    private double _stationarySeconds;
    private bool _snapAppliedForCurrentPosition;
    private int _statusIndicatorId = -1;
    private Rid _trayMenu;
    private bool _globalHotkeyWasPressed;

    public WindowController(
        Window window,
        SettingsStore settingsStore,
        Action togglePause,
        Action openLogs,
        Action quit)
    {
        _window = window;
        _settingsStore = settingsStore;
        _togglePause = togglePause;
        _openLogs = openLogs;
        _quit = quit;
        _settings = settingsStore.Load();
        _standardPosition = _settings.StandardX >= 0
            ? new Vector2I(_settings.StandardX, _settings.StandardY)
            : DisplayServer.WindowGetPosition();
    }

    public bool IsMini { get; private set; }
    public bool IsLarge { get; private set; }
    public bool IsHiddenToTray { get; private set; }
    public bool SnapEnabled => _settings.SnapEnabled;
    public bool AlwaysOnTop => _settings.AlwaysOnTop;
    public bool CanUseLarge => DisplayServer.GetName() != "headless" &&
        DisplayServer.ScreenGetUsableRect(DisplayServer.WindowGetCurrentScreen()).Size.X >= LargeSize.X &&
        DisplayServer.ScreenGetUsableRect(DisplayServer.WindowGetCurrentScreen()).Size.Y >= LargeSize.Y;

    public void Initialize()
    {
        if (DisplayServer.GetName() == "headless")
        {
            return;
        }

        _window.AlwaysOnTop = _settings.AlwaysOnTop;
        ApplyOpacity(_settings.OpacityPercent);
        CreateTray();
        if (_settings.StartMini)
        {
            EnterMini();
        }
        else
        {
            EnterStandard();
        }

        EnsureVisible();
        _lastObservedPosition = DisplayServer.WindowGetPosition();
    }

    public void ToggleMode()
    {
        if (IsMini)
        {
            EnterStandard();
        }
        else
        {
            EnterMini();
        }
    }

    public void ToggleLarge()
    {
        if (IsLarge)
        {
            EnterStandard();
        }
        else if (CanUseLarge)
        {
            EnterLarge();
        }
    }

    public void HideToTray()
    {
        if (DisplayServer.GetName() == "headless")
        {
            return;
        }

        if (!IsMini)
        {
            _standardPosition = DisplayServer.WindowGetPosition();
        }

        if (OperatingSystem.IsWindows())
        {
            _ = ShowWindow(GetNativeWindowHandle(), SwHide);
        }
        else
        {
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Minimized);
        }
        IsHiddenToTray = true;
    }

    public void Restore()
    {
        if (DisplayServer.GetName() == "headless")
        {
            return;
        }

        if (OperatingSystem.IsWindows())
        {
            _ = ShowWindow(GetNativeWindowHandle(), SwShow);
        }
        else
        {
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
        }
        IsHiddenToTray = false;
        EnsureVisible();
        DisplayServer.WindowMoveToForeground();
    }

    public void ToggleAlwaysOnTop()
    {
        SetAlwaysOnTop(!AlwaysOnTop);
    }

    public void SetAlwaysOnTop(bool enabled)
    {
        if (DisplayServer.GetName() != "headless")
        {
            _window.AlwaysOnTop = enabled;
        }

        _settings = _settings with { AlwaysOnTop = enabled };
        _settingsStore.Save(_settings);
    }

    public void SetOpacity(int percent)
    {
        int clamped = Math.Clamp(percent, 70, 100);
        ApplyOpacity(clamped);
        _settings = _settings with { OpacityPercent = clamped };
        _settingsStore.Save(_settings);
    }

    public void SetSnapEnabled(bool enabled)
    {
        _settings = _settings with { SnapEnabled = enabled };
        _snapAppliedForCurrentPosition = false;
        _stationarySeconds = 0;
        _settingsStore.Save(_settings);
    }

    public void SetGlobalHotkeyEnabled(bool enabled)
    {
        _settings = _settings with { GlobalHotkeyEnabled = enabled };
        _globalHotkeyWasPressed = false;
        _settingsStore.Save(_settings);
    }

    public void SetTrayStatus(TrayStatus status)
    {
        if (_statusIndicatorId < 0)
        {
            return;
        }

        DisplayServer.StatusIndicatorSetIcon(_statusIndicatorId, TrayIcon(status));
        DisplayServer.StatusIndicatorSetTooltip(_statusIndicatorId, $"暗门远征 · {TrayStatusText(status)}");
    }

    public void TickSnapping(double delta)
    {
        TickGlobalHotkey();
        if (DisplayServer.GetName() == "headless" || IsHiddenToTray)
        {
            return;
        }

        Vector2I position = DisplayServer.WindowGetPosition();
        if (!SnapEnabled)
        {
            _lastObservedPosition = position;
            if (!IsMini)
            {
                _standardPosition = position;
            }

            return;
        }

        if (position != _lastObservedPosition)
        {
            _lastObservedPosition = position;
            _stationarySeconds = 0;
            _snapAppliedForCurrentPosition = false;
            if (!IsMini)
            {
                _standardPosition = position;
            }

            return;
        }

        if (_snapAppliedForCurrentPosition)
        {
            return;
        }

        _stationarySeconds += delta;
        if (_stationarySeconds < 0.18)
        {
            return;
        }

        Rect2I usable = DisplayServer.ScreenGetUsableRect(DisplayServer.WindowGetCurrentScreen());
        Vector2I decoratedPosition = DisplayServer.WindowGetPositionWithDecorations();
        Vector2I decorationOffset = position - decoratedPosition;
        Vector2I size = DisplayServer.WindowGetSizeWithDecorations();
        int x = Snap(decoratedPosition.X, usable.Position.X, usable.End.X - size.X);
        int y = Snap(decoratedPosition.Y, usable.Position.Y, usable.End.Y - size.Y);
        Vector2I snapped = new Vector2I(x, y) + decorationOffset;
        if (snapped != position)
        {
            DisplayServer.WindowSetPosition(snapped);
            _lastObservedPosition = snapped;
        }

        _snapAppliedForCurrentPosition = true;
        if (!IsMini)
        {
            _standardPosition = snapped;
        }
    }

    private void TickGlobalHotkey()
    {
        if (!_settings.GlobalHotkeyEnabled || !OperatingSystem.IsWindows() || DisplayServer.GetName() == "headless")
        {
            return;
        }

        bool pressed = IsKeyDown(VkControl) && IsKeyDown(VkMenu) && IsKeyDown(VkH);
        if (pressed && !_globalHotkeyWasPressed)
        {
            if (IsHiddenToTray)
            {
                Restore();
            }
            else
            {
                HideToTray();
            }
        }

        _globalHotkeyWasPressed = pressed;
    }

    public void Dispose()
    {
        if (!IsMini && !IsHiddenToTray && DisplayServer.GetName() != "headless")
        {
            _standardPosition = DisplayServer.WindowGetPosition();
        }

        _settings = _settings with { StandardX = _standardPosition.X, StandardY = _standardPosition.Y, StartMini = IsMini };
        _settingsStore.Save(_settings);
        if (_statusIndicatorId >= 0)
        {
            DisplayServer.DeleteStatusIndicator(_statusIndicatorId);
        }

        if (_trayMenu.IsValid)
        {
            NativeMenu.FreeMenu(_trayMenu);
        }

        GC.SuppressFinalize(this);
    }

    private void EnterMini()
    {
        if (!IsMini)
        {
            _standardPosition = DisplayServer.WindowGetPosition();
        }

        IsMini = true;
        IsLarge = false;
        _window.ContentScaleSize = MiniSize;
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, true);
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.ResizeDisabled, true);
        DisplayServer.WindowSetSize(MiniSize);
        EnsureVisible();
    }

    private void EnterStandard()
    {
        IsMini = false;
        IsLarge = false;
        _window.ContentScaleSize = StandardSize;
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, true);
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.ResizeDisabled, false);
        DisplayServer.WindowSetSize(StandardSize);
        DisplayServer.WindowSetPosition(_standardPosition);
        EnsureVisible();
    }

    private void EnterLarge()
    {
        if (!CanUseLarge)
        {
            return;
        }

        IsMini = false;
        IsLarge = true;
        _window.ContentScaleSize = StandardSize;
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, true);
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.ResizeDisabled, true);
        DisplayServer.WindowSetSize(LargeSize);
        EnsureVisible();
    }

    private void EnsureVisible()
    {
        Rect2I usable = DisplayServer.ScreenGetUsableRect(DisplayServer.WindowGetCurrentScreen());
        Vector2I size = DisplayServer.WindowGetSizeWithDecorations();
        Vector2I position = DisplayServer.WindowGetPosition();
        Vector2I decoratedPosition = DisplayServer.WindowGetPositionWithDecorations();
        Vector2I decorationOffset = position - decoratedPosition;
        int maxX = Math.Max(usable.Position.X, usable.End.X - size.X);
        int maxY = Math.Max(usable.Position.Y, usable.End.Y - size.Y);
        var visibleDecoratedPosition = new Vector2I(
            Math.Clamp(decoratedPosition.X, usable.Position.X, maxX),
            Math.Clamp(decoratedPosition.Y, usable.Position.Y, maxY));
        DisplayServer.WindowSetPosition(visibleDecoratedPosition + decorationOffset);
    }

    private void CreateTray()
    {
        Texture2D texture = TrayIcon(TrayStatus.Normal);
        _trayMenu = NativeMenu.CreateMenu();
        NativeMenu.AddItem(_trayMenu, "显示窗口", TrayAction(Restore));
        NativeMenu.AddItem(_trayMenu, "暂停/继续战斗", TrayAction(_togglePause));
        NativeMenu.AddItem(_trayMenu, "打开日志目录", TrayAction(_openLogs));
        NativeMenu.AddSeparator(_trayMenu);
        NativeMenu.AddItem(_trayMenu, "退出", TrayAction(_quit));
        _statusIndicatorId = DisplayServer.CreateStatusIndicator(
            texture,
            "暗门远征 · GameForWork",
            Callable.From<int, Vector2I>((_, _) => Restore()));
        DisplayServer.StatusIndicatorSetMenu(_statusIndicatorId, _trayMenu);
    }

    private static Callable TrayAction(Action action) => Callable.From<Variant>(_ => action());

    private static ImageTexture CreateSolidIcon(Color color)
    {
        Image image = Image.CreateEmpty(16, 16, false, Image.Format.Rgba8);
        image.Fill(color);
        return ImageTexture.CreateFromImage(image);
    }

    private static Texture2D TrayIcon(TrayStatus status)
    {
        string name = status switch
        {
            TrayStatus.Normal => "normal",
            TrayStatus.Waiting => "waiting",
            TrayStatus.Stopped => "error",
            TrayStatus.Paused => "paused",
            _ => throw new ArgumentOutOfRangeException(nameof(status)),
        };
        string path = $"res://assets/p21/brand/p21-tray-{name}.png";
        return ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : CreateSolidIcon(new Color("4d9fd1"));
    }

    private static string TrayStatusText(TrayStatus status) => status switch
    {
        TrayStatus.Normal => "运行中",
        TrayStatus.Waiting => "等待冒险资源",
        TrayStatus.Stopped => "发生错误",
        TrayStatus.Paused => "战斗已暂停",
        _ => status.ToString(),
    };

    private static int Snap(int value, int minimum, int maximum)
    {
        if (Math.Abs(value - minimum) <= SnapDistance)
        {
            return minimum;
        }

        return Math.Abs(value - maximum) <= SnapDistance ? maximum : value;
    }

    private static void ApplyOpacity(int percent)
    {
        if (!OperatingSystem.IsWindows() || DisplayServer.GetName() == "headless")
        {
            return;
        }

        IntPtr handle = GetNativeWindowHandle();
        nint style = GetWindowLongPtr(handle, GwlExStyle);
        _ = SetWindowLongPtr(handle, GwlExStyle, style | WsExLayered);
        _ = SetLayeredWindowAttributes(handle, 0, checked((byte)(255 * percent / 100)), LwaAlpha);
    }

    private const int GwlExStyle = -20;
    private const nint WsExLayered = 0x00080000;
    private const uint LwaAlpha = 0x00000002;
    private const int VkControl = 0x11;
    private const int VkMenu = 0x12;
    private const int VkH = 0x48;
    private const int SwHide = 0;
    private const int SwShow = 5;

    private static bool IsKeyDown(int key) => (GetAsyncKeyState(key) & 0x8000) != 0;

    private static IntPtr GetNativeWindowHandle()
    {
        long handleValue = unchecked((long)DisplayServer.WindowGetNativeHandle(DisplayServer.HandleType.WindowHandle));
        return new IntPtr(handleValue);
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern nint GetWindowLongPtr(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr(IntPtr window, int index, nint newLong);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetLayeredWindowAttributes(IntPtr window, uint colorKey, byte alpha, uint flags);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr window, int command);
}
