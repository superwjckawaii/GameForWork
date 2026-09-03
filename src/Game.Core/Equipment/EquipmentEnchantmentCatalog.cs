using GameForWork.Core.P1.Items;

namespace GameForWork.Core.Equipment;

public static class EquipmentEnchantmentCatalog
{
    private static readonly IReadOnlyDictionary<string, string> LegacyIds = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["core.enchant.precision"] = "精准刻印", ["core.enchant.vigor"] = "坚生命纹",
        ["core.enchant.attack_tempo"] = "迅击刻印", ["core.enchant.cast_tempo"] = "疾咏刻印",
        ["core.enchant.execution"] = "处刑铭文", ["core.enchant.bulwark"] = "壁垒铭文",
        ["core.enchant.sovereign"] = "断界王印", ["core.enchant.immortal"] = "不灭王印",
        ["core.enchant.perfect_chain"] = "完美链印", ["core.enchant.humility"] = "谦逊足印",
    };

    private static readonly IReadOnlyList<ItemEnchantment> Values = EquipmentCatalog.Enchantments.Select(ToDefinition).ToArray();

    public static IReadOnlyList<ItemEnchantment> All => Values;

    public static ItemEnchantment Get(string id)
    {
        string? name = LegacyIds.GetValueOrDefault(id);
        return Values.Single(value => value.StableId == id || value.DisplayName == name);
    }

    public static bool Supports(ItemEnchantment enchantment, ItemBaseDefinition itemBase)
    {
        EquipmentEnchantmentEntry entry = EquipmentCatalog.Enchantments.Single(value => value.Id == enchantment.StableId);
        string text = entry.ApplicableEquipment;
        if (text.Contains("全部", StringComparison.Ordinal) && text.Contains("武器", StringComparison.Ordinal) && IsWeapon(itemBase)) return true;
        if ((text.Contains("所有鞋", StringComparison.Ordinal) || text.Contains("全部鞋", StringComparison.Ordinal)) && itemBase.Category == ItemCategory.Boots) return true;
        if (text.Contains("手套", StringComparison.Ordinal) && itemBase.Category == ItemCategory.Gloves) return true;
        if (text.Contains("头盔", StringComparison.Ordinal) && itemBase.Category == ItemCategory.Helmet) return true;
        if (text.Contains("胸甲", StringComparison.Ordinal) && itemBase.Category == ItemCategory.BodyArmor) return true;
        if (text.Contains("腰带", StringComparison.Ordinal) && itemBase.Category == ItemCategory.Belt) return true;
        if ((text.Contains("护符", StringComparison.Ordinal) || text.Contains("项链", StringComparison.Ordinal)) && itemBase.Category == ItemCategory.Amulet) return true;
        if (text.Contains("戒指", StringComparison.Ordinal) && itemBase.Category == ItemCategory.Ring) return true;
        if (text.Contains("真盾", StringComparison.Ordinal) && itemBase.ItemTags.Contains("true_shield", StringComparer.Ordinal)) return true;
        if (text.Contains("药剂", StringComparison.Ordinal) && itemBase.Category == ItemCategory.LifeFlask) return true;
        if (text.Contains("箭袋", StringComparison.Ordinal) && itemBase.ItemTags.Contains("quiver", StringComparer.Ordinal)) return true;
        if (text.Contains("元素法器", StringComparison.Ordinal) && itemBase.ItemTags.Contains("focus", StringComparer.Ordinal)) return true;
        if (text.Contains("召唤法器", StringComparison.Ordinal) && itemBase.ItemTags.Contains("summoning_focus", StringComparer.Ordinal)) return true;
        if (text.Contains("灵障法器", StringComparison.Ordinal) && (itemBase.ItemTags.Contains("spirit_barrier_focus", StringComparer.Ordinal) || itemBase.DisplayName.Contains("灵障法器", StringComparison.Ordinal))) return true;
        if (text.Contains("构装圣物", StringComparison.Ordinal) && itemBase.ItemTags.Contains("construct_idol", StringComparer.Ordinal)) return true;
        if (text.Contains("灵兽护符", StringComparison.Ordinal) && (itemBase.ItemTags.Contains("beast_talisman", StringComparer.Ordinal) || itemBase.ItemTags.Contains("companion", StringComparer.Ordinal))) return true;
        if (text.Contains("徒手拳套", StringComparison.Ordinal) && itemBase.ItemTags.Contains("unarmed", StringComparer.Ordinal)) return true;
        if (text.Contains("匕首", StringComparison.Ordinal) && itemBase.WeaponFamily == WeaponFamily.Dagger) return true;
        if (text.Contains("法杖", StringComparison.Ordinal) && itemBase.WeaponFamily == WeaponFamily.Wand) return true;
        if (text.Contains("符刃", StringComparison.Ordinal) && itemBase.WeaponFamily == WeaponFamily.Runeblade) return true;
        if (text.Contains("基础护甲", StringComparison.Ordinal) && itemBase.Armor > 0) return true;
        if (text.Contains("基础护盾", StringComparison.Ordinal) && itemBase.Shield > 0) return true;
        if (text.Contains("基础物理伤害", StringComparison.Ordinal) && IsWeapon(itemBase) && itemBase.MaximumPhysicalDamage > 0) return true;
        if (text.Contains("局部防御", StringComparison.Ordinal) && itemBase.Armor + itemBase.Evasion + itemBase.Shield + itemBase.SpiritBarrier > 0) return true;
        if (text.Contains("孔组", StringComparison.Ordinal) && itemBase.SocketLimit > 0) return true;
        return false;
    }

    private static ItemEnchantment ToDefinition(EquipmentEnchantmentEntry entry)
    {
        AffixModifierComponent[] components = Components(entry.DisplayName);
        AffixModifierComponent primary = components[0];
        return new ItemEnchantment(entry.Id, entry.DisplayName, primary.Kind, primary.MinimumValue,
            entry.WorkshopLevel, entry.GoldCost, primary.Scope, components, Categories(entry.DisplayName));
    }

    private static IReadOnlyList<ItemCategory>? Categories(string name) => name switch
    {
        "处刑铭文" or "断界王印" or "奥术王印" => [ItemCategory.OneHandWeapon, ItemCategory.TwoHandWeapon],
        "谦逊足印" or "傲慢之印" or "暴怒之印" or "节制之印" or "慈悲之印" or "懒惰之印" => [ItemCategory.Helmet, ItemCategory.Gloves, ItemCategory.Boots],
        "轻羽刻印" => [ItemCategory.Boots],
        "双御铭文" => [ItemCategory.Shield],
        _ => null,
    };

    private static AffixModifierComponent[] Components(string name) => name switch
    {
        "精准刻印" => [C(ItemModifierKind.FlatAccuracy, 400)],
        "坚生命纹" => [C(ItemModifierKind.FlatMaximumLife, 60)],
        "迅击刻印" => [C(ItemModifierKind.IncreasedAttackSpeedBasisPoints, 1_200)],
        "疾咏刻印" => [C(ItemModifierKind.IncreasedCastSpeedBasisPoints, 1_200)],
        "处刑铭文" => [C(ItemModifierKind.IncreasedPhysicalDamageBasisPoints, 5_500, ItemModifierScope.LocalWeapon)],
        "壁垒铭文" => DefenseIncreases(5_000),
        "断界王印" => [C(ItemModifierKind.IncreasedPhysicalDamageBasisPoints, 12_000, ItemModifierScope.LocalWeapon)],
        "不灭王印" => [C(ItemModifierKind.FlatMaximumLife, 100, ItemModifierScope.Rule)],
        "完美链印" => [C(ItemModifierKind.ExtraSupportLinkCapacity, 1, ItemModifierScope.Rule)],
        "谦逊足印" => [C(ItemModifierKind.VirtueViceGainChanceBasisPoints, 1_500, ItemModifierScope.Rule), C(ItemModifierKind.HumilityMaximum, 1, ItemModifierScope.Rule)],
        "奥术王印" => [C(ItemModifierKind.None, 1, ItemModifierScope.Rule)],
        "秘法铭文" => [C(ItemModifierKind.MoreSpellDamageBasisPoints, 3_500)],
        "三相铭文" => [C(ItemModifierKind.MoreElementalDamageBasisPoints, 3_500)],
        "虚蚀铭文" => [C(ItemModifierKind.MoreVoidDamageBasisPoints, 4_500)],
        "锐目刻印" => [C(ItemModifierKind.IncreasedCriticalChanceBasisPoints, 6_000)],
        "毁伤铭文" => [C(ItemModifierKind.IncreasedCriticalMultiplierBasisPoints, 6_500)],
        "涌泉刻印" => [C(ItemModifierKind.IncreasedMaximumManaBasisPoints, 2_500)],
        "晶心铭文" => [C(ItemModifierKind.MoreLocalShieldBasisPoints, 3_500, ItemModifierScope.LocalDefense)],
        "冥想刻印" => [C(ItemModifierKind.IncreasedManaRegenerationBasisPoints, 4_000)],
        "双御铭文" => [C(ItemModifierKind.AttackBlockChanceBasisPoints, 600), C(ItemModifierKind.SpellBlockChanceBasisPoints, 600)],
        "轻羽刻印" => [C(ItemModifierKind.IncreasedMovementSpeedBasisPoints, 1_500)],
        "固垒铭文" => [C(ItemModifierKind.FlatArmor, 400, ItemModifierScope.LocalDefense), C(ItemModifierKind.FlatEvasion, 400, ItemModifierScope.LocalDefense), C(ItemModifierKind.FlatShield, 400, ItemModifierScope.LocalDefense), C(ItemModifierKind.FlatSpiritBarrier, 400, ItemModifierScope.LocalDefense)],
        "虹彩王印" => [C(ItemModifierKind.FireResistanceBasisPoints, 2_500), C(ItemModifierKind.ColdResistanceBasisPoints, 2_500), C(ItemModifierKind.LightningResistanceBasisPoints, 2_500), C(ItemModifierKind.VoidResistanceBasisPoints, 2_500), C(ItemModifierKind.MaximumVoidResistanceBonusBasisPoints, 500)],
        "归返王印" => [C(ItemModifierKind.ReturnProjectiles, 1, ItemModifierScope.Rule)],
        "复狩王印" => [C(ItemModifierKind.TrapRearm, 1, ItemModifierScope.Rule)],
        "亡军王印" => [C(ItemModifierKind.MinionAutomaticResummon, 1, ItemModifierScope.Rule)],
        "双咒王印" => [C(ItemModifierKind.AdditionalCurseMaximum, 1, ItemModifierScope.Rule)],
        "不离王印" => [C(ItemModifierKind.CompanionCheatDeath, 1, ItemModifierScope.Rule)],
        "炉心王印" => [C(ItemModifierKind.ConstructExplodeAndRebuild, 1, ItemModifierScope.Rule)],
        "空明王印" => [C(ItemModifierKind.UnarmedDefenseToMoreDamage, 500, ItemModifierScope.Rule)],
        "交错王印" => [C(ItemModifierKind.RunebladeAttackSpellBridge, 5_000, ItemModifierScope.Rule)],
        "清创铭文" => [C(ItemModifierKind.FlaskCleanseBleedPoison, 400, ItemModifierScope.Rule)],
        "净元素铭文" => [C(ItemModifierKind.FlaskCleanseElementalAilments, 400, ItemModifierScope.Rule)],
        "祓咒铭文" => [C(ItemModifierKind.FlaskCleanseCurses, 400, ItemModifierScope.Rule)],
        "溢流铭文" => [C(ItemModifierKind.FlaskOverflowCharges, 1, ItemModifierScope.Rule)],
        "余响王印" => [C(ItemModifierKind.FlaskRepeatEffect, 1, ItemModifierScope.Rule)],
        "傲慢之印" => Virtue(ItemModifierKind.ArroganceMaximum),
        "暴怒之印" => Virtue(ItemModifierKind.RageMaximum),
        "节制之印" => Virtue(ItemModifierKind.TemperanceMaximum),
        "慈悲之印" => Virtue(ItemModifierKind.MercyMaximum),
        "懒惰之印" => Virtue(ItemModifierKind.SlothMaximum),
        _ => throw new InvalidDataException($"Missing enchantment implementation: {name}"),
    };

    private static AffixModifierComponent[] Virtue(ItemModifierKind maximum) => [C(ItemModifierKind.VirtueViceGainChanceBasisPoints, 1_000, ItemModifierScope.Rule), C(maximum, 1, ItemModifierScope.Rule)];
    private static AffixModifierComponent[] DefenseIncreases(int value) => [C(ItemModifierKind.IncreasedArmorBasisPoints, value, ItemModifierScope.LocalDefense), C(ItemModifierKind.IncreasedEvasionBasisPoints, value, ItemModifierScope.LocalDefense), C(ItemModifierKind.IncreasedShieldBasisPoints, value, ItemModifierScope.LocalDefense), C(ItemModifierKind.IncreasedSpiritBarrierBasisPoints, value, ItemModifierScope.LocalDefense)];
    private static AffixModifierComponent C(ItemModifierKind kind, int value, ItemModifierScope scope = ItemModifierScope.Global) => new(kind, value, value, scope);
    private static bool IsWeapon(ItemBaseDefinition itemBase) => itemBase.Category is ItemCategory.OneHandWeapon or ItemCategory.TwoHandWeapon;
}
