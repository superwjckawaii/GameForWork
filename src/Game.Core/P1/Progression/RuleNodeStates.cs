namespace GameForWork.Core.P1.Progression;

public sealed class ChargedHeavyStrikeState
{
    public const int ChargeIntervalTicks = 10;
    public const int MaximumCharges = 3;
    public const int MoreDamagePerChargeBasisPoints = 1_200;

    private int _lastObservedTick;
    private int _remainderTicks;

    public int Charges { get; private set; }

    public void AdvanceWithoutAttacking(int tick)
    {
        if (tick < _lastObservedTick)
        {
            throw new ArgumentOutOfRangeException(nameof(tick));
        }

        int elapsed = checked(tick - _lastObservedTick);
        _lastObservedTick = tick;
        _remainderTicks = checked(_remainderTicks + elapsed);
        int gained = _remainderTicks / ChargeIntervalTicks;
        _remainderTicks %= ChargeIntervalTicks;
        Charges = Math.Min(MaximumCharges, checked(Charges + gained));
    }

    public int ConsumeForHeavyStrike(int tick)
    {
        AdvanceWithoutAttacking(tick);
        int multiplier = checked(10_000 + (Charges * MoreDamagePerChargeBasisPoints));
        Charges = 0;
        _remainderTicks = 0;
        return multiplier;
    }

    public void RecordOtherAttack(int tick)
    {
        if (tick < _lastObservedTick)
        {
            throw new ArgumentOutOfRangeException(nameof(tick));
        }

        _lastObservedTick = tick;
        _remainderTicks = 0;
        Charges = 0;
    }
}
