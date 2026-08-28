using GameForWork.Core.P1;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P6;
using GameForWork.Core.P2;
using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.World;
using GameForWork.Core.P4;

namespace GameForWork.Tests;

public sealed class P6FeatureTests
{
    [Fact]
    public void SocketRollIsDeterministicAndRespectsItemAndEquipmentCaps()
    {
        foreach (int level in Enumerable.Range(1, 10))
        {
            ItemInstance first = ItemGenerator.Generate(
                "core.base.rusted_greatsword", level, ItemRarity.Rare, 0x6000UL + (ulong)level);
            ItemInstance second = ItemGenerator.Generate(
                "core.base.rusted_greatsword", level, ItemRarity.Rare, 0x6000UL + (ulong)level);
            Assert.Equal(first.LinkedSocketCount, second.LinkedSocketCount);
            Assert.InRange(first.LinkedSocketCount, 2,
                Math.Min(6, P6SocketRules.ItemLevelMaximum(level)));
        }

        ItemInstance ring = ItemGenerator.Generate("core.base.iron_ring", 10, ItemRarity.Rare, 99);
        Assert.Equal(0, ring.LinkedSocketCount);
    }

    [Fact]
    public void LegacyItemsReceiveStableSocketGroupsDuringRestore()
    {
        P1GameSession session = CreateSession();
        P1GameSessionSnapshot legacy = session.Capture() with
        {
            FormatVersion = 8,
            HeroEquipment = session.Capture().HeroEquipment
                .Select(entry => entry with { Item = entry.Item with { LinkedSocketCount = 0 } })
                .ToArray(),
        };

        P1GameSession first = P1GameSession.Restore(legacy);
        P1GameSession second = P1GameSession.Restore(legacy);

        Assert.All(first.GetSkillChains(), chain => Assert.InRange(chain.TotalSockets, 2, 6));
        Assert.Equal(
            first.GetSkillChains().Select(chain => chain.TotalSockets),
            second.GetSkillChains().Select(chain => chain.TotalSockets));
    }

    [Fact]
    public void SkillStoneHasOneLocationAndSupportCanWaitForActive()
    {
        P1GameSession session = CreateSession();
        var management = session.Management;
        var groups = session.GetSkillChains();
        var target = groups[0];
        SkillLinkConfiguration? existing = management.SkillLinks.FirstOrDefault(link => link.ChainId == target.StableId);
        if (existing?.SocketStoneInstanceIds is not null)
        {
            for (int index = 0; index < target.TotalSockets; index++)
            {
                session.UnsocketSkillStone(target.StableId, index);
            }
        }
        SkillStoneInstance support = management.UninstalledSkillStones.First(stone =>
            stone.Definition.Kind == SkillStoneKind.Support);

        Assert.True(session.TryPlaceSkillStone(target.StableId, 1, support.InstanceId));
        SkillLinkConfiguration waiting = management.SkillLinks.Single(link => link.ChainId == target.StableId);
        Assert.Empty(waiting.ActiveStoneInstanceId);
        Assert.Contains(support.InstanceId, management.InstalledSkillStoneIds);
        Assert.DoesNotContain(management.UninstalledSkillStones, stone => stone.InstanceId == support.InstanceId);

        var other = groups[1];
        Assert.True(session.TryPlaceSkillStone(other.StableId, 1, support.InstanceId));
        Assert.Equal(1, management.SkillLinks.Sum(link =>
            (link.SocketStoneInstanceIds ?? []).Count(id => id == support.InstanceId)));
    }

    [Fact]
    public void SkillTagsRejectIncompatibleSupportsAndApplyProjectileModifiers()
    {
        SkillStoneDefinition heavy = P2SkillStones.Get("core.skill_stone.heavy_strike");
        SkillStoneDefinition brutality = P2SkillStones.Get("core.skill_stone.brutality");
        SkillStoneDefinition projectiles = P2SkillStones.Get("core.skill_stone.multiple_projectiles");
        Assert.True(P6SkillCompatibility.Check(heavy, brutality).Compatible);
        Assert.False(P6SkillCompatibility.Check(heavy, projectiles).Compatible);

        P6ResolvedSkill blade = P6CombatSkillRules.Resolve(new SkillConfiguration(
            P1SkillIds.SpiritBlade, SkillSupport.MultipleProjectiles | SkillSupport.FasterProjectiles), 500);
        Assert.Equal(3, blade.ProjectileCount);
        Assert.Equal(15_000, blade.ProjectileSpeedRawPerSecond);
        Assert.True(blade.RangeRaw > P1Skills.SpiritBlade.RangeRaw);
    }

