using GameForWork.Core.P1;
using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P1.Progression;
using GameForWork.Core.P18;
using GameForWork.Core.P30;
using System.Text.Json;

namespace GameForWork.Tests;

public sealed class P30SystemsTests
{
    [Fact]
    public void SkillCatalogSealsAllConfirmedP30Data()
    {
        Assert.Equal(86, P30SkillCatalog.Active.Count);
        Assert.Equal(98, P30SkillCatalog.Supports.Count);
        P30ActiveSkillDefinition heavy = P30SkillCatalog.Active.Single(item => item.Combat.DisplayName == "重击");
        Assert.Equal(16_000, heavy.DamageAt(1));
        Assert.Equal(42_500, heavy.DamageAt(21));
        Assert.Equal(12, heavy.ManaAt(21));
        Assert.Equal("p30.skill.shield_bash", P30SkillCatalog.Active.Single(item => item.Combat.DisplayName == "盾锋冲击").Combat.SkillId);
        Assert.Equal(5, P30SkillCatalog.Active.Count(item => item.P30Added && item.Combat.Role == GameForWork.Core.P17.P17SkillRole.Reservation));

        P30SupportSkillDefinition temperance = P30SkillCatalog.Supports.Single(item => item.DisplayName == "持律精算");
        Assert.Equal(6, temperance.ValueAt(21));
        Assert.Contains("有效等级 +1", temperance.Effect, StringComparison.Ordinal);
        Assert.Equal(50, P30SkillCatalog.Supports.Single(item => item.DisplayName == "过载供能").ValueAt(21));
    }

    [Fact]
    public void SupportCompatibilitySealsExclusiveModes()
    {
        P30SupportSkillDefinition chain = P30SkillCatalog.Supports.Single(item => item.DisplayName == "追加连锁");
        P30SupportSkillDefinition seeking = P30SkillCatalog.Supports.Single(item => item.DisplayName == "追踪连锁");
        P30SupportSkillDefinition pierce = P30SkillCatalog.Supports.Single(item => item.DisplayName == "贯穿");
        P30SupportSkillDefinition precision = P30SkillCatalog.Supports.Single(item => item.DisplayName == "精准穿透");
        Assert.False(P30SkillCatalog.AreCompatible(chain, seeking));
        Assert.False(P30SkillCatalog.AreCompatible(pierce, precision));
        Assert.True(P30SkillCatalog.AreCompatible(chain, pierce));
        Assert.All(P30SkillCatalog.Supports, support =>
            Assert.Contains(P30SkillCatalog.Active, active => P30SkillCatalog.SupportsActive(support, active)));
    }

