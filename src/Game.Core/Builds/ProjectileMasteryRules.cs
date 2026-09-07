using GameForWork.Core.Campaign.Progression;

namespace GameForWork.Core.Builds;

public sealed record ProjectileMechanics(
    int SpeedMultiplierBasisPoints = 10_000,
    int HitMultiplierBasisPoints = 10_000,
    bool TracksTargets = false,
    bool SequentialVolley = false,
    bool InfinitePierce = false,
    int PierceStepMultiplierBasisPoints = 10_000,
    int ForkCount = 0,
    int ForkMultiplierBasisPoints = 10_000,
    int AdditionalChains = 0,
    int ChainRangeMultiplierBasisPoints = 10_000,
    int ChainStepMultiplierBasisPoints = 10_000,
    bool Returns = false,
    int ReturnMultiplierBasisPoints = 10_000)
{
    public static ProjectileMechanics Default { get; } = new();

    public int AfterPierce(int multiplier) => Apply(multiplier, PierceStepMultiplierBasisPoints);
    public int AfterFork(int multiplier) => Apply(multiplier, ForkMultiplierBasisPoints);
    public int AfterChain(int multiplier) => Apply(multiplier, ChainStepMultiplierBasisPoints);
    public int OnReturn(int multiplier) => Apply(multiplier, ReturnMultiplierBasisPoints);

    private static int Apply(int value, int multiplier) => checked(value * multiplier / 10_000);
}

public static class ProjectileMasteryRules
{
    public static ProjectileMechanics Resolve(PassiveModifiers passives)
    {
        bool Has(int option) => MasteryRuntime.Has(passives, "投射物", option);
        int hitMultiplier = 10_000;
        if (Has(0)) hitMultiplier = Multiply(hitMultiplier, 15_000);
        if (Has(1)) hitMultiplier = Multiply(hitMultiplier, 8_000);
        if (Has(2)) hitMultiplier = Multiply(hitMultiplier, 5_000);
        return new(
            SpeedMultiplierBasisPoints: Has(0) ? 5_000 : 10_000,
            HitMultiplierBasisPoints: hitMultiplier,
            TracksTargets: Has(1),
            SequentialVolley: Has(2),
            InfinitePierce: Has(3),
            PierceStepMultiplierBasisPoints: Has(3) ? 11_000 : 10_000,
            ForkCount: Has(4) ? 4 : 0,
            ForkMultiplierBasisPoints: Has(4) ? 6_000 : 10_000,
            AdditionalChains: Has(5) ? 2 : 0,
            ChainRangeMultiplierBasisPoints: Has(5) ? 15_000 : 10_000,
            ChainStepMultiplierBasisPoints: Has(5) ? 8_500 : 10_000,
            Returns: Has(6),
            ReturnMultiplierBasisPoints: Has(6) ? 7_500 : 10_000);
    }

    private static int Multiply(int left, int right) => checked(left * right / 10_000);
}
