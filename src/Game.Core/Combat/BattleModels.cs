using GameForWork.Core.Simulation;

namespace GameForWork.Core.Combat;

public enum Team
{
    Hero = 0,
    Enemy = 1,
}

public enum BattleOutcome
{
    Running = 0,
    HeroVictory = 1,
    EnemyVictory = 2,
    Draw = 3,
    Timeout = 4,
}

public sealed class ActorState
{
    public required ulong Id { get; init; }
    public required string Name { get; init; }
    public required Team Team { get; init; }
    public required int XRaw { get; set; }
    public required int YRaw { get; set; }
    public required int Life { get; set; }
    public required int MaxLife { get; init; }
    public required int SpeedRawPerSecond { get; init; }
    public required int Damage { get; init; }
    public required int Armor { get; init; }
    public required int HitChanceBasisPoints { get; set; }
    public required int RangeRaw { get; init; }
    public required int WindupTicks { get; set; }
    public required int CooldownTicks { get; init; }
    public int CooldownRemainingTicks { get; set; }
    public int CastResolveTick { get; set; } = -1;
    public ulong CastTargetId { get; set; }

    public bool IsAlive => Life > 0;
    public bool IsCasting => CastResolveTick >= 0;

    public ActorState Clone() => (ActorState)MemberwiseClone();
}

public sealed class BattleState
{
    public const int TicksPerSecond = 20;
    public const int MaxTicks = 600;

    public int Tick { get; set; }
    public BattleOutcome Outcome { get; set; }
    public ulong Seed { get; init; }
    public required Pcg32 Random { get; set; }
    public required SortedDictionary<ulong, ActorState> Actors { get; init; }

    public bool IsFinished => Outcome != BattleOutcome.Running;

    public BattleState Clone() => new()
    {
        Tick = Tick,
        Outcome = Outcome,
        Seed = Seed,
        Random = Random.Clone(),
        Actors = new SortedDictionary<ulong, ActorState>(Actors.ToDictionary(pair => pair.Key, pair => pair.Value.Clone())),
    };
}

public static class BattleFactory
{
    public static BattleState Create(ulong seed) => new()
    {
        Tick = 0,
        Outcome = BattleOutcome.Running,
        Seed = seed,
        Random = new Pcg32(seed),
        Actors = new SortedDictionary<ulong, ActorState>
        {
            [1] = new ActorState
            {
                Id = 1,
                Name = "Hero",
                Team = Team.Hero,
                XRaw = 2 * FixedPoint.Scale,
                YRaw = 6 * FixedPoint.Scale,
                Life = 100,
                MaxLife = 100,
                SpeedRawPerSecond = 3 * FixedPoint.Scale,
                Damage = 12,
                Armor = 3,
                HitChanceBasisPoints = 9_000,
                RangeRaw = 1_250,
                WindupTicks = 6,
                CooldownTicks = 20,
            },
            [2] = new ActorState
            {
                Id = 2,
                Name = "Ravener",
                Team = Team.Enemy,
                XRaw = 10 * FixedPoint.Scale,
                YRaw = 6 * FixedPoint.Scale,
                Life = 70,
                MaxLife = 70,
                SpeedRawPerSecond = 2_500,
                Damage = 8,
                Armor = 2,
                HitChanceBasisPoints = 8_500,
                RangeRaw = 1_250,
                WindupTicks = 8,
                CooldownTicks = 24,
            },
        },
    };
}
