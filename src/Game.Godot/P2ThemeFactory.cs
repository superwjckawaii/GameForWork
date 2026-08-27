using Godot;

namespace GameForWork.GodotClient;

public static class P2ThemeFactory
{
    public static Theme Create(int fontScalePercent)
    {
        int fontSize = (int)Math.Round(14 * Math.Clamp(fontScalePercent, 100, 150) / 100d);
        var theme = new Theme { DefaultFontSize = fontSize };
        theme.SetColor("font_color", "Label", new Color("ddd5c7"));
        theme.SetColor("font_color", "Button", new Color("e6dece"));
        theme.SetColor("font_hover_color", "Button", new Color("fff0c6"));
        theme.SetColor("font_pressed_color", "Button", new Color("fff0c6"));
        theme.SetColor("font_disabled_color", "Button", new Color("736f69"));
        theme.SetStylebox("normal", "Button", Frame("202630", "4d5662", 1));
        theme.SetStylebox("hover", "Button", Frame("2b333e", "c09a55", 2));
        theme.SetStylebox("pressed", "Button", Frame("171c24", "e0bd72", 2));
        theme.SetStylebox("disabled", "Button", Frame("151920", "343a43", 1));
        theme.SetStylebox("normal", "LineEdit", Frame("12171e", "505966", 1));
        theme.SetStylebox("focus", "LineEdit", Frame("171d26", "c09a55", 2));
        theme.SetStylebox("panel", "PanelContainer", Frame("151a22", "3f4752", 1));
        theme.SetStylebox("panel", "TabContainer", Frame("11161d", "3f4752", 1));
        theme.SetConstant("outline_size", "Label", 1);
        theme.SetColor("font_outline_color", "Label", new Color("090b0f"));
        return theme;
    }

    private static StyleBoxFlat Frame(string background, string border, int width) => new()
    {
        BgColor = new Color(background),
        BorderColor = new Color(border),
        BorderWidthLeft = width,
        BorderWidthTop = width,
        BorderWidthRight = width,
        BorderWidthBottom = width,
        CornerRadiusTopLeft = 1,
        CornerRadiusTopRight = 1,
        CornerRadiusBottomLeft = 1,
        CornerRadiusBottomRight = 1,
        ContentMarginLeft = 6,
        ContentMarginTop = 4,
        ContentMarginRight = 6,
        ContentMarginBottom = 4,
    };
}
