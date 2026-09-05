using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Combat;
using GameForWork.Core.Skills;
using GameForWork.Core.Spatial;

namespace GameForWork.Tests;

public sealed partial class CombatClosureTests
{
    [Fact]
    public void PhantomChannelRecordsOneCompletedActionAndPreservesPulseTiming()
    {
        var queue = new CombatActionQueue();
        queue.Record("attack", RecordedAttack().LatestAttack!.Hits[0], 0, false);
        queue.CompleteReady(500, new HashSet<string>(), false, true);
        var config = new SkillConfiguration("archetypes.skill.shield_drain", SkillSupport.None);
        var hit = RecordedAttack().LatestAttack!.Hits[0] with { Skill = CombatSkillRules.Resolve(config, 1_000), Configuration = config };
        for (int tick = 20; tick <= 35; tick += 5)
        {
            queue.Record($"pulse-{tick}", hit, tick, false);
            queue.CompleteReady(tick * 50, new HashSet<string>(), false, true);
            Assert.Empty(queue.Pending);
        }
        // Switching from an attack to the completed channel creates one copy containing four timed pulses.
        queue.CompleteReady(2_100, new HashSet<string>(), false, true);
        Assert.Equal(4, queue.Pending.Count);
        Assert.All(queue.Pending, copy => Assert.Equal("pulse-20", copy.Action.Id));
        Assert.Equal(new[] { 2_350, 2_600, 2_850, 3_100 }, queue.Pending.Select(copy => copy.DueMilliseconds));
        Assert.Single(queue.TakeDue(2_350));
        Assert.Empty(queue.TakeDue(2_599));
        Assert.Single(queue.TakeDue(2_600));
    }

    [Fact]
    public void PhantomReplayClockRespondsToCurrentWarsongRangeAndRetainsPerSkillCooldown()
    {
        var action = RecordedAttack().LatestAttack!;
        var second = action with { SkillId = SkillIds.EarthCleave, Id = "second" };
        var queue = new CombatActionQueue();
        queue.SpawnPhantom(new(1_000, 0), 0, 200, 1, 2_000, memory: [action, second, action]);
        var buffs = new CombatBuffState();
        buffs.Activate(new("archetypes.skill.soul_warsong", SkillSupport.None), false, 0);
        Point hero = new(0, 0);
        int Speed(Point unit) => buffs.ForUnit(0, hero, unit).ActionSpeed;
        Assert.Single(queue.TakeDue(0, Speed));
        Assert.Empty(queue.TakeDue(350, Speed));
        Assert.Equal("second", Assert.Single(queue.TakeDue(400, Speed)).Action.Id);
        Assert.Empty(queue.TakeDue(800, Speed));
        Assert.Empty(queue.TakeDue(950, Speed));
        Assert.Equal(action.Id, Assert.Single(queue.TakeDue(1_000, Speed)).Action.Id);

        queue.SpawnPhantom(new(1_000, 0), 30, 200, 1, 2_000, memory: [action, second]);
        Assert.Single(queue.TakeDue(1_500, Speed));
        Assert.Empty(queue.TakeDue(1_700, Speed));
        hero = new(20_000, 0);
        Assert.Empty(queue.TakeDue(1_900, Speed));
        Assert.Single(queue.TakeDue(1_950, Speed));
    }
}
