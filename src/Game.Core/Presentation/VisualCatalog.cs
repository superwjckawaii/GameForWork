using GameForWork.Core.SkillCatalog;
using GameForWork.Core.Builds;

namespace GameForWork.Core.Presentation;

public enum SkillVisualFamily
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
public enum SupportVisualLayer
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

public sealed record SkillVisualDescriptor(
    string SkillId,
    SkillVisualFamily Family,
    SkillDamageType DamageType,
    SkillShape Shape,
    int AtlasCell,
    int LifetimeMilliseconds,
    int ScaleBasisPoints,
    int VariationSeed,
    bool Signature,
    bool UsesSourceToTarget,
    bool LeavesGroundEffect);

public sealed record SupportVisualDescriptor(
    string StoneId,
    string MechanicKey,
    SupportVisualLayer Layer);

public static class VisualCatalog
{
    private static readonly HashSet<string> SignatureSkills = BuildAudit.Builds
        .Select(item => item.MainSkillId)
        .ToHashSet(StringComparer.Ordinal);

    public static IReadOnlyList<SkillVisualDescriptor> Skills { get; } = ActiveSkillCatalog.Active
        .Select(BuildSkill)
        .ToArray();

    public static IReadOnlyList<SupportVisualDescriptor> Supports { get; } = ActiveSkillCatalog.Supports
        .Select(item => new SupportVisualDescriptor(item.StoneId, item.MechanicKey, SupportLayer(item)))
        .ToArray();

    private static readonly IReadOnlyDictionary<string, SkillVisualDescriptor> SkillById =
        Skills.ToDictionary(item => item.SkillId, StringComparer.Ordinal);

    static VisualCatalog()
    {
        if (Skills.Count != 86 || SkillById.Count != 86)
            throw new InvalidDataException("Presentation requires one stable visual descriptor for each of the 86 active skills.");
        if (Skills.Count(item => item.Signature) != 18)
            throw new InvalidDataException("Presentation requires 18 bespoke endgame-build skill descriptors.");
        if (Supports.Count != 98 || Supports.Any(item => item.Layer == SupportVisualLayer.None))
            throw new InvalidDataException("Presentation requires a visible mechanic layer for all 98 supports.");
    }

    public static SkillVisualDescriptor ForSkill(string skillId) =>
        SkillById.TryGetValue(skillId, out SkillVisualDescriptor? result)
            ? result
            : throw new KeyNotFoundException($"Unknown Presentation skill visual: {skillId}");

    public static bool TryForSkill(string skillId, out SkillVisualDescriptor? result) =>
        SkillById.TryGetValue(skillId, out result);

    public static SupportVisualLayer LayersForLegacySupport(ulong supportFlags)
    {
        SupportVisualLayer result = SupportVisualLayer.None;
        foreach (SupportSkillDefinition support in ActiveSkillCatalog.Supports)
        {
            if (support.LegacySupport == 0 || (supportFlags & (ulong)support.LegacySupport) == 0) continue;
            result |= SupportLayer(support);
        }
        return result;
    }

    private static SkillVisualDescriptor BuildSkill(ActiveSkillDefinition definition)
    {
        SkillCombatDefinition skill = definition.Combat;
        SkillVisualFamily family = Family(skill);
        bool signature = SignatureSkills.Contains(skill.SkillId);
        int variation = StableHash(skill.SkillId) % 7;
        int lifetime = skill.Shape is SkillShape.Projectile or SkillShape.Chain ? 620 :
            skill.Shape == SkillShape.GroundArea ? 1_100 : 760;
        int scale = 8_500 + Math.Min(5_000, skill.RangeRaw / 2) + (signature ? 1_200 : 0);
        return new(skill.SkillId, family, skill.DamageType, skill.Shape, (int)family, lifetime, scale,
            variation, signature,
            skill.Shape is SkillShape.Projectile or SkillShape.Chain or SkillShape.Cone or SkillShape.Single,
            skill.Shape == SkillShape.GroundArea);
    }

