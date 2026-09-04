using GameForWork.Core.Equipment;
using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P1.Progression;
using GameForWork.Core.P30;

namespace GameForWork.Tests;

public sealed class P32EquipmentCatalogTests
{
    [Fact]
    public void FixedSeedAuditCoversAllBasesAndOneHundredThousandItems()
    {
        P32EquipmentAuditResult result = P32EquipmentAudit.Run();
        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Failures));
        Assert.Equal(P32EquipmentAudit.RequiredSampleCount, result.SampleCount);
        Assert.Equal(244, result.CoveredBaseCount);
        Assert.Equal(result.CatalogBaseCount, result.CoveredBaseCount);
        Assert.Equal(212, result.CatalogAffixFamilyCount);
        Assert.Matches("^[0-9a-f]{64}$", result.DeterministicDigest);
    }

    [Fact]
    public void FormalSnapshotHasEverySealedCatalogAndPermanentUniqueIds()
    {
        Assert.Equal(1, EquipmentCatalog.Snapshot.SchemaVersion);
        Assert.Equal(244, EquipmentCatalog.Bases.Count);
        Assert.Equal(212, EquipmentCatalog.Affixes.Select(value => value.StableFamilyId).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(54, EquipmentCatalog.Enchantments.Count);
        Assert.Equal(55, EquipmentCatalog.LegendaryItems.Count);
        Assert.Equal(50, EquipmentCatalog.LegendaryItems.Count(value => value.Rarity == "Legendary"));
        Assert.Equal(5, EquipmentCatalog.LegendaryItems.Count(value => value.Rarity == "Mythic"));
        Assert.Equal(104, EquipmentCatalog.CraftingOperations.Count);
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
        Assert.Equal(54, EquipmentEnchantmentCatalog.All.Count);
        Assert.Equal(109, EquipmentRuleRegistry.All.Count);
        Assert.Equal(400, EquipmentEnchantmentCatalog.All.Single(value => value.DisplayName == "精准刻印").Value);
        Assert.Equal(6_500, EquipmentEnchantmentCatalog.All.Single(value => value.DisplayName == "毁伤铭文").Value);
        Assert.Contains(EquipmentEnchantmentCatalog.All.Single(value => value.DisplayName == "虹彩王印").EffectComponents,
            value => value.Kind == ItemModifierKind.MaximumVoidResistanceBonusBasisPoints && value.MinimumValue == 500);

        EquipmentEnchantmentEntry returning = EquipmentCatalog.Enchantments.Single(value => value.DisplayName == "归返王印");
        EquipmentRuleRegistration registration = EquipmentRuleRegistry.Get(returning.RuleId);
        Assert.Equal(EquipmentRuleEvent.ProjectileFinished, registration.Trigger);
        Assert.DoesNotContain("降低", returning.RuleText, StringComparison.Ordinal);
        Assert.Equal((300, 450), EquipmentRuleEngine.WorldEaterAddedVoidDamage(399));
        Assert.Equal((0, 0), EquipmentRuleEngine.WorldEaterAddedVoidDamage(99));
        Assert.Equal((100_000, 150_000), EquipmentRuleEngine.WorldEaterAddedVoidDamage(100_000));
    }

    [Fact]
    public void ChaosKingEnchantmentSupportsEveryWeaponAndAddsThirtyPercentFinalPhysicalAsVoid()
    {
        ItemEnchantment enchantment = EquipmentEnchantmentCatalog.All.Single(value => value.DisplayName == "混沌王印");
        Assert.Equal(4, enchantment.WorkshopLevel);
        ItemBaseDefinition[] weapons = EquipmentCatalog.Bases
            .Where(value => value.Category is ItemCategory.OneHandWeapon or ItemCategory.TwoHandWeapon)
            .ToArray();
        Assert.NotEmpty(weapons);
        Assert.All(weapons, weapon => Assert.True(EquipmentEnchantmentCatalog.Supports(enchantment, weapon)));

        ItemInstance item = ItemGenerator.Generate(weapons[0].StableId, 100, ItemRarity.Basic, 0x54) with
        {
            Enchantment = enchantment,
        };
        LocalWeaponStats local = EquipmentLoadout.CalculateLocalWeapon(item);
        Assert.Equal(local.Physical.MinimumPhysicalDamage * 30 / 100, local.Void.Minimum);
        Assert.Equal(local.Physical.MaximumPhysicalDamage * 30 / 100, local.Void.Maximum);
    }

    [Fact]
    public void WorldEaterAddsUncappedFinalPhysiqueVoidDamageToTheEquippedWeapon()
    {
        ItemInstance worldEater = EquipmentLegendaryFactory.Create(EquipmentRuleEngine.WorldEaterCatalogId,
            100, "world-eater", 52);
        var loadout = new EquipmentLoadout();
        Assert.True(loadout.TryEquip(EquipmentSlot.MainHand, worldEater));
        AssembledCharacterBuild build = CharacterBuildAssembler.Assemble(100,
            new CharacterAttributes(399, 10, 10, 10), loadout, new PassiveTreeAllocation(),
            new SkillConfiguration(P1SkillIds.HeavyStrike, SkillSupport.None));

        Assert.Equal(new LocalDamageRange(300, 450), build.Equipment.LocalWeapon!.Void);
    }

    [Fact]
    public void AttributeEnchantmentsCoverLevelsTwoThroughFourAndApplyTheirComponents()
    {
        ItemEnchantment[] additions = EquipmentEnchantmentCatalog.All
            .Where(value => value.StableId.StartsWith("equipment.enchantment.", StringComparison.Ordinal) &&
                int.Parse(value.StableId.Split('.')[2]) is >= 42 and <= 53)
            .ToArray();
        Assert.Equal(12, additions.Length);
        Assert.Equal(4, additions.Count(value => value.WorkshopLevel == 2));
        Assert.Equal(4, additions.Count(value => value.WorkshopLevel == 3));
        Assert.Equal(4, additions.Count(value => value.WorkshopLevel == 4));

        ItemBaseDefinition bodyBase = EquipmentCatalog.Bases.First(value => value.Category == ItemCategory.BodyArmor);
        ItemBaseDefinition ringBase = EquipmentCatalog.Bases.First(value => value.Category == ItemCategory.Ring);
        ItemBaseDefinition weaponBase = EquipmentCatalog.Bases.First(value => value.Category == ItemCategory.TwoHandWeapon);
        ItemEnchantment giant = additions.Single(value => value.DisplayName == "巨灵铭文");
        ItemEnchantment titan = additions.Single(value => value.DisplayName == "泰坦王印");
        Assert.True(EquipmentEnchantmentCatalog.Supports(giant, bodyBase));
        Assert.False(EquipmentEnchantmentCatalog.Supports(giant, weaponBase));
        Assert.True(EquipmentEnchantmentCatalog.Supports(titan, ringBase));
        Assert.False(EquipmentEnchantmentCatalog.Supports(titan, bodyBase));
        Assert.Contains("体魄提高 15%", EquipmentEnchantmentCatalog.EffectText(titan), StringComparison.Ordinal);

        ItemInstance cleanBody = ItemGenerator.Generate(bodyBase.StableId, 100, ItemRarity.Basic, 0x4200);
        var cleanLoadout = new EquipmentLoadout();
        var enchantedLoadout = new EquipmentLoadout();
        Assert.True(cleanLoadout.TryEquip(EquipmentSlot.Chest, cleanBody));
        Assert.True(enchantedLoadout.TryEquip(EquipmentSlot.Chest, cleanBody with { Enchantment = giant }));
        Assert.Equal(cleanLoadout.CalculateSummary().Modifiers.Physique + 60,
            enchantedLoadout.CalculateSummary().Modifiers.Physique);

        ItemInstance ring = ItemGenerator.Generate(ringBase.StableId, 100, ItemRarity.Basic, 0x4300) with { Enchantment = titan };
        var ringLoadout = new EquipmentLoadout();
        Assert.True(ringLoadout.TryEquip(EquipmentSlot.RingLeft, ring));
        Assert.Equal(1_500, ringLoadout.CalculateSummary().Modifiers.Value(ItemModifierKind.IncreasedPhysiqueBasisPoints));
    }

    [Fact]
    public void EveryLegendaryFixedAffixHasExecutableComponents()
    {
        foreach ((EquipmentLegendaryEntry entry, int index) in EquipmentCatalog.LegendaryItems.Select((value, index) => (value, index)))
        {
            ItemInstance item = EquipmentLegendaryFactory.Create(entry.Id, 100, $"legendary-audit-{index}", (ulong)index + 1);
            Assert.NotEmpty(item.Affixes);
            Assert.All(item.Affixes, affix =>
            {
                Assert.NotEmpty(affix.Effects);
                Assert.DoesNotContain(affix.Effects, component => component.Kind == ItemModifierKind.None);
            });
        }
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
    public void EveryCorruptionImplicitHasExecutableEffectsAndEveryBaseHasAnOutcome()
    {
        Assert.All(EquipmentCatalog.CorruptionImplicits, entry =>
        {
            IReadOnlyList<AffixModifierComponent> components = EquipmentCorruptionCatalog.Components(entry);
            Assert.NotEmpty(components);
            Assert.DoesNotContain(components, component => component.Kind == ItemModifierKind.None);
            Assert.Contains(EquipmentCatalog.Bases, itemBase => EquipmentCorruptionCatalog.Supports(entry, itemBase));
        });
        Assert.All(EquipmentCatalog.Bases, itemBase =>
            Assert.Contains(EquipmentCatalog.CorruptionImplicits, entry => EquipmentCorruptionCatalog.Supports(entry, itemBase)));
    }

    [Fact]
    public void CorruptionComponentsAffectLocalWeaponDefenseAndGlobalSummary()
    {
        ItemBaseDefinition swordBase = EquipmentCatalog.Bases.First(value => value.Category == ItemCategory.OneHandWeapon && value.CriticalChanceBasisPoints > 0);
        ItemInstance cleanSword = ItemGenerator.Generate(swordBase.StableId, 100, ItemRarity.Basic, 1);
        int cleanCriticalChance = EquipmentLoadout.CalculateLocalWeapon(cleanSword).Physical.CriticalChanceBasisPoints;
        ItemInstance sword = cleanSword with
        {
            IsCorrupted = true,
            CorruptionImplicitId = "test",
            RolledCorruptionComponents = [new(ItemModifierKind.BaseCriticalChanceBasisPoints, 100, ItemModifierScope.LocalWeapon)],
        };
        Assert.True(EquipmentLoadout.CalculateLocalWeapon(sword).Physical.CriticalChanceBasisPoints > cleanCriticalChance);

        ItemBaseDefinition armorBase = EquipmentCatalog.Bases.First(value => value.ArmorMaximum > 0);
        ItemInstance cleanArmor = ItemGenerator.Generate(armorBase.StableId, 100, ItemRarity.Basic, 2) with { Quality = 0 };
        int cleanValue = EquipmentLoadout.CalculateLocalDefense(cleanArmor).Armor;
        ItemInstance armor = cleanArmor with
        {
            Quality = 0,
            RolledCorruptionComponents = [new(ItemModifierKind.MoreLocalArmorBasisPoints, 2_000, ItemModifierScope.LocalDefense),
                new(ItemModifierKind.PhysicalResistanceBasisPoints, 500, ItemModifierScope.Global)],
        };
        Assert.Equal(cleanValue * 12_000 / 10_000, EquipmentLoadout.CalculateLocalDefense(armor).Armor);
        var loadout = new EquipmentLoadout();
        Assert.True(loadout.TryEquip(EquipmentSlot.Chest, armor) || loadout.TryEquip(EquipmentSlot.Helmet, armor) || loadout.TryEquip(EquipmentSlot.Gloves, armor) || loadout.TryEquip(EquipmentSlot.Boots, armor));
        Assert.Equal(500, loadout.CalculateSummary().Modifiers.Value(ItemModifierKind.PhysicalResistanceBasisPoints));
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
    public void ProtectedBiasedCraftCanUseAnOpenPrefixWithoutChangingExistingPrefixes()
    {
        ItemBaseDefinition itemBase = EquipmentCatalog.GetBase("equipment.base.ezomyte_blade");
        AffixRoll[] affixes =
        [
            MinimumRoll(Affix(itemBase, "equipment.affix.localphysicaldamagepercent", 2)),
            MinimumRoll(Affix(itemBase, "equipment.affix.weapon.added_lightning", 5)),
            MinimumRoll(Affix(itemBase, "equipment.affix.ignite.chance", 2)),
            MinimumRoll(Affix(itemBase, "equipment.affix.shock.chance", 4)),
            MinimumRoll(Affix(itemBase, "equipment.affix.bleed.chance", 4)),
        ];
        ItemInstance item = ItemGenerator.Generate(itemBase.StableId, 100, ItemRarity.Rare, 1, "protected-biased") with
        {
            Affixes = affixes,
            ProtectPrefixesNextCraft = true,
        };
        string[] oldPrefixes = item.Affixes.Where(value => value.Definition.Position == AffixPosition.Prefix)
            .Select(Signature).Order(StringComparer.Ordinal).ToArray();
        EquipmentCraftingOperationEntry operation = EquipmentCatalog.CraftingOperations
            .Single(value => value.DisplayName == "物理偏向打造");
        Assert.True(EquipmentCraftingService.Preview(item, new(operation.Id)).Available);

        bool observedAddedPrefix = false;
        for (ulong seed = 1; seed <= 128; seed++)
        {
            var wallet = new EquipmentCraftingWallet();
            EquipmentCraftingPreview preview = EquipmentCraftingService.Preview(item, new(operation.Id, Seed: seed));
            wallet.Credit(preview.Resource, preview.Cost);
            EquipmentCraftingResult result = EquipmentCraftingService.Execute(wallet, item, new(operation.Id, Seed: seed));
            Assert.True(result.Succeeded, result.FailureReason);
            Assert.False(result.Item!.ProtectPrefixesNextCraft);
            Assert.Equal(oldPrefixes, result.Item.Affixes.Where(value => value.Definition.Position == AffixPosition.Prefix)
                .Select(Signature).Where(oldPrefixes.Contains).Order(StringComparer.Ordinal));
            Assert.NotEqual(item.Affixes.Select(Signature).Order(StringComparer.Ordinal),
                result.Item.Affixes.Select(Signature).Order(StringComparer.Ordinal));
            observedAddedPrefix |= result.Item.PrefixCount == 3;
        }
        Assert.True(observedAddedPrefix);
    }

    [Fact]
    public void AttributeTiersAreLocalToTheBaseAndAttributeAugmentCanRollPercentageFamilies()
    {
        ItemBaseDefinition amulet = EquipmentCatalog.GetBase("equipment.base.warfront.last_banner_emblem");
        AffixDefinition bestPhysique = P1Affixes.For(amulet, 102)
            .Where(value => value.StableFamilyId == "equipment.affix.strength")
            .MinBy(value => value.Tier)!;
        Assert.Equal(2, bestPhysique.Tier);
        Assert.Equal(1, P1Affixes.TierFor(amulet, bestPhysique));
        Assert.Equal((51, 55), (bestPhysique.MinimumValue, bestPhysique.MaximumValue));

        AffixDefinition resistance = P1Affixes.For(amulet, 102)
            .First(value => value.Position == AffixPosition.Suffix &&
                value.ModifierKind == ItemModifierKind.FireResistanceBasisPoints);
        ItemInstance source = ItemGenerator.Generate(amulet.StableId, 102, ItemRarity.Rare, 1, "attribute-augment") with
        {
            Affixes = [MinimumRoll(bestPhysique), MinimumRoll(resistance)],
        };
        EquipmentCraftingOperationEntry operation = EquipmentCatalog.CraftingOperations
            .Single(value => value.DisplayName == "属性偏向打造");
        bool rolledPercentage = false;
        for (ulong seed = 1; seed <= 20_000 && !rolledPercentage; seed++)
        {
            EquipmentCraftingPreview preview = EquipmentCraftingService.Preview(source, new(operation.Id, Seed: seed));
            Assert.True(preview.Available, preview.FailureReason);
            var wallet = new EquipmentCraftingWallet();
            wallet.Credit(preview.Resource, preview.Cost);
            EquipmentCraftingResult result = EquipmentCraftingService.Execute(wallet, source, new(operation.Id, Seed: seed));
            Assert.True(result.Succeeded, result.FailureReason);
            rolledPercentage = result.Item!.Affixes.Any(value => value.Effects.Any(effect => effect.Kind is
                ItemModifierKind.IncreasedPhysiqueBasisPoints or ItemModifierKind.IncreasedDexterityBasisPoints or
                ItemModifierKind.IncreasedSpiritBasisPoints or ItemModifierKind.IncreasedEnergyBasisPoints or
                ItemModifierKind.IncreasedAllAttributesBasisPoints));
        }
        Assert.True(rolledPercentage);
    }

    [Fact]
    public void BiasedCraftPreviewRejectsAnItemWhoseOnlyRemovalIsProtected()
    {
        ItemBaseDefinition itemBase = EquipmentCatalog.GetBase("equipment.base.ezomyte_blade");
        ItemInstance item = ItemGenerator.Generate(itemBase.StableId, 100, ItemRarity.Rare, 1, "protected-only") with
        {
            Affixes = [MinimumRoll(Affix(itemBase, "equipment.affix.weapon.added_lightning", 5))],
            ProtectPrefixesNextCraft = true,
        };
        EquipmentCraftingOperationEntry operation = EquipmentCatalog.CraftingOperations
            .Single(value => value.DisplayName == "物理偏向打造");
        EquipmentCraftingPreview preview = EquipmentCraftingService.Preview(item, new(operation.Id));
        Assert.False(preview.Available);
        Assert.Equal("no_replaceable_affix", preview.FailureReason);
    }

    [Fact]
    public void ElementalWeaponDamageUsesPoeRangesPerHandednessAndMigratesExistingRolls()
    {
        ItemBaseDefinition oneHanded = EquipmentCatalog.Bases.First(value => value.Category == ItemCategory.OneHandWeapon);
        ItemBaseDefinition twoHanded = EquipmentCatalog.GetBase("equipment.base.ezomyte_blade");
        AffixDefinition oneHandedLightning = Affix(oneHanded, "equipment.affix.weapon.added_lightning", 1);
        AffixDefinition twoHandedLightning = Affix(twoHanded, "equipment.affix.weapon.added_lightning", 1);
        Assert.Equal([15, 21, 296, 344], oneHandedLightning.EffectComponents
            .SelectMany(value => new[] { value.MinimumValue, value.MaximumValue }));
        Assert.Equal([28, 38, 549, 638], twoHandedLightning.EffectComponents
            .SelectMany(value => new[] { value.MinimumValue, value.MaximumValue }));

        AffixDefinition oldDefinition = twoHandedLightning with
        {
            MinimumValue = 8,
            MaximumValue = 9,
            Components =
            [
                twoHandedLightning.EffectComponents[0] with { MinimumValue = 8, MaximumValue = 9 },
                twoHandedLightning.EffectComponents[1] with { MinimumValue = 69, MaximumValue = 82 },
            ],
        };
        ItemInstance saved = ItemGenerator.Generate(twoHanded.StableId, 100, ItemRarity.Rare, 2, "old-lightning") with
        {
            Affixes = [new AffixRoll(oldDefinition, 8, Components:
            [
                new(ItemModifierKind.AddedMinimumLightningDamage, 8, ItemModifierScope.LocalWeapon),
                new(ItemModifierKind.AddedMaximumLightningDamage, 76, ItemModifierScope.LocalWeapon),
            ])],
        };
        AffixRoll migrated = Assert.Single(EquipmentItemRebinder.Rebind(saved).Affixes);
        Assert.Equal([28, 597], migrated.Effects.Select(value => value.Value));
    }

    [Fact]
    public void EveryFormalCraftingOperationReachesAConcreteExecutor()
    {
        ItemInstance rare = ItemGenerator.Generate("core.base.life_ring", 100, ItemRarity.Rare, 0x321, "all-crafts");
        foreach (EquipmentCraftingOperationEntry operation in EquipmentCatalog.CraftingOperations)
        {
            EquipmentCraftingRequest request = new(operation.Id,
                SelectedDefinitionId: operation.Kind == "Enchantment" ? EquipmentCatalog.Enchantments.First().Id :
                    operation.Kind == "LegendaryExchange" ? EquipmentCatalog.LegendaryItems.First(value => value.Rarity == "Legendary").Id : string.Empty,
                SelectedAffixFamilyId: rare.Affixes.FirstOrDefault()?.Definition.StableFamilyId ?? string.Empty,
                Seed: 0x322);
            EquipmentCraftingPreview preview = EquipmentCraftingService.Preview(rare, request);
            var wallet = new EquipmentCraftingWallet();
            wallet.Credit(preview.Resource, preview.Cost);
            EquipmentCraftingResult result = EquipmentCraftingService.Execute(wallet, rare, request);
            Assert.NotEqual("operation_not_implemented", result.FailureReason);
        }
    }

    [Fact]
    public void FormalNamesExecuteDivineBlessedQualityCorruptionAndBothLinkCrafts()
    {
        string[] names = ["神铸重掷", "祝铸重掷", "精磨品质", "赤蚀腐化", "连接重铸", "稳固增连"];
        ItemInstance rare = ItemGenerator.Generate("core.base.rusted_greatsword", 100, ItemRarity.Rare, 0x400, "formal-metal") with { LinkedSocketCount = 1 };
        foreach (string name in names)
        {
            EquipmentCraftingOperationEntry operation = EquipmentCatalog.CraftingOperations.Single(value => value.DisplayName == name);
            EquipmentCraftingPreview preview = EquipmentCraftingService.Preview(rare, new(operation.Id, Seed: 0x401));
            var wallet = new EquipmentCraftingWallet();
            wallet.Credit(preview.Resource, preview.Cost);
            EquipmentCraftingResult result = EquipmentCraftingService.Execute(wallet, rare, new(operation.Id, Seed: 0x401));
            Assert.True(result.Succeeded, $"{name}: {result.FailureReason}");
            Assert.NotNull(result.Item);
        }
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

    private static AffixDefinition Affix(ItemBaseDefinition itemBase, string family, int tier) =>
        P1Affixes.For(itemBase, 100).Single(value => value.StableFamilyId == family && value.Tier == tier);

    private static AffixRoll MinimumRoll(AffixDefinition definition)
    {
        RolledAffixComponent[] components = definition.EffectComponents.Select(value =>
            new RolledAffixComponent(value.Kind, value.MinimumValue, value.Scope, value.DisplayText)).ToArray();
        return new AffixRoll(definition, components[0].Value, Components: components);
    }
}
