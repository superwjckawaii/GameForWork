using GameForWork.Core.Ascendancies;

namespace GameForWork.Core.Campaign.Combat;

public sealed partial class ResourceState
{
    private readonly CombatProfile _ascendancy;
    private int _lastRechargeTick, _gateReady;
    private long _overchargeDecayRemainder;
    private bool AegisNode(string branch, string size = "small") => _ascendancy.Has($"core.ascendancy.aegis_mage.{branch}.{size}");
    public int Overcharge { get; private set; }
    public int MaximumOvercharge => _ascendancy.Ascendancy == Ascendancy.AegisMage
        ? (int)((long)MaximumShield * (3_000 + (AegisNode("maximum", "core") ? 1_000 : 0) + (AegisNode("recharge", "core") ? 2_000 : 0)) / 10_000) : 0;
    public bool ShieldGate { get; private set; }
    public bool CanFundSpells => AegisNode("casting", "core");
    public bool LastSpellFullyFunded { get; private set; }
    public int AvailableSpellMana => (int)Math.Min(int.MaxValue, (long)Mana + (CanFundSpells ? Overcharge : 0));

    public bool TryPaySpellMana(int amount, bool allowOvercharge = true)
    {
        LastSpellFullyFunded = false;
        int available = allowOvercharge ? AvailableSpellMana : Mana;
        if (amount < 0 || available < amount || !IsAlive) return false;
        int shield = allowOvercharge && CanFundSpells ? Math.Min(amount, Overcharge) : 0;
        Overcharge -= shield;
        Mana -= amount - shield;
        LastSpellFullyFunded = amount > 0 && shield == amount;
        return true;
    }

    private void RechargeOvercharge(int amount, int tick)
    {
        if (!IsAlive || MaximumOvercharge == 0) return;
        _lastRechargeTick = tick;
        _overchargeDecayRemainder = 0;
        Overcharge = (int)Math.Min(MaximumOvercharge, (long)Overcharge + Math.Max(0, amount));
        if (AegisNode("absorb", "core") && tick >= _gateReady && Overcharge == MaximumOvercharge) ShieldGate = true;
    }

    private void DecayOvercharge(int tick)
    {
        int wait = AegisNode("recharge") ? 40 : 20;
        if (tick - _lastRechargeTick <= wait || Overcharge == 0) return;
        int rate = AegisNode("recharge", "core") ? 500 : 1_000;
        _overchargeDecayRemainder += (long)MaximumShield * rate;
        Overcharge = Math.Max(0, Overcharge - (int)(_overchargeDecayRemainder / 200_000));
        _overchargeDecayRemainder %= 200_000;
    }

    private readonly record struct EnemyDamagePlan(int Remaining, int ManaLoss, int OverchargeLoss, bool Gate);

    private EnemyDamagePlan PlanEnemyDamage(int amount, bool hit)
    {
        int mana = hit ? Math.Min(Mana, (int)((long)amount * _manaDamageShare / 10_000)) : 0;
        amount -= mana;
        if (hit && ShieldGate) return new(Math.Min(amount, Math.Max(0, Shield - 1)), mana, 0, true);
        int absorbed = 0;
        if (Overcharge > 0)
        {
            int multiplier = hit && AegisNode("absorb") ? 8_000 : 10_000;
            int reduced = (int)((long)amount * multiplier / 10_000);
            absorbed = Math.Min(Overcharge, reduced);
            amount = reduced <= absorbed ? 0 : Math.Max(0, amount - (int)(((long)absorbed * 10_000 + multiplier - 1) / multiplier));
        }
        return new(amount, mana, absorbed, false);
    }

    public bool WouldEnemyHitBeLethal(int amount) => IsAlive && amount > 0 && PlanEnemyDamage(amount, true).Remaining >= (long)Life + Shield;

    /// <summary>Only enemy damage enters this path. Payments and decay cannot fire the gate or break reactions.</summary>
    public int ApplyEnemyDamage(int amount, bool hit, int tick)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        if (!IsAlive || amount == 0) return 0;
        var plan = PlanEnemyDamage(amount, hit);
        Mana -= plan.ManaLoss;
        if (plan.Gate)
        {
            ShieldGate = false;
            _gateReady = tick + 200;
            Overcharge = 0;
            LastDamageTick = tick;
        }
        else
        {
            if (plan.ManaLoss > 0 || Overcharge > 0) LastDamageTick = tick;
            Overcharge -= plan.OverchargeLoss;
        }
        int actual = (int)Math.Min(plan.Remaining, (long)Life + Shield);
        ApplyDamage(plan.Remaining, tick);
        return plan.ManaLoss + plan.OverchargeLoss + actual;
    }
}
