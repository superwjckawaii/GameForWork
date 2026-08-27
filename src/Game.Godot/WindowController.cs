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
    private int _statusIndicatorId = -1;
    private Rid _trayMenu;

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
    public bool IsHiddenToTray { get; private set; }

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

        _window.Hide();
        IsHiddenToTray = true;
    }

    public void Restore()
    {
        if (DisplayServer.GetName() == "headless")
        {
            return;
        }

        _window.Show();
        IsHiddenToTray = false;
        EnsureVisible();
        DisplayServer.WindowMoveToForeground();
    }

    public void ToggleAlwaysOnTop()
    {
        _window.AlwaysOnTop = !_window.AlwaysOnTop;
        _settings = _settings with { AlwaysOnTop = _window.AlwaysOnTop };
        _settingsStore.Save(_settings);
    }

    public void SetOpacity(int percent)
    {
        int clamped = Math.Clamp(percent, 70, 100);
        ApplyOpacity(clamped);
        _settings = _settings with { OpacityPercent = clamped };
        _settingsStore.Save(_settings);
    }

    public void SetTrayStatus(TrayStatus status)
    {
        if (_statusIndicatorId < 0)
        {
            return;
        }

        Color color = status switch
        {
            TrayStatus.Normal => new Color("4d9fd1"),
            TrayStatus.Waiting => new Color("d8af48"),
            TrayStatus.Stopped => new Color("d45c57"),
            TrayStatus.Paused => new Color("777d88"),
            _ => throw new ArgumentOutOfRangeException(nameof(status)),
        };
        DisplayServer.StatusIndicatorSetIcon(_statusIndicatorId, CreateSolidIcon(color));
        DisplayServer.StatusIndicatorSetTooltip(_statusIndicatorId, $"GameForWork P0 - {status}");
    }

    public void TickSnapping()
    {
        if (DisplayServer.GetName() == "headless" || IsHiddenToTray)
        {
            return;
        }

        Vector2I position = DisplayServer.WindowGetPosition();
        if (position == _lastObservedPosition)
        {
            return;
        }

        Rect2I usable = DisplayServer.ScreenGetUsableRect(DisplayServer.WindowGetCurrentScreen());
        Vector2I size = DisplayServer.WindowGetSizeWithDecorations();
        int x = Snap(position.X, usable.Position.X, usable.End.X - size.X);
        int y = Snap(position.Y, usable.Position.Y, usable.End.Y - size.Y);
        var snapped = new Vector2I(x, y);
        if (snapped != position)
        {
            DisplayServer.WindowSetPosition(snapped);
        }

        _lastObservedPosition = snapped;
        if (!IsMini)
        {
            _standardPosition = snapped;
        }
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
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, true);
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.ResizeDisabled, true);
        DisplayServer.WindowSetSize(MiniSize);
        EnsureVisible();
    }

    private void EnterStandard()
    {
        IsMini = false;
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, false);
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.ResizeDisabled, false);
        DisplayServer.WindowSetSize(StandardSize);
        DisplayServer.WindowSetPosition(_standardPosition);
        EnsureVisible();
    }

    private void EnsureVisible()
    {
        Rect2I usable = DisplayServer.ScreenGetUsableRect(DisplayServer.WindowGetCurrentScreen());
        Vector2I size = DisplayServer.WindowGetSizeWithDecorations();
        Vector2I position = DisplayServer.WindowGetPosition();
        int maxX = Math.Max(usable.Position.X, usable.End.X - size.X);
        int maxY = Math.Max(usable.Position.Y, usable.End.Y - size.Y);
        DisplayServer.WindowSetPosition(new Vector2I(
            Math.Clamp(position.X, usable.Position.X, maxX),
            Math.Clamp(position.Y, usable.Position.Y, maxY)));
    }

    private void CreateTray()
    {
        ImageTexture texture = CreateSolidIcon(new Color("4d9fd1"));
        _trayMenu = NativeMenu.CreateMenu();
        NativeMenu.AddItem(_trayMenu, "显示窗口", Callable.From(Restore));
        NativeMenu.AddItem(_trayMenu, "暂停/继续战斗", Callable.From(_togglePause));
        NativeMenu.AddItem(_trayMenu, "打开日志目录", Callable.From(_openLogs));
        NativeMenu.AddSeparator(_trayMenu);
        NativeMenu.AddItem(_trayMenu, "退出", Callable.From(_quit));
        _statusIndicatorId = DisplayServer.CreateStatusIndicator(
            texture,
            "GameForWork P0",
            Callable.From<int, Vector2I>((_, _) => Restore()));
        DisplayServer.StatusIndicatorSetMenu(_statusIndicatorId, _trayMenu);
    }

    private static ImageTexture CreateSolidIcon(Color color)
    {
        Image image = Image.CreateEmpty(16, 16, false, Image.Format.Rgba8);
        image.Fill(color);
        return ImageTexture.CreateFromImage(image);
    }

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

        long handleValue = unchecked((long)DisplayServer.WindowGetNativeHandle(DisplayServer.HandleType.WindowHandle));
        var handle = new IntPtr(handleValue);
        nint style = GetWindowLongPtr(handle, GwlExStyle);
        _ = SetWindowLongPtr(handle, GwlExStyle, style | WsExLayered);
        _ = SetLayeredWindowAttributes(handle, 0, checked((byte)(255 * percent / 100)), LwaAlpha);
    }

    private const int GwlExStyle = -20;
    private const nint WsExLayered = 0x00080000;
    private const uint LwaAlpha = 0x00000002;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern nint GetWindowLongPtr(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr(IntPtr window, int index, nint newLong);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetLayeredWindowAttributes(IntPtr window, uint colorKey, byte alpha, uint flags);
}
