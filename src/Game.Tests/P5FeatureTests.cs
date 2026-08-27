using GameForWork.Core.P1;
using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.Progression;
using GameForWork.Core.P1.World;
using GameForWork.Core.P2;
using GameForWork.Core.P5;

namespace GameForWork.Tests;

public sealed class P5FeatureTests
{
    [Fact]
    public void DispatchSelectsHighestMapAndKeepsOneVisibleAssignment()
    {
        P1GameSession session = CreateSession();
        session.World.MapInventory.Add(new P1MapItem("p5-low", 2));
        session.World.MapInventory.Add(new P1MapItem("p5-high", 9));

        session.AssignExpedition(
            ExpeditionTeamKind.Hero,
            P5ExpeditionTarget.HighestTierMaps,
            P5DispatchMode.Once);

        Assert.Equal("p5-high", Assert.Single(session.World.Hero.Queue.Maps).InstanceId);
        Assert.Equal(MapRoute.Abyss, session.World.Hero.Policy.PreferredRoute);
        Assert.False(session.World.Expedition.Get(ExpeditionTeamKind.Hero)!.Enabled);
        Assert.Single(session.World.MapInventory);
    }

    [Fact]
    public void TwelveMapCompletionsAutomaticallyCreateOneBossTicket()
    {
        var director = new P5ExpeditionDirector();
        for (int index = 0; index < 12; index++)
        {
            director.RecordResolved(new P1MapItem($"map-{index}", 5), succeeded: true);
        }

        Assert.Equal(0, director.AbyssWardenFragments);
        Assert.Equal(1, director.AbyssWardenTickets);
        Assert.Equal(0, director.MapsTowardNextFragment);
    }

    [Fact]
    public void AutomaticDispatchDoesNotResumeAStoppedTeam()
    {
        P1GameSession session = CreateSession();
        session.World.MapInventory.Add(new P1MapItem("p5-waiting", 3));
        session.World.Expedition.Assign(
            ExpeditionTeamKind.Hero,
            P5ExpeditionTarget.SafeMaps,
            P5DispatchMode.Repeat);
        session.World.Hero.Stop("storage_full");

        Assert.False(session.World.Expedition.PrepareNext(session.World, session.World.Hero));
        Assert.Empty(session.World.Hero.Queue.Maps);
        Assert.Single(session.World.MapInventory);
        Assert.True(session.World.Hero.IsStopped);
    }

    [Fact]
    public void ExpeditionProgressAndAssignmentsSurviveSessionSnapshot()
    {
        P1GameSession session = CreateSession();
        session.World.Expedition.RecordResolved(new P1MapItem("map-one", 1), succeeded: true);
        session.World.MapInventory.Add(new P1MapItem("map-two", 2));
        session.AssignExpedition(ExpeditionTeamKind.Mercenaries, P5ExpeditionTarget.SafeMaps, P5DispatchMode.Repeat);

        P1GameSession restored = P1GameSession.Restore(session.Capture());

        Assert.Equal(1, restored.World.Expedition.MapsTowardNextFragment);
        Assert.Equal(P5ExpeditionTarget.SafeMaps,
            restored.World.Expedition.Get(ExpeditionTeamKind.Mercenaries)!.Target);
        Assert.Single(restored.World.Mercenaries.Queue.Maps);
    }

    [Fact]
    public void StarterEquipmentGeneratesFourCharacterOwnedSkillChains()
    {
        P1GameSession session = CreateSession();

        IReadOnlyList<P5SkillChainDefinition> chains = session.GetSkillChains();

        Assert.Equal(4, chains.Count);
        Assert.Equal(5, chains.Sum(chain => chain.SupportCapacity));
        Assert.Equal(4, session.Management.SkillLinks.Count(link => !string.IsNullOrEmpty(link.ChainId)));
        Assert.Equal("战吼", session.Management.SkillStones.Single(stone => stone.InstanceId ==
            session.Management.SkillLinks.Single(link => link.ChainId == P5SkillChainIds.HelmetTool)
                .ActiveStoneInstanceId).Definition.DisplayName);
    }

    [Fact]
    public void EquipmentChainCapacityRejectsOverflowWithoutDestroyingSupports()
    {
        P1GameSession session = CreateSession();
        SkillStoneInstance heavy = session.Management.SkillStones.Single(
            stone => stone.DefinitionId == "core.skill_stone.heavy_strike");
        SkillStoneInstance speed = session.Management.SkillStones.Single(
            stone => stone.DefinitionId == "core.skill_stone.attack_speed");
        SkillStoneInstance life = session.Management.SkillStones.Single(
            stone => stone.DefinitionId == "core.skill_stone.life_cost");

        Assert.True(session.TryLinkSkillSupport(heavy.InstanceId, speed.InstanceId));
        Assert.False(session.TryLinkSkillSupport(heavy.InstanceId, life.InstanceId));
        Assert.Contains(session.Management.SkillStones, stone => stone.InstanceId == life.InstanceId);
    }

    [Fact]
    public void RefundChecksRemainingGraphConnectivity()
    {
        var allocation = new PassiveTreeAllocation(memoryAshes: 5);
        Assert.True(allocation.TryAllocate("core.passive.heavy.1", 2));
        Assert.True(allocation.TryAllocate("core.passive.heavy.2", 2));

        Assert.False(allocation.TryRefund("core.passive.heavy.1"));
        Assert.True(allocation.TryRefund("core.passive.heavy.2"));
        Assert.Equal(4, allocation.MemoryAshes);
    }

    [Fact]
    public void FirstFourMasteriesProvideBuildDefiningEffects()
    {
        PassiveNodeDefinition[] masteries = P1PassiveTree.Nodes
            .Where(node => node.Kind == PassiveNodeKind.Mastery)
            .ToArray();

        Assert.Equal(4, masteries.Length);
        Assert.All(masteries, mastery => Assert.True(mastery.Effects.Count >= 2));
        Assert.Contains(masteries, mastery => mastery.Effects.Any(effect =>
            effect.Kind == PassiveEffectKind.HeavyWeaponMastery));
        Assert.Contains(masteries, mastery => mastery.Effects.Any(effect =>
            effect.Kind == PassiveEffectKind.BleedMastery));
        Assert.Contains(masteries, mastery => mastery.Effects.Any(effect =>
            effect.Kind == PassiveEffectKind.DefenseMastery));
        Assert.Contains(masteries, mastery => mastery.Effects.Any(effect =>
            effect.Kind == PassiveEffectKind.WarCryMastery));
    }

    private static P1GameSession CreateSession() => P1GameSession.CreateNew(new PlayerIdentity(
        "P5Tester",
        CharacterGender.Androgynous,
        CharacterSkinTone.Umber,
        CharacterHairStyle.Cropped,
        P1Ascendancy.IronOath), 5_005);
}
