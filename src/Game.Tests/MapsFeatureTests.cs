using GameForWork.Core.Campaign;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Spatial;
using GameForWork.Core.Persistence;
using GameForWork.Core.Endgame;
using GameForWork.Core.Maps;
using GameForWork.Core.Atlas;

namespace GameForWork.Tests;

public sealed class MapsFeatureTests
{
    [Fact]
    public void FormalMapGenerationIsDeterministicAndOffersOneToThreeRoutes()
    {
        MapItem first = new MapItem("maps-stable", 8).EnsureFormal(77);
        MapItem replay = new MapItem("maps-stable", 8).EnsureFormal(77);

        Assert.Equal(first.AreaId, replay.AreaId);
        Assert.Equal(first.Altar, replay.Altar);
        Assert.Equal(first.EffectiveRouteCandidates, replay.EffectiveRouteCandidates);
        Assert.InRange(first.EffectiveRouteCandidates.Count, 1, 3);
        Assert.Equal(12, MapCatalog.Areas.Count);
    }

    [Fact]
    public void FormalMapGenerationReplacesUnknownLegacyAreaId()
    {
        var legacy = new MapItem(
            "legacy-map",
            6,
            AreaId: "legacy.unknown.area",
            RouteCandidates: [MapRoute.Safe]);

        MapItem formal = legacy.EnsureFormal(91);

        Assert.True(MapCatalog.TryGet(formal.AreaId, out _));
        Assert.NotEqual(legacy.AreaId, formal.AreaId);
        Assert.InRange(formal.EffectiveRouteCandidates.Count, 1, 3);
    }

    [Fact]
    public void MapCraftingConsumesTheMatchingMetalAndLocksAfterCorruption()
    {
        var economy = EconomyWith(MapCraftOperation.AlchemicalRare, MapCraftOperation.Corrupt);
        MapItem map = new MapItem("maps-craft", 6).EnsureFormal(1);

        MapCraftResult rare = MapCrafting.Apply(economy, map, MapCraftOperation.AlchemicalRare, 2);
        MapCraftResult corrupted = MapCrafting.Apply(economy, rare.Map!, MapCraftOperation.Corrupt, 3);

        Assert.True(rare.Succeeded);
        Assert.Equal(MapRarity.Rare, rare.Map!.Rarity);
        Assert.Equal(4, rare.Map.EffectiveAffixes.Count);
        Assert.True(corrupted.Succeeded);
        if (!corrupted.Destroyed)
        {
            Assert.True(corrupted.Map!.IsCorrupted);
            MapCraftResult locked = MapCrafting.Apply(economy, corrupted.Map, MapCraftOperation.ChaosReroll, 4);
            Assert.False(locked.Succeeded);
        }
    }

    [Fact]
    public void TeamPolicyHonorsCandidatesBlocksAndMapFilter()
    {
        MapItem map = new MapItem("maps-policy", 12).EnsureFormal(9) with
        {
            RouteCandidates = [MapRoute.Safe, MapRoute.Abyss, MapRoute.LifeGarden],
        };
        var policy = ExpeditionPolicy.Recommended with
        {
            RoutePriority = [MapRoute.Abyss, MapRoute.LifeGarden, MapRoute.Safe],
            BlockedRoutes = [MapRoute.Abyss],
            MapFilter = new MapFilter(MaximumTier: 12),
        };

        Assert.Equal(MapRoute.LifeGarden, policy.SelectUnattendedRoute(map, 100, 11));
    }

    [Fact]
    public void LockedWorldRefusesTierSeventeenButBreakthroughUnlocksIt()
    {
        TeamBuild build = Build();
        var locked = new WorldState(build, build);
        MapItem map = new MapItem("maps-t17", 17).EnsureFormal(5) with { SelectedRoute = MapRoute.Safe };
        if (!map.EffectiveRouteCandidates.Contains(MapRoute.Safe))
            map = map with { RouteCandidates = [MapRoute.Safe], SelectedRoute = MapRoute.Safe };
        Assert.True(locked.Hero.Queue.TryEnqueue(map));

        new WorldSimulator(new MapAttemptResolver()).Simulate(locked, 1, 4);
        Assert.True(locked.Hero.IsStopped);
        Assert.Equal("tier_locked", locked.Hero.StopReason);

        var unlocked = new WorldState(build, build);
        unlocked.UnlockFinalMapTiers();
        Assert.Equal(20, unlocked.MaximumUnlockedMapTier);
    }

    [Fact]
    public void AtlasUsesOnePermanentGoldPurchasedAllocationAndRoundTrips()
    {
        var state = new EndgameState();
        state.RecordMapCompletion(new MapItem("maps-atlas", 1), MapRoute.Safe, 1);
        var economy = new TownEconomyState(gold: 100);
        Assert.True(state.TryPurchaseAtlas("atlas.atlas.map.01", economy));

        EndgameState restored = EndgameState.Restore(state.Capture());
        Assert.Contains("atlas.atlas.map.01", restored.AtlasPassives);
    }

