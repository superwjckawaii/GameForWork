using GameForWork.Core.P1.Items;

namespace GameForWork.Core.Equipment;

public enum EquipmentEffectDomain
{
    Character,
    Action,
    Damage,
    Defense,
    Recovery,
    Skill,
    Unit,
    Flask,
    Rule,
}

public sealed record EquipmentEffectSnapshot(
    IReadOnlyDictionary<ItemModifierKind, int> Character,
    IReadOnlyDictionary<ItemModifierKind, int> Action,
    IReadOnlyDictionary<ItemModifierKind, int> Damage,
    IReadOnlyDictionary<ItemModifierKind, int> Defense,
    IReadOnlyDictionary<ItemModifierKind, int> Recovery,
    IReadOnlyDictionary<ItemModifierKind, int> Skill,
    IReadOnlyDictionary<ItemModifierKind, int> Unit,
    IReadOnlyDictionary<ItemModifierKind, int> Flask,
    IReadOnlyDictionary<ItemModifierKind, int> Rule)
{
    public int Value(ItemModifierKind kind) => Domain(kind).GetValueOrDefault(kind);

    public IReadOnlyDictionary<ItemModifierKind, int> Domain(ItemModifierKind kind) => EquipmentEffectCompiler.Domain(kind) switch
    {
        EquipmentEffectDomain.Character => Character,
        EquipmentEffectDomain.Action => Action,
        EquipmentEffectDomain.Damage => Damage,
        EquipmentEffectDomain.Defense => Defense,
        EquipmentEffectDomain.Recovery => Recovery,
        EquipmentEffectDomain.Skill => Skill,
        EquipmentEffectDomain.Unit => Unit,
        EquipmentEffectDomain.Flask => Flask,
        _ => Rule,
    };
}

/// <summary>Compiles every modifier into one explicit gameplay domain before combat reads it.</summary>
public static class EquipmentEffectCompiler
{
    public static EquipmentEffectSnapshot Compile(IReadOnlyDictionary<ItemModifierKind, int> values)
    {
        var domains = Enum.GetValues<EquipmentEffectDomain>().ToDictionary(domain => domain,
            _ => new Dictionary<ItemModifierKind, int>());
        foreach ((ItemModifierKind kind, int value) in values)
        {
            if (kind == ItemModifierKind.None || value == 0) continue;
            domains[Domain(kind)][kind] = value;
        }
        return new(domains[EquipmentEffectDomain.Character], domains[EquipmentEffectDomain.Action],
            domains[EquipmentEffectDomain.Damage], domains[EquipmentEffectDomain.Defense],
            domains[EquipmentEffectDomain.Recovery], domains[EquipmentEffectDomain.Skill],
            domains[EquipmentEffectDomain.Unit], domains[EquipmentEffectDomain.Flask], domains[EquipmentEffectDomain.Rule]);
    }

    public static EquipmentEffectDomain Domain(ItemModifierKind kind)
    {
        string name = kind.ToString();
        if (kind == ItemModifierKind.None) return EquipmentEffectDomain.Rule;
        if (name.Contains("Flask", StringComparison.Ordinal)) return EquipmentEffectDomain.Flask;
        if (name.Contains("Minion", StringComparison.Ordinal) || name.Contains("Construct", StringComparison.Ordinal) ||
            name.Contains("Companion", StringComparison.Ordinal) || name.Contains("Trap", StringComparison.Ordinal) ||
            name.Contains("Phantom", StringComparison.Ordinal) || name.Contains("Unit", StringComparison.Ordinal)) return EquipmentEffectDomain.Unit;
        if (name.Contains("Skill", StringComparison.Ordinal) || name.Contains("Aura", StringComparison.Ordinal) ||
            name.Contains("Curse", StringComparison.Ordinal) || name.Contains("Warcry", StringComparison.Ordinal) ||
            name.Contains("Reservation", StringComparison.Ordinal) || name.Contains("Projectile", StringComparison.Ordinal) ||
            name.Contains("Chain", StringComparison.Ordinal) || name.Contains("Pierce", StringComparison.Ordinal) ||
            name.Contains("StrikeTarget", StringComparison.Ordinal)) return EquipmentEffectDomain.Skill;
        if (name.Contains("Leech", StringComparison.Ordinal) || name.Contains("Regeneration", StringComparison.Ordinal) ||
            name.Contains("Recovery", StringComparison.Ordinal) || name.Contains("OnHit", StringComparison.Ordinal) ||
            name.Contains("Recharge", StringComparison.Ordinal)) return EquipmentEffectDomain.Recovery;
        if (name.Contains("Armor", StringComparison.Ordinal) || name.Contains("Evasion", StringComparison.Ordinal) ||
            name.Contains("Shield", StringComparison.Ordinal) || name.Contains("Barrier", StringComparison.Ordinal) ||
            name.Contains("Resistance", StringComparison.Ordinal) || name.Contains("Block", StringComparison.Ordinal) ||
            name.Contains("Suppression", StringComparison.Ordinal) || name.Contains("Avoidance", StringComparison.Ordinal) ||
            name.Contains("Debuff", StringComparison.Ordinal)) return EquipmentEffectDomain.Defense;
        if (name.Contains("Damage", StringComparison.Ordinal) || name.Contains("Critical", StringComparison.Ordinal) ||
            name.Contains("Penetration", StringComparison.Ordinal) || name.Contains("Conversion", StringComparison.Ordinal) ||
            name.Contains("Bleed", StringComparison.Ordinal) || name.Contains("Poison", StringComparison.Ordinal) ||
            name.Contains("Ignite", StringComparison.Ordinal) || name.Contains("Shock", StringComparison.Ordinal) ||
            name.Contains("Chill", StringComparison.Ordinal) || name.Contains("Freeze", StringComparison.Ordinal)) return EquipmentEffectDomain.Damage;
        if (name.Contains("Speed", StringComparison.Ordinal) || name.Contains("Accuracy", StringComparison.Ordinal) ||
            name.Contains("Cooldown", StringComparison.Ordinal)) return EquipmentEffectDomain.Action;
        if (name.StartsWith("Hold", StringComparison.Ordinal) || name.Contains("Maximum", StringComparison.Ordinal) ||
            name.Contains("Automatic", StringComparison.Ordinal) || name.Contains("Repeat", StringComparison.Ordinal) ||
            name.Contains("Rearm", StringComparison.Ordinal) || name.Contains("CheatDeath", StringComparison.Ordinal) ||
            name.Contains("Bridge", StringComparison.Ordinal) || name.Contains("Cleanse", StringComparison.Ordinal) ||
            name.Contains("Overflow", StringComparison.Ordinal) || name.Contains("BaseImplicitRule", StringComparison.Ordinal)) return EquipmentEffectDomain.Rule;
        return EquipmentEffectDomain.Character;
    }
}

public static class EquipmentResourceRules
{
    public static int RegenerationPerSecond(int maximum, int flat, int maximumBasisPoints, int recoveryRateBasisPoints) =>
        checked((flat + maximum * maximumBasisPoints / 10_000) * (10_000 + recoveryRateBasisPoints) / 10_000);

    public static int HitRecovery(int hitCount, int valuePerHit) => checked(Math.Max(0, hitCount) * Math.Max(0, valuePerHit));

    public static int LeechAmount(int hitDamage, int leechBasisPoints) => checked(Math.Max(0, hitDamage) * Math.Max(0, leechBasisPoints) / 10_000);

    public static int LeechPerSecondCap(int maximumResource, int increasedCapBasisPoints) =>
        checked(maximumResource * 3_500 / 10_000 * (10_000 + increasedCapBasisPoints) / 10_000);
}
