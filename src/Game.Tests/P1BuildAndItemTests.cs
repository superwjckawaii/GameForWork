using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P1.Progression;

namespace GameForWork.Tests;

public sealed class P1BuildAndItemTests
{
    [Fact]
    public void P205PassiveTreeContainsTwelveThematicSectorsAndMovementGrowth()
    {
        Assert.Equal(1_200, P1PassiveTree.Nodes.Count);
        Assert.Equal(12, P1PassiveTree.Nodes.Select(node => node.Branch).Distinct().Count());

        var allocation = new PassiveTreeAllocation(start: PassiveStartKind.Dexterity);
        Assert.True(allocation.TryAllocatePath("core.passive.v3.cluster.03.04.mastery", 70));

        Assert.True(allocation.CalculateModifiers().IncreasedMovementSpeedBasisPoints > 0);
    }

    [Fact]
    public void ExperienceTableStopsAtOneHundredUntilBreakthroughThenReachesOneTwenty()
    {
        var progression = new CharacterProgression();

        ExperienceGainResult result = progression.AddExperience(CharacterProgression.TotalExperienceToCap);

        Assert.Equal(100, progression.Level);
        Assert.Equal(99, progression.EarnedPassivePoints);
        Assert.Equal(99, result.PassivePointsGained);
        Assert.True(result.ReachedLevelCap);
        Assert.Equal(1_140, CharacterProgression.RequiredExperience(9));
        Assert.True(progression.UnlockFinalBreakthrough());
        progression.AddExperience(CharacterProgression.TotalExperienceToCap);
        Assert.Equal(120, progression.Level);
        Assert.Equal(119, progression.EarnedPassivePoints);
        Assert.Equal(CharacterProgression.TotalExperienceToCap, progression.Experience);
    }

    [Fact]
    public void FirstBossGrantsOnlyOneAdditionalPassivePoint()
    {
        var progression = new CharacterProgression();

        Assert.True(progression.ClaimFirstBossPassivePoint());
        Assert.False(progression.ClaimFirstBossPassivePoint());
        Assert.Equal(1, progression.EarnedPassivePoints);
    }

    [Fact]
    public void PassiveTreeHasP23NodeCounts()
    {
        Assert.Equal(1_200, P1PassiveTree.Nodes.Count);
        Assert.Equal(6, P1PassiveTree.Nodes.Count(node => node.Kind == PassiveNodeKind.Start));
        Assert.Equal(882, P1PassiveTree.Nodes.Count(node => node.Kind == PassiveNodeKind.Small));
        Assert.Equal(180, P1PassiveTree.Nodes.Count(node => node.Kind == PassiveNodeKind.Notable));
        Assert.Equal(36, P1PassiveTree.Nodes.Count(node => node.Kind == PassiveNodeKind.Rule));
        Assert.Equal(72, P1PassiveTree.Nodes.Count(node => node.Kind == PassiveNodeKind.Mastery));
        Assert.Equal(24, P1PassiveTree.Nodes.Count(node => node.Kind == PassiveNodeKind.JewelSocket));
        Assert.Equal(120, PassiveTreeAllocation.MaximumAllocatedPoints);
    }

    [Fact]
    public void PassiveTreeLayoutFitsItsDeterministicSquareCanvas()
    {
        Assert.All(P1PassiveTree.Nodes, node =>
        {
            Assert.InRange(node.X, -P1PassiveTree.LayoutExtent, P1PassiveTree.LayoutExtent);
            Assert.InRange(node.Y, -P1PassiveTree.LayoutExtent, P1PassiveTree.LayoutExtent);
        });
        Assert.Equal(P1PassiveTree.Nodes.Count,
            P1PassiveTree.Nodes.Select(node => (MathF.Round(node.X, 2), MathF.Round(node.Y, 2))).Distinct().Count());
    }

    [Fact]
    public void P25ClustersHaveEightRelatedSmallNodesAndSixCareerStartGardens()
    {
        PassiveNodeDefinition[] clusterSmall = P1PassiveTree.Nodes
            .Where(node => node.Kind == PassiveNodeKind.Small && node.StableId.Contains(".v3.cluster.", StringComparison.Ordinal))
            .ToArray();
        IGrouping<string, PassiveNodeDefinition>[] clusters = clusterSmall
            .GroupBy(node => string.Join('.', node.StableId.Split('.')[..^2]))
            .ToArray();

        Assert.Equal(72, clusters.Length);
        Assert.All(clusters, cluster => Assert.Equal(8, cluster.Count()));
        Assert.Equal(6, P1PassiveTree.Nodes.Count(node => node.Kind == PassiveNodeKind.Start));
        Assert.All(P1PassiveTree.Nodes.Where(node => node.Kind == PassiveNodeKind.Start), node =>
        {
            Assert.Empty(node.Effects);
            Assert.Equal(8, P1PassiveTree.Neighbors(node.StableId).Count);
            Assert.Equal(6, P1PassiveTree.Neighbors(node.StableId).Count(id => id.Contains(".start_garden.", StringComparison.Ordinal)));
        });
    }

