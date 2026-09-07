using GameForWork.Core.SkillCatalog;
using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Progression;
using GameForWork.Core.Skills;

namespace GameForWork.Core.Combat;

public sealed record PendingAreaBurst(GameForWork.Core.Spatial.Point Origin, int BaseDamage, SkillDamageType Type, int Radius, string Detail);

public sealed record PendingReaction(string SkillId, string TargetId, int Multiplier = 10_000,
    ResolvedSkill? Resolved = null, int IncreasedDamage = 0, bool RecoverLife = false, bool PayCost = false, SkillConfiguration? Configuration = null);

/// <summary>Sources enqueue after their result is known; reactions never enqueue further reactions.</summary>
public sealed class ReactionState(PassiveModifiers? passives = null)
{
    private sealed record StoredReaction(PendingReaction Reaction, int CooldownTicks);
    private readonly PassiveModifiers _passives = passives ?? PassiveModifiers.Empty;
    public const string Mirror = "archetypes.skill.mirror_counter";
    public const string Answer = "archetypes.skill.answering_formula";
    public const string Overload = "archetypes.skill.spellarmor_overload";
    public const string ShieldBreak = "archetypes.skill.shieldbreak_counter";
    private readonly Dictionary<string, int> _ready = [];
    private readonly Dictionary<string, int> _actionMultipliers = [];
    private readonly Dictionary<string, int> _spellIncreases = [];
    private readonly Queue<PendingReaction> _pending = [];
    private readonly Dictionary<string, StoredReaction> _stored = [];
    private readonly HashSet<string> _countedAttackActions = [];
    private readonly Queue<PendingAreaBurst> _bursts = [];
    public void EnqueueBurst(PendingAreaBurst burst) => _bursts.Enqueue(burst);
    public IEnumerable<PendingAreaBurst> DrainBursts()
    {
        while (_bursts.TryDequeue(out var burst)) yield return burst;
    }
    private readonly Dictionary<string, long> _damageTaken = [];
    private int _boost, _boostExpires;
    private string _channelSkill = "";
    private int _channelTick = -100, _channelMultiplier, _channelIncrease;
    private int _tick;
    private int _selfAttackHits;
    public int Tick
    {
        get => _tick;
        set
        {
            _tick = value;
            foreach ((string id, StoredReaction stored) in _stored.Where(pair => value >= _ready.GetValueOrDefault(pair.Key)).ToArray())
            {
                _stored.Remove(id);
                _ready[id] = value + stored.CooldownTicks;
                _pending.Enqueue(stored.Reaction);
            }
        }
    }
    public string? LastSelfSpellId { get; private set; }
    public bool Arm(SkillConfiguration configuration, GuardState guard)
    {
        int energy = guard.ConsumeEnergy();
        if (energy == 0) return false;
        _boost = energy * (600 + Math.Clamp(configuration.Quality, 0, 20) * 5);
        _boostExpires = Tick + 80;
        return true;
    }
    public void Begin(string actionId, string skillId, GuardState? guard = null, int paidSpellMultiplier = 10_000, int paymentMultiplier = 10_000)
    {
        SkillTag tags = SkillDefinitions.Get(skillId).Tags;
        if (tags.HasFlag(SkillTag.Spell) && (tags & (SkillTag.Trigger | SkillTag.Counter | SkillTag.Reservation | SkillTag.Channelling)) == 0 &&
            ActiveSkillCatalog.ActiveForSkill(skillId).Curve != SkillCurve.Unit) LastSelfSpellId = skillId;
        bool channel = SkillDefinitions.Get(skillId).Tags.HasFlag(SkillTag.Channelling);
        if (channel && _channelSkill == skillId && Tick - _channelTick <= 5)
        {
            _channelTick = Tick; _actionMultipliers[actionId] = CombatRules.ApplyMore(_channelMultiplier, [paidSpellMultiplier, paymentMultiplier]);
            _spellIncreases[actionId] = _channelIncrease; return;
        }
        if (SkillDefinitions.Get(skillId).Tags.HasFlag(SkillTag.Spell) && guard is not null)
        {
            _spellIncreases[actionId] = guard.SpellDamageIncrease;
            _actionMultipliers[actionId] = guard.ConsumeSpellEnergy();
        }
        _channelSkill = channel ? skillId : "";
        if (channel) { _channelTick = Tick; _channelMultiplier = ActionMultiplier(actionId); _channelIncrease = SpellIncrease(actionId); }
        if (SkillDefinitions.Get(skillId).Tags.HasFlag(SkillTag.Spell))
            _actionMultipliers[actionId] = (int)((long)ActionMultiplier(actionId) * paidSpellMultiplier / 10_000);
        _actionMultipliers[actionId] = (int)((long)ActionMultiplier(actionId) * paymentMultiplier / 10_000);
        if (skillId == Overload || _boost == 0 || !SkillDefinitions.Get(skillId).Tags.HasFlag(SkillTag.Attack)) return;
        if (Tick < _boostExpires)
        {
            _actionMultipliers[actionId] = (int)((long)ActionMultiplier(actionId) * (10_000 + _boost) / 10_000);
            _pending.Enqueue(new(Overload, ""));
        }
        _boost = 0;
    }
    public int ActionMultiplier(string actionId) => _actionMultipliers.GetValueOrDefault(actionId, 10_000);
    public int SpellIncrease(string actionId) => _spellIncreases.GetValueOrDefault(actionId);
    public void Enqueue(PendingReaction reaction) => _pending.Enqueue(reaction);
    public bool ThirdAttack(string action)
    {
        if (action.Length == 0 || !_countedAttackActions.Add(action)) return false;
        return ++_selfAttackHits % 3 == 0;
    }
    public bool Schedule(SkillConfiguration configuration, string target, int cooldownTicks, int multiplier = 10_000, bool payCost = false)
    {
        int effectiveCooldown = Math.Max(5, (int)Math.Ceiling(cooldownTicks * 10_000d /
            (10_000 + (payCost ? 0 : Math.Clamp(configuration.Quality, 0, 20) * 100))));
        if (Tick < _ready.GetValueOrDefault(configuration.SkillId))
        {
            if (!MasteryRuntime.Has(_passives, "触发_冷却", 1)) return false;
            _stored[configuration.SkillId] = new(new(configuration.SkillId, target, multiplier, PayCost: payCost,
                Configuration: configuration), effectiveCooldown);
            return true;
        }
        _ready[configuration.SkillId] = Tick + effectiveCooldown;
        _pending.Enqueue(new(configuration.SkillId, target, multiplier, PayCost: payCost, Configuration: configuration));
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
