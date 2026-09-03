using GameForWork.Core.Equipment;
using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.Items;

namespace GameForWork.Tests;

public sealed class P19FeatureTests
{
    [Fact]
    public void ImportedEquipmentNowLivesOnlyInTheFormalCatalog()
    {
        Assert.Equal(244, EquipmentCatalog.Bases.Count);
        Assert.Equal(212, EquipmentCatalog.Affixes.Select(value => value.StableFamilyId).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(EquipmentCatalog.Bases, item => item.RequiredEnergy > 0);
        Assert.All(EquipmentCatalog.Bases, item =>
        {
            Assert.InRange(item.RequiredLevel, 1, 100);
            Assert.Equal("equipment.catalog", item.SourceId);
            Assert.NotEmpty(item.ItemTags);
        });
    }

    [Fact]
    public void VoidMappingAndEnergyMirrorAreComplete()
    {
        Assert.DoesNotContain(EquipmentCatalog.Affixes, affix => affix.RawText.Contains("Chaos", StringComparison.Ordinal));
        Assert.Contains(EquipmentCatalog.Affixes, affix => affix.ModifierKind == ItemModifierKind.VoidResistanceBasisPoints);
        AffixModifierComponent[] spirit = EquipmentCatalog.Affixes.SelectMany(affix => affix.EffectComponents)
            .Where(component => component.Kind == ItemModifierKind.Spirit).ToArray();
        AffixModifierComponent[] energy = EquipmentCatalog.Affixes.SelectMany(affix => affix.EffectComponents)
            .Where(component => component.Kind == ItemModifierKind.Energy).ToArray();
        Assert.NotEmpty(spirit);
        Assert.NotEmpty(energy);
        Assert.All(EquipmentCatalog.Affixes.Where(affix => affix.StableFamilyId == "equipment.affix.attributes.spirit_energy"), affix =>
        {
            Assert.Contains(affix.EffectComponents, component => component.Kind == ItemModifierKind.Spirit);
            Assert.Contains(affix.EffectComponents, component => component.Kind == ItemModifierKind.Energy);
        });
    }

    [Fact]
    public void RequirementsAndImportedStatsReachCharacterCalculation()
    {
        ItemBaseDefinition high = EquipmentCatalog.Bases.OrderByDescending(item => item.RequiredLevel).First();
        Assert.False(high.MeetsRequirements(1, CharacterAttributes.IronOathStarting));
        Assert.True(high.MeetsRequirements(120, new CharacterAttributes(1_000, 1_000, 1_000, 1_000)));

        AffixDefinition dexterity = EquipmentCatalog.Affixes.First(affix => affix.ModifierKind == ItemModifierKind.Dexterity);
        AffixDefinition energy = EquipmentCatalog.Affixes.First(affix => affix.ModifierKind == ItemModifierKind.Energy);
        var item = new ItemInstance("formal-calc", P1ItemBases.Get("core.base.iron_ring"), 100, ItemRarity.Rare,
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
            .OrderBy(affix => affix.Tier).First();
        Assert.Equal(1, top.Tier);
        Assert.InRange(top.MinimumValue, 40, 60);
        Assert.InRange(top.MaximumValue, 40, 60);

        var legacy = new AffixDefinition("core.affix.ring.physique", "体魄", ItemCategory.Ring,
            AffixPosition.Suffix, 1, 6, 2, 3, 300, ItemModifierKind.Physique);
        var legacyRoll = new AffixRoll(legacy, 3);
        Assert.Equal(55, legacyRoll.EffectiveValue);
        Assert.Equal(51, legacyRoll.EffectiveMinimumValue);
        Assert.Equal(55, legacyRoll.EffectiveMaximumValue);
    }

    [Fact]
    public void AffixBrowserUsesTheFormalCatalogWithoutTruncation()
    {
        IReadOnlyList<EquipmentAffixView> all = EquipmentAffixBrowser.Query(new());
        Assert.Equal(EquipmentCatalog.Affixes.Count, all.Count);
        Assert.Contains(all, row => row.Definition.StableFamilyId == "equipment.affix.attack.damage");

        ItemBaseDefinition sword = P1ItemBases.Get("p19.base.ezomyte_blade");
        IReadOnlyList<EquipmentAffixView> rows = EquipmentAffixBrowser.Query(new(BaseStableId: sword.StableId));
        Assert.NotEmpty(rows);
        Assert.All(rows, row => Assert.True(row.Definition.Supports(sword)));
        Assert.All(rows, row => Assert.Equal(row.Definition.WeightFor(sword), row.Weight));
    }

}
