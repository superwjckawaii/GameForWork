using GameForWork.Core.P1;
using GameForWork.Core.P1.Progression;
using GameForWork.Core.P10;
using GameForWork.Core.P18;
using GameForWork.Core.P23;

namespace GameForWork.Tests;

public sealed class P23FeatureTests
{
    [Fact]
    public void SixClassesHaveStableIdsBalancedStartingAttributesAndThreeAscendancies()
    {
        Assert.Equal(6, P23ClassCatalog.All.Count);
        Assert.Equal(["斗士", "侠客", "灵能使", "秘术师", "僧侣", "隐士"],
            P23ClassCatalog.All.Select(value => value.DisplayName));
        Assert.All(P23ClassCatalog.All, definition =>
        {
            Assert.Equal(50, definition.StartingAttributes.Physique + definition.StartingAttributes.Dexterity +
                definition.StartingAttributes.Spirit + definition.StartingAttributes.Energy);
            Assert.Equal(3, definition.Ascendancies.Count);
            Assert.Equal(3, definition.Ascendancies.Distinct().Count());
            Assert.StartsWith("core.class.", definition.StableId, StringComparison.Ordinal);
        });
        Assert.Equal(18, P23ClassCatalog.All.SelectMany(value => value.Ascendancies).Distinct().Count());
    }

