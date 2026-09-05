using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Management;
using GameForWork.Core.Content;
using GameForWork.Core.Economy;
using GameForWork.Core.Encounters;
using GameForWork.Core.Resources;
using GameForWork.Core.Endgame;
using GameForWork.Core.Expeditions;
using GameForWork.Core.Simulation;
using GameForWork.Core.Equipment;

namespace GameForWork.Tests;

public sealed class ResourcesFeatureTests
{
    [Fact]
    public void AshenCitadelMapRoutesToCitadelLegendaryPoolBeforeGenericBossRouting()
    {
        var map = new MapItem($"{EndgameState.CitadelMapPrefix}regression", 16);

        Assert.True(ExpeditionDirector.IsBoss(map));
        Assert.Equal("citadel", LegendaryDrops.BossPool(map));
        Assert.Contains(LegendaryDrops.Pool("citadel"), item => item.StableId == "equipment.legendary.52.44a586da1f");
    }

    [Fact]
    public void WarfrontHasSixRealBasesPerTierAndDeterministicNonRepeatingSupply()
    {
        Assert.Equal(18, WarfrontBases.All.Count);
        foreach (int tier in Enumerable.Range(1, 3))
        {
            ItemBaseDefinition[] pool = WarfrontBases.ForTier(tier).ToArray();
            Assert.Equal(6, pool.Length);
            Assert.Equal(2, pool.Count(item => item.Category == ItemCategory.Ring));
            Assert.Equal(2, pool.Count(item => item.Category == ItemCategory.Amulet));
            Assert.Equal(2, pool.Count(item => item.Category == ItemCategory.Belt));
            Assert.All(pool, item => Assert.NotEqual(ItemModifierKind.None, item.ImplicitModifier));
            ItemInstance first = WarfrontRewards.Create(tier, 77, string.Empty, $"war-{tier}-1");
            ItemInstance replay = WarfrontRewards.Create(tier, 77, string.Empty, $"war-{tier}-1");
            ItemInstance second = WarfrontRewards.Create(tier, 77, first.Base.StableId, $"war-{tier}-2");
            Assert.Equal(first.Base.StableId, replay.Base.StableId);
            Assert.Equal(first.Affixes.Select(affix => (affix.Definition.StableFamilyId, affix.Definition.Tier, affix.Value)),
                replay.Affixes.Select(affix => (affix.Definition.StableFamilyId, affix.Definition.Tier, affix.Value)));
            Assert.NotEqual(first.Base.StableId, second.Base.StableId);
            Assert.True(first.Affixes.Count >= (tier == 1 ? 4 : tier == 2 ? 5 : 6));
            if (tier >= 2) Assert.True(first.Affixes.Min(affix => Affixes.TierFor(first.Base, affix.Definition)) <= (tier == 2 ? 3 : 2));
        }
    }

    [Fact]
    public void LegendaryAndMechanicPoolsIncludeBuildsVirtueViceItems()
    {
        Assert.Equal(50, UniqueItems.All.Count(item => !item.Mythic));
        foreach (string pool in new[] { "warden", "citadel", "abyss", "garden", "red", "blue", "warfront" })
        {
            IReadOnlyList<UniqueDefinition> items = LegendaryDrops.Pool(pool);
            Assert.Equal(pool == "citadel" ? 9 : 4, items.Count);
            Assert.All(items, item =>
            {
                EquipmentLegendaryEntry entry = EquipmentCatalog.LegendaryItems.Single(value => value.DisplayName == item.DisplayName);
                Assert.Equal(entry.Id, EquipmentRuleRegistry.Get(entry.RuleId).SourceDefinitionId);
            });
        }
        Assert.Equal(UniqueItems.All.Where(item => item.Mythic).Select(item => item.StableId).Order(),
            LegendaryDrops.Pool("citadel").Where(item => item.Mythic).Select(item => item.StableId).Order());
        Assert.DoesNotContain(new[] { "warden", "abyss", "garden", "red", "blue", "warfront" }
            .SelectMany(LegendaryDrops.Pool), item => item.Mythic);
        Assert.Equal(12, LegendaryDrops.Pool("common").Count);
    }

    [Fact]
    public void SkillStoneDropsNeverExceedFivePerDefinitionAndMutationState()
    {
        ManagementState state = ManagementState.CreateNew();
        IReadOnlySet<string> themed = SkillDropCatalog.For(Mechanic.Abyss);
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
            .First(candidate => candidate.Affixes.Count >= 4 && GardenCrafting.CanApply(candidate, GardenCraft.ReplaceCritical));
        ItemInstance replaced = GardenCrafting.Apply(item, GardenCraft.ReplaceCritical, 9);
        Assert.Equal(item.Affixes.Count, replaced.Affixes.Count);
        Assert.Contains(replaced.Affixes, affix => GardenCrafting.Tagged(affix.Definition, GardenCraft.BiasCritical));

        AffixRoll shiftable = item.Affixes.First(affix => !affix.Crafted && Affixes.For(item.Base, item.ItemLevel)
            .Count(definition => definition.StableFamilyId == affix.Definition.StableFamilyId) >= 2);
        ResourceCraftResult red = ResourceCrafting.ShiftAffixTier(item, shiftable.Definition.StableFamilyId, 11);
        Assert.True(red.Succeeded);
        ResourceCraftResult blue = ResourceCrafting.RerollQuality(item, 12);
        Assert.True(blue.Succeeded);
        Assert.InRange(blue.Result!.Quality, 20, 40);
        Assert.Equal(blue, ResourceCrafting.RerollQuality(item, 12));
    }

