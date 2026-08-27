namespace GameForWork.Core.Simulation;

public readonly record struct FixedPoint(int Raw) : IComparable<FixedPoint>
{
    public const int Scale = 1_000;

    public static FixedPoint FromTiles(int tiles) => new(checked(tiles * Scale));

    public static FixedPoint FromRatio(int numerator, int denominator) =>
        new(checked((int)((long)numerator * Scale / denominator)));

    public int CompareTo(FixedPoint other) => Raw.CompareTo(other.Raw);

    public static FixedPoint operator +(FixedPoint left, FixedPoint right) =>
        new(checked(left.Raw + right.Raw));

    public static FixedPoint operator -(FixedPoint left, FixedPoint right) =>
        new(checked(left.Raw - right.Raw));

    public static FixedPoint operator *(FixedPoint value, int multiplier) =>
        new(checked(value.Raw * multiplier));

    public static FixedPoint operator /(FixedPoint value, int divisor) =>
        new(value.Raw / divisor);

    public static bool operator <(FixedPoint left, FixedPoint right) => left.Raw < right.Raw;
    public static bool operator >(FixedPoint left, FixedPoint right) => left.Raw > right.Raw;
    public static bool operator <=(FixedPoint left, FixedPoint right) => left.Raw <= right.Raw;
    public static bool operator >=(FixedPoint left, FixedPoint right) => left.Raw >= right.Raw;
}

public static class IntegerMath
{
    public static long Square(long value) => checked(value * value);

    public static int SquareRoot(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        if (value == 0)
        {
            return 0;
        }

        long result = 0;
        long bit = 1L << 62;
        while (bit > value)
        {
            bit >>= 2;
        }

        while (bit != 0)
        {
            if (value >= result + bit)
            {
                value -= result + bit;
                result = (result >> 1) + bit;
            }
            else
            {
                result >>= 1;
            }

            bit >>= 2;
        }

        return checked((int)result);
    }

    public static int Distance(int x1, int y1, int x2, int y2)
    {
        long dx = (long)x2 - x1;
        long dy = (long)y2 - y1;
        return SquareRoot(checked(Square(dx) + Square(dy)));
    }
}
