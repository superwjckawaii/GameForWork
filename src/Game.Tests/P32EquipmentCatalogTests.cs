using GameForWork.Core.Equipment;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P30;

namespace GameForWork.Tests;

public sealed class P32EquipmentCatalogTests
{
    [Fact]
    public void FormalSnapshotHasEverySealedCatalogAndPermanentUniqueIds()
    {
        Assert.Equal(1, EquipmentCatalog.Snapshot.SchemaVersion);
        Assert.Equal(244, EquipmentCatalog.Bases.Count);
        Assert.Equal(212, EquipmentCatalog.Affixes.Select(value => value.StableFamilyId).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(41, EquipmentCatalog.Enchantments.Count);
        Assert.Equal(55, EquipmentCatalog.LegendaryItems.Count);
        Assert.Equal(50, EquipmentCatalog.LegendaryItems.Count(value => value.Rarity == "Legendary"));
        Assert.Equal(5, EquipmentCatalog.LegendaryItems.Count(value => value.Rarity == "Mythic"));
        Assert.Equal(92, EquipmentCatalog.CraftingOperations.Count);
        Assert.Equal(37, EquipmentCatalog.CorruptionImplicits.Count);
        Assert.All(EquipmentCatalog.Bases, value => Assert.StartsWith("equipment.base.", value.StableId, StringComparison.Ordinal));
        Assert.All(EquipmentCatalog.Affixes, value => Assert.StartsWith("equipment.affix.", value.StableFamilyId, StringComparison.Ordinal));
    }

    [Fact]
    public void EveryBaseGeneratesSavedLocalRollsAndRebindsLegacyIds()
    {
        foreach ((ItemBaseDefinition itemBase, int index) in EquipmentCatalog.Bases.Select((value, index) => (value, index)))
        {
            ItemInstance item = ItemGenerator.Generate(itemBase.StableId, 100, ItemRarity.Basic, (ulong)index + 32);
            Assert.Same(itemBase, item.Base);
            Assert.InRange(item.RolledBaseArmor, itemBase.ArmorMinimum, itemBase.ArmorMaximum == 0 ? 0 : itemBase.ArmorMaximum);
            Assert.InRange(item.RolledBaseEvasion, itemBase.EvasionMinimum, itemBase.EvasionMaximum == 0 ? 0 : itemBase.EvasionMaximum);
            Assert.InRange(item.RolledBaseShield, itemBase.ShieldMinimum, itemBase.ShieldMaximum == 0 ? 0 : itemBase.ShieldMaximum);
            Assert.NotEmpty(item.EffectiveImplicitComponents);
        }

        Assert.Equal("equipment.base.heavy_battleaxe", EquipmentCatalog.GetBase("core.base.heavy_battleaxe").StableId);
        ItemBaseDefinition barrierFocus = EquipmentCatalog.Bases.Single(value => value.DisplayName == "灵障法器");
        ItemInstance barrier = ItemGenerator.Generate(barrierFocus.StableId, 100, ItemRarity.Basic, 0x1234);
        Assert.InRange(barrier.RolledBaseSpiritBarrier, 600, 850);
    }

    [Fact]
    public void ConfirmedEnchantmentsAndLegendaryRulesUseOneRegistry()
    {
        Assert.Equal(41, EquipmentEnchantmentCatalog.All.Count);
        Assert.Equal(96, EquipmentRuleRegistry.All.Count);
        Assert.Equal(400, EquipmentEnchantmentCatalog.All.Single(value => value.DisplayName == "精准刻印").Value);
        Assert.Equal(6_500, EquipmentEnchantmentCatalog.All.Single(value => value.DisplayName == "毁伤铭文").Value);
        Assert.Contains(EquipmentEnchantmentCatalog.All.Single(value => value.DisplayName == "虹彩王印").EffectComponents,
            value => value.Kind == ItemModifierKind.MaximumVoidResistanceBonusBasisPoints && value.MinimumValue == 500);

        EquipmentEnchantmentEntry returning = EquipmentCatalog.Enchantments.Single(value => value.DisplayName == "归返王印");
        EquipmentRuleRegistration registration = EquipmentRuleRegistry.Get(returning.RuleId);
        Assert.Equal(EquipmentRuleEvent.ProjectileFinished, registration.Trigger);
        Assert.DoesNotContain("降低", returning.RuleText, StringComparison.Ordinal);
        Assert.Equal((150, 300), EquipmentRuleEngine.MythicDaggerAddedVoidDamage(399));
        Assert.Equal((0, 0), EquipmentRuleEngine.MythicDaggerAddedVoidDamage(99));
    }

    [Fact]
    public void ArcaneSealCopiesAttackElementalRangeWithoutCrossingDamageSets()
    {
        var copied = EquipmentRuleEngine.CopyHighestElementalRange((0, 0), (0, 0), (150, 250));
        Assert.Equal((150, 250, 150, 250, 150, 250), copied);
        Assert.Equal(600, EquipmentRuleEngine.ImmortalMaximumLife(3_499));
        Assert.Equal(3_000, EquipmentRuleEngine.UnarmedMoreDamageBasisPoints(345));
    }

    [Fact]
    public void AtomicCraftingHidesRandomResultAndConsumesPrefixProtectionOnlyOnSuccess()
    {
        ItemInstance rare = Enumerable.Range(0, 200)
            .Select(seed => ItemGenerator.Generate("core.base.life_ring", 100, ItemRarity.Rare, (ulong)seed + 1, "p32-craft"))
            .First(item => item.PrefixCount > 0 && item.SuffixCount > 0);
        EquipmentCraftingOperationEntry protect = EquipmentCatalog.CraftingOperations.Single(value => value.DisplayName == "赤誓保护");
        var wallet = new EquipmentCraftingWallet();
        EquipmentCraftingPreview protectionPreview = EquipmentCraftingService.Preview(rare, new(protect.Id));
        wallet.Credit(protectionPreview.Resource, protectionPreview.Cost);
        EquipmentCraftingResult protectedResult = EquipmentCraftingService.Execute(wallet, rare, new(protect.Id, Seed: 7));
        Assert.True(protectedResult.Succeeded);
        Assert.True(protectedResult.Item!.ProtectPrefixesNextCraft);

        EquipmentCraftingOperationEntry chaos = EquipmentCatalog.CraftingOperations.Single(value => value.DisplayName == "混沌重铸");
        EquipmentCraftingPreview preview = EquipmentCraftingService.Preview(protectedResult.Item, new(chaos.Id, Seed: 8));
        Assert.DoesNotContain(preview.PossibleOutcomes, value => value.Contains("T1", StringComparison.Ordinal));
        wallet.Credit(preview.Resource, preview.Cost);
        AffixRoll[] oldPrefixes = protectedResult.Item.Affixes.Where(value => value.Definition.Position == AffixPosition.Prefix).ToArray();
        EquipmentCraftingResult crafted = EquipmentCraftingService.Execute(wallet, protectedResult.Item, new(chaos.Id, Seed: 8));
        Assert.True(crafted.Succeeded);
        Assert.False(crafted.Item!.ProtectPrefixesNextCraft);
        Assert.Equal(oldPrefixes.Select(Signature), crafted.Item.Affixes.Where(value => value.Definition.Position == AffixPosition.Prefix).Select(Signature));
    }

    [Fact]
    public void SkillCurvesExtrapolateToFortyAndClampThere()
    {
        int atTwentyOne = P30SkillCatalog.Interpolate(100, 300, 21, false);
        int atForty = P30SkillCatalog.Interpolate(100, 300, 40, false);
        Assert.True(atForty > atTwentyOne);
        Assert.Equal(atForty, P30SkillCatalog.Interpolate(100, 300, 41, false));
        Assert.True(P30SkillCatalog.Interpolate(100, 300, 40, true) > P30SkillCatalog.Interpolate(100, 300, 21, true));
    }

    private static string Signature(AffixRoll value) => $"{value.Definition.StableFamilyId}:{value.Value}:{string.Join(',', value.Effects.Select(effect => effect.Value))}";
}
