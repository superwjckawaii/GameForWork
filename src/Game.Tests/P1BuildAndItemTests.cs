using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P1.Progression;

namespace GameForWork.Tests;

public sealed class P1BuildAndItemTests
{
    [Fact]
    public void P3PassiveTreeContainsTenOriginalClustersAndMovementGrowth()
    {
        Assert.Equal(180, P1PassiveTree.Nodes.Count);
        Assert.Equal(10, P1PassiveTree.Nodes.Select(node => node.Branch).Distinct().Count());

        var allocation = new PassiveTreeAllocation();
        for (int index = 1; index <= 3; index++)
        {
            Assert.True(allocation.TryAllocate($"core.passive.mobility.{index}", 70));
        }

        Assert.True(allocation.CalculateModifiers().IncreasedMovementSpeedBasisPoints > 0);
    }

    [Fact]
    public void ExperienceTableReachesLevelSixtyAndFiftyNineLevelPoints()
    {
        var progression = new CharacterProgression();

        ExperienceGainResult result = progression.AddExperience(CharacterProgression.TotalExperienceToCap);

        Assert.Equal(60, progression.Level);
        Assert.Equal(59, progression.EarnedPassivePoints);
        Assert.Equal(59, result.PassivePointsGained);
        Assert.True(result.ReachedLevelCap);
        Assert.Equal(1_140, CharacterProgression.RequiredExperience(9));
        progression.AddExperience(10_000);
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
    public void PassiveTreeHasP3NodeCounts()
    {
        Assert.Equal(180, P1PassiveTree.Nodes.Count);
        Assert.Equal(156, P1PassiveTree.Nodes.Count(node => node.Kind == PassiveNodeKind.Small));
        Assert.Equal(16, P1PassiveTree.Nodes.Count(node => node.Kind == PassiveNodeKind.Notable));
        Assert.Equal(8, P1PassiveTree.Nodes.Count(node => node.Kind == PassiveNodeKind.Rule));
        Assert.Equal(70, PassiveTreeAllocation.MaximumAllocatedPoints);
    }

    [Fact]
    public void PassiveAllocationEnforcesPathPointsAndLeafRefund()
    {
        var allocation = new PassiveTreeAllocation();

        Assert.False(allocation.TryAllocate("core.passive.heavy.2", 10));
        Assert.True(allocation.TryAllocate("core.passive.heavy.1", 1));
        Assert.False(allocation.TryAllocate("core.passive.heavy.2", 1));
        Assert.True(allocation.TryAllocate("core.passive.heavy.2", 2));
        Assert.False(allocation.TryRefund("core.passive.heavy.1"));
        Assert.True(allocation.TryRefund("core.passive.heavy.2"));
        Assert.Equal(4, allocation.MemoryAshes);
    }

    [Fact]
    public void PassiveResetCostsTenMemoryAshes()
    {
        var allocation = new PassiveTreeAllocation(10);
        Assert.True(allocation.TryAllocate("core.passive.defense.1", 1));

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
    [InlineData(ItemRarity.Rare, 2, 4)]
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
            Assert.All(lowLevel.Affixes, affix => Assert.Equal(2, affix.Definition.Tier));
            Assert.DoesNotContain(
                lowLevel.Affixes,
                affix => affix.Definition.ModifierKind is ItemModifierKind.IncreasedEvasionBasisPoints or
                    ItemModifierKind.IncreasedShieldBasisPoints);
        }

        Assert.Contains(P1Affixes.For(ItemCategory.BodyArmor, 6), affix => affix.Tier == 1);
    }

    [Fact]
    public void IronRingImplicitRollIsOneOrTwo()
    {
        for (ulong seed = 1; seed <= 20; seed++)
        {
            ItemInstance ring = ItemGenerator.Generate("core.base.iron_ring", 1, ItemRarity.Basic, seed);
            Assert.InRange(ring.ImplicitValue, 1, 2);
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
        Assert.True(passives.TryAllocate("core.passive.heavy.1", 1));

        AssembledCharacterBuild build = CharacterBuildAssembler.Assemble(
            1,
            CharacterAttributes.IronOathStarting,
            loadout,
            passives,
            new SkillConfiguration(P1SkillIds.HeavyStrike, SkillSupport.None));

        Assert.Equal(116, build.Sheet.MaximumLife().Value);
        Assert.Equal(500, build.IncreasedAttackDamageBasisPoints);
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
