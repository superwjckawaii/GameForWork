using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Progression;
using GameForWork.Core.SkillCatalog;

namespace GameForWork.Core.Skills;

public static class AreaRules
{
    public static int Increased(SkillConfiguration configuration, PassiveModifiers profile)
    {
        var tags = SkillDefinitions.Get(configuration.SkillId).Tags;
        if (!tags.HasFlag(SkillTag.Area)) return 0;
        int increased = profile.SpecializedValue(PassiveEffectKind.IncreasedAreaEffectBasisPoints);
        if (tags.HasFlag(SkillTag.Void) && MasteryRuntime.Has(profile, "虚空", 4)) increased += 8_000;
        if (configuration.Supports.HasFlag(SkillSupport.IncreasedArea))
        {
            var link = CombatSkillRules.SupportLink(configuration, SkillSupport.IncreasedArea);
            increased += CombatSkillRules.SupportValue(configuration, SkillSupport.IncreasedArea) * 100 + Math.Clamp(link.Quality, 0, 20) * 50;
        }
        if (configuration.SkillId is SkillIds.EarthCleave or SkillIds.EmberNova or "archetypes.skill.void_rift" or
            "archetypes.skill.skyquake_palm" or "archetypes.skill.sixfold_burst" or "builds.skill.shield_bash")
            increased += Math.Clamp(configuration.Quality, 0, 20) * 100;
        return increased;
    }

    public static int More(SkillConfiguration configuration, PassiveModifiers profile)
    {
        if (!SkillDefinitions.Get(configuration.SkillId).Tags.HasFlag(SkillTag.Area)) return 10_000;
        int concentrated = configuration.Supports.HasFlag(SkillSupport.ConcentratedEffect)
            ? 7_500 + Math.Clamp(CombatSkillRules.SupportLink(configuration, SkillSupport.ConcentratedEffect).Quality, 0, 20) * 25 : 10_000;
        return CombatRules.ApplyMore(10_000, [concentrated,
            MasteryRuntime.Has(profile, "范围_距离", 0) ? 5_000 : 10_000,
            MasteryRuntime.Has(profile, "范围_距离", 1) ? 20_000 : 10_000]);
    }

    public static int EngagementRange(ResolvedSkill skill) => skill.Shape is SkillShape.Circle or SkillShape.Cone
        ? skill.AreaRadiusRaw : skill.RangeRaw;

    public static int PositionMultiplier(PassiveModifiers profile, int distance, int radius)
    {
        if (radius <= 0) return 10_000;
        int value = 10_000;
        if (MasteryRuntime.Has(profile, "范围_距离", 2))
            value = CombatRules.ApplyMore(value, [(long)Math.Max(0, distance) * 4 <= radius ? 15_000 : 8_000]);
        if (MasteryRuntime.Has(profile, "范围_距离", 3))
            value = CombatRules.ApplyMore(value, [(long)Math.Max(0, distance) * 4 >= (long)radius * 3 ? 15_000 : 8_000]);
        return value;
    }

    public static int Duration(int baseline, SkillTag tags, PassiveModifiers profile) =>
        tags.HasFlag(SkillTag.Area) && MasteryRuntime.Has(profile, "范围_距离", 5) ? checked(baseline * 2) : baseline;

    public static int Radius(int baseline, int areaMultiplier) =>
        (int)Math.Clamp(Math.Round(Math.Max(0, baseline) * Math.Sqrt(Math.Max(2_500, areaMultiplier) / 10_000d)), 0, int.MaxValue);
}
