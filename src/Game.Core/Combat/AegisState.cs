using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Equipment;

namespace GameForWork.Core.Combat;

public sealed partial class GuardState
{
    private int _polarizedUntil, _echoUntil, _echoReady, _zeroReady, _immuneUntil, _mirrorReady;
    private readonly Queue<(string Target, int Multiplier)> _aegisReplays = [];
    private bool AegisNode(string branch, string size = "small") => _profile.Has($"core.ascendancy.aegis_mage.{branch}.{size}");
    public int SpellBlockBonus(int tick) => tick < _polarizedUntil ? 2_000 : 0;
    public bool Immune(int tick) => tick < _immuneUntil;
    public void EnemySpellHit(string source, bool blocked, int tick)
    {
        if (!blocked && AegisNode("counter")) _polarizedUntil = tick + 40;
        if (blocked && AegisNode("counter", "core") && tick >= _mirrorReady)
        {
            _mirrorReady = tick + 20;
            _aegisReplays.Enqueue((source, 6_000));
        }
    }
    private void ObserveAegisDamage(EnemyDamageResult result)
    {
        if (!result.ShieldBroken) return;
        if (AegisNode("break") && result.Tick >= _echoReady)
        {
            _echoUntil = result.Tick + 80; _echoReady = result.Tick + 160;
        }
        if (AegisNode("break", "core") && result.Tick >= _zeroReady)
        {
            _immuneUntil = result.Tick + 40; _zeroReady = result.Tick + 240;
            _aegisReplays.Enqueue(("", 10_000));
        }
    }
    public IReadOnlyList<(string Target, int Multiplier)> TakeAegisReplays()
    {
        var result = _aegisReplays.ToArray(); _aegisReplays.Clear(); return result;
    }
    private TeamBuild ApplyAegisBonuses(TeamBuild build, ResourceState hero, int tick) => build with
    {
        IncreasedSpellDamageBasisPoints = build.IncreasedSpellDamageBasisPoints + (tick < _echoUntil ? 3_000 : 0),
        IncreasedCastSpeedBasisPoints = build.IncreasedCastSpeedBasisPoints + (tick < _echoUntil ? 1_500 : 0) +
            (AegisNode("casting") && (hero.Shield + (long)hero.Overcharge) * 2 > hero.MaximumShield ? 2_000 : 0),
    };
}
