using GameForWork.Core.P1;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P1.Progression;
using GameForWork.Core.P14;
using GameForWork.Core.P18;
using GameForWork.Core.P21;
using GameForWork.Core.P23;
using GameForWork.Core.P24;
using GameForWork.Core.P25;
using GameForWork.Core.P6;

namespace GameForWork.Tests;

public sealed class P25FeatureTests
{
    [Fact]
    public void EveryEquipmentBaseHasARollableImplicit()
    {
        Assert.Equal(130, P1ItemBases.All.Count);
        Assert.All(P1ItemBases.All, itemBase =>
        {
            Assert.NotEqual(ItemModifierKind.None, itemBase.ImplicitModifier);
            Assert.True(itemBase.ImplicitMinimumValue > 0);
            Assert.True(itemBase.ImplicitMaximumValue >= itemBase.ImplicitMinimumValue);
            Assert.False(string.IsNullOrWhiteSpace(itemBase.ImplicitText));
        });
    }

    [Fact]
    public void QuiverRequiresBowAndDualWieldRequiresOneHandMainHand()
    {
        ItemInstance bow = ItemGenerator.Generate("p24.base.bow.1", 20, ItemRarity.Basic, 1);
        ItemInstance dagger = ItemGenerator.Generate("p24.base.dagger.1", 20, ItemRarity.Basic, 2);
        ItemInstance secondDagger = ItemGenerator.Generate("p24.base.dagger.2", 20, ItemRarity.Basic, 3);
        ItemInstance quiver = ItemGenerator.Generate("p24.base.quiver.1", 20, ItemRarity.Basic, 4);
        var loadout = new EquipmentLoadout();

        Assert.False(loadout.TryEquip(EquipmentSlot.OffHand, quiver));
        Assert.True(loadout.TryEquip(EquipmentSlot.MainHand, bow));
        Assert.True(loadout.TryEquip(EquipmentSlot.OffHand, quiver));
        Assert.True(loadout.TryEquip(EquipmentSlot.MainHand, dagger));
        Assert.False(loadout.Items.ContainsKey(EquipmentSlot.OffHand));
        Assert.True(loadout.TryEquip(EquipmentSlot.OffHand, secondDagger));
    }

    [Fact]
    public void P24EquipmentUsesExplicitCategoryArtAndSkillStonesUseSemanticRows()
    {
        int bow = P25EquipmentArt.IconIndex(P1ItemBases.Get("p24.base.bow.1"));
        int dagger = P25EquipmentArt.IconIndex(P1ItemBases.Get("p24.base.dagger.1"));
        int quiver = P25EquipmentArt.IconIndex(P1ItemBases.Get("p24.base.quiver.1"));
        Assert.NotEqual(bow, dagger);
        Assert.NotEqual(bow, quiver);
        Assert.NotEqual(dagger, quiver);
        int[] equipmentIndexes = P24ItemCatalog.Bases.Select(P25EquipmentArt.IconIndex).ToArray();
        Assert.Equal(P24ItemCatalog.Bases.Count, equipmentIndexes.Distinct().Count());
        Assert.All(equipmentIndexes, index => Assert.InRange(index, 0, 129));
        int[] skillIndexes = P24SkillCatalog.Active.Select(skill => P21ArtContract.SkillStoneIndex(skill.Combat.StoneId))
            .Concat(P24SkillCatalog.Supports.Select(skill => P21ArtContract.SkillStoneIndex(skill.StoneId))).ToArray();
        Assert.Equal(90, skillIndexes.Length);
        Assert.Equal(Enumerable.Range(0, 90), skillIndexes.Order());
    }

    [Fact]
    public void EveryBaseAndLegendaryHasOneStableExplicitArtCell()
    {
        int[] baseIndexes = P1ItemBases.All.Select(P25EquipmentArt.IconIndex).Order().ToArray();
        Assert.Equal(Enumerable.Range(0, 130), baseIndexes);
        Assert.Equal(P14UniqueItems.All.Select(item => item.StableId), P25LegendaryArt.StableIds);
        Assert.Equal(2, P25LegendaryArt.IconIndex("core.unique.ravens_answer"));
        Assert.Equal(24, P25LegendaryArt.IconIndex("core.mythic.heart_of_ash"));
        Assert.Equal(ItemCategory.Helmet, P1ItemBases.Get(P14UniqueItems.All[2].BaseStableId).Category);
        Assert.Equal(ItemCategory.BodyArmor, P1ItemBases.Get(P14UniqueItems.All[24].BaseStableId).Category);
    }

    [Fact]
    public void WeaponSubtypesAndImportedBaseNamesAreSemantic()
    {
        Assert.Equal(WeaponFamily.Sword, P1ItemBases.Get("core.base.rusted_greatsword").WeaponFamily);
        Assert.Equal(WeaponFamily.Axe, P1ItemBases.Get("core.base.heavy_battleaxe").WeaponFamily);
        Assert.Equal(WeaponFamily.Mace, P1ItemBases.Get("core.base.pole_warhammer").WeaponFamily);
        Assert.Equal(WeaponFamily.Mace, P1ItemBases.Get("p19.base.flanged_mace").WeaponFamily);
        Assert.Equal("破血钉锤", P1ItemBases.Get("p19.base.flanged_mace").DisplayName);
        Assert.Equal("单手锤", P1ItemBases.Get("p19.base.flanged_mace").DetailedTypeName);
        Assert.DoesNotContain(P1ItemBases.All, item => item.DisplayName.Contains("远古", StringComparison.Ordinal) &&
            item.DisplayName.Any(char.IsDigit));
        Assert.All(P1ItemBases.All, item => Assert.DoesNotMatch("[A-Za-z]", item.ImplicitText));
    }

