using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Simulation;

namespace GameForWork.Core.Equipment;

public static class EquipmentCorruptionCatalog
{
    public static bool Supports(EquipmentCorruptionImplicitEntry entry, ItemBaseDefinition itemBase)
    {
        string text = entry.ApplicabilityText;
        bool weapon = itemBase.Category is ItemCategory.OneHandWeapon or ItemCategory.TwoHandWeapon;
        bool Has(string tag) => itemBase.ItemTags.Contains(tag, StringComparer.Ordinal);
        if (text.StartsWith("所有具有非零基础暴击率", StringComparison.Ordinal)) return weapon && itemBase.CriticalChanceBasisPoints > 0;
        if (text.StartsWith("所有具有基础攻击伤害", StringComparison.Ordinal) || text.StartsWith("所有具有基础物理攻击伤害", StringComparison.Ordinal))
            return weapon && itemBase.MaximumPhysicalDamage > 0;
        if (text.Contains("全部双手剑", StringComparison.Ordinal)) return itemBase.Category == ItemCategory.TwoHandWeapon && (Has("sword") || Has("axe") || Has("mace"));
        if (text.Contains("所有近战", StringComparison.Ordinal)) return Has("melee") || Has("unarmed") || Has("wrap");
        if (text.StartsWith("全部弓", StringComparison.Ordinal)) return Has("bow") && !Has("quiver");
        if (text.StartsWith("全部箭袋", StringComparison.Ordinal)) return Has("quiver");
        if (text.StartsWith("全部匕首", StringComparison.Ordinal)) return Has("dagger");
        if (text.StartsWith("全部法杖、符刃", StringComparison.Ordinal)) return Has("wand") || Has("runeblade");
        if (text.StartsWith("任何至少具有一种基础局部防御", StringComparison.Ordinal))
            return itemBase.ArmorMaximum + itemBase.EvasionMaximum + itemBase.ShieldMaximum + itemBase.SpiritBarrierMaximum > 0;
        if (text.StartsWith("任何具有基础护甲", StringComparison.Ordinal)) return itemBase.ArmorMaximum > 0;
        if (text.StartsWith("任何具有基础闪避", StringComparison.Ordinal)) return itemBase.EvasionMaximum > 0;
        if (text.StartsWith("任何具有基础护盾", StringComparison.Ordinal)) return itemBase.ShieldMaximum > 0;
        if (text.StartsWith("任何具有基础灵障", StringComparison.Ordinal)) return itemBase.SpiritBarrierMaximum > 0;
        if (text.StartsWith("所有具有非零基础格挡率", StringComparison.Ordinal)) return Has("true_shield") && itemBase.BlockChanceBasisPoints > 0;
        if (text.StartsWith("全部胸甲、头盔", StringComparison.Ordinal)) return itemBase.Category is ItemCategory.BodyArmor or ItemCategory.Helmet || Has("true_shield");
        if (text.StartsWith("全部具有孔组的头盔", StringComparison.Ordinal)) return itemBase.Category == ItemCategory.Helmet && itemBase.SocketLimit > 0;
        if (text.StartsWith("全部手套", StringComparison.Ordinal)) return itemBase.Category == ItemCategory.Gloves;
        if (text.StartsWith("全部鞋", StringComparison.Ordinal)) return itemBase.Category == ItemCategory.Boots;
        if (text.StartsWith("全部胸甲", StringComparison.Ordinal)) return itemBase.Category == ItemCategory.BodyArmor;
        if (text.StartsWith("全部戒指", StringComparison.Ordinal)) return itemBase.Category == ItemCategory.Ring;
        if (text.StartsWith("全部角色护符与灵兽护符", StringComparison.Ordinal)) return itemBase.Category == ItemCategory.Amulet;
        if (text.StartsWith("全部角色护符", StringComparison.Ordinal)) return itemBase.Category == ItemCategory.Amulet && !Has("beast_talisman");
        if (text.StartsWith("全部腰带", StringComparison.Ordinal)) return itemBase.Category == ItemCategory.Belt;
        if (text.StartsWith("全部元素法器", StringComparison.Ordinal)) return Has("focus") && !Has("summoning_focus");
        if (text.StartsWith("全部召唤法器", StringComparison.Ordinal)) return Has("summoning_focus");
        if (text.StartsWith("全部灵兽护符", StringComparison.Ordinal)) return Has("beast_talisman") || Has("companion");
        if (text.StartsWith("全部构装圣物", StringComparison.Ordinal)) return Has("construct_idol");
        if (text.StartsWith("全部灵障法器", StringComparison.Ordinal))
            return Has("spirit_barrier_focus") || itemBase.DisplayName.Contains("灵障法器", StringComparison.Ordinal);
        if (text.StartsWith("全部生命药剂", StringComparison.Ordinal)) return itemBase.Category == ItemCategory.LifeFlask && Has("life_flask");
        if (text.StartsWith("全部法力药剂", StringComparison.Ordinal)) return itemBase.Category == ItemCategory.LifeFlask && Has("mana_flask");
        if (text.StartsWith("全部护甲、闪避与移动功能药剂", StringComparison.Ordinal))
            return itemBase.Category == ItemCategory.LifeFlask && Has("utility_flask") &&
                !itemBase.DisplayName.Contains("棱彩", StringComparison.Ordinal);
        if (text.StartsWith("全部生命、法力和功能药剂", StringComparison.Ordinal)) return itemBase.Category == ItemCategory.LifeFlask;
        return false;
    }

