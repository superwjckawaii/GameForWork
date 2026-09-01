using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P1.World;
using GameForWork.Core.P2;
using GameForWork.Core.P4;
using GameForWork.Core.P6;
using GameForWork.Core.P17;
using GameForWork.Core.P23;
using GameForWork.Core.P24;

namespace GameForWork.Tests;

public sealed class P24FeatureTests
{
    [Fact]
    public void CatalogCompletesFiveClassBuildChains()
    {
        Assert.Equal(50, P24SkillCatalog.Active.Count);
        Assert.Equal(40, P24SkillCatalog.Supports.Count);
        Assert.Equal(80, P1Skills.All.Count);
        Assert.Equal(88, P2SkillStones.All.Count(value => value.Kind == SkillStoneKind.Support));
        Assert.Equal(148, P1ItemBases.All.Count);
        Assert.Equal(50, P24ItemCatalog.Bases.Count);
        Assert.Equal(49, P24ItemCatalog.Families.Count);
        Assert.Equal(147, P24ItemCatalog.Affixes.Count);
        Assert.DoesNotContain(P24ItemCatalog.Families, value => value.StableId == "p24.affix.rune.spellblade");
        Assert.All(Enum.GetValues<P23BaseClass>().Where(value => value != P23BaseClass.Fighter), theme =>
            Assert.Equal(10, P24SkillCatalog.Active.Count(value => value.Theme == theme)));
    }

    [Fact]
    public void P24SkillsAndSupportsResolveThroughExistingSocketPipeline()
    {
        P24ActiveSkillDefinition active = P24SkillCatalog.Active.Single(value => value.Combat.DisplayName == "穿云箭");
        P24SupportSkillDefinition support = P24SkillCatalog.Supports.Single(value => value.DisplayName == "精准穿透");
        SkillStoneDefinition activeStone = P2SkillStones.Get(active.Combat.StoneId);
        SkillStoneDefinition supportStone = P2SkillStones.Get(support.StoneId);
        Assert.True(P6SkillCompatibility.Check(activeStone, supportStone).Compatible);

        P6ResolvedSkill plain = P6CombatSkillRules.Resolve(new SkillConfiguration(active.Combat.SkillId, SkillSupport.None), 1_000);
        P6ResolvedSkill linked = P6CombatSkillRules.Resolve(new SkillConfiguration(active.Combat.SkillId, SkillSupport.None,
            P24Supports: [support.Mechanic]), 1_000);
        Assert.Equal(2, linked.PierceCount);
        Assert.True(linked.DamageMultiplierBasisPoints < plain.DamageMultiplierBasisPoints);
    }

    [Fact]
    public void SkillStonesAreCapabilityRestrictedButNotClassLocked()
    {
        Assert.All(P24SkillCatalog.Active, value =>
        {
            SkillStoneDefinition stone = P2SkillStones.Get(value.Combat.StoneId);
            Assert.DoesNotContain("职业", stone.Description, StringComparison.Ordinal);
            Assert.Equal(SkillStoneKind.Active, stone.Kind);
        });
    }

    [Fact]
    public void BowCanUseQuiverButOtherTwoHandWeaponsCannot()
    {
        ItemInstance bow = Item("bow", P24ItemCatalog.Bases.First(value => value.ItemTags.Contains("bow")));
        ItemInstance quiver = Item("quiver", P24ItemCatalog.Bases.First(value => value.ItemTags.Contains("quiver")));
        var loadout = new EquipmentLoadout();
        Assert.True(loadout.TryEquip(EquipmentSlot.MainHand, bow));
        Assert.True(loadout.TryEquip(EquipmentSlot.OffHand, quiver));
        Assert.False(loadout.CalculateSummary().HasShield);

        ItemInstance sword = Item("sword", P1ItemBases.All.First(value => value.Category == ItemCategory.TwoHandWeapon &&
            !value.ItemTags.Contains("bow")));
        Assert.True(loadout.TryEquip(EquipmentSlot.MainHand, sword));
        Assert.False(loadout.Items.ContainsKey(EquipmentSlot.OffHand));
    }

