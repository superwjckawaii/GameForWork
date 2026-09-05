using GameForWork.Core.Ascendancies;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Spatial;

namespace GameForWork.Core.Combat;

public sealed partial class CombatActionQueue
{
    private readonly List<CombatActionSnapshot> _memory = [];
    private int _memoryUses, _swapReady, _unityReady;
    public int UntargetableUntil { get; private set; }
    public IReadOnlyList<CombatActionSnapshot> Memory => _memory.ToArray();
    private bool Node(string branch, string size = "small") => _profile.Has($"core.ascendancy.phantom_master.{branch}.{size}");
    private void Remember(CombatActionSnapshot action, int milliseconds, Point origin, ResourceState? hero)
    {
        if (_profile.Ascendancy != Ascendancy.PhantomMaster || action.Hits.Count == 0 ||
            (action.Tags & (SkillTag.Counter | SkillTag.Trigger | SkillTag.Reservation)) != 0 ||
            action.SkillId is "archetypes.skill.phantom_step" or "archetypes.skill.hundred_shadows") return;
        _memory.Add(action);
        int length = Node("afterimage") ? 4 : 3;
        if (_memory.Count > length) _memory.RemoveAt(0);
        int every = Node("spawn", "core") ? 3 : Node("spawn") ? 4 : 6;
        if (++_memoryUses % every != 0) return;
        int maximum = Node("spawn", "core") ? 4 : Node("spawn") ? 2 : 1;
        maximum += action.Hits[0].Build.CombatEquipment?.Value(Campaign.Items.ItemModifierKind.AdditionalPhantomMaximum) ?? 0;
        int ratio = Node("copy", "core") ? 5_000 : Node("copy") ? 3_000 : 2_000;
        var memories = _memory.Where(item => item.Tags.HasFlag(SkillTag.Attack) || Node("copy", "core")).ToArray();
        var mode = Node("afterimage", "core") ? _profile.Configuration?.PhantomMode ?? PhantomReplayMode.Sequential : PhantomReplayMode.Sequential;
        SpawnPhantom(origin, milliseconds / 50, Node("spawn", "core") ? 160 : 120, maximum, ratio,
            hero: hero, recovery: Node("sustain"), memory: memories, mode: mode, interval: Node("afterimage") ? 400 : 500);
    }
    private void ReplayMemory(Phantom phantom, IReadOnlyList<CombatActionSnapshot> memory, PhantomReplayMode mode, int milliseconds, int interval)
    {
        if (memory.Count == 0) return;
        IEnumerable<CombatActionSnapshot> sequence = mode switch
        {
            PhantomReplayMode.Focus => Enumerable.Repeat(memory[^1], 3),
            PhantomReplayMode.Reverse => memory.Reverse(),
            _ => memory,
        };
        int ratio = phantom.Ratio * (mode == PhantomReplayMode.Focus ? 6_000 : mode == PhantomReplayMode.Reverse ? 12_000 : 10_000) / 10_000;
        if (mode == PhantomReplayMode.Reverse) interval = interval * 3 / 2;
        var ready = new Dictionary<string, int>();
        foreach (var action in sequence)
        {
            int due = Math.Max(milliseconds, ready.GetValueOrDefault(action.SkillId));
            if (due >= phantom.Expires * 50) break;
            Enqueue(action with { Hits = action.Hits.Select(hit => hit with { Origin = phantom.Position }).ToArray() }, due, ratio, false, phantom.Id);
            ready[action.SkillId] = due + 1_000;
            milliseconds = due + interval;
        }
    }
    public Point? TrySwap(Point origin, int tick, int actualDamage, int maximumResources)
    {
        if (!Node("swap") || tick < _swapReady || actualDamage * 5L < maximumResources) return null;
        var phantom = _phantoms.Where(phantom => phantom.Expires > tick).MaxBy(phantom => Point.DistanceSquared(origin, phantom.Position));
        if (phantom is null) return null;
        Point destination = phantom.Position;
        phantom.Position = origin;
        _swapReady = tick + (Node("swap", "core") ? 40 : 100);
        if (Node("swap", "core")) UntargetableUntil = tick + 20;
        return destination;
    }
    public bool TryUnity(int tick)
    {
        var phantoms = _phantoms.Where(phantom => phantom.Expires > tick).ToArray();
        if (!Node("unity", "core") || tick < _unityReady || phantoms.Length < 4 || _memory.LastOrDefault() is not { } action) return false;
        _unityReady = tick + 160;
        foreach (var phantom in phantoms)
        {
            RemovePhantom(phantom, tick, null, false, true);
            Enqueue(action with { Hits = action.Hits.Select(hit => hit with { Origin = phantom.Position }).ToArray() }, tick * 50, 7_500, false, phantom.Id);
        }
        return true;
    }
    public TeamBuild ApplyPhantomBonuses(TeamBuild build, int tick)
    {
        if (!Node("unity")) return build;
        int count = Math.Min(4, _phantoms.Count(phantom => phantom.Expires > tick));
        return build with
        {
            IncreasedDamageBasisPoints = build.IncreasedDamageBasisPoints + count * 600,
            IncreasedSpellDamageBasisPoints = build.IncreasedSpellDamageBasisPoints + count * 600,
            MovementSpeedBasisPoints = build.MovementSpeedBasisPoints + count * 200
        };
    }
    public int PhantomTargetMultiplier(bool rareOrBoss) => rareOrBoss && Node("unity") ? 13_000 : 10_000;
}
