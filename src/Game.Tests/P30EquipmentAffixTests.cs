using GameForWork.Core.P1.Items;
using GameForWork.Core.P9;
using GameForWork.Core.P6;
using GameForWork.Core.P14;
using GameForWork.Core.P19;
using GameForWork.Core.P24;
using GameForWork.Core.P30;

namespace GameForWork.Tests;

public sealed class P30EquipmentAffixTests
{
    [Fact]
    public void LegacyAndForbiddenNaturalFamiliesAreAbsent()
    {
        Assert.DoesNotContain(P1Affixes.All, affix => affix.StableFamilyId.StartsWith("core.affix.", StringComparison.Ordinal));
        Assert.DoesNotContain(P1Affixes.All, affix => affix.StableFamilyId.Contains("stunrecovery", StringComparison.Ordinal));
        Assert.DoesNotContain(P1Affixes.All, affix => affix.StableFamilyId == "p24.affix.rune.spellblade");
        Assert.DoesNotContain(P1Affixes.All, affix => affix.Source is "Natural" or "P30" &&
            affix.EffectComponents.Any(component => component.Kind == ItemModifierKind.ExtraSupportLinkCapacity));
    }

    [Fact]
    public void ImportedCompoundAffixesKeepEveryNumericComponent()
    {
        AffixDefinition oneHand = P19Catalog.Affixes.Single(affix =>
            affix.StableFamilyId == "p19.affix.localphysicaldamage" && affix.Tier == 1);
        Assert.Collection(oneHand.EffectComponents,
            low => { Assert.Equal(ItemModifierKind.AddedMinimumPhysicalDamage, low.Kind); Assert.Equal((22, 29), (low.MinimumValue, low.MaximumValue)); },
            high => { Assert.Equal(ItemModifierKind.AddedMaximumPhysicalDamage, high.Kind); Assert.Equal((45, 52), (high.MinimumValue, high.MaximumValue)); });

        AffixDefinition armourLife = P19Catalog.Affixes.Single(affix =>
            affix.StableFamilyId == "p19.affix.localbasearmourandlife" && affix.Tier == 1);
        Assert.Contains(armourLife.EffectComponents, component => component.Kind == ItemModifierKind.FlatArmor && component.MinimumValue == 97 && component.MaximumValue == 144);
        Assert.Contains(armourLife.EffectComponents, component => component.Kind == ItemModifierKind.FlatMaximumLife && component.MinimumValue == 34 && component.MaximumValue == 38);

        AffixDefinition regeneration = P19Catalog.Affixes.Single(affix =>
            affix.StableFamilyId == "p19.affix.liferegeneration" && affix.Tier == 1);
        Assert.Equal((180, 200), (regeneration.MinimumValue, regeneration.MaximumValue));
        Assert.Equal(ItemModifierKind.MaximumLifeRegenerationBasisPoints, regeneration.ModifierKind);

        AffixDefinition instantFlask = P19Catalog.Affixes.Single(affix =>
            affix.StableFamilyId == "p19.affix.flaskpartialinstantrecovery" && affix.Tier == 1);
        Assert.Collection(instantFlask.EffectComponents,
            amount => { Assert.Equal(ItemModifierKind.IncreasedFlaskRecoveryAmountBasisPoints, amount.Kind); Assert.True(amount.MaximumValue < 0); },
            speed => { Assert.Equal(ItemModifierKind.IncreasedFlaskRecoveryRateBasisPoints, speed.Kind); Assert.Equal(13_500, speed.MinimumValue); },
            instant => { Assert.Equal(ItemModifierKind.InstantFlaskRecoveryPortionBasisPoints, instant.Kind); Assert.Equal(5_000, instant.MinimumValue); });
    }

