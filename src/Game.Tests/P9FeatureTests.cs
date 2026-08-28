using GameForWork.Core.P1;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P4;
using GameForWork.Core.P9;

namespace GameForWork.Tests;

public sealed class P9FeatureTests
{
    [Fact]
    public void MetalCatalogOwnsNineteenUniquePersistentCurrencies()
    {
        Assert.Equal(19, P4MetalCurrencies.All.Count);
        Assert.Equal(19, P4MetalCurrencies.All.Select(item => item.Kind).Distinct().Count());
        Assert.Equal(19, P4MetalCurrencies.All.Select(item => item.StableId).Distinct().Count());

        P1GameSession restored = P1GameSession.Restore(CreateSession().Capture());
        Assert.All(Enum.GetValues<MetalCurrencyKind>(), kind =>
            Assert.True(restored.World.Economy.MetalCurrencies.ContainsKey(kind)));
    }

    [Fact]
    public void MetalCraftingIsDeterministicAndConsumesExactlyOneMetal()
    {
        ItemInstance basic = ItemGenerator.Generate(
            "core.base.rusted_greatsword", 12, ItemRarity.Basic, 0x9001, "p9-craft-target");
        P1GameSession session = CreateSession();
        session.World.Economy.AddMetal(MetalCurrencyKind.AwakeningCopper, 1);

        P9CraftResult first = P9CraftingRules.Craft(
            session.World.Economy, basic, P9CraftOperation.AwakenMagic);
        P9CraftResult replay = P9CraftingRules.Preview(basic, P9CraftOperation.AwakenMagic);

        Assert.True(first.Succeeded);
        Assert.Equal(ItemRarity.Magic, first.Result!.Rarity);
        Assert.Equal(first.Result.Affixes.Select(affix => (affix.Definition.StableFamilyId, affix.Value)),
            replay.Result!.Affixes.Select(affix => (affix.Definition.StableFamilyId, affix.Value)));
        Assert.Equal(0, session.World.Economy.MetalAmount(MetalCurrencyKind.AwakeningCopper));
    }

    [Fact]
    public void SevenBuildingsUpgradeWithOldEffectsUntilConstructionCompletes()
    {
        P1GameSession session = CreateSession();
        session.World.Economy.AddDispositionProceeds(10_000, 1_000);

        Assert.Equal(7, session.Town.Buildings.Count);
        Assert.True(session.TryUpgradeTownBuilding(P9BuildingKind.Storage, out _));
        Assert.Equal(100, session.World.Storage.Capacity);

        session.AdvanceTownOnly(143_999);
        Assert.Equal(1, session.Town.Level(P9BuildingKind.Storage));
        session.AdvanceTownOnly(1);

        Assert.Equal(2, session.Town.Level(P9BuildingKind.Storage));
        Assert.Equal(150, session.World.Storage.Capacity);
    }

    [Fact]
    public void TavernRetainsTwoLockedCandidatesAcrossDeterministicRefresh()
    {
        P1GameSession session = CreateSession();
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
        P1GameSession session = CreateSession();
        P9MercenaryMember member = session.Town.Roster[0];
        Assert.Equal(3, session.Town.ActiveMembers().Count);
        Assert.False(session.TryPlaceMercenary(member.Identity.StableId, 3));

        P1GameSession restored = P1GameSession.Restore(session.Capture());

        Assert.Equal(session.Town.Formation, restored.Town.Formation);
        Assert.Equal(3, restored.World.Mercenaries.Build.PartySize);
        Assert.Equal(member.Equipment.Items.Keys, restored.Town.Roster[0].Equipment.Items.Keys);
    }

    [Fact]
    public void CartographyIdsRemainUniqueAcrossSaveRestore()
    {
        P1GameSession session = CreateSession();
        var maps = new List<string>();
        session.Town.Advance(3_600_000, session.World.Economy, map => maps.Add(map.InstanceId));
        P9TownState restored = P9TownState.Restore(
            session.Town.Capture(), session.Seed ^ 0x7039746f776eUL, session.MercenaryEquipment);
        restored.Advance(1_800_000, session.World.Economy, map => maps.Add(map.InstanceId));

        Assert.Equal(3, maps.Count);
        Assert.Equal(maps.Count, maps.Distinct().Count());
    }

    private static P1GameSession CreateSession() => P1GameSession.CreateNew(new PlayerIdentity(
        "铸城者", CharacterGender.Androgynous, CharacterSkinTone.Umber,
        CharacterHairStyle.Braided, P1Ascendancy.IronOath), 0x9090, tutorialEnabled: false);
}
