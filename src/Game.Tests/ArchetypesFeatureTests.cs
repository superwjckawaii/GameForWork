using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Management;
using GameForWork.Core.Spatial;
using GameForWork.Core.Skills;
using GameForWork.Core.SkillCatalog;
using GameForWork.Core.Characters;
using GameForWork.Core.Archetypes;
using GameForWork.Core.Equipment;

namespace GameForWork.Tests;

public sealed class ArchetypesFeatureTests
{
    [Fact]
    public void CatalogCompletesFiveClassBuildChains()
    {
        Assert.Equal(50, ArchetypeSkillDefinitions.Active.Count);
        Assert.Equal(40, ArchetypeSkillDefinitions.Supports.Count);
        Assert.Equal(86, SkillDefinitions.All.Count);
        Assert.Equal(98, SkillStoneCatalog.All.Count(value => value.Kind == SkillStoneKind.Support));
        Assert.Equal(244, ItemBases.All.Count);
        Assert.Equal(50, EquipmentCatalog.Snapshot.Bases.Count(value => value.LegacyIds.Any(id => id.StartsWith("archetypes.base.", StringComparison.Ordinal))));
        Assert.Equal(49, GuideCatalog.SpecialAffixFamilies.Count);
        Assert.DoesNotContain(EquipmentCatalog.Affixes, value => value.StableFamilyId == "archetypes.affix.rune.spellblade");
        Assert.All(Enum.GetValues<BaseClass>().Where(value => value != BaseClass.Fighter), theme =>
            Assert.Equal(10, ArchetypeSkillDefinitions.Active.Count(value => value.Theme == theme)));
    }

    [Fact]
    public void ArchetypesSkillsAndSupportsResolveThroughExistingSocketPipeline()
    {
        ArchetypeSkillDefinition active = ArchetypeSkillDefinitions.Active.Single(value => value.Combat.DisplayName == "穿云箭");
        ArchetypeSupportDefinition support = ArchetypeSkillDefinitions.Supports.Single(value => value.DisplayName == "精准穿透");
        SkillStoneDefinition activeStone = SkillStoneCatalog.Get(active.Combat.StoneId);
        SkillStoneDefinition supportStone = SkillStoneCatalog.Get(support.StoneId);
        Assert.True(SkillCompatibility.Check(activeStone, supportStone).Compatible);

        ResolvedSkill plain = CombatSkillRules.Resolve(new SkillConfiguration(active.Combat.SkillId, SkillSupport.None), 1_000);
        ResolvedSkill linked = CombatSkillRules.Resolve(new SkillConfiguration(active.Combat.SkillId, SkillSupport.None,
            ArchetypeSupports: [support.Mechanic]), 1_000);
        Assert.Equal(2, linked.PierceCount);
        Assert.True(linked.DamageMultiplierBasisPoints < plain.DamageMultiplierBasisPoints);
    }

    [Fact]
    public void SkillStonesAreCapabilityRestrictedButNotClassLocked()
    {
        Assert.All(ArchetypeSkillDefinitions.Active, value =>
        {
            SkillStoneDefinition stone = SkillStoneCatalog.Get(value.Combat.StoneId);
            Assert.DoesNotContain("职业", stone.Description, StringComparison.Ordinal);
            Assert.Equal(SkillStoneKind.Active, stone.Kind);
        });
    }

    [Fact]
    public void BowCanUseQuiverButOtherTwoHandWeaponsCannot()
    {
        ItemInstance bow = Item("bow", ItemBases.All.First(value => value.ItemTags.Contains("bow")));
        ItemInstance quiver = Item("quiver", ItemBases.All.First(value => value.ItemTags.Contains("quiver")));
        var loadout = new EquipmentLoadout();
        Assert.True(loadout.TryEquip(EquipmentSlot.MainHand, bow));
        Assert.True(loadout.TryEquip(EquipmentSlot.OffHand, quiver));
        Assert.False(loadout.CalculateSummary().HasShield);

        ItemInstance sword = Item("sword", ItemBases.All.First(value => value.Category == ItemCategory.TwoHandWeapon &&
            !value.ItemTags.Contains("bow")));
        Assert.True(loadout.TryEquip(EquipmentSlot.MainHand, sword));
        Assert.False(loadout.Items.ContainsKey(EquipmentSlot.OffHand));
    }

