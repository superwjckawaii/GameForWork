using GameForWork.Core.P1.Items;
using GameForWork.Core.P21;
using GameForWork.Core.P25;
using Godot;

namespace GameForWork.GodotClient;

internal sealed class P21ArtAtlas
{
    public Texture2D? Actors { get; } = Load("res://assets/p21/characters/p21-actor-animation.png");
    public Texture2D? Enemies { get; } = Load("res://assets/p21/enemies/p21-enemy-animation.png");
    public Texture2D? Bosses { get; } = Load("res://assets/p21/enemies/p21-boss-animation.png");
    public Texture2D? Regions { get; } = Load("res://assets/p21/regions/p21-region-atlas.png");
    public Texture2D? Vfx { get; } = Load("res://assets/p21/vfx/p21-combat-vfx.png");
    public Texture2D? ItemBases { get; } = Load("res://assets/p21/ui/p21-item-bases.png");
    public Texture2D? UniqueItems { get; } = Load("res://assets/p21/ui/p21-unique-items.png");
    public Texture2D? P25Equipment { get; } = Load("res://assets/p25/ui/p25-equipment-icons.png");
    public Texture2D? SkillGems { get; } = Load("res://assets/p25/ui/p25-skill-stones.png");
    public Texture2D? Jewels { get; } = Load("res://assets/p21/ui/p21-jewel-atlas.png");

    public Texture2D? ItemIcon(ItemInstance item)
    {
        if (item.LegendaryRule is not null && UniqueItems is not null)
        {
            try { return Icon(UniqueItems, P21ArtContract.UniqueItemIndex(item.LegendaryRule.StableId), 5); }
            catch (KeyNotFoundException) { }
        }
        if (item.Base.StableId.StartsWith("p24.base.", StringComparison.Ordinal) && P25Equipment is not null)
            return Icon(P25Equipment, P25EquipmentArt.IconIndex(item.Base), P25EquipmentArt.Columns);
        return ItemBases is null ? null : Icon(ItemBases, P21ArtContract.ItemBaseIndex(item.Base.StableId), 10);
    }

    public Texture2D? SkillIcon(string stableId) => SkillGems is null
        ? null
        : Icon(SkillGems, P21ArtContract.SkillStoneIndex(stableId), P25SkillStoneArt.Columns);

    public Texture2D? JewelIcon(int index) => Jewels is null ? null : Icon(Jewels, index, 3);

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