    [Fact]
    public void NewActiveSkillsRunInAuthoritativeSpatialCombat()
    {
        var build = new P1TeamBuild(
            new CharacterSheet(60, new CharacterAttributes(250, 160, 140, 120),
                new DefensiveEquipment(700, 160, 220), FlatMaximumLife: 1_600),
            new WeaponProfile("test.p6", 90, 130, 1_500, 800),
            new SkillConfiguration(P1SkillIds.HeavyStrike, SkillSupport.None),
            FlatAccuracy: 1_200,
            IncreasedDamageBasisPoints: 2_000,
            ActiveSkills:
            [
                new SkillConfiguration(P1SkillIds.HeavyStrike, SkillSupport.None),
                new SkillConfiguration(P1SkillIds.SeismicCharge, SkillSupport.Brutality),
                new SkillConfiguration(P1SkillIds.BloodTideSpin, SkillSupport.Bleed),
                new SkillConfiguration(P1SkillIds.IronOathBanner, SkillSupport.None),
            ]);

        P4NodeCombatResult result = new P4SpatialCombatRunner().Run(new P4NodeCombatRequest(
            build, 1, 5, 12, HasElite: true, HasBoss: false, AbyssRoute: false, Formation: 0), 606);

        Assert.Contains(result.Events, item => item.Kind == P4SpatialEventKind.BannerActivated);
        Assert.Contains(result.Events, item => item.Kind == P4SpatialEventKind.SeismicCharge);
        Assert.Contains(result.Events, item => item.Kind == P4SpatialEventKind.BloodTideSpin);
    }

    [Fact]
    public void NewSkillStonesComeFromDropsInsteadOfStarterGrant()
    {
        P1GameSession session = CreateSession();
        Assert.DoesNotContain(session.Management.SkillStones, stone => !stone.Definition.StarterGranted);

        SkillStoneInstance dropped = session.Management.AddDroppedSkillStone(77);

        Assert.False(dropped.Definition.StarterGranted);
        Assert.Contains(dropped, session.Management.UninstalledSkillStones);
    }

    [Fact]
    public void OnlyEffectiveInstalledSkillStonesGainExperience()
    {
        P1GameSession session = CreateSession();
        SkillStoneInstance installed = session.Management.SkillStones.First(stone =>
            session.Management.InstalledSkillStoneIds.Contains(stone.InstanceId));
        SkillStoneInstance uninstalled = session.Management.UninstalledSkillStones.First();

        session.Management.AddSkillExperience(1_100);

        Assert.Equal(2, session.Management.SkillStones.Single(stone => stone.InstanceId == installed.InstanceId).Level);
        Assert.Equal(1, session.Management.SkillStones.Single(stone => stone.InstanceId == uninstalled.InstanceId).Level);
        Assert.Equal(0, session.Management.SkillStones.Single(stone => stone.InstanceId == uninstalled.InstanceId).Experience);
    }

    [Fact]
    public void AiEnemyCountConditionGatesRealSkillExecution()
    {
        P1TeamBuild baseBuild = AiBuild(new SkillAiRule(MinimumEnemyCount: 20));
        P4NodeCombatResult blocked = new P4SpatialCombatRunner().Run(new P4NodeCombatRequest(
            baseBuild, 1, 5, 8, false, false, false, 0, MaximumTicks: 120), 81);
        P4NodeCombatResult enabled = new P4SpatialCombatRunner().Run(new P4NodeCombatRequest(
            AiBuild(new SkillAiRule(MinimumEnemyCount: 1)), 1, 5, 8, false, false, false, 0, MaximumTicks: 120), 81);

        Assert.DoesNotContain(blocked.Events, item => item.Kind == P4SpatialEventKind.HeavyStrike);
        Assert.Contains(enabled.Events, item => item.Kind == P4SpatialEventKind.HeavyStrike);
    }

    [Fact]
    public void SkillSchemesPreserveSocketLayoutAndAiRules()
    {
        P1GameSession session = CreateSession();
        SkillLinkConfiguration original = session.Management.SkillLinks.First(link => !string.IsNullOrEmpty(link.ChainId));
        Assert.True(session.ConfigureActiveSkill(original.ActiveStoneInstanceId, 77,
            new SkillAiRule(MinimumEnemyCount: 4, MaximumDistanceRaw: 5_000), true));
        session.Management.SaveSkillScheme(P6SkillSchemeKind.Custom);
        Assert.True(session.ConfigureActiveSkill(original.ActiveStoneInstanceId, 3, new SkillAiRule(), true));

        P6SchemeSwitchResult result = session.Management.SwitchSkillScheme(P6SkillSchemeKind.Custom, session.GetSkillChains());

        Assert.True(result.Succeeded);
        SkillLinkConfiguration restored = session.Management.SkillLinks.Single(link =>
            link.ActiveStoneInstanceId == original.ActiveStoneInstanceId);
        Assert.Equal(77, restored.Priority);
        Assert.Equal(4, restored.AiRule!.MinimumEnemyCount);
    }

    private static P1TeamBuild AiBuild(SkillAiRule rule) => new(
        new CharacterSheet(60, new CharacterAttributes(250, 160, 140, 120),
            new DefensiveEquipment(700, 160, 220), FlatMaximumLife: 1_600),
        new WeaponProfile("test.p6.ai", 160, 220, 1_700, 1_000),
        new SkillConfiguration(P1SkillIds.HeavyStrike, SkillSupport.None, 1, rule),
        FlatAccuracy: 1_200,
        IncreasedDamageBasisPoints: 4_000,
        ActiveSkills: [new SkillConfiguration(P1SkillIds.HeavyStrike, SkillSupport.None, 1, rule)]);

    private static P1GameSession CreateSession() => P1GameSession.CreateNew(new PlayerIdentity(
        "孔铸者", CharacterGender.Androgynous, CharacterSkinTone.Umber,
        CharacterHairStyle.Braided, P1Ascendancy.IronOath), 0x6060);
}