    public static IReadOnlyList<AffixModifierComponent> Components(EquipmentCorruptionImplicitEntry entry) => entry.DisplayName switch
    {
        "裂界锋缘" => [C(ItemModifierKind.BaseCriticalChanceBasisPoints, 80, 120, ItemModifierScope.LocalWeapon)],
        "破界凶意" => [C(ItemModifierKind.MoreAttackDamageBasisPoints, 1_000, 1_400)],
        "虚空浸染" => [C(ItemModifierKind.AddedMinimumVoidDamage, 20, 35, ItemModifierScope.LocalWeapon), C(ItemModifierKind.AddedMaximumVoidDamage, 40, 60, ItemModifierScope.LocalWeapon)],
        "分身之击" => [C(ItemModifierKind.AdditionalStrikeTarget, 1, 1)],
        "震域烙印" => [C(ItemModifierKind.SkillAreaBasisPoints, 2_500, 3_500), C(ItemModifierKind.MoreAttackDamageBasisPoints, 1_000, 1_400)],
        "血毒孪生" => [C(ItemModifierKind.BleedChanceBasisPoints, 2_500, 3_500), C(ItemModifierKind.PoisonChanceBasisPoints, 2_500, 3_500)],
        "裂空箭痕" or "猎空蚀羽" => [C(ItemModifierKind.AdditionalProjectile, 1, 1)],
        "伏机刻蚀" => [C(ItemModifierKind.AdditionalTrapMaximum, 1, 1)],
        "秘术增幅" => [C(ItemModifierKind.ActiveSkillGemLevels, 1, 1)],
        "禁法洪流" => [C(ItemModifierKind.MoreSpellDamageBasisPoints, 1_200, 1_600)],
        "四御蚀层" => [C(ItemModifierKind.MoreLocalArmorBasisPoints, 1_200, 1_600, ItemModifierScope.LocalDefense), C(ItemModifierKind.MoreLocalEvasionBasisPoints, 1_200, 1_600, ItemModifierScope.LocalDefense), C(ItemModifierKind.MoreLocalShieldBasisPoints, 1_200, 1_600, ItemModifierScope.LocalDefense), C(ItemModifierKind.MoreLocalSpiritBarrierBasisPoints, 1_200, 1_600, ItemModifierScope.LocalDefense)],
        "铁血侵蚀" => [C(ItemModifierKind.PhysicalResistanceBasisPoints, 400, 600)],
        "影步侵蚀" => [C(ItemModifierKind.SpellSuppressionBasisPoints, 1_000, 1_400)],
        "晶壳侵蚀" => [C(ItemModifierKind.IncreasedMaximumShieldBasisPoints, 800, 1_200)],
        "灵障侵蚀" => [C(ItemModifierKind.MoreLocalSpiritBarrierBasisPoints, 1_500, 2_000, ItemModifierScope.LocalDefense)],
        "天限裂口" => [C(ItemModifierKind.MaximumFireResistanceBasisPoints, 100, 100), C(ItemModifierKind.MaximumColdResistanceBasisPoints, 100, 100), C(ItemModifierKind.MaximumLightningResistanceBasisPoints, 100, 100)],
        "冥限裂口" => [C(ItemModifierKind.MaximumVoidResistanceBasisPoints, 200, 200)],
        "技能蚀刻" => [C(ItemModifierKind.ActiveSkillGemLevels, 1, 1), C(ItemModifierKind.SupportSkillGemLevels, 1, 1)],
        "躁动蚀纹" => [C(ItemModifierKind.IncreasedAttackSpeedBasisPoints, 800, 1_200), C(ItemModifierKind.IncreasedCastSpeedBasisPoints, 800, 1_200)],
        "逐风蚀印" => [C(ItemModifierKind.IncreasedMovementSpeedBasisPoints, 1_000, 1_500)],
        "双格蚀印" => [C(ItemModifierKind.AttackBlockChanceBasisPoints, 500, 500), C(ItemModifierKind.SpellBlockChanceBasisPoints, 500, 500), C(ItemModifierKind.MaximumAttackBlockChanceBasisPoints, 200, 200), C(ItemModifierKind.MaximumSpellBlockChanceBasisPoints, 200, 200)],
        "众生蚀环" => [C(ItemModifierKind.IncreasedMaximumLifeBasisPoints, 500, 700), C(ItemModifierKind.IncreasedMaximumManaBasisPoints, 500, 700), C(ItemModifierKind.IncreasedMaximumShieldBasisPoints, 500, 700)],
        "亵咒蚀环" => [C(ItemModifierKind.AdditionalCurseMaximum, 1, 1)],
        "万法蚀坠" => [C(ItemModifierKind.AllActiveSkillGemLevels, 1, 1)],
        "四柱蚀坠" => [C(ItemModifierKind.Physique, 24, 32), C(ItemModifierKind.Dexterity, 24, 32), C(ItemModifierKind.Spirit, 24, 32), C(ItemModifierKind.Energy, 24, 32)],
        "沸腾蚀带" or "恒效瓶印" => [C(ItemModifierKind.MoreFlaskEffectBasisPoints, entry.DisplayName == "沸腾蚀带" ? 1_500 : 1_200, entry.DisplayName == "沸腾蚀带" ? 2_000 : 1_600, ItemModifierScope.Flask)],
        "永续蚀带" or "无尽瓶印" => [C(ItemModifierKind.FlaskNoChargeConsumptionChanceBasisPoints, 2_000, 2_500, ItemModifierScope.Flask)],
        "三相蚀核" => [C(ItemModifierKind.FirePenetrationBasisPoints, 1_000, 1_200), C(ItemModifierKind.ColdPenetrationBasisPoints, 1_000, 1_200), C(ItemModifierKind.LightningPenetrationBasisPoints, 1_000, 1_200)],
        "群魂蚀契" => [C(ItemModifierKind.AdditionalMinionMaximum, 1, 1)],
        "野性蚀契" => [C(ItemModifierKind.IncreasedCompanionLifeBasisPoints, 2_000, 2_500), C(ItemModifierKind.IncreasedCompanionDamageBasisPoints, 2_000, 2_500)],
        "机群蚀契" => [C(ItemModifierKind.AdditionalConstructMaximum, 1, 1)],
        "灵界蚀核" => [C(ItemModifierKind.IncreasedSpiritBarrierBasisPoints, 1_200, 1_600)],
        "血潮瓶印" => [C(ItemModifierKind.IncreasedFlaskRecoveryAmountBasisPoints, 5_000, 5_000, ItemModifierScope.Flask)],
        "灵泉瓶印" => [C(ItemModifierKind.FlaskDoesNotEndAtFullMana, 1, 1, ItemModifierScope.Flask), C(ItemModifierKind.IncreasedFlaskRecoveryRateBasisPoints, 2_000, 2_000, ItemModifierScope.Flask)],
        _ => throw new InvalidDataException($"Missing corruption implementation: {entry.DisplayName}"),
    };

    public static IReadOnlyList<RolledAffixComponent> Roll(EquipmentCorruptionImplicitEntry entry, Pcg32 random) =>
        Components(entry).Select(component => new RolledAffixComponent(component.Kind,
            component.MinimumValue == component.MaximumValue ? component.MinimumValue : component.MinimumValue +
                (int)(random.NextUInt() % (uint)(component.MaximumValue - component.MinimumValue + 1)),
            component.Scope, entry.ModifierText)).ToArray();

    private static AffixModifierComponent C(ItemModifierKind kind, int minimum, int maximum,
        ItemModifierScope scope = ItemModifierScope.Global) => new(kind, minimum, maximum, scope);
}
