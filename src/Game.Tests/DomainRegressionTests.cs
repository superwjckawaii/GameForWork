using System.Text.Json;
using GameForWork.Core.Builds;
using GameForWork.Core.Campaign;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Characters;
using GameForWork.Core.Equipment;
using GameForWork.Core.Persistence;
using GameForWork.Core.SkillCatalog;
using GameForWork.Core.Spatial;
using Microsoft.Data.Sqlite;

using GameForWork.Core.Campaign.Progression;

namespace GameForWork.Tests;

public sealed class DomainRegressionTests
{
    [Fact]
    public void StarArrowsWaitForReturnAndReuseFinalDamageWithoutRecursion()
    {
        TeamBuild team = Team(SkillIds.SpiritBlade) with { CombatEquipment = Equipment("逐星者余响",
            new() { [ItemModifierKind.ReturnProjectiles] = 1 }) };
        NodeCombatResult result = Run(team, 500);
        SpatialEvent launched = result.Events.First(e => e.Detail.Contains("projectile:star-launched"));
        string originalId = launched.Detail.Split("|source-action:")[1];
        string starId = launched.Detail.Split("|projectile:")[0];
        SpatialEvent completed = result.Events.Single(e => e.Detail.StartsWith($"action:{originalId}|projectile:completed"));
        Assert.Equal(completed.AtMilliseconds, launched.AtMilliseconds);
        Assert.Contains(result.Events, e => e.Detail.StartsWith($"action:{originalId}|projectile:return") && e.AtMilliseconds <= launched.AtMilliseconds);
        Assert.Equal(1, launched.Value);
        SpatialEvent star = Assert.Single(result.Events, e => e.Detail.StartsWith($"{starId}|projectile:star|"));
        Assert.True(star.AtMilliseconds > launched.AtMilliseconds);
        int starIndex = result.Events.ToList().IndexOf(star);
        int originalDamage = result.Events.First(e => e.Kind == SpatialEventKind.SpiritBladeHit && e.Value > 0).Value;
        Assert.Equal(originalDamage * 3_500 / 10_000, result.Events[starIndex - 1].Value);
        Assert.DoesNotContain(result.Events, e => e.Detail.EndsWith($"|source-action:{starId[7..]}"));
        Assert.DoesNotContain(result.Events, e => e.Detail.StartsWith($"{starId}|projectile:return"));
    }

    [Fact]
    public void CohuntUsesFiveDistinctActionsAndClearsOnTargetChangeOrEncounterEnd()
    {
        var runtime = new EquipmentCombatRuntime(Equipment("合猎箭匣"), 1);
        for (int action = 0; action < 5; action++)
        {
            runtime.BeginAction(SkillIds.SpiritBlade, 0, 0, false, null);
            Assert.False(runtime.BeginProjectileAction("boss"));
            runtime.ProjectileHit(true);
            runtime.ProjectileHit(true);
            Assert.Equal(action + 1, runtime.CohuntLayers);
        }
        runtime.BeginAction(SkillIds.SpiritBlade, 0, 0, false, null);
        Assert.True(runtime.BeginProjectileAction("boss"));
        Assert.Equal(0, runtime.CohuntLayers);
        runtime.ProjectileHit(true);
        EquipmentActionContext previous = runtime.CaptureAction();
        runtime.BeginAction(SkillIds.SpiritBlade, 0, 0, false, null);
        Assert.False(runtime.BeginProjectileAction("other"));
        runtime.InAction(previous, () => runtime.ProjectileHit(true));
        Assert.Equal(0, runtime.CohuntLayers);
        runtime.ProjectileHit(true);
        Assert.Equal(1, runtime.CohuntLayers);
        runtime.EndEncounter();
        Assert.Equal(0, runtime.CohuntLayers);
    }

