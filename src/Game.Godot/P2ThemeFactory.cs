using Godot;

namespace GameForWork.GodotClient;

public static class P2ThemeFactory
{
    public static Theme Create(int fontScalePercent)
    {
        int fontSize = (int)Math.Round(14 * Math.Clamp(fontScalePercent, 80, 150) / 100d);
        var theme = new Theme { DefaultFontSize = fontSize };
        const string skinPath = "res://assets/p21/ui/p21-ui-skin.png";
        Texture2D? skin = ResourceLoader.Exists(skinPath) ? GD.Load<Texture2D>(skinPath) : null;
        theme.SetColor("font_color", "Label", new Color("ddd5c7"));
        theme.SetColor("font_color", "Button", new Color("e6dece"));
        theme.SetColor("font_hover_color", "Button", new Color("fff0c6"));
        theme.SetColor("font_pressed_color", "Button", new Color("fff0c6"));
        theme.SetColor("font_disabled_color", "Button", new Color("736f69"));
        StyleBox buttonNormal = skin is null ? Frame("202630", "4d5662", 1) : PixelFrame(skin, 1);
        StyleBox buttonHover = skin is null ? Frame("2b333e", "c09a55", 2) : PixelFrame(skin, 2);
        StyleBox buttonPressed = skin is null ? Frame("171c24", "e0bd72", 2) : PixelFrame(skin, 3);
        StyleBox buttonDisabled = skin is null ? Frame("151920", "343a43", 1) : PixelFrame(skin, 4);
        StyleBox input = skin is null ? Frame("12171e", "505966", 1) : PixelFrame(skin, 5);
        StyleBox panel = skin is null ? Frame("151a22", "3f4752", 1) : PixelFrame(skin, 0);
        StyleBox accentPanel = skin is null ? Frame("11161d", "3f4752", 1) : PixelFrame(skin, 6);
        theme.SetStylebox("normal", "Button", buttonNormal);
        theme.SetStylebox("hover", "Button", buttonHover);
        theme.SetStylebox("pressed", "Button", buttonPressed);
        theme.SetStylebox("disabled", "Button", buttonDisabled);
        theme.SetStylebox("normal", "LineEdit", input);
        theme.SetStylebox("focus", "LineEdit", skin is null ? Frame("171d26", "c09a55", 2) : PixelFrame(skin, 7));
        theme.SetStylebox("panel", "PanelContainer", panel);
        theme.SetStylebox("panel", "TabContainer", accentPanel);
        theme.SetStylebox("panel", "PopupPanel", skin is null ? Frame("10151c", "c09a55", 2) : PixelFrame(skin, 7));
        theme.SetStylebox("panel", "TooltipPanel", skin is null ? Frame("10151c", "c09a55", 2) : PixelFrame(skin, 7));
        theme.SetStylebox("panel", "PopupMenu", skin is null ? Frame("10151c", "c09a55", 2) : PixelFrame(skin, 7));
        theme.SetStylebox("hover", "PopupMenu", buttonHover);
        theme.SetStylebox("separator", "PopupMenu", new StyleBoxLine
        {
            Color = new Color("4d5662"),
            Thickness = 1,
            GrowBegin = -4,
            GrowEnd = -4,
        });
        theme.SetColor("font_color", "PopupMenu", new Color("ddd5c7"));
        theme.SetColor("font_hover_color", "PopupMenu", new Color("fff0c6"));
        theme.SetColor("font_disabled_color", "PopupMenu", new Color("736f69"));
        theme.SetConstant("item_start_padding", "PopupMenu", 8);
        theme.SetConstant("item_end_padding", "PopupMenu", 8);
        theme.SetStylebox("tab_selected", "TabBar", skin is null ? Frame("202630", "c09a55", 2) : PixelFrame(skin, 6));
        theme.SetStylebox("tab_unselected", "TabBar", buttonNormal);
        theme.SetStylebox("tab_hovered", "TabBar", buttonHover);
        theme.SetConstant("outline_size", "Label", 1);
        theme.SetColor("font_outline_color", "Label", new Color("090b0f"));
        return theme;
    }

    private static StyleBoxTexture PixelFrame(Texture2D texture, int index)
    {
        var style = new StyleBoxTexture
        {
            Texture = texture,
            RegionRect = new Rect2(index % 4 * 64, index / 4 * 32, 64, 32),
            TextureMarginLeft = 7,
            TextureMarginTop = 7,
            TextureMarginRight = 7,
            TextureMarginBottom = 7,
            AxisStretchHorizontal = StyleBoxTexture.AxisStretchMode.TileFit,
            AxisStretchVertical = StyleBoxTexture.AxisStretchMode.TileFit,
        };
        style.SetContentMargin(Side.Left, 7);
        style.SetContentMargin(Side.Top, 5);
        style.SetContentMargin(Side.Right, 7);
        style.SetContentMargin(Side.Bottom, 5);
        return style;
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
