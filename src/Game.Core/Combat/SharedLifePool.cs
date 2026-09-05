namespace GameForWork.Core.Combat;

/// <summary>A group's shared life, preserving its percentage when membership changes.</summary>
public sealed class SharedLifePool
{
    public int Capacity { get; private set; }
    public int Life { get; private set; }
    public void Resize(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);
        Life = Capacity == 0 ? capacity : (int)(((long)capacity * Life + Capacity - 1) / Capacity);
        Capacity = capacity;
    }
    public void Damage(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        Life = Math.Max(0, Life - amount);
    }
    public int MemberLife(int maximum) => Life == 0 || Capacity == 0 ? 0 :
        Math.Max(1, (int)((long)maximum * Life / Capacity));
}
