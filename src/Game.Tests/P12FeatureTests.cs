using GameForWork.Core.P1;
using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P1.World;
using GameForWork.Core.P4;
using GameForWork.Core.Persistence;
using GameForWork.Core.P10;
using GameForWork.Core.P12;

namespace GameForWork.Tests;

public sealed class P12FeatureTests
{
    [Fact]
    public void FormalMapGenerationIsDeterministicAndOffersOneToThreeRoutes()
    {
        P1MapItem first = new P1MapItem("p12-stable", 8).EnsureFormal(77);
        P1MapItem replay = new P1MapItem("p12-stable", 8).EnsureFormal(77);

        Assert.Equal(first.AreaId, replay.AreaId);
        Assert.Equal(first.Altar, replay.Altar);
        Assert.Equal(first.EffectiveRouteCandidates, replay.EffectiveRouteCandidates);
        Assert.InRange(first.EffectiveRouteCandidates.Count, 1, 3);
        Assert.Equal(12, P12MapCatalog.Areas.Count);
    }

    [Fact]
    public void MapCraftingConsumesTheMatchingMetalAndLocksAfterCorruption()
    {
        var economy = EconomyWith(P12MapCraftOperation.AlchemicalRare, P12MapCraftOperation.Corrupt);
        P1MapItem map = new P1MapItem("p12-craft", 6).EnsureFormal(1);

        P12MapCraftResult rare = P12MapCrafting.Apply(economy, map, P12MapCraftOperation.AlchemicalRare, 2);
        P12MapCraftResult corrupted = P12MapCrafting.Apply(economy, rare.Map, P12MapCraftOperation.Corrupt, 3);
        P12MapCraftResult locked = P12MapCrafting.Apply(economy, corrupted.Map, P12MapCraftOperation.ChaosReroll, 4);

        Assert.True(rare.Succeeded);
        Assert.Equal(P12MapRarity.Rare, rare.Map.Rarity);
        Assert.InRange(rare.Map.EffectiveAffixes.Count, 4, 6);
        Assert.True(corrupted.Succeeded);
        Assert.True(corrupted.Map.IsCorrupted);
        Assert.False(locked.Succeeded);
    }

    [Fact]
    public void TeamPolicyHonorsCandidatesBlocksAndDangerCeiling()
    {
        P1MapItem map = new P1MapItem("p12-policy", 12).EnsureFormal(9) with
        {
            RouteCandidates = [MapRoute.Safe, MapRoute.Abyss, MapRoute.LifeGarden],
        };
        var policy = ExpeditionPolicy.Recommended with
        {
            RoutePriority = [MapRoute.Abyss, MapRoute.LifeGarden, MapRoute.Safe],
            BlockedRoutes = [MapRoute.Abyss],
            MaximumMapDanger = 100,
        };

        Assert.Equal(MapRoute.LifeGarden, policy.SelectUnattendedRoute(map, 100, 11));
    }

    [Fact]
    public void LockedWorldRefusesTierSeventeenButBreakthroughUnlocksIt()
    {
        P1TeamBuild build = Build();
        var locked = new P1WorldState(build, build);
        P1MapItem map = new P1MapItem("p12-t17", 17).EnsureFormal(5) with { SelectedRoute = MapRoute.Safe };
        if (!map.EffectiveRouteCandidates.Contains(MapRoute.Safe))
            map = map with { RouteCandidates = [MapRoute.Safe], SelectedRoute = MapRoute.Safe };
        Assert.True(locked.Hero.Queue.TryEnqueue(map));

        new P1WorldSimulator(new P1MapAttemptResolver()).Simulate(locked, 1, 4);
        Assert.True(locked.Hero.IsStopped);
        Assert.Equal("tier_locked", locked.Hero.StopReason);

        var unlocked = new P1WorldState(build, build);
        unlocked.UnlockFinalMapTiers();
        Assert.Equal(20, unlocked.MaximumUnlockedMapTier);
    }

