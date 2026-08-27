namespace GameForWork.Core.P1.Combat;

public sealed record BleedInstance(
    ulong SourceActorId,
    int TotalDamage,
    int RemainingDamage,
    int StartTick,
    int EndTick,
    int DamageRemainder = 0);

public sealed class BleedCollection
{
    private readonly List<BleedInstance> _instances = [];

    public BleedCollection(bool deepWoundAllocated = false, bool fasterBleedingAllocated = false)
    {
        DeepWoundAllocated = deepWoundAllocated;
        FasterBleedingAllocated = fasterBleedingAllocated;
    }

    public bool DeepWoundAllocated { get; }
    public bool FasterBleedingAllocated { get; }
    public IReadOnlyList<BleedInstance> Instances => _instances;

    public void Apply(ulong sourceActorId, int totalDamage, int currentTick, int durationTicks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(totalDamage);
        if (durationTicks <= 0 || totalDamage == 0)
        {
            return;
        }

        int adjustedTotal = DeepWoundAllocated ? checked(totalDamage * 6_000 / 10_000) : totalDamage;
        int adjustedDuration = FasterBleedingAllocated
            ? Math.Max(1, checked(durationTicks * 10_000 / 12_000))
            : durationTicks;
        int maximumLayers = DeepWoundAllocated ? 2 : 1;
        BleedInstance[] sameSource = _instances
            .Where(instance => instance.SourceActorId == sourceActorId)
            .OrderBy(instance => instance.TotalDamage)
            .ThenBy(instance => instance.StartTick)
            .ToArray();

        if (sameSource.Length >= maximumLayers)
        {
            BleedInstance weakest = sameSource[0];
            if (weakest.TotalDamage >= adjustedTotal)
            {
                return;
            }

            _instances.Remove(weakest);
        }

        _instances.Add(new BleedInstance(
            sourceActorId,
            adjustedTotal,
            adjustedTotal,
            currentTick,
            checked(currentTick + adjustedDuration)));
    }

    public int AdvanceTick(int tick)
    {
        int damage = 0;
        for (int index = _instances.Count - 1; index >= 0; index--)
        {
            BleedInstance instance = _instances[index];
            int duration = instance.EndTick - instance.StartTick;
            int remainder = checked(instance.DamageRemainder + instance.TotalDamage);
            int tickDamage = remainder / duration;
            remainder %= duration;
            tickDamage = Math.Min(tickDamage, instance.RemainingDamage);
            damage = checked(damage + tickDamage);
            int remaining = instance.RemainingDamage - tickDamage;
            if (tick + 1 >= instance.EndTick || remaining <= 0)
            {
                damage = checked(damage + remaining);
                _instances.RemoveAt(index);
            }
            else
            {
                _instances[index] = instance with { RemainingDamage = remaining, DamageRemainder = remainder };
            }
        }

        return damage;
    }
}
