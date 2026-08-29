using GameForWork.Core.P1.Combat;
using GameForWork.Core.P17;
using GameForWork.Core.P23;

namespace GameForWork.Core.P24;

public enum P24CombatUnitKind { Hero, Mercenary, Minion, Companion, Construct, Trap, Phantom }
public enum P24UnitAiMode { Melee, Ranged, Caster, Summoner, Bodyguard, BossPriority }

public static class P24CombatCaps
{
    public const int BaseMinions = 6;
    public const int HardMinions = 16;
    public const int Companions = 1;
    public const int BaseConstructs = 3;
    public const int HardConstructs = 8;
    public const int BaseTraps = 3;
    public const int HardTraps = 8;
    public const int HardPhantoms = 6;

    public static int Clamp(P24CombatUnitKind kind, int requested) => Math.Clamp(requested, 0, kind switch
    {
        P24CombatUnitKind.Minion => HardMinions,
        P24CombatUnitKind.Companion => Companions,
        P24CombatUnitKind.Construct => HardConstructs,
        P24CombatUnitKind.Trap => HardTraps,
        P24CombatUnitKind.Phantom => HardPhantoms,
        _ => 1,
    });
}

public sealed record P24CombatUnit(
    string StableId,
    P24CombatUnitKind Kind,
    string OwnerStableId,
    int MaximumLife,
    int CurrentLife,
    int SpawnTick,
    int ExpiresTick = -1,
    int TriggerRadiusRaw = 0)
{
    public bool Alive => CurrentLife > 0;
}

public sealed class P24CombatRoster
{
    public const int TrapDurationTicks = 160;
    public const int TrapTriggerRadiusRaw = 2_000;
    private readonly List<P24CombatUnit> _units = [];
    private int _sequence;

    public IReadOnlyList<P24CombatUnit> Units => _units;
    public bool AttachedMercenaryAllowed { get; init; }
    public bool HeroAlive => _units.Any(unit => unit.Kind == P24CombatUnitKind.Hero && unit.Alive);
    public bool MercenaryAlive => _units.Any(unit => unit.Kind == P24CombatUnitKind.Mercenary && unit.Alive);
    public bool PartyFailed => !HeroAlive && (!AttachedMercenaryAllowed || !MercenaryAlive);

    public void StartBattle(string heroId, int heroLife, string? mercenaryId = null, int mercenaryLife = 0)
    {
        _units.Clear();
        _sequence = 0;
        _units.Add(New(heroId, P24CombatUnitKind.Hero, heroId, heroLife, 0));
        if (AttachedMercenaryAllowed && !string.IsNullOrWhiteSpace(mercenaryId))
            _units.Add(New(mercenaryId, P24CombatUnitKind.Mercenary, heroId, mercenaryLife, 0));
    }

    public IReadOnlyList<P24CombatUnit> InstantiateArmy(string ownerId, P24CombatUnitKind kind, int requested,
        int maximumLife, int tick, int? configuredMaximum = null)
    {
        if (kind is P24CombatUnitKind.Hero or P24CombatUnitKind.Mercenary or P24CombatUnitKind.Trap)
            throw new ArgumentOutOfRangeException(nameof(kind));
        int limit = P24CombatCaps.Clamp(kind, configuredMaximum ?? DefaultMaximum(kind));
        int existing = _units.Count(unit => unit.Kind == kind && unit.OwnerStableId == ownerId && unit.Alive);
        int create = Math.Clamp(requested, 0, Math.Max(0, limit - existing));
        var result = new List<P24CombatUnit>(create);
        for (int index = 0; index < create; index++)
        {
            P24CombatUnit unit = New($"{ownerId}:{kind.ToString().ToLowerInvariant()}:{++_sequence}", kind,
                ownerId, maximumLife, tick, kind == P24CombatUnitKind.Phantom ? tick + 80 : -1);
            _units.Add(unit);
            result.Add(unit);
        }
        return result;
    }

