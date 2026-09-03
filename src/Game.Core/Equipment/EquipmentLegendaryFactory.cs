using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P14;

namespace GameForWork.Core.Equipment;

public static class EquipmentLegendaryFactory
{
    public static ItemInstance Create(string catalogId, int itemLevel, string instanceId, ulong seed = 0)
    {
        EquipmentLegendaryEntry entry = EquipmentCatalog.LegendaryItems.Single(value => value.Id == catalogId);
        return Create(entry, itemLevel, instanceId, seed);
    }

    public static ItemInstance CreateByName(string displayName, int itemLevel, string instanceId, ulong seed = 0)
    {
        EquipmentLegendaryEntry entry = EquipmentCatalog.LegendaryItems.Single(value => value.DisplayName == displayName);
        return Create(entry, itemLevel, instanceId, seed);
    }

    public static IReadOnlyList<EquipmentLegendaryEntry> ForBase(ItemBaseDefinition itemBase, bool includeMythic = false) =>
        EquipmentCatalog.LegendaryItems.Where(entry =>
            (includeMythic || entry.Rarity == "Legendary") && ResolveBase(entry).StableId == itemBase.StableId).ToArray();

    private static ItemInstance Create(EquipmentLegendaryEntry entry, int itemLevel, string instanceId, ulong seed)
    {
        ItemBaseDefinition itemBase = ResolveBase(entry);
        bool mythic = entry.Rarity == "Mythic";
        AffixRoll[] affixes = entry.FixedAffixesText.Split('；', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select((text, index) => FixedAffix(entry, itemBase, text, index)).ToArray();
        int implicitValue = itemBase.ImplicitMaximumValue;
        string variant = entry.DisplayName == "两极德印" ? ResolvePairedVirtueVariant(seed) : string.Empty;
        string publicRuleId = P14UniqueItems.All.FirstOrDefault(value => value.DisplayName == entry.DisplayName)?.StableId ?? entry.RuleId;
        return new ItemInstance(instanceId, itemBase, Math.Clamp(itemLevel, 1, 120), ItemRarity.Legendary, affixes,
            new LegendaryRule(publicRuleId, 10_000, 10_000, entry.RuleText), ImplicitValue: implicitValue,
            LinkedSocketCount: Math.Min(mythic ? 6 : 5, itemBase.SocketLimit == 0 ? mythic ? 6 : 5 : itemBase.SocketLimit),
            Quality: mythic ? 20 : 10, RolledName: entry.DisplayName, DropSource: entry.BaseAndSource,
            RolledBaseArmor: itemBase.ArmorMaximum, RolledBaseEvasion: itemBase.EvasionMaximum,
            RolledBaseShield: itemBase.ShieldMaximum, RolledBaseSpiritBarrier: itemBase.SpiritBarrierMaximum,
            LegendaryCatalogId: entry.Id, CorruptionOutcome: variant);
    }

    private static ItemBaseDefinition ResolveBase(EquipmentLegendaryEntry entry)
    {
        string name = entry.BaseAndSource.Split('；', StringSplitOptions.TrimEntries)[0]
            .Replace("（新增）", string.Empty, StringComparison.Ordinal).Trim();
        return EquipmentCatalog.Bases.FirstOrDefault(value => value.DisplayName == name)
            ?? throw new InvalidOperationException($"Legendary {entry.DisplayName} references unknown base {name}.");
    }

    private static AffixRoll FixedAffix(EquipmentLegendaryEntry entry, ItemBaseDefinition itemBase, string text, int index)
    {
        AffixModifierComponent[] components = ParseFixedStats(text, itemBase).ToArray();
        AffixModifierComponent primary = components[0];
        string id = $"{entry.Id}.fixed.{index + 1}";
        var definition = new AffixDefinition(id, text, itemBase.Category,
            index % 2 == 0 ? AffixPosition.Prefix : AffixPosition.Suffix, 0, 1, primary.MinimumValue, primary.MaximumValue, 0, primary.Kind,
            SourceId: entry.Id, RawText: text, Source: "传奇固定", Components: components);
        return new AffixRoll(definition, primary.MinimumValue, Components: components.Select(component =>
            new RolledAffixComponent(component.Kind, component.MinimumValue, component.Scope, text)).ToArray());
    }

    private static IReadOnlyList<AffixModifierComponent> ParseFixedStats(string text, ItemBaseDefinition itemBase)
    {
        int value = ParseLastNumber(text);
        ItemModifierScope local = itemBase.Category is ItemCategory.OneHandWeapon or ItemCategory.TwoHandWeapon
            ? ItemModifierScope.LocalWeapon : ItemModifierScope.LocalDefense;
        AffixModifierComponent C(ItemModifierKind kind, ItemModifierScope scope = ItemModifierScope.Global) => new(kind, value, value, scope, text);
        if (text.Contains("本装备现有局部防御", StringComparison.Ordinal))
            return [C(ItemModifierKind.IncreasedArmorBasisPoints, ItemModifierScope.LocalDefense), C(ItemModifierKind.IncreasedEvasionBasisPoints, ItemModifierScope.LocalDefense), C(ItemModifierKind.IncreasedShieldBasisPoints, ItemModifierScope.LocalDefense), C(ItemModifierKind.IncreasedSpiritBarrierBasisPoints, ItemModifierScope.LocalDefense)];
        if (text.Contains("局部护甲、闪避与护盾", StringComparison.Ordinal))
            return [C(ItemModifierKind.IncreasedArmorBasisPoints, ItemModifierScope.LocalDefense), C(ItemModifierKind.IncreasedEvasionBasisPoints, ItemModifierScope.LocalDefense), C(ItemModifierKind.IncreasedShieldBasisPoints, ItemModifierScope.LocalDefense)];
        if (text.Contains("局部护甲与护盾", StringComparison.Ordinal))
            return text.Contains("总增", StringComparison.Ordinal)
                ? [C(ItemModifierKind.MoreLocalArmorBasisPoints, ItemModifierScope.LocalDefense), C(ItemModifierKind.MoreLocalShieldBasisPoints, ItemModifierScope.LocalDefense)]
                : [C(ItemModifierKind.IncreasedArmorBasisPoints, ItemModifierScope.LocalDefense), C(ItemModifierKind.IncreasedShieldBasisPoints, ItemModifierScope.LocalDefense)];
        if (text.Contains("局部物理", StringComparison.Ordinal)) return [C(ItemModifierKind.IncreasedPhysicalDamageBasisPoints, local)];
        if (text.Contains("局部攻击速度", StringComparison.Ordinal)) return [C(ItemModifierKind.IncreasedAttackSpeedBasisPoints, local)];
        if (text.Contains("局部护甲", StringComparison.Ordinal)) return [C(ItemModifierKind.IncreasedArmorBasisPoints, ItemModifierScope.LocalDefense)];
        if (text.Contains("局部闪避", StringComparison.Ordinal)) return [C(ItemModifierKind.IncreasedEvasionBasisPoints, ItemModifierScope.LocalDefense)];
        if (text.Contains("局部护盾", StringComparison.Ordinal)) return [C(ItemModifierKind.IncreasedShieldBasisPoints, ItemModifierScope.LocalDefense)];
        if (text.Contains("局部灵障", StringComparison.Ordinal)) return [C(text.Contains("总增", StringComparison.Ordinal) ? ItemModifierKind.MoreLocalSpiritBarrierBasisPoints : ItemModifierKind.IncreasedSpiritBarrierBasisPoints, ItemModifierScope.LocalDefense)];
        if (text.Contains("固定灵障", StringComparison.Ordinal)) return [C(ItemModifierKind.FlatSpiritBarrier, ItemModifierScope.LocalDefense)];
        if (text.Contains("四种最大抗性", StringComparison.Ordinal)) return [C(ItemModifierKind.MaximumFireResistanceBasisPoints), C(ItemModifierKind.MaximumColdResistanceBasisPoints), C(ItemModifierKind.MaximumLightningResistanceBasisPoints), C(ItemModifierKind.MaximumVoidResistanceBasisPoints)];
        if (text.Contains("火焰、冰霜、闪电、虚空抗性", StringComparison.Ordinal)) return [C(ItemModifierKind.FireResistanceBasisPoints), C(ItemModifierKind.ColdResistanceBasisPoints), C(ItemModifierKind.LightningResistanceBasisPoints), C(ItemModifierKind.VoidResistanceBasisPoints)];
        if (text.Contains("火焰、冰霜、闪电抗性", StringComparison.Ordinal)) return [C(ItemModifierKind.FireResistanceBasisPoints), C(ItemModifierKind.ColdResistanceBasisPoints), C(ItemModifierKind.LightningResistanceBasisPoints)];
        if (text.Contains("体魄、灵巧、精神、能量", StringComparison.Ordinal)) return [C(ItemModifierKind.Physique), C(ItemModifierKind.Dexterity), C(ItemModifierKind.Spirit), C(ItemModifierKind.Energy)];
        if (text.Contains("攻击与施法速度", StringComparison.Ordinal)) return [C(ItemModifierKind.IncreasedAttackSpeedBasisPoints), C(ItemModifierKind.IncreasedCastSpeedBasisPoints)];
        if (text.Contains("最大生命", StringComparison.Ordinal) && text.Contains('+')) return [C(ItemModifierKind.FlatMaximumLife)];
        if (text.Contains("角色最大生命提高", StringComparison.Ordinal)) return [C(ItemModifierKind.IncreasedMaximumLifeBasisPoints)];
        if (text.Contains("最大法力", StringComparison.Ordinal)) return [C(ItemModifierKind.FlatMaximumMana)];
        if (text.Contains("体魄", StringComparison.Ordinal)) return [C(ItemModifierKind.Physique)];
        if (text.Contains("灵巧", StringComparison.Ordinal)) return [C(ItemModifierKind.Dexterity)];
        if (text.Contains("精神", StringComparison.Ordinal)) return [C(ItemModifierKind.Spirit)];
        if (text.Contains("能量", StringComparison.Ordinal)) return [C(ItemModifierKind.Energy)];
        if (text.Contains("命中值", StringComparison.Ordinal)) return [C(ItemModifierKind.FlatAccuracy)];
        if (text.Contains("火焰抗性", StringComparison.Ordinal)) return [C(ItemModifierKind.FireResistanceBasisPoints)];
        if (text.Contains("冰霜抗性", StringComparison.Ordinal)) return [C(ItemModifierKind.ColdResistanceBasisPoints)];
        if (text.Contains("闪电抗性", StringComparison.Ordinal)) return [C(ItemModifierKind.LightningResistanceBasisPoints)];
        if (text.Contains("虚空抗性", StringComparison.Ordinal)) return [C(ItemModifierKind.VoidResistanceBasisPoints)];
        if (text.Contains("移动速度", StringComparison.Ordinal)) return [C(ItemModifierKind.IncreasedMovementSpeedBasisPoints)];
        if (text.Contains("攻击格挡", StringComparison.Ordinal)) return [C(ItemModifierKind.AttackBlockChanceBasisPoints)];
        if (text.Contains("法术压制", StringComparison.Ordinal)) return [C(ItemModifierKind.SpellSuppressionBasisPoints)];
        if (text.Contains("物理伤害减免", StringComparison.Ordinal)) return [C(ItemModifierKind.PhysicalResistanceBasisPoints)];
        if (text.Contains("战吼效果", StringComparison.Ordinal)) return [C(ItemModifierKind.IncreasedWarcryEffectBasisPoints)];
        if (text.Contains("流血持续时间", StringComparison.Ordinal)) return [C(ItemModifierKind.IncreasedBleedDurationBasisPoints)];
        if (text.Contains("药剂充能获取", StringComparison.Ordinal)) return [C(ItemModifierKind.IncreasedFlaskChargeGainBasisPoints)];
        if (text.Contains("暴击伤害倍率", StringComparison.Ordinal)) return [C(ItemModifierKind.IncreasedCriticalMultiplierBasisPoints)];
        if (text.Contains("全局法术暴击率", StringComparison.Ordinal) || text.Contains("全局暴击率", StringComparison.Ordinal)) return [C(ItemModifierKind.IncreasedCriticalChanceBasisPoints)];
        if (text.Contains("投射物额外连锁", StringComparison.Ordinal)) return [C(ItemModifierKind.AdditionalChain)];
        if (text.Contains("投射物数量", StringComparison.Ordinal)) return [C(ItemModifierKind.AdditionalProjectile)];
        if (text.Contains("生命恢复率", StringComparison.Ordinal) || text.Contains("法力恢复率", StringComparison.Ordinal)) return [C(ItemModifierKind.IncreasedResourceRecoveryRateBasisPoints)];
        if (text.Contains("法术伤害总增", StringComparison.Ordinal)) return [C(ItemModifierKind.MoreSpellDamageBasisPoints)];
        if (text.Contains("法术伤害提高", StringComparison.Ordinal)) return [C(ItemModifierKind.IncreasedSpellDamageBasisPoints)];
        if (text.Contains("施法速度", StringComparison.Ordinal)) return [C(ItemModifierKind.IncreasedCastSpeedBasisPoints)];
        if (text.Contains("护甲提高", StringComparison.Ordinal)) return [C(ItemModifierKind.IncreasedArmorBasisPoints)];
        if (text.Contains("普通召唤物最大生命", StringComparison.Ordinal)) return [C(ItemModifierKind.IncreasedMinionLifeBasisPoints)];
        if (text.Contains("普通召唤物伤害", StringComparison.Ordinal)) return [C(ItemModifierKind.IncreasedMinionDamageBasisPoints)];
        if (text.Contains("徒手攻击速度", StringComparison.Ordinal)) return [C(ItemModifierKind.IncreasedAttackSpeedBasisPoints)];
        if (text.Contains("灵兽最大生命", StringComparison.Ordinal)) return [C(text.Contains('+') ? ItemModifierKind.FlatCompanionMaximumLife : ItemModifierKind.IncreasedCompanionLifeBasisPoints)];
        if (text.Contains("灵兽伤害总增", StringComparison.Ordinal)) return [C(ItemModifierKind.MoreCompanionDamageBasisPoints)];
        if (text.Contains("灵兽伤害", StringComparison.Ordinal)) return [C(ItemModifierKind.IncreasedCompanionDamageBasisPoints)];
        if (text.Contains("构装体最大生命总增", StringComparison.Ordinal)) return [C(ItemModifierKind.MoreConstructLifeBasisPoints)];
        if (text.Contains("构装体伤害总增", StringComparison.Ordinal)) return [C(ItemModifierKind.MoreConstructDamageBasisPoints)];
        if (text.Contains("构装体最大生命", StringComparison.Ordinal)) return [C(ItemModifierKind.IncreasedConstructLifeBasisPoints)];
        if (text.Contains("构装体伤害", StringComparison.Ordinal)) return [C(ItemModifierKind.IncreasedConstructDamageBasisPoints)];
        if (text.Contains("基础暴击率", StringComparison.Ordinal)) return [C(ItemModifierKind.BaseCriticalChanceBasisPoints, ItemModifierScope.LocalWeapon)];
        throw new InvalidDataException($"Missing fixed legendary affix implementation: {text}");
    }

    private static int ParseLastNumber(string text)
    {
        MatchCollection matches = Regex.Matches(text, @"\d+(?:\.\d+)?");
        if (matches.Count == 0) return 1;
        decimal number = decimal.Parse(matches[^1].Value, System.Globalization.CultureInfo.InvariantCulture);
        return (int)Math.Round(number * (text.Contains('%') ? 100 : 1), MidpointRounding.AwayFromZero);
    }

    private static string ResolvePairedVirtueVariant(ulong seed)
    {
        string[] virtues = ["谦逊", "节制", "慈悲"];
        string[] vices = ["傲慢", "暴怒", "懒惰"];
        if (seed == 0) seed = BitConverter.ToUInt64(SHA256.HashData(Encoding.UTF8.GetBytes("两极德印")), 0);
        return virtues[(int)(seed % 3)] + "+" + vices[(int)((seed / 3) % 3)];
    }
}
