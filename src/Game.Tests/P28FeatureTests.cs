using System.Text.Json;
using GameForWork.Core.P1;
using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P1.World;
using GameForWork.Core.P2;
using GameForWork.Core.P3;
using GameForWork.Core.P4;
using GameForWork.Core.P10;
using GameForWork.Core.P12;
using GameForWork.Core.P14;
using GameForWork.Core.P20;
using GameForWork.Core.P26;
using GameForWork.Core.P28;

namespace GameForWork.Tests;

public sealed class P28FeatureTests
{
    private static string[] Atlas => P26AtlasCatalog.All.Select(n => n.StableId).ToArray();
    private static P1MapItem Map(MapRoute route, P12MapAltar altar = P12MapAltar.None, P28GameplayPolicy? policy = null) =>
        new("p28-test", 8, P12MapCatalog.Areas[0].StableId, RouteCandidates: [route], SelectedRoute: route,
            Altar: altar, AtlasSnapshot: Atlas, Gameplay: policy ?? new());

    [Fact]
    public void BuyingEveryNodeNeverEnablesDifficultyAndPolicyIsValidated()
    {
        P28GameplayPolicy normal = P28Gameplay.Policy(Map(MapRoute.Abyss));
        Assert.Equal(1, normal.AbyssIntensity); Assert.False(normal.AbyssFinalGuardian);
        Assert.Equal(P28GardenMode.Single, normal.Garden); Assert.Equal(P28WarfrontMode.Normal, normal.Warfront);
        var requested = new P28GameplayPolicy(AbyssIntensity: 5, AbyssFinalGuardian: true,
            Garden: P28GardenMode.Triple, Red: P28AltarMode.Extreme, Blue: P28AltarMode.Extreme, Warfront: P28WarfrontMode.Decisive);
        Assert.Equal(requested, P28Gameplay.Policy(Map(MapRoute.Abyss, policy: requested)));
        P28GameplayPolicy gated = P28Gameplay.Policy(Map(MapRoute.Abyss, policy: requested) with { AtlasSnapshot = [] });
        Assert.Equal(2, gated.AbyssIntensity); Assert.Equal(P28GardenMode.Single, gated.Garden);
        Assert.False(gated.AbyssFinalGuardian);
        Assert.Throws<ArgumentException>(() => new P28GameplayPolicy(AbyssIntensity: 6).Validate());
    }

    [Theory]
    [InlineData(1, 10000, 10000, 10000)]
    [InlineData(2, 12500, 11500, 13500)]
    [InlineData(3, 15000, 13000, 17500)]
    [InlineData(4, 20000, 16000, 25000)]
    [InlineData(5, 25000, 19000, 35000)]
    public void AbyssDifficultyReplacesLowerTier(int intensity, int life, int damage, int reward)
    {
        var policy = new P28GameplayPolicy(AbyssIntensity: intensity);
        var rule = P28Gameplay.Abyss(policy, false);
        Assert.Equal((life, damage, reward), (rule.Life, rule.Damage, rule.Reward));
        var final = P28Gameplay.Abyss(policy with { AbyssFinalGuardian = true }, true);
        Assert.Equal(reward, final.Reward); Assert.Equal(20000, final.TerminalReward);
        Assert.Equal(reward, P28Gameplay.Abyss(policy with { AbyssFinalGuardian = true }, false).Reward);
        Assert.True(P14MechanicRules.ResolveAbyss(4, 4, -500).Completed);
    }

    [Theory]
    [InlineData(P28GardenMode.Single, 3, 10000)]
    [InlineData(P28GardenMode.Twin, 2, 20000)]
    [InlineData(P28GardenMode.Triple, 1, 35000)]
    public void GardenHasExactlyThreePlotsAndWholeGardenRefreshBudget(P28GardenMode mode, int batches, int reward)
    {
        var map = Map(MapRoute.LifeGarden, policy: new(Garden: mode, Refresh: P28Refresh.Always));
        var plan = P14MapPlanner.Build(map, MapRoute.LifeGarden, [], 7);
        var plots = plan.Nodes.Where(n => n.Kind == P14MapNodeKind.GardenPlot).ToArray();
        Assert.Equal(batches, plots.Length); Assert.Equal(3, plots.Sum(n => n.Gameplay!.Units));
        Assert.Equal(2, plots.Sum(n => n.Gameplay!.Refreshes));
        Assert.Equal(12, plots.Sum(n => n.Gameplay!.Candidates!.Count));
        Assert.Equal(3, plots.Sum(n => n.Gameplay!.Selections!.Count));
        Assert.All(plots, n => Assert.Equal(reward, n.Gameplay!.Reward));
    }

