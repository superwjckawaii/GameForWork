using GameForWork.Core.Builds;
using GameForWork.Core.SkillCatalog;

namespace GameForWork.Core.Combat;

/// <summary>Enemy-owned negative effects and their immunity windows, separate from player buffs.</summary>
public sealed class HarmfulStatus
{
    private readonly Dictionary<Ailment, (int Effect, int Expires)> _effects = [];
    private readonly Dictionary<Ailment, int> _immuneUntil = [];
    private int _curseImmuneUntil;
    public int Tick { get; set; }
    public int Generation { get; private set; }
    public AilmentState DamageOverTime { get; } = new();
    public CurseState Curses { get; } = new();
    public int Effect(Ailment kind) => _effects.TryGetValue(kind, out var effect) && effect.Expires > Tick ? effect.Effect : 0;
    public bool Immune(Ailment kind) => _immuneUntil.GetValueOrDefault(kind) > Tick;
    public bool Apply(Ailment kind, int effect, int durationTicks)
    {
        if (Immune(kind) || effect <= 0 || durationTicks <= 0) return false;
        _effects[kind] = (Math.Max(Effect(kind), effect), Tick + durationTicks);
        return true;
    }
    public bool ApplyDot(Ailment kind, DamageType type, decimal dps, int durationMilliseconds, string source)
    {
        if (Immune(kind) || dps <= 0 || durationMilliseconds <= 0) return false;
        DamageOverTime.Apply(kind, type, dps, durationMilliseconds, 0, source);
        return true;
    }
    public void ApplyCurse(string id, int effect, int durationTicks)
    {
        if (_curseImmuneUntil <= Tick) Curses.Apply(id, effect, 0, Tick + durationTicks, 10, Tick);
    }
    public void Cleanse(int immunityTicks, params Ailment[] kinds)
    {
        DamageOverTime.Remove(kinds);
        foreach (var kind in kinds) { _effects.Remove(kind); _immuneUntil[kind] = Math.Max(_immuneUntil.GetValueOrDefault(kind), Tick + immunityTicks); }
    }
    public void CleanseCurses(int immunityTicks)
    {
        foreach (var curse in Curses.Active(Tick)) Curses.Remove(curse.Id);
        _curseImmuneUntil = Math.Max(_curseImmuneUntil, Tick + immunityTicks);
    }
    public void Clear()
    {
        Generation++;
        Cleanse(0, Enum.GetValues<Ailment>());
        CleanseCurses(0);
    }
}
