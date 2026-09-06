using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Campaign.Progression;
using GameForWork.Core.Spatial;
using GameForWork.Core.Archetypes;
using GameForWork.Core.Skills;
using GameForWork.Core.Ascendancies;
using static GameForWork.Core.Skills.LinkedSupportRules;

namespace GameForWork.Core.Combat;

public sealed record CombatBuff(string Id, int Expires, int Radius, int DamageIncrease = 0,
    int ActionSpeed = 0, int MovementSpeed = 0, int Resistance = 0, string TargetId = "", int MoreDamage = 0);
public readonly record struct UnitBuff(int DamageIncrease, int ActionSpeed, int MovementSpeed, int Resistance, int MoreDamage = 0);

public sealed class CombatBuffState(CombatProfile? profile = null)
{
    private readonly Dictionary<string, CombatBuff> _active = [];
    private SkillConfiguration? _stance;
    private SkillConfiguration? _blessingConfiguration;
    private int _stanceReady, _overlapUntil, _overlapReady;
    private SkillConfiguration? _previousStance;
    private int _warSongLayers, _warSongUntil;
    private bool Cantor(string branch, string size = "small") => profile?.Has($"core.ascendancy.spirit_cantor.{branch}.{size}") == true;
    public int WarSongMore(int tick) => tick < _warSongUntil ? _warSongLayers * 1_500 : 0;
    public bool Instant(string id) => id == "archetypes.skill.soul_warsong" && Cantor("war_song", "core");
    public int CooldownRecovery(string id) => id == "archetypes.skill.fellowship_blessing" && Cantor("blessing") ? 2_500 : 0;
    public static bool IsSkill(string id) => id is "archetypes.skill.fellowship_blessing" or "archetypes.skill.soul_warsong" or
        "archetypes.skill.yin_yang_stance" or "archetypes.skill.king_soul_command";
    public bool CanUse(string id, int tick) => id == "archetypes.skill.soul_warsong" || (id == "archetypes.skill.yin_yang_stance" ? _stance is null && tick >= _stanceReady :
        !_active.TryGetValue(id, out var buff) || tick >= buff.Expires);
    public bool CanUse(SkillConfiguration skill, int tick)
    {
        if (skill.SkillId != "archetypes.skill.yin_yang_stance") return CanUse(skill.SkillId, tick);
        if (tick < _stanceReady) return false;
        if (_stance is null) return true;
        if (skill.Mode.Length > 0) return Yin(skill) != Yin(_stance);
        return UnarmedRules.Has(profile, "stance", "core") && tick >= _overlapReady;
    }
    private static bool Yin(SkillConfiguration skill) => skill.Mode is "Yin" or "阴" or "阴式";
    private SkillConfiguration? Stance(bool yin, int tick) => _stance is { } current && Yin(current) == yin ? current :
        tick < _overlapUntil && _previousStance is { } previous && Yin(previous) == yin ? previous : null;
    public bool Activate(SkillConfiguration skill, bool unarmed, int tick, string target = "")
    {
        string id = skill.SkillId;
        if (!IsSkill(id)) return false;
        if (id == "archetypes.skill.yin_yang_stance")
        {
            if (!unarmed || tick < _stanceReady) return false;
            if (skill.Mode.Length == 0) skill = skill with { Mode = _stance is null || Yin(_stance) ? "Yang" : "Yin" };
            if (_stance is not null && Yin(_stance) == Yin(skill)) return false;
            if (_stance is not null && UnarmedRules.Has(profile, "stance", "core") && tick >= _overlapReady)
            {
                _previousStance = _stance;
                _overlapUntil = _overlapReady = tick + 60;
            }
            _stance = skill;
            _stanceReady = tick + (int)Math.Ceiling((16 + SupportValue(skill, SupportMechanic.StanceAmplify, 20, 10)) *
                10_000d / (10_000 + SupportQuality(skill, SupportMechanic.StanceAmplify) * 100));
            return true;
        }
        int blessingEffect = id == "archetypes.skill.fellowship_blessing" ? SupportValue(skill, SupportMechanic.LastingBlessing, 2_000, 3_500) : 0;
        if (id == "archetypes.skill.fellowship_blessing") _blessingConfiguration = skill;
        if (id == "archetypes.skill.fellowship_blessing" && Cantor("blessing")) blessingEffect += 2_000;
        if (id == "archetypes.skill.soul_warsong" && Cantor("war_song")) blessingEffect += 2_500;
        int Value(int one, int maximum) => CombatRules.ApplyIncreased(ActiveSkillCatalog.Interpolate(one, maximum, skill.Level, false), blessingEffect);
        int duration = CombatRules.ApplyIncreased(id == "archetypes.skill.fellowship_blessing" ? 160 : 120,
            skill.Quality * 100 + (id == "archetypes.skill.fellowship_blessing" ? SupportValue(skill, SupportMechanic.LastingBlessing, 5_000, 10_000) + (Cantor("blessing") ? 3_000 : 0) :
                id == "archetypes.skill.soul_warsong" && Cantor("war_song") ? 3_000 : 0));
        if (id == "archetypes.skill.soul_warsong" && Cantor("war_song", "core"))
        {
            _warSongLayers = Math.Min(3, tick < _warSongUntil ? _warSongLayers + 1 : 1);
            _warSongUntil = tick + 120;
        }
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
        return new(damage, speed, movement, resistance, WarSongMore(tick));
    }
    public int IncomingHitMultiplier(bool unarmed, int tick = 0) => IncomingDamageMultiplier(unarmed, tick, true);
    public int IncomingDamageMultiplier(bool unarmed, int tick, bool hit)
    {
        if (!unarmed || Stance(true, tick) is not { } stance) return 10_000;
        int value = hit ? 10_000 - StanceValue(stance, 800, 1_200) : 10_000;
        return UnarmedRules.Has(profile, "stance") ? CombatRules.ApplyMore(value, [9_000]) : value;
    }
    public TeamBuild Apply(TeamBuild build, int tick)
    {
        int warSong = WarSongMore(tick);
        if (warSong > 0)
        {
            var passive = build.PassiveProfile ?? PassiveModifiers.Empty;
            build = build with
            {
                WarSongMoreDamageBasisPoints = warSong,
                PassiveProfile = passive with { MoreDamageBasisPoints = CombatRules.CombineMoreBasisPoints(passive.MoreDamageBasisPoints, warSong) }
            };
        }
        if (_active.TryGetValue("archetypes.skill.fellowship_blessing", out var blessing) && tick < blessing.Expires)
        {
            if (Cantor("blessing", "core") && _blessingConfiguration is { } configuration)
            {
                int effect = 4_000 + (Cantor("blessing") ? 2_000 : 0) + SupportValue(configuration, SupportMechanic.LastingBlessing, 2_000, 3_500);
                int Value(int one, int maximum) => CombatRules.ApplyIncreased(ActiveSkillCatalog.Interpolate(one, maximum, configuration.Level, false), effect);
                blessing = blessing with
                {
                    DamageIncrease = Value(2_500, 4_000),
                    ActionSpeed = Value(1_200, 2_000),
                    MovementSpeed = Value(1_200, 2_000),
                    Resistance = Value(1_000, 1_500)
                };
            }
            build = build with
            {
                IncreasedDamageBasisPoints = build.IncreasedDamageBasisPoints + blessing.DamageIncrease,
                IncreasedSpellDamageBasisPoints = build.IncreasedSpellDamageBasisPoints + blessing.DamageIncrease,
                IncreasedActionSpeedBasisPoints = build.IncreasedActionSpeedBasisPoints + blessing.ActionSpeed,
                MovementSpeedBasisPoints = build.MovementSpeedBasisPoints + blessing.MovementSpeed,
                Sheet = build.Sheet with
                {
                    FireResistanceBasisPoints = build.Sheet.FireResistanceBasisPoints + blessing.Resistance,
                    ColdResistanceBasisPoints = build.Sheet.ColdResistanceBasisPoints + blessing.Resistance,
                    LightningResistanceBasisPoints = build.Sheet.LightningResistanceBasisPoints + blessing.Resistance,
                    VoidResistanceBasisPoints = build.Sheet.VoidResistanceBasisPoints + blessing.Resistance
                },
            };
        }
        if (build.HasUsableWeapon) return build;
        if (Stance(false, tick) is { } yang) build = build with
        {
            IncreasedDamageBasisPoints = build.IncreasedDamageBasisPoints + StanceValue(yang, 2_500, 4_500) +
                (UnarmedRules.Has(profile, "stance") ? 2_000 : 0),
            IncreasedAttackSpeedBasisPoints = build.IncreasedAttackSpeedBasisPoints + StanceValue(yang, 1_200, 2_000)
        };
        if (Stance(true, tick) is { } yin)
        {
            int block = StanceValue(yin, 600, 1_000);
            build = build with
            {
                MoreAttackDamageBasisPoints = CombatRules.CombineMoreBasisPoints(build.MoreAttackDamageBasisPoints, -StanceValue(yin, 2_000, 2_000)),
                BlockChanceBasisPoints = build.BlockChanceBasisPoints + block,
                Sheet = build.Sheet with { SpellBlockChanceBasisPoints = build.Sheet.SpellBlockChanceBasisPoints + block }
            };
        }
        return build;
    }
    private static int StanceValue(SkillConfiguration skill, int one, int maximum) => CombatRules.ApplyIncreased(
        ActiveSkillCatalog.Interpolate(one, maximum, skill.Level, false), skill.Quality * 50 + SupportValue(skill, SupportMechanic.StanceAmplify, 3_000, 5_000));
}
