using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.SkillCatalog;
using GameForWork.Core.Characters;

namespace GameForWork.Core.Archetypes;

public enum CombatUnitKind { Hero, Mercenary, Minion, Companion, Construct, Trap, Phantom }
public enum UnitAiMode { Melee, Ranged, Caster, Summoner, Bodyguard, BossPriority }

public static class CombatCaps
{
    public const int BaseMinions = 6;
    public const int HardMinions = 16;
    public const int Companions = 1;
    public const int BaseConstructs = 3;
    public const int HardConstructs = 8;
    public const int BaseTraps = 3;
    public const int HardTraps = 8;
    public const int HardPhantoms = 6;

    public static int Clamp(CombatUnitKind kind, int requested) => Math.Clamp(requested, 0, kind switch
    {
        CombatUnitKind.Minion => HardMinions,
        CombatUnitKind.Companion => Companions,
        CombatUnitKind.Construct => HardConstructs,
        CombatUnitKind.Trap => HardTraps,
        CombatUnitKind.Phantom => HardPhantoms,
        _ => 1,
    });
}

public sealed record CombatUnit(
    string StableId,
    CombatUnitKind Kind,
    string OwnerStableId,
    int MaximumLife,
    int CurrentLife,
    int SpawnTick,
    int ExpiresTick = -1,
    int TriggerRadiusRaw = 0)
{
    public bool Alive => CurrentLife > 0;
}

public sealed class CombatRoster
{
    public const int TrapDurationTicks = 160;
    public const int TrapTriggerRadiusRaw = 2_000;
    private readonly List<CombatUnit> _units = [];
    private int _sequence;

    public IReadOnlyList<CombatUnit> Units => _units;
    public bool AttachedMercenaryAllowed { get; init; }
    public bool HeroAlive => _units.Any(unit => unit.Kind == CombatUnitKind.Hero && unit.Alive);
    public bool MercenaryAlive => _units.Any(unit => unit.Kind == CombatUnitKind.Mercenary && unit.Alive);
    public bool PartyFailed => !HeroAlive && (!AttachedMercenaryAllowed || !MercenaryAlive);

    public void StartBattle(string heroId, int heroLife, string? mercenaryId = null, int mercenaryLife = 0)
    {
        _units.Clear();
        _sequence = 0;
        _units.Add(New(heroId, CombatUnitKind.Hero, heroId, heroLife, 0));
        if (AttachedMercenaryAllowed && !string.IsNullOrWhiteSpace(mercenaryId))
            _units.Add(New(mercenaryId, CombatUnitKind.Mercenary, heroId, mercenaryLife, 0));
    }

    public IReadOnlyList<CombatUnit> InstantiateArmy(string ownerId, CombatUnitKind kind, int requested,
        int maximumLife, int tick, int? configuredMaximum = null)
    {
        if (kind is CombatUnitKind.Hero or CombatUnitKind.Mercenary or CombatUnitKind.Trap)
            throw new ArgumentOutOfRangeException(nameof(kind));
        int limit = CombatCaps.Clamp(kind, configuredMaximum ?? DefaultMaximum(kind));
        int existing = _units.Count(unit => unit.Kind == kind && unit.OwnerStableId == ownerId && unit.Alive);
        int create = Math.Clamp(requested, 0, Math.Max(0, limit - existing));
        var result = new List<CombatUnit>(create);
        for (int index = 0; index < create; index++)
        {
            CombatUnit unit = New($"{ownerId}:{kind.ToString().ToLowerInvariant()}:{++_sequence}", kind,
                ownerId, maximumLife, tick, kind == CombatUnitKind.Phantom ? tick + 80 : -1);
            _units.Add(unit);
            result.Add(unit);
        }
        return result;
    }

    public CombatUnit PlaceTrap(string ownerId, int maximumLife, int tick, int configuredMaximum = CombatCaps.BaseTraps)
    {
        int limit = CombatCaps.Clamp(CombatUnitKind.Trap, configuredMaximum);
        CombatUnit[] traps = _units.Where(unit => unit.Kind == CombatUnitKind.Trap &&
            unit.OwnerStableId == ownerId && unit.Alive).OrderBy(unit => unit.SpawnTick).ThenBy(unit => unit.StableId).ToArray();
        if (traps.Length >= limit && traps.Length > 0) _units.Remove(traps[0]);
        CombatUnit trap = New($"{ownerId}:trap:{++_sequence}", CombatUnitKind.Trap, ownerId, maximumLife,
            tick, tick + TrapDurationTicks, TrapTriggerRadiusRaw);
        _units.Add(trap);
        return trap;
    }

    public bool Damage(string stableId, int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        int index = _units.FindIndex(unit => unit.StableId == stableId);
        if (index < 0) return false;
        CombatUnit unit = _units[index];
        _units[index] = unit with { CurrentLife = Math.Max(0, unit.CurrentLife - amount) };
        return true;
    }

    public int Expire(int tick)
    {
        int before = _units.Count;
        _units.RemoveAll(unit => unit.ExpiresTick >= 0 && unit.ExpiresTick <= tick);
        return before - _units.Count;
    }

    private static int DefaultMaximum(CombatUnitKind kind) => kind switch
    {
        CombatUnitKind.Minion => CombatCaps.BaseMinions,
        CombatUnitKind.Companion => CombatCaps.Companions,
        CombatUnitKind.Construct => CombatCaps.BaseConstructs,
        CombatUnitKind.Phantom => CombatCaps.HardPhantoms,
        _ => 1,
    };

    private static CombatUnit New(string id, CombatUnitKind kind, string owner, int life, int tick,
        int expires = -1, int triggerRadius = 0)
    {
        if (life <= 0) throw new ArgumentOutOfRangeException(nameof(life));
        return new CombatUnit(id, kind, owner, life, life, tick, expires, triggerRadius);
    }
}

public sealed record UnitAiProfile(UnitAiMode Mode, int PreferredDistanceRaw, int RetreatDistanceRaw,
    bool MoveWhileUsing, bool PrioritizeRareAndBoss);

public static class UnitAiRules
{
    public static UnitAiProfile ForMainSkill(SkillCombatDefinition skill, CombatUnitKind unitKind)
    {
        if (unitKind == CombatUnitKind.Construct)
            return new(UnitAiMode.BossPriority, 8_000, 2_000, false, true);
        if (unitKind == CombatUnitKind.Minion)
            return new(UnitAiMode.Bodyguard, 1_500, 0, false, true);
        if (ArchetypeSkillDefinitions.TryActiveForSkill(skill.SkillId, out ArchetypeSkillDefinition? archetypes) &&
            archetypes!.Mechanic is SkillMechanic.Minion or SkillMechanic.Companion or SkillMechanic.Construct)
            return new(UnitAiMode.Summoner, 9_000, 4_000, false, true);
        if (skill.Capabilities.HasFlag(SkillCapability.Projectile) && skill.Capabilities.HasFlag(SkillCapability.Attack))
            return new(UnitAiMode.Ranged, 8_000, 3_000, true, true);
        if (skill.Capabilities.HasFlag(SkillCapability.Spell))
            return new(UnitAiMode.Caster, 7_000, 2_500, false, true);
        return new(UnitAiMode.Melee, 1_500, 500, false, true);
    }
}

public sealed class ContributionReport
{
    private readonly Dictionary<CombatUnitKind, long> _damage = [];
    public IReadOnlyDictionary<CombatUnitKind, long> Damage => _damage;
    public void Add(CombatUnitKind source, int damage)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(damage);
        _damage[source] = checked(_damage.GetValueOrDefault(source) + damage);
    }
}
