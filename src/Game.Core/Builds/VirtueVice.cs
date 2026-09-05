namespace GameForWork.Core.Builds;

public enum VirtueViceKind { Mercy, Temperance, Humility, Rage, Sloth, Arrogance }

public sealed record VirtueViceBonuses(
    int IncreasedMaximumLifeBasisPoints,
    int IncreasedRecoveryRateBasisPoints,
    int IncreasedArmorBasisPoints,
    int IncreasedEvasionBasisPoints,
    int IncreasedMaximumShieldBasisPoints,
    int IncreasedSpiritBarrierBasisPoints,
    int ReducedSkillCostBasisPoints,
    int ReducedFlaskChargeCostBasisPoints,
    int EffectiveGemLevels,
    int IncreasedActionSpeedBasisPoints,
    int AdditionalProjectiles,
    int AdditionalMinions,
    int IncreasedCriticalChanceBasisPoints,
    int MoreCriticalDamageBasisPoints,
    int PhysicalVoidDamageTakenMultiplierBasisPoints,
    int ElementalDamageTakenMultiplierBasisPoints,
    int CharacterScaleBasisPoints);

public sealed record VirtueViceSnapshot(
    IReadOnlyDictionary<VirtueViceKind, int> Layers,
    IReadOnlyDictionary<VirtueViceKind, int> RemainingMilliseconds,
    IReadOnlySet<VirtueViceKind> Permanent);

public sealed record VirtueViceLoadout(IReadOnlyDictionary<VirtueViceKind, int> AdditionalMaximum,
    IReadOnlyList<VirtueViceKind> HeldAtMaximum, IReadOnlyList<VirtueViceKind> Oaths)
{
    public static VirtueViceLoadout Empty { get; } = new(new Dictionary<VirtueViceKind, int>(),
        [], []);
}

/// <summary>Authoritative, deterministic Builds virtue/vice combat state.</summary>
public sealed class VirtueViceState
{
    public const int BaseMaximumLayers = 2;
    public const int AuditedMaximumLayers = 10;
    public const int BaseDurationMilliseconds = 12_000;
    private readonly Dictionary<VirtueViceKind, int> _layers = [];
    private readonly Dictionary<VirtueViceKind, int> _remaining = [];
    private readonly HashSet<VirtueViceKind> _permanent = [];
    private readonly Dictionary<VirtueViceKind, int> _maximum = [];
    private readonly HashSet<string> _resolvedOathActions = new(StringComparer.Ordinal);
    private int _slothHitProgress;

    public VirtueViceState(IReadOnlyDictionary<VirtueViceKind, int>? additionalMaximum = null,
        IEnumerable<VirtueViceKind>? heldAtMaximum = null)
    {
        foreach (VirtueViceKind kind in Enum.GetValues<VirtueViceKind>())
            _maximum[kind] = Math.Clamp(BaseMaximumLayers +
                (additionalMaximum?.GetValueOrDefault(kind) ?? 0), 0, AuditedMaximumLayers);
        foreach (VirtueViceKind kind in heldAtMaximum ?? [])
        {
            _permanent.Add(kind);
            _layers[kind] = Maximum(kind);
        }
    }

    public int Layers(VirtueViceKind kind) => _layers.GetValueOrDefault(kind);
    public int Maximum(VirtueViceKind kind) => _maximum.GetValueOrDefault(kind, BaseMaximumLayers);
    public bool IsPermanent(VirtueViceKind kind) => _permanent.Contains(kind);
    public bool HasAnyVirtue => Layers(VirtueViceKind.Mercy) + Layers(VirtueViceKind.Temperance) +
        Layers(VirtueViceKind.Humility) > 0;
    public bool HasAnyVice => Layers(VirtueViceKind.Rage) + Layers(VirtueViceKind.Sloth) +
        Layers(VirtueViceKind.Arrogance) > 0;

