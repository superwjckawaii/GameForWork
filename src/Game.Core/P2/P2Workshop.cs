using GameForWork.Core.P1.Items;
using GameForWork.Core.P1.World;

namespace GameForWork.Core.P2;

public enum P2WorkshopRecipe
{
    WeaponPhysical,
    ReinforceDefense,
    VitalityEtching,
}

public sealed record P2WorkshopPreview(
    bool Succeeded,
    string FailureReason,
    ItemInstance? Result,
    int GoldCost,
    int IronScrapCost,
    string Summary);

public static class P2Workshop
{
    public static P2WorkshopPreview Preview(ItemInstance item, P2WorkshopRecipe recipe)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.IsLocked)
        {
            return Fail("item_locked", "锁定物品不能制作。");
        }

        return recipe switch
        {
            P2WorkshopRecipe.WeaponPhysical => Weapon(item),
            P2WorkshopRecipe.ReinforceDefense => Defense(item),
            P2WorkshopRecipe.VitalityEtching => Vitality(item),
            _ => Fail("recipe_unknown", "未知配方。"),
        };
    }

    public static P2WorkshopPreview Craft(TownEconomyState economy, ItemInstance item, P2WorkshopRecipe recipe)
    {
        P2WorkshopPreview preview = Preview(item, recipe);
        if (!preview.Succeeded || !economy.TryPay(preview.GoldCost, preview.IronScrapCost))
        {
            return preview.Succeeded ? preview with { Succeeded = false, FailureReason = "insufficient_materials" } : preview;
        }

        return preview;
    }

    private static P2WorkshopPreview Weapon(ItemInstance item)
    {
        if (item.Base.Category != ItemCategory.TwoHandWeapon)
        {
            return Fail("weapon_required", "物理锻造只适用于双手武器。");
        }

        return AddCrafted(item, ItemModifierKind.IncreasedPhysicalDamageBasisPoints, 2_000, 50, 10, "物理伤害增加 20%");
    }

    private static P2WorkshopPreview Defense(ItemInstance item)
    {
        ItemModifierKind kind = item.Base.Armor > 0
            ? ItemModifierKind.IncreasedArmorBasisPoints
            : item.Base.Evasion > 0 ? ItemModifierKind.IncreasedEvasionBasisPoints
            : item.Base.Shield > 0 ? ItemModifierKind.IncreasedShieldBasisPoints
            : ItemModifierKind.None;
        return kind == ItemModifierKind.None
            ? Fail("defense_required", "加固只适用于具有护甲、闪避或护盾的装备。")
            : AddCrafted(item, kind, 1_500, 35, 8, "基础防御增加 15%");
    }

    private static P2WorkshopPreview Vitality(ItemInstance item) => item.Base.Category == ItemCategory.TwoHandWeapon
        ? Fail("accessory_or_armor_required", "生命刻印不适用于武器。")
        : AddCrafted(item, ItemModifierKind.FlatMaximumLife, 8, 30, 6, "最大生命 +8");

    private static P2WorkshopPreview AddCrafted(
        ItemInstance item,
        ItemModifierKind kind,
        int value,
        int gold,
        int scraps,
        string summary)
    {
        AffixRoll[] retained = item.Affixes.Where(affix => !affix.Crafted).ToArray();
        int maximumPrefixes = item.Rarity == ItemRarity.Magic ? 1 : item.Rarity == ItemRarity.Basic ? 1 : 3;
        if (retained.Count(affix => affix.Definition.Position == AffixPosition.Prefix) >= maximumPrefixes)
        {
            return Fail("no_prefix_slot", "没有可用前缀位置。");
        }

        var definition = new AffixDefinition(
            $"core.affix.workshop.{kind}",
            summary,
            item.Base.Category,
            AffixPosition.Prefix,
            0,
            1,
            value,
            value,
            0,
            kind);
        ItemInstance result = item with { Affixes = retained.Append(new AffixRoll(definition, value, Crafted: true)).ToArray() };
        return new P2WorkshopPreview(true, string.Empty, result, gold, scraps, summary);
    }

    private static P2WorkshopPreview Fail(string reason, string summary) =>
        new(false, reason, null, 0, 0, summary);
}
