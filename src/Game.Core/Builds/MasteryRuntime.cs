using GameForWork.Core.Equipment;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Campaign.Progression;
using System.Runtime.CompilerServices;

namespace GameForWork.Core.Builds;

/// <summary>
/// Executes mastery mechanics by stable IDs.  Display text is deliberately never inspected here.
/// </summary>
public static class MasteryRuntime
{
    private const string Prefix = "builds.mastery.rule.";

    private static readonly ConditionalWeakTable<string, Dictionary<string, int>> MechanicCache = new();
    public static bool Has(PassiveModifiers profile, string group, int option) =>
        profile.MasteryMechanics.Length > 0 && MechanicCache.GetValue(profile.MasteryMechanics, ParseMechanics).TryGetValue(group, out int options) &&
        option is >= 0 and < 7 && (options & (1 << option)) != 0;

    private static Dictionary<string, int> ParseMechanics(string mechanics)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (string id in mechanics.Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            int dot = id.LastIndexOf('.');
            if (!id.StartsWith(Prefix, StringComparison.Ordinal) || dot < Prefix.Length ||
                !int.TryParse(id.AsSpan(dot + 1), out int option) || option is < 0 or >= 7) continue;
            string group = id[Prefix.Length..dot];
            result[group] = result.GetValueOrDefault(group) | (1 << option);
        }
        return result;
    }

    public static CharacterSheet ApplySheet(CharacterSheet sheet, PassiveModifiers profile, WeaponProfile weapon, bool hasShield)
    {
        bool convertEvasion = Has(profile, "护甲", 1);
        if (convertEvasion) sheet = sheet with
        {
            Equipment = sheet.Equipment with
            { Armor = checked(sheet.Equipment.Armor + sheet.Equipment.Evasion + sheet.Attributes.Dexterity), Evasion = 0 }
        };
        int lifeDefense = Has(profile, "生命", 5) ? Math.Min(10, sheet.MaximumLife().Value / 500) * 1_000 : 0;
        sheet = sheet with
        {
            ArmorMultiplierBasisPoints = CombatRules.ApplyMore(sheet.ArmorMultiplierBasisPoints, [ArmorMultiplier(profile, weapon)]),
            EvasionMultiplierBasisPoints = convertEvasion ? 0 : CombatRules.ApplyMore(sheet.EvasionMultiplierBasisPoints, [EvasionMultiplier(profile, weapon, hasShield)]),
            IncreasedArmorBasisPoints = sheet.IncreasedArmorBasisPoints + lifeDefense,
            IncreasedSpiritBarrierBasisPoints = sheet.IncreasedSpiritBarrierBasisPoints + lifeDefense,
            MaximumShieldRegenerationBasisPoints = sheet.MaximumShieldRegenerationBasisPoints + (Has(profile, "能量护盾", 2) ? 200 : 0),
            AdditionalManaRegenerationBasisPoints = sheet.AdditionalManaRegenerationBasisPoints + (Has(profile, "法力", 4) ? 600 : 0),
            IncreasedMovementSpeedBasisPoints = sheet.IncreasedMovementSpeedBasisPoints + (!hasShield && Has(profile, "闪避", 3) ? 1_500 : 0),
            IncreasedLifeRegenerationBasisPoints = sheet.IncreasedLifeRegenerationBasisPoints - (Has(profile, "生命", 1) ? 3_000 : 0),
            MaximumVoidResistanceBasisPoints = sheet.MaximumVoidResistanceBasisPoints + (Has(profile, "虚空", 6) ? 300 : 0),
        };
        if (Has(profile, "闪避", 6)) sheet = sheet with { SpellSuppressionBasisPoints = sheet.SpellSuppressionBasisPoints + sheet.Evasion().Value / 1_000 * 100 };
        return sheet;
    }

    public static int IncomingResourceMultiplier(PassiveModifiers profile, ResourceState resource, bool hit)
    {
        int result = 10_000;
        if (hit)
        {
            if (Has(profile, "生命", 2) && resource.Life == resource.MaximumLife) result = Multiply(result, 8_500);
            if (Has(profile, "生命", 6) && resource.Life * 2L <= resource.MaximumLife) result = Multiply(result, 8_000);
            if (Has(profile, "法力", 2) && resource.Mana * 2L >= resource.MaximumMana) result = Multiply(result, 9_000);
            if (Has(profile, "能量护盾", 4) && resource.MaximumShield > 0 && resource.Shield == resource.MaximumShield) result = Multiply(result, 8_000);
        }
        else
        {
            if (Has(profile, "能量护盾", 3) && resource.Shield > 0) result = Multiply(result, 8_000);
            if (Has(profile, "虚空", 6)) result = Multiply(result, 9_000);
        }
        return result;
    }
    public static int OffensiveResourceMultiplier(PassiveModifiers profile, ResourceState resource)
    {
        int result = 10_000;
        if (Has(profile, "生命", 1) && resource.Life * 2L <= resource.MaximumLife) result = Multiply(result, 14_000);
        if (Has(profile, "能量护盾", 5) && resource.MaximumShield > 0 && resource.Shield * 10L >= resource.MaximumShield * 8L) result = Multiply(result, 13_000);
        if (resource.HasRecentSkillLifePayment) result = Multiply(result, 13_000);
        return result;
    }
    public static int ManaCost(PassiveModifiers profile, SkillTag tags, int cost) => CombatRules.ApplyMore(cost,
        [Has(profile, "法力", 3) ? 5_000 : 10_000, Has(profile, "法力", 6) ? 15_000 : 10_000,
            (tags & (SkillTag.Attack | SkillTag.Spell)) != 0 && Has(profile, "能量护盾", 5) ? 12_000 : 10_000]);

    public static bool HasManaWard(PassiveModifiers profile, WeaponProfile weapon) => Has(profile, "法杖", 6) && IsFamily(weapon, WeaponFamily.Wand);
    public static int ManaDamageShare(PassiveModifiers profile, WeaponProfile weapon) =>
        Has(profile, "法力", 0) ? 3_000 : HasManaWard(profile, weapon) ? 2_000 : 0;

    public static int OffensiveMultiplier(PassiveModifiers profile, SkillTag tags, WeaponProfile weapon,
        int targetLife, int targetMaximumLife, int nearbyEnemyCount = 1, int distanceRaw = 1_000,
        bool hasOffHand = false, bool hit = true)
    {
        if (profile.MasteryMechanics.Length == 0) return 10_000;
        int result = 10_000;
        bool attack = tags.HasFlag(SkillTag.Attack);
        bool twoHand = IsCategory(weapon, ItemCategory.TwoHandWeapon);
        if (hit && Has(profile, "法力", 3)) result = Multiply(result, 8_500);
        if (hit && Has(profile, "法力", 6)) result = Multiply(result, 14_000);
        if (attack && hit && Has(profile, "攻击", 0)) result = Multiply(result, 14_000);
        if (attack && hit && tags.HasFlag(SkillTag.Physical) && IsFamily(weapon, WeaponFamily.Axe) &&
            Has(profile, "斧类", 0)) result = Multiply(result, 16_000);
        if (attack && hit && IsCategory(weapon, ItemCategory.OneHandWeapon) && !hasOffHand &&
            Has(profile, "单手", 0)) result = Multiply(result, 16_000);
        if (attack && hit && twoHand && Has(profile, "双手", 0)) result = Multiply(result, 20_000);
        if (attack && hit && twoHand && Has(profile, "双手", 2) &&
            (long)targetLife * 10_000 > (long)targetMaximumLife * 7_000)
            result = Multiply(result, 15_000);
        if (attack && hit && tags.HasFlag(SkillTag.Melee) && Has(profile, "近战打击", 2) && nearbyEnemyCount <= 1)
            result = Multiply(result, 15_000);
        if (attack && hit && tags.HasFlag(SkillTag.Melee) && Has(profile, "近战打击", 3) && distanceRaw <= 1_500)
            result = Multiply(result, 13_500);
        if (tags.HasFlag(SkillTag.Area) && Has(profile, "范围_距离", 0)) result = Multiply(result, 16_000);
        if (tags.HasFlag(SkillTag.Area) && Has(profile, "范围_距离", 1)) result = Multiply(result, 8_000);
        if (hit && tags.HasFlag(SkillTag.Area) && Has(profile, "范围_距离", 4)) result = Multiply(result, 6_000);
        if (tags.HasFlag(SkillTag.Projectile) && Has(profile, "投射物", 0)) result = Multiply(result, 15_000);
        return result;
    }

    public static int ActionSpeedMultiplier(PassiveModifiers profile, SkillTag tags, WeaponProfile weapon)
    {
        if (profile.MasteryMechanics.Length == 0) return 10_000;
        int result = 10_000;
        if (tags.HasFlag(SkillTag.Attack) && IsFamily(weapon, WeaponFamily.Sword) && Has(profile, "剑类", 6))
            result = Multiply(result, 13_500);
        if (tags.HasFlag(SkillTag.Attack) && IsCategory(weapon, ItemCategory.TwoHandWeapon) && Has(profile, "双手", 0))
            result = Multiply(result, 6_500);
        if (tags.HasFlag(SkillTag.Attack) && IsFamily(weapon, WeaponFamily.Axe) && Has(profile, "斧类", 0))
            result = Multiply(result, 8_500);
        return result;
    }

    public static bool CannotCrit(PassiveModifiers profile) =>
        Has(profile, "眩晕", 1) || Has(profile, "暴击", 6) || Has(profile, "斧类", 0) ||
        Has(profile, "攻击", 0);

    public static bool AlwaysHits(PassiveModifiers profile, SkillTag tags) =>
        tags.HasFlag(SkillTag.Attack) && Has(profile, "攻击", 0);

    public static int AdditionalLifeLeech(PassiveModifiers profile) => Has(profile, "偷取", 0) ? 300 : 0;
    public static int IncreasedLifeLeechRecoverySpeed(PassiveModifiers profile) =>
        Has(profile, "偷取", 0) ? 10_000 : 0;

    public static int AdditionalBleedChance(PassiveModifiers profile, SkillTag tags, WeaponProfile weapon) =>
        Has(profile, "剑类", 5) && tags.HasFlag(SkillTag.Attack) && tags.HasFlag(SkillTag.Physical) && IsFamily(weapon, WeaponFamily.Sword) ? 3_000 : 0;

    public static int ArmorMultiplier(PassiveModifiers profile, WeaponProfile weapon)
    {
        int result = Has(profile, "双手", 5) && IsCategory(weapon, ItemCategory.TwoHandWeapon) ? 14_000 : 10_000;
        if (Has(profile, "护甲", 0)) result = Multiply(result, 16_000);
        if (Has(profile, "闪避", 0)) result = Multiply(result, 5_000);
        return result;
    }

    public static int EvasionMultiplier(PassiveModifiers profile, WeaponProfile weapon, bool hasShield)
    {
        int result = Has(profile, "双手", 5) && IsCategory(weapon, ItemCategory.TwoHandWeapon) ? 14_000 : 10_000;
        if (Has(profile, "护甲", 0)) result = Multiply(result, 5_000);
        if (Has(profile, "闪避", 0)) result = Multiply(result, 16_000);
        if (!hasShield && Has(profile, "闪避", 3)) result = Multiply(result, 13_500);
        return result;
    }

    public static int IncomingAttackMultiplier(PassiveModifiers profile, WeaponProfile weapon) =>
        Has(profile, "双手", 5) && IsCategory(weapon, ItemCategory.TwoHandWeapon) ? 9_000 : 10_000;

    public static int MaximumLifeMultiplier(PassiveModifiers profile)
    {
        int result = 10_000;
        if (Has(profile, "生命", 0)) result = Multiply(result, 13_000);
        if (Has(profile, "生命", 3)) result = Multiply(result, 14_000);
        if (Has(profile, "能量护盾", 0)) result = Multiply(result, 5_000);
        if (Has(profile, "能量护盾", 1)) result = Multiply(result, 11_500);
        return result;
    }

    public static int MaximumManaMultiplier(PassiveModifiers profile) =>
        Has(profile, "法力", 2) ? 12_000 : 10_000;

    public static int ShieldMultiplier(PassiveModifiers profile)
    {
        if (Has(profile, "生命", 3)) return 0;
        int result = Has(profile, "生命", 0) ? 5_000 : 10_000;
        if (Has(profile, "能量护盾", 0)) result = Multiply(result, 14_000);
        if (Has(profile, "能量护盾", 1)) result = Multiply(result, 11_500);
        return result;
    }

    public static int FortificationMaximum(PassiveModifiers profile) =>
        Has(profile, "护体_承伤缓冲", 0) ? 20 : 10;

    private static bool IsCategory(WeaponProfile weapon, ItemCategory category)
        => EquipmentCatalog.TryGetBase(weapon.StableId, out var item) && item.Category == category;

    private static bool IsFamily(WeaponProfile weapon, WeaponFamily family)
        => EquipmentCatalog.TryGetBase(weapon.StableId, out var item) && item.WeaponFamily == family;

    private static int Multiply(int left, int right) =>
        (int)Math.Clamp((long)left * right / 10_000, 0, int.MaxValue);
}
