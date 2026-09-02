using GameForWork.Core.P1.Items;
using GameForWork.Core.P21;
using GameForWork.Core.P25;
using Godot;

namespace GameForWork.GodotClient;

internal sealed class P21ArtAtlas
{
    public Texture2D? UniqueItems { get; } = Load("res://assets/p25/ui/p25-legendary-atlas.png");
    public Texture2D? P25Equipment { get; } = Load("res://assets/p25/ui/p25-equipment-atlas.png");
    public Texture2D? SkillGems { get; } = Load("res://assets/p25/ui/p25-skill-stones.png");

    public Texture2D? ItemIcon(ItemInstance item)
    {
        if (item.LegendaryRule is not null && UniqueItems is not null)
        {
            try { return Icon(UniqueItems, P25LegendaryArt.IconIndex(item.LegendaryRule.StableId), P25LegendaryArt.Columns); }
            catch (KeyNotFoundException) { }
        }
        return P25Equipment is null ? null : Icon(P25Equipment, P25EquipmentArt.IconIndex(item.Base), P25EquipmentArt.Columns);
    }

    public Texture2D? SkillIcon(string stableId) => SkillGems is null
        ? null
        : Icon(SkillGems, P21ArtContract.SkillStoneIndex(stableId), P25SkillStoneArt.Columns);

    public static AtlasTexture Icon(Texture2D atlas, int index, int columns)
    {
        float width = atlas.GetWidth() / (float)columns;
        int rows = Math.Max(1, (int)MathF.Round(atlas.GetHeight() / width));
        float height = atlas.GetHeight() / (float)rows;
        return new AtlasTexture
        {
            Atlas = atlas,
            Region = new Rect2(index % columns * width, index / columns * height, width, height),
            FilterClip = true,
        };
    }

    public static Rect2 AnimationCell(int column, int row, int width, int height) =>
        new(column * width, row * height, width, height);

    private static Texture2D? Load(string path) => ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
}
