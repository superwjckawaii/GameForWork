using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Art;
using GameForWork.Core.Equipment;
using Godot;

namespace GameForWork.GodotClient;

internal sealed class ArtAtlas
{
    public Texture2D? UniqueItems { get; } = Load("res://assets/equipmentArt/ui/equipmentArt-legendary-atlas.png");
    public Texture2D? EquipmentArtEquipment { get; } = Load("res://assets/equipmentArt/ui/equipmentArt-equipment-atlas.png");
    public Texture2D? SkillGems { get; } = Load("res://assets/equipmentArt/ui/equipmentArt-skill-stones.png");

    public Texture2D? ItemIcon(ItemInstance item)
    {
        if (item.LegendaryRule is not null && UniqueItems is not null)
        {
            try { return Icon(UniqueItems, EquipmentLegendaryArt.IconIndex(item.LegendaryRule.StableId), EquipmentLegendaryArt.Columns); }
            catch (KeyNotFoundException) { }
        }
        return EquipmentArtEquipment is null ? null : Icon(EquipmentArtEquipment, EquipmentBaseArt.IconIndex(item.Base), EquipmentBaseArt.Columns);
    }

    public Texture2D? SkillIcon(string stableId) => SkillGems is null
        ? null
        : Icon(SkillGems, ArtContract.SkillStoneIndex(stableId), SkillStoneArt.Columns);

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
