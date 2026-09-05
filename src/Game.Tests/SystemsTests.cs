using GameForWork.Core.Campaign;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Campaign.Progression;
using GameForWork.Core.Ascendancies;
using GameForWork.Core.Endgame;
using GameForWork.Core.SkillCatalog;
using GameForWork.Core.Spatial;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Equipment;
using GameForWork.Core.Skills;
using GameForWork.Core.Builds;
using System.Text.Json;

namespace GameForWork.Tests;

public sealed class SystemsTests
{
    [Fact]
    public void AttackFrequencyUsesSixtyPerSecondCapAndCarriesSubTickAttacks()
    {
        Assert.Equal(60_000, CombatRules.AttackFrequencyMilliPerSecond(2_000, 500_000));
        Assert.Equal(17, CombatRules.AttackIntervalMilliseconds(2_000, 500_000));

        WeaponProfile weapon = EquipmentCatalog.GetBase("core.base.rusted_greatsword").ToWeaponProfile() with
        { AttacksPerSecondMilli = 1_000 };
        SkillUseProfile profile = SkillRules.BuildHeavyStrike(
            new SkillConfiguration(SkillIds.HeavyStrike, SkillSupport.None), weapon, 1_000, 1_000_000);
        Assert.True(profile.UncappedAttackFrequencyMilliPerSecond > 60_000);
        Assert.Equal(60_000, profile.AttackFrequencyMilliPerSecond);
        var build = new TeamBuild(
            new CharacterSheet(100, new CharacterAttributes(100, 100, 100, 100),
                new DefensiveEquipment(0, 0, 0)),
            weapon,
            new SkillConfiguration(SkillIds.HeavyStrike, SkillSupport.None),
            IncreasedAttackSpeedBasisPoints: 1_000_000);
        Assert.Equal(60_000, CombatSkillRules.ActionFrequencyMilliPerSecond(build, 1, 1,
            SkillTag.Attack));

        int carry = 0;
        int attacks = 0;
        for (int tick = 0; tick < 20; tick++)
            attacks += CombatRules.AttacksForScheduledSimulationTick(21_000, ref carry);
        Assert.Equal(21, attacks);
        Assert.Equal(3, CombatRules.AttacksForScheduledSimulationTick(60_000, ref carry));
    }

    [Fact]
    public void JewelStashOrdersSameNameByEffectiveRadiusDescending()
    {
        JewelInstance small = JewelCatalog.CreateLegendary("bloodbound_domain", 100, "small", 1) with
        { RolledRadius = 180 };
        JewelInstance large = JewelCatalog.CreateLegendary("bloodbound_domain", 100, "large", 2) with
        { RolledRadius = 720 };
        JewelInstance medium = JewelCatalog.CreateLegendary("bloodbound_domain", 100, "medium", 3) with
        { RolledRadius = 440 };

        Assert.Equal(["large", "medium", "small"],
            JewelCatalog.OrderForStash([small, large, medium]).Select(jewel => jewel.InstanceId));
    }

    [Fact]
    public void MoreModifiersCompoundAcrossIndependentDamageBuckets()
    {
        Assert.Equal(5_600, CombatRules.CombineMoreBasisPoints(2_000, 3_000));
        SkillConfiguration configuration = new(SkillIds.HeavyStrike, SkillSupport.None);
        WeaponProfile weapon = EquipmentCatalog.GetBase("core.base.rusted_greatsword").ToWeaponProfile() with
        { MinimumPhysicalDamage = 100, MaximumPhysicalDamage = 100 };
        var sheet = new CharacterSheet(1, new CharacterAttributes(20, 10, 10, 10),
            new DefensiveEquipment(0, 0, 0));
        ResolvedSkill skill = CombatSkillRules.Resolve(configuration, sheet.MaximumLife().Value) with
        { Role = SkillRole.DamageOverTime };
        var baselineBuild = new TeamBuild(sheet, weapon, configuration);
        TeamBuild moreBuild = baselineBuild with
        {
            MoreAttackDamageBasisPoints = 2_000,
            MoreDamageOverTimeBasisPoints = 3_000,
            MoreElementalDamageBasisPoints = 1_000,
            MoreRareBossDamageBasisPoints = 400,
        };
        SkillTag tags = SkillTag.Attack | SkillTag.Fire | SkillTag.Duration;

        int baseline = CombatSkillRules.ScaleOffensiveDamage(100, skill, configuration,
            baselineBuild, tags, 10_000, 10_000, targetRareOrBoss: true);
        int actual = CombatSkillRules.ScaleOffensiveDamage(100, skill, configuration,
            moreBuild, tags, 10_000, 10_000, targetRareOrBoss: true);
        long expected = baseline;
        foreach (int multiplier in new[] { 12_000, 13_000, 11_000, 10_400 })
            expected = expected * multiplier / 10_000;
        Assert.Equal((int)expected, actual);
    }

