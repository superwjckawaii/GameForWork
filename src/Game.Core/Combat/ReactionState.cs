using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Skills;

namespace GameForWork.Core.Combat;

public sealed record PendingReaction(string SkillId, string TargetId, int Multiplier = 10_000,
    ResolvedSkill? Resolved = null, int IncreasedDamage = 0, bool RecoverLife = false, bool PayCost = false);

/// <summary>Sources enqueue after their result is known; reactions never enqueue further reactions.</summary>
public sealed class ReactionState
{
    public const string Mirror = "archetypes.skill.mirror_counter";
    public const string Answer = "archetypes.skill.answering_formula";
    public const string Overload = "archetypes.skill.spellarmor_overload";
    public const string ShieldBreak = "archetypes.skill.shieldbreak_counter";
    private readonly Dictionary<string, int> _ready = [];
    private readonly Dictionary<string, int> _attackMultipliers = [];
    private readonly Queue<PendingReaction> _pending = [];
    private readonly Dictionary<string, long> _damageTaken = [];
    private int _boost, _boostExpires;
    public int Tick { get; set; }
    public bool Arm(SkillConfiguration configuration, GuardState guard)
    {
        int energy = guard.ConsumeEnergy();
        if (energy == 0) return false;
        _boost = energy * (600 + Math.Clamp(configuration.Quality, 0, 20) * 5);
        _boostExpires = Tick + 80;
        return true;
    }
    public void Begin(string actionId, string skillId)
    {
        if (skillId == Overload || _boost == 0 || !SkillDefinitions.Get(skillId).Tags.HasFlag(SkillTag.Attack)) return;
        if (Tick < _boostExpires)
        {
            _attackMultipliers[actionId] = 10_000 + _boost;
            _pending.Enqueue(new(Overload, ""));
        }
        _boost = 0;
    }
    public int AttackMultiplier(string actionId) => _attackMultipliers.GetValueOrDefault(actionId, 10_000);
    public void Enqueue(PendingReaction reaction) => _pending.Enqueue(reaction);
    public bool Schedule(SkillConfiguration configuration, string target, int cooldownTicks, int multiplier = 10_000, bool payCost = false)
    {
        if (Tick < _ready.GetValueOrDefault(configuration.SkillId)) return false;
        _ready[configuration.SkillId] = Tick + Math.Max(5, (int)Math.Ceiling(cooldownTicks * 10_000d /
            (10_000 + (payCost ? 0 : Math.Clamp(configuration.Quality, 0, 20) * 100))));
        _pending.Enqueue(new(configuration.SkillId, target, multiplier, PayCost: payCost));
        return true;
    }
    public bool AccumulateDamage(string id, int damage, int threshold)
    {
        long total = _damageTaken.GetValueOrDefault(id) + damage;
        bool ready = total >= Math.Max(1, threshold);
        _damageTaken[id] = ready ? 0 : total;
        return ready;
    }
    public IReadOnlyList<PendingReaction> Drain()
    {
        var pending = _pending.ToArray(); _pending.Clear(); return pending;
    }
}