    [Fact]
    public void CohuntActuallyAllowsTheEmpoweredVolleyToHitThePrimaryTarget()
    {
        TeamBuild team = Team(SkillIds.SpiritBlade) with { CombatEquipment = Equipment("合猎箭匣",
            new() { [ItemModifierKind.AdditionalProjectile] = 2 }) };
        var result = new SpatialCombatRunner().Run(new(team, 1, 1, 1, true, false, false, 0,
            MaximumTicks: 1_200, EnemyDamageBasisPoints: 1_000,
            EnemyPool: [Enemies.CorruptedWorker with { Life = 1_000_000, MovementSpeedRawPerSecond = 0 }]), 731);
        var empowered = result.Events.Where(e => e.Detail.Contains("cohunt:True") && e.Detail.Contains("projectile:outbound")).ToArray();
        Assert.NotEmpty(empowered);
        var firstVolley = empowered.GroupBy(e => e.Detail.Split("|projectile:")[0]).First().ToArray();
        Assert.Equal(3, firstVolley.Length);
        Assert.Single(firstVolley, e => e.Detail.EndsWith("scale:10000"));
        Assert.Equal(2, firstVolley.Count(e => e.Detail.EndsWith("scale:4000")));
        Assert.Single(firstVolley.Select(e => e.TargetId).Distinct());
        int[] damage = firstVolley.Select(e => result.Events[result.Events.ToList().IndexOf(e) - 1].Value).ToArray();
        Assert.All(damage.Skip(1), value => Assert.InRange(value, 1, damage[0] - 1));
    }

    [Fact]
    public void StarArrowsCountDistinctEnemiesAndCapAtFive()
    {
        TeamBuild team = Team(SkillIds.SpiritBlade) with { CombatEquipment = Equipment("逐星者余响",
            new() { [ItemModifierKind.AdditionalProjectile] = 7, [ItemModifierKind.ReturnProjectiles] = 1 }),
            PassiveProfile = PassiveModifiers.Empty with { IncreasedSkillRangeBasisPoints = 90_000 } };
        var result = new SpatialCombatRunner().Run(new(team, 1, 1, 8, false, false, false, 0,
            MaximumTicks: 800, EnemyDamageBasisPoints: 1_000,
            EnemyPool: [Enemies.CorruptedWorker with { Life = 1_000_000, MovementSpeedRawPerSecond = 0 }]), 731);
        SpatialEvent launched = result.Events.First(e => e.Detail.Contains("projectile:star-launched"));
        Assert.Equal(5, launched.Value);
        string starId = launched.Detail.Split("|projectile:")[0];
        SpatialEvent[] hits = result.Events.Where(e => e.Detail.StartsWith($"{starId}|projectile:star|")).ToArray();
        Assert.Equal(5, hits.Length);
        Assert.Single(hits.Select(e => e.TargetId).Distinct());
    }

    [Theory]
    [InlineData(SkillIds.HeavyStrike)]
    [InlineData(SkillIds.SpiritBlade)]
    public void UnifiedHitsStillGainSlothFromOaths(string skillId)
    {
        TeamBuild team = Team(skillId) with { VirtueViceLoadout = new(new Dictionary<VirtueViceKind, int>(), [], [VirtueViceKind.Sloth]) };
        var result = new SpatialCombatRunner().Run(new(team, 1, 1, 1, true, false, false, 0,
            MaximumTicks: 1_000, EnemyDamageBasisPoints: 1_000,
            EnemyPool: [Enemies.CorruptedWorker with { Life = 1_000_000, MovementSpeedRawPerSecond = 0 }]), 731);
        Assert.Contains(result.Frames, frame => frame.HeroVirtueViceLayers?.GetValueOrDefault(VirtueViceKind.Sloth) > 0);
    }

    [Fact]
    public void ConvertedAttackUsesEachHistoryStageOnce()
    {
        var damage = DamagePacketRules.ResolveMixed(100, SkillDamageType.Physical, default, SkillSupport.None,
            0, 0, 0, 0, 0, equipment: new Dictionary<ItemModifierKind, int>
            { [ItemModifierKind.PhysicalToFireConversionBasisPoints] = 10_000 },
            modifiers: new(new Dictionary<DamageType, int> { [DamageType.Physical] = 20_000, [DamageType.Fire] = 5_000 }, 4_000));
        Assert.Equal(510, damage.Fire);
        Assert.Equal(0, damage.Physical);
        Assert.Equal(510, damage.Total);
    }

    [Fact]
    public void AddedElementAndConvertedElementBothProduceExtraVoidBeforeScaling()
    {
        var damage = DamagePacketRules.ResolveMixed(100, SkillDamageType.Physical, new AddedWeaponDamage(Fire: 50),
            SkillSupport.None, 0, 0, 0, 0, 0, equipment: new Dictionary<ItemModifierKind, int>
            {
                [ItemModifierKind.PhysicalToColdConversionBasisPoints] = 10_000,
                [ItemModifierKind.ColdToFireConversionBasisPoints] = 10_000,
                [ItemModifierKind.ElementalAsExtraVoidBasisPoints] = 2_000,
            });
        Assert.Equal(150, damage.Fire);
        Assert.Equal(50, damage.Void); // 100 cold, 100 converted fire and 50 original fire.
        Assert.Equal(200, damage.Total);
    }