    [Fact]
    public void ResourceAndWeaponMasteriesUseTheirDescribedMoreMultipliers()
    {
        PassiveModifiers resources = PassiveModifiers.Empty with
        {
            MasteryMechanics = string.Join('|',
                "builds.mastery.rule.生命.0",
                "builds.mastery.rule.生命.3",
                "builds.mastery.rule.偷取.0"),
        };
        Assert.Equal(18_200, MasteryRuntime.MaximumLifeMultiplier(resources));
        Assert.Equal(0, MasteryRuntime.ShieldMultiplier(resources));
        Assert.Equal(10_000, MasteryRuntime.IncreasedLifeLeechRecoverySpeed(resources));

        PassiveModifiers oneHand = PassiveModifiers.Empty with
        { MasteryMechanics = "builds.mastery.rule.单手.0" };
        WeaponProfile sword = EquipmentCatalog.Bases.First(item => item.Category == ItemCategory.OneHandWeapon)
            .ToWeaponProfile();
        Assert.Equal(16_000, MasteryRuntime.OffensiveMultiplier(oneHand,
            SkillTag.Attack | SkillTag.Physical, sword, 100, 100, hasOffHand: false));
        Assert.Equal(10_000, MasteryRuntime.OffensiveMultiplier(oneHand,
            SkillTag.Attack | SkillTag.Physical, sword, 100, 100, hasOffHand: true));
    }

    [Fact]
    public void MaximumLifeMoreMultiplierIsSeparateFromIncreasedLife()
    {
        var sheet = new CharacterSheet(1, new CharacterAttributes(10, 10, 10, 10),
            new DefensiveEquipment(0, 0, 0), IncreasedMaximumLifeBasisPoints: 1_000,
            MaximumLifeMultiplierBasisPoints: 13_000);

        Assert.Equal(139, sheet.MaximumLife().Value);
    }

    [Fact]
    public void VersionTwentyThreeCitadelVictoriesGrantExactlyOneTargetedCompensation()
    {
        GameSession source = GameSession.CreateNew(new("补偿测试", CharacterGender.Androgynous,
            CharacterSkinTone.Umber, CharacterHairStyle.Cropped, GameForWork.Core.Characters.BaseClass.Fighter), 0x3051);
        var oldEndgame = new EndgameState();
        for (int index = 0; index < 11_463; index++) oldEndgame.RecordCitadelVictory();
        GameSessionSnapshot old = source.Capture() with { FormatVersion = 23, Endgame = oldEndgame.Capture() };

        GameSession migrated = GameSession.Restore(old);
        GameSession restoredAgain = GameSession.Restore(migrated.Capture());
        int Count(GameSession session) => session.World.Storage.Items.Count(item => item.LegendaryCatalogId == "equipment.legendary.52.44a586da1f") +
            session.Management.Recovery.Count(item => item.LegendaryCatalogId == "equipment.legendary.52.44a586da1f");

        Assert.True(migrated.CitadelDropCompensationGranted);
        Assert.Equal(1, Count(migrated));
        Assert.Equal(1, Count(restoredAgain));
        Assert.Contains(migrated.Management.OperationHistory, line => line.Contains("11,463", StringComparison.Ordinal));
    }

    [Fact]
    public void MainSkillCandidatesExcludeReservationsAndSelectionPersists()
    {
        GameSession session = GameSession.CreateNew(new("技能预览", CharacterGender.Androgynous,
            CharacterSkinTone.Umber, CharacterHairStyle.Cropped, GameForWork.Core.Characters.BaseClass.Fighter), 0x3052);
        SkillConfiguration[] candidates = session.GetPreviewSkillCandidates().ToArray();

        Assert.NotEmpty(candidates);
        Assert.All(candidates, candidate =>
        {
            ActiveSkillDefinition definition = ActiveSkillCatalog.ActiveForSkill(candidate.SkillId);
            Assert.True(definition.Combat.Capabilities.HasFlag(SkillCapability.Damage));
            Assert.NotEqual(SkillRole.Reservation, definition.Combat.Role);
        });
        Assert.True(session.SelectPreviewSkill(candidates[0].StoneInstanceId));
        Assert.Equal(candidates[0].StoneInstanceId,
            GameSession.Restore(session.Capture()).GetPreviewSkill()!.StoneInstanceId);
        Assert.Equal(ActiveSkillCatalog.ActiveForSkill(candidates[0].SkillId).Combat.DisplayName,
            session.GetBuildSummary().MainSkill);
    }

    [Fact]
    public void LinkAndMasteryChangesAffectSharedPreviewMathAndAuthoritativeCombat()
    {
        ActiveSkillDefinition active = ActiveSkillCatalog.Active.Single(item => item.Combat.DisplayName == "十方终式");
        SupportSkillDefinition support = ActiveSkillCatalog.Supports.Single(item => item.DisplayName == "孤锋专注");
        var plain = new SkillConfiguration(active.Combat.SkillId, SkillSupport.None, Level: 21);
        var linked = plain with { SupportLinks = [new(support.StoneId, 21, 20)] };
        var noMastery = PassiveModifiers.Empty;
        var mastery = noMastery with { MasteryMechanics = "builds.mastery.rule.双手.2" };
        WeaponProfile weapon = EquipmentCatalog.GetBase("core.base.rusted_greatsword").ToWeaponProfile() with
        { MinimumPhysicalDamage = 100, MaximumPhysicalDamage = 100 };
        TeamBuild Build(SkillConfiguration configuration, PassiveModifiers profile) => new(
            new CharacterSheet(100, new CharacterAttributes(300, 120, 100, 100),
                new DefensiveEquipment(5_000, 500, 0), FlatMaximumLife: 5_000),
            weapon,
            new SkillConfiguration(SkillIds.HeavyStrike, SkillSupport.None),
            FlatAccuracy: 10_000, UseWarCry: false, ActiveSkills: [configuration], PassiveProfile: profile);
        int Preview(SkillConfiguration configuration, PassiveModifiers profile)
        {
            ResolvedSkill resolved = CombatSkillRules.Resolve(configuration, 5_000, profile);
            int raw = CombatSkillRules.BaseDamage(resolved, active.Combat.Tags,
                weapon, 0);
            return CombatSkillRules.ScaleOffensiveDamage(raw, resolved, configuration,
                Build(configuration, profile), active.Combat.Tags, 10_000, 10_000);
        }
        int Actual(SkillConfiguration configuration, PassiveModifiers profile) =>
            new SpatialCombatRunner().Run(new NodeCombatRequest(Build(configuration, profile), 1, 100, 1,
                false, true, false, 0, MaximumTicks: 100), 0x3053).Events
                .First(item => item.Kind == SpatialEventKind.SkillEffect && item.Value > 0 &&
                    item.Detail.Contains(active.Combat.SkillId, StringComparison.Ordinal)).Value;

        Assert.True(Preview(linked, noMastery) > Preview(plain, noMastery));
        Assert.True(Actual(linked, noMastery) > Actual(plain, noMastery));
        Assert.True(Preview(plain, mastery) > Preview(plain, noMastery));
        Assert.True(Actual(plain, mastery) > Actual(plain, noMastery));
    }

