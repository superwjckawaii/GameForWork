using Godot;

namespace GameForWork.GodotClient;

/// <summary>In-theme title bar for native secondary windows.</summary>
public partial class SecondaryTitleBar : PanelContainer
{
    private Window? _target;
    private string _title = string.Empty;
    private Action? _close;

    public void Initialize(Window target, string title, Action close)
    {
        _target = target;
        _title = title;
        _close = close;
    }

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(0, 32);
        AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("171b24"),
            BorderColor = new Color("8f7043"),
            BorderWidthBottom = 2,
            ContentMarginLeft = 5,
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
            Text = _title,
            Flat = true,
            FocusMode = FocusModeEnum.None,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Alignment = HorizontalAlignment.Left,
            MouseDefaultCursorShape = CursorShape.Move,
        };
        drag.GuiInput += inputEvent =>
        {
            if (inputEvent is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } &&
                _target is { Visible: true })
            {
                DisplayServer.WindowStartDrag(_target.GetWindowId());
            }
        };
        row.AddChild(drag);

        AddTitleButton(row, "↘", "拖动调整窗口大小", () =>
        {
            if (_target is { Visible: true })
                DisplayServer.WindowStartResize(DisplayServer.WindowResizeEdge.BottomRight, _target.GetWindowId());
        });
        AddTitleButton(row, "×", "关闭角色与物品", () => _close?.Invoke(), danger: true);
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
        if (danger) button.AddThemeColorOverride("font_hover_color", new Color("ff8a78"));
        button.Pressed += action;
        parent.AddChild(button);
    }
}