    [Fact]
    public void WeaponAndDefenceUseP30LocalOrder()
    {
        var weaponBase = new ItemBaseDefinition("test.weapon", "测试剑", ItemCategory.OneHandWeapon, EquipmentSlot.MainHand,
            MinimumPhysicalDamage: 100, MaximumPhysicalDamage: 200, AttacksPerSecondMilli: 1_000, CriticalChanceBasisPoints: 500,
            Tags: ["weapon", "sword", "one_hand_weapon"]);
        ItemInstance weapon = Item(weaponBase, [
            new(ItemModifierKind.AddedMinimumPhysicalDamage, 20, ItemModifierScope.LocalWeapon),
            new(ItemModifierKind.AddedMaximumPhysicalDamage, 40, ItemModifierScope.LocalWeapon),
            new(ItemModifierKind.IncreasedPhysicalDamageBasisPoints, 10_000, ItemModifierScope.LocalWeapon),
            new(ItemModifierKind.IncreasedAttackSpeedBasisPoints, 2_000, ItemModifierScope.LocalWeapon),
            new(ItemModifierKind.IncreasedCriticalChanceBasisPoints, 4_000, ItemModifierScope.LocalWeapon),
        ], quality: 20);
        var profile = EquipmentLoadout.CalculateWeapon(weapon);
        Assert.Equal((288, 576), (profile.MinimumPhysicalDamage, profile.MaximumPhysicalDamage));
        Assert.Equal(1_200, profile.AttacksPerSecondMilli);
        Assert.Equal(700, profile.CriticalChanceBasisPoints);

        var armourBase = new ItemBaseDefinition("test.armour", "测试盾", ItemCategory.Shield, EquipmentSlot.OffHand,
            Armor: 100, Evasion: 100, Shield: 100, Tags: ["shield"], BlockChanceBasisPoints: 2_500, SpiritBarrier: 100);
        ItemInstance armour = Item(armourBase, [
            new(ItemModifierKind.FlatArmor, 50, ItemModifierScope.LocalDefense),
            new(ItemModifierKind.FlatEvasion, 50, ItemModifierScope.LocalDefense),
            new(ItemModifierKind.FlatShield, 50, ItemModifierScope.LocalDefense),
            new(ItemModifierKind.FlatSpiritBarrier, 50, ItemModifierScope.LocalDefense),
            new(ItemModifierKind.IncreasedArmorBasisPoints, 10_000, ItemModifierScope.LocalDefense),
            new(ItemModifierKind.IncreasedEvasionBasisPoints, 10_000, ItemModifierScope.LocalDefense),
            new(ItemModifierKind.IncreasedShieldBasisPoints, 10_000, ItemModifierScope.LocalDefense),
            new(ItemModifierKind.IncreasedSpiritBarrierBasisPoints, 10_000, ItemModifierScope.LocalDefense),
            new(ItemModifierKind.IncreasedLocalBlockBasisPoints, 8_000, ItemModifierScope.LocalBlock),
        ], quality: 20);
        var defense = EquipmentLoadout.CalculateLocalDefense(armour);
        Assert.Equal((360, 360, 360, 360, 4_500), defense);
    }

    [Fact]
    public void P30OrdinaryTiersAndSpecialSourcesAreSealed()
    {
        AffixDefinition[] attack = P30EquipmentAffixes.Ordinary.Where(affix => affix.StableFamilyId == "p30.affix.attack.damage").ToArray();
        Assert.Equal(6, attack.Length);
        Assert.Equal([1, 20, 40, 60, 75, 85], attack.OrderByDescending(affix => affix.Tier).Select(affix => affix.MinimumItemLevel));
        AffixDefinition top = attack.Single(affix => affix.Tier == 1);
        Assert.Equal((3_500, 4_500), (top.MinimumValue, top.MaximumValue));

        Assert.Equal(49, P24ItemCatalog.Families.Count);
        Assert.All(P24ItemCatalog.Affixes, affix =>
        {
            Assert.Equal("P24Special", affix.Source);
            Assert.Equal(affix.ModifierKind, affix.EffectComponents[0].Kind);
            Assert.NotEmpty(affix.RequiredBaseTags!);
        });
    }