    [Fact]
    public void OversubscribedConversionDistributesRoundingWithoutInventingPhysicalRemainder()
    {
        var damage = DamagePacketRules.ResolveMixed(7, SkillDamageType.Physical, default, SkillSupport.PhysicalToLightning,
            0, 0, 0, 0, 0, equipment: new Dictionary<ItemModifierKind, int>
            { [ItemModifierKind.PhysicalToFireConversionBasisPoints] = 10_000 });
        Assert.Equal(7, damage.Total);
        Assert.Equal(0, damage.Physical);
        Assert.True(damage.Fire > 0 && damage.Lightning > 0);
    }

    [Fact]
    public void HeavyStrikeActuallyConvertsAndUsesFinalTypeResistance()
    {
        TeamBuild team = Team(SkillIds.HeavyStrike) with { CombatEquipment = Equipment(modifiers: new()
            { [ItemModifierKind.PhysicalToFireConversionBasisPoints] = 10_000 }) };
        int Hit(int resistance)
        {
            NodeCombatResult result = Run(team, 160, resistance);
            SpatialEvent hit = result.Events.First(e => e.Kind == SpatialEventKind.HeavyStrike && e.Value > 0);
            Assert.Contains("physical:0,fire:", hit.Detail);
            return hit.Value;
        }
        int baseResistance = EnemyRules.Scale(Enemies.CorruptedWorker, 1, [], false, EnemyRarity.Normal).FireResistanceBasisPoints;
        int expected = Hit(0) * (5_000 - baseResistance) / (10_000 - baseResistance);
        Assert.InRange(Hit(5_000), expected - 1, expected + 1);
    }

    [Fact]
    public void ProjectilesTravelAndReturnWithoutAnUnspecifiedDamagePenalty()
    {
        TeamBuild team = Team(SkillIds.SpiritBlade) with { CombatEquipment = Equipment(modifiers: new()
            { [ItemModifierKind.ReturnProjectiles] = 1 }) };
        NodeCombatResult result = Run(team, 160);
        SpatialEvent outbound = result.Events.First(e => e.Detail.Contains("projectile:outbound"));
        SpatialEvent returned = result.Events.First(e => e.Detail.Contains("projectile:return"));
        Assert.True(returned.AtMilliseconds > outbound.AtMilliseconds);
        Assert.Contains("equipment-action:", outbound.Detail);
        Assert.Equal(result.FinalHash, Run(team, 160).FinalHash);
    }

    [Fact]
    public void DeferredActionRetainsPaymentStateAndRestoresCurrentAction()
    {
        var runtime = new EquipmentCombatRuntime(Equipment("血税契据"), 1);
        runtime.BeginAction(SkillIds.HeavyStrike, 10, 0, false, null);
        EquipmentActionContext paid = runtime.CaptureAction();
        runtime.BeginAction(SkillIds.HeavyStrike, 0, 0, false, null);
        string current = runtime.ActionId;
        var hero = new ResourceState(Sheet());
        int Multiplier() => runtime.HitMultiplier(Team(SkillIds.HeavyStrike), hero, SkillTag.Attack, "enemy", 100, 100,
            false, false, false, 0, 1, 20);
        Assert.Equal(10_000, Multiplier());
        runtime.InAction(paid, () => Assert.Equal(16_000, Multiplier()));
        Assert.Equal(current, runtime.ActionId);
        Assert.Equal(10_000, Multiplier());
    }

    [Fact]
    public void AutomaticResummonOccursOnlyOncePerMinion()
    {
        const string bone = "archetypes.skill.summon_boneguard";
        TeamBuild team = Team(bone) with { CombatEquipment = Equipment(modifiers: new()
            { [ItemModifierKind.MinionAutomaticResummon] = 1 }) };
        NodeCombatResult result = new SpatialCombatRunner().Run(new(team, 1, 80, 1, false, false, false, 0,
            MaximumTicks: 400, EnemyDamageBasisPoints: 100_000,
            EnemyPool: [Enemies.CorruptedWorker with { Life = 100_000 }]), 731);
        SpatialEvent[] revives = result.Events.Where(e => e.Detail == "unit:Minion|revive").ToArray();
        Assert.NotEmpty(revives);
        Assert.All(revives.GroupBy(e => e.SourceId), group => Assert.Single(group));
        Assert.All(revives, revived => Assert.Contains(result.Events, died => died.SourceId == revived.SourceId &&
            died.Detail == "unit:Minion|death" && revived.AtMilliseconds - died.AtMilliseconds >= 2_000));
    }

