using GameForWork.Core.Simulation;

namespace GameForWork.Tests;

public sealed class FixedPointAndRandomTests
{
    [Fact]
    public void FixedPointArithmeticUsesThousandths()
    {
        FixedPoint value = FixedPoint.FromTiles(2) + FixedPoint.FromRatio(1, 2);
        Assert.Equal(2_500, value.Raw);
        Assert.Equal(1_250, (value / 2).Raw);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(15, 3)]
    [InlineData(16, 4)]
    [InlineData(17, 4)]
    [InlineData(1_000_000, 1_000)]
    public void IntegerSquareRootFloors(long value, int expected)
    {
        Assert.Equal(expected, IntegerMath.SquareRoot(value));
    }

    [Fact]
    public void Pcg32MatchesReferenceVector()
    {
        var random = new Pcg32(42, 54);
        uint[] actual = Enumerable.Range(0, 5).Select(_ => random.NextUInt()).ToArray();
        uint[] expected = [0xa15c02b7, 0x7b47f409, 0xba1d3330, 0x83d2f293, 0xbfa4784b];
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void PcgStateCanBeRestoredExactly()
    {
        var random = new Pcg32(1234);
        _ = random.NextUInt();
        Pcg32 restored = Pcg32.Restore(random.State, random.Increment);
        Assert.Equal(random.NextUInt(), restored.NextUInt());
    }
}
