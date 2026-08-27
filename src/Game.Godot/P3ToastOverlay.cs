using Godot;

namespace GameForWork.GodotClient;

public partial class P3ToastOverlay : CanvasLayer
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
            BgColor = new Color("151a22ee"),
            BorderColor = new Color("c08b46"),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
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
        _panel.Visible = true;
    }
}
