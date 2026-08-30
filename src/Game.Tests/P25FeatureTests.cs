using GameForWork.Core.P1;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P1.Progression;
using GameForWork.Core.P14;
using GameForWork.Core.P18;
using GameForWork.Core.P21;
using GameForWork.Core.P23;
using GameForWork.Core.P24;
using GameForWork.Core.P25;

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
        Assert.Equal(0, bow);
        Assert.Equal(1, dagger);
        Assert.Equal(3, quiver);
        Assert.Equal(50, P25EquipmentArt.IconIndex(P1ItemBases.Get("p24.base.bow.6")));
        Assert.Throws<KeyNotFoundException>(() => P21ArtContract.ItemBaseIndex("p24.base.bow.1"));
        int[] equipmentIndexes = P24ItemCatalog.Bases.Select(P25EquipmentArt.IconIndex).ToArray();
        Assert.Equal(P24ItemCatalog.Bases.Count, equipmentIndexes.Distinct().Count());
        Assert.All(equipmentIndexes, index => Assert.InRange(index, 0, 59));
        int[] skillIndexes = P24SkillCatalog.Active.Select(skill => P21ArtContract.SkillStoneIndex(skill.Combat.StoneId))
            .Concat(P24SkillCatalog.Supports.Select(skill => P21ArtContract.SkillStoneIndex(skill.StoneId))).ToArray();
        Assert.Equal(90, skillIndexes.Length);
        Assert.Equal(Enumerable.Range(0, 90), skillIndexes.Order());
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
