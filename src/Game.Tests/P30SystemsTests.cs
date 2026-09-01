using GameForWork.Core.P1;
using GameForWork.Core.P1.Progression;
using GameForWork.Core.P18;
using GameForWork.Core.P30;

namespace GameForWork.Tests;

public sealed class P30SystemsTests
{
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
        P1GameSession restored = P1GameSession.Restore(session.Capture());
        Assert.Contains(restored.Jewels.Items, item => item.InstanceId == "saved-jewel");
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
