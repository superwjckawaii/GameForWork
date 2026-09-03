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
        (ItemModifierKind kind, int value, ItemModifierScope scope) = ParseFixedStat(text, itemBase);
        string id = $"{entry.Id}.fixed.{index + 1}";
        var component = new AffixModifierComponent(kind, value, value, scope, text);
        var definition = new AffixDefinition(id, text, itemBase.Category,
            index % 2 == 0 ? AffixPosition.Prefix : AffixPosition.Suffix, 0, 1, value, value, 0, kind,
            SourceId: entry.Id, RawText: text, Source: "传奇固定", Components: [component]);
        return new AffixRoll(definition, value, Components: [new RolledAffixComponent(kind, value, scope, text)]);
    }

    private static (ItemModifierKind kind, int value, ItemModifierScope scope) ParseFixedStat(string text, ItemBaseDefinition itemBase)
    {
        int value = ParseLastNumber(text);
        ItemModifierScope local = itemBase.Category is ItemCategory.OneHandWeapon or ItemCategory.TwoHandWeapon
            ? ItemModifierScope.LocalWeapon : ItemModifierScope.LocalDefense;
        if (text.Contains("局部物理", StringComparison.Ordinal)) return (ItemModifierKind.IncreasedPhysicalDamageBasisPoints, value, local);
        if (text.Contains("局部攻击速度", StringComparison.Ordinal)) return (ItemModifierKind.IncreasedAttackSpeedBasisPoints, value, local);
        if (text.Contains("局部护甲", StringComparison.Ordinal)) return (ItemModifierKind.IncreasedArmorBasisPoints, value, local);
        if (text.Contains("局部闪避", StringComparison.Ordinal)) return (ItemModifierKind.IncreasedEvasionBasisPoints, value, local);
        if (text.Contains("局部护盾", StringComparison.Ordinal)) return (ItemModifierKind.IncreasedShieldBasisPoints, value, local);
        if (text.Contains("局部灵障", StringComparison.Ordinal)) return (ItemModifierKind.IncreasedSpiritBarrierBasisPoints, value, local);
        if (text.Contains("最大生命", StringComparison.Ordinal) && text.Contains('+')) return (ItemModifierKind.FlatMaximumLife, value, ItemModifierScope.Global);
        if (text.Contains("命中值", StringComparison.Ordinal)) return (ItemModifierKind.FlatAccuracy, value, ItemModifierScope.Global);
        if (text.Contains("火焰抗性", StringComparison.Ordinal)) return (ItemModifierKind.FireResistanceBasisPoints, value, ItemModifierScope.Global);
        if (text.Contains("冰霜抗性", StringComparison.Ordinal)) return (ItemModifierKind.ColdResistanceBasisPoints, value, ItemModifierScope.Global);
        if (text.Contains("闪电抗性", StringComparison.Ordinal)) return (ItemModifierKind.LightningResistanceBasisPoints, value, ItemModifierScope.Global);
        if (text.Contains("虚空抗性", StringComparison.Ordinal)) return (ItemModifierKind.VoidResistanceBasisPoints, value, ItemModifierScope.Global);
        return (ItemModifierKind.None, value, ItemModifierScope.Rule);
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
