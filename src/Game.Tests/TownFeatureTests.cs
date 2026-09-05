using GameForWork.Core.Campaign;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Spatial;
using GameForWork.Core.Town;

namespace GameForWork.Tests;

public sealed class TownFeatureTests
{
    [Fact]
    public void MetalCatalogOwnsNineteenUniquePersistentCurrencies()
    {
        Assert.Equal(19, MetalCurrencies.All.Count);
        Assert.Equal(19, MetalCurrencies.All.Select(item => item.Kind).Distinct().Count());
        Assert.Equal(19, MetalCurrencies.All.Select(item => item.StableId).Distinct().Count());

        GameSession restored = GameSession.Restore(CreateSession().Capture());
        Assert.All(Enum.GetValues<MetalCurrencyKind>(), kind =>
            Assert.True(restored.World.Economy.MetalCurrencies.ContainsKey(kind)));
    }

    [Fact]
    public void MetalCraftingIsDeterministicAndConsumesExactlyOneMetal()
    {
        ItemInstance basic = ItemGenerator.Generate(
            "core.base.rusted_greatsword", 12, ItemRarity.Basic, 0x9001, "town-craft-target");
        GameSession session = CreateSession();
        session.World.Economy.AddMetal(MetalCurrencyKind.AwakeningCopper, 1);

        CraftResult first = ItemCraftingRules.Craft(
            session.World.Economy, basic, ItemCraftOperation.AwakenMagic);
        CraftResult replay = ItemCraftingRules.Preview(basic, ItemCraftOperation.AwakenMagic);

        Assert.True(first.Succeeded);
        Assert.Equal(ItemRarity.Magic, first.Result!.Rarity);
        Assert.Equal(first.Result.Affixes.Select(affix => (affix.Definition.StableFamilyId, affix.Value)),
            replay.Result!.Affixes.Select(affix => (affix.Definition.StableFamilyId, affix.Value)));
        Assert.Equal(0, session.World.Economy.MetalAmount(MetalCurrencyKind.AwakeningCopper));
    }

    [Fact]
    public void SevenBuildingsUpgradeWithOldEffectsUntilConstructionCompletes()
    {
        GameSession session = CreateSession();
        session.World.Economy.AddDispositionProceeds(10_000, 1_000);

        Assert.Equal(7, session.Town.Buildings.Count);
        Assert.True(session.TryUpgradeTownBuilding(BuildingKind.Storage, out _));
        Assert.Equal(100, session.World.Storage.Capacity);

        session.AdvanceTownOnly(143_999);
        Assert.Equal(1, session.Town.Level(BuildingKind.Storage));
        session.AdvanceTownOnly(1);

        Assert.Equal(2, session.Town.Level(BuildingKind.Storage));
        Assert.Equal(150, session.World.Storage.Capacity);
    }

    [Fact]
    public void TavernRetainsTwoLockedCandidatesAcrossDeterministicRefresh()
    {
        GameSession session = CreateSession();
        string[] ids = session.Town.Candidates.Select(candidate => candidate.StableId).ToArray();
        Assert.Equal(4, ids.Length);
        Assert.True(session.Town.ToggleCandidateLock(ids[0]));
        Assert.True(session.Town.ToggleCandidateLock(ids[1]));
        Assert.False(session.Town.ToggleCandidateLock(ids[2]));
        session.World.Economy.AddDispositionProceeds(100, 0);

        Assert.True(session.TryRefreshTavern());
        Assert.Equal(4, session.Town.Candidates.Count);
        Assert.Contains(session.Town.Candidates, candidate => candidate.StableId == ids[0]);
        Assert.Contains(session.Town.Candidates, candidate => candidate.StableId == ids[1]);
    }

    [Fact]
    public void FormationStartsWithThreeMembersAndPersistsIndividualEquipment()
    {
        GameSession session = CreateSession();
        MercenaryMember member = session.Town.Roster[0];
        Assert.Equal(3, session.Town.ActiveMembers().Count);
        Assert.False(session.TryPlaceMercenary(member.Identity.StableId, 3));

        GameSession restored = GameSession.Restore(session.Capture());

        Assert.Equal(session.Town.Formation, restored.Town.Formation);
        Assert.Equal(3, restored.World.Mercenaries.Build.PartySize);
        Assert.Equal(member.Equipment.Items.Keys, restored.Town.Roster[0].Equipment.Items.Keys);
    }

    [Fact]
    public void RosterCanAddAndRemoveMembersWithoutManualFormationSlots()
    {
        GameSession session = CreateSession();
        session.World.Economy.AddDispositionProceeds(20_000, 2_000);
        Assert.True(session.TryUpgradeTownBuilding(BuildingKind.Teleporter, out _));
        session.AdvanceTownOnly(10_000_000);

        string candidateId = session.Town.Candidates[0].StableId;
        Assert.True(session.TryRecruitMercenary(candidateId, out _));
        Assert.True(session.TryAddMercenaryToParty(candidateId));
        Assert.Contains(candidateId, session.Town.ActiveMembers().Select(member => member.Identity.StableId));

        Assert.True(session.TryRemoveMercenaryFromParty(candidateId));
        Assert.DoesNotContain(candidateId, session.Town.ActiveMembers().Select(member => member.Identity.StableId));
        Assert.Equal(3, session.World.Mercenaries.Build.PartySize);
    }

    [Fact]
    public void CartographyIdsRemainUniqueAcrossSaveRestore()
    {
        GameSession session = CreateSession();
        var maps = new List<string>();
        session.Town.Advance(3_600_000, session.World.Economy, map => maps.Add(map.InstanceId));
        TownState restored = TownState.Restore(
            session.Town.Capture(), session.Seed ^ 0x7039746f776eUL, session.MercenaryEquipment);
        restored.Advance(1_800_000, session.World.Economy, map => maps.Add(map.InstanceId));

        Assert.Equal(3, maps.Count);
        Assert.Equal(maps.Count, maps.Distinct().Count());
    }

    private static GameSession CreateSession() => GameSession.CreateNew(new PlayerIdentity(
        "铸城者", CharacterGender.Androgynous, CharacterSkinTone.Umber,
        CharacterHairStyle.Braided, BaseClass.Fighter), 0x9090, tutorialEnabled: false);
}
