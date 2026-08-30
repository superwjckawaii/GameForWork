using GameForWork.Core.P1;
using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P1.World;
using GameForWork.Core.P2;
using GameForWork.Core.P4;

namespace GameForWork.Tests;

public sealed class P4SpatialCombatTests
{
    [Fact]
    public void WholeEnemyGroupSpawnsTogetherWithoutForcedRoleComposition()
    {
        P4NodeCombatResult result = Run(PowerfulBuild(), 12, seed: 71);
        P4SpatialFrame first = result.Frames[0];

        Assert.Equal(12, first.Enemies.Count);
        Assert.Equal(12, first.Enemies.Select(enemy => enemy.Position).Distinct().Count());
        Assert.InRange(first.Enemies.Select(enemy => enemy.Role).Distinct().Count(), 1, 6);
        Assert.All(first.Enemies, enemy => Assert.True(enemy.Life > 0));
    }

    [Fact]
    public void SpatialCombatMovesAndUsesAreaProjectileAndChainSkills()
    {
        P4NodeCombatResult result = Run(PowerfulBuild(), 12, seed: 99);

        Assert.Equal(P1BattleOutcome.HeroVictory, result.Outcome);
        Assert.Contains(result.Events, item => item.Kind == P4SpatialEventKind.HeroMoved);
        Assert.Contains(result.Events, item => item.Kind == P4SpatialEventKind.EarthCleave);
        Assert.Contains(result.Events, item => item.Kind == P4SpatialEventKind.SpiritBladeLaunched);
        Assert.Contains(result.Events, item => item.Kind == P4SpatialEventKind.ChainHit);
        Assert.Equal(0, result.Frames[^1].Enemies.Count(enemy => enemy.Life > 0));
    }

    [Fact]
    public void SpatialCombatIsDeterministic()
    {
        P4NodeCombatResult first = Run(PowerfulBuild(), 16, seed: 1_337);
        P4NodeCombatResult second = Run(PowerfulBuild(), 16, seed: 1_337);

        Assert.Equal(first.FinalHash, second.FinalHash);
        Assert.Equal(first.Events, second.Events);
        Assert.Equal(
            first.Frames.Select(frame => $"{frame.AtMilliseconds}:{frame.HeroPosition}:{string.Join(',', frame.Enemies.Select(enemy => $"{enemy.EntityId}:{enemy.Life}:{enemy.Position}"))}"),
            second.Frames.Select(frame => $"{frame.AtMilliseconds}:{frame.HeroPosition}:{string.Join(',', frame.Enemies.Select(enemy => $"{enemy.EntityId}:{enemy.Life}:{enemy.Position}"))}"));
    }

    [Fact]
    public void UnlimitedSpatialBattleResolvesDeadlockWithoutTimeoutOrUnboundedFrames()
    {
        CharacterSheet sheet = new(100, new CharacterAttributes(300, 100, 100, 100),
            new DefensiveEquipment(20_000, 100, 0), FlatMaximumLife: 10_000, FlatLifeRegeneration: 10_000);
        var weapon = new WeaponProfile("slow-spatial-progress", 1, 1, 1_000, 0);
        var skill = new SkillConfiguration(P1SkillIds.HeavyStrike, SkillSupport.None);
        var build = new P1TeamBuild(sheet, weapon, skill, UseWarCry: false, ActiveSkills: [skill]);

        P4NodeCombatResult result = new P4SpatialCombatRunner().Run(new P4NodeCombatRequest(
            build, 1, 100, 1, HasElite: true, HasBoss: true, AbyssRoute: false, Formation: 0), 771);

        Assert.NotEqual(P1BattleOutcome.Timeout, result.Outcome);
        Assert.InRange(result.Ticks, 1, 2_400);
        Assert.True(result.Frames.Count < 700);
    }

    [Fact]
    public void WorkshopConsumesRecipeSpecificMetalOnly()
    {
        var economy = new TownEconomyState(metalCurrencies: new Dictionary<MetalCurrencyKind, int>
        {
            [MetalCurrencyKind.TemperingIron] = 2,
            [MetalCurrencyKind.WardSteel] = 2,
            [MetalCurrencyKind.VitalSilver] = 2,
        });
        ItemInstance weapon = ItemGenerator.Generate("core.base.rusted_greatsword", 4, ItemRarity.Basic, 8, "metal-test");

        P2WorkshopPreview result = P2Workshop.Craft(economy, weapon, P2WorkshopRecipe.WeaponPhysical);

        Assert.True(result.Succeeded);
        Assert.Equal(MetalCurrencyKind.TemperingIron, result.MetalCostKind);
        Assert.Equal(1, economy.MetalAmount(MetalCurrencyKind.TemperingIron));
        Assert.Equal(2, economy.MetalAmount(MetalCurrencyKind.WardSteel));
        Assert.Equal(0, economy.Gold);
        Assert.Equal(0, economy.IronScraps);
    }

    [Fact]
    public void MetalWalletSurvivesWorldSnapshot()
    {
        P1GameSession session = P1GameSession.CreateNew(new PlayerIdentity(
            "铸行者", CharacterGender.Androgynous, CharacterSkinTone.Umber,
            CharacterHairStyle.Braided, P23BaseClass.Fighter), 17);
        session.World.Economy.AddMetal(MetalCurrencyKind.ChaosGold, 4);

        P1GameSession restored = P1GameSession.Restore(session.Capture());

        Assert.Equal(4, restored.World.Economy.MetalAmount(MetalCurrencyKind.ChaosGold));
        Assert.Equal(3, restored.World.Economy.MetalAmount(MetalCurrencyKind.TemperingIron));
    }

    private static P4NodeCombatResult Run(P1TeamBuild build, int count, ulong seed) =>
        new P4SpatialCombatRunner().Run(new P4NodeCombatRequest(
            build, 1, 5, count, HasElite: true, HasBoss: false, AbyssRoute: false, Formation: 0), seed);

    private static P1TeamBuild PowerfulBuild() => new(
        new CharacterSheet(60, new CharacterAttributes(250, 160, 140, 120),
            new DefensiveEquipment(700, 160, 220), FlatMaximumLife: 1_600),
        new WeaponProfile("test.p4", 160, 220, 1_700, 1_000),
        new SkillConfiguration(P1SkillIds.HeavyStrike, SkillSupport.Bleed),
        FlatAccuracy: 1_200,
        IncreasedDamageBasisPoints: 4_000,
        IncreasedCriticalChanceBasisPoints: 1_000,
        IncreasedBleedChanceBasisPoints: 2_000,
        MovementSpeedBasisPoints: 12_000,
        ActiveSkills:
        [
            new SkillConfiguration(P1SkillIds.HeavyStrike, SkillSupport.Bleed),
            new SkillConfiguration(P1SkillIds.EarthCleave, SkillSupport.IncreasedArea),
            new SkillConfiguration(P1SkillIds.SpiritBlade, SkillSupport.Chain),
        ]);
}