    [Fact]
    public void SharedConstructPoolPreservesPercentageAcrossDeploymentAndReplacement()
    {
        var pool = new GameForWork.Core.Combat.SharedLifePool();
        pool.Resize(320);
        pool.Damage(80);
        Assert.Equal(120, pool.MemberLife(160));
        pool.Resize(480);
        Assert.Equal(360, pool.Life);
        Assert.Equal(120, pool.MemberLife(160));
        pool.Resize(320);
        Assert.Equal(240, pool.Life);
        pool.Damage(240);
        Assert.Equal(0, pool.MemberLife(160));
        pool.Resize(0);
        pool.Resize(160);
        Assert.Equal(160, pool.Life);
    }
    [Fact]
    public void CompassRequiresMovementSkillRatherThanOrdinaryWalking()
    {
        var runtime = new EquipmentCombatRuntime(Equipment("界行罗盘"), 1);
        var hero = new ResourceState(Sheet());
        runtime.Advance(0, hero, true);
        Assert.Equal(10_000, runtime.IncomingMultiplier(hero.Sheet, EnemyDamageType.Physical, true, 1));
        runtime.UsedMovementSkill(2);
        Assert.Equal(8_500, runtime.IncomingMultiplier(hero.Sheet, EnemyDamageType.Physical, true, 3));
        Assert.Equal(10_000, runtime.IncomingMultiplier(hero.Sheet, EnemyDamageType.Physical, true, 62));
    }

    [Fact]
    public void SeaWallsPrioritizesShieldBreakAndSharesCooldownWithDotRefill()
    {
        var runtime = new EquipmentCombatRuntime(Equipment("静海双壁"), 1);
        var hero = new ResourceState(Sheet(), initialShield: 100);
        int barrier = runtime.SpiritBarrier(hero.Sheet, 0);
        runtime.ApplyEnemyDamage(hero, 110, false, 0, null);
        Assert.Equal(0, hero.Shield);
        Assert.Equal(barrier * 2, runtime.SpiritBarrier(hero.Sheet, 1));
        runtime.ApplyEnemyDamage(hero, 1, false, 1, null);
        Assert.Equal(0, hero.Shield);
        runtime.ApplyEnemyDamage(hero, 1, false, 120, null);
        Assert.Equal(hero.MaximumShield, hero.Shield);
        Assert.Equal(barrier, runtime.SpiritBarrier(hero.Sheet, 120));
    }

    [Fact]
    public void SelfDamageAndPaymentDoNotActivateSeaWalls()
    {
        var runtime = new EquipmentCombatRuntime(Equipment("静海双壁"), 1);
        var hero = new ResourceState(Sheet(), initialShield: 100);
        hero.ApplyDamage(100, 0);
        Assert.True(hero.TryPayLifeCost(1));
        Assert.Equal(hero.Sheet.SpiritBarrier().Value, runtime.SpiritBarrier(hero.Sheet, 1));
        Assert.Equal(0, hero.Shield);
    }

    [Fact]
    public void DamageOverTimeUsesResistanceAndBarrierButNotHitPenetration()
    {
        var runtime = new EquipmentCombatRuntime(EquipmentCombatLoadout.Empty, 1);
        int plain = runtime.MitigateDamageOverTime(Sheet(), 500, EnemyDamageType.Fire, 0, 2);
        int resistant = runtime.MitigateDamageOverTime(Sheet() with { FireResistanceBasisPoints = 5_000 }, 500, EnemyDamageType.Fire, 0, 2);
        Assert.InRange(resistant, 1, plain - 1);
        Assert.True(runtime.MitigateDamageOverTime(Sheet() with { FlatSpiritBarrier = 10_000 }, 500, EnemyDamageType.Fire, 0, 2) < plain);
    }

