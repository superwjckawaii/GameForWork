namespace GameForWork.Core.Combat;

public sealed class CombatConditionState
{
    public int BlockRecentUntil { get; private set; }
    private int _guaranteedBlockReady = 60, _blockChargeReady, _blockChargeUntil;
    public bool GuaranteedBlock(GameForWork.Core.Campaign.Progression.PassiveModifiers p, int tick) => GameForWork.Core.Builds.BlockMasteryRules.Has(p, 6) && tick >= _guaranteedBlockReady;
    public void Blocked(GameForWork.Core.Campaign.Progression.PassiveModifiers p, int tick)
    {
        BlockRecentUntil = tick + 80; _guaranteedBlockReady = tick + 60;
        if (GameForWork.Core.Builds.BlockMasteryRules.Has(p, 5) && tick >= _blockChargeReady) { _blockChargeUntil = tick + 80; _blockChargeReady = tick + 20; }
    }
    public int ConsumeBlockCharge(int tick, bool self)
    {
        if (!self || tick >= _blockChargeUntil) return 10000;
        _blockChargeUntil = 0; return 15000;
    }
    public int HitRecentUntil { get; private set; }
    public int DamageOverTimeRecentUntil { get; private set; }
    public void Damaged(bool hit, int tick) { if (hit) HitRecentUntil = tick + 80; else DamageOverTimeRecentUntil = tick + 20; }
    public int ElementalHitRecentUntil { get; private set; }
    public int VoidHitRecentUntil { get; private set; }
    public void EnemyHit(GameForWork.Core.Campaign.Combat.EnemyDamageType type, int tick)
    {
        if (type == GameForWork.Core.Campaign.Combat.EnemyDamageType.Void) VoidHitRecentUntil = tick + 80;
        else if (type != GameForWork.Core.Campaign.Combat.EnemyDamageType.Physical) ElementalHitRecentUntil = tick + 80;
    }
    public int StunRecentUntil { get; private set; }
    public int SuppressionRecentUntil { get; private set; }
    public void Suppressed(int tick) => SuppressionRecentUntil = tick + 80;
    public int KillRecentUntil { get; private set; }
    public void Stunned(int tick) => StunRecentUntil = tick + 80;
    public void Killed(int tick) => KillRecentUntil = tick + 80;
    private string _attackTarget = "";
    private int _attackLayers, _attackUntil;
    private readonly HashSet<string> _attackActions = [];
    public void SelectAttackTarget(string target)
    {
        if (_attackTarget == target) return;
        _attackTarget = target; _attackLayers = 0; _attackUntil = 0;
    }
    public void AttackHit(string target, string action, int tick, bool self)
    {
        if (!self || target != _attackTarget || !_attackActions.Add(action)) return;
        if (tick >= _attackUntil) _attackLayers = 0;
        _attackLayers = Math.Min(8, _attackLayers + 1); _attackUntil = tick + 80;
    }
    public int AttackMultiplier(string target, int tick) => target == _attackTarget && tick < _attackUntil ? 10_000 + _attackLayers * 400 : 10_000;
}
