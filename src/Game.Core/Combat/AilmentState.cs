using GameForWork.Core.Builds;
using GameForWork.Core.SkillCatalog;

namespace GameForWork.Core.Combat;

public sealed record DamageOverTimeInstance(Ailment Kind, DamageType Type, decimal DamagePerSecond,
    decimal RemainingMilliseconds, string SourceId, bool Propagated = false, string InstanceId = "", decimal? DebuffedDamagePerSecond = null, bool SelfCast = false);
public readonly record struct DamageOverTimePulse(Ailment Kind, DamageType Type, int Damage, bool SelfCast = false);

/// <summary>Attacker snapshots; target defenses are supplied afresh for every simulation step.</summary>
public sealed class AilmentState
{
    private readonly List<DamageOverTimeInstance> _instances = [];
    private readonly Dictionary<(Ailment, DamageType), decimal> _remainders = [];
    private readonly Dictionary<Ailment, (int Count, int Until)> _debuffs = [];
    private readonly HashSet<(AilmentState Source, string Instance)> _received = [];
    private readonly HashSet<(Ailment Kind, string Action)> _settledActions = [];
    private readonly HashSet<string> _poisonCopyActions = [];
    private int _sequence;
    public IReadOnlyList<DamageOverTimeInstance> Instances => _instances;
    public int BleedMaximum { get; set; } = 1;
    public int BleedMultiplier { get; set; } = 10_000;
    public int IgniteMaximum { get; set; } = 1;
    public int IgniteMultiplier { get; set; } = 10_000;
    public void Remove(params Ailment[] kinds)
    {
        _instances.RemoveAll(instance => kinds.Contains(instance.Kind));
        foreach (var key in _remainders.Keys.Where(key => kinds.Contains(key.Item1)).ToArray()) _remainders.Remove(key);
        foreach (var kind in kinds) _debuffs.Remove(kind);
    }

    public void Apply(Ailment kind, DamageType type, decimal dps, int durationMilliseconds,
        int fasterBasisPoints, string sourceId, bool propagated = false, string? instanceId = null, decimal? debuffedDamagePerSecond = null, bool selfCast = false)
    {
        if (dps <= 0 || durationMilliseconds <= 0) return;
        decimal speed = Math.Max(1, 10_000 + fasterBasisPoints) / 10_000m;
        _instances.Add(new(kind, type, dps * speed, durationMilliseconds / speed, sourceId, propagated, instanceId ?? $"dot:{++_sequence}", debuffedDamagePerSecond * speed, selfCast));
    }

    public decimal ApplyPoison(GameForWork.Core.Campaign.Progression.PassiveModifiers passive,
        decimal dps, int duration, int faster, string sourceId, string actionId, bool selfCast, decimal? debuffedDps = null)
    {
        if (dps <= 0 || duration <= 0) return 0;
        bool Has(int option) => MasteryRuntime.Has(passive, "中毒", option);
        bool first = Count(Ailment.Poison) == 0 && Has(6);
        decimal more = (Has(0) ? 1.6m : 1m) * (Has(1) ? .8m : 1m) * (first ? 2m : 1m);
        duration = checked(duration * (10_000 + (Has(1) ? 10_000 : 0) + (first ? 10_000 : 0)) / 10_000);
        if (Has(0)) duration = duration * 3 / 4;
        Apply(Ailment.Poison, DamageType.Void, dps * more, duration, faster, sourceId,
            debuffedDamagePerSecond: debuffedDps * more, selfCast: selfCast);
        if (selfCast && Has(2) && _poisonCopyActions.Add(actionId))
            Apply(Ailment.Poison, DamageType.Void, dps * more * .7m, duration, faster, sourceId,
                debuffedDamagePerSecond: debuffedDps * more * .7m, selfCast: true);
        return dps * more;
    }

    public int Stack(Ailment kind, int tick) => _debuffs.TryGetValue(kind, out var value) && tick < value.Until ? value.Count : 0;
    public void AddStack(Ailment kind, int count, int maximum, int durationTicks, int tick) =>
        _debuffs[kind] = (Math.Min(maximum, Stack(kind, tick) + Math.Max(0, count)), tick + durationTicks);