    [Fact]
    public void LegacySavedItemsRebindToTheCurrentSemanticBaseDefinition()
    {
        ItemBaseDefinition current = P1ItemBases.Get("p19.base.carnal_armour");
        ItemBaseDefinition legacy = current with { DisplayName = "远古胸甲89", ImplicitText = "legacy implicit" };
        var saved = new ItemInstance("p25-legacy-base", legacy, 94, ItemRarity.Rare, [], LinkedSocketCount: 4);

        ItemInstance restored = P6SocketRules.Ensure(saved);

        Assert.Equal("血肉战甲", restored.Base.DisplayName);
        Assert.Equal(current.ImplicitText, restored.Base.ImplicitText);
        Assert.Same(current, restored.Base);
        Assert.Equal(4, restored.LinkedSocketCount);
    }

    [Fact]
    public void EveryLegendaryHasConcreteRuleAffixesAndRuntimeHandler()
    {
        Assert.Equal(25, P14UniqueItems.All.Count);
        Assert.All(P14UniqueItems.All, definition =>
        {
            Assert.Contains(definition.RuleText, character => char.IsDigit(character));
            Assert.True(P25LegendaryRules.HasImplementation(definition.StableId));
            ItemInstance item = P14UniqueItems.Create(definition.StableId, 94, "p25-test-" + definition.StableId);
            Assert.InRange(item.Affixes.Count, 4, 6);
            Assert.Contains(item.Affixes, affix => affix.Definition.Position == AffixPosition.Prefix);
            Assert.Contains(item.Affixes, affix => affix.Definition.Position == AffixPosition.Suffix);
            Assert.Equal(item.Base.ImplicitMaximumValue, item.EffectiveImplicitValue);
            Assert.True(item.Quality >= 10);
        });
    }

    [Fact]
    public void SixStartGardensPreserveTreeSizeAndProvideEquipmentAndAttributeChoices()
    {
        Assert.Equal(1_200, P1PassiveTree.Nodes.Count);
        foreach (PassiveNodeDefinition start in P1PassiveTree.Nodes.Where(node => node.Kind == PassiveNodeKind.Start))
        {
            PassiveNodeDefinition[] garden = P1PassiveTree.Nodes.Where(node =>
                node.StableId.StartsWith($"core.passive.v3.start_garden.{start.Start.ToString().ToLowerInvariant()}.", StringComparison.Ordinal)).ToArray();
            Assert.Equal(24, garden.Length);
            Assert.Equal(3, garden.Count(node => node.StableId.Contains(".cluster.") && node.Kind == PassiveNodeKind.Notable));
            Assert.Equal(3, garden.Count(node => node.StableId.Contains(".attribute.") && node.Kind == PassiveNodeKind.Notable));
        }
    }

    [Fact]
    public void V03MaintainsThirtySixAscendancyBenchmarkBuilds()
    {
        P18BenchmarkBuild[] builds = P18BenchmarkBuilds.All.Concat(P231BenchmarkBuilds.All).ToArray();
        Assert.Equal(36, builds.Length);
        Assert.Equal(18, builds.Select(build => build.Ascendancy).Distinct().Count());
        Assert.All(builds.GroupBy(build => build.Ascendancy), group =>
        {
            Assert.Single(group, build => !build.EndgameGear);
            Assert.Single(group, build => build.EndgameGear);
        });
    }

    [Fact]
    public void VersionNineteenPreservesClassAndResourcesButRefundsTheOldTree()
    {
        P1GameSession current = P1GameSession.CreateNew(new PlayerIdentity("迁移测试", CharacterGender.Androgynous,
            CharacterSkinTone.Umber, CharacterHairStyle.Cropped, P23BaseClass.Monk), 0x2519UL, false);
        P1GameSessionSnapshot legacy = current.Capture() with
        {
            FormatVersion = 19,
            AllocatedPassives = ["core.passive.v3.cluster.00.00.small.09"],
            MemoryAshes = 17,
            MasterySelections = new Dictionary<string, int> { ["core.passive.v3.cluster.00.00.mastery"] = 0 },
            SocketedJewels = new Dictionary<string, PassiveJewelKind> { ["core.passive.v3.jewel.00.00"] = PassiveJewelKind.CrimsonMemory },
        };

        P1GameSession restored = P1GameSession.Restore(legacy);

        Assert.Equal(P23BaseClass.Monk, restored.Player.BaseClass);
        Assert.Empty(restored.Passives.Allocated);
        Assert.Empty(restored.Passives.MasterySelections);
        Assert.Empty(restored.Passives.SocketedJewels);
        Assert.Equal(17, restored.Passives.MemoryAshes);
    }
}
