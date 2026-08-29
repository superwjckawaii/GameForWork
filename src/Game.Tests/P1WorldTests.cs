using GameForWork.Core.Offline;
using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P1.World;
using System.Diagnostics;

namespace GameForWork.Tests;

public sealed class P1WorldTests
{
    [Fact]
    public void MapQueueCapacityAndAreaLevelAreEnforced()
    {
        var queue = new P1MapQueue();
        for (int index = 0; index < P1MapQueue.MaximumCount; index++)
        {
            Assert.True(queue.TryEnqueue(new P1MapItem($"map-{index}", 1)));
        }

        Assert.False(queue.TryEnqueue(new P1MapItem("overflow", 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => queue.TryEnqueue(new P1MapItem("bad", 21)));
    }

    [Fact]
    public void RoutePolicySupportsManualAndAutomaticSelection()
    {
        ExpeditionPolicy automatic = ExpeditionPolicy.Recommended with { PreferredRoute = MapRoute.Abyss };
        Assert.Equal(MapRoute.Abyss, automatic.SelectRoute(null));

        ExpeditionPolicy manual = automatic with { RouteSelection = RouteSelectionMode.Manual };
        Assert.Equal(MapRoute.Safe, manual.SelectRoute(MapRoute.Safe));
        Assert.Throws<InvalidOperationException>(() => manual.SelectRoute(null));
        Assert.Equal(MapRoute.Abyss, manual.SelectUnattendedRoute());
    }

    [Fact]
    public void MapRunnerUsesTwoRescuesBeforeSuccess()
    {
        var runner = new P1MapRunner(new SucceedOnAttemptResolver(3));

        P1MapRunResult result = runner.Run(
            new P1MapItem("rescue-test", 1),
            MapRoute.Safe,
            Build(),
            7);

        Assert.True(result.Succeeded);
        Assert.Equal(3, result.AttemptsUsed);
        Assert.Equal(0, result.RescueChancesRemaining);
        Assert.Equal(3, result.Attempts.Count);
    }

    [Fact]
    public void FormalMapResolverRunsAcrossEightGridNodesEndingAtBoss()
    {
        var powerful = new P1TeamBuild(
            new CharacterSheet(
                10,
                new CharacterAttributes(200, 100, 100, 100),
                new DefensiveEquipment(500, 100, 200),
                FlatMaximumLife: 1_000),
            new WeaponProfile("test.overwhelming", 1_000, 1_000, 2_000, 10_000),
            new SkillConfiguration(P1SkillIds.HeavyStrike, SkillSupport.AttackSpeed),
            FlatAccuracy: 1_000);

        MapAttemptResult result = new P1MapAttemptResolver().Resolve(
            new P1MapItem("formal", 1), MapRoute.Safe, powerful, 1, 42);

        Assert.True(result.Succeeded);
        Assert.InRange(result.Timeline!.NodeCount, 5, 8);
        Assert.Equal(result.Timeline.TotalWaves, result.Nodes.Select(node => node.NodeIndex).Distinct().Count());
        Assert.Equal(12, result.Timeline.GridWidth);
        Assert.Equal(24, result.Timeline.GridHeight);
        Assert.StartsWith("core.boss.map.", result.Nodes[^1].EnemyStableId);
        Assert.All(result.Nodes, node => Assert.Equal(P1BattleOutcome.HeroVictory, node.Outcome));
    }

    [Fact]
    public void MapRewardsUseP20BudgetsAndTierDistribution()
    {
        for (ulong seed = 1; seed <= 100; seed++)
        {
            P1MapRewards safe = P1MapRewardGenerator.Generate(new P1MapItem($"safe-{seed}", 5), MapRoute.Safe, seed);
            Assert.Equal(190, safe.Experience);
            Assert.InRange(safe.Equipment.Count, 1, 41);
            Assert.True(safe.Stackables.Gold > 0);
            Assert.Equal(0, safe.Stackables.IronScraps);
            Assert.Equal(1, safe.Stackables.MemoryAshes);
            Assert.Equal(0, safe.Stackables.WardenMarks);
            Assert.All(safe.Maps, map => Assert.InRange(map.Tier, 4, 7));
            Assert.NotNull(safe.Trace);
            Assert.Equal(safe.Equipment.Count, safe.Trace!.EquipmentCount);

            P1MapRewards abyss = P1MapRewardGenerator.Generate(new P1MapItem($"abyss-{seed}", 10), MapRoute.Abyss, seed);
            Assert.InRange(abyss.Equipment.Count, 1, 41);
            Assert.True(abyss.Stackables.Metals!.Sum(item => item.Amount) > 0);
            Assert.All(abyss.Maps, map => Assert.InRange(map.Tier, 9, 12));
        }
    }

    [Fact]
    public void WorkshopPaysCostAndReplacesCraftedPrefix()
    {
        var economy = new TownEconomyState(gold: 100, ironScraps: 20);
        ItemInstance weapon = ItemGenerator.Generate(
            "core.base.rusted_greatsword", 1, ItemRarity.Basic, 1, "workshop-weapon");

        WorkshopResult first = P1Workshop.CraftPhysicalIncrease(economy, weapon);
        WorkshopResult second = P1Workshop.CraftPhysicalIncrease(economy, first.Item!);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal(0, economy.Gold);
        Assert.Equal(0, economy.IronScraps);
        Assert.Single(second.Item!.Affixes, affix => affix.Crafted);
    }

    [Fact]
    public void FirstDiscoveryOverridesDefaultDismantleFilter()
    {
        var storage = new EquipmentStorage();
        var filter = new LootFilter();
        ItemInstance first = ItemGenerator.Generate(
            "core.base.iron_ring", 1, ItemRarity.Basic, 1, "first-ring");
        ItemInstance second = ItemGenerator.Generate(
            "core.base.iron_ring", 1, ItemRarity.Basic, 2, "second-ring");

        LootProcessingResult firstResult = LootProcessor.Process(
            [first], storage, filter, StorageFullBehavior.AcceptStackablesOnly);
        LootProcessingResult secondResult = LootProcessor.Process(
            [second], storage, filter, StorageFullBehavior.AcceptStackablesOnly);

        Assert.Equal(1, firstResult.Stored);
        Assert.Contains("core.base.iron_ring", firstResult.ForcedFirstDiscoveries);
        Assert.Equal(1, secondResult.Dismantled);
        Assert.Equal(1, secondResult.IronScrapsGained);
    }

    [Fact]
    public void CustomFilterCanMatchAffixValue()
    {
        AffixDefinition affix = P1Affixes.All.First(definition =>
            definition.StableFamilyId == "core.affix.ring.life" && definition.Tier == 2);
        var ring = new ItemInstance(
            "filtered-ring",
            P1ItemBases.Get("core.base.life_ring"),
            1,
            ItemRarity.Magic,
            [new AffixRoll(affix, 8)],
            ImplicitValue: 8);
        var filter = new LootFilter(
        [
            new LootFilterRule(
                "custom.high_life",
                LootDisposition.Keep,
                ItemRarity.Magic,
                AffixFamilyId: "core.affix.ring.life",
                MinimumAffixValue: 8),
            new LootFilterRule("custom.magic", LootDisposition.Sell, ItemRarity.Magic),
        ]);

        Assert.Equal(LootDisposition.Keep, filter.Evaluate(ring));
    }

    [Fact]
    public void StorageFullPolicyCanStopExpedition()
    {
        var storage = new EquipmentStorage(1);
        ItemInstance first = ItemGenerator.Generate("core.base.life_ring", 1, ItemRarity.Rare, 1, "one");
        ItemInstance second = ItemGenerator.Generate("core.base.focus_ring", 1, ItemRarity.Rare, 2, "two");

        LootProcessingResult result = LootProcessor.Process(
            [first, second], storage, new LootFilter(), StorageFullBehavior.StopExpedition);

        Assert.True(result.StorageBecameFull);
        Assert.True(result.ExpeditionMustStop);
        Assert.Equal(1, result.Stored);
    }

    [Fact]
    public void ExpeditionBackpackHasTwentySlots()
    {
        var backpack = new ExpeditionBackpack();
        for (int index = 0; index < ExpeditionBackpack.Capacity; index++)
        {
            Assert.True(backpack.TryAdd(ItemGenerator.Generate(
                "core.base.life_flask", 1, ItemRarity.Basic, (ulong)index, $"flask-{index}")));
        }

        Assert.False(backpack.TryAdd(ItemGenerator.Generate(
            "core.base.life_flask", 1, ItemRarity.Basic, 99, "overflow")));
        Assert.Equal(20, backpack.TakeAll().Count);
        Assert.Equal(0, backpack.Count);
    }

    [Fact]
    public void TwoTeamsAdvanceIndependentlyAndPreservePartialMaps()
    {
        P1WorldState state = WorldWithInitialMaps();
        var simulator = new P1WorldSimulator(new SucceedOnAttemptResolver(1));

        P1OfflineResult first = simulator.Simulate(state, 120_000, 11);

        Assert.Equal(2, first.TotalMapsCompleted);
        Assert.NotNull(state.Hero.ActiveMap);
        Assert.NotNull(state.Mercenaries.ActiveMap);
        Assert.Equal(60_000, state.Hero.RemainingMapTimeMilliseconds);
        Assert.Equal(60_000, state.Mercenaries.RemainingMapTimeMilliseconds);

        P1OfflineResult second = simulator.Simulate(state, 60_000, 12);
        Assert.Equal(2, second.TotalMapsCompleted);
        Assert.Equal(2, state.Hero.MapsCompleted);
        Assert.Equal(2, state.Mercenaries.MapsCompleted);
        Assert.NotEmpty(state.Hero.Backpack.Items);
        Assert.NotEmpty(state.Mercenaries.Backpack.Items);
    }

    [Fact]
    public void ExpeditionBackpacksSurviveWorldSnapshotRoundTrip()
    {
        P1WorldState state = WorldWithInitialMaps();
        var simulator = new P1WorldSimulator(new SucceedOnAttemptResolver(1));
        simulator.Simulate(state, 90_000, 17);

        P1WorldState restored = P1WorldSnapshots.Restore(P1WorldSnapshots.Capture(state));

        Assert.Equal(
            state.Hero.Backpack.Items.Select(item => item.InstanceId),
            restored.Hero.Backpack.Items.Select(item => item.InstanceId));
        Assert.Equal(
            state.Mercenaries.Backpack.Items.Select(item => item.InstanceId),
            restored.Mercenaries.Backpack.Items.Select(item => item.InstanceId));
    }

    [Fact]
    public void FortyEightHourSimulationClampsAndConsumesQueuedMapsOnly()
    {
        P1WorldState state = WorldWithInitialMaps();
        var simulator = new P1WorldSimulator(new SucceedOnAttemptResolver(1));

        P1OfflineResult result = simulator.Simulate(state, OfflineTime.MaximumMilliseconds + 1, 99);

        Assert.True(result.WasClamped);
        Assert.Equal(OfflineTime.MaximumMilliseconds, result.EffectiveMilliseconds);
        Assert.Equal(10, result.TotalMapsCompleted);
        Assert.All(result.Teams, team => Assert.Equal(0, team.RemainingQueue));
        Assert.True(state.MapInventory.Count > 0);
        Assert.Equal(0, state.Economy.WardenMarks);
    }

    [Fact]
    public void TwelveWardenMarksExchangeForSelectedLegendary()
    {
        var economy = new TownEconomyState(wardenMarks: 12);

        Assert.True(economy.TryExchangeLegendary("core.unique.iron_moon", out ItemInstance? item));
        Assert.Equal("core.unique.iron_moon", item?.LegendaryRule?.StableId);
        Assert.Equal(0, economy.WardenMarks);
        Assert.False(economy.TryExchangeLegendary(out _));
    }

    [Fact]
    public void GeneratedCantorOwnsSkillsAndAiButExposesFinalStats()
    {
        P1MercenaryProfile first = P1MercenaryFactory.GenerateCantor(123);
        P1MercenaryProfile replay = P1MercenaryFactory.GenerateCantor(123);

        Assert.Equal(first.Name, replay.Name);
        Assert.Equal(MercenaryArchetype.Cantor, first.Archetype);
        Assert.Equal(new CharacterAttributes(16, 12, 18, 12), first.FinalAttributes);
        Assert.Contains(first.AutonomousConfiguration.Skills, skill => skill.SkillId == P1SkillIds.WarCry);
        Assert.Contains("玩家不可修改", first.AutonomousConfiguration.GrowthSummary);
        Assert.Equal(first.AutonomousConfiguration.AiSummary, first.CreateTeamBuild().AiSummary);
    }

    [Fact]
    public void TeleporterCapacityGrowsThreeThroughSixWithoutAddingATeam()
    {
        var teleporter = new TeleporterState();
        Assert.Equal(3, teleporter.MercenaryTeamCapacity);
        for (int level = 2; level <= 4; level++)
        {
            Assert.True(teleporter.TrySetLevel(level));
            Assert.Equal(level + 2, teleporter.MercenaryTeamCapacity);
        }

        Assert.False(teleporter.TrySetLevel(5));
        Assert.Equal(6, teleporter.MercenaryTeamCapacity);
        Assert.Equal(2, WorldWithInitialMaps().Teams.Count);
    }

    [Fact]
    public void WorldSimulationReplaysToSameHash()
    {
        P1WorldState first = WorldWithInitialMaps();
        P1WorldState replay = WorldWithInitialMaps();
        var simulator = new P1WorldSimulator(new SucceedOnAttemptResolver(1));

        P1OfflineResult firstResult = simulator.Simulate(first, 600_000, 456);
        P1OfflineResult replayResult = simulator.Simulate(replay, 600_000, 456);

        Assert.Equal(firstResult.FinalHash, replayResult.FinalHash);
        Assert.Equal(first.Economy.Gold, replay.Economy.Gold);
        Assert.Equal(first.Storage.Count, replay.Storage.Count);
        Assert.Equal(first.MapInventory, replay.MapInventory);
    }

    [Fact]
    public void FullResolverProcessesTwoStableQueuesWithinOfflineBudget()
    {
        var strong = new P1TeamBuild(
            new CharacterSheet(
                10,
                new CharacterAttributes(200, 100, 100, 100),
                new DefensiveEquipment(500, 100, 200),
                FlatMaximumLife: 1_000),
            new WeaponProfile("test.offline", 1_000, 1_000, 2_000, 10_000),
            new SkillConfiguration(P1SkillIds.HeavyStrike, SkillSupport.AttackSpeed),
            FlatAccuracy: 1_000);
        var state = new P1WorldState(strong, strong);
        state.AddInitialMaps();
        var stopwatch = Stopwatch.StartNew();

        P1OfflineResult result = new P1WorldSimulator(new P1MapAttemptResolver()).Simulate(
            state,
            OfflineTime.MaximumMilliseconds,
            789);

        stopwatch.Stop();
        Assert.Equal(10, result.TotalMapsCompleted);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(3), $"Elapsed: {stopwatch.Elapsed}");
    }

    private static P1WorldState WorldWithInitialMaps()
    {
        var state = new P1WorldState(Build(), Build());
        state.AddInitialMaps();
        return state;
    }

    private static P1TeamBuild Build() => new(
        new CharacterSheet(1, CharacterAttributes.IronOathStarting, new DefensiveEquipment(30, 0, 0)),
        P1Weapons.RustedGreatsword,
        new SkillConfiguration(P1SkillIds.HeavyStrike, SkillSupport.None));

    private sealed class SucceedOnAttemptResolver(int successAttempt) : IP1MapAttemptResolver
    {
        public MapAttemptResult Resolve(P1MapItem map, MapRoute route, P1TeamBuild team, int attempt, ulong seed)
        {
            bool succeeded = attempt >= successAttempt;
            IReadOnlyList<MapNodeResult> nodes = succeeded
                ? Enumerable.Range(1, 5)
                    .Select(index => new MapNodeResult(
                        index,
                        index == 5 ? P1Enemies.AbyssWarden.StableId : P1Enemies.CorruptedWorker.StableId,
                        false,
                        P1BattleOutcome.HeroVictory,
                        20,
                        $"hash-{seed}-{index}"))
                    .ToArray()
                : [new MapNodeResult(1, P1Enemies.CorruptedWorker.StableId, false, P1BattleOutcome.EnemyVictory, 20, $"fail-{seed}")];
            return new MapAttemptResult(succeeded, nodes, succeeded ? string.Empty : "test_failure");
        }
    }
}
