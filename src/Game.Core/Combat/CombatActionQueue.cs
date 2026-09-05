using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.SkillCatalog;
using GameForWork.Core.Skills;
using GameForWork.Core.Spatial;
using GameForWork.Core.Ascendancies;

namespace GameForWork.Core.Combat;

public sealed record CombatHitSnapshot(string TargetId, Point Origin, ResolvedSkill Skill, SkillConfiguration Configuration,
    TeamBuild Build, DamagePacket OffensivePacket, IReadOnlyList<DamageBranch> AilmentSource, bool Critical,
    int AppliedCriticalMultiplier = 10_000);
public sealed record CombatActionSnapshot(string Id, string SkillId, SkillTag Tags, bool Unarmed,
    int StartedMilliseconds, int CompletesMilliseconds, IReadOnlyList<CombatHitSnapshot> Hits);
public sealed record DeferredCombatCopy(string Id, CombatActionSnapshot Action, int DueMilliseconds,
    int Multiplier, bool RollCritical, string Source, bool Sacrifice = false, int Radius = 0);

/// <summary>Only original actions enter the recorder. Copies are terminal queue entries.</summary>
public sealed partial class CombatActionQueue(CombatProfile? profile = null)
{
    private readonly CombatProfile _profile = profile ?? CombatProfile.Empty;
    private sealed class Phantom(string id, Point position, int expires, int sacrifice, int radius, int ratio)
    {
        public string Id { get; } = id;
        public Point Position { get; set; } = position;
        public int Expires { get; } = expires;
        public int Sacrifice { get; } = sacrifice;
        public int Radius { get; } = radius;
        public int Ratio { get; } = ratio;
        public CombatActionSnapshot? LastReplay { get; set; }
        public int LastMultiplier { get; set; }
    }
    private readonly List<Phantom> _phantoms = [];
    private readonly Dictionary<string, CombatActionSnapshot> _recording = [];
    private readonly List<DeferredCombatCopy> _pending = [];
    private readonly HashSet<string> _completed = [];
    private int _unarmedCount, _sequence;
    private int _substituteReady, _recoveryReady;
    private SkillTag _previousCategory;
    public CombatActionSnapshot? LatestAttack { get; private set; }
    public IReadOnlyList<DeferredCombatCopy> Pending => _pending;
    public void Begin(string id, ResolvedSkill skill, TeamBuild build, int tick, bool triggered)
    {
        SkillTag tags = SkillDefinitions.Get(skill.SkillId).Tags;
        if (triggered || _completed.Contains(id) || _recording.ContainsKey(id) ||
            (tags & (SkillTag.Attack | SkillTag.Spell)) == 0 || skill.Role is SkillRole.Reservation or SkillRole.DamageOverTime) return;
        _recording.Add(id, new(id, skill.SkillId, tags, !build.HasUsableWeapon, tick * 50,
            (tick + CombatSkillRules.ActionDelay(build, skill.CastTimeTicks, tags)) * 50, []));
    }
    public IReadOnlyList<AllyFrame> PhantomFrames(int tick) => _phantoms.Where(phantom => phantom.Expires > tick)
        .Select(phantom => new AllyFrame(phantom.Id, phantom.Position, false, "archetypes.skill.phantom_step")).ToArray();
    public void SpawnPhantom(Point origin, int tick, int duration, int maximum, int ratio,
        int sacrifice = 0, int radius = 3_000, ResourceState? hero = null, bool recovery = false,
        IReadOnlyList<CombatActionSnapshot>? memory = null, PhantomReplayMode mode = PhantomReplayMode.Sequential, int interval = 500)
    {
        ExpirePhantoms(tick, hero, recovery);
        if (maximum <= 0) return;
        while (_phantoms.Count >= Math.Min(6, maximum))
            RemovePhantom(_phantoms.MinBy(phantom => phantom.Expires)!, tick, hero, recovery, false);
        string id = $"phantom:{++_sequence}";
        _phantoms.Add(new(id, origin, tick + duration, sacrifice, radius, ratio));
        if (memory is not null) { ReplayMemory(_phantoms[^1], memory, mode, tick * 50, interval); return; }
        if (LatestAttack is { } attack) Enqueue(attack with { Hits = attack.Hits.Select(hit => hit with { Origin = origin }).ToArray() },
            tick * 50 + 300, ratio, false, id);
    }
    public void ExpirePhantoms(int tick, ResourceState? hero = null, bool recovery = false)
    {
        foreach (var phantom in _phantoms.Where(phantom => phantom.Expires <= tick).ToArray())
            RemovePhantom(phantom, tick, hero, recovery, false);
    }
    public bool TrySubstitute(Point heroPosition, int tick)
    {
        if (tick < _substituteReady) return false;
        var phantom = _phantoms.Where(phantom => phantom.Expires > tick)
            .MinBy(phantom => Point.DistanceSquared(heroPosition, phantom.Position));
        if (phantom is null) return false;
        RemovePhantom(phantom, tick, null, false, true);
        _substituteReady = tick + 60;
        return true;
    }
    private void RemovePhantom(Phantom phantom, int tick, ResourceState? hero, bool recovery, bool substitute)
    {
        _phantoms.Remove(phantom);
        _pending.RemoveAll(copy => copy.Source == phantom.Id);
        if (substitute) return;
        if (recovery && hero is not null && tick >= _recoveryReady)
        {
            hero.HealLife(hero.MaximumLife / 50); hero.RestoreShield(hero.MaximumShield / 50);
            _recoveryReady = tick + 10;
        }
        if (phantom.Sacrifice > 0 && phantom.LastReplay is { } replay)
            _pending.Add(new($"copy:{++_sequence}", replay, tick * 50,
                (int)((long)phantom.LastMultiplier * phantom.Sacrifice / 10_000), false,
                phantom.Id, true, phantom.Radius));
    }
    public void Replayed(DeferredCombatCopy copy, IReadOnlyList<CombatHitSnapshot> hits)
    {
        var phantom = _phantoms.FirstOrDefault(phantom => phantom.Id == copy.Source);
        if (phantom is null || hits.Count == 0) return;
        phantom.LastReplay = copy.Action with { Hits = hits };
        phantom.LastMultiplier = copy.Multiplier;
    }
    public void CommandPhantoms(int tick)
    {
        if (LatestAttack is not { } attack) return;
        foreach (var phantom in _phantoms.Where(phantom => phantom.Expires > tick).Take(6))
            Enqueue(attack with { Hits = attack.Hits.Select(hit => hit with { Origin = phantom.Position }).ToArray() }, tick * 50, phantom.Ratio, false, phantom.Id);
    }
    public void Record(string actionId, CombatHitSnapshot hit, int tick, bool triggered)
    {
        if (triggered || _completed.Contains(actionId)) return;
        SkillTag tags = SkillDefinitions.Get(hit.Skill.SkillId).Tags;
        if ((tags & (SkillTag.Attack | SkillTag.Spell)) == 0 || hit.Skill.Role is SkillRole.Reservation or SkillRole.DamageOverTime) return;
        Begin(actionId, hit.Skill, hit.Build, tick, triggered);
        if (!_recording.TryGetValue(actionId, out var action)) return;
        _recording[actionId] = action with { Hits = action.Hits.Append(hit).ToArray() };
    }
    public void CompleteReady(int milliseconds, IReadOnlySet<string> flyingActions, bool hundredReturn, bool alternatingCopy,
        Point? origin = null, ResourceState? hero = null)
    {
        foreach (var action in _recording.Values.Where(action => action.CompletesMilliseconds <= milliseconds && !flyingActions.Contains(action.Id)).ToArray())
        {
            _recording.Remove(action.Id);
            _completed.Add(action.Id);
            if (action.Tags.HasFlag(SkillTag.Attack)) LatestAttack = action;
            if (action.Tags.HasFlag(SkillTag.Spell) && action.Hits.FirstOrDefault()?.Configuration.Supports.HasFlag(SkillSupport.SpellEcho) == true)
                Enqueue(action, milliseconds + Math.Max(50, action.CompletesMilliseconds - action.StartedMilliseconds), 10_000, false, "support:spell-echo");
            if (hundredReturn && action.Unarmed && action.Tags.HasFlag(SkillTag.Attack) && ++_unarmedCount % 5 == 0)
                for (int repeat = 1; repeat <= 4; repeat++) Enqueue(action, milliseconds + repeat * 120, 3_500, true, "equipment:百式回身");
            SkillTag category = action.Tags.HasFlag(SkillTag.Attack) ? SkillTag.Attack : SkillTag.Spell;
            if (alternatingCopy && _previousCategory != 0 && _previousCategory != category)
                Enqueue(action, milliseconds + 250, 6_000, false, "equipment:攻法回文");
            _previousCategory = category;
            Remember(action, milliseconds, origin ?? action.Hits.FirstOrDefault()?.Origin ?? default, hero);
        }
    }
    public void Enqueue(CombatActionSnapshot action, int dueMilliseconds, int multiplier, bool rollCritical, string source) =>
        _pending.Add(new($"copy:{++_sequence}", action, dueMilliseconds, multiplier, rollCritical, source));
    public IReadOnlyList<DeferredCombatCopy> TakeDue(int milliseconds)
    {
        var due = _pending.Where(copy => copy.DueMilliseconds <= milliseconds).OrderBy(copy => copy.DueMilliseconds).ThenBy(copy => copy.Id, StringComparer.Ordinal).ToArray();
        _pending.RemoveAll(due.Contains);
        return due.Select(copy => _phantoms.FirstOrDefault(phantom => phantom.Id == copy.Source) is { } phantom ?
            copy with { Action = copy.Action with { Hits = copy.Action.Hits.Select(hit => hit with { Origin = phantom.Position }).ToArray() } } : copy).ToArray();
    }
}
