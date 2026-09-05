using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.SkillCatalog;
using GameForWork.Core.Skills;
using GameForWork.Core.Spatial;

namespace GameForWork.Core.Combat;

public sealed record CombatHitSnapshot(string TargetId, Point Origin, ResolvedSkill Skill, SkillConfiguration Configuration,
    TeamBuild Build, DamagePacket OffensivePacket, IReadOnlyList<DamageBranch> AilmentSource, bool Critical,
    int AppliedCriticalMultiplier = 10_000);
public sealed record CombatActionSnapshot(string Id, string SkillId, SkillTag Tags, bool Unarmed,
    int StartedMilliseconds, int CompletesMilliseconds, IReadOnlyList<CombatHitSnapshot> Hits);
public sealed record DeferredCombatCopy(string Id, CombatActionSnapshot Action, int DueMilliseconds,
    int Multiplier, bool RollCritical, string Source);

/// <summary>Only original actions enter the recorder. Copies are terminal queue entries.</summary>
public sealed class CombatActionQueue
{
    private readonly List<(string Id, Point Position, int Expires)> _phantoms = [];
    private readonly Dictionary<string, CombatActionSnapshot> _recording = [];
    private readonly List<DeferredCombatCopy> _pending = [];
    private readonly HashSet<string> _completed = [];
    private int _unarmedCount, _sequence;
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
    public void SpawnPhantom(Point origin, int tick, int duration, int maximum, int ratio)
    {
        _phantoms.RemoveAll(phantom => phantom.Expires <= tick);
        if (maximum <= 0) return;
        while (_phantoms.Count >= Math.Min(6, maximum)) _phantoms.RemoveAt(0);
        string id = $"phantom:{++_sequence}";
        _phantoms.Add((id, origin, tick + duration));
        if (LatestAttack is { } attack) Enqueue(attack with { Hits = attack.Hits.Select(hit => hit with { Origin = origin }).ToArray() },
            tick * 50 + 300, ratio, false, id);
    }
    public void CommandPhantoms(int tick, int ratio)
    {
        if (LatestAttack is not { } attack) return;
        foreach (var phantom in _phantoms.Where(phantom => phantom.Expires > tick).Take(6))
            Enqueue(attack with { Hits = attack.Hits.Select(hit => hit with { Origin = phantom.Position }).ToArray() }, tick * 50, ratio, false, phantom.Id);
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
    public void CompleteReady(int milliseconds, IReadOnlySet<string> flyingActions, bool hundredReturn, bool alternatingCopy)
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
        }
    }
    public void Enqueue(CombatActionSnapshot action, int dueMilliseconds, int multiplier, bool rollCritical, string source) =>
        _pending.Add(new($"copy:{++_sequence}", action, dueMilliseconds, multiplier, rollCritical, source));
    public IReadOnlyList<DeferredCombatCopy> TakeDue(int milliseconds)
    {
        var due = _pending.Where(copy => copy.DueMilliseconds <= milliseconds).OrderBy(copy => copy.DueMilliseconds).ThenBy(copy => copy.Id, StringComparer.Ordinal).ToArray();
        _pending.RemoveAll(due.Contains);
        return due;
    }
}
