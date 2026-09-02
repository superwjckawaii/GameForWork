using GameForWork.Core.P17;
using GameForWork.Core.P30;

namespace GameForWork.Core.P31;

public enum P31SkillVisualFamily
{
    GoldenCrescent,
    BloodCrescent,
    GroundImpact,
    PoisonBurst,
    Projectile,
    ReturningBlades,
    FireProjectile,
    IceProjectile,
    LightningArc,
    VoidBeam,
    ElementalNova,
    BurningGround,
    TrapOrRune,
    Summoning,
    GuardSphere,
    BossWarning,
}

[Flags]
public enum P31SupportVisualLayer
{
    None = 0,
    ExtraProjectiles = 1 << 0,
    ChainOrFork = 1 << 1,
    Return = 1 << 2,
    AreaPulse = 1 << 3,
    Repeat = 1 << 4,
    CriticalFlash = 1 << 5,
    AilmentTrail = 1 << 6,
    GuardShell = 1 << 7,
    TriggerRune = 1 << 8,
    MinionAura = 1 << 9,
}

public sealed record P31SkillVisualDescriptor(
    string SkillId,
    P31SkillVisualFamily Family,
    P17DamageType DamageType,
    P17SkillShape Shape,
    int AtlasCell,
    int LifetimeMilliseconds,
    int ScaleBasisPoints,
    int VariationSeed,
    bool Signature,
    bool UsesSourceToTarget,
    bool LeavesGroundEffect);

public sealed record P31SupportVisualDescriptor(
    string StoneId,
    string MechanicKey,
    P31SupportVisualLayer Layer);

public static class P31VisualCatalog
{
    private static readonly HashSet<string> SignatureSkills = P30BuildAudit.Builds
        .Select(item => item.MainSkillId)
        .ToHashSet(StringComparer.Ordinal);

    public static IReadOnlyList<P31SkillVisualDescriptor> Skills { get; } = P30SkillCatalog.Active
        .Select(BuildSkill)
        .ToArray();

    public static IReadOnlyList<P31SupportVisualDescriptor> Supports { get; } = P30SkillCatalog.Supports
        .Select(item => new P31SupportVisualDescriptor(item.StoneId, item.MechanicKey, SupportLayer(item)))
        .ToArray();

    private static readonly IReadOnlyDictionary<string, P31SkillVisualDescriptor> SkillById =
        Skills.ToDictionary(item => item.SkillId, StringComparer.Ordinal);

    static P31VisualCatalog()
    {
        if (Skills.Count != 86 || SkillById.Count != 86)
            throw new InvalidDataException("P31 requires one stable visual descriptor for each of the 86 active skills.");
        if (Skills.Count(item => item.Signature) != 18)
            throw new InvalidDataException("P31 requires 18 bespoke endgame-build skill descriptors.");
        if (Supports.Count != 98 || Supports.Any(item => item.Layer == P31SupportVisualLayer.None))
            throw new InvalidDataException("P31 requires a visible mechanic layer for all 98 supports.");
    }

    public static P31SkillVisualDescriptor ForSkill(string skillId) =>
        SkillById.TryGetValue(skillId, out P31SkillVisualDescriptor? result)
            ? result
            : throw new KeyNotFoundException($"Unknown P31 skill visual: {skillId}");

    public static bool TryForSkill(string skillId, out P31SkillVisualDescriptor? result) =>
        SkillById.TryGetValue(skillId, out result);

    public static P31SupportVisualLayer LayersForLegacySupport(ulong supportFlags)
    {
        P31SupportVisualLayer result = P31SupportVisualLayer.None;
        foreach (P30SupportSkillDefinition support in P30SkillCatalog.Supports)
        {
            if (support.LegacySupport == 0 || (supportFlags & (ulong)support.LegacySupport) == 0) continue;
            result |= SupportLayer(support);
        }
        return result;
    }

    private static P31SkillVisualDescriptor BuildSkill(P30ActiveSkillDefinition definition)
    {
        P17ActiveSkillDefinition skill = definition.Combat;
        P31SkillVisualFamily family = Family(skill);
        bool signature = SignatureSkills.Contains(skill.SkillId);
        int variation = StableHash(skill.SkillId) % 7;
        int lifetime = skill.Shape is P17SkillShape.Projectile or P17SkillShape.Chain ? 620 :
            skill.Shape == P17SkillShape.GroundArea ? 1_100 : 760;
        int scale = 8_500 + Math.Min(5_000, skill.RangeRaw / 2) + (signature ? 1_200 : 0);
        return new(skill.SkillId, family, skill.DamageType, skill.Shape, (int)family, lifetime, scale,
            variation, signature,
            skill.Shape is P17SkillShape.Projectile or P17SkillShape.Chain or P17SkillShape.Cone or P17SkillShape.Single,
            skill.Shape == P17SkillShape.GroundArea);
    }

