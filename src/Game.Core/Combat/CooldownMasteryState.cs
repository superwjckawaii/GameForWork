using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Progression;
using GameForWork.Core.Skills;

namespace GameForWork.Core.Combat;

public sealed class CooldownMasteryState(PassiveModifiers? passives = null)
{
    private sealed class Entry(int charges)
    {
        public int Charges { get; set; } = charges;
        public int NextChargeTick { get; set; }
    }

    private readonly PassiveModifiers _passives = passives ?? PassiveModifiers.Empty;
    private readonly Dictionary<string, Entry> _entries = [];
    private readonly Dictionary<string, int> _overdraftReady = [];
    private readonly Dictionary<string, int> _rouletteReady = [];
    private readonly HashSet<string> _roulette = [];

    private int MaximumCharges => MasteryRuntime.Has(_passives, "触发_冷却", 4) ? 2 : 1;

    public bool CanUse(ResolvedSkill skill, int tick, ResourceState hero)
    {
        if (skill.CooldownTicks <= 1) return true;
        Entry entry = Advance(skill, tick);
        return entry.Charges > 0 || MasteryRuntime.Has(_passives, "触发_冷却", 5) &&
            tick >= _overdraftReady.GetValueOrDefault(skill.SkillId) &&
            hero.Mana >= (long)hero.MaximumMana / 5 + skill.ManaCost;
    }

    public bool Start(ResolvedSkill skill, int tick, ResourceState hero)
    {
        if (skill.CooldownTicks <= 1) return true;
        Entry entry = Advance(skill, tick);
        if (entry.Charges > 0)
        {
            if (entry.Charges-- == MaximumCharges) entry.NextChargeTick = tick + skill.CooldownTicks;
        }
        else
        {
            int mana = hero.MaximumMana / 5;
            if (!MasteryRuntime.Has(_passives, "触发_冷却", 5) ||
                tick < _overdraftReady.GetValueOrDefault(skill.SkillId) || !hero.TryPayMana(mana)) return false;
            _overdraftReady[skill.SkillId] = tick + 80;
        }
        Rotate(skill.SkillId, tick);
        return true;
    }

    public void Reset(ResolvedSkill skill, int tick)
    {
        Entry entry = Get(skill.SkillId);
        entry.Charges = MaximumCharges;
        entry.NextChargeTick = tick;
    }

    public int Charges(ResolvedSkill skill, int tick) => Advance(skill, tick).Charges;
    public int Remaining(string id, int tick) => Math.Max(0, Get(id).NextChargeTick - tick);

    private Entry Advance(ResolvedSkill skill, int tick)
    {
        Entry entry = Get(skill.SkillId);
        while (entry.Charges < MaximumCharges && tick >= entry.NextChargeTick)
        {
            entry.Charges++;
            if (entry.Charges < MaximumCharges) entry.NextChargeTick += skill.CooldownTicks;
        }
        return entry;
    }

    private Entry Get(string id)
    {
        if (!_entries.TryGetValue(id, out Entry? entry))
            _entries[id] = entry = new(MaximumCharges);
        return entry;
    }

    private void Rotate(string id, int tick)
    {
        if (!MasteryRuntime.Has(_passives, "触发_冷却", 6) || tick < _rouletteReady.GetValueOrDefault(id)) return;
        _roulette.Add(id);
        _rouletteReady[id] = tick + 80;
        if (_roulette.Count < 3) return;
        foreach (string skill in _roulette)
        {
            Entry entry = Get(skill);
            int remaining = Math.Max(0, entry.NextChargeTick - tick);
            entry.NextChargeTick = tick + remaining * 7 / 10;
        }
        _roulette.Clear();
    }
}