    [Fact]
    public void AltarsAreIndependentRejectableAndCostsDoNotLeakAcrossMaps()
    {
        var map = Map(MapRoute.LifeGarden, P12MapAltar.RedOath, new(Red: P28AltarMode.Extreme, Reward: P28RewardPreference.Materials));
        var plan = P14MapPlanner.Build(map, MapRoute.LifeGarden, [], 42);
        Assert.Equal(3, plan.Nodes.Count(n => n.Kind == P14MapNodeKind.Altar));
        Assert.Equal(3, plan.Nodes.Count(n => n.Kind == P14MapNodeKind.GardenPlot));
        Assert.True(P28EncounterModifiers.For(plan, plan.Nodes[^1].Index, map).MaximumLife < 10000);
        Assert.Equal(10000, P28EncounterModifiers.For(plan, 2, map).MaximumLife);
        var skipped = P14MapPlanner.Build(map with { Gameplay = new(RejectedCosts: Enum.GetValues<P28Cost>()) }, MapRoute.LifeGarden, [], 42);
        Assert.DoesNotContain(skipped.Nodes, n => n.Kind == P14MapNodeKind.Altar);
        Assert.All(plan.Nodes.Where(n => n.Kind == P14MapNodeKind.Altar), n => Assert.Equal(40000, n.Gameplay!.Reward));
    }

    [Theory]
    [InlineData(P28AltarMode.Normal, P28Cost.BossLife, 12_000, 10_000)]
    [InlineData(P28AltarMode.HighPressure, P28Cost.BossLife, 17_500, 15_000)]
    [InlineData(P28AltarMode.HighPressure, P28Cost.BossDamage, 17_500, 15_000)]
    [InlineData(P28AltarMode.Extreme, P28Cost.BossLife, 25_000, 20_000)]
    [InlineData(P28AltarMode.Extreme, P28Cost.BossDamage, 25_000, 20_000)]
    public void BlueDifficultyReplacesBaseLifeAndDamageInsteadOfMultiplyingTwice(
        P28AltarMode mode, P28Cost cost, int expectedLife, int expectedDamage)
    {
        int magnitude = cost == P28Cost.BossLife ? 2_000 : 2_500;
        var choice = new P28Choice("blue.test", "测试苍誓", P28RewardPreference.HighBases, cost,
            magnitude, "test", "苍誓守卫", "测试代价");
        var rule = new P28EncounterRule(P28Mechanic.Blue, Choice: choice);
        var plan = new P14MapPlan("blue-modifier-test", MapRoute.Safe,
            [new(1, P14MapNodeKind.Altar, "苍誓祭坛", 8, Gameplay: rule),
             new(2, P14MapNodeKind.Boss, "最终Boss", 5)],
            P12MapAltar.BlueOath, Atlas, 0, "core.boss.map.01");
        P1MapItem map = Map(MapRoute.Safe, P12MapAltar.BlueOath, new(Blue: mode));

        P28EncounterModifiers modifiers = P28EncounterModifiers.For(plan, 2, map);

        Assert.Equal(expectedLife, modifiers.Life);
        Assert.Equal(expectedDamage, modifiers.Damage);
    }

