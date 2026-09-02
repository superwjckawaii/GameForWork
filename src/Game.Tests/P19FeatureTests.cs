using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P19;

namespace GameForWork.Tests;

public sealed class P19FeatureTests
{
    [Fact]
    public void SnapshotHasSealedBaseRoutesAffixDataAndHashes()
    {
        Assert.Equal(80, P19Catalog.Bases.Count);
        Assert.Equal(12, P19Catalog.Bases.Count(item => item.Category == ItemCategory.TwoHandWeapon));
        Assert.Equal(8, P19Catalog.Bases.Count(item => item.Category == ItemCategory.OneHandWeapon));
        Assert.Equal(8, P19Catalog.Bases.Count(item => item.Category == ItemCategory.Shield));
        Assert.Contains(P19Catalog.Bases, item => item.RequiredEnergy > 0);
        Assert.InRange(P19Catalog.Affixes.Count, 300, 450);
        Assert.Equal("PathOfBuildingCommunity-Portable", P19Catalog.Source.SourceRoot);
        Assert.All(P19Catalog.Source.Files, file => Assert.Matches("^[0-9a-f]{64}$", file.Sha256));
        Assert.All(P19Catalog.Bases, item =>
        {
            Assert.InRange(item.RequiredLevel, 1, 100);
            Assert.False(string.IsNullOrWhiteSpace(item.SourceId));
            Assert.NotEmpty(item.ItemTags);
        });
    }

    [Fact]
    public void VoidMappingAndEnergyMirrorAreComplete()
    {
        Assert.DoesNotContain(P19Catalog.Affixes, affix => affix.RawText.Contains("Chaos", StringComparison.Ordinal));
        Assert.Contains(P19Catalog.Affixes, affix => affix.ModifierKind == ItemModifierKind.VoidResistanceBasisPoints);
        AffixDefinition[] spirit = P19Catalog.Affixes.Where(affix => affix.ModifierKind == ItemModifierKind.Spirit)
            .OrderBy(affix => affix.MinimumItemLevel).ToArray();
        AffixDefinition[] energy = P19Catalog.Affixes.Where(affix => affix.ModifierKind == ItemModifierKind.Energy)
            .OrderBy(affix => affix.MinimumItemLevel).ToArray();
        Assert.Equal(spirit.Length, energy.Length);
        Assert.Equal(spirit.Select(ValueSignature), energy.Select(ValueSignature));
    }

    [Fact]
    public void RequirementsAndImportedStatsReachCharacterCalculation()
    {
        ItemBaseDefinition high = P19Catalog.Bases.OrderByDescending(item => item.RequiredLevel).First();
        Assert.False(high.MeetsRequirements(1, CharacterAttributes.IronOathStarting));
        Assert.True(high.MeetsRequirements(120, new CharacterAttributes(1_000, 1_000, 1_000, 1_000)));

        AffixDefinition dexterity = P19Catalog.Affixes.First(affix => affix.ModifierKind == ItemModifierKind.Dexterity);
        AffixDefinition energy = P19Catalog.Affixes.First(affix => affix.ModifierKind == ItemModifierKind.Energy);
        var item = new ItemInstance("p19-calc", P1ItemBases.Get("core.base.iron_ring"), 100, ItemRarity.Rare,
            [new AffixRoll(dexterity, dexterity.MaximumValue), new AffixRoll(energy, energy.MaximumValue)]);
        var loadout = new EquipmentLoadout();
        Assert.True(loadout.TryEquip(EquipmentSlot.RingLeft, item));
        EquipmentModifiers modifiers = loadout.CalculateSummary().Modifiers;
        Assert.Equal(dexterity.MaximumValue, modifiers.Dexterity);
        Assert.Equal(energy.MaximumValue, modifiers.Energy);
    }