    public P24CombatUnit PlaceTrap(string ownerId, int maximumLife, int tick, int configuredMaximum = P24CombatCaps.BaseTraps)
    {
        int limit = P24CombatCaps.Clamp(P24CombatUnitKind.Trap, configuredMaximum);
        P24CombatUnit[] traps = _units.Where(unit => unit.Kind == P24CombatUnitKind.Trap &&
            unit.OwnerStableId == ownerId && unit.Alive).OrderBy(unit => unit.SpawnTick).ThenBy(unit => unit.StableId).ToArray();
        if (traps.Length >= limit && traps.Length > 0) _units.Remove(traps[0]);
        P24CombatUnit trap = New($"{ownerId}:trap:{++_sequence}", P24CombatUnitKind.Trap, ownerId, maximumLife,
            tick, tick + TrapDurationTicks, TrapTriggerRadiusRaw);
        _units.Add(trap);
        return trap;
    }

    public bool Damage(string stableId, int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        int index = _units.FindIndex(unit => unit.StableId == stableId);
        if (index < 0) return false;
        P24CombatUnit unit = _units[index];
        _units[index] = unit with { CurrentLife = Math.Max(0, unit.CurrentLife - amount) };
        return true;
    }

    public int Expire(int tick)
    {
        int before = _units.Count;
        _units.RemoveAll(unit => unit.ExpiresTick >= 0 && unit.ExpiresTick <= tick);
        return before - _units.Count;
    }

    private static int DefaultMaximum(P24CombatUnitKind kind) => kind switch
    {
        P24CombatUnitKind.Minion => P24CombatCaps.BaseMinions,
        P24CombatUnitKind.Companion => P24CombatCaps.Companions,
        P24CombatUnitKind.Construct => P24CombatCaps.BaseConstructs,
        P24CombatUnitKind.Phantom => P24CombatCaps.HardPhantoms,
        _ => 1,
    };

    private static P24CombatUnit New(string id, P24CombatUnitKind kind, string owner, int life, int tick,
        int expires = -1, int triggerRadius = 0)
    {
        if (life <= 0) throw new ArgumentOutOfRangeException(nameof(life));
        return new P24CombatUnit(id, kind, owner, life, life, tick, expires, triggerRadius);
    }
}

public sealed record P24UnitAiProfile(P24UnitAiMode Mode, int PreferredDistanceRaw, int RetreatDistanceRaw,
    bool MoveWhileUsing, bool PrioritizeRareAndBoss);

public static class P24UnitAiRules
{
    public static P24UnitAiProfile ForMainSkill(P17ActiveSkillDefinition skill, P24CombatUnitKind unitKind)
    {
        if (unitKind == P24CombatUnitKind.Construct)
            return new(P24UnitAiMode.BossPriority, 8_000, 2_000, false, true);
        if (unitKind == P24CombatUnitKind.Minion)
            return new(P24UnitAiMode.Bodyguard, 1_500, 0, false, true);
        if (P24SkillCatalog.TryActiveForSkill(skill.SkillId, out P24ActiveSkillDefinition? p24) &&
            p24!.Mechanic is P24SkillMechanic.Minion or P24SkillMechanic.Companion or P24SkillMechanic.Construct)
            return new(P24UnitAiMode.Summoner, 9_000, 4_000, false, true);
        if (skill.Capabilities.HasFlag(P17SkillCapability.Projectile) && skill.Capabilities.HasFlag(P17SkillCapability.Attack))
            return new(P24UnitAiMode.Ranged, 8_000, 3_000, true, true);
        if (skill.Capabilities.HasFlag(P17SkillCapability.Spell))
            return new(P24UnitAiMode.Caster, 7_000, 2_500, false, true);
        return new(P24UnitAiMode.Melee, 1_500, 500, false, true);
    }
}

public sealed class P24ContributionReport
{
    private readonly Dictionary<P24CombatUnitKind, long> _damage = [];
    public IReadOnlyDictionary<P24CombatUnitKind, long> Damage => _damage;
    public void Add(P24CombatUnitKind source, int damage)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(damage);
        _damage[source] = checked(_damage.GetValueOrDefault(source) + damage);
    }
}