    [Fact]
    public void DamageClusterSmallNodesUseSixteenToTwentyPercentIncreased()
    {
        PassiveEffectKind[] damageKinds =
        [
            PassiveEffectKind.IncreasedTwoHandDamageBasisPoints, PassiveEffectKind.IncreasedMeleeDamageBasisPoints,
            PassiveEffectKind.IncreasedBleedDamageBasisPoints, PassiveEffectKind.IncreasedPhysicalDamageOverTimeBasisPoints,
            PassiveEffectKind.IncreasedAttackSkillDamageBasisPoints, PassiveEffectKind.IncreasedProjectileDamageBasisPoints,
            PassiveEffectKind.IncreasedAreaDamageBasisPoints, PassiveEffectKind.IncreasedElementalDamageBasisPoints,
            PassiveEffectKind.IncreasedSpellDamageBasisPoints, PassiveEffectKind.IncreasedVoidDamageBasisPoints,
            PassiveEffectKind.IncreasedDamageOverTimeBasisPoints,
        ];
        PassiveEffect[] effects = P1PassiveTree.Nodes
            .Where(node => node.Kind == PassiveNodeKind.Small && node.StableId.Contains(".v3.cluster.", StringComparison.Ordinal))
            .SelectMany(node => node.Effects).Where(effect => damageKinds.Contains(effect.Kind)).ToArray();

        Assert.NotEmpty(effects);
        Assert.All(effects, effect => Assert.InRange(effect.Value, 1_600, 2_000));
    }

    [Fact]
    public void PassiveAllocationEnforcesPathPointsAndLeafRefund()
    {
        var allocation = new PassiveTreeAllocation();

        Assert.False(allocation.TryAllocate("core.passive.v3.travel.00.10", 10));
        Assert.False(allocation.TryAllocate(P1PassiveTree.StartNode(PassiveStartKind.Physique), 1));
        Assert.True(allocation.TryAllocate("core.passive.v3.travel.00.15", 1));
        Assert.False(allocation.TryAllocate("core.passive.v3.travel.00.14", 1));
        Assert.True(allocation.TryAllocate("core.passive.v3.travel.00.14", 2));
        Assert.False(allocation.TryRefund("core.passive.v3.travel.00.15"));
        Assert.True(allocation.TryRefund("core.passive.v3.travel.00.14"));
        Assert.Equal(4, allocation.MemoryAshes);
    }

    [Fact]
    public void PassiveResetCostsTenMemoryAshes()
    {
        var allocation = new PassiveTreeAllocation(10);
        Assert.True(allocation.TryAllocate("core.passive.v3.travel.00.15", 1));

        Assert.True(allocation.TryReset());

        Assert.Empty(allocation.Allocated);
        Assert.Equal(0, allocation.MemoryAshes);
    }

    [Fact]
    public void ChargedHeavyStrikeGainsThreeChargesAndConsumesThem()
    {
        var state = new ChargedHeavyStrikeState();

        state.AdvanceWithoutAttacking(30);

        Assert.Equal(3, state.Charges);
        Assert.Equal(13_600, state.ConsumeForHeavyStrike(30));
        Assert.Equal(0, state.Charges);
        state.AdvanceWithoutAttacking(40);
        state.RecordOtherAttack(40);
        Assert.Equal(0, state.Charges);
    }

    [Theory]
    [InlineData(ItemRarity.Basic, 0, 0)]
    [InlineData(ItemRarity.Magic, 1, 2)]
    [InlineData(ItemRarity.Rare, 4, 6)]
    public void GeneratedAffixCountsRespectNaturalDropRules(ItemRarity rarity, int minimum, int maximum)
    {
        for (ulong seed = 1; seed <= 20; seed++)
        {
            ItemInstance item = ItemGenerator.Generate("core.base.rusted_greatsword", 10, rarity, seed);
            Assert.InRange(item.Affixes.Count, minimum, maximum);
            Assert.True(item.PrefixCount <= (rarity == ItemRarity.Magic ? 1 : 3));
            Assert.True(item.SuffixCount <= (rarity == ItemRarity.Magic ? 1 : 3));
            Assert.Equal(item.Affixes.Count, item.Affixes.Select(affix => affix.Definition.StableFamilyId).Distinct().Count());
            Assert.True(item.IsIdentified);
        }
    }

    [Fact]
    public void TiersAndDefenseAffixesRespectItemLevelAndBaseType()
    {
        for (ulong seed = 1; seed <= 100; seed++)
        {
            ItemInstance lowLevel = ItemGenerator.Generate("core.base.crude_chainmail", 1, ItemRarity.Rare, seed);
            Assert.All(lowLevel.Affixes, affix => Assert.True(affix.Definition.MinimumItemLevel <= 1));
            Assert.All(lowLevel.Affixes, affix => Assert.True(affix.Definition.Supports(lowLevel.Base)));
        }

        Assert.Contains(P1Affixes.For(ItemCategory.BodyArmor, 120), affix => affix.Tier == 1);
    }

