using GameForWork.Core.Builds;
using GameForWork.Core.SkillCatalog;

namespace GameForWork.Core.Combat;

public sealed record DamageOverTimeInstance(Ailment Kind, DamageType Type, decimal DamagePerSecond,
    decimal RemainingMilliseconds, string SourceId, bool Propagated = false);
public readonly record struct DamageOverTimePulse(Ailment Kind, DamageType Type, int Damage);

/// <summary>Attacker snapshots; target defenses are supplied afresh for every simulation step.</summary>
public sealed class AilmentState
{
    private readonly List<DamageOverTimeInstance> _instances = [];
    private readonly Dictionary<(Ailment, DamageType), decimal> _remainders = [];
    private readonly Dictionary<Ailment, (int Count, int Until)> _debuffs = [];
    public IReadOnlyList<DamageOverTimeInstance> Instances => _instances;
    public int BleedMaximum { get; set; } = 1;
    public int BleedMultiplier { get; set; } = 10_000;
    public int IgniteMaximum { get; set; } = 1;
    public int IgniteMultiplier { get; set; } = 10_000;

    public void Apply(Ailment kind, DamageType type, decimal dps, int durationMilliseconds,
        int fasterBasisPoints, string sourceId, bool propagated = false)
    {
        if (dps <= 0 || durationMilliseconds <= 0) return;
        decimal speed = Math.Max(1, 10_000 + fasterBasisPoints) / 10_000m;
        _instances.Add(new(kind, type, dps * speed, durationMilliseconds / speed, sourceId, propagated));
    }

    public int Stack(Ailment kind, int tick) => _debuffs.TryGetValue(kind, out var value) && tick < value.Until ? value.Count : 0;
    public void AddStack(Ailment kind, int count, int maximum, int durationTicks, int tick) =>
        _debuffs[kind] = (Math.Min(maximum, Stack(kind, tick) + Math.Max(0, count)), tick + durationTicks);

    private IEnumerable<DamageOverTimeInstance> Active() => _instances.GroupBy(instance => instance.Kind)
        .SelectMany(group => group.OrderByDescending(instance => instance.DamagePerSecond).Take(group.Key switch
        { Ailment.Bleed => BleedMaximum, Ailment.Ignite => IgniteMaximum, _ => int.MaxValue }));
    public int Count(Ailment kind) => Active().Count(instance => instance.Kind == kind);
    public decimal Remaining(Ailment kind) => Active().Where(instance => instance.Kind == kind)
        .Sum(instance => instance.DamagePerSecond * instance.RemainingMilliseconds / 1000 * Multiplier(kind));
    public decimal Consume(Ailment kind, int portionBasisPoints)
    {
        DamageOverTimeInstance[] active = Active().Where(instance => instance.Kind == kind).ToArray();
        decimal amount = Remaining(kind) * Math.Clamp(portionBasisPoints, 0, 10_000) / 10_000;
        _instances.RemoveAll(active.Contains);
        return amount;
    }
    public void Clear(params Ailment[] kinds) => _instances.RemoveAll(instance => kinds.Contains(instance.Kind));
    public void SpreadTo(AilmentState target, Ailment kind)
    {
        foreach (DamageOverTimeInstance instance in Active().Where(instance => instance.Kind == kind && !instance.Propagated))
            target._instances.Add(instance with { Propagated = true });
    }

    public IReadOnlyList<DamageOverTimePulse> Advance(int milliseconds, Func<DamageType, decimal, decimal> defend)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(milliseconds);
        var output = new List<DamageOverTimePulse>();
        decimal remainingStep = milliseconds;
        // Split at expiry boundaries so a weaker candidate immediately takes over.
        while (remainingStep > 0 && _instances.Count > 0)
        {
            decimal step = Math.Min(remainingStep, _instances.Min(instance => instance.RemainingMilliseconds));
            DamageOverTimeInstance[] active = Active().ToArray();
            foreach (var type in active.GroupBy(instance => instance.Type))
            {
                decimal totalDps = type.Sum(instance => instance.DamagePerSecond * Multiplier(instance.Kind));
                decimal defended = Math.Max(0, defend(type.Key, totalDps));
                foreach (var kind in type.GroupBy(instance => instance.Kind))
                {
                    var key = (kind.Key, type.Key);
                    decimal portion = kind.Sum(instance => instance.DamagePerSecond * Multiplier(instance.Kind)) / totalDps;
                    decimal amount = defended * portion * step / 1000 + _remainders.GetValueOrDefault(key);
                    int damage = (int)Math.Min(int.MaxValue, decimal.Floor(amount));
                    _remainders[key] = amount - damage;
                    if (damage > 0) output.Add(new(kind.Key, type.Key, damage));
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
    private decimal Multiplier(Ailment kind) => (kind switch
    { Ailment.Bleed => BleedMultiplier, Ailment.Ignite => IgniteMultiplier, _ => 10_000 }) / 10_000m;
}
