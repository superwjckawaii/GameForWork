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
    int AppliedCriticalMultiplier = 10_000, int OffsetMilliseconds = 0);
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
        public Queue<DeferredCombatCopy> MemoryReplays { get; } = [];
        public int ReplayInterval { get; set; }
        public int LastReplayTick { get; set; }
        public long ReplayWork { get; set; }
        public Dictionary<string, int> SkillReady { get; } = [];
    }
    private readonly List<Phantom> _phantoms = [];
    private readonly Dictionary<string, CombatActionSnapshot> _recording = [];
    private readonly List<DeferredCombatCopy> _pending = [];
    private readonly HashSet<string> _completed = [];
    private int _unarmedCount, _sequence;
    private int _substituteReady, _recoveryReady;
    private SkillTag _previousCategory;
    private string? _channelId;
    private int _channelLastTick;
    private readonly Dictionary<string, string> _channelActions = [];
    public CombatActionSnapshot? LatestAttack { get; private set; }
    public IReadOnlyList<DeferredCombatCopy> Pending => _pending.Concat(_phantoms.SelectMany(phantom => phantom.MemoryReplays)).ToArray();
    public void Begin(string id, ResolvedSkill skill, TeamBuild build, int tick, bool triggered)
    {
        SkillTag tags = SkillDefinitions.Get(skill.SkillId).Tags;
        if (!triggered && tags.HasFlag(SkillTag.Channelling))
        {
            if (_channelId is not null && _recording.TryGetValue(_channelId, out var channel) &&
                channel.SkillId == skill.SkillId && tick - _channelLastTick <= 5)
            {
                _channelLastTick = tick;
                _channelActions[id] = _channelId;
                _recording[_channelId] = channel with { CompletesMilliseconds = (tick + 6) * 50 };
                return;
            }
            _channelId = id;
            _channelLastTick = tick;
            _channelActions[id] = id;
        }
        else if (!triggered) _channelId = null;
        if (triggered || _completed.Contains(id) || _recording.ContainsKey(id) ||
            (tags & (SkillTag.Attack | SkillTag.Spell)) == 0 || skill.Role is SkillRole.Reservation or SkillRole.DamageOverTime) return;
        _recording.Add(id, new(id, skill.SkillId, tags, !build.HasUsableWeapon, tick * 50,
            (tick + (tags.HasFlag(SkillTag.Channelling) ? 6 : CombatSkillRules.ActionDelay(build, skill.CastTimeTicks, tags))) * 50, []));
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
        actionId = _channelActions.GetValueOrDefault(actionId, actionId);
        if (!_recording.TryGetValue(actionId, out var action)) return;
        if (tags.HasFlag(SkillTag.Channelling)) hit = hit with { OffsetMilliseconds = tick * 50 - action.StartedMilliseconds };
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
            if (action.Tags.HasFlag(SkillTag.Spell) && !action.Tags.HasFlag(SkillTag.Channelling) && action.Hits.FirstOrDefault()?.Configuration.Supports.HasFlag(SkillSupport.SpellEcho) == true)
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
        _pending.AddRange(Segments(new($"copy:{++_sequence}", action, dueMilliseconds, multiplier, rollCritical, source)));
    private static IEnumerable<DeferredCombatCopy> Segments(DeferredCombatCopy copy)
    {
        if (!copy.Action.Tags.HasFlag(SkillTag.Channelling)) { yield return copy; yield break; }
        foreach (var group in copy.Action.Hits.GroupBy(hit => hit.OffsetMilliseconds))
            yield return copy with
            {
                Id = $"{copy.Id}:pulse:{group.Key}",
                DueMilliseconds = copy.DueMilliseconds + group.Key,
                Action = copy.Action with { Hits = group.ToArray() }
            };
    }
    public IReadOnlyList<DeferredCombatCopy> TakeDue(int milliseconds, Func<Point, int>? actionSpeed = null)
    {
        var due = _pending.Where(copy => copy.DueMilliseconds <= milliseconds).ToList();
        _pending.RemoveAll(due.Contains);
        foreach (var phantom in _phantoms)
        {
            int speed = Math.Max(1, 10_000 + (actionSpeed?.Invoke(phantom.Position) ?? 0));
            while (phantom.LastReplayTick <= milliseconds && phantom.MemoryReplays.TryPeek(out var next))
            {
                int now = phantom.LastReplayTick;
                if (now >= phantom.Expires * 50) break;
                if (phantom.ReplayWork >= phantom.ReplayInterval * 10_000L && now >= phantom.SkillReady.GetValueOrDefault(next.Action.SkillId))
                {
                    phantom.MemoryReplays.Dequeue();
                    foreach (var segment in Segments(next with { DueMilliseconds = now }))
                    {
                        if (segment.DueMilliseconds <= milliseconds) due.Add(segment);
                        else _pending.Add(segment);
                    }
                    phantom.ReplayWork = 0;
                    phantom.SkillReady[next.Action.SkillId] = now + 1_000;
                }
                phantom.LastReplayTick += 50;
                phantom.ReplayWork += 50L * speed;
            }
        }
        return due.OrderBy(copy => copy.DueMilliseconds).ThenBy(copy => copy.Id, StringComparer.Ordinal)
            .Select(copy => _phantoms.FirstOrDefault(phantom => phantom.Id == copy.Source) is { } phantom ?
            copy with { Action = copy.Action with { Hits = copy.Action.Hits.Select(hit => hit with { Origin = phantom.Position }).ToArray() } } : copy).ToArray();
    }
}