    [Fact]
    public void OnlineFormalMapWaitsThirtySecondsButThenUsesPolicy()
    {
        TeamBuild build = Build();
        var world = new WorldState(build, build);
        MapItem map = new MapItem("maps-timeout", 2).EnsureFormal(8) with
        {
            RouteCandidates = [MapRoute.Safe, MapRoute.Abyss],
            SelectedRoute = null,
        };
        Assert.True(world.Hero.Queue.TryEnqueue(map));
        var simulator = new WorldSimulator(new ImmediateResolver());

        simulator.Simulate(world, 1, 9);
        Assert.Null(world.Hero.ActiveMap);
        Assert.Equal(30_000, world.Hero.RouteDecisionRemainingMilliseconds);
        simulator.Simulate(world, 29_999, 9);
        Assert.Null(world.Hero.ActiveMap);
        simulator.Simulate(world, 1, 9);
        Assert.NotNull(world.Hero.ActiveMap);
    }

    [Fact]
    public void BatchCraftStopsAndKeepsMapWhenMaterialsRunOut()
    {
        GameSession session = GameSession.CreateNew(new PlayerIdentity("制图测试", CharacterGender.Androgynous,
            CharacterSkinTone.Umber, CharacterHairStyle.Cropped, BaseClass.Fighter), 12);
        session.World.MapInventory.Add(new MapItem("maps-batch", 4));
        session.World.Economy.AddMetal(MetalCurrencyKind.PolishingCobalt, 10);

        MapBatchResult result = session.BatchCraftMaps(new MapBatchRule());

        Assert.Equal(4, result.MetalsSpent);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(20, session.World.MapInventory[0].Quality);
        Assert.Equal(MapRarity.Basic, session.World.MapInventory[0].Rarity);
    }

    [Fact]
    public void BatchCraftCanFillRareMapToSixAffixesWithExaltedGold()
    {
        GameSession session = GameSession.CreateNew(new PlayerIdentity("崇高制图", CharacterGender.Androgynous,
            CharacterSkinTone.Umber, CharacterHairStyle.Cropped, BaseClass.Fighter), 120);
        MapItem rare = new MapItem("maps-exalted", 12, Rarity: MapRarity.Rare,
            Affixes: MapItemRules.RollAffixes(MapRarity.Rare, 12, 4)).EnsureFormal(4);
        session.World.MapInventory.Add(rare);
        session.World.Economy.AddMetal(MetalCurrencyKind.ExaltedGold, 2);

        MapBatchResult result = session.BatchCraftMaps(new MapBatchRule(
            TargetRarity: MapRarity.Rare, MinimumQuality: 0, FillAffixes: true));

        Assert.Equal(1, result.Completed);
        Assert.Equal(2, result.MetalsSpent);
        Assert.Equal(6, session.World.MapInventory[0].EffectiveAffixes.Count);
        Assert.Equal(6, session.World.MapInventory[0].EffectiveAffixes.Select(affix => affix.Kind).Distinct().Count());
    }

    [Fact]
    public void IncompatibleTestDatabaseCanBeArchivedUnderLegacyRecovery()
    {
        string root = Path.Combine(Path.GetTempPath(), "GameForWork.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            using var repository = new SaveRepository(root, 1);
            repository.Initialize();
            repository.SaveCampaignSessionJson("{\"FormatVersion\":0}");

            string archived = repository.ArchiveLegacyAndReset();

            Assert.True(File.Exists(archived));
            Assert.Null(repository.LoadCampaignSessionJson());
            Assert.StartsWith(repository.LegacyRecoveryDirectory, archived, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static TownEconomyState EconomyWith(params MapCraftOperation[] operations)
    {
        Dictionary<MetalCurrencyKind, int> metals = Enum.GetValues<MetalCurrencyKind>().ToDictionary(kind => kind, _ => 0);
        foreach (MapCraftOperation operation in operations) metals[MapCrafting.Cost(operation).Currency]++;
        return new TownEconomyState(metalCurrencies: metals);
    }

    private static TeamBuild Build() => new(
        new CharacterSheet(100, CharacterAttributes.IronOathStarting, new DefensiveEquipment(500, 50, 50)),
        Weapons.RustedGreatsword,
        new SkillConfiguration(SkillIds.HeavyStrike, SkillSupport.Bleed));

    private sealed class ImmediateResolver : ICampaignMapAttemptResolver
    {
        public MapAttemptResult Resolve(MapItem map, MapRoute route, TeamBuild team, int attempt, ulong seed) =>
            new(true, [new MapNodeResult(1, "test", false, BattleOutcome.HeroVictory, 1, "ok")], string.Empty);
    }
}