    [Fact]
    public void RingTopPhysiqueTierUsesFormalRangeAndLegacyRollIsNormalized()
    {
        ItemBaseDefinition ring = P1ItemBases.Get("core.base.iron_ring");
        AffixDefinition top = P1Affixes.For(ring, 120)
            .Where(affix => affix.ModifierKind == ItemModifierKind.Physique)
            .OrderBy(affix => P1Affixes.TierFor(ring, affix))
            .First();
        Assert.Equal(1, P1Affixes.TierFor(ring, top));
        Assert.InRange(top.MinimumValue, 40, 60);
        Assert.InRange(top.MaximumValue, 40, 60);
        Assert.DoesNotContain(P1Affixes.All, affix =>
            affix.StableFamilyId == "core.affix.ring.physique");

        var legacy = new AffixDefinition("core.affix.ring.physique", "体魄", ItemCategory.Ring,
            AffixPosition.Suffix, 1, 6, 2, 3, 300, ItemModifierKind.Physique);
        var legacyRoll = new AffixRoll(legacy, 3);
        Assert.Equal(55, legacyRoll.EffectiveValue);
        Assert.Equal(51, legacyRoll.EffectiveMinimumValue);
        Assert.Equal(55, legacyRoll.EffectiveMaximumValue);
    }

    [Fact]
    public void AffixBrowserUsesLiveP30CatalogWithoutTruncationAndSupportsExactBases()
    {
        IReadOnlyList<P19AffixView> all = P19AffixBrowser.Query(new());
        Assert.Equal(P1Affixes.All.Count(affix => affix.SourceId.Length > 0), all.Count);
        Assert.Contains(all, row => row.Definition.Source == "P30" && row.Definition.StableFamilyId == "p30.affix.attack.damage");

        ItemBaseDefinition sword = P1ItemBases.Get("p19.base.ezomyte_blade");
        IReadOnlyList<P19AffixView> swordRows = P19AffixBrowser.Query(new(BaseStableId: sword.StableId));
        Assert.NotEmpty(swordRows);
        Assert.All(swordRows, row => Assert.True(row.Definition.Supports(sword)));
        Assert.All(swordRows, row => Assert.Equal(P1Affixes.TierFor(sword, row.Definition), row.Tier));
        Assert.All(swordRows, row => Assert.Equal(row.Definition.WeightFor(sword), row.Weight));
    }

    [Fact]
    public void OneHundredThousandGeneratedItemsAreLegal()
    {
        ItemBaseDefinition[] bases = P19Catalog.Bases.Select(item => P1ItemBases.Get(item.StableId)).ToArray();
        for (int index = 0; index < 100_000; index++)
        {
            ItemBaseDefinition itemBase = bases[index % bases.Length];
            int itemLevel = 1 + index % 120;
            ItemRarity rarity = itemBase.Category == ItemCategory.LifeFlask
                ? ItemRarity.Basic
                : index % 3 == 0 ? ItemRarity.Magic : ItemRarity.Rare;
            ItemInstance item = ItemGenerator.Generate(itemBase.StableId, itemLevel, rarity, (ulong)index + 1);
            int minimum = rarity == ItemRarity.Basic ? 0 : rarity == ItemRarity.Magic ? 1 : 4;
            int maximum = rarity == ItemRarity.Basic ? 0 : rarity == ItemRarity.Magic ? 2 : 6;
            if (item.Affixes.Count < minimum || item.Affixes.Count > maximum || item.PrefixCount > (rarity == ItemRarity.Magic ? 1 : 3) ||
                item.SuffixCount > (rarity == ItemRarity.Magic ? 1 : 3) ||
                item.Affixes.Any(affix => affix.Definition.MinimumItemLevel > itemLevel || !affix.Definition.Supports(itemBase)) ||
                item.Affixes.Select(affix => affix.Definition.StableFamilyId).Distinct(StringComparer.Ordinal).Count() != item.Affixes.Count ||
                item.Affixes.Select(affix => affix.Definition.MutualExclusionGroup).Distinct(StringComparer.Ordinal).Count() != item.Affixes.Count)
            {
                throw new Xunit.Sdk.XunitException(
                    $"Illegal P19 item at audit index {index}: {itemBase.StableId} ilvl {itemLevel}, " +
                    $"rarity {rarity}, affixes {item.Affixes.Count} [{string.Join(',', item.Affixes.Select(affix => affix.Definition.SourceId))}].");
            }
        }
    }

    private static string ValueSignature(AffixDefinition affix) =>
        $"{affix.MinimumItemLevel}:{affix.MinimumValue}:{affix.MaximumValue}:{affix.Weight}:{affix.Tier}";
}
