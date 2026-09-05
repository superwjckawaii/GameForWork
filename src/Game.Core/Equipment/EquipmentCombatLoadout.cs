using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.SkillCatalog;

namespace GameForWork.Core.Equipment;

/// <summary>Immutable equipment input carried with the team into online and offline simulation.</summary>
public sealed record EquipmentCombatLoadout(
    IReadOnlyDictionary<ItemModifierKind, int> Modifiers,
    IReadOnlyList<string> LegendaryIds,
    IReadOnlyDictionary<string, int> Enchantments,
    int ShieldArmor = 0,
    int UnarmedMoreDamageBasisPoints = 0,
    int PhysicalIncreaseIncludedInAttack = 0, IReadOnlyList<Combat.EquippedFlask>? Flasks = null)
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
        }), summary.Modifiers.IncreasedPhysicalDamageBasisPoints,
        loadout.Items.Where(pair => pair.Key is >= EquipmentSlot.Flask1 and <= EquipmentSlot.Flask5)
            .Where(pair => FlaskRules.KindForBase(pair.Value.Base.StableId).HasValue)
            .Select(pair => new Combat.EquippedFlask(FlaskRules.KindForBase(pair.Value.Base.StableId)!.Value, pair.Value.InstanceId, (int)pair.Key,
                pair.Value.EffectiveImplicitComponents.Concat(pair.Value.Affixes.SelectMany(affix => affix.Effects)).Concat(pair.Value.CorruptionComponents)
                    .Select(effect => (effect.Kind, effect.Value, effect.Scope))
                    .Concat((pair.Value.Enchantment?.EffectComponents ?? []).Select(effect => (effect.Kind, Value: effect.MinimumValue, effect.Scope)))
                    .Where(effect => effect.Scope is ItemModifierScope.Flask or ItemModifierScope.Rule).GroupBy(effect => effect.Kind)
                    .ToDictionary(group => group.Key, group => group.Sum(effect => effect.Value)), pair.Value.Quality)).ToArray());

    public int Penetration(SkillDamageType type) => Value(type switch
    {
        SkillDamageType.Fire => ItemModifierKind.FirePenetrationBasisPoints,
        SkillDamageType.Cold => ItemModifierKind.ColdPenetrationBasisPoints,
        SkillDamageType.Lightning => ItemModifierKind.LightningPenetrationBasisPoints,
        SkillDamageType.Void => ItemModifierKind.VoidPenetrationBasisPoints,
        _ => ItemModifierKind.None,
    });
}