    [Fact]
    public void P30LinkedSupportsUseTheirOwnLevelsCostsAndRuntimeMechanics()
    {
        P30ActiveSkillDefinition heavy = P30SkillCatalog.Active.Single(item => item.Combat.DisplayName == "重击");
        P30SupportSkillDefinition lone = P30SkillCatalog.Supports.Single(item => item.DisplayName == "孤锋专注");
        P30SupportSkillDefinition overload = P30SkillCatalog.Supports.Single(item => item.DisplayName == "过载供能");
        P30SupportRuntimeProfile profile = P30SkillCatalog.ResolveSupports(heavy,
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
        IReadOnlyList<P30BuildAuditResult> results = P30BuildAudit.Run();
        Assert.Equal(36, results.Count);
        Assert.Empty(P30BuildAudit.Validate(results));
        Assert.Equal(18, results.Select(item => item.Build.Ascendancy).Distinct().Count());
        Assert.All(results, item => Assert.True(item.Passed, item.Build.DisplayName));
    }

    [Fact]
    public void ConfirmedCombatMathUsesHistoryAndDirectedConversion()
    {
        P30DamagePacket packet = P30CombatRules.ConvertAndScale(100, P30DamageType.Physical,
            [new(P30DamageType.Physical, P30DamageType.Fire, 10_000, "test")], [],
            new(new Dictionary<P30DamageType, int>
            { [P30DamageType.Physical] = 20_000, [P30DamageType.Fire] = 5_000 }));
        Assert.Equal(450, packet.Fire);
        Assert.Equal(0, packet.Physical);
        Assert.Equal(10_000, P30CombatRules.HitChance(10_000_000, 1));
        Assert.Equal(-50_000, P30CombatRules.EffectiveResistance(-90_000, 7_500));
        Assert.Equal(600, P30CombatRules.NaturalSpiritBarrier(100, 100));
        Assert.True(P30CombatRules.PhysicalDotArmorReduction(10_000, 1_000) <
                    P30CombatRules.ArmorReduction(10_000, 1_000));
    }

    [Fact]
    public void MainTreeIsTheConfirmed1475NodeTopology()
    {
        Assert.Equal(1_475, P1PassiveTree.Nodes.Count);
        Assert.Equal(24, P1PassiveTree.Nodes.Count(node => node.Kind == PassiveNodeKind.JewelSocket));
        Assert.Equal(168, P1PassiveTree.Nodes.Count(node => node.Kind == PassiveNodeKind.Mastery));
        Assert.Equal(149, PassiveTreeAllocation.MaximumAllocatedPoints);
        Assert.All(Enum.GetValues<PassiveStartKind>().Where(value => value != PassiveStartKind.None), start =>
            Assert.Equal(3, P1PassiveTree.Neighbors(P1PassiveTree.StartNode(start)).Count));
    }

    [Fact]
    public void ConfirmedMasteryDescriptionsAndNodeNamesDriveTheP30Tree()
    {
        PassiveNodeDefinition notable = P1PassiveTree.Get("p30.cluster.v5_o05.notable01");
        Assert.Equal("毒潮积累", notable.DisplayName);
        Assert.Equal("中毒", notable.ClusterTheme);
        Assert.StartsWith("中毒持续时间提高 60%", notable.SpecialRule);
        PassiveNodeDefinition mastery = P1PassiveTree.Get("p30.cluster.v5_o05.mastery");
        IReadOnlyList<string> options = P1PassiveTree.MasteryOptionDescriptions(mastery);
        Assert.Equal(7, options.Count);
        Assert.StartsWith("浓缩毒液：中毒造成 60% 更多伤害", options[0]);
        Assert.StartsWith("致命潜伏：", options[6]);
    }

    [Fact]
    public void PassiveLayoutKeepsNodesAndUnrelatedConnectionsApart()
    {
        PassiveNodeDefinition[] nodes = P1PassiveTree.Nodes.ToArray();
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
        foreach (string targetId in P1PassiveTree.Neighbors(from.StableId))
        {
            string edge = string.CompareOrdinal(from.StableId, targetId) < 0
                ? from.StableId + '|' + targetId
                : targetId + '|' + from.StableId;
            if (!edges.Add(edge)) continue;
            PassiveNodeDefinition to = P1PassiveTree.Get(targetId);
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
    public void AllEighteenAscendanciesUseConfirmedP30Nodes()
    {
        Assert.Equal(18, P30Ascendancies.All.Count);
        Assert.Equal(216, P18AscendancyCatalog.Nodes.Count);
        Assert.All(P30Ascendancies.All, path =>
        {
            Assert.Equal(6, path.Branches.Count);
            Assert.Equal(12, P18AscendancyCatalog.For(path.Ascendancy).Count);
        });
        Assert.Equal("血肉薪火", P18AscendancyCatalog.For(P18Ascendancy.BloodFighter)[0].DisplayName);
        Assert.Contains("50% 更多伤害", P18AscendancyCatalog.For(P18Ascendancy.Warbreaker)
            .Single(node => node.DisplayName == "摧城崩线").Effect);
    }

    [Fact]
    public void JewelInstancesRollPersistSocketAndCorrupt()
    {
        P30JewelInstance jewel = P30Jewels.RollPrismatic(90, 0x123456789UL, "jewel-1");
        Assert.Equal(4, jewel.Affixes.Count);
        Assert.InRange(jewel.Resonance, 0, 40);
        var state = new P30JewelState();
        Assert.True(state.TryAdd(jewel));
        string socket = P1PassiveTree.Nodes.First(node => node.Kind == PassiveNodeKind.JewelSocket).StableId;
        Assert.True(state.TrySocket(socket, jewel.InstanceId, 100, out _));
        P30JewelState restored = P30JewelState.Restore(state.Capture());
        Assert.Equal(jewel.InstanceId, restored.Socketed[socket]);
        Assert.Equal(20, P30Jewels.Legendary.Count);
        Assert.Equal(P30JewelCorruptionResult.PowerfulImplicit, P30Jewels.Corrupt(jewel, 1).Result);
    }

    [Fact]
    public void JewelSocketingMovesOneInstanceAndCraftingUsesConfirmedCorruptionValues()
    {
        P30JewelInstance jewel = P30Jewels.RollPrismatic(100, 0x3005UL, "craft-jewel");
        var state = new P30JewelState();
        Assert.True(state.TryAdd(jewel));
        string[] sockets = P1PassiveTree.Nodes.Where(node => node.Kind == PassiveNodeKind.JewelSocket)
            .Take(2).Select(node => node.StableId).ToArray();
        Assert.True(state.TrySocket(sockets[0], jewel.InstanceId, 100, out _));
        Assert.True(state.TrySocket(sockets[1], jewel.InstanceId, 100, out _));
        Assert.False(state.Socketed.ContainsKey(sockets[0]));
        Assert.Equal(jewel.InstanceId, state.Socketed[sockets[1]]);

        P30JewelInstance corrupted = P30Jewels.Corrupt(jewel, 1).Jewel!;
        P30JewelAffix implicitAffix = Assert.Single(corrupted.Affixes,
            affix => affix.Position == P30JewelAffixPosition.CorruptedImplicit);
        Assert.Equal(600, implicitAffix.Value);
        Assert.Contains("6% 更多", P30Jewels.AffixText(implicitAffix));
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
        P30JewelInstance source = P30Jewels.RollPrismatic(100, 0x3001UL, "json-jewel");
        string json = JsonSerializer.Serialize(source);
        P30JewelInstance restored = JsonSerializer.Deserialize<P30JewelInstance>(json)!;

        Assert.Equal(source.Affixes.Select(affix => affix.Tags), restored.Affixes.Select(affix => affix.Tags));
    }

    [Fact]
    public void VirtueViceUsesSharedDurationAndConfirmedLinearBonuses()
    {
        var state = new P30VirtueViceState(new Dictionary<P30VirtueViceKind, int>
        { [P30VirtueViceKind.Mercy] = 1, [P30VirtueViceKind.Arrogance] = 1 });
        Assert.True(state.Gain(P30VirtueViceKind.Mercy, 3));
        Assert.True(state.Gain(P30VirtueViceKind.Arrogance, 3));
        P30VirtueViceBonuses bonuses = state.Bonuses();
        Assert.Equal(4_500, bonuses.IncreasedMaximumLifeBasisPoints);
        Assert.Equal(7_900, bonuses.PhysicalVoidDamageTakenMultiplierBasisPoints);
        Assert.Equal(12_000, bonuses.IncreasedCriticalChanceBasisPoints);
        Assert.Equal(3_600, bonuses.MoreCriticalDamageBasisPoints);
        state.Advance(12_000);
        Assert.Equal(0, state.Layers(P30VirtueViceKind.Mercy));
    }

    [Fact]
    public void OathActionsAreDeterministicLimitedAndCanRefreshAtMaximum()
    {
        var state = new P30VirtueViceState();
        Assert.True(state.TryOathChance(P30VirtueViceKind.Rage, "action-1", 1_200, 0));
        Assert.False(state.TryOathChance(P30VirtueViceKind.Rage, "action-1", 1_200, 0));
        for (int index = 0; index < 7; index++) Assert.False(state.RecordSlothOathHit($"sloth-{index}"));
        Assert.True(state.RecordSlothOathHit("sloth-7"));
        Assert.Equal(1, state.Layers(P30VirtueViceKind.Sloth));
    }

    [Fact]
    public void NewSessionSnapshotPersistsP30JewelState()
    {
        P1GameSession session = P1GameSession.CreateNew(new("P30测试", CharacterGender.Woman,
            CharacterSkinTone.Fair, CharacterHairStyle.Cropped, GameForWork.Core.P23.P23BaseClass.Fighter), 30);
        Assert.True(session.Jewels.TryAdd(P30Jewels.CreateLegendary("ember_core", 70, "saved-jewel")));
        Assert.True(session.Jewels.TryAdd(P30Jewels.RollPrismatic(100, 0x3002UL, "saved-rare-jewel")));
        string json = JsonSerializer.Serialize(session.Capture());
        P1GameSessionSnapshot snapshot = JsonSerializer.Deserialize<P1GameSessionSnapshot>(json)!;
        P1GameSession restored = P1GameSession.Restore(snapshot);
        Assert.Contains(restored.Jewels.Items, item => item.InstanceId == "saved-jewel");
        Assert.Contains(restored.Jewels.Items, item => item.InstanceId == "saved-rare-jewel" && item.Affixes.Count == 4);
        Assert.Equal(22, P1GameSession.CurrentFormatVersion);
    }

    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(60, 30, 89)]
    [InlineData(70, 30, 99)]
    [InlineData(100, 30, 129)]
    [InlineData(120, 30, 149)]
    public void PassivePointEconomyIsLevelMinusOnePlusThirtyStoryFlags(int level, int story, int expected)
    {
        var progression = new GameForWork.Core.P1.Progression.CharacterProgression();
        progression.Restore(level, GameForWork.Core.P1.Progression.CharacterProgression.CumulativeExperienceForLevel(level),
            Math.Max(0, level - 1), false);
        progression.SynchronizeStoryPassivePoints(story);
        Assert.Equal(expected, progression.EarnedPassivePoints);
    }
}
