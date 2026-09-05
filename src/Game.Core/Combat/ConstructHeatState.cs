using GameForWork.Core.Ascendancies;

namespace GameForWork.Core.Combat;

/// <summary>Heat advances once per completed action, independent of hits and extra projectiles.</summary>
public sealed class ConstructHeatState(CombatConfiguration configuration)
{
    private int _lastAction, _lastDecay, _actions;
    public int Heat { get; private set; }
    public int OverheatedUntil { get; private set; }
    public bool CanAct(int tick) => tick >= OverheatedUntil;
    public int DamageIncrease => Heat / 10 * (configuration.Has(ConstructModule.Firepower) ? 500 : 300);
    public int SpeedIncrease => Heat / 10 * 200;
    public int HitMultiplier => 10_000 - (configuration.Has(ConstructModule.Guardian) ? Heat / 10 * 100 : 0);
    public bool FinalAction => configuration.Has(ConstructModule.Stabilizer) ? (_actions + 1) % 10 == 0 : Heat >= 90;
    public void Advance(int tick)
    {
        if (OverheatedUntil > 0)
        {
            if (tick < OverheatedUntil) return;
            Reset(tick);
            return;
        }
        int start = Math.Max(_lastAction + 20, _lastDecay);
        if (tick > start) { Heat = Math.Max(0, Heat - (tick - start)); _lastDecay = tick; }
    }
    public void Complete(int tick)
    {
        _lastAction = _lastDecay = tick;
        _actions++;
        Heat = Math.Min(configuration.Has(ConstructModule.Stabilizer) ? 70 : 100, Heat + 10);
        if (Heat == 100) OverheatedUntil = tick + 40;
    }
    public void Reset(int tick)
    {
        Heat = OverheatedUntil = _actions = 0;
        _lastAction = _lastDecay = tick;
    }
}
