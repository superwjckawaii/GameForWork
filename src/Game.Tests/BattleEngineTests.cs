using GameForWork.Core.Combat;
using GameForWork.Core.Simulation;

namespace GameForWork.Tests;

public sealed class BattleEngineTests
{
    [Fact]
    public void CommandsAreResolvedByStableActorId()
    {
        BattleState state = CreateDuel(hitChance: 10_000, windupTicks: 1);
        var engine = new BattleEngine();
        BattleCommand[] commands =
        [
            Attack(state, 2, 1),
            Attack(state, 1, 2),
        ];

        BattleEvent[] events = engine.Step(state, commands).Where(item => item.Kind == BattleEventKind.CastStarted).ToArray();

        Assert.Equal([1UL, 2UL], events.Select(item => item.ActorId));
    }

    [Fact]
    public void MovementUsesTwentyHertzSpeed()
    {
        BattleState state = P0BattleFactory.Create(1);
        var engine = new BattleEngine();
        ActorState hero = state.Actors[1];

        engine.Step(state, [new BattleCommand(0, 1, BattleCommandKind.MoveTo, 10_000, 6_000)]);

        Assert.Equal(2_150, hero.XRaw);
        Assert.Equal(6_000, hero.YRaw);
    }

    [Fact]
    public void OutOfRangeAttackDoesNotStart()
    {
        BattleState state = P0BattleFactory.Create(1);
        var engine = new BattleEngine();

        IReadOnlyList<BattleEvent> events = engine.Step(state, [Attack(state, 1, 2)]);

        Assert.DoesNotContain(events, item => item.Kind == BattleEventKind.CastStarted);
    }

    [Fact]
    public void WindupDelaysDamageAndMissStartsFullCooldown()
    {
        BattleState state = CreateDuel(hitChance: 0, windupTicks: 2);
        var engine = new BattleEngine();
        engine.Step(state, [Attack(state, 1, 2)]);
        engine.Step(state, []);
        IReadOnlyList<BattleEvent> resolved = engine.Step(state, []);

        Assert.Contains(resolved, item => item.Kind == BattleEventKind.HitResolved && !item.Success);
        Assert.Equal(state.Actors[1].CooldownTicks, state.Actors[1].CooldownRemainingTicks);
        Assert.Equal(state.Actors[2].MaxLife, state.Actors[2].Life);
    }

    [Fact]
    public void ArmorReducesDamageWithMinimumOne()
    {
        BattleState state = CreateDuel(hitChance: 10_000, windupTicks: 0);
        state.Actors[1].Life = 100;
        var engine = new BattleEngine();

        engine.Step(state, [Attack(state, 1, 2)]);

        Assert.Equal(70 - 10, state.Actors[2].Life);
    }

    [Fact]
    public void SimultaneousLethalHitsAreDraw()
    {
        BattleState state = CreateDuel(hitChance: 10_000, windupTicks: 0);
        state.Actors[1].Life = 1;
        state.Actors[2].Life = 1;
        var engine = new BattleEngine();

        IReadOnlyList<BattleEvent> events = engine.Step(state, [Attack(state, 2, 1), Attack(state, 1, 2)]);

        Assert.Equal(BattleOutcome.Draw, state.Outcome);
        Assert.Equal(2, events.Count(item => item.Kind == BattleEventKind.ActorDied));
    }

    [Fact]
    public void AutomaticBattleAlwaysFinishes()
    {
        BattleRun run = new BattleRunner().RunAutomatic(99);
        Assert.NotEqual(BattleOutcome.Running, run.FinalState.Outcome);
        Assert.InRange(run.FinalState.Tick, 1, BattleState.MaxTicks);
    }

    [Fact]
    public void ThousandSeedsFinishWithoutInvalidState()
    {
        var runner = new BattleRunner();
        for (ulong seed = 0; seed < 1_000; seed++)
        {
            BattleRun run = runner.RunAutomatic(seed);
            Assert.NotEqual(BattleOutcome.Running, run.FinalState.Outcome);
            Assert.All(run.FinalState.Actors.Values, actor => Assert.InRange(actor.Life, 0, actor.MaxLife));
        }
    }

    private static BattleCommand Attack(BattleState state, ulong actorId, ulong targetId) =>
        new(state.Tick, actorId, BattleCommandKind.UseSkill, TargetActorId: targetId);

    private static BattleState CreateDuel(int hitChance, int windupTicks)
    {
        BattleState state = P0BattleFactory.Create(7);
        state.Actors[1].XRaw = 5 * FixedPoint.Scale;
        state.Actors[1].YRaw = 5 * FixedPoint.Scale;
        state.Actors[2].XRaw = 6 * FixedPoint.Scale;
        state.Actors[2].YRaw = 5 * FixedPoint.Scale;
        foreach (ActorState actor in state.Actors.Values)
        {
            actor.HitChanceBasisPoints = hitChance;
            actor.WindupTicks = windupTicks;
        }

        return state;
    }
}