    [Theory]
    [InlineData(P28WarfrontMode.Normal, 5, 1)]
    [InlineData(P28WarfrontMode.Expanded, 7, 2)]
    [InlineData(P28WarfrontMode.Decisive, 9, 2)]
    public void WarfrontNodeCountsAndActualProgressMerit(P28WarfrontMode mode, int count, int officers)
    {
        var map = Map(MapRoute.Warfront, policy: new(Warfront: mode)) with { AtlasSnapshot = mode == P28WarfrontMode.Normal ? [] : Atlas };
        var plan = P14MapPlanner.Build(map, MapRoute.Warfront, [], 42);
        Assert.Equal(count, plan.Nodes.Count); Assert.Equal(officers, plan.Nodes.Count(n => n.Kind == P14MapNodeKind.WarfrontOfficer));
        Assert.Equal(0, P28Rewards.Roll(FakeRun(map, plan, 0, false), 4).Merit);
        var full = P28Rewards.Roll(FakeRun(map, plan, int.MaxValue, true), 4);
        Assert.True(full.Merit >= 80); Assert.True(full.Reputation > 0);
        if (mode == P28WarfrontMode.Normal) Assert.Equal(80, full.Merit);
        var partial = P28Rewards.Roll(FakeRun(map, plan, 1, false), 4);
        Assert.InRange(partial.Merit, 1, full.Merit - 1); Assert.Equal(0, partial.Reputation);
    }

    [Fact]
    public void FailureKeepsGardenAndVestedRedButNeverPaysBluePromise()
    {
        var garden = Map(MapRoute.LifeGarden, P12MapAltar.RedOath);
        var plan = P14MapPlanner.Build(garden, MapRoute.LifeGarden, [], 31);
        var failed = FakeRun(garden, plan, plan.Nodes.Count - 1, false);
        var earned = P28Rewards.Roll(failed, 99);
        Assert.True(earned.LifeForce > 0); Assert.True(earned.RedFavor > 0);
        var blue = Map(MapRoute.Safe, P12MapAltar.BlueOath);
        var bluePlan = P14MapPlanner.Build(blue, MapRoute.Safe, [], 31);
        var blueFail = P28Rewards.Roll(FakeRun(blue, bluePlan, bluePlan.Nodes.Count - 1, false), 99);
        Assert.Equal(0, blueFail.BlueFavor); Assert.Null(blueFail.BlueTarget);
        Assert.True(P20DropFormula.ExtractDefeated(FakeRun(blue, bluePlan, bluePlan.Nodes.Count - 1, false), 80).Count > 0);
    }

    [Fact]
    public void RescueDoesNotDuplicateKillsOrVestedRewards()
    {
        var map = Map(MapRoute.LifeGarden);
        var plan = P14MapPlanner.Build(map, MapRoute.LifeGarden, [], 31);
        var once = FakeRun(map, plan, 4, false);
        var repeated = once with { Attempts = [once.Attempts[0], once.Attempts[0], once.Attempts[0]] };
        Assert.Equal(P20DropFormula.ExtractDefeated(once, 80), P20DropFormula.ExtractDefeated(repeated, 80));
        Assert.Equal(JsonSerializer.Serialize(P28Rewards.Roll(once, 33)), JsonSerializer.Serialize(P28Rewards.Roll(repeated, 33)));
    }

    [Fact]
    public void BlueGuaranteeCountsOnlyQualifyingSuccessesAndIsPerTargetAndPersistent()
    {
        var state = new P10EndgameState();
        P28RewardLedger miss = new([], 0, 0, 0, 0, 0, 0, [], [], new(0, 0, 0, 0, 0), P28RewardPreference.Legendary);
        for (int i = 0; i < 9; i++) Assert.False(state.RecordGameplay(miss, true));
        state = P10EndgameState.Restore(JsonSerializer.Deserialize<P10EndgameSnapshot>(JsonSerializer.Serialize(state.Capture())));
        Assert.False(state.RecordGameplay(miss with { BlueTarget = null }, true));
        Assert.False(state.RecordGameplay(miss with { BlueTarget = P28RewardPreference.HighBases }, true));
        Assert.True(state.RecordGameplay(miss, true));
        Assert.Equal(0, state.BlueMisses[P28RewardPreference.Legendary]);
        Assert.Equal(1, state.BlueMisses[P28RewardPreference.HighBases]);
    }

