using Godot;

namespace GameForWork.GodotClient;

public partial class ToastOverlay : CanvasLayer
{
    private PanelContainer? _panel;
    private Label? _label;
    private double _remaining;

    public override void _Ready()
    {
        Layer = 50;
        var host = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        host.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(host);
        _panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(330, 54),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
        };
        _panel.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        _panel.Position = new Vector2(-346, -70);
        _panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("182330f5"),
            BorderColor = new Color("c3a56d"),
            BorderWidthLeft = 3,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            ContentMarginLeft = 10,
            ContentMarginRight = 10,
            ContentMarginTop = 7,
            ContentMarginBottom = 7,
        });
        _label = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _panel.AddChild(_label);
        host.AddChild(_panel);
    }

    public override void _Process(double delta)
    {
        if (_remaining <= 0 || _panel is null)
        {
            return;
        }

        _remaining -= delta;
        _panel.Modulate = new Color(1, 1, 1, (float)Math.Clamp(_remaining / 0.25, 0, 1));
        if (_remaining <= 0)
        {
            _panel.Visible = false;
        }
    }

    public void ShowMessage(string message, double seconds = 3.2)
    {
        if (_panel is null || _label is null)
        {
            return;
        }

        _label.Text = $"◆ {message}";
        _remaining = seconds;
        _panel.Modulate = Colors.White;
        _panel.Visible = true;
    }
}
