using GameForWork.Core.P1.Items;
using GameForWork.Core.P1.Combat;
using GameForWork.Core.P17;

namespace GameForWork.Core.Equipment;

/// <summary>Immutable equipment input carried with the team into online and offline simulation.</summary>
public sealed record EquipmentCombatLoadout(
    IReadOnlyDictionary<ItemModifierKind, int> Modifiers,
    IReadOnlyList<string> LegendaryIds,
    IReadOnlyDictionary<string, int> Enchantments,
    int ShieldArmor = 0,
    int UnarmedMoreDamageBasisPoints = 0,
    int PhysicalIncreaseIncludedInAttack = 0)
{
    public static EquipmentCombatLoadout Empty { get; } = new(
        new Dictionary<ItemModifierKind, int>(), [], new Dictionary<string, int>());
    private static readonly IReadOnlyDictionary<string, string> LegendaryNames = EquipmentCatalog.LegendaryItems
        .ToDictionary(item => item.DisplayName, item => item.Id, StringComparer.Ordinal);
    public int Value(ItemModifierKind kind) => Modifiers.GetValueOrDefault(kind);
    public bool Has(string name) => LegendaryNames.TryGetValue(name, out string? id) && LegendaryIds.Contains(id);
    public int EnchantmentCount(string name) => Enchantments.GetValueOrDefault(name);
    public static EquipmentCombatLoadout From(EquipmentLoadout loadout, EquipmentSummary summary) => new(
        summary.Modifiers.Extended ?? new Dictionary<ItemModifierKind, int>(),
        loadout.Items.Values.Select(item => item.LegendaryCatalogId).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToArray(),
        loadout.Items.Values.Where(item => item.Enchantment is not null)
            .GroupBy(item => item.Enchantment!.DisplayName).ToDictionary(group => group.Key, group => group.Count()),
        summary.ShieldArmor,
        loadout.Items.Values.Where(item => item.Enchantment?.DisplayName == "空明王印").Sum(item =>
        {
            var defense = EquipmentLoadout.CalculateLocalDefense(item);
            return EquipmentRuleEngine.UnarmedMoreDamageBasisPoints(defense.Armor + defense.Evasion + defense.Shield + defense.SpiritBarrier);
        }), summary.Modifiers.IncreasedPhysicalDamageBasisPoints);

    public int DamageIncrease(SkillTag tags, P17DamageType type, bool damageOverTime)
    {
        int value = type switch
        {
            P17DamageType.Fire => Value(ItemModifierKind.IncreasedFireDamageBasisPoints),
            P17DamageType.Cold => Value(ItemModifierKind.IncreasedColdDamageBasisPoints),
            P17DamageType.Lightning => Value(ItemModifierKind.IncreasedLightningDamageBasisPoints),
            P17DamageType.Void => Value(ItemModifierKind.IncreasedVoidDamageBasisPoints),
            P17DamageType.Physical => Value(ItemModifierKind.IncreasedPhysicalDamageBasisPoints),
            _ => 0,
        };
        if (type is P17DamageType.Fire or P17DamageType.Cold or P17DamageType.Lightning)
            value += Value(ItemModifierKind.IncreasedElementalDamageBasisPoints);
        if (tags.HasFlag(SkillTag.Melee)) value += Value(ItemModifierKind.IncreasedMeleeDamageBasisPoints);
        if (tags.HasFlag(SkillTag.Projectile)) value += Value(ItemModifierKind.IncreasedProjectileDamageBasisPoints);
        if (tags.HasFlag(SkillTag.Area)) value += Value(ItemModifierKind.IncreasedAreaDamageBasisPoints);
        if (damageOverTime) value += Value(ItemModifierKind.IncreasedDamageOverTimeBasisPoints);
        return value;
    }
    public int Penetration(P17DamageType type) => Value(type switch
    {
        P17DamageType.Fire => ItemModifierKind.FirePenetrationBasisPoints,
        P17DamageType.Cold => ItemModifierKind.ColdPenetrationBasisPoints,
        P17DamageType.Lightning => ItemModifierKind.LightningPenetrationBasisPoints,
        P17DamageType.Void => ItemModifierKind.VoidPenetrationBasisPoints,
        _ => ItemModifierKind.None,
    });
}