    [Fact]
    public void HighDamagePreviewUsesWideIntermediateArithmetic()
    {
        SkillConfiguration configuration = new(SkillIds.HeavyStrike, SkillSupport.None);
        WeaponProfile weapon = EquipmentCatalog.GetBase("core.base.rusted_greatsword").ToWeaponProfile() with
        { MinimumPhysicalDamage = 8_000, MaximumPhysicalDamage = 8_000 };
        PassiveModifiers passive = PassiveModifiers.Empty with
        {
            MoreDamageBasisPoints = 20_000,
            MasteryMechanics = "builds.mastery.rule.双手.0",
        };
        TeamBuild build = new(
            new CharacterSheet(100, new CharacterAttributes(300, 120, 100, 100),
                new DefensiveEquipment(5_000, 500, 0)),
            weapon,
            configuration,
            IncreasedDamageBasisPoints: 20_000,
            PassiveProfile: passive);
        ResolvedSkill resolved = CombatSkillRules.Resolve(configuration,
            build.Sheet.MaximumLife().Value, passive);

        int damage = CombatSkillRules.ScaleOffensiveDamage(
            CombatSkillRules.BaseDamage(resolved, SkillDefinitions.HeavyStrike.Tags, weapon, 0),
            resolved, configuration, build, SkillDefinitions.HeavyStrike.Tags, 100_000, 100_000);

        Assert.Equal(230_400, damage);
    }

    [Fact]
    public void EveryMasteryOptionHasAUniqueExplicitMechanicAndNoTextDerivedRuntimeFallback()
    {
        MasteryChoice[] choices = PassiveTree.Nodes.Where(node => node.Kind == PassiveNodeKind.Mastery)
            .SelectMany(PassiveTreeCatalog.MasteryChoices).GroupBy(choice => choice.MechanicId, StringComparer.Ordinal)
            .Select(group => group.First()).ToArray();

        Assert.Equal(434, PassiveTreeCatalog.ExplicitMasteryRuleCount);
        Assert.Equal(434, choices.Length);
        Assert.All(choices, choice => Assert.StartsWith("builds.mastery.rule.", choice.MechanicId));
    }

    [Fact]
    public void SkillCatalogSealsAllConfirmedBuildsData()
    {
        Assert.Equal(86, ActiveSkillCatalog.Active.Count);
        Assert.Equal(98, ActiveSkillCatalog.Supports.Count);
        ActiveSkillDefinition heavy = ActiveSkillCatalog.Active.Single(item => item.Combat.DisplayName == "重击");
        Assert.Equal(16_000, heavy.DamageAt(1));
        Assert.Equal(42_500, heavy.DamageAt(21));
        Assert.Equal(12, heavy.ManaAt(21));
        Assert.Equal("builds.skill.shield_bash", ActiveSkillCatalog.Active.Single(item => item.Combat.DisplayName == "盾锋冲击").Combat.SkillId);
        string[] highReservationAuras = ["迅行战律", "猎王战旗", "百魂军势", "不灭圣域", "原初映照"];
        Assert.All(highReservationAuras, name => Assert.Contains(ActiveSkillCatalog.Active,
            item => item.Combat.DisplayName == name && item.Combat.Role == GameForWork.Core.SkillCatalog.SkillRole.Reservation));

        SupportSkillDefinition temperance = ActiveSkillCatalog.Supports.Single(item => item.DisplayName == "持律精算");
        Assert.Equal(6, temperance.ValueAt(21));
        Assert.Contains("有效等级 +1", temperance.Effect, StringComparison.Ordinal);
        Assert.Equal(50, ActiveSkillCatalog.Supports.Single(item => item.DisplayName == "过载供能").ValueAt(21));
    }

    [Fact]
    public void SupportCompatibilitySealsExclusiveModes()
    {
        SupportSkillDefinition chain = ActiveSkillCatalog.Supports.Single(item => item.DisplayName == "追加连锁");
        SupportSkillDefinition seeking = ActiveSkillCatalog.Supports.Single(item => item.DisplayName == "追踪连锁");
        SupportSkillDefinition pierce = ActiveSkillCatalog.Supports.Single(item => item.DisplayName == "贯穿");
        SupportSkillDefinition precision = ActiveSkillCatalog.Supports.Single(item => item.DisplayName == "精准穿透");
        Assert.False(ActiveSkillCatalog.AreCompatible(chain, seeking));
        Assert.False(ActiveSkillCatalog.AreCompatible(pierce, precision));
        Assert.True(ActiveSkillCatalog.AreCompatible(chain, pierce));
        Assert.All(ActiveSkillCatalog.Supports, support =>
            Assert.Contains(ActiveSkillCatalog.Active, active => ActiveSkillCatalog.SupportsActive(support, active)));
    }

