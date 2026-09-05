using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Economy;
using GameForWork.Core.Expeditions;
using GameForWork.Core.Endgame;

namespace GameForWork.Tests;

public sealed class EconomyFeatureTests
{
    [Fact]
    public void DropBudgetUsesSealedRarityCoefficients()
    {
        Assert.Equal(100, DropFormula.EnemyCoefficient(EnemyRarity.Normal));
        Assert.Equal(250, DropFormula.EnemyCoefficient(EnemyRarity.Magic));
        Assert.Equal(800, DropFormula.EnemyCoefficient(EnemyRarity.Rare));
        Assert.Equal(3_000, DropFormula.EnemyCoefficient(EnemyRarity.Boss));
    }

    [Fact]
    public void AuditIsDeterministicAndMeetsBaselineTargets()
    {
        IReadOnlyList<AuditResult> first = EconomyAudit.Run(2_000, 77);
        IReadOnlyList<AuditResult> replay = EconomyAudit.Run(2_000, 77);

        Assert.Equal(first, replay);
        AuditResult t1 = first.Single(result => result.Bracket.Name == "T1");
        AuditResult t16 = first.Single(result => result.Bracket.Name == "T16");
        AuditResult t20 = first.Single(result => result.Bracket.Name == "T20");
        AuditResult boss = first.Single(result => result.Bracket.Name == "Boss");
        Assert.InRange(t1.AverageMaps, 1.10, 1.20);
        Assert.InRange(t16.AverageMaps, 0.90, 1.05);
        Assert.InRange(t20.AverageMaps, 0.855, 0.945);
        Assert.InRange(t1.LegendaryRate, 0.02, 0.05);
        Assert.InRange(boss.LegendaryRate, 0.06, 0.10);
    }

    [Fact]
    public void QuantityDoesNotScaleFixedBossProgressOrCitadelFragments()
    {
        var director = new ExpeditionDirector();
        var doubled = new MapItem("quantity-map", 12, Quality: 20);
        director.RecordResolved(doubled, succeeded: true, seed: 12);
        Assert.Equal(1, director.MapsTowardNextFragment);

        var endgame = new EndgameState();
        endgame.RecordMapCompletion(doubled, MapRoute.Safe, 12);
        Assert.Equal(1, endgame.CitadelFragments);
    }

    [Fact]
    public void EstimatedValueCanDriveIgnoreRuleAndProtectedItemsStayKept()
    {
        ItemInstance item = ItemGenerator.Generate("core.base.iron_ring", 70, ItemRarity.Magic, 11, "value-ring");
        int value = ItemValue.Estimate(item);
        var filter = new LootFilter([
            new LootFilterRule("value.ignore", LootDisposition.Ignore, MinimumEstimatedValue: value),
        ]);
        Assert.Equal(LootDisposition.Ignore, filter.Evaluate(item));
        Assert.Equal(LootDisposition.Keep, filter.Evaluate(item with { IsLocked = true }));
        var storage = new EquipmentStorage();
        Assert.True(storage.TryStore(item with { InstanceId = "discovered-ring" }));
        LootProcessingResult processed = LootProcessor.Process([item], storage, filter,
            StorageFullBehavior.AcceptStackablesOnly);
        Assert.Equal(1, processed.Ignored);
        Assert.Equal(0, processed.GoldGained);
        Assert.Equal(0, processed.IronScrapsGained);
    }

    [Fact]
    public void SelectedLegendaryExchangeCostsTwelveMarks()
    {
        var economy = new TownEconomyState(wardenMarks: 12);
        Assert.True(economy.TryExchangeLegendary("core.unique.silent_anvil", out ItemInstance? item));
        Assert.Equal("core.unique.silent_anvil", item!.LegendaryRule!.StableId);
        Assert.Equal(0, economy.WardenMarks);
    }
}