    [Fact]
    public void GardenCraftsPreserveExactlyOneSideAndGuaranteeLegalTag()
    {
        ItemInstance item = ItemGenerator.Generate("core.base.rusted_warhammer", 94, ItemRarity.Rare, 75);
        for (ulong seed = 1; seed < 40; seed++)
        {
            foreach (var craft in new[] { P14GardenCraft.KeepPrefixes, P14GardenCraft.KeepSuffixes })
            {
                ItemInstance next = P14GardenCrafting.Apply(item, craft, seed);
                Assert.Equal(P14GardenCrafting.SelectRetained(item, craft), P14GardenCrafting.SelectRetained(next, craft));
                Assert.InRange(next.PrefixCount, 0, 3); Assert.InRange(next.SuffixCount, 0, 3);
                Assert.Equal(next.Affixes.Count, next.Affixes.Select(a => a.Definition.MutualExclusionGroup).Distinct().Count());
            }
            ItemInstance biased = P14GardenCrafting.Apply(item, P14GardenCraft.BiasAttack, seed);
            Assert.Contains(biased.Affixes, a => P14GardenCrafting.Tagged(a.Definition, P14GardenCraft.BiasAttack));
        }
        foreach (P14GardenCraft craft in new[] { P14GardenCraft.BiasLife, P14GardenCraft.BiasDefense, P14GardenCraft.BiasSpell })
        {
            ItemInstance? candidate = P1ItemBases.All.Select(b => ItemGenerator.Generate(b.StableId, 94, ItemRarity.Rare, 1))
                .FirstOrDefault(i => P14GardenCrafting.CanApply(i, craft));
            Assert.NotNull(candidate);
            Assert.Contains(P14GardenCrafting.Apply(candidate, craft, 42).Affixes, a => P14GardenCrafting.Tagged(a.Definition, craft));
        }
    }

    [Fact]
    public void ElementalAttacksAreNotSpellsAndPhysicalSpellsCanBeSuppressed()
    {
        int Damage(bool spell, EnemyDamageType type, int suppression) => Combat(new(EnemySkillKind.BasicStrike, "验证攻击", type,
            10000, RangeRaw: 30000, Avoidable: false, IsSpell: spell), suppression: suppression).Events
            .Where(e => e.Kind == P4SpatialEventKind.EnemyAttack).Sum(e => e.Value);
        Assert.Equal(Damage(false, EnemyDamageType.Fire, 0), Damage(false, EnemyDamageType.Fire, 10000));
        Assert.True(Damage(true, EnemyDamageType.Physical, 10000) < Damage(true, EnemyDamageType.Physical, 0));
    }

    [Fact]
    public void SummonsAreRealCappedAndHaveNoLootIdentity()
    {
        var result = Combat(new(EnemySkillKind.SummonSwarm, "增援", EnemyDamageType.Physical, 10000, RangeRaw: 30000), ticks: 800);
        Assert.Contains(result.Frames.SelectMany(f => f.Enemies), e => e.Summoned);
        Assert.All(result.Frames, f => Assert.InRange(f.Enemies.Count(e => e.Summoned && e.Life > 0), 0, 8));
        Assert.DoesNotContain(result.Events, e => e.Kind == P4SpatialEventKind.FlaskCharge && e.Value > 0);
    }

    [Fact]
    public void GroundHazardsPersistAndTelegraphsCanBeDodged()
    {
        var skill = new EnemySkillProfile(EnemySkillKind.GroundHazard, "验证地面", EnemyDamageType.Fire, 10000,
            RangeRaw: 30000, Area: true, IsSpell: true);
        var slow = Combat(skill, speed: 10);
        var fast = Combat(skill, speed: 60000);
        Assert.Contains(slow.Events, e => e.Kind == P4SpatialEventKind.BossTelegraph && e.Detail.Contains("验证地面"));
        Assert.Contains(slow.Events, e => e.Detail.Contains("持续危险地面") && e.Value > 0);
        Assert.True(fast.Events.Where(e => e.Kind == P4SpatialEventKind.EnemyAttack).Sum(e => e.Value) <
            slow.Events.Where(e => e.Kind == P4SpatialEventKind.EnemyAttack).Sum(e => e.Value));
        var roots = Combat(skill with { Kind = EnemySkillKind.RootSnare }, speed: 10);
        Assert.Contains(roots.Events, e => e.Detail.Contains("缠根"));
        var linked = Combat(skill with { Kind = EnemySkillKind.ShieldLink });
        Assert.Contains(linked.Events, e => e.Detail.Contains("护盾链接"));
    }

