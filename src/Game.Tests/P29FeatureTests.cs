using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P1.World;
using GameForWork.Core.P2;
using GameForWork.Core.P14;
using GameForWork.Core.P20;
using GameForWork.Core.P28;
using GameForWork.Core.P29;
using GameForWork.Core.Simulation;

namespace GameForWork.Tests;

public sealed class P29FeatureTests
{
    [Fact]
    public void WarfrontHasSixRealBasesPerTierAndDeterministicNonRepeatingSupply()
    {
        Assert.Equal(18, P29WarfrontBases.All.Count);
        foreach (int tier in Enumerable.Range(1, 3))
        {
            ItemBaseDefinition[] pool = P29WarfrontBases.ForTier(tier).ToArray();
            Assert.Equal(6, pool.Length);
            Assert.Equal(2, pool.Count(item => item.Category == ItemCategory.Ring));
            Assert.Equal(2, pool.Count(item => item.Category == ItemCategory.Amulet));
            Assert.Equal(2, pool.Count(item => item.Category == ItemCategory.Belt));
            Assert.All(pool, item => Assert.NotEqual(ItemModifierKind.None, item.ImplicitModifier));
            ItemInstance first = P29WarfrontRewards.Create(tier, 77, string.Empty, $"war-{tier}-1");
            ItemInstance replay = P29WarfrontRewards.Create(tier, 77, string.Empty, $"war-{tier}-1");
            ItemInstance second = P29WarfrontRewards.Create(tier, 77, first.Base.StableId, $"war-{tier}-2");
            Assert.Equal(first.Base.StableId, replay.Base.StableId);
            Assert.Equal(first.Affixes.Select(affix => (affix.Definition.StableFamilyId, affix.Definition.Tier, affix.Value)),
                replay.Affixes.Select(affix => (affix.Definition.StableFamilyId, affix.Definition.Tier, affix.Value)));
            Assert.NotEqual(first.Base.StableId, second.Base.StableId);
            Assert.True(first.Affixes.Count >= (tier == 1 ? 4 : tier == 2 ? 5 : 6));
            if (tier >= 2) Assert.True(first.Affixes.Min(affix => P1Affixes.TierFor(first.Base, affix.Definition)) <= (tier == 2 ? 3 : 2));
        }
    }

    [Fact]
    public void LegendaryAndMechanicPoolsIncludeP30VirtueViceItems()
    {
        Assert.Equal(40, P14UniqueItems.All.Count(item => !item.Mythic));
        foreach (string pool in new[] { "warden", "citadel", "abyss", "garden", "red", "blue", "warfront" })
        {
            IReadOnlyList<P14UniqueDefinition> items = P20LegendaryDrops.Pool(pool);
            Assert.Equal(4, items.Count);
            Assert.All(items, item => Assert.True(GameForWork.Core.P25.P25LegendaryRules.HasImplementation(item.StableId)));
        }
        Assert.Equal(12, P20LegendaryDrops.Pool("common").Count);
    }

    [Fact]
    public void SkillStoneDropsNeverExceedFivePerDefinitionAndMutationState()
    {
        P2ManagementState state = P2ManagementState.CreateNew();
        IReadOnlySet<string> themed = P29SkillDropCatalog.For(P28Mechanic.Abyss);
        int initial = state.SkillStones.Count;
        for (ulong seed = 1; seed <= 2_000; seed++) state.AddDroppedSkillStone(seed, false, preferredDefinitions: themed);
        Assert.All(state.SkillStones.GroupBy(stone => (stone.DefinitionId, stone.Mutated)), group => Assert.InRange(group.Count(), 1, 5));
        Assert.True(state.SkillStones.Count > initial);
        Assert.Equal(10, themed.Count);
    }

