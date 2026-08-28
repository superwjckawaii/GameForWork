using Godot;

namespace GameForWork.GodotClient;

public partial class PixelGoldIcon : Control
{
    public PixelGoldIcon()
    {
        CustomMinimumSize = new Vector2(20, 20);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Draw()
    {
        Color outline = Color.FromHtml("6b4011");
        Color shadow = Color.FromHtml("b86b17");
        Color gold = Color.FromHtml("f2b632");
        Color shine = Color.FromHtml("ffe783");
        DrawRect(new Rect2(5, 2, 10, 2), outline);
        DrawRect(new Rect2(3, 4, 14, 12), outline);
        DrawRect(new Rect2(5, 16, 10, 2), outline);
        DrawRect(new Rect2(5, 4, 10, 12), gold);
        DrawRect(new Rect2(3, 7, 2, 6), shadow);
        DrawRect(new Rect2(15, 7, 2, 6), shadow);
        DrawRect(new Rect2(7, 5, 5, 2), shine);
        DrawRect(new Rect2(6, 7, 2, 4), shine);
        DrawRect(new Rect2(10, 9, 3, 5), shadow);
    }
}
