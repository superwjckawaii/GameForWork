using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Town;
using GameForWork.Core.Skills;
using GameForWork.Core.Content;
using GameForWork.Core.Archetypes;
using GameForWork.Core.Builds;
using GameForWork.Core.Equipment;
using GameForWork.Core.SkillCatalog;
using GameForWork.Core.Campaign.Combat;

namespace GameForWork.Tests;

public sealed class EquipmentAffixTests
{
    [Fact]
    public void LegacyAndForbiddenNaturalFamiliesAreAbsent()
    {
        Assert.DoesNotContain(Affixes.All, affix => affix.StableFamilyId.StartsWith("core.affix.", StringComparison.Ordinal));
        Assert.DoesNotContain(Affixes.All, affix => affix.StableFamilyId.Contains("stunrecovery", StringComparison.Ordinal));
        Assert.DoesNotContain(Affixes.All, affix => affix.StableFamilyId == "archetypes.affix.rune.spellblade");
        Assert.DoesNotContain(Affixes.All, affix => affix.Source is "Natural" or "Builds" &&
            affix.EffectComponents.Any(component => component.Kind == ItemModifierKind.ExtraSupportLinkCapacity));
    }

    [Fact]
    public void ImportedCompoundAffixesKeepEveryNumericComponent()
    {
        AffixDefinition oneHand = EquipmentCatalog.GetAffix("equipmentImport.affix.localphysicaldamage", 1);
        Assert.Collection(oneHand.EffectComponents,
            low => { Assert.Equal(ItemModifierKind.AddedMinimumPhysicalDamage, low.Kind); Assert.Equal((22, 29), (low.MinimumValue, low.MaximumValue)); },
            high => { Assert.Equal(ItemModifierKind.AddedMaximumPhysicalDamage, high.Kind); Assert.Equal((45, 52), (high.MinimumValue, high.MaximumValue)); });

        AffixDefinition armourLife = EquipmentCatalog.GetAffix("equipmentImport.affix.localbasearmourandlife", 1);
        Assert.Contains(armourLife.EffectComponents, component => component.Kind == ItemModifierKind.FlatArmor && component.MinimumValue == 97 && component.MaximumValue == 144);
        Assert.Contains(armourLife.EffectComponents, component => component.Kind == ItemModifierKind.FlatMaximumLife && component.MinimumValue == 34 && component.MaximumValue == 38);

        AffixDefinition regeneration = EquipmentCatalog.GetAffix("equipmentImport.affix.liferegeneration", 1);
        Assert.Equal((180, 200), (regeneration.MinimumValue, regeneration.MaximumValue));
        Assert.Equal(ItemModifierKind.MaximumLifeRegenerationBasisPoints, regeneration.ModifierKind);

        AffixDefinition instantFlask = EquipmentCatalog.GetAffix("equipmentImport.affix.flaskpartialinstantrecovery", 1);
        Assert.Collection(instantFlask.EffectComponents,
            amount => { Assert.Equal(ItemModifierKind.IncreasedFlaskRecoveryAmountBasisPoints, amount.Kind); Assert.True(amount.MaximumValue < 0); },
            speed => { Assert.Equal(ItemModifierKind.IncreasedFlaskRecoveryRateBasisPoints, speed.Kind); Assert.Equal(13_500, speed.MinimumValue); },
            instant => { Assert.Equal(ItemModifierKind.InstantFlaskRecoveryPortionBasisPoints, instant.Kind); Assert.Equal(5_000, instant.MinimumValue); });
    }