    [Fact]
    public void BuildsLinkedSupportsUseTheirOwnLevelsCostsAndRuntimeMechanics()
    {
        ActiveSkillDefinition heavy = ActiveSkillCatalog.Active.Single(item => item.Combat.DisplayName == "重击");
        SupportSkillDefinition lone = ActiveSkillCatalog.Supports.Single(item => item.DisplayName == "孤锋专注");
        SupportSkillDefinition overload = ActiveSkillCatalog.Supports.Single(item => item.DisplayName == "过载供能");
        SupportRuntimeProfile profile = ActiveSkillCatalog.ResolveSupports(heavy,
        [
            new(lone.StoneId, 21, 20),
            new(overload.StoneId, 1, 0),
        ]);

        Assert.Equal(21_000, profile.ResourceMultiplierBasisPoints);
        Assert.Equal(21_332, profile.DamageMultiplierBasisPoints);
        Assert.True(profile.SingleTargetOnly);
        Assert.True(profile.OverloadRepeatsEveryThirdUse);
    }

    [Fact]
    public void ThirtySixBuildAuditPassesFormalGates()
    {
        IReadOnlyList<BuildAuditResult> results = BuildAudit.Run();
        Assert.Equal(36, results.Count);
        Assert.Empty(BuildAudit.Validate(results));
        Assert.Equal(18, results.Select(item => item.Build.Ascendancy).Distinct().Count());
        Assert.All(results, item => Assert.True(item.Passed, item.Build.DisplayName));
    }

    [Fact]
    public void ConfirmedCombatMathUsesHistoryAndDirectedConversion()
    {
        DamagePacket packet = CombatRules.ConvertAndScale(100, DamageType.Physical,
            [new(DamageType.Physical, DamageType.Fire, 10_000, "test")], [],
            new(new Dictionary<DamageType, int>
            { [DamageType.Physical] = 20_000, [DamageType.Fire] = 5_000 }));
        Assert.Equal(450, packet.Fire);
        Assert.Equal(0, packet.Physical);
        Assert.Equal(10_000, CombatRules.HitChance(10_000_000, 1));
        Assert.Equal(-50_000, CombatRules.EffectiveResistance(-90_000, 7_500));
        Assert.Equal(600, CombatRules.NaturalSpiritBarrier(100, 100));
        Assert.True(CombatRules.PhysicalDotArmorReduction(10_000, 1_000) <
                    CombatRules.ArmorReduction(10_000, 1_000));
    }

    [Fact]
    public void MainTreeIsTheConfirmed1475NodeTopology()
    {
        Assert.Equal(1_475, PassiveTree.Nodes.Count);
        Assert.Equal(24, PassiveTree.Nodes.Count(node => node.Kind == PassiveNodeKind.JewelSocket));
        Assert.Equal(168, PassiveTree.Nodes.Count(node => node.Kind == PassiveNodeKind.Mastery));
        Assert.Equal(149, PassiveTreeAllocation.MaximumAllocatedPoints);
        Assert.All(Enum.GetValues<PassiveStartKind>().Where(value => value != PassiveStartKind.None), start =>
            Assert.Equal(3, PassiveTree.Neighbors(PassiveTree.StartNode(start)).Count));
    }

    [Fact]
    public void ConfirmedMasteryDescriptionsAndNodeNamesDriveTheBuildsTree()
    {
        PassiveNodeDefinition notable = PassiveTree.Get("builds.cluster.v5_o05.notable01");
        Assert.Equal("毒潮积累", notable.DisplayName);
        Assert.Equal("中毒", notable.ClusterTheme);
        Assert.StartsWith("中毒持续时间提高 60%", notable.SpecialRule);
        PassiveNodeDefinition mastery = PassiveTree.Get("builds.cluster.v5_o05.mastery");
        IReadOnlyList<string> options = PassiveTree.MasteryOptionDescriptions(mastery);
        Assert.Equal(7, options.Count);
        Assert.StartsWith("浓缩毒液：中毒造成 60% 更多伤害", options[0]);
        Assert.StartsWith("致命潜伏：", options[6]);
    }

    [Fact]
    public void PassiveLayoutKeepsNodesAndUnrelatedConnectionsApart()
    {
        PassiveNodeDefinition[] nodes = PassiveTree.Nodes.ToArray();
        var violations = new List<string>();
        for (int left = 0; left < nodes.Length; left++)
        for (int right = left + 1; right < nodes.Length; right++)
        {
            double distance = Distance(nodes[left], nodes[right]);
            if (distance < Radius(nodes[left]) + Radius(nodes[right]))
                violations.Add($"nodes: {nodes[left].StableId} / {nodes[right].StableId} ({distance:0.0})");
        }

        var edges = new HashSet<string>(StringComparer.Ordinal);
        foreach (PassiveNodeDefinition from in nodes)
        foreach (string targetId in PassiveTree.Neighbors(from.StableId))
        {
            string edge = string.CompareOrdinal(from.StableId, targetId) < 0
                ? from.StableId + '|' + targetId
                : targetId + '|' + from.StableId;
            if (!edges.Add(edge)) continue;
            PassiveNodeDefinition to = PassiveTree.Get(targetId);
            foreach (PassiveNodeDefinition node in nodes)
            {
                if (node.StableId == from.StableId || node.StableId == to.StableId) continue;
                double clearance = DistanceToSegment(node.X, node.Y, from.X, from.Y, to.X, to.Y);
                if (clearance < Radius(node) + 1)
                    violations.Add($"edge: {edge} / {node.StableId} ({clearance:0.0})");
            }
        }
        Assert.True(violations.Count == 0,
            $"Passive layout has {violations.Count} collision(s):\n{string.Join('\n', violations.Take(100))}");
    }

