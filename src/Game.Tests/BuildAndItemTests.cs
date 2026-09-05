using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Campaign.Progression;

namespace GameForWork.Tests;

public sealed class BuildAndItemTests
{
    [Fact]
    public void BuildsPassiveTreeContainsSixRegionsAndAttributeGrowth()
    {
        Assert.Equal(1_475, PassiveTree.Nodes.Count);
        Assert.Equal(6, PassiveTree.Nodes.Select(node => node.Branch).Distinct().Count());

        var allocation = new PassiveTreeAllocation(start: PassiveStartKind.Dexterity);
        Assert.True(allocation.TryAllocate("builds.path.ring.inner.e5.p01", 1));

        Assert.True(allocation.CalculateModifiers().Advanced!.Dexterity > 0);
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
    public void PassiveTreeHasBuildsNodeCounts()
    {
        Assert.Equal(1_475, PassiveTree.Nodes.Count);
        Assert.Equal(6, PassiveTree.Nodes.Count(node => node.Kind == PassiveNodeKind.Start));
        Assert.Equal(1_054, PassiveTree.Nodes.Count(node => node.Kind == PassiveNodeKind.Small));
        Assert.Equal(223, PassiveTree.Nodes.Count(node => node.Kind == PassiveNodeKind.Notable));
        Assert.Equal(0, PassiveTree.Nodes.Count(node => node.Kind == PassiveNodeKind.Rule));
        Assert.Equal(168, PassiveTree.Nodes.Count(node => node.Kind == PassiveNodeKind.Mastery));
        Assert.Equal(24, PassiveTree.Nodes.Count(node => node.Kind == PassiveNodeKind.JewelSocket));
        Assert.Equal(149, PassiveTreeAllocation.MaximumAllocatedPoints);
    }

    [Fact]
    public void PassiveTreeLayoutFitsItsDeterministicSquareCanvas()
    {
        Assert.All(PassiveTree.Nodes, node =>
        {
            Assert.InRange(node.X, -PassiveTree.LayoutExtent, PassiveTree.LayoutExtent);
            Assert.InRange(node.Y, -PassiveTree.LayoutExtent, PassiveTree.LayoutExtent);
        });
        Assert.Equal(PassiveTree.Nodes.Count,
            PassiveTree.Nodes.Select(node => (MathF.Round(node.X, 2), MathF.Round(node.Y, 2))).Distinct().Count());
    }

    [Fact]
    public void BuildsClustersUseConfirmedMediumAndLargeTemplates()
    {
        PassiveNodeDefinition[] clusterSmall = PassiveTree.Nodes
            .Where(node => node.Kind == PassiveNodeKind.Small && node.StableId.StartsWith("builds.cluster.", StringComparison.Ordinal))
            .ToArray();
        IGrouping<string, PassiveNodeDefinition>[] clusters = clusterSmall
            .GroupBy(node => string.Join('.', node.StableId.Split('.')[..^1]))
            .ToArray();

        Assert.Equal(168, clusters.Length);
        Assert.Equal(131, clusters.Count(cluster => cluster.Count() == 4));
        Assert.Equal(37, clusters.Count(cluster => cluster.Count() == 8));
        Assert.Equal(6, PassiveTree.Nodes.Count(node => node.Kind == PassiveNodeKind.Start));
        Assert.All(PassiveTree.Nodes.Where(node => node.Kind == PassiveNodeKind.Start), node =>
        {
            Assert.Empty(node.Effects);
            Assert.Equal(3, PassiveTree.Neighbors(node.StableId).Count);
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
        PassiveEffect[] effects = PassiveTree.Nodes
            .Where(node => node.Kind == PassiveNodeKind.Small && node.StableId.StartsWith("builds.cluster.", StringComparison.Ordinal))
            .SelectMany(node => node.Effects).Where(effect => damageKinds.Contains(effect.Kind)).ToArray();

        Assert.NotEmpty(effects);
        Assert.All(effects, effect => Assert.InRange(effect.Value, 300, 6_000));
    }

    [Fact]
    public void PassiveAllocationEnforcesPathPointsAndLeafRefund()
    {
        var allocation = new PassiveTreeAllocation();

        Assert.False(allocation.TryAllocate("builds.path.ring.inner.e0.p07", 10));
        Assert.False(allocation.TryAllocate(PassiveTree.StartNode(PassiveStartKind.Physique), 1));
        Assert.True(allocation.TryAllocate("builds.path.ring.inner.e0.p01", 1));
        Assert.False(allocation.TryAllocate("builds.path.ring.inner.e0.p02", 1));
        Assert.True(allocation.TryAllocate("builds.path.ring.inner.e0.p02", 2));
        Assert.False(allocation.TryRefund("builds.path.ring.inner.e0.p01"));
        Assert.True(allocation.TryRefund("builds.path.ring.inner.e0.p02"));
        Assert.Equal(4, allocation.MemoryAshes);
    }

    [Fact]
    public void PassiveResetCostsTenMemoryAshes()
    {
        var allocation = new PassiveTreeAllocation(10);
        Assert.True(allocation.TryAllocate("builds.path.ring.inner.e0.p01", 1));

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

        Assert.Contains(Affixes.For(ItemCategory.BodyArmor, 120), affix => affix.Tier == 1);
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
                new SkillConfiguration(SkillIds.HeavyStrike, SkillSupport.AttackSpeed | SkillSupport.Bleed),
                new SkillConfiguration(SkillIds.WarCry, SkillSupport.IncreasedArea),
            ],
            summary);
        Assert.True(capacity.IsValid);
    }

    [Fact]
    public void NaturalAffixPoolDoesNotGrantSupportLinks()
    {
        Assert.DoesNotContain(Affixes.All, affix =>
            affix.Source == "Natural" && affix.EffectComponents.Any(effect =>
                effect.Kind == ItemModifierKind.ExtraSupportLinkCapacity));
    }

    [Fact]
    public void BuildAssemblerAppliesEquipmentAndPassiveBonuses()
    {
        var loadout = new EquipmentLoadout();
        Assert.True(loadout.TryEquip(EquipmentSlot.MainHand, Basic("core.base.rusted_greatsword")));
        Assert.True(loadout.TryEquip(EquipmentSlot.RingLeft, Basic("core.base.life_ring")));
        var passives = new PassiveTreeAllocation();
        Assert.True(passives.TryAllocatePath("builds.cluster.v0_i03.small01", 20));

        AssembledCharacterBuild build = CharacterBuildAssembler.Assemble(
            1,
            CharacterAttributes.IronOathStarting,
            loadout,
            passives,
            new SkillConfiguration(SkillIds.HeavyStrike, SkillSupport.None));

        Assert.True(build.Sheet.MaximumLife().Value > 108);
        Assert.True(build.Passives.Advanced!.IncreasedAttackSkillDamageBasisPoints > 0);
        Assert.NotNull(build.Equipment.Weapon);
    }

    [Fact]
    public void FlaskEffectAndLegendaryRulesAreDeterministic()
    {
        var flask = new LifeFlaskState(new LifeFlaskDefinition(40, 30, 10));
        Assert.Equal(48, flask.TryUse(100, 2_000));
        Assert.Equal(20, flask.Charges);

        ItemInstance legendary = Legendary.Create(10);
        SkillUseProfile baseProfile = SkillRules.BuildHeavyStrike(
            new SkillConfiguration(SkillIds.HeavyStrike, SkillSupport.None),
            legendary.Base.ToWeaponProfile(),
            100);
        SkillUseProfile modified = LegendaryRules.ApplyToHeavyStrike(baseProfile, legendary.LegendaryRule);

        Assert.True(modified.AttackIntervalTicks > baseProfile.AttackIntervalTicks);
        Assert.Equal(70, LegendaryRules.CalculateAftershockDamage(100, legendary.LegendaryRule));
    }

    private static ItemInstance Basic(string baseStableId) =>
        ItemGenerator.Generate(baseStableId, 1, ItemRarity.Basic, 1, $"test-{baseStableId}");
}