    [Fact]
    public void ConversionOverOneHundredPercentIsNormalizedWithoutReverseFlow()
    {
        RolledAffixComponent[] effects =
        [
            new(ItemModifierKind.PhysicalToFireConversionBasisPoints, 6_000),
            new(ItemModifierKind.PhysicalToColdConversionBasisPoints, 4_000),
            new(ItemModifierKind.PhysicalToVoidConversionBasisPoints, 3_000),
        ];
        P30ConversionAllocation result = P30ConversionRules.NormalizePhysical(effects);
        Assert.Equal(10_000, result.TotalBasisPoints);
        Assert.True(result.ToFireBasisPoints > result.ToColdBasisPoints);
        Assert.True(result.ToColdBasisPoints > result.ToVoidBasisPoints);
    }

    [Fact]
    public void DivineRerollPreservesCompoundShapeAndNaturalPoolCannotGrantVirtueVice()
    {
        ItemBaseDefinition itemBase = P1ItemBases.Get("p19.base.broad_sword");
        AffixDefinition definition = P19Catalog.Affixes.Single(affix =>
            affix.StableFamilyId == "p19.affix.localphysicaldamage" && affix.Tier == 1);
        ItemInstance item = new("compound-divine", itemBase, 100, ItemRarity.Rare,
            [new AffixRoll(definition, 22, Components:
            [
                new(ItemModifierKind.AddedMinimumPhysicalDamage, 22, ItemModifierScope.LocalWeapon),
                new(ItemModifierKind.AddedMaximumPhysicalDamage, 45, ItemModifierScope.LocalWeapon),
            ])]);
        P6CraftPreview preview = P6CraftingRules.Preview(item, P6CraftOperation.DivineReroll, seed: 30);
        Assert.True(preview.Succeeded);
        AffixRoll rerolled = Assert.Single(preview.Result!.Affixes);
        Assert.Equal(2, rerolled.Effects.Count);
        Assert.InRange(rerolled.Effects[0].Value, 22, 29);
        Assert.InRange(rerolled.Effects[1].Value, 45, 52);

        Assert.DoesNotContain(P1Affixes.All.Where(affix => affix.Source is "Natural" or "P30")
            .SelectMany(affix => affix.EffectComponents), component =>
                component.Kind.ToString().StartsWith("Hold", StringComparison.Ordinal));
    }

    [Fact]
    public void LegendaryAffixesAreFixedAndEnchantmentsAreSlotCompatible()
    {
        Assert.All(P14UniqueItems.All, definition =>
        {
            ItemInstance item = P14UniqueItems.Create(definition.StableId, 100, "p30-" + definition.StableId);
            Assert.All(item.Affixes, affix =>
            {
                Assert.Equal("传奇固定", affix.Definition.Source);
                Assert.Equal(definition.StableId, affix.Definition.SourceId);
                Assert.NotEmpty(affix.Effects);
            });
        });

        Assert.True(P9EnchantmentCatalog.Get("core.enchant.execution").Supports(P1ItemBases.Get("core.base.rusted_greatsword")));
        Assert.False(P9EnchantmentCatalog.Get("core.enchant.execution").Supports(P1ItemBases.Get("core.base.iron_ring")));
        Assert.True(P9EnchantmentCatalog.Get("core.enchant.humility").Supports(P1ItemBases.Get("core.base.march_boots")));

        Assert.Equal(9, P30VirtueViceEquipment.BeltPool.Count);
        ItemInstance belt = P30VirtueViceEquipment.CreateBelt(100, 7, "p30-belt");
        Assert.Equal(2, belt.Affixes.SelectMany(affix => affix.Effects).Count(effect => effect.Scope == ItemModifierScope.Rule &&
            effect.Kind.ToString().StartsWith("Hold", StringComparison.Ordinal)));
    }

    private static ItemInstance Item(ItemBaseDefinition itemBase, IReadOnlyList<RolledAffixComponent> effects, int quality)
    {
        var definition = new AffixDefinition("test.affix", "测试词缀", itemBase.Category, AffixPosition.Prefix,
            1, 1, 1, 1, 1, effects[0].Kind, Components: effects.Select(effect =>
                new AffixModifierComponent(effect.Kind, effect.Value, effect.Value, effect.Scope, effect.DisplayText)).ToArray());
        return new ItemInstance("test-item", itemBase, 100, ItemRarity.Rare,
            [new AffixRoll(definition, effects[0].Value, Components: effects)], Quality: quality);
    }
}