    [Fact]
    public void ProductionMapRunsAllFiveMechanicsAndRewardsAreRepeatable()
    {
        foreach (var pair in new[] { (MapRoute.Abyss, P12MapAltar.None), (MapRoute.LifeGarden, P12MapAltar.None),
            (MapRoute.Safe, P12MapAltar.RedOath), (MapRoute.Safe, P12MapAltar.BlueOath), (MapRoute.Warfront, P12MapAltar.None) })
        {
            var map = Map(pair.Item1, pair.Item2);
            var run = new P1MapRunner(new P1MapAttemptResolver()).Run(map, pair.Item1, Strong(), 281);
            Assert.True(run.Succeeded, $"{pair}: {run.FailureReason}");
            var rewards = P28Rewards.Roll(run, 281);
            Assert.Contains(rewards.Encounters, e => e.Cleared && e.Kills > 0);
            Assert.Equal(JsonSerializer.Serialize(rewards), JsonSerializer.Serialize(P28Rewards.Roll(run, 281)));
            Assert.NotEmpty(rewards.Equipment.Concat(P1MapRewardGenerator.Generate(map, pair.Item1, 281, 20, run).Equipment));
        }
    }

    [Fact]
    public void CompletedNodesAreSkippedByProductionRescueAndSeedRemainsStable()
    {
        var map = Map(MapRoute.Safe);
        var first = new P1MapAttemptResolver().Resolve(map, MapRoute.Safe, Strong(), 1, 28);
        var resumed = new P1MapAttemptResolver().Resolve(map with { ResumeNode = 4 }, MapRoute.Safe, Strong(), 2, 28);
        Assert.All(resumed.Nodes, n => Assert.True(n.NodeIndex >= 4));
        var originalSpawns = first.Timeline!.SpatialFrames!.First(f => f.NodeIndex == 4).Enemies;
        var resumedSpawns = resumed.Timeline!.SpatialFrames!.First(f => f.NodeIndex == 4).Enemies;
        Assert.Equal(JsonSerializer.Serialize(originalSpawns), JsonSerializer.Serialize(resumedSpawns));
    }

    [Fact]
    public void OptionalNoRescueIsEnforcedAtTheFailedEncounter()
    {
        var hard = Map(MapRoute.Abyss, policy: new(AbyssIntensity: 5, AbyssFinalGuardian: true));
        var plan = P14MapPlanner.Build(hard, MapRoute.Abyss, [], 28);
        var resolver = new FailAt(plan, plan.Nodes.First(n => n.Kind == P14MapNodeKind.AbyssFissure).Index);
        Assert.Equal(1, new P1MapRunner(resolver).Run(hard, MapRoute.Abyss, Strong(), 28).AttemptsUsed);
        var normal = hard with { Gameplay = new() };
        var normalPlan = P14MapPlanner.Build(normal, MapRoute.Abyss, [], 28);
        Assert.Equal(3, new P1MapRunner(new FailAt(normalPlan, 3)).Run(normal, MapRoute.Abyss, Strong(), 28).AttemptsUsed);
        // Outside the abyss, the same purchased/selected modifier must not remove normal rescues.
        Assert.Equal(3, new P1MapRunner(new FailAt(plan, 2)).Run(hard, MapRoute.Abyss, Strong(), 28).AttemptsUsed);
    }

    private sealed class FailAt(P14MapPlan plan, int index) : IP1MapAttemptResolver
    {
        public MapAttemptResult Resolve(P1MapItem map, MapRoute route, P1TeamBuild team, int attempt, ulong seed)
        {
            var timeline = new P3SceneTimeline("failure",12,24,plan.Nodes.Count,1,1000,P1BattleOutcome.EnemyVictory,0,0,0,[],[],"",[],plan.Nodes);
            return new(false,[new(index,"enemy",false,P1BattleOutcome.EnemyVictory,20,"")],"failure",timeline);
        }
    }

