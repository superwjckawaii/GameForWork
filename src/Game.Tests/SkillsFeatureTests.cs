using GameForWork.Core.Campaign;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Skills;
using GameForWork.Core.Management;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Spatial;
using GameForWork.Core.Scenes;
using GameForWork.Core.Expeditions;

namespace GameForWork.Tests;

public sealed class SkillsFeatureTests
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
                Math.Min(6, SocketRules.ItemLevelMaximum(level)));
        }

        ItemInstance ring = ItemGenerator.Generate("core.base.iron_ring", 10, ItemRarity.Rare, 99);
        Assert.Equal(0, ring.LinkedSocketCount);
    }

    [Fact]
    public void LegacySocketSnapshotsAreRejectedAtSessionBoundary()
    {
        GameSession session = CreateSession();
        GameSessionSnapshot legacy = session.Capture() with
        {
            FormatVersion = 8,
            HeroEquipment = session.Capture().HeroEquipment
                .Select(entry => entry with { Item = entry.Item with { LinkedSocketCount = 0 } })
                .ToArray(),
        };

        Assert.Throws<InvalidDataException>(() => GameSession.Restore(legacy));
    }

    [Fact]
    public void SkillStoneHasOneLocationAndSupportCanWaitForActive()
    {
        GameSession session = CreateSession();
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
        SkillLinkConfiguration? otherLink = management.SkillLinks.FirstOrDefault(link => link.ChainId == other.StableId);
        if (otherLink?.SocketStoneInstanceIds is not null)
        {
            for (int index = 0; index < other.TotalSockets; index++)
                session.UnsocketSkillStone(other.StableId, index);
        }
        Assert.True(session.TryPlaceSkillStone(other.StableId, 1, support.InstanceId));
        Assert.Equal(1, management.SkillLinks.Sum(link =>
            (link.SocketStoneInstanceIds ?? []).Count(id => id == support.InstanceId)));
    }

    [Fact]
    public void SkillTagsRejectIncompatibleSupportsAndApplyProjectileModifiers()
    {
        SkillStoneDefinition heavy = SkillStoneCatalog.Get("core.skill_stone.heavy_strike");
        SkillStoneDefinition brutality = SkillStoneCatalog.Get("core.skill_stone.brutality");
        SkillStoneDefinition projectiles = SkillStoneCatalog.Get("core.skill_stone.multiple_projectiles");
        Assert.True(SkillCompatibility.Check(heavy, brutality).Compatible);
        Assert.False(SkillCompatibility.Check(heavy, projectiles).Compatible);

        ResolvedSkill blade = CombatSkillRules.Resolve(new SkillConfiguration(
            SkillIds.SpiritBlade, SkillSupport.MultipleProjectiles | SkillSupport.FasterProjectiles), 500);
        Assert.Equal(3, blade.ProjectileCount);
        Assert.Equal(19_200, blade.ProjectileSpeedRawPerSecond);
        Assert.True(blade.RangeRaw > SkillDefinitions.SpiritBlade.RangeRaw);
    }

    [Fact]
    public void NewActiveSkillsRunInAuthoritativeSpatialCombat()
    {
        var build = new TeamBuild(
            new CharacterSheet(60, new CharacterAttributes(250, 160, 140, 120),
                new DefensiveEquipment(700, 160, 220), FlatMaximumLife: 1_600),
            new WeaponProfile("test.skills", 90, 130, 1_500, 800),
            new SkillConfiguration(SkillIds.HeavyStrike, SkillSupport.None),
            FlatAccuracy: 1_200,
            IncreasedDamageBasisPoints: 2_000,
            ActiveSkills:
            [
                new SkillConfiguration(SkillIds.HeavyStrike, SkillSupport.None),
                new SkillConfiguration(SkillIds.SeismicCharge, SkillSupport.Brutality),
                new SkillConfiguration(SkillIds.BloodTideSpin, SkillSupport.Bleed),
                new SkillConfiguration(SkillIds.IronOathBanner, SkillSupport.None),
            ]);

        NodeCombatResult result = new SpatialCombatRunner().Run(new NodeCombatRequest(
            build, 1, 5, 12, HasElite: true, HasBoss: false, AbyssRoute: false, Formation: 0), 606);

        Assert.Contains(result.Events, item => item.Kind == SpatialEventKind.BannerActivated);
        Assert.Contains(result.Events, item => item.Kind == SpatialEventKind.SeismicCharge);
        Assert.Contains(result.Events, item => item.Kind == SpatialEventKind.BloodTideSpin);
    }

    [Fact]
    public void NewSkillStonesComeFromDropsInsteadOfStarterGrant()
    {
        GameSession session = CreateSession();
        Assert.DoesNotContain(session.Management.SkillStones, stone => !stone.Definition.StarterGranted);

        SkillStoneInstance dropped = session.Management.AddDroppedSkillStone(77);

        Assert.False(dropped.Definition.StarterGranted);
        Assert.Contains(dropped, session.Management.UninstalledSkillStones);
    }

    [Fact]
    public void OnlyEffectiveInstalledSkillStonesGainExperience()
    {
        GameSession session = CreateSession();
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
        TeamBuild baseBuild = AiBuild(new SkillAiRule(MinimumEnemyCount: 20));
        NodeCombatResult blocked = new SpatialCombatRunner().Run(new NodeCombatRequest(
            baseBuild, 1, 5, 8, false, false, false, 0, MaximumTicks: 120), 81);
        NodeCombatResult enabled = new SpatialCombatRunner().Run(new NodeCombatRequest(
            AiBuild(new SkillAiRule(MinimumEnemyCount: 1)), 1, 5, 8, false, false, false, 0, MaximumTicks: 120), 81);

        Assert.DoesNotContain(blocked.Events, item => item.Kind == SpatialEventKind.HeavyStrike);
        Assert.Contains(enabled.Events, item => item.Kind == SpatialEventKind.HeavyStrike);
    }

    [Fact]
    public void PerSkillTargetPolicyGatesNormalEliteAndBossTargets()
    {
        TeamBuild bossOnly = AiBuild(new SkillAiRule(TargetPolicy: SkillTargetPolicy.BossOnly));
        NodeCombatResult normal = new SpatialCombatRunner().Run(new NodeCombatRequest(
            bossOnly, 1, 5, 8, false, false, false, 0, MaximumTicks: 160), 82);
        NodeCombatResult boss = new SpatialCombatRunner().Run(new NodeCombatRequest(
            bossOnly, 1, 5, 8, false, true, false, 0, MaximumTicks: 160), 82);
        NodeCombatResult elite = new SpatialCombatRunner().Run(new NodeCombatRequest(
            AiBuild(new SkillAiRule(TargetPolicy: SkillTargetPolicy.EliteAndBoss)),
            1, 5, 8, true, false, false, 0, MaximumTicks: 160), 82);

        Assert.DoesNotContain(normal.Events, item => item.Kind == SpatialEventKind.HeavyStrike);
        Assert.Contains(boss.Events, item => item.Kind == SpatialEventKind.HeavyStrike);
        Assert.Contains(elite.Events, item => item.Kind == SpatialEventKind.HeavyStrike);
    }

    [Fact]
    public void SimpleTargetPolicySurvivesSessionCaptureAndRestore()
    {
        GameSession session = CreateSession();
        SkillLinkConfiguration link = session.Management.SkillLinks.First(item =>
            !string.IsNullOrEmpty(item.ActiveStoneInstanceId));

        Assert.True(session.ConfigureSkillTarget(link.ActiveStoneInstanceId, SkillTargetPolicy.EliteAndBoss));
        GameSession restored = GameSession.Restore(session.Capture());

        Assert.Equal(SkillTargetPolicy.EliteAndBoss, restored.Management.SkillLinks.Single(item =>
            item.ActiveStoneInstanceId == link.ActiveStoneInstanceId).AiRule!.TargetPolicy);
    }

    [Fact]
    public void SkillSchemesPreserveSocketLayoutAndAiRules()
    {
        GameSession session = CreateSession();
        SkillLinkConfiguration original = session.Management.SkillLinks.First(link => !string.IsNullOrEmpty(link.ChainId));
        Assert.True(session.ConfigureActiveSkill(original.ActiveStoneInstanceId, 77,
            new SkillAiRule(MinimumEnemyCount: 4, MaximumDistanceRaw: 5_000), true));
        session.Management.SaveSkillScheme(SkillSchemeKind.Custom);
        Assert.True(session.ConfigureActiveSkill(original.ActiveStoneInstanceId, 3, new SkillAiRule(), true));

        SchemeSwitchResult result = session.Management.SwitchSkillScheme(SkillSchemeKind.Custom, session.GetSkillChains());

        Assert.True(result.Succeeded);
        SkillLinkConfiguration restored = session.Management.SkillLinks.Single(link =>
            link.ActiveStoneInstanceId == original.ActiveStoneInstanceId);
        Assert.Equal(77, restored.Priority);
        Assert.Equal(4, restored.AiRule!.MinimumEnemyCount);
    }

    [Fact]
    public void ChainSteelCraftingUsesGuaranteedCostsAndProtectsSixLinks()
    {
        GameSession session = CreateSession();
        session.World.Economy.AddMetal(MetalCurrencyKind.ChainSteel, 20);
        ItemInstance item = ItemGenerator.Generate(
            "core.base.rusted_greatsword", 10, ItemRarity.Rare, 601) with { LinkedSocketCount = 3 };

        CraftPreview upgrade = SocketCraftingRules.Craft(
            session.World.Economy, item, SocketCraftOperation.UpgradeLinks);
        CraftPreview reroll = SocketCraftingRules.Preview(
            upgrade.Result! with { LinkedSocketCount = 4 }, SocketCraftOperation.RerollLinks, seed: 602);
        CraftPreview protectedSix = SocketCraftingRules.Preview(
            item with { LinkedSocketCount = 6 }, SocketCraftOperation.RerollLinks, seed: 603);

        Assert.True(upgrade.Succeeded);
        Assert.Equal(4, upgrade.ResultLinks);
        Assert.Equal(2, upgrade.Cost);
        Assert.True(reroll.Succeeded);
        Assert.InRange(reroll.ResultLinks, 2, 6);
        Assert.False(protectedSix.Succeeded);
        Assert.Equal("six_link_locked", protectedSix.FailureReason);
    }

    [Fact]
    public void AdvancedMetalsPreserveCraftedFracturedAndSocketState()
    {
        ItemInstance generated = ItemGenerator.Generate(
            "core.base.rusted_greatsword", 10, ItemRarity.Rare, 610) with { LinkedSocketCount = 5, IsLocked = false };
        AffixRoll crafted = generated.Affixes[0] with { Crafted = true };
        AffixRoll natural = generated.Affixes[1];
        ItemInstance prepared = generated with { Affixes = [crafted, natural] };
        CraftPreview fracture = SocketCraftingRules.Preview(
            prepared, SocketCraftOperation.FractureAffix, natural.Definition.StableFamilyId);
        CraftPreview chaos = SocketCraftingRules.Preview(
            fracture.Result!, SocketCraftOperation.ChaosReroll, seed: 611);
        CraftPreview divine = SocketCraftingRules.Preview(
            chaos.Result!, SocketCraftOperation.DivineReroll, seed: 612);

        Assert.True(fracture.Succeeded);
        Assert.True(chaos.Succeeded);
        Assert.True(divine.Succeeded);
        Assert.Equal(5, divine.Result!.LinkedSocketCount);
        Assert.Equal(natural.Definition.StableFamilyId, divine.Result.FracturedAffixFamilyId);
        Assert.Contains(divine.Result.Affixes, affix => affix.Crafted && affix.Value == crafted.Value);
        Assert.Contains(divine.Result.Affixes, affix =>
            affix.Definition.StableFamilyId == natural.Definition.StableFamilyId && affix.Value == natural.Value);
    }

    [Fact]
    public void LinkFilterPrecedesRarityAndBuildSummaryStatesItsAssumptions()
    {
        var filter = new LootFilter();
        ItemInstance sixLinkMagic = ItemGenerator.Generate(
            "core.base.rusted_greatsword", 10, ItemRarity.Magic, 620) with { LinkedSocketCount = 6 };
        GameSession session = CreateSession();
        BuildSummary summary = session.GetBuildSummary();

        Assert.Equal(LootDisposition.Sell, filter.Evaluate(sixLinkMagic));
        Assert.NotEqual("无", summary.MainSkill);
        Assert.InRange(summary.MainSkillLinks, 1, 6);
        Assert.Equal(session.World.Hero.Build.Sheet.MaximumLife().Value, summary.Defense.MaximumLife);
        Assert.Equal(session.World.Hero.Build.Sheet.MaximumMana().Value, summary.Defense.MaximumMana);
        Assert.True(summary.Offense.DamagePerSecond > 0);
        Assert.True(summary.Offense.BaseMaximumDamage >= summary.Offense.BaseMinimumDamage);
        Assert.InRange(summary.Offense.HitChanceBasisPoints, 0, 10_000);
        Assert.Contains("估算假设", summary.Assumptions);
    }

    [Fact]
    public void CombatReportUsesAuthoritativeTimelineAndPersistsLatestFifty()
    {
        GameSession session = CreateSession();
        CampaignNodeDefinition node = CampaignCatalog.Nodes.First(item => item.Kind == CampaignNodeKind.NormalCombat);
        SceneTimeline timeline = SceneTimelineBuilder.BuildCampaign(session.World.Hero.Build, node, 630);
        CombatReport report = CombatReportBuilder.Build(timeline, "测试战斗", offline: true);
        var director = new ExpeditionDirector();
        for (int index = 0; index < 55; index++)
        {
            director.AddCombatReport(report with { StableId = $"report-{index}" });
        }
        ExpeditionDirector restored = ExpeditionDirector.Restore(director.Capture());

        Assert.True(report.DamageDealt > 0);
        Assert.NotEmpty(report.Skills);
        Assert.True(report.Offline);
        Assert.NotEmpty(report.LastFiveSeconds);
        Assert.Equal(50, restored.Reports.Count);
        Assert.Equal("report-5", restored.Reports[0].StableId);
        Assert.Equal("report-54", restored.Reports[^1].StableId);
    }

    private static TeamBuild AiBuild(SkillAiRule rule) => new(
        new CharacterSheet(60, new CharacterAttributes(250, 160, 140, 120),
            new DefensiveEquipment(700, 160, 220), FlatMaximumLife: 1_600),
        new WeaponProfile("test.skills.ai", 160, 220, 1_700, 1_000),
        new SkillConfiguration(SkillIds.HeavyStrike, SkillSupport.None, 1, rule),
        FlatAccuracy: 1_200,
        IncreasedDamageBasisPoints: 4_000,
        ActiveSkills: [new SkillConfiguration(SkillIds.HeavyStrike, SkillSupport.None, 1, rule)]);

    private static GameSession CreateSession() => GameSession.CreateNew(new PlayerIdentity(
        "孔铸者", CharacterGender.Androgynous, CharacterSkinTone.Umber,
        CharacterHairStyle.Braided, BaseClass.Fighter), 0x6060);
}
