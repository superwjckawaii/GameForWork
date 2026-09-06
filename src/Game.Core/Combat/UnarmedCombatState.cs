using GameForWork.Core.Archetypes;
using GameForWork.Core.Ascendancies;
using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Skills;

namespace GameForWork.Core.Combat;

public readonly record struct UnarmedActionBonus(int Multiplier, int IncreasedDamage, bool ForceCritical, int ConsumedCombo);

public sealed class UnarmedCombatState(CombatProfile? profile, bool baseCombo)
{
    private int _combo, _expires, _distance, _movementReady, _duration = 80, _counterUntil, _counterReady;
    private readonly Dictionary<string, int> _linkedComboGains = [];
    private string _target = "", _action = "";
    private readonly HashSet<string> _hitActions = [];
    private UnarmedActionBonus _bonus = new(10_000, 0, false, 0);
    private bool Has(string branch, string size = "small") => UnarmedRules.Has(profile, branch, size);
    public int Combo(int tick)
    {
        if (tick >= _expires) _combo = 0;
        return _combo;
    }
    public TeamBuild Apply(TeamBuild build, int tick, int mercyLayers = 0) => build.HasUsableWeapon ? build : build with
    {
        IncreasedAttackSpeedBasisPoints = build.IncreasedAttackSpeedBasisPoints + (Has("combo") ? Combo(tick) * 200 : 0),
        IncreasedDamageBasisPoints = build.IncreasedDamageBasisPoints + (Has("stance", "core") ? mercyLayers * 500 : 0)
    };
    public ResolvedSkill Resolve(ResolvedSkill skill, int tick, int mercyLayers = 0)
    {
        if (!UnarmedRules.IsSkill(skill.SkillId)) return skill;
        if (Has("stance", "core"))
            skill = SkillDefinitions.Get(skill.SkillId).Tags.HasFlag(SkillTag.Area)
                ? skill with { AreaIncreasedBasisPoints = skill.AreaIncreasedBasisPoints + mercyLayers * 500 }
                : skill with { RangeRaw = CombatRules.ApplyIncreased(skill.RangeRaw, mercyLayers * 500) };
        if (skill.SkillId == "archetypes.skill.skyquake_palm")
            skill = skill with { AreaIncreasedBasisPoints = skill.AreaIncreasedBasisPoints + Combo(tick) * 400 };
        if (skill.SkillId == "archetypes.skill.gale_kick" && Has("movement"))
            skill = skill with
            {
                RangeRaw = CombatRules.ApplyIncreased(skill.RangeRaw, 2_000),
                CooldownTicks = Math.Max(1, (int)Math.Ceiling(skill.CooldownTicks * 10_000d / 13_000))
            };
        return skill;
    }
    public UnarmedActionBonus Begin(string action, SkillConfiguration config, TeamBuild build, string target, int tick, bool triggered)
    {
        if (build.HasUsableWeapon || !UnarmedRules.IsSkill(config.SkillId)) return new(10_000, 0, false, 0);
        if (_action == action) return _bonus;
        _action = action;
        int layers = Combo(tick);
        if (!triggered && _target.Length > 0 && _target != target && !Has("combo", "core")) layers = _combo = 0;
        if (!triggered) _target = target;
        int more = layers == 10 && Has("combo", "core") ? 15_000 : 10_000;
        bool movement = !triggered && Has("movement", "core") && _distance >= 2_000 && tick >= _movementReady;
        if (movement) { more = CombatRules.ApplyMore(more, [16_000]); _distance = 0; _movementReady = tick + 40; }
        int consumed = !triggered && config.SkillId == "archetypes.skill.tenfold_finisher" ? layers : 0;
        if (consumed > 0)
        {
            _combo = 0;
            more = CombatRules.ApplyMore(more, [10_000 + consumed * (500 + Math.Clamp(config.Quality, 0, 20) * 10)]);
            if (Has("finisher", "core")) more = CombatRules.ApplyMore(more, [10_000 + consumed * 1_200]);
        }
        _bonus = new(more, Has("finisher") ? consumed * 800 : 0, movement, consumed);
        return _bonus;
    }
    public bool Hit(string action, SkillConfiguration config, int tick, bool triggered)
    {
        if (triggered || !UnarmedRules.IsSkill(config.SkillId) || !_hitActions.Add(action)) return false;
        int previous = Combo(tick);
        bool finisher = config.SkillId == "archetypes.skill.tenfold_finisher";
        if (finisher && Has("finisher", "core")) _combo = 5;
        else if (!finisher && (baseCombo || Has("combo") || config.SkillId == "archetypes.skill.chain_fists")) _combo = Math.Min(10, previous + 1);
        if (!finisher && _combo > previous && LinkedSupportRules.Support(config, SupportMechanic.ComboDuration))
        {
            string key = config.StoneInstanceId.Length > 0 ? config.StoneInstanceId : config.SkillId;
            int gains = _linkedComboGains.GetValueOrDefault(key) + 1;
            _linkedComboGains[key] = gains % 3;
            if (gains == 3) _combo = Math.Min(10, _combo + 1);
        }
        _duration = CombatRules.ApplyIncreased(80, LinkedSupportRules.SupportValue(config, SupportMechanic.ComboDuration, 6_000, 12_000) +
            LinkedSupportRules.SupportQuality(config, SupportMechanic.ComboDuration) * 150);
        if (_combo > 0) _expires = tick + _duration;
        return previous != _combo;
    }
    public int CounterIncrease(int tick) => Has("counter") && tick < _counterUntil ? 4_000 : 0;
    public bool Avoided(int tick, bool attack, bool unarmed)
    {
        _counterUntil = tick + 40;
        if (!attack || !unarmed || !Has("counter", "core") || tick < _counterReady) return false;
        _counterReady = tick + (Has("counter") ? 17 : 20);
        return true;
    }
    public void Moved(int distance, int tick, string skillId = "")
    {
        if (distance <= 0 || skillId == SkillIds.FlameStep || skillId == "archetypes.skill.phantom_step") return;
        _distance = (int)Math.Min(int.MaxValue, (long)_distance + distance);
        if (skillId == "archetypes.skill.gale_kick" && distance >= 2_000 && Combo(tick) > 0) _expires = tick + _duration;
    }
}