    [Fact]
    public void EveryResolvedMapHasOwnCallbackAndOnlineOfflineSegmentationMatches()
    {
        P1WorldState World()
        {
            var state = new P1WorldState(Strong(), Strong());
            state.Hero.Policy = ExpeditionPolicy.Recommended with { Gameplay = new(), RouteDecisionTimeoutSeconds = 0 };
            foreach (int tier in new[] { 1, 3, 5 }) state.Hero.Queue.TryEnqueue(Map(MapRoute.LifeGarden) with { InstanceId = $"p28-batch-{tier}", Tier = tier });
            return state;
        }
        var whole = World(); var split = World();
        var endWhole = new P10EndgameState(); var endSplit = new P10EndgameState();
        var observed = new List<int>();
        P1WorldSimulator Sim(P10EndgameState end, bool record) => new(new P1MapAttemptResolver())
        {
            MapResolved = (_, run, seed, _, _) =>
            {
                if (record) observed.Add(run.Map.Tier);
                end.RecordGameplay(P28Rewards.Roll(run, seed), true);
                if (run.Succeeded) end.RecordMapCompletion(run.Map, run.Route, seed);
            },
        };
        Sim(endWhole, true).Simulate(whole, 3_600_000, 28, offline: true);
        var splitSim = Sim(endSplit, false);
        for (int i = 0; i < 60; i++) splitSim.Simulate(split, 60_000, 28);
        Assert.Equal(new[] { 1, 3, 5 }, observed);
        Assert.Equal(JsonSerializer.Serialize(endWhole.Capture()), JsonSerializer.Serialize(endSplit.Capture()));
        // Report labels deliberately say online/offline; gameplay state and rewards must be identical.
        Assert.Equal(JsonSerializer.Serialize(P1WorldSnapshots.Capture(whole) with { P5Expedition = null }),
            JsonSerializer.Serialize(P1WorldSnapshots.Capture(split) with { P5Expedition = null }));
    }

    [Fact]
    public void PolicySnapshotSurvivesSaveAndUpdatesOnlyNextMap()
    {
        var world = new P1WorldState(Strong(), Strong());
        world.Hero.Policy = ExpeditionPolicy.Recommended with { Gameplay = new(AbyssIntensity: 1), RouteDecisionTimeoutSeconds = 0 };
        world.Hero.Queue.TryEnqueue(Map(MapRoute.Abyss));
        var simulator = new P1WorldSimulator(new P1MapAttemptResolver());
        simulator.Simulate(world, 1, 28);
        Assert.Equal(1, world.Hero.ActiveRun!.Map.Gameplay!.AbyssIntensity);
        world.Hero.ApplyPolicy(world.Hero.Policy with { Gameplay = new(AbyssIntensity: 5) });
        var snapshot = P1WorldSnapshots.Capture(world);
        var restored = P1WorldSnapshots.Restore(JsonSerializer.Deserialize<P1WorldSnapshot>(JsonSerializer.Serialize(snapshot))!);
        Assert.Equal(1, restored.Hero.ActiveRun!.Map.Gameplay!.AbyssIntensity);
        Assert.Equal(5, restored.Hero.PendingPolicy!.Gameplay!.AbyssIntensity);
        simulator.Simulate(restored, 3_600_000, 28);
        Assert.Equal(5, restored.Hero.Policy.Gameplay!.AbyssIntensity);
    }

    [Fact]
    public void SupplyExchangeIsRepeatableAtomicAndSequencePersists()
    {
        var session = P1GameSession.CreateNew(new("P28", CharacterGender.Androgynous, CharacterSkinTone.Fair,
            CharacterHairStyle.Cropped, P23BaseClass.Fighter), 28);
        session.Endgame.DiscoverWarfront();
        session.Endgame.RecordGameplay(new([], 0, 0, 0, 500, 60, 0, [], [], new(0, 0, 0, 0, 0)), false);
        Assert.Equal(3, session.Endgame.SupplyTier);
        Assert.True(session.TryExchangeWarfrontSupply(P28RewardPreference.Materials));
        Assert.Equal(350, session.Endgame.WarfrontMerit);
        session = P1GameSession.Restore(session.Capture());
        Assert.Equal(1, session.Endgame.GameplayOperationSequence);
        Assert.True(session.TryExchangeWarfrontSupply(P28RewardPreference.Weapons));
        Assert.True(session.TryExchangeWarfrontSupply(P28RewardPreference.Armor));
        Assert.False(session.TryExchangeWarfrontSupply(P28RewardPreference.Jewelry));
        Assert.Equal(50, session.Endgame.WarfrontMerit);
    }

