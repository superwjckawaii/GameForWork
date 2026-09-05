using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Equipment;
using GameForWork.Core.Builds;

namespace GameForWork.Core.Combat;

public sealed partial class GuardState
{
    private int _energyReady, _overloadReady, _breakReady, _guardDuration, _guardExpires;
    private bool _guardRefreshed;
    private int _reverseBarrier, _reverseExpires, _guardBarrier;
    private int _pendingBurst;
    public int OverloadUntil { get; private set; }
    public int ReverseBarrier(int tick) => tick < _reverseExpires ? _reverseBarrier : 0;
    public int GuardBarrier(int tick) => tick < _guardExpires ? _guardBarrier : 0;
    public int SpellDamageIncrease => ArmorNode("charge", "core") ? ArmorEnergy * 400 : 0;
    private bool ArmorNode(string branch, string size = "small") => _profile.Has($"core.ascendancy.spellarmor.{branch}.{size}");

    public void StartGuard(ResourceState hero, int tick, int duration)
    {
        _guardDuration = duration; _guardExpires = tick + duration; _guardRefreshed = false;
        _guardBarrier = ArmorNode("guard") ? hero.MaximumShield * 15 / 100 : 0;
    }
    public void EnemyHit(int tick)
    {
        int previous = ArmorEnergy;
        if (ArmorNode("charge") && tick >= _energyReady) { GainEnergy(1); _energyReady = tick + 5; }
        if (ArmorNode("guard", "core") && tick < _guardExpires)
        {
            GainEnergy(1);
            if (!_guardRefreshed && previous < 10 && ArmorEnergy == 10)
            {
                _guardRefreshed = true;
                _guardExpires = tick + _guardDuration;
                Expires = _guardExpires;
            }
        }
    }
    public int ConsumeSpellEnergy() => ArmorNode("charge", "core") ? 10_000 + ConsumeEnergy() * 800 : 10_000;
    public bool TryOverload(ResourceState hero, int tick)
    {
        if (!ArmorNode("overload", "core") || tick < _overloadReady || hero.Shield < 2 || !hero.IsAlive) return false;
        if (!hero.TryPayShield(hero.Shield / 2)) return false;
        OverloadUntil = tick + 120; _overloadReady = tick + 160;
        return true;
    }
    public TeamBuild ApplyArmorBonuses(TeamBuild build, ResourceState hero, int tick)
    {
        int speed = ArmorNode("overload") && hero.Shield * 2L > hero.MaximumShield ? 1_500 : 0;
        return build with
        {
            Sheet = build.Sheet with { IncreasedArmorBasisPoints = build.Sheet.IncreasedArmorBasisPoints + (ArmorNode("charge") ? ArmorEnergy * 300 : 0) },
            IncreasedAttackSpeedBasisPoints = build.IncreasedAttackSpeedBasisPoints + speed,
            IncreasedCastSpeedBasisPoints = build.IncreasedCastSpeedBasisPoints + speed,
            MoreAttackDamageBasisPoints = CombatRules.CombineMoreBasisPoints(build.MoreAttackDamageBasisPoints, tick < OverloadUntil ? 5_000 : 0),
            MoreSpellDamageBasisPoints = CombatRules.CombineMoreBasisPoints(build.MoreSpellDamageBasisPoints, tick < OverloadUntil ? 5_000 : 0),
        };
    }
    public int AbsorbBarriers(int damage, int tick)
    {
        if (tick >= _reverseExpires) _reverseBarrier = 0;
        if (tick >= _guardExpires) _guardBarrier = 0;
        int absorbed = Math.Min(damage, _reverseBarrier);
        _reverseBarrier -= absorbed; damage -= absorbed;
        absorbed = Math.Min(damage, _guardBarrier);
        _guardBarrier -= absorbed;
        return damage - absorbed;
    }
    public void ObserveEnemyDamage(ResourceState hero, EnemyDamageResult result)
    {
        if (!hero.IsAlive) return;
        if (result.ShieldLoss > 0 && ArmorNode("absorb"))
        {
            int recovery = (int)((long)result.ShieldLoss * (ArmorNode("absorb", "core") ? 2_500 : 500) / 10_000);
            if (ArmorNode("absorb", "core") && hero.Life == hero.MaximumLife)
            {
                if (result.Tick >= _reverseExpires) _reverseBarrier = 0;
                _reverseBarrier = Math.Min(hero.MaximumLife * 3 / 10, _reverseBarrier + recovery);
                _reverseExpires = result.Tick + 80;
            }
            else hero.HealLife(recovery);
        }
        if (result.ShieldBroken && ArmorNode("break") && result.Tick >= _breakReady)
        {
            _breakReady = result.Tick + 120;
            _pendingBurst = hero.MaximumShield * (ArmorNode("break", "core") ? 100 : 30) / 100;
        }
    }
    public (int Damage, bool Empowered) TakeShieldBurst()
    {
        int damage = _pendingBurst; _pendingBurst = 0;
        return (damage, ArmorNode("break", "core"));
    }
}