    private static SkillVisualFamily Family(SkillCombatDefinition skill)
    {
        if (skill.Role is SkillRole.Guard or SkillRole.Reservation) return SkillVisualFamily.GuardSphere;
        if (skill.Role == SkillRole.WarCry) return SkillVisualFamily.ElementalNova;
        if (skill.DisplayName.Contains('召') || skill.DisplayName.Contains('魂') ||
            skill.Description.Contains("召唤", StringComparison.Ordinal) || skill.Description.Contains("构装", StringComparison.Ordinal))
            return SkillVisualFamily.Summoning;
        if (skill.Shape == SkillShape.GroundArea) return skill.DamageType == SkillDamageType.Fire
            ? SkillVisualFamily.BurningGround : SkillVisualFamily.TrapOrRune;
        if (skill.Shape == SkillShape.Chain) return skill.DamageType switch
        {
            SkillDamageType.Lightning => SkillVisualFamily.LightningArc,
            SkillDamageType.Void => SkillVisualFamily.VoidBeam,
            _ => SkillVisualFamily.ReturningBlades,
        };
        if (skill.Shape == SkillShape.Projectile) return skill.DamageType switch
        {
            SkillDamageType.Fire => SkillVisualFamily.FireProjectile,
            SkillDamageType.Cold => SkillVisualFamily.IceProjectile,
            SkillDamageType.Lightning => SkillVisualFamily.LightningArc,
            SkillDamageType.Void => SkillVisualFamily.VoidBeam,
            _ => SkillVisualFamily.Projectile,
        };
        if (skill.Shape is SkillShape.Circle or SkillShape.MovementCircle)
            return skill.DamageType == SkillDamageType.Fire ? SkillVisualFamily.BurningGround : SkillVisualFamily.ElementalNova;
        if (skill.DamageType == SkillDamageType.Void) return SkillVisualFamily.VoidBeam;
        if (skill.Ailment is Ailment.Bleed or Ailment.Ignite or Ailment.Erosion or Ailment.Wither)
            return SkillVisualFamily.BloodCrescent;
        return skill.Capabilities.HasFlag(SkillCapability.Slam)
            ? SkillVisualFamily.GroundImpact
            : SkillVisualFamily.GoldenCrescent;
    }

    private static SupportVisualLayer SupportLayer(SupportSkillDefinition support)
    {
        string key = support.MechanicKey;
        if (key.Contains("projectile", StringComparison.Ordinal) || key.Contains("volley", StringComparison.Ordinal))
            return SupportVisualLayer.ExtraProjectiles;
        if (key.Contains("chain", StringComparison.Ordinal) || key.Contains("fork", StringComparison.Ordinal) || key.Contains("pierce", StringComparison.Ordinal))
            return SupportVisualLayer.ChainOrFork;
        if (key.Contains("return", StringComparison.Ordinal)) return SupportVisualLayer.Return;
        if (key.Contains("area", StringComparison.Ordinal) || key.Contains("nova", StringComparison.Ordinal)) return SupportVisualLayer.AreaPulse;
        if (key.Contains("repeat", StringComparison.Ordinal) || key.Contains("cycle", StringComparison.Ordinal)) return SupportVisualLayer.Repeat;
        if (key.Contains("crit", StringComparison.Ordinal)) return SupportVisualLayer.CriticalFlash;
        if (key.Contains("ailment", StringComparison.Ordinal) || key.Contains("dot", StringComparison.Ordinal) || key.Contains("bleed", StringComparison.Ordinal) || key.Contains("poison", StringComparison.Ordinal))
            return SupportVisualLayer.AilmentTrail;
        if (key.Contains("guard", StringComparison.Ordinal) || key.Contains("shield", StringComparison.Ordinal)) return SupportVisualLayer.GuardShell;
        if (key.Contains("trigger", StringComparison.Ordinal) || key.Contains("trap", StringComparison.Ordinal) || key.Contains("rune", StringComparison.Ordinal))
            return SupportVisualLayer.TriggerRune;
        if (key.Contains("minion", StringComparison.Ordinal) || key.Contains("unit", StringComparison.Ordinal) || key.Contains("construct", StringComparison.Ordinal))
            return SupportVisualLayer.MinionAura;
        return support.RequiredAny.HasFlag(SkillCapability.Projectile) ? SupportVisualLayer.ExtraProjectiles :
            support.RequiredAny.HasFlag(SkillCapability.Area) ? SupportVisualLayer.AreaPulse :
            support.RequiredAny.HasFlag(SkillCapability.Guard) ? SupportVisualLayer.GuardShell :
            support.RequiredAny.HasFlag(SkillCapability.Damage) || support.RequiredAll.HasFlag(SkillCapability.Damage)
                ? SupportVisualLayer.CriticalFlash
                : SupportVisualLayer.TriggerRune;
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