    [Fact]
    public void LifeForceReplacementRedTierShiftAndBlueQualityAreDeterministic()
    {
        ItemInstance item = Enumerable.Range(1, 100).Select(seed => ItemGenerator.Generate("core.base.iron_ring", 100, ItemRarity.Rare, (ulong)seed, $"craft-{seed}"))
            .First(candidate => candidate.Affixes.Count >= 4 && P14GardenCrafting.CanApply(candidate, P14GardenCraft.ReplaceCritical));
        ItemInstance replaced = P14GardenCrafting.Apply(item, P14GardenCraft.ReplaceCritical, 9);
        Assert.Equal(item.Affixes.Count, replaced.Affixes.Count);
        Assert.Contains(replaced.Affixes, affix => P14GardenCrafting.Tagged(affix.Definition, P14GardenCraft.BiasCritical));

        AffixRoll shiftable = item.Affixes.First(affix => !affix.Crafted && P1Affixes.For(item.Base, item.ItemLevel)
            .Count(definition => definition.StableFamilyId == affix.Definition.StableFamilyId) >= 2);
        P29ResourceCraftResult red = P29ResourceCrafting.ShiftAffixTier(item, shiftable.Definition.StableFamilyId, 11);
        Assert.True(red.Succeeded);
        P29ResourceCraftResult blue = P29ResourceCrafting.RerollQuality(item, 12);
        Assert.True(blue.Succeeded);
        Assert.InRange(blue.Result!.Quality, 0, 40);
        Assert.Equal(blue, P29ResourceCrafting.RerollQuality(item, 12));
    }

    [Fact]
    public void SourceAndBaseTierFiltersUsePersistedDropMetadata()
    {
        ItemInstance item = ItemGenerator.Generate("core.base.iron_ring", 100, ItemRarity.Rare, 13, "filtered") with
        { DropSource = "p29.source.RedOath.Rare" };
        var rule = new LootFilterRule("p29.filter", LootDisposition.Keep,
            BaseTier: P29DropCatalog.BaseTier(item.Base), DropSource: "RedOath");
        Assert.True(rule.Matches(item));
        Assert.False((rule with { DropSource = "BlueOath" }).Matches(item));
    }

    [Fact]
    public void SaveOneFilterSellsLowRarityAndNonPinnacleDropsEvenWhenTheyHaveManyLinks()
    {
        var filter = new LootFilter([
            new LootFilterRule("save1.keep.pinnacle.rare", LootDisposition.Keep,
                Rarity: ItemRarity.Rare, BaseTier: P29BaseTier.Pinnacle),
            new LootFilterRule("save1.sell.basic-through-rare", LootDisposition.Sell,
                MinimumRarity: ItemRarity.Basic, MaximumRarity: ItemRarity.Rare),
        ]);
        ItemInstance sixLinkMagic = ItemGenerator.Generate(
            "core.base.rusted_greatsword", 100, ItemRarity.Magic, 0x2911, "save1-six-link") with
        { LinkedSocketCount = 6 };
        ItemInstance ordinaryRare = ItemGenerator.Generate(
            "core.base.iron_ring", 100, ItemRarity.Rare, 0x2912, "save1-rare");
        ItemInstance pinnacleRare = new("save1-pinnacle", P29WarfrontBases.ForTier(3).First(), 100,
            ItemRarity.Rare, [], LinkedSocketCount: 0);

        Assert.Equal(LootDisposition.Sell, filter.Evaluate(sixLinkMagic));
        Assert.Equal(LootDisposition.Sell, filter.Evaluate(ordinaryRare));
        Assert.Equal(LootDisposition.Keep, filter.Evaluate(pinnacleRare));
        Assert.Equal(LootDisposition.Keep, filter.Evaluate(sixLinkMagic with { IsLocked = true }));
    }

    [Fact]
    public void P29AuditCoversEveryBracketAndSustainTargets()
    {
        IReadOnlyList<P20AuditResult> results = P20EconomyAudit.Run(10_000, 0x29ec0a11UL);
        P20EconomyAudit.ValidateSustain(results);
        Assert.Contains(results, result => result.Bracket.Name == "T6");
        Assert.Contains(results, result => result.Bracket.Name == "T11");
        Assert.Equal(15, results.Count(result => result.Bracket.Name.Contains('高') || result.Bracket.Name.Contains("普通") || result.Bracket.Name.Contains("极限")));
    }
}
