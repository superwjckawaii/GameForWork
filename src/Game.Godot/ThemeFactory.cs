using Godot;

namespace GameForWork.GodotClient;

public static class ThemeFactory
{
    public static Theme Create(int fontScalePercent)
    {
        int size = (int)Math.Round(14 * Math.Clamp(fontScalePercent, 80, 150) / 100d);
        var theme = new Theme { DefaultFontSize = size };
        foreach (string type in new[] { "Label", "RichTextLabel", "Button", "OptionButton", "CheckBox", "CheckButton", "LineEdit", "TextEdit", "PopupMenu", "Tree", "ItemList", "TabBar" })
        {
            theme.SetColor("font_color", type, new Color("e0e5e8"));
            theme.SetColor("font_disabled_color", type, new Color("78828d"));
            theme.SetColor("font_hover_color", type, new Color("fff0cf"));
            theme.SetColor("font_pressed_color", type, new Color("f4d69c"));
            theme.SetColor("font_focus_color", type, new Color("fff0cf"));
        }
        foreach (string type in new[] { "Button", "OptionButton" })
        {
            theme.SetStylebox("normal", type, Frame("202b38", "3a4858"));
            theme.SetStylebox("hover", type, Frame("2b3948", "b59a69"));
            theme.SetStylebox("pressed", type, Frame("303b44", "d6b678", accent: true));
            theme.SetStylebox("hover_pressed", type, Frame("394650", "e3c993", accent: true));
            theme.SetStylebox("disabled", type, Frame("171e27", "2a3440"));
            theme.SetStylebox("focus", type, Focus());
            theme.SetConstant("h_separation", type, 7);
        }
        foreach (string type in new[] { "LineEdit", "TextEdit" })
        {
            theme.SetStylebox("normal", type, Frame("0e1620", "344253"));
            theme.SetStylebox("read_only", type, Frame("121b25", "273341"));
            theme.SetStylebox("focus", type, Focus());
            theme.SetColor("caret_color", type, new Color("e3c993"));
            theme.SetColor("selection_color", type, new Color("425675"));
        }
        theme.SetStylebox("panel", "PanelContainer", Frame("161f2b", "303d4c", shadow: 3));
        theme.SetStylebox("panel", "TabContainer", Frame("121b26", "344253"));
        foreach (string type in new[] { "PopupPanel", "PopupMenu", "TooltipPanel", "AcceptDialog" })
            theme.SetStylebox("panel", type, Frame("182330", "8c7b5b", shadow: 8));
        theme.SetStylebox("hover", "PopupMenu", Frame("304050", "304050"));
        theme.SetConstant("v_separation", "PopupMenu", 7);
        theme.SetConstant("item_start_padding", "PopupMenu", 10);
        theme.SetConstant("item_end_padding", "PopupMenu", 10);
        theme.SetStylebox("tab_selected", "TabBar", Frame("263442", "c3a56d", accent: true));
        theme.SetStylebox("tab_unselected", "TabBar", Frame("131d29", "273341"));
        theme.SetStylebox("tab_hovered", "TabBar", Frame("202e3d", "63758a"));
        theme.SetStylebox("tab_disabled", "TabBar", Frame("101721", "222e3b"));
        theme.SetStylebox("tab_focus", "TabBar", Focus());
        theme.SetColor("font_selected_color", "TabBar", new Color("f4d69c"));
        theme.SetColor("font_unselected_color", "TabBar", new Color("a5b2c1"));
        theme.SetStylebox("background", "ProgressBar", Frame("0d1520", "2c3a4b", padding: 0));
        theme.SetStylebox("fill", "ProgressBar", Frame("668b99", "88b1b5", padding: 0));
        foreach (string type in new[] { "HScrollBar", "VScrollBar" })
        {
            theme.SetStylebox("scroll", type, Frame("101822", "101822", padding: 3));
            theme.SetStylebox("grabber", type, Frame("425267", "425267", padding: 3));
            theme.SetStylebox("grabber_highlight", type, Frame("718298", "718298", padding: 3));
            theme.SetStylebox("grabber_pressed", type, Frame("b59a69", "b59a69", padding: 3));
        }
        foreach (string type in new[] { "Tree", "ItemList" })
        {
            theme.SetStylebox("panel", type, Frame("101924", "303d4c"));
            theme.SetStylebox("selected", type, Frame("304354", "708894"));
            theme.SetStylebox("selected_focus", type, Frame("304354", "c3a56d"));
        }
        foreach (string type in new[] { "HSeparator", "VSeparator", "PopupMenu" })
            theme.SetStylebox("separator", type, new StyleBoxLine { Color = new Color("354252"), Thickness = 1, Vertical = type == "VSeparator" });
        theme.SetConstant("separation", "VBoxContainer", 7);
        theme.SetConstant("separation", "HBoxContainer", 7);
        theme.SetConstant("h_separation", "GridContainer", 7);
        theme.SetConstant("v_separation", "GridContainer", 7);
        theme.SetConstant("outline_size", "Label", 0);
        foreach (string key in new[] { "tab_selected", "tab_unselected", "tab_hovered", "tab_disabled", "tab_focus" })
            theme.SetStylebox(key, "TabContainer", theme.GetStylebox(key, "TabBar"));
        theme.SetColor("font_selected_color", "TabContainer", new Color("f4d69c"));
        theme.SetColor("font_unselected_color", "TabContainer", new Color("a5b2c1"));
        return theme;
    }

    private static StyleBoxFlat Focus() => new()
    {
        DrawCenter = false, BorderColor = new Color("e3c993"),
        BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
        ExpandMarginLeft = 1, ExpandMarginRight = 1, ExpandMarginTop = 1, ExpandMarginBottom = 1
    };

    private static StyleBoxFlat Frame(string background, string border, int padding = 7, int shadow = 0, bool accent = false) => new()
    {
        BgColor = new Color(background), BorderColor = new Color(border),
        BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = accent ? 2 : 1,
        CornerRadiusTopLeft = 2, CornerRadiusTopRight = 2, CornerRadiusBottomLeft = 2, CornerRadiusBottomRight = 2,
        AntiAliasing = false, ShadowColor = new Color(0.02f, 0.035f, 0.06f, 0.35f), ShadowSize = shadow,
        ShadowOffset = new Vector2(0, shadow > 0 ? 2 : 0),
        ContentMarginLeft = padding, ContentMarginRight = padding, ContentMarginTop = padding == 0 ? 0 : 5, ContentMarginBottom = padding == 0 ? 0 : 5
    };
}
