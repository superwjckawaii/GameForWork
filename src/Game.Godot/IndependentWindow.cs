using Godot;

namespace GameForWork.GodotClient;

public partial class IndependentWindow : Window
{
    private bool _dragging;

    protected VBoxContainer InitializePixelWindow(string title, Vector2I size, Vector2I minimumSize)
    {
        Hide();
        Title = title;
        Size = size;
        MinSize = minimumSize;
        Exclusive = false;
        Transient = false;
        ForceNative = true;
        Borderless = true;
        Unresizable = false;
        CloseRequested += Hide;

        var frame = new PanelContainer();
        frame.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        frame.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("101721"),
            BorderColor = new Color("8d7952"),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
        });
        AddChild(frame);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 0);
        frame.AddChild(root);

        var titleBar = new HBoxContainer { CustomMinimumSize = new Vector2(0, 34) };
        titleBar.AddThemeConstantOverride("separation", 2);
        titleBar.GuiInput += HandleTitleInput;
        var titleLabel = new Label
        {
            Name = "PixelWindowTitle",
            Text = $"◆ {title}",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center,
        };
        titleBar.AddChild(titleLabel);
        AddTitleButton(titleBar, "—", "最小化", () => Mode = ModeEnum.Minimized);
        AddTitleButton(titleBar, "□", "最大化/还原", () => Mode = Mode == ModeEnum.Maximized ? ModeEnum.Windowed : ModeEnum.Maximized);
        AddTitleButton(titleBar, "×", "关闭", Hide);
        root.AddChild(titleBar);
        root.AddChild(new HSeparator());

        var margin = new MarginContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        root.AddChild(margin);
        var content = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        content.AddThemeConstantOverride("separation", 8);
        margin.AddChild(content);
        return content;
    }

    public void OpenCentered()
    {
        AlwaysOnTop = GetTree().Root.AlwaysOnTop;
        PopupCentered();
        GrabFocus();
    }

    private void HandleTitleInput(InputEvent input)
    {
        if (input is InputEventMouseButton button && button.ButtonIndex == MouseButton.Left)
        {
            _dragging = button.Pressed;
            if (button.DoubleClick) Mode = Mode == ModeEnum.Maximized ? ModeEnum.Windowed : ModeEnum.Maximized;
        }
        else if (input is InputEventMouseMotion motion && _dragging && Mode == ModeEnum.Windowed)
        {
            Position += new Vector2I((int)Math.Round(motion.Relative.X), (int)Math.Round(motion.Relative.Y));
        }
    }

    private static void AddTitleButton(Container parent, string text, string tooltip, Action action)
    {
        var button = new Button
        {
            Text = text,
            TooltipText = tooltip,
            CustomMinimumSize = new Vector2(36, 30),
        };
        button.Pressed += action;
        parent.AddChild(button);
    }
}
