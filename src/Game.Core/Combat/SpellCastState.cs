using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Progression;
using GameForWork.Core.Skills;
namespace GameForWork.Core.Combat;
public sealed class SpellCastState
{
    private int _casts, _channelUntil = -1, _channelMultiplier = 10000, _recoveryReady;
    private string _channel = "";
    private bool _channelFree;
    private readonly Dictionary<string, int> _multipliers = [];
    private bool Continuing(string id, SkillTag tags, int tick) => tags.HasFlag(SkillTag.Channelling) && id == _channel && tick <= _channelUntil;
    public ResolvedSkill Quote(ResolvedSkill skill, PassiveModifiers p, int tick)
    {
        var tags = SkillDefinitions.Get(skill.SkillId).Tags | skill.AdditionalTags;
        if (!SpellMasteryRules.Self(skill.SkillId, tags) || !SpellMasteryRules.Has(p, 6)) return skill;
        bool free = Continuing(skill.SkillId, tags, tick) ? _channelFree : (_casts + 1) % 3 == 0;
        return free ? skill with { WaiveManaCost = true } : skill;
    }
    public void Started(string action, string id, SkillTag tags, PassiveModifiers p, int tick, bool highMana)
    {
        if (!SpellMasteryRules.Self(id, tags))
        {
            if ((tags & (SkillTag.Trigger | SkillTag.Counter)) == 0) _channel = "";
            return;
        }
        bool continuing = Continuing(id, tags, tick);
        if (!continuing)
        {
            _casts++;
            _channelFree = SpellMasteryRules.Has(p, 6) && _casts % 3 == 0;
            _channelMultiplier = CombatRules.ApplyMore(10000, [highMana && SpellMasteryRules.Has(p, 2) ? 14000 : 10000, _channelFree ? 14000 : 10000]);
        }
        _multipliers[action] = _channelMultiplier;
        _channel = tags.HasFlag(SkillTag.Channelling) ? id : "";
        _channelUntil = tick + 5;
    }
    public int Multiplier(string action) => _multipliers.GetValueOrDefault(action, 10000);
    public int HitRecovery(PassiveModifiers p, bool self, bool rare, int tick, int maximumMana)
    {
        if (!self || !rare || tick < _recoveryReady || !SpellMasteryRules.Has(p, 5)) return 0;
        _recoveryReady = tick + 10;
        return (int)((long)maximumMana * 200 / 10000);
    }
}