    [Fact]
    public void SourceAndBaseTierFiltersUsePersistedDropMetadata()
    {
        ItemInstance item = ItemGenerator.Generate("core.base.iron_ring", 100, ItemRarity.Rare, 13, "filtered") with
        { DropSource = "resources.source.RedOath.Rare" };
        var rule = new LootFilterRule("resources.filter", LootDisposition.Keep,
            BaseTier: DropCatalog.ResolveBaseTier(item.Base), DropSource: "RedOath");
        Assert.True(rule.Matches(item));
        Assert.False((rule with { DropSource = "BlueOath" }).Matches(item));
    }

    [Fact]
    public void SaveOnePinnacleTwoHandRuleHasRealSwordCandidates()
    {
        ItemBaseDefinition[] pinnacleSwords = ItemBases.All
            .Where(item => item.Category == ItemCategory.TwoHandWeapon && item.ItemTags.Contains("sword", StringComparer.Ordinal))
            .Where(item => DropCatalog.ResolveBaseTier(item) == BaseTier.Pinnacle)
            .ToArray();
        Assert.NotEmpty(pinnacleSwords);
        Assert.Contains(pinnacleSwords, item => item.StableId == "equipment.base.ezomyte_blade");

        var filter = new LootFilter([
            new LootFilterRule("save1.keep.pinnacle.twohand", LootDisposition.Keep,
                MinimumRarity: ItemRarity.Basic, MaximumRarity: ItemRarity.Legendary,
                Category: ItemCategory.TwoHandWeapon, BaseTier: BaseTier.Pinnacle),
            new LootFilterRule("save1.keep.high.twohand", LootDisposition.Keep,
                Category: ItemCategory.TwoHandWeapon, BaseTier: BaseTier.High),
            new LootFilterRule("save1.sell.rest", LootDisposition.Sell,
                MinimumRarity: ItemRarity.Basic, MaximumRarity: ItemRarity.Legendary),
        ]);
        ItemInstance wanted = ItemGenerator.Generate(pinnacleSwords[0].StableId, 100, ItemRarity.Basic, 0x2913, "save1-wanted");
        Assert.Equal(LootDisposition.Keep, filter.Evaluate(wanted));

        var random = new Pcg32(0x2914);
        SourceProfile source = DropCatalog.Source(EnemyFamily.RiftBeast, EnemyRarity.Rare);
        ItemBaseDefinition[] sampled = Enumerable.Range(0, 10_000)
            .Select(_ => DropFormula.PickBase(100, source, random)).ToArray();
        Assert.Contains(sampled, item => item.Category == ItemCategory.TwoHandWeapon &&
            item.ItemTags.Contains("sword", StringComparer.Ordinal) &&
            DropCatalog.ResolveBaseTier(item) == BaseTier.Pinnacle);
    }

    [Fact]
    public void SaveOneFilterSellsLowRarityAndNonPinnacleDropsEvenWhenTheyHaveManyLinks()
    {
        var filter = new LootFilter([
            new LootFilterRule("save1.keep.pinnacle.rare", LootDisposition.Keep,
                Rarity: ItemRarity.Rare, BaseTier: BaseTier.Pinnacle),
            new LootFilterRule("save1.sell.basic-through-rare", LootDisposition.Sell,
                MinimumRarity: ItemRarity.Basic, MaximumRarity: ItemRarity.Rare),
        ]);
        ItemInstance sixLinkMagic = ItemGenerator.Generate(
            "core.base.rusted_greatsword", 100, ItemRarity.Magic, 0x2911, "save1-six-link") with
        { LinkedSocketCount = 6 };
        ItemInstance ordinaryRare = ItemGenerator.Generate(
            "core.base.iron_ring", 100, ItemRarity.Rare, 0x2912, "save1-rare");
        ItemInstance pinnacleRare = new("save1-pinnacle", WarfrontBases.ForTier(3).First(), 100,
            ItemRarity.Rare, [], LinkedSocketCount: 0);

        Assert.Equal(LootDisposition.Sell, filter.Evaluate(sixLinkMagic));
        Assert.Equal(LootDisposition.Sell, filter.Evaluate(ordinaryRare));
        Assert.Equal(LootDisposition.Keep, filter.Evaluate(pinnacleRare));
        Assert.Equal(LootDisposition.Keep, filter.Evaluate(sixLinkMagic with { IsLocked = true }));
    }

    [Fact]
    public void ResourcesAuditCoversEveryBracketAndSustainTargets()
    {
        IReadOnlyList<AuditResult> results = EconomyAudit.Run(10_000, 0x29ec0a11UL);
        EconomyAudit.ValidateSustain(results);
        Assert.Contains(results, result => result.Bracket.Name == "T6");
        Assert.Contains(results, result => result.Bracket.Name == "T11");
        Assert.Equal(15, results.Count(result => result.Bracket.Name.Contains('高') || result.Bracket.Name.Contains("普通") || result.Bracket.Name.Contains("极限")));
    }
}