    [Fact]
    public void WeaponAndDefenceUseBuildsLocalOrder()
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
            new(ItemModifierKind.AddedMinimumFireDamage, 10, ItemModifierScope.LocalWeapon),
            new(ItemModifierKind.AddedMaximumFireDamage, 20, ItemModifierScope.LocalWeapon),
            new(ItemModifierKind.AddedMinimumColdDamage, 20, ItemModifierScope.LocalWeapon),
            new(ItemModifierKind.AddedMaximumColdDamage, 30, ItemModifierScope.LocalWeapon),
            new(ItemModifierKind.AddedMinimumLightningDamage, 30, ItemModifierScope.LocalWeapon),
            new(ItemModifierKind.AddedMaximumLightningDamage, 40, ItemModifierScope.LocalWeapon),
            new(ItemModifierKind.AddedMinimumVoidDamage, 40, ItemModifierScope.LocalWeapon),
            new(ItemModifierKind.AddedMaximumVoidDamage, 50, ItemModifierScope.LocalWeapon),
        ], quality: 20);
        LocalWeaponStats localWeapon = EquipmentLoadout.CalculateLocalWeapon(weapon);
        WeaponProfile profile = localWeapon.Physical;
        Assert.Equal((288, 576), (profile.MinimumPhysicalDamage, profile.MaximumPhysicalDamage));
        Assert.Equal(1_200, profile.AttacksPerSecondMilli);
        Assert.Equal(700, profile.CriticalChanceBasisPoints);
        Assert.Equal(new LocalDamageRange(12, 24), localWeapon.Fire);
        Assert.Equal(new LocalDamageRange(24, 36), localWeapon.Cold);
        Assert.Equal(new LocalDamageRange(36, 48), localWeapon.Lightning);
        Assert.Equal(new LocalDamageRange(48, 60), localWeapon.Void);
        Assert.Equal(691.2, localWeapon.TotalDamagePerSecond, .01);

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
    public void MixedWeaponDamageKeepsLocalElementalAndVoidChannels()
    {
        DamageBreakdown damage = DamagePacketRules.ResolveMixed(100, SkillDamageType.Physical,
            new AddedWeaponDamage(Fire: 20, Cold: 30, Lightning: 40, Void: 50), SkillSupport.None,
            targetArmor: 0, fireResistance: 0, coldResistance: 0, lightningResistance: 0,
            voidResistance: 0);

        Assert.Equal((100, 20, 30, 40, 50, 240),
            (damage.Physical, damage.Fire, damage.Cold, damage.Lightning, damage.Void, damage.Total));
    }

    [Fact]
    public void BuildsOrdinaryTiersAndSpecialSourcesAreSealed()
    {
        AffixDefinition[] attack = EquipmentCatalog.Affixes.Where(affix => affix.StableFamilyId == "equipment.affix.attack.damage").ToArray();
        Assert.Equal(6, attack.Length);
        Assert.Equal([1, 20, 40, 60, 75, 85], attack.OrderByDescending(affix => affix.Tier).Select(affix => affix.MinimumItemLevel));
        AffixDefinition top = attack.Single(affix => affix.Tier == 1);
        Assert.Equal((3_500, 4_500), (top.MinimumValue, top.MaximumValue));

        AffixDefinition[] special = GuideCatalog.SpecialAffixFamilies
            .SelectMany(family => family).Select(value => EquipmentCatalog.GetAffix(value.Id, value.Tier)).ToArray();
        Assert.Equal(49, special.Select(value => value.StableFamilyId).Distinct(StringComparer.Ordinal).Count());
        Assert.All(special, affix =>
        {
            Assert.Equal("Natural", affix.Source);
            Assert.Equal(affix.ModifierKind, affix.EffectComponents[0].Kind);
            Assert.Contains(ItemBases.All, affix.Supports);
        });
    }

    [Fact]
    public void GlobalDamageIncreaseAffixesNeverRollOnWeapons()
    {
        HashSet<ItemModifierKind> globalDamageIncreases =
        [
            ItemModifierKind.IncreasedAttackDamageBasisPoints,
            ItemModifierKind.IncreasedSpellDamageBasisPoints,
            ItemModifierKind.IncreasedElementalDamageBasisPoints,
            ItemModifierKind.IncreasedPhysicalDamageBasisPoints,
            ItemModifierKind.IncreasedFireDamageBasisPoints,
            ItemModifierKind.IncreasedColdDamageBasisPoints,
            ItemModifierKind.IncreasedLightningDamageBasisPoints,
            ItemModifierKind.IncreasedVoidDamageBasisPoints,
            ItemModifierKind.IncreasedMeleeDamageBasisPoints,
            ItemModifierKind.IncreasedProjectileDamageBasisPoints,
            ItemModifierKind.IncreasedAreaDamageBasisPoints,
            ItemModifierKind.IncreasedDamageOverTimeBasisPoints,
            ItemModifierKind.IncreasedBleedDamageBasisPoints,
            ItemModifierKind.IncreasedPoisonDamageBasisPoints,
            ItemModifierKind.IncreasedIgniteDamageBasisPoints,
        ];
        ItemBaseDefinition[] weapons = ItemBases.All.Where(item =>
            item.Category is ItemCategory.OneHandWeapon or ItemCategory.TwoHandWeapon).ToArray();

        AffixDefinition[] illegal = EquipmentCatalog.Affixes.Where(affix =>
            !new[] { "equipment.affix.occult.wither", "equipment.affix.projectile.far_damage", "equipment.affix.spell.elemental", "equipment.affix.spell.void" }.Contains(affix.StableFamilyId, StringComparer.Ordinal) &&
            affix.EffectComponents.Any(component => component.Scope == ItemModifierScope.Global && globalDamageIncreases.Contains(component.Kind)) &&
            weapons.Any(affix.Supports)).ToArray();
        Assert.True(illegal.Length == 0, string.Join(Environment.NewLine,
            illegal.Select(affix => $"{affix.StableFamilyId}/T{affix.Tier}")));
        Assert.Contains(EquipmentCatalog.Affixes, affix =>
            affix.StableFamilyId == "equipment.affix.attack.damage" && affix.ApplicableCategories!.Contains(ItemCategory.Ring));
    }

    [Fact]
    public void RestoringLegacyWeaponsRemovesGlobalDamageButKeepsLocalPhysicalDamage()
    {
        ItemBaseDefinition itemBase = ItemBases.Get("equipmentImport.base.broad_sword");
        AffixDefinition global = EquipmentCatalog.Affixes.First(affix => affix.StableFamilyId == "equipment.affix.attack.damage");
        AffixDefinition local = Affixes.All.First(affix => affix.StableFamilyId == "equipment.affix.localphysicaldamage" && affix.Supports(itemBase));
        var item = new ItemInstance("legacy-global-weapon", itemBase, 100, ItemRarity.Rare,
            [new AffixRoll(global, global.MinimumValue), new AffixRoll(local, local.MinimumValue)]);

        ItemInstance normalized = EquipmentItemRebinder.Rebind(item);

        AffixRoll kept = Assert.Single(normalized.Affixes);
        Assert.Equal(local.StableFamilyId, kept.Definition.StableFamilyId);
        Assert.Contains(kept.Effects, effect => effect.Scope == ItemModifierScope.LocalWeapon);
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
        DamageBreakdown result = DamagePacketRules.ResolveMixed(10_000, SkillDamageType.Physical,
            default, SkillSupport.None, 0, 0, 0, 0, 0, equipment: effects.ToDictionary(effect => effect.Kind, effect => effect.Value));
        Assert.Equal(10_000, result.Total);
        Assert.Equal(0, result.Physical);
        Assert.True(result.Fire > result.Cold);
        Assert.True(result.Cold > result.Void);
    }

    [Fact]
    public void DivineRerollPreservesCompoundShapeAndNaturalPoolCannotGrantVirtueVice()
    {
        ItemBaseDefinition itemBase = ItemBases.Get("equipmentImport.base.broad_sword");
        AffixDefinition definition = EquipmentCatalog.GetAffix("equipmentImport.affix.localphysicaldamage", 1);
        ItemInstance item = new("compound-divine", itemBase, 100, ItemRarity.Rare,
            [new AffixRoll(definition, 22, Components:
            [
                new(ItemModifierKind.AddedMinimumPhysicalDamage, 22, ItemModifierScope.LocalWeapon),
                new(ItemModifierKind.AddedMaximumPhysicalDamage, 45, ItemModifierScope.LocalWeapon),
            ])]);
        CraftPreview preview = SocketCraftingRules.Preview(item, SocketCraftOperation.DivineReroll, seed: 30);
        Assert.True(preview.Succeeded);
        AffixRoll rerolled = Assert.Single(preview.Result!.Affixes);
        Assert.Equal(2, rerolled.Effects.Count);
        Assert.InRange(rerolled.Effects[0].Value, 22, 29);
        Assert.InRange(rerolled.Effects[1].Value, 45, 52);

        Assert.DoesNotContain(Affixes.All.Where(affix => affix.Source is "Natural" or "Builds")
            .SelectMany(affix => affix.EffectComponents), component =>
                component.Kind.ToString().StartsWith("Hold", StringComparison.Ordinal));
    }

    [Fact]
    public void LegendaryAffixesAreFixedAndEnchantmentsAreSlotCompatible()
    {
        Assert.All(UniqueItems.All, definition =>
        {
            ItemInstance item = UniqueItems.Create(definition.StableId, 100, "builds-" + definition.StableId);
            Assert.All(item.Affixes, affix =>
            {
                Assert.Equal("传奇固定", affix.Definition.Source);
                Assert.Equal(item.LegendaryCatalogId, affix.Definition.SourceId);
                Assert.NotEmpty(affix.Effects);
            });
        });

        Assert.True(EnchantmentCatalog.Get("core.enchant.execution").Supports(ItemBases.Get("core.base.rusted_greatsword")));
        Assert.False(EnchantmentCatalog.Get("core.enchant.execution").Supports(ItemBases.Get("core.base.iron_ring")));
        Assert.True(EnchantmentCatalog.Get("core.enchant.humility").Supports(ItemBases.Get("core.base.march_boots")));

        Assert.Equal(9, VirtueViceEquipment.BeltPool.Count);
        ItemInstance belt = VirtueViceEquipment.CreateBelt(100, 7, "builds-belt");
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