    private static double Distance(PassiveNodeDefinition left, PassiveNodeDefinition right)
    {
        double dx = left.X - right.X;
        double dy = left.Y - right.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static double Radius(PassiveNodeDefinition node) => node.Kind switch
    {
        PassiveNodeKind.Start => 16, PassiveNodeKind.Small => 7, PassiveNodeKind.Notable => 11,
        PassiveNodeKind.Mastery => 13, PassiveNodeKind.Rule => 15, _ => 12,
    };

    private static double DistanceToSegment(double px, double py, double ax, double ay, double bx, double by)
    {
        double dx = bx - ax;
        double dy = by - ay;
        if (dx == 0 && dy == 0) return Math.Sqrt((px - ax) * (px - ax) + (py - ay) * (py - ay));
        double t = Math.Clamp(((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy), 0, 1);
        double nearestX = ax + t * dx;
        double nearestY = ay + t * dy;
        return Math.Sqrt((px - nearestX) * (px - nearestX) + (py - nearestY) * (py - nearestY));
    }

    [Fact]
    public void AllEighteenAscendanciesUseConfirmedBuildsNodes()
    {
        Assert.Equal(18, AscendancyDefinitions.All.Count);
        Assert.Equal(216, WarriorAscendancyCatalog.Nodes.Count);
        Assert.All(AscendancyDefinitions.All, path =>
        {
            Assert.Equal(6, path.Branches.Count);
            Assert.Equal(12, WarriorAscendancyCatalog.For(path.Ascendancy).Count);
        });
        Assert.Equal("血肉薪火", WarriorAscendancyCatalog.For(Ascendancy.BloodFighter)[0].DisplayName);
        Assert.Contains("50% 更多伤害", WarriorAscendancyCatalog.For(Ascendancy.Warbreaker)
            .Single(node => node.DisplayName == "摧城崩线").Effect);
    }

    [Fact]
    public void JewelInstancesRollPersistSocketAndCorrupt()
    {
        JewelInstance jewel = JewelCatalog.RollPrismatic(90, 0x123456789UL, "jewel-1");
        Assert.Equal(4, jewel.Affixes.Count);
        Assert.InRange(jewel.Resonance, 0, 40);
        var state = new JewelState();
        Assert.True(state.TryAdd(jewel));
        string socket = PassiveTree.Nodes.First(node => node.Kind == PassiveNodeKind.JewelSocket).StableId;
        Assert.True(state.TrySocket(socket, jewel.InstanceId, 100, out _));
        JewelState restored = JewelState.Restore(state.Capture());
        Assert.Equal(jewel.InstanceId, restored.Socketed[socket]);
        Assert.Equal(24, JewelCatalog.Legendary.Count);
        Assert.Equal(JewelCorruptionResult.PowerfulImplicit, JewelCatalog.Corrupt(jewel, 1).Result);
    }

    [Fact]
    public void CitadelLegendaryJewelsRollRadiusAndApplyTheirPassiveTreeRules()
    {
        ulong dropSeed = Enumerable.Range(1, 100_000).Select(value => (ulong)value)
            .First(seed => JewelCatalog.RollCitadelLegendary(100, seed, "probe") is not null);
        JewelInstance drop = JewelCatalog.RollCitadelLegendary(100, dropSeed, "citadel-drop")!;
        Assert.Contains(drop.Legendary!.StableId,
            JewelCatalog.CitadelLegendaryIds.Select(id => $"builds.jewel.{id}"));
        Assert.InRange(drop.EffectiveRadius, drop.Legendary.MinimumRadius, drop.Legendary.MaximumRadius);
        Assert.Equal(780, JewelCatalog.Legendary.Single(value => value.StableId == "builds.jewel.bloodbound_domain").MaximumRadius);
        Assert.Equal(600, JewelCatalog.Legendary.Single(value => value.StableId == "builds.jewel.pathless_chart").MaximumRadius);
        Assert.Equal(720, JewelCatalog.Legendary.Single(value => value.StableId == "builds.jewel.bastion_abacus").MaximumRadius);
        Assert.Equal(720, JewelCatalog.Legendary.Single(value => value.StableId == "builds.jewel.rampart_echo").MaximumRadius);
        (bool rerolled, _, JewelInstance? divine, _) = JewelCatalog.Craft(drop,
            JewelCraftOperation.RerollLegendaryRadius, 0x77);
        Assert.True(rerolled);
        Assert.InRange(divine!.EffectiveRadius, drop.Legendary.MinimumRadius, drop.Legendary.MaximumRadius);

        var allocation = new PassiveTreeAllocation(memoryAshes: 20);
        string socket = PassiveTree.Nodes.Where(node => node.Kind == PassiveNodeKind.JewelSocket)
            .Select(node => node.StableId)
            .First(id => PassiveTree.FindShortestPath(id, allocation.Allocated, allocation.StartKind).Count < 140);
        Assert.True(allocation.TryAllocatePath(socket, 149));

        var state = new JewelState();
        JewelInstance pathless = JewelCatalog.CreateLegendary("pathless_chart", 100, "pathless", 7) with
        {
            RolledRadius = 140,
        };
        Assert.True(state.TryAdd(pathless));
        Assert.True(state.TrySocket(socket, pathless.InstanceId, 100, out _));
        PassiveNodeDefinition direct = PassiveTree.Nodes.First(node =>
            node.Kind is not (PassiveNodeKind.Start or PassiveNodeKind.JewelSocket) &&
            node.Start == PassiveStartKind.None && !allocation.Allocated.Contains(node.StableId) &&
            !PassiveTree.Neighbors(node.StableId).Any(allocation.Allocated.Contains) &&
            Distance(socket, node.StableId) <= pathless.EffectiveRadius);
        Assert.True(allocation.TryAllocate(direct.StableId, 149, state));
        PassiveNodeDefinition outside = PassiveTree.Nodes.First(node =>
            node.Kind is not (PassiveNodeKind.Start or PassiveNodeKind.JewelSocket) &&
            node.Start == PassiveStartKind.None && !allocation.Allocated.Contains(node.StableId) &&
            Distance(socket, node.StableId) > pathless.EffectiveRadius &&
            !PassiveTree.Neighbors(node.StableId).Any(allocation.Allocated.Contains) &&
            !PassiveTree.Neighbors(PassiveTreeCatalog.StartNode(allocation.StartKind)).Contains(node.StableId));
        Assert.False(allocation.TryAllocate(outside.StableId, 149, state));
        Assert.False(allocation.IsValidAllocation(state, socket));
        PassiveTreeAllocation restored = PassiveTreeAllocation.Restore(allocation.Allocated, 20,
            buildsJewels: state);
        Assert.Contains(direct.StableId, restored.Allocated);

        Assert.True(state.TryUnsocket(socket));
        JewelInstance physique = JewelCatalog.CreateLegendary("bloodbound_domain", 100, "physique", 9);
        Assert.True(state.TryAdd(physique));
        Assert.True(state.TrySocket(socket, physique.InstanceId, 100, out _));
        JewelModifiers modifiers = JewelCatalog.CalculateModifiers(state, allocation);
        PassiveNodeDefinition[] affected = allocation.Allocated.Where(id => id != socket &&
                Distance(socket, id) <= physique.EffectiveRadius).Select(PassiveTree.Get).ToArray();
        Assert.Equal(8 + affected.Count(node => node.Kind == PassiveNodeKind.Small) * 20, modifiers.Physique);
        Assert.Equal(affected.Count(node => node.Kind is PassiveNodeKind.Notable or PassiveNodeKind.Mastery) * 500,
            modifiers.IncreasedPhysiqueBasisPoints);

        Assert.True(state.TryUnsocket(socket));
        Assert.True(state.TryRemove(physique.InstanceId));
        JewelInstance abacus = JewelCatalog.CreateLegendary("bastion_abacus", 100, "abacus", 11);
        Assert.True(state.TryAdd(abacus));
        Assert.True(state.TrySocket(socket, abacus.InstanceId, 100, out _));
        modifiers = JewelCatalog.CalculateModifiers(state, allocation);
        Assert.Equal(affected.Count(node => node.Kind == PassiveNodeKind.Small) * 400,
            modifiers.IncreasedAttackDamageBasisPoints);
        Assert.Equal(affected.Count(node => node.Kind is PassiveNodeKind.Notable or PassiveNodeKind.Mastery) * 300,
            modifiers.IncreasedAttackSpeedBasisPoints);

        Assert.True(state.TryUnsocket(socket));
        Assert.True(state.TryRemove(abacus.InstanceId));
        JewelInstance echo = JewelCatalog.CreateLegendary("rampart_echo", 100, "echo", 13);
        Assert.True(state.TryAdd(echo));
        Assert.True(state.TrySocket(socket, echo.InstanceId, 100, out _));
        modifiers = JewelCatalog.CalculateModifiers(state, allocation);
        Assert.Equal(affected.Count(node => node.Kind == PassiveNodeKind.Small) * 400,
            modifiers.IncreasedArmorBasisPoints);
        Assert.Equal(affected.Count(node => node.Kind is PassiveNodeKind.Notable or PassiveNodeKind.Mastery) * 200,
            modifiers.IncreasedMaximumLifeBasisPoints);

        static double Distance(string left, string right)
        {
            PassiveNodeDefinition a = PassiveTree.Get(left);
            PassiveNodeDefinition b = PassiveTree.Get(right);
            return Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
        }
    }

    [Fact]
    public void CrimsonMemoryUsesFinalPhysiqueInCharacterPreviewAndCombatBuild()
    {
        string socket = PassiveTree.Nodes.First(node => node.Kind == PassiveNodeKind.JewelSocket).StableId;
        var jewels = new JewelState();
        JewelInstance memory = JewelCatalog.CreateLegendary("crimson_memory", 1, "crimson-memory");
        Assert.True(jewels.TryAdd(memory));
        Assert.True(jewels.TrySocket(socket, memory.InstanceId, 100, out _));
        var passives = new PassiveTreeAllocation();
        var loadout = new EquipmentLoadout();
        var skill = new SkillConfiguration(SkillIds.HeavyStrike, SkillSupport.None);

        AssembledCharacterBuild lower = CharacterBuildAssembler.Assemble(100,
            new CharacterAttributes(1_100, 10, 10, 10), loadout, passives, skill, jewels);
        AssembledCharacterBuild higher = CharacterBuildAssembler.Assemble(100,
            new CharacterAttributes(1_300, 10, 10, 10), loadout, passives, skill, jewels);

        Assert.Equal(33_000, lower.IncreasedAttackDamageBasisPoints);
        Assert.Equal(39_000, higher.IncreasedAttackDamageBasisPoints);
        Assert.Equal(26_400, lower.Sheet.IncreasedArmorBasisPoints);
        Assert.Equal(31_200, higher.Sheet.IncreasedArmorBasisPoints);
        Assert.True(higher.IncreasedAttackDamageBasisPoints > lower.IncreasedAttackDamageBasisPoints);
        Assert.Equal(6_000, higher.IncreasedAttackDamageBasisPoints - lower.IncreasedAttackDamageBasisPoints);
        Assert.True(higher.Sheet.IncreasedArmorBasisPoints > lower.Sheet.IncreasedArmorBasisPoints);
    }

    [Fact]
    public void AllAttributeMemoryJewelsScaleFromTheirFinalAttribute()
    {
        CharacterAttributes attributes = new(1_000, 750, 500, 250);
        AssertMemory("crimson_memory", modifiers =>
        {
            Assert.Equal(30_000, modifiers.IncreasedAttackDamageBasisPoints);
            Assert.Equal(24_000, modifiers.IncreasedArmorBasisPoints);
        });
        AssertMemory("verdant_memory", modifiers =>
        {
            Assert.Equal(6_000, modifiers.IncreasedAttackSpeedBasisPoints);
            Assert.Equal(18_000, modifiers.IncreasedEvasionBasisPoints);
        });
        AssertMemory("golden_memory", modifiers =>
        {
            Assert.Equal(8_000, modifiers.IncreasedMaximumManaBasisPoints);
            Assert.Equal(4_000, modifiers.IncreasedRecoveryRateBasisPoints);
        });
        AssertMemory("azure_memory", modifiers =>
        {
            Assert.Equal(7_500, modifiers.IncreasedSpellDamageBasisPoints);
            Assert.Equal(6_000, modifiers.IncreasedMaximumShieldBasisPoints);
        });

        void AssertMemory(string legendaryId, Action<JewelModifiers> assert)
        {
            string socket = PassiveTree.Nodes.First(node => node.Kind == PassiveNodeKind.JewelSocket).StableId;
            var state = new JewelState();
            JewelInstance jewel = JewelCatalog.CreateLegendary(legendaryId, 1, legendaryId);
            Assert.True(state.TryAdd(jewel));
            Assert.True(state.TrySocket(socket, jewel.InstanceId, 100, out _));
            assert(JewelCatalog.CalculateAttributeMemoryModifiers(state, attributes));
        }
    }

    [Fact]
    public void JewelCleanupProtectsSocketedJewelsAndRequiresConfirmationForBatchAndRareItems()
    {
        GameSession session = GameSession.CreateNew(new("珠宝清理", CharacterGender.Androgynous,
            CharacterSkinTone.Fair, CharacterHairStyle.Cropped, GameForWork.Core.Characters.BaseClass.Fighter), 0x30c1);
        JewelInstance normal = JewelCatalog.RollPrismatic(1, 1, "cleanup-normal", JewelRarity.Normal);
        JewelInstance magic = JewelCatalog.RollPrismatic(1, 2, "cleanup-magic", JewelRarity.Magic);
        JewelInstance rare = JewelCatalog.RollPrismatic(1, 3, "cleanup-rare", JewelRarity.Rare);
        Assert.True(session.Jewels.TryAdd(normal));
        Assert.True(session.Jewels.TryAdd(magic));
        Assert.True(session.Jewels.TryAdd(rare));
        string socket = PassiveTree.Nodes.First(node => node.Kind == PassiveNodeKind.JewelSocket).StableId;
        Assert.True(session.Jewels.TrySocket(socket, normal.InstanceId, 1, out _));
        int scraps = session.World.Economy.IronScraps;

        Assert.False(session.TryDismantleBuildsJewels(JewelRarity.Magic, confirmed: false, out string preview));
        Assert.Contains("1 枚", preview);
        Assert.True(session.TryDismantleBuildsJewels(JewelRarity.Magic, confirmed: true, out _));
        Assert.Contains(session.Jewels.Items, item => item.InstanceId == normal.InstanceId);
        Assert.DoesNotContain(session.Jewels.Items, item => item.InstanceId == magic.InstanceId);
        Assert.Equal(scraps + 2, session.World.Economy.IronScraps);

        Assert.False(session.TryDismantleBuildsJewel(rare.InstanceId, confirmed: false, out _));
        Assert.True(session.TryDismantleBuildsJewel(rare.InstanceId, confirmed: true, out _));
        Assert.Equal(scraps + 7, session.World.Economy.IronScraps);
        Assert.False(session.TryDismantleBuildsJewel(normal.InstanceId, confirmed: true, out string protectedMessage));
        Assert.Contains("已镶嵌", protectedMessage);
    }

    [Fact]
    public void JewelSocketingMovesOneInstanceAndCraftingUsesConfirmedCorruptionValues()
    {
        JewelInstance jewel = JewelCatalog.RollPrismatic(100, 0x3005UL, "craft-jewel");
        var state = new JewelState();
        Assert.True(state.TryAdd(jewel));
        string[] sockets = PassiveTree.Nodes.Where(node => node.Kind == PassiveNodeKind.JewelSocket)
            .Take(2).Select(node => node.StableId).ToArray();
        Assert.True(state.TrySocket(sockets[0], jewel.InstanceId, 100, out _));
        Assert.True(state.TrySocket(sockets[1], jewel.InstanceId, 100, out _));
        Assert.False(state.Socketed.ContainsKey(sockets[0]));
        Assert.Equal(jewel.InstanceId, state.Socketed[sockets[1]]);

        JewelInstance corrupted = JewelCatalog.Corrupt(jewel, 1).Jewel!;
        JewelAffix implicitAffix = Assert.Single(corrupted.Affixes,
            affix => affix.Position == JewelAffixPosition.CorruptedImplicit);
        Assert.Equal(600, implicitAffix.Value);
        Assert.Contains("6% 更多", JewelCatalog.AffixText(implicitAffix));
    }

    [Fact]
    public void LocalQualityScalesTheFinalBaseAndLocalAffixValue()
    {
        ItemInstance item = ItemGenerator.Generate(
            "core.base.rusted_greatsword", 90, ItemRarity.Basic, 0x3010, "quality-test");
        WeaponProfile plain = EquipmentLoadout.CalculateWeapon(item);
        WeaponProfile polished = EquipmentLoadout.CalculateWeapon(item with { Quality = 20 });

        Assert.Equal(plain.MinimumPhysicalDamage * 120 / 100, polished.MinimumPhysicalDamage);
        Assert.Equal(plain.MaximumPhysicalDamage * 120 / 100, polished.MaximumPhysicalDamage);
    }

    [Fact]
    public void JewelAffixTagsRoundTripThroughTheSaveJsonSerializer()
    {
        JewelInstance source = JewelCatalog.RollPrismatic(100, 0x3001UL, "json-jewel");
        string json = JsonSerializer.Serialize(source);
        JewelInstance restored = JsonSerializer.Deserialize<JewelInstance>(json)!;

        Assert.Equal(source.Affixes.Select(affix => affix.Tags), restored.Affixes.Select(affix => affix.Tags));
    }

    [Fact]
    public void VirtueViceUsesSharedDurationAndConfirmedLinearBonuses()
    {
        var state = new VirtueViceState(new Dictionary<VirtueViceKind, int>
        { [VirtueViceKind.Mercy] = 1, [VirtueViceKind.Arrogance] = 1 });
        Assert.True(state.Gain(VirtueViceKind.Mercy, 3));
        Assert.True(state.Gain(VirtueViceKind.Arrogance, 3));
        VirtueViceBonuses bonuses = state.Bonuses();
        Assert.Equal(4_500, bonuses.IncreasedMaximumLifeBasisPoints);
        Assert.Equal(7_900, bonuses.PhysicalVoidDamageTakenMultiplierBasisPoints);
        Assert.Equal(12_000, bonuses.IncreasedCriticalChanceBasisPoints);
        Assert.Equal(3_600, bonuses.MoreCriticalDamageBasisPoints);
        state.Advance(12_000);
        Assert.Equal(0, state.Layers(VirtueViceKind.Mercy));
    }

    [Fact]
    public void OathActionsAreDeterministicLimitedAndCanRefreshAtMaximum()
    {
        var state = new VirtueViceState();
        Assert.True(state.TryOathChance(VirtueViceKind.Rage, "action-1", 1_200, 0));
        Assert.False(state.TryOathChance(VirtueViceKind.Rage, "action-1", 1_200, 0));
        for (int index = 0; index < 7; index++) Assert.False(state.RecordSlothOathHit($"sloth-{index}"));
        Assert.True(state.RecordSlothOathHit("sloth-7"));
        Assert.Equal(1, state.Layers(VirtueViceKind.Sloth));
    }

    [Fact]
    public void NewSessionSnapshotPersistsBuildsJewelState()
    {
        GameSession session = GameSession.CreateNew(new("Builds测试", CharacterGender.Woman,
            CharacterSkinTone.Fair, CharacterHairStyle.Cropped, GameForWork.Core.Characters.BaseClass.Fighter), 30);
        Assert.True(session.Jewels.TryAdd(JewelCatalog.CreateLegendary("ember_core", 70, "saved-jewel")));
        Assert.True(session.Jewels.TryAdd(JewelCatalog.RollPrismatic(100, 0x3002UL, "saved-rare-jewel")));
        string json = JsonSerializer.Serialize(session.Capture());
        GameSessionSnapshot snapshot = JsonSerializer.Deserialize<GameSessionSnapshot>(json)!;
        GameSession restored = GameSession.Restore(snapshot);
        Assert.Contains(restored.Jewels.Items, item => item.InstanceId == "saved-jewel");
        Assert.Contains(restored.Jewels.Items, item => item.InstanceId == "saved-rare-jewel" && item.Affixes.Count == 4);
        Assert.Equal(25, GameSession.CurrentFormatVersion);
    }

    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(60, 30, 89)]
    [InlineData(70, 30, 99)]
    [InlineData(100, 30, 129)]
    [InlineData(120, 30, 149)]
    public void PassivePointEconomyIsLevelMinusOnePlusThirtyStoryFlags(int level, int story, int expected)
    {
        var progression = new GameForWork.Core.Campaign.Progression.CharacterProgression();
        progression.Restore(level, GameForWork.Core.Campaign.Progression.CharacterProgression.CumulativeExperienceForLevel(level),
            Math.Max(0, level - 1), false);
        progression.SynchronizeStoryPassivePoints(story);
        Assert.Equal(expected, progression.EarnedPassivePoints);
    }
}
