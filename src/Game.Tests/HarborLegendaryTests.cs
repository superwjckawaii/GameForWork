using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Combat;
using GameForWork.Core.Equipment;
using GameForWork.Core.Harbor;
using GameForWork.Core.Content;
using GameForWork.Core.SkillCatalog;
using GameForWork.Core.Town;

namespace GameForWork.Tests;

public sealed class HarborLegendaryTests
{
    [Fact]
    public void HarborLegendaryCatalogIsCompleteAndExcludedFromNormalFatefulCrafting()
    {
        Assert.Equal(8, HarborRunner.LegendaryIds.Count);
        Assert.All(HarborRunner.LegendaryIds, id =>
        {
            EquipmentLegendaryEntry entry = Assert.Single(EquipmentCatalog.LegendaryItems, value => value.Id == id);
            Assert.Equal("Legendary", entry.Rarity);
            ItemInstance item = EquipmentLegendaryFactory.Create(id, 120, $"test:{id}");
            Assert.Equal(id, item.LegendaryCatalogId);
            Assert.Empty(EquipmentLegendaryFactory.ForBase(item.Base));
        });
    }

    [Fact]
    public void HarborCandidatesCanProduceAUniqueAndPreserveDistinctCandidateIdentities()
    {
        TeamBuild build = Team();
        HarborRun run = Enumerable.Range(1, 32).Select(seed => HarborRunner.Run($"candidate-{seed}", 3, build, (ulong)seed))
            .First(value => value.Candidates.Any(item => !string.IsNullOrWhiteSpace(item.LegendaryCatalogId)));
        Assert.True(run.IsValid);
        Assert.Contains(run.Candidates, item => item.DropSource == "沉金港" && !string.IsNullOrWhiteSpace(item.LegendaryCatalogId));
        Assert.Equal(3, run.Candidates.Select(item => string.IsNullOrWhiteSpace(item.LegendaryCatalogId) ? item.Base.StableId : item.LegendaryCatalogId)
            .Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void ReverseTideAndLighthouseUseMovementAndDistanceConditions()
    {
        TeamBuild build = Team(Loadout("harbor.legendary.reverse_tide_edge"));
        var hero = new ResourceState(build.Sheet);
        var runtime = new EquipmentCombatRuntime(build.CombatEquipment!, 1);
        for (int tick = 0; tick < 20; tick++) runtime.Advance(tick, hero, true);
        runtime.BeginAction(SkillIds.HeavyStrike, 0, 0, false, null);
        Assert.Equal(11_750, runtime.HitMultiplier(build, hero, SkillTag.Attack | SkillTag.Melee, "enemy", 100, 100,
            false, false, false, 1_000, 1, 20));
        Assert.Equal(10_000, runtime.HitMultiplier(build, hero, SkillTag.Attack | SkillTag.Melee, "enemy", 100, 100,
            false, false, false, 1_000, 1, 20));

        build = Team(Loadout("harbor.legendary.lighthouse_watch"));
        runtime = new EquipmentCombatRuntime(build.CombatEquipment!, 2);
        Assert.Equal(13_000, runtime.HitMultiplier(build, hero, SkillTag.Projectile, "enemy", 100, 100,
            false, false, false, 6_000, 1, 0));
        Assert.Equal(8_000, runtime.HitMultiplier(build, hero, SkillTag.Projectile, "enemy", 100, 100,
            false, false, false, 2_000, 1, 0));
    }

    [Fact]
    public void LastHoldConsumesTemporaryMercyAndTwinTideAlternatesDamageWindows()
    {
        TeamBuild build = Team(Loadout("harbor.legendary.last_hold"));
        var hero = new ResourceState(build.Sheet);
        var runtime = new EquipmentCombatRuntime(build.CombatEquipment!, 3);
        var virtues = new VirtueViceState();
        virtues.Gain(VirtueViceKind.Mercy); virtues.Gain(VirtueViceKind.Mercy);
        int lethal = hero.Life + hero.Shield;
        Assert.Equal(lethal - 1, runtime.ApplyEnemyDamage(hero, lethal, true, 0, virtues));
        Assert.Equal(0, virtues.Layers(VirtueViceKind.Mercy));
        Assert.True(hero.IsAlive);
        runtime.ApplyEnemyDamage(hero, hero.MaximumLife, true, 1, virtues);
        Assert.False(hero.IsAlive);

        build = Team(Loadout("harbor.legendary.twin_tide_weaver"));
        hero = new ResourceState(build.Sheet);
        runtime = new EquipmentCombatRuntime(build.CombatEquipment!, 4);
        runtime.BeginAction(SkillIds.HeavyStrike, 0, 0, false, null);
        runtime.OnHit(hero, SkillTag.Attack, "enemy", false, false, 100, null, 10);
        Assert.Equal(12_500, runtime.HitMultiplier(build, hero, SkillTag.Spell, "enemy", 100, 100,
            false, false, false, 0, 1, 20));
    }

    [Fact]
    public void UnreturningWakeAndEmptyBottleApplyOnlyTheirSpecifiedConditions()
    {
        TeamBuild build = Team(Loadout("harbor.legendary.unreturning_wake"));
        var hero = new ResourceState(build.Sheet);
        var runtime = new EquipmentCombatRuntime(build.CombatEquipment!, 5);
        runtime.Advance(0, hero, true);
        Assert.Equal(7_000, runtime.IncomingMultiplier(hero.Sheet, EnemyDamageType.Fire, false, 0));
        runtime.Advance(1, hero, false);
        Assert.Equal(11_000, runtime.IncomingMultiplier(hero.Sheet, EnemyDamageType.Fire, true, 1));

        build = Team(Loadout("harbor.legendary.empty_bottle_oath")) with { Flasks = [FlaskKind.Life] };
        hero = new ResourceState(build.Sheet);
        hero.ApplyDamage(hero.MaximumShield + 200, 0);
        var rack = new FlaskRack(build);
        Assert.NotNull(rack.TryUse(FlaskKind.Life, hero, new GameForWork.Core.Simulation.Pcg32(7)));
        Assert.Equal(0, rack.Bottles[0].RemainingRecovery);
        rack.Bottles[0].Charges = 0;
        rack.GainCharges(10);
        Assert.Equal(6, (int)rack.Bottles[0].Charges);
    }

    [Fact]
    public void ThreeTidesResonanceAcceptsThreeDifferentEnchantmentsOnly()
    {
        ItemInstance item = EquipmentLegendaryFactory.Create("harbor.legendary.three_tides_resonance", 120, "amulet-test");
        string[] choices = EquipmentEnchantmentCatalog.All.Where(value => value.DisplayName is "泰坦王印" or "逐风王印" or "万象王印" or "星海王印")
            .Select(value => value.StableId).Take(4).ToArray();
        Assert.True(choices.Length >= 4);
        for (int index = 0; index < 3; index++)
        {
            CraftResult result = EnchantmentCatalog.Preview(item, choices[index], 99);
            Assert.True(result.Succeeded, result.FailureReason);
            item = result.Result!;
        }
        Assert.Equal(3, item.AllEnchantments.Count);
        CraftResult full = EnchantmentCatalog.Preview(item, choices[3], 99);
        Assert.False(full.Succeeded);
        CraftResult duplicate = EnchantmentCatalog.Preview(item, choices[0], 99);
        Assert.False(duplicate.Succeeded);
    }

    [Fact]
    public void HarborBaseRulesReachCombatRuntime()
    {
        EquipmentCombatLoadout loadout = new(
            new Dictionary<ItemModifierKind, int>(), [], new Dictionary<string, int>(),
            BaseRuleValues: new Dictionary<string, int>
            {
                ["harbor.base.anchored_belt"] = 1_000,
                ["harbor.base.ballast_plate"] = 5,
                ["harbor.base.wavebreaker_gloves"] = 1_000,
                ["harbor.base.tidewalker_rapier"] = 1_500,
                ["harbor.base.tidewading_boots"] = 1,
                ["harbor.base.backflow_belt"] = 7_500,
            });
        TeamBuild build = Team(loadout);
        var hero = new ResourceState(build.Sheet);
        var runtime = new EquipmentCombatRuntime(loadout, 6);
        Assert.Equal(90, runtime.ApplyEnemyDamage(hero, 100, true, 0, null));
        var virtues = new VirtueViceState();
        runtime.Advance(100, hero, false, virtues);
        Assert.Equal(1, virtues.Layers(VirtueViceKind.Mercy));
        runtime.Advance(101, hero, true, virtues);
        Assert.Equal(0, runtime.SpeedBonus(101));
        Assert.Equal(1_500, runtime.AttackSpeedBonus(101));
        Assert.Equal(1_000, runtime.CastSpeedBonus(101));
        Assert.True(runtime.IsImmune(Ailment.Bleed));
        Assert.True(runtime.IsImmune(Ailment.Ignite));
        Assert.Equal(7_500, loadout.BaseRuleValue("harbor.base.backflow_belt"));
    }

    [Fact]
    public void HarborFirstBatchBaseRulesUseTheirActualCombatWindows()
    {
        EquipmentCombatLoadout loadout = new(
            new Dictionary<ItemModifierKind, int>(), [], new Dictionary<string, int>(),
            BaseRuleValues: new Dictionary<string, int>
            {
                ["harbor.base.cablecleaver_axe"] = 2_000,
                ["harbor.base.sunken_anchor_maul"] = 1_500,
                ["harbor.base.tideskimmer_bow"] = 1_200,
                ["harbor.base.tidal_wand"] = 500,
                ["harbor.base.returning_tide_shield"] = 3_000,
                ["harbor.base.downstream_quiver"] = 3_000,
            });
        TeamBuild build = Team(loadout);
        var hero = new ResourceState(build.Sheet);
        var runtime = new EquipmentCombatRuntime(loadout, 7);
        runtime.BeginTick(0);
        runtime.BeginAction(SkillIds.HeavyStrike, 0, 50, false, null);
        Assert.Equal(12_500, runtime.HitMultiplier(build, hero, SkillTag.Attack, "enemy", 100, 100,
            false, false, false, 1_000, 1, 0));

        loadout = loadout with { BaseRuleValues = new Dictionary<string, int>
        {
            ["harbor.base.cablecleaver_axe"] = 2_000,
            ["harbor.base.sunken_anchor_maul"] = 1_500,
            ["harbor.base.tideskimmer_bow"] = 1_200,
            ["harbor.base.returning_tide_shield"] = 3_000,
            ["harbor.base.downstream_quiver"] = 3_000,
        }};
        runtime = new EquipmentCombatRuntime(loadout, 8);
        runtime.Advance(0, hero, true);
        Assert.Equal(11_200, runtime.HitMultiplier(build, hero, SkillTag.Attack | SkillTag.Projectile, "enemy", 100, 100,
            false, false, false, 1_000, 1, 1));
        runtime.Blocked(2, false);
        Assert.Equal(13_000, runtime.HitMultiplier(build, hero, SkillTag.Attack, "enemy", 100, 100,
            false, false, false, 1_000, 1, 2));
        runtime.OnHit(hero, SkillTag.Attack | SkillTag.Projectile, "enemy", false, false, 100, null, 3);
        Assert.Equal(3_000, runtime.MovementBonus(3));
    }

    [Fact]
    public void HarborUsesDedicatedEnemyAndBossIdentitiesPerRegion()
    {
        Assert.Equal(9, Enemies.HarborEnemies.Count);
        Assert.Equal(3, Enemies.HarborElites.Count);
        Assert.Equal(3, Bosses.HarborBosses.Count);
        Assert.All(Enemies.HarborEnemies.Concat(Enemies.HarborElites), enemy =>
        {
            Assert.StartsWith("harbor.", enemy.StableId);
            Assert.NotEmpty(enemy.EffectiveSkills);
        });
        Assert.Equal(3, HarborRunner.EnemyPools.Count);
        Assert.All(HarborRunner.EnemyPools, pool => Assert.Equal(3, pool.Count));
        Assert.All(Bosses.HarborBosses, boss => Assert.NotNull(Bosses.TryGet(boss.StableId)));
        Assert.All(Bosses.HarborBosses, boss => Assert.NotEmpty(Bosses.CombatProfile(boss.StableId).EffectiveSkills));
    }

    private static EquipmentCombatLoadout Loadout(string legendaryId) => new(
        new Dictionary<ItemModifierKind, int>(), [legendaryId], new Dictionary<string, int>());

    private static TeamBuild Team(EquipmentCombatLoadout? equipment = null) => new(
        new CharacterSheet(20, new(100, 100, 100, 100), new(100, 0, 100), FlatMaximumLife: 1_000, FlatMaximumMana: 1_000),
        new("test", 100, 100, 2_000, 0), new(SkillIds.HeavyStrike, SkillSupport.None),
        UseWarCry: false, AlwaysHit: true, CombatEquipment: equipment ?? EquipmentCombatLoadout.Empty);
}
