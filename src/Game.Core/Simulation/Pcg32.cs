namespace GameForWork.Core.Simulation;

public sealed class Pcg32
{
    private const ulong Multiplier = 6364136223846793005UL;
    private ulong _state;
    private readonly ulong _increment;

    public Pcg32(ulong seed, ulong sequence = 54UL)
    {
        _increment = (sequence << 1) | 1UL;
        _state = 0;
        NextUInt();
        _state = unchecked(_state + seed);
        NextUInt();
    }

    private Pcg32(ulong state, ulong increment, bool _)
    {
        _state = state;
        _increment = increment;
    }

    public ulong State => _state;
    public ulong Increment => _increment;

    public uint NextUInt()
    {
        ulong oldState = _state;
        _state = unchecked(oldState * Multiplier + _increment);
        uint xorShifted = (uint)(((oldState >> 18) ^ oldState) >> 27);
        int rotation = (int)(oldState >> 59);
        return (xorShifted >> rotation) | (xorShifted << ((-rotation) & 31));
    }

    public int NextBasisPoints() => (int)(NextUInt() % 10_000U);

    public Pcg32 Clone() => new(_state, _increment, true);

    public static Pcg32 Restore(ulong state, ulong increment) => new(state, increment, true);
}
