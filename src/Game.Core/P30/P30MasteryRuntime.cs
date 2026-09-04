using GameForWork.Core.Equipment;
using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P1.Progression;

namespace GameForWork.Core.P30;

/// <summary>
/// Executes mastery mechanics by stable IDs.  Display text is deliberately never inspected here.
/// </summary>
public static class P30MasteryRuntime
{
    private const string Prefix = "p30.mastery.rule.";

    public static bool Has(P205PassiveModifiers profile, string group, int option) =>
        profile.MasteryMechanics.Split('|', StringSplitOptions.RemoveEmptyEntries)
            .Contains($"{Prefix}{group}.{option}", StringComparer.Ordinal);

    public static int OffensiveMultiplier(P205PassiveModifiers profile, SkillTag tags, WeaponProfile weapon,
        int targetLife, int targetMaximumLife, int nearbyEnemyCount = 1, int distanceRaw = 1_000)
    {
        int result = 10_000;
        bool attack = tags.HasFlag(SkillTag.Attack);
        bool hit = !tags.HasFlag(SkillTag.Duration);
        bool twoHand = IsCategory(weapon, ItemCategory.TwoHandWeapon);
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
        if (tags.HasFlag(SkillTag.Projectile) && Has(profile, "投射物", 0)) result = Multiply(result, 15_000);
        if (tags.HasFlag(SkillTag.Physical) && Has(profile, "物理", 0)) result = Multiply(result, 16_000);
        if (tags.HasFlag(SkillTag.Void) && Has(profile, "虚空", 0)) result = Multiply(result, 15_000);
        return result;
    }

    public static int ActionSpeedMultiplier(P205PassiveModifiers profile, SkillTag tags, WeaponProfile weapon)
    {
        int result = 10_000;
        if (tags.HasFlag(SkillTag.Attack) && IsFamily(weapon, WeaponFamily.Sword) && Has(profile, "剑类", 6))
            result = Multiply(result, 13_500);
        if (tags.HasFlag(SkillTag.Attack) && IsCategory(weapon, ItemCategory.TwoHandWeapon) && Has(profile, "双手", 0))
            result = Multiply(result, 6_500);
        return result;
    }

    public static bool CannotCrit(P205PassiveModifiers profile) =>
        Has(profile, "眩晕", 1) || Has(profile, "暴击", 6) || Has(profile, "斧类", 0);

    public static int AdditionalLifeLeech(P205PassiveModifiers profile) => Has(profile, "偷取", 0) ? 300 : 0;

    public static int AdditionalBleedChance(P205PassiveModifiers profile, SkillTag tags, WeaponProfile weapon) =>
        tags.HasFlag(SkillTag.Attack) && tags.HasFlag(SkillTag.Physical) && IsFamily(weapon, WeaponFamily.Sword) &&
        Has(profile, "剑类", 5) ? 3_000 : 0;

    public static int DefenseMultiplier(P205PassiveModifiers profile, WeaponProfile weapon) =>
        IsCategory(weapon, ItemCategory.TwoHandWeapon) && Has(profile, "双手", 5) ? 14_000 : 10_000;

    public static int IncomingAttackMultiplier(P205PassiveModifiers profile, WeaponProfile weapon) =>
        IsCategory(weapon, ItemCategory.TwoHandWeapon) && Has(profile, "双手", 5) ? 9_000 : 10_000;

    public static int ShieldMultiplier(P205PassiveModifiers profile)
    {
        if (Has(profile, "生命", 3)) return 0;
        return Has(profile, "生命", 0) ? 5_000 : 10_000;
    }

    private static bool IsCategory(WeaponProfile weapon, ItemCategory category)
    {
        try { return EquipmentCatalog.GetBase(weapon.StableId).Category == category; }
        catch (KeyNotFoundException) { return false; }
    }

    private static bool IsFamily(WeaponProfile weapon, WeaponFamily family)
    {
        try { return EquipmentCatalog.GetBase(weapon.StableId).WeaponFamily == family; }
        catch (KeyNotFoundException) { return false; }
    }

    private static int Multiply(int left, int right) => checked(left * right / 10_000);
}
