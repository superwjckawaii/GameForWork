using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Spatial;

namespace GameForWork.Core.Combat;

public sealed record CombatBuff(string Id, int Expires, int Radius, int DamageIncrease = 0,
    int ActionSpeed = 0, int MovementSpeed = 0, int Resistance = 0, string TargetId = "", int MoreDamage = 0);
public readonly record struct UnitBuff(int DamageIncrease, int ActionSpeed, int MovementSpeed, int Resistance);

public sealed class CombatBuffState
{
    private readonly Dictionary<string, CombatBuff> _active = [];
    private SkillConfiguration? _stance;
    private int _stanceReady;
    public static bool IsSkill(string id) => id is "archetypes.skill.fellowship_blessing" or "archetypes.skill.soul_warsong" or
        "archetypes.skill.yin_yang_stance" or "archetypes.skill.king_soul_command";
    public bool CanUse(string id, int tick) => id == "archetypes.skill.yin_yang_stance" ? _stance is null && tick >= _stanceReady :
        !_active.TryGetValue(id, out var buff) || tick >= buff.Expires;
    public bool Activate(SkillConfiguration skill, bool unarmed, int tick, string target = "")
    {
        string id = skill.SkillId;
        if (!IsSkill(id)) return false;
        if (id == "archetypes.skill.yin_yang_stance")
        {
            if (!unarmed || tick < _stanceReady) return false;
            _stance = skill; _stanceReady = tick + 16;
            return true;
        }
        int Value(int one, int maximum) => ActiveSkillCatalog.Interpolate(one, maximum, skill.Level, false);
        int duration = CombatRules.ApplyIncreased(id == "archetypes.skill.fellowship_blessing" ? 160 : 120, skill.Quality * 100);
        _active[id] = id switch
        {
            "archetypes.skill.fellowship_blessing" => new(id, tick + duration, 9_000, Value(2_500, 4_000), Value(1_200, 2_000), Value(1_200, 2_000), Value(1_000, 1_500)),
            "archetypes.skill.soul_warsong" => new(id, tick + duration, 10_000, ActionSpeed: Value(2_500, 4_000), MovementSpeed: Value(2_000, 3_500)),
            _ => new(id, tick + duration, int.MaxValue, MovementSpeed: 3_000, TargetId: target, MoreDamage: Value(2_000, 3_500)),
        };
        return true;
    }
    public CombatBuff? Command(int tick) => _active.TryGetValue("archetypes.skill.king_soul_command", out var buff) && tick < buff.Expires ? buff : null;
    public UnitBuff ForUnit(int tick, Point hero, Point unit, bool minion = false)
    {
        int damage = 0, speed = 0, movement = 0, resistance = 0;
        foreach (var buff in _active.Values.Where(buff => tick < buff.Expires && Point.DistanceSquared(hero, unit) <= (long)buff.Radius * buff.Radius))
        {
            if (buff.Id == "archetypes.skill.king_soul_command" && !minion) continue;
            damage += buff.DamageIncrease; speed += buff.ActionSpeed; movement += buff.MovementSpeed; resistance += buff.Resistance;
        }
        return new(damage, speed, movement, resistance);
    }
    public int IncomingHitMultiplier(bool unarmed) => unarmed && _stance is { Mode: "Yin" or "阴" or "阴式" } stance
        ? 10_000 - StanceValue(stance, 800, 1_200) : 10_000;
    public TeamBuild Apply(TeamBuild build, int tick)
    {
        if (_active.TryGetValue("archetypes.skill.fellowship_blessing", out var blessing) && tick < blessing.Expires)
            build = build with
            {
                IncreasedDamageBasisPoints = build.IncreasedDamageBasisPoints + blessing.DamageIncrease,
                IncreasedSpellDamageBasisPoints = build.IncreasedSpellDamageBasisPoints + blessing.DamageIncrease,
                IncreasedActionSpeedBasisPoints = build.IncreasedActionSpeedBasisPoints + blessing.ActionSpeed,
                MovementSpeedBasisPoints = build.MovementSpeedBasisPoints + blessing.MovementSpeed,
                Sheet = build.Sheet with { FireResistanceBasisPoints = build.Sheet.FireResistanceBasisPoints + blessing.Resistance,
                    ColdResistanceBasisPoints = build.Sheet.ColdResistanceBasisPoints + blessing.Resistance,
                    LightningResistanceBasisPoints = build.Sheet.LightningResistanceBasisPoints + blessing.Resistance,
                    VoidResistanceBasisPoints = build.Sheet.VoidResistanceBasisPoints + blessing.Resistance },
            };
        if (build.HasUsableWeapon || _stance is not { } stance) return build;
        bool yin = stance.Mode is "Yin" or "阴" or "阴式";
        if (!yin) return build with { IncreasedDamageBasisPoints = build.IncreasedDamageBasisPoints + StanceValue(stance, 2_500, 4_500),
            IncreasedAttackSpeedBasisPoints = build.IncreasedAttackSpeedBasisPoints + StanceValue(stance, 1_200, 2_000) };
        int block = StanceValue(stance, 600, 1_000);
        return build with { MoreAttackDamageBasisPoints = CombatRules.CombineMoreBasisPoints(build.MoreAttackDamageBasisPoints, -2_000),
            BlockChanceBasisPoints = build.BlockChanceBasisPoints + block,
            Sheet = build.Sheet with { SpellBlockChanceBasisPoints = build.Sheet.SpellBlockChanceBasisPoints + block } };
    }
    private static int StanceValue(SkillConfiguration skill, int one, int maximum) => CombatRules.ApplyIncreased(
        ActiveSkillCatalog.Interpolate(one, maximum, skill.Level, false), skill.Quality * 50);
}
