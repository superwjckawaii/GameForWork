using GameForWork.Core.P1.Items;
using GameForWork.Core.P1.World;
using GameForWork.Core.P4;

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
    string Summary,
    MetalCurrencyKind? MetalCostKind = null,
    int MetalCost = 0);

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
        if (!preview.Succeeded)
        {
            return preview;
        }

        bool paid = preview.MetalCostKind is MetalCurrencyKind metal
            ? economy.TrySpendMetal(metal, preview.MetalCost)
            : economy.TryPay(preview.GoldCost, preview.IronScrapCost);
        if (!paid)
        {
            return preview with { Succeeded = false, FailureReason = "insufficient_materials" };
        }

        return preview;
    }

    private static P2WorkshopPreview Weapon(ItemInstance item)
    {
        if (item.Base.Category is not (ItemCategory.TwoHandWeapon or ItemCategory.OneHandWeapon))
        {
            return Fail("weapon_required", "物理锻造只适用于武器。");
        }

        return AddCrafted(item, ItemModifierKind.IncreasedPhysicalDamageBasisPoints, 3_500,
            MetalCurrencyKind.TemperingIron, 1, "物理伤害增加 35%");
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
            : AddCrafted(item, kind, 3_000, MetalCurrencyKind.WardSteel, 1, "基础防御增加 30%");
    }

    private static P2WorkshopPreview Vitality(ItemInstance item) => item.Base.Category == ItemCategory.TwoHandWeapon
        ? Fail("accessory_or_armor_required", "生命刻印不适用于武器。")
        : AddCrafted(item, ItemModifierKind.FlatMaximumLife, 40, MetalCurrencyKind.VitalSilver, 1, "最大生命 +40");

    private static P2WorkshopPreview AddCrafted(
        ItemInstance item,
        ItemModifierKind kind,
        int value,
        MetalCurrencyKind metal,
        int metalCost,
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
        return new P2WorkshopPreview(true, string.Empty, result, 0, 0, summary, metal, metalCost);
    }

    private static P2WorkshopPreview Fail(string reason, string summary) =>
        new(false, reason, null, 0, 0, summary);
}