    [Fact]
    public void IndependentUnitCapsAndBattleLifetimeAreEnforced()
    {
        var roster = new P24CombatRoster { AttachedMercenaryAllowed = true };
        roster.StartBattle("hero", 100, "merc", 80);
        Assert.Equal(16, roster.InstantiateArmy("hero", P24CombatUnitKind.Minion, 99, 20, 0, 99).Count);
        Assert.Equal(8, roster.InstantiateArmy("hero", P24CombatUnitKind.Construct, 99, 20, 0, 99).Count);
        Assert.Equal(6, roster.InstantiateArmy("hero", P24CombatUnitKind.Phantom, 99, 20, 0, 99).Count);
        Assert.Single(roster.InstantiateArmy("hero", P24CombatUnitKind.Companion, 99, 20, 0, 99));
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
        var roster = new P24CombatRoster();
        roster.StartBattle("hero", 100);
        P24CombatUnit first = roster.PlaceTrap("hero", 1, 1, 3);
        roster.PlaceTrap("hero", 1, 2, 3);
        roster.PlaceTrap("hero", 1, 3, 3);
        P24CombatUnit fourth = roster.PlaceTrap("hero", 1, 4, 3);
        Assert.DoesNotContain(roster.Units, value => value.StableId == first.StableId);
        Assert.Equal(P24CombatRoster.TrapTriggerRadiusRaw, fourth.TriggerRadiusRaw);
        Assert.Equal(3, roster.Units.Count(value => value.Kind == P24CombatUnitKind.Trap));
        Assert.Equal(3, roster.Expire(164));
    }

    [Fact]
    public void MainSkillSelectsConfirmedCombatDistance()
    {
        P24ActiveSkillDefinition bow = P24SkillCatalog.Active.Single(value => value.Combat.DisplayName == "穿云箭");
        P24ActiveSkillDefinition summon = P24SkillCatalog.Active.Single(value => value.Combat.DisplayName == "召唤骸卫");
        P24ActiveSkillDefinition spell = P24SkillCatalog.Active.Single(value => value.Combat.DisplayName == "熔火弹");
        P24ActiveSkillDefinition melee = P24SkillCatalog.Active.Single(value => value.Combat.DisplayName == "连环拳");
        Assert.Equal(8_000, P24UnitAiRules.ForMainSkill(bow.Combat, P24CombatUnitKind.Hero).PreferredDistanceRaw);
        Assert.Equal(9_000, P24UnitAiRules.ForMainSkill(summon.Combat, P24CombatUnitKind.Hero).PreferredDistanceRaw);
        Assert.Equal(7_000, P24UnitAiRules.ForMainSkill(spell.Combat, P24CombatUnitKind.Hero).PreferredDistanceRaw);
        Assert.Equal(1_500, P24UnitAiRules.ForMainSkill(melee.Combat, P24CombatUnitKind.Hero).PreferredDistanceRaw);
    }

    [Fact]
    public void FormalArtAndGuideContractsCoverP24()
    {
        Assert.Equal(5, P24ArtContract.Characters.Count);
        Assert.Equal(5, P24ArtContract.Units.Count);
        Assert.Equal(15, P24ArtContract.Ascendancies.Count);
        Assert.Equal(4, P24ArtContract.DirectionCount);
        Assert.Equal(20, P24ArtContract.SkillVfx.Count);
        Assert.Contains(P24GuideCatalog.Entries, value => value.StableId == "p24.guide.item_families" && value.Rules.Count == 49);
    }

    [Fact]
    public void P24ActiveExecutesInSpatialCombat()
    {
        P24ActiveSkillDefinition active = P24SkillCatalog.Active.Single(value => value.Combat.DisplayName == "穿云箭");
        var configuration = new SkillConfiguration(active.Combat.SkillId, SkillSupport.None, 1,
            P24Supports: [P24SupportMechanic.PrecisionPierce]);
        var fallback = new SkillConfiguration(P1SkillIds.HeavyStrike, SkillSupport.None, 100);
        var build = new P1TeamBuild(
            new CharacterSheet(60, new CharacterAttributes(180, 260, 120, 100),
                new DefensiveEquipment(500, 500, 100), FlatMaximumLife: 2_000),
            new WeaponProfile("p24.test.bow", 300, 400, 1_600, 800),
            fallback, UseWarCry: false, FlatAccuracy: 2_000, IncreasedDamageBasisPoints: 8_000,
            ActiveSkills: [configuration, fallback]);

        P4NodeCombatResult result = new P4SpatialCombatRunner().Run(new P4NodeCombatRequest(
            build, 1, 10, 2, HasElite: false, HasBoss: false, AbyssRoute: false, Formation: 0), 0x24);

        Assert.Contains(result.Events, value => value.Kind == P4SpatialEventKind.SkillEffect &&
            value.Detail.Contains(active.Combat.SkillId, StringComparison.Ordinal));
    }

    private static ItemInstance Item(string id, ItemBaseDefinition definition) =>
        new(id, definition, 80, ItemRarity.Rare, [], ImplicitValue: 0);
}