    [Fact]
    public void IndependentUnitCapsAndBattleLifetimeAreEnforced()
    {
        var roster = new CombatRoster { AttachedMercenaryAllowed = true };
        roster.StartBattle("hero", 100, "merc", 80);
        Assert.Equal(16, roster.InstantiateArmy("hero", CombatUnitKind.Minion, 99, 20, 0, 99).Count);
        Assert.Equal(8, roster.InstantiateArmy("hero", CombatUnitKind.Construct, 99, 20, 0, 99).Count);
        Assert.Equal(6, roster.InstantiateArmy("hero", CombatUnitKind.Phantom, 99, 20, 0, 99).Count);
        Assert.Single(roster.InstantiateArmy("hero", CombatUnitKind.Companion, 99, 20, 0, 99));
        Assert.Equal(6, roster.Expire(80));
        Assert.False(roster.PartyFailed);
        Assert.True(roster.Damage("hero", 100));
        Assert.False(roster.PartyFailed);
        Assert.True(roster.Damage("merc", 80));
        Assert.True(roster.PartyFailed);
    }

    [Fact]
    public void TrapsExpireTriggerAtTwoMetersAndReplaceOldest()
    {
        var roster = new CombatRoster();
        roster.StartBattle("hero", 100);
        CombatUnit first = roster.PlaceTrap("hero", 1, 1, 3);
        roster.PlaceTrap("hero", 1, 2, 3);
        roster.PlaceTrap("hero", 1, 3, 3);
        CombatUnit fourth = roster.PlaceTrap("hero", 1, 4, 3);
        Assert.DoesNotContain(roster.Units, value => value.StableId == first.StableId);
        Assert.Equal(CombatRoster.TrapTriggerRadiusRaw, fourth.TriggerRadiusRaw);
        Assert.Equal(3, roster.Units.Count(value => value.Kind == CombatUnitKind.Trap));
        Assert.Equal(3, roster.Expire(164));
    }

    [Fact]
    public void MainSkillSelectsConfirmedCombatDistance()
    {
        ArchetypeSkillDefinition bow = ArchetypeSkillDefinitions.Active.Single(value => value.Combat.DisplayName == "穿云箭");
        ArchetypeSkillDefinition summon = ArchetypeSkillDefinitions.Active.Single(value => value.Combat.DisplayName == "召唤骸卫");
        ArchetypeSkillDefinition spell = ArchetypeSkillDefinitions.Active.Single(value => value.Combat.DisplayName == "熔火弹");
        ArchetypeSkillDefinition melee = ArchetypeSkillDefinitions.Active.Single(value => value.Combat.DisplayName == "连环拳");
        Assert.Equal(8_000, UnitAiRules.ForMainSkill(bow.Combat, CombatUnitKind.Hero).PreferredDistanceRaw);
        Assert.Equal(9_000, UnitAiRules.ForMainSkill(summon.Combat, CombatUnitKind.Hero).PreferredDistanceRaw);
        Assert.Equal(7_000, UnitAiRules.ForMainSkill(spell.Combat, CombatUnitKind.Hero).PreferredDistanceRaw);
        Assert.Equal(1_500, UnitAiRules.ForMainSkill(melee.Combat, CombatUnitKind.Hero).PreferredDistanceRaw);
    }

    [Fact]
    public void CurrentGuideContractCoversEveryItemMechanicFamily()
    {
        Assert.Contains(GuideCatalog.Entries, value => value.StableId == "archetypes.guide.item_families" && value.Rules.Count == 49);
    }

    [Fact]
    public void ArchetypesActiveExecutesInSpatialCombat()
    {
        ArchetypeSkillDefinition active = ArchetypeSkillDefinitions.Active.Single(value => value.Combat.DisplayName == "穿云箭");
        var configuration = new SkillConfiguration(active.Combat.SkillId, SkillSupport.None, 1,
            ArchetypeSupports: [SupportMechanic.PrecisionPierce]);
        var fallback = new SkillConfiguration(SkillIds.HeavyStrike, SkillSupport.None, 100);
        var build = new TeamBuild(
            new CharacterSheet(60, new CharacterAttributes(180, 260, 120, 100),
                new DefensiveEquipment(500, 500, 100), FlatMaximumLife: 2_000),
            new WeaponProfile("archetypes.test.bow", 300, 400, 1_600, 800),
            fallback, UseWarCry: false, FlatAccuracy: 2_000, IncreasedDamageBasisPoints: 8_000,
            ActiveSkills: [configuration, fallback]);

        NodeCombatResult result = new SpatialCombatRunner().Run(new NodeCombatRequest(
            build, 1, 10, 2, HasElite: false, HasBoss: false, AbyssRoute: false, Formation: 0), 0x24);

        Assert.Contains(result.Events, value => value.Kind == SpatialEventKind.SkillEffect &&
            value.Detail.Contains(active.Combat.SkillId, StringComparison.Ordinal));
    }

    private static ItemInstance Item(string id, ItemBaseDefinition definition) =>
        new(id, definition, 80, ItemRarity.Rare, [], ImplicitValue: 0);
}