    [Fact]
    public void EighteenAscendanciesHaveUniqueFinalDisplayNames()
    {
        P18Ascendancy[] values = Enum.GetValues<P18Ascendancy>().Where(value => value != P18Ascendancy.None).ToArray();
        string[] names = values.Select(P18AscendancyCatalog.DisplayName).ToArray();

        Assert.Equal(18, values.Length);
        Assert.Equal(18, names.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(["血战士", "铁壁卫", "破军者"],
            P23ClassCatalog.Get(P23BaseClass.Fighter).Ascendancies.Select(P18AscendancyCatalog.DisplayName));
    }

    [Theory]
    [InlineData(P23BaseClass.Fighter)]
    [InlineData(P23BaseClass.Rogue)]
    [InlineData(P23BaseClass.Psion)]
    [InlineData(P23BaseClass.Occultist)]
    [InlineData(P23BaseClass.Monk)]
    [InlineData(P23BaseClass.Hermit)]
    public void EveryClassCreatesAPlayableStarterAndRoundTrips(P23BaseClass baseClass)
    {
        P23ClassDefinition definition = P23ClassCatalog.Get(baseClass);
        var identity = new PlayerIdentity($"角色{(int)baseClass}", CharacterGender.Androgynous,
            CharacterSkinTone.Umber, CharacterHairStyle.Cropped, baseClass);

        P1GameSession session = P1GameSession.CreateNew(identity, 0x2300UL + (ulong)baseClass, tutorialEnabled: false);
        P1GameSession restored = P1GameSession.Restore(session.Capture());

        Assert.Equal(baseClass, restored.Player.BaseClass);
        Assert.Equal(session.HeroBuild.Sheet.Attributes, restored.HeroBuild.Sheet.Attributes);
        Assert.Equal(definition.PassiveStart, restored.Passives.StartKind);
        Assert.Contains(restored.Management.SkillLinks, link => restored.Management.SkillStones
            .Any(stone => stone.InstanceId == link.ActiveStoneInstanceId && stone.DefinitionId ==
                definition.StarterSkillId.Replace("core.skill.", "core.skill_stone.", StringComparison.Ordinal)));

        var advance = restored.Advance(240_000);
        Assert.True(advance.EffectiveMilliseconds > 0);
        Assert.False(string.IsNullOrWhiteSpace(advance.FinalHash));
    }

    [Fact]
    public void VersionEighteenMigratesToFighterAndRefundsTheOldTree()
    {
        P1GameSession current = P1GameSession.CreateNew(new PlayerIdentity("旧档角色", CharacterGender.Androgynous,
            CharacterSkinTone.Umber, CharacterHairStyle.Cropped, P23BaseClass.Rogue), 0x2318);
        Assert.True(current.Passives.TryAllocate(P1PassiveTree.Neighbors(P1PassiveTree.StartNode(current.Passives.StartKind)).First(), 1));
        P1GameSessionSnapshot legacy = current.Capture() with { FormatVersion = 18 };

        P1GameSession migrated = P1GameSession.Restore(legacy);

        Assert.Equal(P23BaseClass.Fighter, migrated.Player.BaseClass);
        Assert.Empty(migrated.Passives.Allocated);
        Assert.Equal(legacy.MemoryAshes, migrated.Passives.MemoryAshes);
        Assert.Equal(PassiveStartKind.Physique, migrated.Passives.StartKind);
        Assert.True(migrated.Management.FreeFullRespecAvailable);
    }

    [Fact]
    public void RestoreRejectsAnAscendancyFromAnotherBaseClass()
    {
        P1GameSession session = P1GameSession.CreateNew(new PlayerIdentity("错配角色", CharacterGender.Androgynous,
            CharacterSkinTone.Umber, CharacterHairStyle.Cropped, P23BaseClass.Fighter), 0x23ff);
        P1GameSessionSnapshot snapshot = session.Capture();
        P10EndgameSnapshot invalidEndgame = snapshot.Endgame! with { SelectedAscendancy = P18Ascendancy.Marksman };

        Assert.Throws<InvalidDataException>(() => P1GameSession.Restore(snapshot with { Endgame = invalidEndgame }));
    }

    [Fact]
    public void SixFreeStartsAndSeventyTwoMasteryGroupsAreReachable()
    {
        PassiveNodeDefinition[] starts = P1PassiveTree.Nodes.Where(node => node.Kind == PassiveNodeKind.Start).ToArray();
        PassiveNodeDefinition[] masteries = P1PassiveTree.Nodes.Where(node => node.Kind == PassiveNodeKind.Mastery).ToArray();

        Assert.Equal(6, starts.Length);
        Assert.All(starts, start =>
        {
            Assert.Empty(start.Effects);
            Assert.Equal(3, P1PassiveTree.Neighbors(start.StableId).Count);
        });
        Assert.Equal(168, masteries.Length);
        Assert.InRange(masteries.Select(node => node.MasteryGroup).Distinct(StringComparer.Ordinal).Count(), 70, 168);
    }

    [Fact]
    public void AClassCanGrowFromBothSidesOfItsFreeStartAnchor()
    {
        var allocation = new PassiveTreeAllocation(start: PassiveStartKind.Physique);
        string[] startNeighbors = P1PassiveTree.Neighbors(P1PassiveTree.StartNode(PassiveStartKind.Physique)).ToArray();

        Assert.Equal(3, startNeighbors.Length);
        Assert.True(allocation.TryAllocate(startNeighbors[0], 2));
        Assert.True(allocation.TryAllocate(startNeighbors[1], 2));
        Assert.Equal(2, allocation.Allocated.Count);
    }

    [Fact]
    public void CombatFoundationEnforcesCapsAndShieldRechargeDelay()
    {
        Assert.Equal(6, P23CombatLimits.Maximum(P23CombatEntityKind.Minion));
        Assert.Equal(1, P23CombatLimits.Maximum(P23CombatEntityKind.Companion));
        Assert.Equal(3, P23CombatLimits.Maximum(P23CombatEntityKind.Construct));
        Assert.Equal(3, P23CombatLimits.Maximum(P23CombatEntityKind.Trap));

        var shield = new P23EnergyShieldState(100);
        Assert.Equal(0, shield.AbsorbHit(40));
        Assert.Equal(60, shield.Current);
        Assert.Equal(40, shield.AbsorbHit(100));
        Assert.Equal(0, shield.Current);
        for (int index = 0; index < P23EnergyShieldState.RechargeDelayTicks - 1; index++) shield.AdvanceTick();
        Assert.False(shield.IsRecharging);
        shield.AdvanceTick();
        Assert.True(shield.IsRecharging);
        shield.AdvanceTick();
        Assert.True(shield.Current > 0);
    }
}