    [Fact]
    public void IronRingUsesImportedPhysicalImplicitRange()
    {
        for (ulong seed = 1; seed <= 20; seed++)
        {
            ItemInstance ring = ItemGenerator.Generate("core.base.iron_ring", 1, ItemRarity.Basic, seed);
            Assert.InRange(ring.ImplicitValue, 1, 4);
        }
    }

    [Fact]
    public void WeaponChestAndHelmetProvideTwoCoreAndFiveLinks()
    {
        var loadout = new EquipmentLoadout();
        Assert.True(loadout.TryEquip(EquipmentSlot.MainHand, Basic("core.base.rusted_greatsword")));
        Assert.True(loadout.TryEquip(EquipmentSlot.Chest, Basic("core.base.crude_chainmail")));
        Assert.True(loadout.TryEquip(EquipmentSlot.Helmet, Basic("core.base.iron_helmet")));

        EquipmentSummary summary = loadout.CalculateSummary();

        Assert.Equal(2, summary.CoreSkillCapacity);
        Assert.Equal(5, summary.SupportLinkCapacity);
        SkillCapacityResult capacity = SkillCapacityRules.Validate(
            [
                new SkillConfiguration(P1SkillIds.HeavyStrike, SkillSupport.AttackSpeed | SkillSupport.Bleed),
                new SkillConfiguration(P1SkillIds.WarCry, SkillSupport.IncreasedArea),
            ],
            summary);
        Assert.True(capacity.IsValid);
    }

    [Fact]
    public void ExtraLinkAffixAddsAtMostItsSinglePoint()
    {
        AffixDefinition extraLink = Assert.Single(
            P1Affixes.All,
            affix => affix.StableFamilyId == "core.affix.weapon.extra_link");
        var weapon = new ItemInstance(
            "linked-weapon",
            P1ItemBases.Get("core.base.rusted_greatsword"),
            10,
            ItemRarity.Rare,
            [new AffixRoll(extraLink, 1)]);
        var loadout = new EquipmentLoadout();
        Assert.True(loadout.TryEquip(EquipmentSlot.MainHand, weapon));

        Assert.Equal(3, loadout.CalculateSummary().SupportLinkCapacity);
        Assert.Equal(1, weapon.ExtraSupportLinkCapacity);
    }

    [Fact]
    public void BuildAssemblerAppliesEquipmentAndPassiveBonuses()
    {
        var loadout = new EquipmentLoadout();
        Assert.True(loadout.TryEquip(EquipmentSlot.MainHand, Basic("core.base.rusted_greatsword")));
        Assert.True(loadout.TryEquip(EquipmentSlot.RingLeft, Basic("core.base.life_ring")));
        var passives = new PassiveTreeAllocation();
        Assert.True(passives.TryAllocatePath("core.passive.v3.cluster.00.00.small.00", 20));

        AssembledCharacterBuild build = CharacterBuildAssembler.Assemble(
            1,
            CharacterAttributes.IronOathStarting,
            loadout,
            passives,
            new SkillConfiguration(P1SkillIds.HeavyStrike, SkillSupport.None));

        Assert.InRange(build.Sheet.MaximumLife().Value, 270, 280);
        Assert.Equal(1_600 + build.Equipment.Modifiers.IncreasedPhysicalDamageBasisPoints,
            build.IncreasedAttackDamageBasisPoints);
        Assert.NotNull(build.Equipment.Weapon);
    }

    [Fact]
    public void FlaskEffectAndLegendaryRulesAreDeterministic()
    {
        var flask = new LifeFlaskState(new LifeFlaskDefinition(40, 30, 10));
        Assert.Equal(48, flask.TryUse(100, 2_000));
        Assert.Equal(20, flask.Charges);

        ItemInstance legendary = P1Legendary.Create(10);
        SkillUseProfile baseProfile = SkillRules.BuildHeavyStrike(
            new SkillConfiguration(P1SkillIds.HeavyStrike, SkillSupport.None),
            legendary.Base.ToWeaponProfile(),
            100);
        SkillUseProfile modified = P1LegendaryRules.ApplyToHeavyStrike(baseProfile, legendary.LegendaryRule);

        Assert.True(modified.AttackIntervalTicks > baseProfile.AttackIntervalTicks);
        Assert.Equal(70, P1LegendaryRules.CalculateAftershockDamage(100, legendary.LegendaryRule));
    }

    private static ItemInstance Basic(string baseStableId) =>
        ItemGenerator.Generate(baseStableId, 1, ItemRarity.Basic, 1, $"test-{baseStableId}");
}
