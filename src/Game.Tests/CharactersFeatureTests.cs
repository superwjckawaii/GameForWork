using GameForWork.Core.Campaign;
using GameForWork.Core.Campaign.Progression;
using GameForWork.Core.Endgame;
using GameForWork.Core.Ascendancies;
using GameForWork.Core.Characters;

namespace GameForWork.Tests;

public sealed class CharactersFeatureTests
{
    [Fact]
    public void SixClassesHaveStableIdsBalancedStartingAttributesAndThreeAscendancies()
    {
        Assert.Equal(6, ClassCatalog.All.Count);
        Assert.Equal(["斗士", "侠客", "灵能使", "秘术师", "僧侣", "隐士"],
            ClassCatalog.All.Select(value => value.DisplayName));
        Assert.All(ClassCatalog.All, definition =>
        {
            Assert.Equal(50, definition.StartingAttributes.Physique + definition.StartingAttributes.Dexterity +
                definition.StartingAttributes.Spirit + definition.StartingAttributes.Energy);
            Assert.Equal(3, definition.Ascendancies.Count);
            Assert.Equal(3, definition.Ascendancies.Distinct().Count());
            Assert.StartsWith("core.class.", definition.StableId, StringComparison.Ordinal);
        });
        Assert.Equal(18, ClassCatalog.All.SelectMany(value => value.Ascendancies).Distinct().Count());
    }

    [Fact]
    public void EighteenAscendanciesHaveUniqueFinalDisplayNames()
    {
        Ascendancy[] values = Enum.GetValues<Ascendancy>().Where(value => value != Ascendancy.None).ToArray();
        string[] names = values.Select(WarriorAscendancyCatalog.DisplayName).ToArray();

        Assert.Equal(18, values.Length);
        Assert.Equal(18, names.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(["血战士", "铁壁卫", "破军者"],
            ClassCatalog.Get(BaseClass.Fighter).Ascendancies.Select(WarriorAscendancyCatalog.DisplayName));
    }

    [Theory]
    [InlineData(BaseClass.Fighter)]
    [InlineData(BaseClass.Rogue)]
    [InlineData(BaseClass.Psion)]
    [InlineData(BaseClass.Occultist)]
    [InlineData(BaseClass.Monk)]
    [InlineData(BaseClass.Hermit)]
    public void EveryClassCreatesAPlayableStarterAndRoundTrips(BaseClass baseClass)
    {
        ClassDefinition definition = ClassCatalog.Get(baseClass);
        var identity = new PlayerIdentity($"角色{(int)baseClass}", CharacterGender.Androgynous,
            CharacterSkinTone.Umber, CharacterHairStyle.Cropped, baseClass);

        GameSession session = GameSession.CreateNew(identity, 0x2300UL + (ulong)baseClass, tutorialEnabled: false);
        GameSession restored = GameSession.Restore(session.Capture());

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
        GameSession current = GameSession.CreateNew(new PlayerIdentity("旧档角色", CharacterGender.Androgynous,
            CharacterSkinTone.Umber, CharacterHairStyle.Cropped, BaseClass.Rogue), 0x2318);
        Assert.True(current.Passives.TryAllocate(PassiveTree.Neighbors(PassiveTree.StartNode(current.Passives.StartKind)).First(), 1));
        GameSessionSnapshot legacy = current.Capture() with { FormatVersion = 18 };

        GameSession migrated = GameSession.Restore(legacy);

        Assert.Equal(BaseClass.Fighter, migrated.Player.BaseClass);
        Assert.Empty(migrated.Passives.Allocated);
        Assert.Equal(legacy.MemoryAshes, migrated.Passives.MemoryAshes);
        Assert.Equal(PassiveStartKind.Physique, migrated.Passives.StartKind);
        Assert.True(migrated.Management.FreeFullRespecAvailable);
    }

    [Fact]
    public void RestoreRejectsAnAscendancyFromAnotherBaseClass()
    {
        GameSession session = GameSession.CreateNew(new PlayerIdentity("错配角色", CharacterGender.Androgynous,
            CharacterSkinTone.Umber, CharacterHairStyle.Cropped, BaseClass.Fighter), 0x23ff);
        GameSessionSnapshot snapshot = session.Capture();
        EndgameSnapshot invalidEndgame = snapshot.Endgame! with { SelectedAscendancy = Ascendancy.Marksman };

        Assert.Throws<InvalidDataException>(() => GameSession.Restore(snapshot with { Endgame = invalidEndgame }));
    }

    [Fact]
    public void SixFreeStartsAndSixtyTwoConfirmedMasteryGroupsAreReachable()
    {
        PassiveNodeDefinition[] starts = PassiveTree.Nodes.Where(node => node.Kind == PassiveNodeKind.Start).ToArray();
        PassiveNodeDefinition[] masteries = PassiveTree.Nodes.Where(node => node.Kind == PassiveNodeKind.Mastery).ToArray();

        Assert.Equal(6, starts.Length);
        Assert.All(starts, start =>
        {
            Assert.Empty(start.Effects);
            Assert.Equal(3, PassiveTree.Neighbors(start.StableId).Count);
        });
        Assert.Equal(168, masteries.Length);
        Assert.Equal(62, masteries.Select(node => node.MasteryGroup).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void AClassCanGrowFromBothSidesOfItsFreeStartAnchor()
    {
        var allocation = new PassiveTreeAllocation(start: PassiveStartKind.Physique);
        string[] startNeighbors = PassiveTree.Neighbors(PassiveTree.StartNode(PassiveStartKind.Physique)).ToArray();

        Assert.Equal(3, startNeighbors.Length);
        Assert.True(allocation.TryAllocate(startNeighbors[0], 2));
        Assert.True(allocation.TryAllocate(startNeighbors[1], 2));
        Assert.Equal(2, allocation.Allocated.Count);
    }

    [Fact]
    public void CombatFoundationEnforcesCapsAndShieldRechargeDelay()
    {
        Assert.Equal(6, CombatLimits.Maximum(CombatEntityKind.Minion));
        Assert.Equal(1, CombatLimits.Maximum(CombatEntityKind.Companion));
        Assert.Equal(3, CombatLimits.Maximum(CombatEntityKind.Construct));
        Assert.Equal(3, CombatLimits.Maximum(CombatEntityKind.Trap));

        var shield = new EnergyShieldState(100);
        Assert.Equal(0, shield.AbsorbHit(40));
        Assert.Equal(60, shield.Current);
        Assert.Equal(40, shield.AbsorbHit(100));
        Assert.Equal(0, shield.Current);
        for (int index = 0; index < EnergyShieldState.RechargeDelayTicks - 1; index++) shield.AdvanceTick();
        Assert.False(shield.IsRecharging);
        shield.AdvanceTick();
        Assert.True(shield.IsRecharging);
        shield.AdvanceTick();
        Assert.True(shield.Current > 0);
    }
}