    private static P1TeamBuild Strong()
    {
        var skill = new SkillConfiguration(P1SkillIds.HeavyStrike, SkillSupport.AttackSpeed);
        return new(new(90, new(300, 100, 100, 100), new(5000, 100, 500), FlatMaximumLife: 30000, FlatMaximumMana: 10000,
                FlatLifeRegeneration: 200, FireResistanceBasisPoints: 7500, ColdResistanceBasisPoints: 7500, LightningResistanceBasisPoints: 7500, VoidResistanceBasisPoints: 7500),
            new("p28-strong", 10000, 10000, 2000, 10000), skill, FlatAccuracy: 10000, UseWarCry: false,
            ActiveSkills: [skill, new(P1SkillIds.EarthCleave, SkillSupport.IncreasedArea)]);
    }

    private static P4NodeCombatResult Combat(EnemySkillProfile skill, int suppression = 0, int speed = 10000, int ticks = 160)
    {
        var sheet = new CharacterSheet(65, new(50, 0, 20, 0), new(0, 0, 0), FlatMaximumLife: 100000,
            FlatMaximumMana: 10000, SpellSuppressionBasisPoints: suppression);
        var active = new SkillConfiguration(P1SkillIds.HeavyStrike, SkillSupport.None);
        var build = new P1TeamBuild(sheet, new("test", 1, 1, 1000, 0), active, UseWarCry: false, MovementSpeedBasisPoints: speed, ActiveSkills: [active]);
        var enemy = new EnemyProfile("p28.enemy", "机制验证", 10000, 100, 100, 0, 0, 100000, 0, 1000, 1,
            Skills: [skill]);
        return new P4SpatialCombatRunner().Run(new(build, 1, 65, 2, false, false, false, 0, MaximumTicks: ticks, EnemyPool: [enemy]), 17);
    }

    private static P1MapRunResult FakeRun(P1MapItem map, P14MapPlan plan, int clearedThrough, bool success)
    {
        var events = new List<P3SceneEvent>(); var frames = new List<P4SpatialFrame>(); var nodes = new List<MapNodeResult>();
        foreach (var node in plan.Nodes.Where(n => n.EnemyCount > 0 && n.Index <= clearedThrough))
        {
            events.Add(new(0, P3SceneEventKind.WaveStarted, node.Index, 1, node.EnemyCount, "", 100, 100, 100, 100, 0, 0, 0, 0, new(0, 0)));
            var enemies = new List<P4EnemyFrame>();
            for (int i = 0; i < node.EnemyCount; i++)
            {
                string id = $"enemy-{node.Index}-{i}";
                enemies.Add(new(id, "core.enemy.corrupted_worker", "敌人", P4UnitRole.Melee, EnemyRarity.Normal, false, false, 0, 100, new(0, 0), "hero"));
                events.Add(new(1, P3SceneEventKind.EnemyDefeated, node.Index, 1, 0, $"hero|{id}|core.enemy.corrupted_worker", 100, 100, 100, 100, 0, 0, 0, 100, new(0, 0)));
            }
            frames.Add(new(0, node.Index, new(0, 0), 100, 100, 100, 100, 0, 0, "", enemies));
            nodes.Add(new(node.Index, "group", false, P1BattleOutcome.HeroVictory, 1, ""));
        }
        var timeline = new P3SceneTimeline("test", 12, 24, plan.Nodes.Count, nodes.Count, 1000, success ? P1BattleOutcome.HeroVictory : P1BattleOutcome.EnemyVictory,
            0, 100, 0, [], events, "test", frames, plan.Nodes);
        return new(map, plan.Route, success, 1, 2, [new(success, nodes, "", timeline)], "", 1000);
    }
}
