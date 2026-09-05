using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Simulation;

namespace GameForWork.Core.Skills;

public sealed record SpellHitProfile(int Minimum, int Maximum, int CriticalChance, int Effectiveness);

public static class SpellHitRules
{
    private static readonly IReadOnlyDictionary<string, SpellHitProfile> Profiles = new Dictionary<string, SpellHitProfile>
    {
        [SkillIds.EmberNova] = new(36, 54, 500, 10_000),
        [SkillIds.FrostShard] = new(28, 44, 600, 11_000),
        [SkillIds.ChainLightning] = new(20, 68, 500, 9_000),
        [SkillIds.StormBrand] = new(18, 72, 500, 7_000),
        ["archetypes.skill.plague_detonation"] = new(42, 63, 500, 12_000),
        ["archetypes.skill.molten_orb"] = new(38, 58, 600, 12_000),
        ["archetypes.skill.ice_lance"] = new(44, 66, 900, 13_000),
        ["archetypes.skill.elemental_prism"] = new(32, 64, 600, 11_000),
        ["archetypes.skill.forbidden_collapse"] = new(70, 105, 500, 16_000),
        ["archetypes.skill.aegis_pulse"] = new(30, 46, 500, 10_000),
        ["archetypes.skill.sixfold_burst"] = new(58, 87, 600, 15_000),
        ["archetypes.skill.thunderstorm"] = new(12, 48, 500, 5_000),
        ["archetypes.skill.withering_ray"] = new(6, 18, 500, 3_500),
        ["archetypes.skill.shield_drain"] = new(5, 15, 500, 3_000),
        ["archetypes.skill.void_rift"] = new(28, 42, 500, 8_000),
        ["archetypes.skill.mirror_counter"] = new(28, 84, 600, 10_000),
        ["archetypes.skill.answering_formula"] = new(24, 48, 500, 9_000),
        ["archetypes.skill.doom_brand"] = new(48, 72, 500, 12_000),
    };

    public static int Roll(ResolvedSkill skill, int level, Pcg32 random)
    {
        var (minimum, maximum) = DamageRange(skill, level);
        if (!Profiles.ContainsKey(skill.SkillId)) return minimum;
        return minimum + (int)(random.NextUInt() % (uint)(maximum - minimum + 1));
    }

    public static (int Minimum, int Maximum) DamageRange(ResolvedSkill skill, int level)
    {
        if (!Profiles.TryGetValue(skill.SkillId, out var profile))
        {
            int value = Math.Max(1, (skill.BaseDamageBasisPoints + 50) / 100);
            return (value, value);
        }
        double growth = Math.Pow(1.07, Math.Clamp(level, 1, 40) - 1);
        int minimum = (int)Math.Round(profile.Minimum * growth, MidpointRounding.AwayFromZero);
        int maximum = (int)Math.Round(profile.Maximum * growth, MidpointRounding.AwayFromZero);
        return (minimum, maximum);
    }

    public static int BaseCriticalChance(string skillId, int distanceRaw, int quality)
    {
        int chance = Profiles.GetValueOrDefault(skillId)?.CriticalChance ?? 500;
        if (skillId == "archetypes.skill.ice_lance" && distanceRaw >= 7_000) chance += 600 + Math.Clamp(quality, 0, 20) * 10;
        return chance;
    }
    public static int Effectiveness(string skillId) => Profiles.GetValueOrDefault(skillId)?.Effectiveness ?? 10_000;
}
