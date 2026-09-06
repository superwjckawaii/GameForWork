namespace GameForWork.Core.Campaign.Combat;

public sealed partial class ResourceState
{
    private int _enemyHitUntil, _attackHitUntil, _evadedUntil, _evadeChargeUntil, _evadeChargeReady;

    public int MasteryEvasion(int tick) => (int)Math.Min(int.MaxValue,
        (long)Sheet.Evasion().Value * (Mastery("闪避", 4) && tick < _attackHitUntil ? 2 : 1));

    public int MasteryArmor(int armor, int incomingHit, EnemyDamageType type, int tick)
    {
        long result = armor;
        if (Mastery("护甲", 5) && tick < _enemyHitUntil) result = result * 3 / 2;
        if (type == EnemyDamageType.Physical && Mastery("护甲", 4) && incomingHit * 5L >= (long)MaximumLife + MaximumShield) result *= 2;
        if (type is EnemyDamageType.Fire or EnemyDamageType.Cold or EnemyDamageType.Lightning)
            result = Mastery("护甲", 2) ? result * 3 / 10 : 0;
        if (type == EnemyDamageType.Void) result = 0;
        return (int)Math.Clamp(result, 0, int.MaxValue);
    }

    public int IncomingMasteryCriticalMultiplier(int multiplier, int additionalReduction = 0) =>
        10_000 + (int)((long)Math.Max(0, multiplier - 10_000) *
            Math.Max(0, 10_000 - additionalReduction - (Mastery("护甲", 3) ? 5_000 : 0)) / 10_000);

    public bool LuckyEvasion => Mastery("闪避", 2);
    public int RecentEvadeHitMultiplier(int tick) => Mastery("闪避", 1) && tick < _evadedUntil ? 8_500 : 10_000;

    public void ObserveEnemyHit(bool attack, int tick)
    {
        _enemyHitUntil = tick + 80;
        if (attack) _attackHitUntil = tick + 40;
    }

    public void ObserveEvade(int tick)
    {
        _evadedUntil = tick + 80;
        if (!Mastery("闪避", 5) || tick < _evadeChargeReady) return;
        _evadeChargeUntil = tick + 80;
        _evadeChargeReady = tick + 20;
    }

    private int ConsumeEvadeCharge()
    {
        if (_resourceTick >= _evadeChargeUntil) return 10_000;
        _evadeChargeUntil = 0;
        return 14_000;
    }
}