    public bool Gain(VirtueViceKind kind, int amount = 1, int increasedDurationBasisPoints = 0)
    {
        if (amount <= 0) return false;
        int before = Layers(kind);
        _layers[kind] = Math.Min(Maximum(kind), before + amount);
        if (!_permanent.Contains(kind))
            _remaining[kind] = CombatRules.ApplyIncreased(BaseDurationMilliseconds, increasedDurationBasisPoints);
        return _layers[kind] > before;
    }

    public void Advance(int milliseconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(milliseconds);
        foreach (VirtueViceKind kind in _remaining.Keys.ToArray())
        {
            if (_permanent.Contains(kind)) continue;
            int next = _remaining[kind] - milliseconds;
            if (next > 0) _remaining[kind] = next;
            else { _remaining.Remove(kind); _layers.Remove(kind); }
        }
        foreach (VirtueViceKind kind in _permanent) _layers[kind] = Maximum(kind);
    }

    public int Consume(VirtueViceKind kind, int amount)
    {
        if (amount <= 0 || _permanent.Contains(kind)) return 0;
        int consumed = Math.Min(Layers(kind), amount);
        _layers[kind] = Layers(kind) - consumed;
        if (_layers[kind] == 0) _remaining.Remove(kind);
        return consumed;
    }

    public bool TryOathChance(VirtueViceKind kind, string actionId, int chanceBasisPoints, uint roll)
    {
        if (!_resolvedOathActions.Add($"{kind}:{actionId}")) return false;
        return roll % 10_000 < Math.Clamp(chanceBasisPoints, 0, 10_000) && Gain(kind);
    }

    public bool RecordSlothOathHit(string actionId)
    {
        if (!_resolvedOathActions.Add($"sloth:{actionId}")) return false;
        _slothHitProgress++;
        if (_slothHitProgress < 8) return false;
        _slothHitProgress = 0;
        return Gain(VirtueViceKind.Sloth);
    }

    public void ClearEncounter()
    {
        _layers.Clear(); _remaining.Clear();
        _resolvedOathActions.Clear(); _slothHitProgress = 0;
        foreach (VirtueViceKind kind in _permanent) _layers[kind] = Maximum(kind);
    }

    public VirtueViceBonuses Bonuses()
    {
        int mercy = Layers(VirtueViceKind.Mercy), temperance = Layers(VirtueViceKind.Temperance);
        int humility = Layers(VirtueViceKind.Humility), rage = Layers(VirtueViceKind.Rage);
        int sloth = Layers(VirtueViceKind.Sloth), arrogance = Layers(VirtueViceKind.Arrogance);
        return new(mercy * 1_500, mercy * 1_500, humility * 1_500, humility * 1_500,
            humility * 1_500, humility * 1_500, temperance * 1_000, temperance * 800,
            temperance / 2, rage * 1_200, sloth, sloth, arrogance * 4_000, arrogance * 1_200,
            Math.Max(0, CombatRules.Basis - mercy * 700),
            Math.Max(0, CombatRules.Basis - humility * 800), CombatRules.Basis + mercy * 1_000);
    }

    public VirtueViceSnapshot Capture() => new(new Dictionary<VirtueViceKind, int>(_layers),
        new Dictionary<VirtueViceKind, int>(_remaining), _permanent.ToHashSet());
}

public static class VirtueViceSources
{
    public static VirtueViceKind? Ascendancy(GameForWork.Core.Ascendancies.Ascendancy ascendancy) => ascendancy switch
    {
        GameForWork.Core.Ascendancies.Ascendancy.BloodFighter => VirtueViceKind.Rage,
        GameForWork.Core.Ascendancies.Ascendancy.Marksman => VirtueViceKind.Arrogance,
        GameForWork.Core.Ascendancies.Ascendancy.SoulShepherd => VirtueViceKind.Sloth,
        GameForWork.Core.Ascendancies.Ascendancy.Elementalist => VirtueViceKind.Temperance,
        GameForWork.Core.Ascendancies.Ascendancy.MartialMonk => VirtueViceKind.Mercy,
        GameForWork.Core.Ascendancies.Ascendancy.Runecarver => VirtueViceKind.Humility,
        _ => null,
    };
}
