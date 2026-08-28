using GameForWork.Core.P1;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P6;
using GameForWork.Core.P2;

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

    private static P1GameSession CreateSession() => P1GameSession.CreateNew(new PlayerIdentity(
        "孔铸者", CharacterGender.Androgynous, CharacterSkinTone.Umber,
        CharacterHairStyle.Braided, P1Ascendancy.IronOath), 0x6060);
}