    private static decimal Dps(DamageOverTimeInstance instance, bool voidDebuffed) =>
        voidDebuffed ? instance.DebuffedDamagePerSecond ?? instance.DamagePerSecond : instance.DamagePerSecond;
    private IEnumerable<DamageOverTimeInstance> Active(bool voidDebuffed = false) => _instances.Where(instance => instance.Kind != Ailment.Ground).GroupBy(instance => instance.Kind)
        .SelectMany(group => group.OrderByDescending(instance => Dps(instance, voidDebuffed)).Take(group.Key switch
        { Ailment.Bleed => BleedMaximum, Ailment.Ignite => IgniteMaximum, _ => int.MaxValue }))
        .Concat(_instances.Where(instance => instance.Kind == Ailment.Ground).GroupBy(instance => instance.SourceId)
            .SelectMany(group => group.GroupBy(instance => instance.InstanceId).MaxBy(candidate => candidate.Sum(instance => Dps(instance, voidDebuffed)))!));
    public int Count(Ailment kind) => Active().Count(instance => instance.Kind == kind);
    public decimal Remaining(Ailment kind) => Active().Where(instance => instance.Kind == kind)
        .Sum(instance => instance.DamagePerSecond * instance.RemainingMilliseconds / 1000 * Multiplier(kind));
    public decimal Consume(Ailment kind, int portionBasisPoints,
        Func<DamageType, decimal, decimal>? defend = null, bool voidDebuffed = false)
    {
        decimal amount = 0;
        foreach (var group in Active(voidDebuffed).Where(instance => instance.Kind == kind).GroupBy(instance => instance.Type))
        {
            decimal dps = group.Sum(instance => Dps(instance, voidDebuffed) * Multiplier(kind));
            decimal remaining = group.Sum(instance => Dps(instance, voidDebuffed) * instance.RemainingMilliseconds / 1000 * Multiplier(kind));
            if (dps > 0) amount += remaining * Math.Max(0, defend?.Invoke(group.Key, dps) ?? dps) / dps;
        }
        _instances.RemoveAll(instance => instance.Kind == kind);
        return amount * Math.Clamp(portionBasisPoints, 0, 10_000) / 10_000;
    }
    public decimal ConsumeForAction(Ailment kind, string actionId, int portionBasisPoints,
        Func<DamageType, decimal, decimal>? defend = null, bool voidDebuffed = false) =>
        _settledActions.Add((kind, actionId)) ? Consume(kind, portionBasisPoints, defend, voidDebuffed) : 0;

    public int ConsumeStacks(Ailment kind, int maximum, int tick)
    {
        if (kind == Ailment.Poison)
        {
            var consumed = _instances.Where(instance => instance.Kind == kind).Take(maximum).ToArray();
            _instances.RemoveAll(consumed.Contains);
            return consumed.Length;
        }
        int count = Math.Min(maximum, Stack(kind, tick));
        if (_debuffs.TryGetValue(kind, out var value)) _debuffs[kind] = (value.Count - count, value.Until);
        return count;
    }
    public void SpreadTo(AilmentState target, Ailment kind, Func<bool>? targetAllows = null, int maximum = int.MaxValue, bool voidDebuffed = false)
    {
        if (ReferenceEquals(this, target)) return;
        foreach (DamageOverTimeInstance instance in Active(voidDebuffed).Where(instance => instance.Kind == kind && !instance.Propagated)
            .OrderByDescending(instance => Dps(instance, voidDebuffed)).Take(Math.Max(0, maximum)))
            if (target._received.Add((this, instance.InstanceId)) && (targetAllows?.Invoke() ?? true))
                target._instances.Add(instance with { Propagated = true });
    }

    public IReadOnlyList<DamageOverTimePulse> Advance(int milliseconds, Func<DamageType, decimal, decimal> defend, bool voidDebuffed = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(milliseconds);
        var output = new List<DamageOverTimePulse>();
        decimal remainingStep = milliseconds;
        // Split at expiry boundaries so a weaker candidate immediately takes over.
        while (remainingStep > 0 && _instances.Count > 0)
        {
            decimal step = Math.Min(remainingStep, _instances.Min(instance => instance.RemainingMilliseconds));
            DamageOverTimeInstance[] active = Active(voidDebuffed).ToArray();
            foreach (var type in active.GroupBy(instance => instance.Type))
            {
                decimal totalDps = type.Sum(instance => Dps(instance, voidDebuffed) * Multiplier(instance.Kind));
                decimal defended = Math.Max(0, defend(type.Key, totalDps));
                foreach (var kind in type.GroupBy(instance => instance.Kind))
                {
                    var key = (kind.Key, type.Key);
                    decimal portion = kind.Sum(instance => Dps(instance, voidDebuffed) * Multiplier(instance.Kind)) / totalDps;
                    decimal amount = defended * portion * step / 1000 + _remainders.GetValueOrDefault(key);
                    int damage = (int)Math.Min(int.MaxValue, decimal.Floor(amount));
                    _remainders[key] = amount - damage;
                    if (damage > 0) output.Add(new(kind.Key, type.Key, damage, kind.Any(instance => instance.SelfCast)));
                }
            }
            for (int index = _instances.Count - 1; index >= 0; index--)
            {
                var instance = _instances[index];
                if (instance.RemainingMilliseconds <= step) _instances.RemoveAt(index);
                else _instances[index] = instance with { RemainingMilliseconds = instance.RemainingMilliseconds - step };
            }
            remainingStep -= step;
        }
        return output;
    }
    private decimal Multiplier(Ailment kind) => kind is not (Ailment.Bleed or Ailment.Ignite) || Count(kind) <= 1 ? 1m : (kind switch
    { Ailment.Bleed => BleedMultiplier, Ailment.Ignite => IgniteMultiplier, _ => 10_000 }) / 10_000m;
}
