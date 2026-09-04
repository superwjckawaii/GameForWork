using GameForWork.Core.P1.Items;
using GameForWork.Core.Simulation;

namespace GameForWork.Core.Equipment;

public enum EquipmentRuleEvent
{
    Passive,
    DamageTaken,
    DirectCritical,
    WarcryUsed,
    SkillResourcePaid,
    FlaskUsed,
    ProjectileOrMinionHit,
    ProjectileFinished,
    TrapTriggered,
    MinionRemoved,
    CompanionLethalDamage,
    ConstructDestroyed,
    EnemyKilled,
    Hit,
    LethalDamage,
    BattleStarted,
}

public sealed record EquipmentRuleRegistration(
    string RuleId,
    string SourceDefinitionId,
    EquipmentRuleEvent Trigger,
    bool Static,
    string Description);

public sealed record EquipmentRuleContext(
    EquipmentRuleEvent Event,
    ulong Seed,
    int DamageTaken = 0,
    bool PlayerDirectCritical = false,
    bool WarcrySucceeded = false,
    int SkillResourcePaid = 0,
    bool FlaskChargeSpent = false,
    bool ProjectileOrOrdinaryMinionHit = false,
    int LocalArmor = 0,
    int LocalArmorEvasionShieldBarrier = 0,
    int Physique = 0,
    string SourceInstanceId = "",
    string ActionId = "");

public sealed record EquipmentRuleState(
    IReadOnlyDictionary<string, int>? Stacks = null,
    IReadOnlySet<string>? ConsumedKeys = null,
    IReadOnlyDictionary<string, int>? Counters = null)
{
    public IReadOnlyDictionary<string, int> StackValues => Stacks ?? new Dictionary<string, int>();
    public IReadOnlySet<string> Consumed => ConsumedKeys ?? new HashSet<string>();
    public IReadOnlyDictionary<string, int> CounterValues => Counters ?? new Dictionary<string, int>();
}

public sealed record EquipmentRuleOutcome(
    EquipmentRuleState State,
    bool Triggered,
    string Summary = "",
    int Value = 0);

/// <summary>
/// Shared event registry for every enchantment and legendary signature rule.  Systems dispatch
/// one event here, so online, offline and preview callers do not grow definition-specific branches.
/// </summary>
public static class EquipmentRuleRegistry
{
    private static readonly IReadOnlyList<EquipmentRuleRegistration> Values = Build();

    public static IReadOnlyList<EquipmentRuleRegistration> All => Values;

    public static EquipmentRuleRegistration Get(string ruleId) => Values.Single(value => value.RuleId == ruleId);

    private static IReadOnlyList<EquipmentRuleRegistration> Build()
    {
        EquipmentRuleRegistration[] enchantments = EquipmentCatalog.Enchantments.Select(entry => new EquipmentRuleRegistration(
            entry.RuleId, entry.Id, TriggerFor(entry.RuleText), !IsDynamic(entry.RuleText), entry.RuleText)).ToArray();
        EquipmentRuleRegistration[] legendary = EquipmentCatalog.LegendaryItems.Select(entry => new EquipmentRuleRegistration(
            entry.RuleId, entry.Id, TriggerFor(entry.RuleText), !IsDynamic(entry.RuleText), entry.RuleText)).ToArray();
        EquipmentRuleRegistration[] result = enchantments.Concat(legendary).ToArray();
        if (result.Length != 109 || result.Select(value => value.RuleId).Distinct(StringComparer.Ordinal).Count() != 109)
            throw new InvalidOperationException("Equipment rule registry must contain exactly 54 enchantment and 55 legendary rules.");
        return result;
    }

    private static bool IsDynamic(string text) => TriggerFor(text) != EquipmentRuleEvent.Passive;

    private static EquipmentRuleEvent TriggerFor(string text)
    {
        if (text.Contains("受到伤害", StringComparison.Ordinal) || text.Contains("受到未格挡", StringComparison.Ordinal)) return EquipmentRuleEvent.DamageTaken;
        if (text.Contains("直接暴击", StringComparison.Ordinal) || text.Contains("法术暴击", StringComparison.Ordinal)) return EquipmentRuleEvent.DirectCritical;
        if (text.Contains("战吼", StringComparison.Ordinal) && text.Contains("施放", StringComparison.Ordinal)) return EquipmentRuleEvent.WarcryUsed;
        if (text.Contains("支付", StringComparison.Ordinal) || text.Contains("消耗生命", StringComparison.Ordinal)) return EquipmentRuleEvent.SkillResourcePaid;
        if (text.Contains("使用药剂", StringComparison.Ordinal) || text.Contains("药剂使用", StringComparison.Ordinal)) return EquipmentRuleEvent.FlaskUsed;
        if (text.Contains("投射物", StringComparison.Ordinal) && text.Contains("返回", StringComparison.Ordinal)) return EquipmentRuleEvent.ProjectileFinished;
        if (text.Contains("投射物", StringComparison.Ordinal) && text.Contains("命中", StringComparison.Ordinal)) return EquipmentRuleEvent.ProjectileOrMinionHit;
        if (text.Contains("陷阱", StringComparison.Ordinal) && text.Contains("触发", StringComparison.Ordinal)) return EquipmentRuleEvent.TrapTriggered;
        if (text.Contains("召唤物死亡", StringComparison.Ordinal) || text.Contains("召唤物死亡", StringComparison.Ordinal)) return EquipmentRuleEvent.MinionRemoved;
        if (text.Contains("灵兽", StringComparison.Ordinal) && text.Contains("致命", StringComparison.Ordinal)) return EquipmentRuleEvent.CompanionLethalDamage;
        if (text.Contains("构装体被摧毁", StringComparison.Ordinal)) return EquipmentRuleEvent.ConstructDestroyed;
        if (text.Contains("击败", StringComparison.Ordinal) || text.Contains("击杀", StringComparison.Ordinal)) return EquipmentRuleEvent.EnemyKilled;
        if (text.Contains("致命伤害", StringComparison.Ordinal) || text.Contains("濒死", StringComparison.Ordinal)) return EquipmentRuleEvent.LethalDamage;
        if (text.Contains("每场战斗", StringComparison.Ordinal)) return EquipmentRuleEvent.BattleStarted;
        if (text.Contains("命中", StringComparison.Ordinal)) return EquipmentRuleEvent.Hit;
        return EquipmentRuleEvent.Passive;
    }
}

