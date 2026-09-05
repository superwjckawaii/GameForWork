using Godot;

namespace GameForWork.GodotClient;

public partial class PixelTitleBar : PanelContainer
{
    private Action? _toggleSize;
    private Action? _togglePin;
    private Action? _showInformation;
    private Action? _minimize;
    private Action? _hide;
    private Action? _close;

    public void Initialize(Action toggleSize, Action togglePin, Action showInformation, Action minimize, Action hide, Action close)
    {
        _toggleSize = toggleSize;
        _togglePin = togglePin;
        _showInformation = showInformation;
        _minimize = minimize;
        _hide = hide;
        _close = close;
    }

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(0, 30);
        AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("171b24"),
            BorderColor = new Color("8f7043"),
            BorderWidthBottom = 2,
            ContentMarginLeft = 3,
            ContentMarginRight = 3,
            ContentMarginTop = 2,
            ContentMarginBottom = 2,
        });
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 2);
        AddChild(row);
        const string iconPath = "res://assets/art/brand/art-app-icon.png";
        if (ResourceLoader.Exists(iconPath))
        {
            row.AddChild(new TextureRect
            {
                Texture = GD.Load<Texture2D>(iconPath),
                CustomMinimumSize = new Vector2(24, 24),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = MouseFilterEnum.Ignore,
            });
        }
        var drag = new Button
        {
            Text = "暗门远征 · GameForWork",
            Flat = true,
            FocusMode = FocusModeEnum.None,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Alignment = HorizontalAlignment.Left,
            MouseDefaultCursorShape = CursorShape.Move,
        };
        drag.GuiInput += inputEvent =>
        {
            if (inputEvent is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mouse)
            {
                return;
            }

            if (mouse.DoubleClick)
            {
                _toggleSize?.Invoke();
            }
            else
            {
                DisplayServer.WindowStartDrag();
            }
        };
        row.AddChild(drag);
        AddTitleButton(row, "?", "游戏信息与指引", () => _showInformation?.Invoke());
        AddTitleButton(row, "◆", "置顶 / 取消置顶", () => _togglePin?.Invoke());
        AddTitleButton(row, "—", "最小化", () => _minimize?.Invoke());
        AddTitleButton(row, "▾", "隐藏到托盘", () => _hide?.Invoke());
        AddTitleButton(row, "□", "标准/大窗口", () => _toggleSize?.Invoke());
        AddTitleButton(row, "↘", "拖动调整窗口大小", () =>
            DisplayServer.WindowStartResize(DisplayServer.WindowResizeEdge.BottomRight));
        AddTitleButton(row, "×", "关闭", () => _close?.Invoke(), danger: true);
    }

    private static void AddTitleButton(Container parent, string text, string tooltip, Action action, bool danger = false)
    {
        var button = new Button
        {
            Text = text,
            TooltipText = tooltip,
            CustomMinimumSize = new Vector2(34, 24),
            FocusMode = FocusModeEnum.None,
        };
        if (danger)
        {
            button.AddThemeColorOverride("font_hover_color", new Color("ff8a78"));
        }

        button.Pressed += action;
        parent.AddChild(button);
    }
}
