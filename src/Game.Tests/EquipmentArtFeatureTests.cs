using GameForWork.Core.Campaign;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Campaign.Progression;
using GameForWork.Core.Content;
using GameForWork.Core.Ascendancies;
using GameForWork.Core.Art;
using GameForWork.Core.Characters;
using GameForWork.Core.Archetypes;
using GameForWork.Core.Equipment;
using GameForWork.Core.Skills;

namespace GameForWork.Tests;

public sealed class EquipmentArtFeatureTests
{
    [Fact]
    public void EveryEquipmentBaseHasARollableImplicit()
    {
        Assert.Equal(244, ItemBases.All.Count);
        Assert.All(ItemBases.All, itemBase =>
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
        ItemInstance bow = ItemGenerator.Generate("archetypes.base.bow.1", 20, ItemRarity.Basic, 1);
        ItemInstance dagger = ItemGenerator.Generate("archetypes.base.dagger.1", 20, ItemRarity.Basic, 2);
        ItemInstance secondDagger = ItemGenerator.Generate("archetypes.base.dagger.2", 20, ItemRarity.Basic, 3);
        ItemInstance quiver = ItemGenerator.Generate("archetypes.base.quiver.1", 20, ItemRarity.Basic, 4);
        var loadout = new EquipmentLoadout();

        Assert.False(loadout.TryEquip(EquipmentSlot.OffHand, quiver));
        Assert.True(loadout.TryEquip(EquipmentSlot.MainHand, bow));
        Assert.True(loadout.TryEquip(EquipmentSlot.OffHand, quiver));
        Assert.True(loadout.TryEquip(EquipmentSlot.MainHand, dagger));
        Assert.False(loadout.Items.ContainsKey(EquipmentSlot.OffHand));
        Assert.True(loadout.TryEquip(EquipmentSlot.OffHand, secondDagger));
    }

    [Fact]
    public void ArchetypesEquipmentUsesExplicitCategoryArtAndSkillStonesUseSemanticRows()
    {
        int bow = EquipmentBaseArt.IconIndex(ItemBases.Get("archetypes.base.bow.1"));
        int dagger = EquipmentBaseArt.IconIndex(ItemBases.Get("archetypes.base.dagger.1"));
        int quiver = EquipmentBaseArt.IconIndex(ItemBases.Get("archetypes.base.quiver.1"));
        Assert.NotEqual(bow, dagger);
        Assert.NotEqual(bow, quiver);
        Assert.NotEqual(dagger, quiver);
        ItemBaseDefinition[] archetypesBases = EquipmentCatalog.Snapshot.Bases
            .Where(value => value.LegacyIds.Any(id => id.StartsWith("archetypes.base.", StringComparison.Ordinal)))
            .Select(value => EquipmentCatalog.GetBase(value.Id)).ToArray();
        int[] equipmentIndexes = archetypesBases.Select(EquipmentBaseArt.IconIndex).ToArray();
        Assert.Equal(archetypesBases.Length, equipmentIndexes.Distinct().Count());
        Assert.All(equipmentIndexes, index => Assert.InRange(index, 0, 243));
        int[] skillIndexes = ArchetypeSkillDefinitions.Active.Select(skill => ArtContract.SkillStoneIndex(skill.Combat.StoneId))
            .Concat(ArchetypeSkillDefinitions.Supports.Select(skill => ArtContract.SkillStoneIndex(skill.StoneId))).ToArray();
        Assert.Equal(90, skillIndexes.Length);
        Assert.Equal(90, skillIndexes.Distinct().Count());
        Assert.All(skillIndexes, index => Assert.InRange(index, 0, SkillStoneArt.StableIds.Count - 1));
    }

    [Fact]
    public void EveryBaseAndLegendaryHasOneStableExplicitArtCell()
    {
        int[] baseIndexes = ItemBases.All.Select(EquipmentBaseArt.IconIndex).ToArray();
        Assert.All(baseIndexes, index => Assert.InRange(index, 0, 243));
        Assert.Equal(244, baseIndexes.Distinct().Count());
        Assert.Equal(baseIndexes, ItemBases.All.Select(EquipmentBaseArt.IconIndex));
        Assert.Equal(UniqueItems.All.Select(item => item.StableId), EquipmentLegendaryArt.StableIds);
        Assert.Equal(2, EquipmentLegendaryArt.IconIndex("core.unique.ravens_answer"));
        Assert.Equal(40, EquipmentLegendaryArt.IconIndex("core.mythic.heart_of_ash"));
        Assert.Equal(55, UniqueItems.All.Select(item => EquipmentLegendaryArt.IconIndex(item.StableId)).Distinct().Count());
        Assert.Equal(ItemCategory.Helmet, ItemBases.Get(UniqueItems.All[2].BaseStableId).Category);
        Assert.Contains(UniqueItems.All.Where(item => item.Mythic), item =>
            ItemBases.Get(item.BaseStableId).Category == ItemCategory.BodyArmor);
    }

    [Fact]
    public void WeaponSubtypesAndImportedBaseNamesAreSemantic()
    {
        Assert.Equal(WeaponFamily.Sword, ItemBases.Get("core.base.rusted_greatsword").WeaponFamily);
        Assert.Equal(WeaponFamily.Axe, ItemBases.Get("core.base.heavy_battleaxe").WeaponFamily);
        Assert.Equal(WeaponFamily.Mace, ItemBases.Get("core.base.pole_warhammer").WeaponFamily);
        Assert.Equal(WeaponFamily.Mace, ItemBases.Get("equipmentImport.base.flanged_mace").WeaponFamily);
        Assert.Equal("破血钉锤", ItemBases.Get("equipmentImport.base.flanged_mace").DisplayName);
        Assert.Equal("单手锤", ItemBases.Get("equipmentImport.base.flanged_mace").DetailedTypeName);
        Assert.DoesNotContain(ItemBases.All, item => item.DisplayName.Contains("远古", StringComparison.Ordinal) &&
            item.DisplayName.Any(char.IsDigit));
        Assert.All(ItemBases.All, item => Assert.DoesNotMatch("[A-Za-z]", item.ImplicitText));
    }

    [Fact]
    public void LegacySavedItemsRebindToTheCurrentSemanticBaseDefinition()
    {
        ItemBaseDefinition current = ItemBases.Get("equipmentImport.base.carnal_armour");
        ItemBaseDefinition legacy = current with { DisplayName = "远古胸甲89", ImplicitText = "legacy implicit" };
        var saved = new ItemInstance("equipmentArt-legacy-base", legacy, 94, ItemRarity.Rare, [], LinkedSocketCount: 4);

        ItemInstance restored = SocketRules.Ensure(saved);

        Assert.Equal("血肉战甲", restored.Base.DisplayName);
        Assert.Equal(current.ImplicitText, restored.Base.ImplicitText);
        Assert.Same(current, restored.Base);
        Assert.Equal(4, restored.LinkedSocketCount);
    }

    [Fact]
    public void EveryLegendaryHasConcreteRuleAffixesAndRuntimeHandler()
    {
        Assert.Equal(55, UniqueItems.All.Count);
        Assert.Contains(UniqueItems.All, definition => definition.LegendaryAffixes.Count > 1);
        Assert.Equal(UniqueItems.All.SelectMany(definition => definition.LegendaryAffixes).Count(),
            UniqueItems.All.SelectMany(definition => definition.LegendaryAffixes)
                .Select(affix => affix.StableId).Distinct(StringComparer.Ordinal).Count());
        Assert.All(UniqueItems.All, definition =>
        {
            Assert.Contains(definition.RuleText, character => char.IsDigit(character));
            Assert.NotEmpty(definition.LegendaryAffixes);
            Assert.Contains(EquipmentRuleRegistry.All, registration =>
                registration.SourceDefinitionId == EquipmentCatalog.LegendaryItems.Single(entry => entry.DisplayName == definition.DisplayName).Id);
            ItemInstance item = UniqueItems.Create(definition.StableId, 94, "equipmentArt-test-" + definition.StableId);
            Assert.InRange(item.Affixes.Count, 2, 6);
            Assert.All(item.Affixes, affix => Assert.Equal("传奇固定", affix.Definition.Source));
            Assert.Contains(item.Affixes, affix => affix.Definition.Position == AffixPosition.Prefix);
            Assert.Contains(item.Affixes, affix => affix.Definition.Position == AffixPosition.Suffix);
            Assert.Equal(item.Base.ImplicitMaximumValue, item.EffectiveImplicitValue);
            Assert.True(item.Quality >= 10);
        });
    }

    [Fact]
    public void BuildsSixStartsAndAttributeMajorsUseFinalTopology()
    {
        Assert.Equal(1_475, PassiveTree.Nodes.Count);
        Assert.Equal(18, PassiveTree.Nodes.Count(node => node.StableId.StartsWith("builds.attr.major.", StringComparison.Ordinal)));
        foreach (PassiveNodeDefinition start in PassiveTree.Nodes.Where(node => node.Kind == PassiveNodeKind.Start))
        {
            Assert.Equal(3, PassiveTree.Neighbors(start.StableId).Count);
        }
    }

    [Fact]
    public void V03MaintainsThirtySixAscendancyBenchmarkBuilds()
    {
        BenchmarkBuild[] builds = WarriorBenchmarkBuilds.All.Concat(ClassBenchmarkBuilds.All).ToArray();
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
        GameSession current = GameSession.CreateNew(new PlayerIdentity("迁移测试", CharacterGender.Androgynous,
            CharacterSkinTone.Umber, CharacterHairStyle.Cropped, BaseClass.Monk), 0x2519UL, false);
        GameSessionSnapshot legacy = current.Capture() with
        {
            FormatVersion = 19,
            AllocatedPassives = ["core.passive.v3.cluster.00.00.small.09"],
            MemoryAshes = 17,
            MasterySelections = new Dictionary<string, int> { ["core.passive.v3.cluster.00.00.mastery"] = 0 },
            SocketedJewels = new Dictionary<string, PassiveJewelKind> { ["core.passive.v3.jewel.00.00"] = PassiveJewelKind.CrimsonMemory },
        };

        GameSession restored = GameSession.Restore(legacy);

        Assert.Equal(BaseClass.Monk, restored.Player.BaseClass);
        Assert.Empty(restored.Passives.Allocated);
        Assert.Empty(restored.Passives.MasterySelections);
        Assert.Empty(restored.Passives.SocketedJewels);
        Assert.Equal(17, restored.Passives.MemoryAshes);
    }
}
