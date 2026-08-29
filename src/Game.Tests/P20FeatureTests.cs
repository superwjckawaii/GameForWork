using GameForWork.Core.P1.Items;
using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.World;
using GameForWork.Core.P20;
using GameForWork.Core.P5;
using GameForWork.Core.P10;

namespace GameForWork.Tests;

public sealed class P20FeatureTests
{
    [Fact]
    public void DropBudgetUsesSealedRarityCoefficients()
    {
        Assert.Equal(100, P20DropFormula.EnemyCoefficient(EnemyRarity.Normal));
        Assert.Equal(250, P20DropFormula.EnemyCoefficient(EnemyRarity.Magic));
        Assert.Equal(800, P20DropFormula.EnemyCoefficient(EnemyRarity.Rare));
        Assert.Equal(3_000, P20DropFormula.EnemyCoefficient(EnemyRarity.Boss));
    }

    [Fact]
    public void AuditIsDeterministicAndMeetsBaselineTargets()
    {
        IReadOnlyList<P20AuditResult> first = P20EconomyAudit.Run(2_000, 77);
        IReadOnlyList<P20AuditResult> replay = P20EconomyAudit.Run(2_000, 77);

        Assert.Equal(first, replay);
        P20AuditResult t1 = first.Single(result => result.Bracket.Name == "T1");
        P20AuditResult t16 = first.Single(result => result.Bracket.Name == "T16");
        P20AuditResult t20 = first.Single(result => result.Bracket.Name == "T20");
        P20AuditResult boss = first.Single(result => result.Bracket.Name == "Boss");
        Assert.InRange(t1.AverageMaps, 1.05, 1.14);
        Assert.InRange(t16.AverageMaps, 0.90, 1.05);
        Assert.InRange(t20.AverageMaps, 0.90, 1.05);
        Assert.InRange(t1.LegendaryRate, 0.02, 0.05);
        Assert.InRange(boss.LegendaryRate, 0.06, 0.10);
    }

    [Fact]
    public void QuantityScalesFixedBossProgressAndCitadelFragments()
    {
        var director = new P5ExpeditionDirector();
        var doubled = new P1MapItem("quantity-map", 12, Quality: 20);
        director.RecordResolved(doubled, succeeded: true, seed: 12);
        Assert.InRange(director.MapsTowardNextFragment, 1, 2);

        var endgame = new P10EndgameState();
        endgame.RecordMapCompletion(doubled, MapRoute.Safe, 12);
        Assert.InRange(endgame.CitadelFragments, 1, 2);
    }

    [Fact]
    public void EstimatedValueCanDriveIgnoreRuleAndProtectedItemsStayKept()
    {
        ItemInstance item = ItemGenerator.Generate("core.base.iron_ring", 70, ItemRarity.Magic, 11, "value-ring");
        int value = P20ItemValue.Estimate(item);
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