public static class EquipmentRuleEngine
{
    public const string WorldEaterCatalogId = "equipment.legendary.52.44a586da1f";

    public static EquipmentRuleOutcome Dispatch(
        EquipmentRuleRegistration registration,
        EquipmentRuleState state,
        EquipmentRuleContext context,
        int sourceCount = 1)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(context);
        if (registration.Trigger != context.Event || registration.Static) return new(state, false);

        string onceKey = $"{registration.RuleId}|{context.SourceInstanceId}|{context.ActionId}|{context.Event}";
        if (state.Consumed.Contains(onceKey)) return new(state, false);

        string description = registration.Description;
        if (description.Contains("谦逊", StringComparison.Ordinal))
            return GainStack(state, context, onceKey, "谦逊", Math.Min(10, 1 + sourceCount), 1_500 * sourceCount, context.DamageTaken > 0);
        if (description.Contains("傲慢", StringComparison.Ordinal))
            return GainStack(state, context, onceKey, "傲慢", Math.Min(10, 1 + sourceCount), 1_000 * sourceCount, context.PlayerDirectCritical);
        if (description.Contains("暴怒", StringComparison.Ordinal))
            return GainStack(state, context, onceKey, "暴怒", Math.Min(10, 1 + sourceCount), 1_000 * sourceCount, context.WarcrySucceeded);
        if (description.Contains("节制", StringComparison.Ordinal))
            return GainStack(state, context, onceKey, "节制", Math.Min(10, 1 + sourceCount), 1_000 * sourceCount, context.SkillResourcePaid > 0);
        if (description.Contains("慈悲", StringComparison.Ordinal))
            return GainStack(state, context, onceKey, "慈悲", Math.Min(10, 1 + sourceCount), 1_000 * sourceCount, context.FlaskChargeSpent);
        if (description.Contains("懒惰", StringComparison.Ordinal))
            return GainStack(state, context, onceKey, "懒惰", Math.Min(10, 1 + sourceCount), 1_000 * sourceCount, context.ProjectileOrOrdinaryMinionHit);

        var consumed = state.Consumed.ToHashSet(StringComparer.Ordinal);
        consumed.Add(onceKey);
        return new(state with { ConsumedKeys = consumed }, true, description);
    }

    public static (int fireMinimum, int fireMaximum, int coldMinimum, int coldMaximum, int lightningMinimum, int lightningMaximum)
        CopyHighestElementalRange((int min, int max) fire, (int min, int max) cold, (int min, int max) lightning)
    {
        (int min, int max) best = new[] { fire, cold, lightning }
            .OrderByDescending(value => value.min + value.max).First();
        return (best.min, best.max, best.min, best.max, best.min, best.max);
    }

    public static int ImmortalMaximumLife(int finalLocalArmor) => Math.Max(0, finalLocalArmor / 500) * 100;

    public static int UnarmedMoreDamageBasisPoints(int finalLocalDefense) => Math.Max(0, finalLocalDefense / 50) * 500;

    public static (int minimum, int maximum) WorldEaterAddedVoidDamage(int finalPhysique)
    {
        int steps = Math.Max(0, finalPhysique / 100);
        return (checked(steps * 100), checked(steps * 150));
    }

    private static EquipmentRuleOutcome GainStack(EquipmentRuleState state, EquipmentRuleContext context,
        string onceKey, string stack, int maximum, int chanceBasisPoints, bool eligible)
    {
        if (!eligible) return new(state, false);
        var random = new Pcg32(context.Seed);
        if (random.NextBasisPoints() >= Math.Min(10_000, chanceBasisPoints)) return new(state, false);
        var stacks = state.StackValues.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        stacks[stack] = Math.Min(maximum, stacks.GetValueOrDefault(stack) + 1);
        var consumed = state.Consumed.ToHashSet(StringComparer.Ordinal);
        consumed.Add(onceKey);
        return new(state with { Stacks = stacks, ConsumedKeys = consumed }, true, $"获得1层{stack}", stacks[stack]);
    }
}
