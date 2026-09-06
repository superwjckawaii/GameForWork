using GameForWork.Core.Ascendancies;
using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Progression;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.SkillCatalog;
using GameForWork.Core.Skills;

namespace GameForWork.Core.Combat;

public sealed class ElementalCombatState(CombatProfile? profile)
{
    private readonly Dictionary<DamageType, int> _resonances = [];
    private readonly Dictionary<string, int> _actions = [];
    public int Count(int tick) => _resonances.Count(pair => pair.Value > tick);
    public TeamBuild Apply(TeamBuild build, int temperance)
    {
        if (!ElementalRules.Has(profile, "resonance", "core")) return build;
        var passive = build.PassiveProfile ?? PassiveModifiers.Empty;
        return build with { PassiveProfile = passive with { IncreasedElementalDamageBasisPoints = passive.IncreasedElementalDamageBasisPoints + temperance * 500 } };
    }
    public int Begin(string action, SkillTag tags, int tick, bool triggered)
    {
        if (triggered || (tags & SkillTag.Elemental) == 0 || !ElementalRules.Has(profile, "resonance", "core")) return 10_000;
        if (_actions.TryGetValue(action, out int multiplier)) return multiplier;
        multiplier = Count(tick) == 3 ? 13_000 : 10_000;
        if (multiplier > 10_000) _resonances.Clear();
        _actions[action] = multiplier;
        return multiplier;
    }
    public void Observe(DamageBreakdown damage, int tick, bool triggered)
    {
        if (triggered || !ElementalRules.Has(profile, "resonance")) return;
        if (damage.Fire > 0) _resonances[DamageType.Fire] = tick + 160;
        if (damage.Cold > 0) _resonances[DamageType.Cold] = tick + 160;
        if (damage.Lightning > 0) _resonances[DamageType.Lightning] = tick + 160;
    }
}