    private static P31SkillVisualFamily Family(P17ActiveSkillDefinition skill)
    {
        if (skill.Role is P17SkillRole.Guard or P17SkillRole.Reservation) return P31SkillVisualFamily.GuardSphere;
        if (skill.Role == P17SkillRole.WarCry) return P31SkillVisualFamily.ElementalNova;
        if (skill.DisplayName.Contains('召') || skill.DisplayName.Contains('魂') ||
            skill.Description.Contains("召唤", StringComparison.Ordinal) || skill.Description.Contains("构装", StringComparison.Ordinal))
            return P31SkillVisualFamily.Summoning;
        if (skill.Shape == P17SkillShape.GroundArea) return skill.DamageType == P17DamageType.Fire
            ? P31SkillVisualFamily.BurningGround : P31SkillVisualFamily.TrapOrRune;
        if (skill.Shape == P17SkillShape.Chain) return skill.DamageType switch
        {
            P17DamageType.Lightning => P31SkillVisualFamily.LightningArc,
            P17DamageType.Void => P31SkillVisualFamily.VoidBeam,
            _ => P31SkillVisualFamily.ReturningBlades,
        };
        if (skill.Shape == P17SkillShape.Projectile) return skill.DamageType switch
        {
            P17DamageType.Fire => P31SkillVisualFamily.FireProjectile,
            P17DamageType.Cold => P31SkillVisualFamily.IceProjectile,
            P17DamageType.Lightning => P31SkillVisualFamily.LightningArc,
            P17DamageType.Void => P31SkillVisualFamily.VoidBeam,
            _ => P31SkillVisualFamily.Projectile,
        };
        if (skill.Shape is P17SkillShape.Circle or P17SkillShape.MovementCircle)
            return skill.DamageType == P17DamageType.Fire ? P31SkillVisualFamily.BurningGround : P31SkillVisualFamily.ElementalNova;
        if (skill.DamageType == P17DamageType.Void) return P31SkillVisualFamily.VoidBeam;
        if (skill.Ailment is P17Ailment.Bleed or P17Ailment.Ignite or P17Ailment.Erosion or P17Ailment.Wither)
            return P31SkillVisualFamily.BloodCrescent;
        return skill.Capabilities.HasFlag(P17SkillCapability.Slam)
            ? P31SkillVisualFamily.GroundImpact
            : P31SkillVisualFamily.GoldenCrescent;
    }

    private static P31SupportVisualLayer SupportLayer(P30SupportSkillDefinition support)
    {
        string key = support.MechanicKey;
        if (key.Contains("projectile", StringComparison.Ordinal) || key.Contains("volley", StringComparison.Ordinal))
            return P31SupportVisualLayer.ExtraProjectiles;
        if (key.Contains("chain", StringComparison.Ordinal) || key.Contains("fork", StringComparison.Ordinal) || key.Contains("pierce", StringComparison.Ordinal))
            return P31SupportVisualLayer.ChainOrFork;
        if (key.Contains("return", StringComparison.Ordinal)) return P31SupportVisualLayer.Return;
        if (key.Contains("area", StringComparison.Ordinal) || key.Contains("nova", StringComparison.Ordinal)) return P31SupportVisualLayer.AreaPulse;
        if (key.Contains("repeat", StringComparison.Ordinal) || key.Contains("cycle", StringComparison.Ordinal)) return P31SupportVisualLayer.Repeat;
        if (key.Contains("crit", StringComparison.Ordinal)) return P31SupportVisualLayer.CriticalFlash;
        if (key.Contains("ailment", StringComparison.Ordinal) || key.Contains("dot", StringComparison.Ordinal) || key.Contains("bleed", StringComparison.Ordinal) || key.Contains("poison", StringComparison.Ordinal))
            return P31SupportVisualLayer.AilmentTrail;
        if (key.Contains("guard", StringComparison.Ordinal) || key.Contains("shield", StringComparison.Ordinal)) return P31SupportVisualLayer.GuardShell;
        if (key.Contains("trigger", StringComparison.Ordinal) || key.Contains("trap", StringComparison.Ordinal) || key.Contains("rune", StringComparison.Ordinal))
            return P31SupportVisualLayer.TriggerRune;
        if (key.Contains("minion", StringComparison.Ordinal) || key.Contains("unit", StringComparison.Ordinal) || key.Contains("construct", StringComparison.Ordinal))
            return P31SupportVisualLayer.MinionAura;
        return support.RequiredAny.HasFlag(P17SkillCapability.Projectile) ? P31SupportVisualLayer.ExtraProjectiles :
            support.RequiredAny.HasFlag(P17SkillCapability.Area) ? P31SupportVisualLayer.AreaPulse :
            support.RequiredAny.HasFlag(P17SkillCapability.Guard) ? P31SupportVisualLayer.GuardShell :
            support.RequiredAny.HasFlag(P17SkillCapability.Damage) || support.RequiredAll.HasFlag(P17SkillCapability.Damage)
                ? P31SupportVisualLayer.CriticalFlash
                : P31SupportVisualLayer.TriggerRune;
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            uint hash = 2_166_136_261;
            foreach (char character in value) hash = (hash ^ character) * 16_777_619;
            return (int)(hash & 0x7fff_ffff);
        }
    }
}