    [Fact]
    public void LegacyIdentifiersMigrateWithoutChangingUserNamesOrInstanceIds()
    {
        string oldPrefix = "p" + 30;
        GameSession original = GameSession.CreateNew(new(oldPrefix + ".name", CharacterGender.Androgynous,
            CharacterSkinTone.Fair, CharacterHairStyle.Cropped, BaseClass.Fighter), 7123);
        GameSessionSnapshot snapshot = original.Capture() with { FormatVersion = 24 };
        string json = JsonSerializer.Serialize(snapshot).Replace("builds.", oldPrefix + ".", StringComparison.Ordinal);
        GameSession restored = GameSession.Restore(SaveIdentifierMigration.Deserialize(json));
        Assert.Equal(original.Player.Name, restored.Player.Name);
        Assert.Equal(original.Capture().AllocatedPassives, restored.Capture().AllocatedPassives);
        Assert.Equal(original.HeroEquipment.Items.Values.Select(item => item.InstanceId),
            restored.HeroEquipment.Items.Values.Select(item => item.InstanceId));
        Assert.Equal(GameSession.CurrentFormatVersion, restored.Capture().FormatVersion);
    }

    [Fact]
    public void LegacySupportFieldsAreRewrittenBeforeJsonDeserialization()
    {
        var original = GameSession.CreateNew(new("存档迁移", CharacterGender.Androgynous,
            CharacterSkinTone.Fair, CharacterHairStyle.Cropped, BaseClass.Fighter), 91).Capture();
        string supportId = ActiveSkillCatalog.Supports.First().StoneId;
        var linked = original.World.Hero.Build.HeavyStrike with { SupportLinks = [new(supportId, 17, 13)] };
        original = original with { FormatVersion = 24, World = original.World with
            { Hero = original.World.Hero with { Build = original.World.Hero.Build with { HeavyStrike = linked } } } };
        string oldField = "P" + 30 + "SupportLinks";
        string json = JsonSerializer.Serialize(original).Replace("\"SupportLinks\":", $"\"{oldField}\":", StringComparison.Ordinal);
        var restored = SaveIdentifierMigration.Deserialize(json);
        Assert.Equal(new LinkedSupport(supportId, 17, 13), Assert.Single(restored.World.Hero.Build.HeavyStrike.SupportLinks!));
    }

    [Fact]
    public void LegacySqliteStateTableIsImportedAndDoesNotOverwriteNewerSession()
    {
        string root = Path.Combine(Path.GetTempPath(), "domain-migration-" + Guid.NewGuid().ToString("N"));
        string path;
        try
        {
            using (var repository = new SaveRepository(root, 1))
            {
                repository.Initialize();
                repository.SaveCampaignSessionJson("{\"marker\":\"old\"}");
                path = repository.DatabasePath;
            }
            using (var connection = new SqliteConnection($"Data Source={path}"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = $"ALTER TABLE campaign_state RENAME TO \"p{1}_state\";";
                command.ExecuteNonQuery();
            }
            using (var repository = new SaveRepository(root, 1))
            {
                repository.Initialize();
                Assert.Equal("{\"marker\":\"old\"}", repository.LoadCampaignSessionJson());
                repository.SaveCampaignSessionJson("{\"marker\":\"new\"}");
            }
            using (var repository = new SaveRepository(root, 1))
            {
                repository.Initialize();
                Assert.Equal("{\"marker\":\"new\"}", repository.LoadCampaignSessionJson());
            }
        }
        finally { SqliteConnection.ClearAllPools(); Directory.Delete(root, true); }
    }

    private static CharacterSheet Sheet() => new(20, new(100, 100, 100, 100), new(100, 0, 100),
        FlatMaximumLife: 10_000, FlatMaximumMana: 10_000);
    private static TeamBuild Team(string id) => new(Sheet(), new("test-weapon", 100, 100, 1_000, 0),
        new(SkillIds.HeavyStrike, SkillSupport.None), AlwaysHit: true, CannotCrit: true, UseWarCry: false,
        ActiveSkills: [new(id, SkillSupport.None)]);
    private static EquipmentCombatLoadout Equipment(string? name = null, Dictionary<ItemModifierKind, int>? modifiers = null) =>
        new(modifiers ?? [], name is null ? [] : [EquipmentCatalog.LegendaryItems.Single(item => item.DisplayName == name).Id], new Dictionary<string, int>());
    private static NodeCombatResult Run(TeamBuild team, int ticks, int fireResistance = 0) => new SpatialCombatRunner().Run(
        new(team, 1, 1, 1, false, false, false, 0, MaximumTicks: ticks, EnemyElementalResistanceBasisPoints: fireResistance,
            EnemyPool: [Enemies.CorruptedWorker with { Life = 100_000,
                MovementSpeedRawPerSecond = 0 }]), 731);
}