    [Fact]
    public void AtlasOwnsThreeNamedSchemesAndRoundTripsActiveAllocation()
    {
        var state = new P10EndgameState();
        state.RecordMapCompletion(new P1MapItem("p12-atlas", 1), MapRoute.Safe, 1);
        Assert.True(state.TryAllocateAtlas("core.atlas.00.00"));
        Assert.True(state.TryRenameAtlasScheme(1, "裂渊专精"));
        Assert.True(state.TrySwitchAtlasScheme(1));

        P10EndgameState restored = P10EndgameState.Restore(state.Capture());
        Assert.Equal(3, restored.AtlasSchemeNames.Count);
        Assert.Equal("裂渊专精", restored.AtlasSchemeNames[1]);
        Assert.Equal(1, restored.ActiveAtlasSchemeIndex);
        Assert.Empty(restored.AtlasPassives);
    }

    [Fact]
    public void OnlineFormalMapWaitsThirtySecondsButThenUsesPolicy()
    {
        P1TeamBuild build = Build();
        var world = new P1WorldState(build, build);
        P1MapItem map = new P1MapItem("p12-timeout", 2).EnsureFormal(8) with
        {
            RouteCandidates = [MapRoute.Safe, MapRoute.Abyss],
            SelectedRoute = null,
        };
        Assert.True(world.Hero.Queue.TryEnqueue(map));
        var simulator = new P1WorldSimulator(new ImmediateResolver());

        simulator.Simulate(world, 1, 9);
        Assert.Null(world.Hero.ActiveMap);
        Assert.Equal(30_000, world.Hero.RouteDecisionRemainingMilliseconds);
        simulator.Simulate(world, 29_999, 9);
        Assert.Null(world.Hero.ActiveMap);
        simulator.Simulate(world, 1, 9);
        Assert.NotNull(world.Hero.ActiveMap);
    }

    [Fact]
    public void BatchCraftNeverExceedsPerMapBudget()
    {
        P1GameSession session = P1GameSession.CreateNew(new PlayerIdentity("制图测试", CharacterGender.Androgynous,
            CharacterSkinTone.Umber, CharacterHairStyle.Cropped, P1Ascendancy.IronOath), 12);
        session.World.MapInventory.Add(new P1MapItem("p12-batch", 4));
        session.World.Economy.AddMetal(MetalCurrencyKind.PolishingCobalt, 10);
        session.World.Economy.AddMetal(MetalCurrencyKind.AlchemicalGold, 10);

        P12MapBatchResult result = session.BatchCraftMaps(new P12MapBatchRule(MaximumMetalSpendPerMap: 4));

        Assert.Equal(4, result.MetalsSpent);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(20, session.World.MapInventory[0].Quality);
        Assert.Equal(P12MapRarity.Basic, session.World.MapInventory[0].Rarity);
    }

    [Fact]
    public void IncompatibleTestDatabaseCanBeArchivedUnderLegacyRecovery()
    {
        string root = Path.Combine(Path.GetTempPath(), "GameForWork.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            using var repository = new SaveRepository(root, 1);
            repository.Initialize();
            repository.SaveP1SessionJson("{\"FormatVersion\":0}");

            string archived = repository.ArchiveLegacyAndReset();

            Assert.True(File.Exists(archived));
            Assert.Null(repository.LoadP1SessionJson());
            Assert.StartsWith(repository.LegacyRecoveryDirectory, archived, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static TownEconomyState EconomyWith(params P12MapCraftOperation[] operations)
    {
        Dictionary<MetalCurrencyKind, int> metals = Enum.GetValues<MetalCurrencyKind>().ToDictionary(kind => kind, _ => 0);
        foreach (P12MapCraftOperation operation in operations) metals[P12MapCrafting.Cost(operation).Currency]++;
        return new TownEconomyState(metalCurrencies: metals);
    }

    private static P1TeamBuild Build() => new(
        new CharacterSheet(100, CharacterAttributes.IronOathStarting, new DefensiveEquipment(500, 50, 50)),
        P1Weapons.RustedGreatsword,
        new SkillConfiguration(P1SkillIds.HeavyStrike, SkillSupport.Bleed));

    private sealed class ImmediateResolver : IP1MapAttemptResolver
    {
        public MapAttemptResult Resolve(P1MapItem map, MapRoute route, P1TeamBuild team, int attempt, ulong seed) =>
            new(true, [new MapNodeResult(1, "test", false, P1BattleOutcome.HeroVictory, 1, "ok")], string.Empty);
    }
}
