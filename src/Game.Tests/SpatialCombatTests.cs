using GameForWork.Core.Campaign;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Management;
using GameForWork.Core.Spatial;

namespace GameForWork.Tests;

public sealed class SpatialCombatTests
{
    [Fact]
    public void WholeEnemyGroupSpawnsTogetherWithoutForcedRoleComposition()
    {
        NodeCombatResult result = Run(PowerfulBuild(), 12, seed: 71);
        SpatialFrame first = result.Frames[0];

        Assert.Equal(12, first.Enemies.Count);
        Assert.Equal(12, first.Enemies.Select(enemy => enemy.Position).Distinct().Count());
        Assert.InRange(first.Enemies.Select(enemy => enemy.Role).Distinct().Count(), 1, 6);
        Assert.All(first.Enemies, enemy => Assert.True(enemy.Life > 0));
    }

    [Fact]
    public void SpatialCombatMovesAndUsesAreaProjectileAndChainSkills()
    {
        NodeCombatResult result = Run(PowerfulBuild(), 12, seed: 99);

        Assert.Equal(BattleOutcome.HeroVictory, result.Outcome);
        Assert.Contains(result.Events, item => item.Kind == SpatialEventKind.HeroMoved);
        Assert.Contains(result.Events, item => item.Kind == SpatialEventKind.EarthCleave);
        Assert.Contains(result.Events, item => item.Kind == SpatialEventKind.SpiritBladeLaunched);
        Assert.Contains(result.Events, item => item.Kind == SpatialEventKind.ChainHit);
        Assert.Equal(0, result.Frames[^1].Enemies.Count(enemy => enemy.Life > 0));
    }

    [Fact]
    public void SpatialCombatIsDeterministic()
    {
        NodeCombatResult first = Run(PowerfulBuild(), 16, seed: 1_337);
        NodeCombatResult second = Run(PowerfulBuild(), 16, seed: 1_337);

        Assert.Equal(first.FinalHash, second.FinalHash);
        Assert.Equal(first.Events, second.Events);
        Assert.Equal(
            first.Frames.Select(frame => $"{frame.AtMilliseconds}:{frame.HeroPosition}:{string.Join(',', frame.Enemies.Select(enemy => $"{enemy.EntityId}:{enemy.Life}:{enemy.Position}"))}"),
            second.Frames.Select(frame => $"{frame.AtMilliseconds}:{frame.HeroPosition}:{string.Join(',', frame.Enemies.Select(enemy => $"{enemy.EntityId}:{enemy.Life}:{enemy.Position}"))}"));
    }

    [Fact]
    public void SpatialCombatAcceptsMaximumKingDisasterAreaRoll()
    {
        NodeCombatResult result = new SpatialCombatRunner().Run(new NodeCombatRequest(
            PowerfulBuild(), 1, 100, 1, HasElite: false, HasBoss: false, AbyssRoute: false,
            Formation: 0, MaximumTicks: 1, EnemyAreaBasisPoints: 34_200), 0x48);

        Assert.Equal(1, result.Ticks);
    }

    [Fact]
    public void HighDamageBleedUsesWideIntermediateArithmeticInAuthoritativeCombat()
    {
        TeamBuild build = PowerfulBuild() with
        {
            Weapon = new WeaponProfile("test.high-damage-bleed", 500_000, 500_000, 1_700, 0),
            IncreasedBleedChanceBasisPoints = 10_000,
            AlwaysHit = true,
        };

        NodeCombatResult result = new SpatialCombatRunner().Run(new NodeCombatRequest(
            build, 1, 100, 1, HasElite: false, HasBoss: true, AbyssRoute: false, Formation: 0,
            MaximumTicks: 100, EnemyLifeBasisPoints: 500_000, BossLifeBasisPoints: 100_000), 0x4801);

        Assert.Contains(result.Events, item => item.Kind == SpatialEventKind.HeavyStrike && item.Value > 0);
    }

    [Fact]
    public void UnlimitedSpatialBattleResolvesDeadlockWithoutTimeoutOrUnboundedFrames()
    {
        CharacterSheet sheet = new(100, new CharacterAttributes(300, 100, 100, 100),
            new DefensiveEquipment(20_000, 100, 0), FlatMaximumLife: 10_000, FlatLifeRegeneration: 10_000);
        var weapon = new WeaponProfile("slow-spatial-progress", 1, 1, 1_000, 0);
        var skill = new SkillConfiguration(SkillIds.HeavyStrike, SkillSupport.None);
        var build = new TeamBuild(sheet, weapon, skill, UseWarCry: false, ActiveSkills: [skill]);

        NodeCombatResult result = new SpatialCombatRunner().Run(new NodeCombatRequest(
            build, 1, 100, 1, HasElite: true, HasBoss: true, AbyssRoute: false, Formation: 0), 771);

        Assert.NotEqual(BattleOutcome.Timeout, result.Outcome);
        Assert.True(result.Ticks > 2_400); // Slow, real progress is not converted into a fabricated victory.
        Assert.True(result.Frames.Count <= 4_096);
        Assert.Equal(BattleOutcome.HeroVictory, result.Outcome);
        Assert.All(result.Frames[^1].Enemies, enemy => Assert.Equal(0, enemy.Life));
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

        WorkshopPreview result = WorkshopCommands.Craft(economy, weapon, WorkshopRecipe.WeaponPhysical);

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
        GameSession session = GameSession.CreateNew(new PlayerIdentity(
            "铸行者", CharacterGender.Androgynous, CharacterSkinTone.Umber,
            CharacterHairStyle.Braided, BaseClass.Fighter), 17);
        session.World.Economy.AddMetal(MetalCurrencyKind.ChaosGold, 4);

        GameSession restored = GameSession.Restore(session.Capture());

        Assert.Equal(4, restored.World.Economy.MetalAmount(MetalCurrencyKind.ChaosGold));
        Assert.Equal(3, restored.World.Economy.MetalAmount(MetalCurrencyKind.TemperingIron));
    }

    private static NodeCombatResult Run(TeamBuild build, int count, ulong seed) =>
        new SpatialCombatRunner().Run(new NodeCombatRequest(
            build, 1, 5, count, HasElite: true, HasBoss: false, AbyssRoute: false, Formation: 0), seed);

    private static TeamBuild PowerfulBuild() => new(
        new CharacterSheet(60, new CharacterAttributes(250, 160, 140, 120),
            new DefensiveEquipment(700, 160, 220), FlatMaximumLife: 1_600),
        new WeaponProfile("test.spatial", 160, 220, 1_700, 1_000),
        new SkillConfiguration(SkillIds.HeavyStrike, SkillSupport.Bleed),
        FlatAccuracy: 1_200,
        IncreasedDamageBasisPoints: 4_000,
        IncreasedCriticalChanceBasisPoints: 1_000,
        IncreasedBleedChanceBasisPoints: 2_000,
        MovementSpeedBasisPoints: 12_000,
        ActiveSkills:
        [
            new SkillConfiguration(SkillIds.HeavyStrike, SkillSupport.Bleed),
            new SkillConfiguration(SkillIds.EarthCleave, SkillSupport.IncreasedArea),
            new SkillConfiguration(SkillIds.SpiritBlade, SkillSupport.Chain),
        ]);
}
