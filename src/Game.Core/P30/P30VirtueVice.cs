namespace GameForWork.Core.P30;

public enum P30VirtueViceKind { Mercy, Temperance, Humility, Rage, Sloth, Arrogance }

public sealed record P30VirtueViceBonuses(
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

public sealed record P30VirtueViceSnapshot(
    IReadOnlyDictionary<P30VirtueViceKind, int> Layers,
    IReadOnlyDictionary<P30VirtueViceKind, int> RemainingMilliseconds,
    IReadOnlySet<P30VirtueViceKind> Permanent);

public sealed record P30VirtueViceLoadout(IReadOnlyDictionary<P30VirtueViceKind, int> AdditionalMaximum,
    IReadOnlyList<P30VirtueViceKind> HeldAtMaximum, IReadOnlyList<P30VirtueViceKind> Oaths)
{
    public static P30VirtueViceLoadout Empty { get; } = new(new Dictionary<P30VirtueViceKind, int>(),
        [], []);
}

/// <summary>Authoritative, deterministic P30 virtue/vice combat state.</summary>
public sealed class P30VirtueViceState
{
    public const int BaseMaximumLayers = 2;
    public const int AuditedMaximumLayers = 10;
    public const int BaseDurationMilliseconds = 12_000;
    private readonly Dictionary<P30VirtueViceKind, int> _layers = [];
    private readonly Dictionary<P30VirtueViceKind, int> _remaining = [];
    private readonly HashSet<P30VirtueViceKind> _permanent = [];
    private readonly Dictionary<P30VirtueViceKind, int> _maximum = [];
    private readonly HashSet<string> _resolvedOathActions = new(StringComparer.Ordinal);
    private int _slothHitProgress;

    public P30VirtueViceState(IReadOnlyDictionary<P30VirtueViceKind, int>? additionalMaximum = null,
        IEnumerable<P30VirtueViceKind>? heldAtMaximum = null)
    {
        foreach (P30VirtueViceKind kind in Enum.GetValues<P30VirtueViceKind>())
            _maximum[kind] = Math.Clamp(BaseMaximumLayers +
                (additionalMaximum?.GetValueOrDefault(kind) ?? 0), 0, AuditedMaximumLayers);
        foreach (P30VirtueViceKind kind in heldAtMaximum ?? [])
        {
            _permanent.Add(kind);
            _layers[kind] = Maximum(kind);
        }
    }

    public int Layers(P30VirtueViceKind kind) => _layers.GetValueOrDefault(kind);
    public int Maximum(P30VirtueViceKind kind) => _maximum.GetValueOrDefault(kind, BaseMaximumLayers);
    public bool IsPermanent(P30VirtueViceKind kind) => _permanent.Contains(kind);
    public bool HasAnyVirtue => Layers(P30VirtueViceKind.Mercy) + Layers(P30VirtueViceKind.Temperance) +
        Layers(P30VirtueViceKind.Humility) > 0;
    public bool HasAnyVice => Layers(P30VirtueViceKind.Rage) + Layers(P30VirtueViceKind.Sloth) +
        Layers(P30VirtueViceKind.Arrogance) > 0;

    public bool Gain(P30VirtueViceKind kind, int amount = 1, int increasedDurationBasisPoints = 0)
    {
        if (amount <= 0) return false;
        int before = Layers(kind);
        _layers[kind] = Math.Min(Maximum(kind), before + amount);
        if (!_permanent.Contains(kind))
            _remaining[kind] = P30CombatRules.ApplyIncreased(BaseDurationMilliseconds, increasedDurationBasisPoints);
        return _layers[kind] > before;
    }

    public void Advance(int milliseconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(milliseconds);
        foreach (P30VirtueViceKind kind in _remaining.Keys.ToArray())
        {
            if (_permanent.Contains(kind)) continue;
            int next = _remaining[kind] - milliseconds;
            if (next > 0) _remaining[kind] = next;
            else { _remaining.Remove(kind); _layers.Remove(kind); }
        }
        foreach (P30VirtueViceKind kind in _permanent) _layers[kind] = Maximum(kind);
    }

    public int Consume(P30VirtueViceKind kind, int amount)
    {
        if (amount <= 0 || _permanent.Contains(kind)) return 0;
        int consumed = Math.Min(Layers(kind), amount);
        _layers[kind] = Layers(kind) - consumed;
        if (_layers[kind] == 0) _remaining.Remove(kind);
        return consumed;
    }

    public bool TryOathChance(P30VirtueViceKind kind, string actionId, int chanceBasisPoints, uint roll)
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
        return Gain(P30VirtueViceKind.Sloth);
    }

    public void ClearEncounter()
    {
        _layers.Clear(); _remaining.Clear();
        _resolvedOathActions.Clear(); _slothHitProgress = 0;
        foreach (P30VirtueViceKind kind in _permanent) _layers[kind] = Maximum(kind);
    }

    public P30VirtueViceBonuses Bonuses()
    {
        int mercy = Layers(P30VirtueViceKind.Mercy), temperance = Layers(P30VirtueViceKind.Temperance);
        int humility = Layers(P30VirtueViceKind.Humility), rage = Layers(P30VirtueViceKind.Rage);
        int sloth = Layers(P30VirtueViceKind.Sloth), arrogance = Layers(P30VirtueViceKind.Arrogance);
        return new(mercy * 1_500, mercy * 1_500, humility * 1_500, humility * 1_500,
            humility * 1_500, humility * 1_500, temperance * 1_000, temperance * 800,
            temperance / 2, rage * 1_200, sloth, sloth, arrogance * 4_000, arrogance * 1_200,
            Math.Max(0, P30CombatRules.Basis - mercy * 700),
            Math.Max(0, P30CombatRules.Basis - humility * 800), P30CombatRules.Basis + mercy * 1_000);
    }

    public P30VirtueViceSnapshot Capture() => new(new Dictionary<P30VirtueViceKind, int>(_layers),
        new Dictionary<P30VirtueViceKind, int>(_remaining), _permanent.ToHashSet());
}

public static class P30VirtueViceSources
{
    public static P30VirtueViceKind? Ascendancy(P18.P18Ascendancy ascendancy) => ascendancy switch
    {
        P18.P18Ascendancy.BloodFighter => P30VirtueViceKind.Rage,
        P18.P18Ascendancy.Marksman => P30VirtueViceKind.Arrogance,
        P18.P18Ascendancy.SoulShepherd => P30VirtueViceKind.Sloth,
        P18.P18Ascendancy.Elementalist => P30VirtueViceKind.Temperance,
        P18.P18Ascendancy.MartialMonk => P30VirtueViceKind.Mercy,
        P18.P18Ascendancy.Runecarver => P30VirtueViceKind.Humility,
        _ => null,
    };
}
