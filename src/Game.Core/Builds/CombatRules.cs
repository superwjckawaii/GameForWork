using GameForWork.Core.SkillCatalog;

namespace GameForWork.Core.Builds;

public enum DamageType { Physical, Fire, Cold, Lightning, Void }

public enum CombatRarity { Normal, Magic, Rare, MapBoss, PinnacleBoss }

public readonly record struct Conversion(DamageType Source, DamageType Target, int BasisPoints,
    string StableId);

public readonly record struct ExtraDamage(DamageType Source, DamageType Target, int BasisPoints,
    string StableId);

public sealed record DamageModifiers(
    IReadOnlyDictionary<DamageType, int>? IncreasedByType = null,
    int InitialIncreasedBasisPoints = 0,
    int ElementalIncreasedBasisPoints = 0,
    IReadOnlyDictionary<string, int>? MoreByStableId = null);

public sealed record DamageBranch(
    int BaseDamage,
    DamageType CurrentType,
    IReadOnlyList<DamageType> History,
    IReadOnlyList<string> Trace);

public sealed record DamagePacket(
    int Physical,
    int Fire,
    int Cold,
    int Lightning,
    int Void,
    IReadOnlyList<DamageBranch> Branches,
    IReadOnlyList<string> Trace)
{
    public int Total => (int)Math.Clamp(
        (long)Physical + Fire + Cold + Lightning + Void, 0, int.MaxValue);
}

public readonly record struct ResistanceProfile(
    int Physical,
    int Fire,
    int Cold,
    int Lightning,
    int Void,
    int PhysicalMaximum = 3_500,
    int ElementalMaximum = 7_500,
    int VoidMaximum = 7_500)
{
    public int For(DamageType type) => type switch
    {
        DamageType.Physical => Physical,
        DamageType.Fire => Fire,
        DamageType.Cold => Cold,
        DamageType.Lightning => Lightning,
        _ => Void,
    };

    public int MaximumFor(DamageType type) => type == DamageType.Physical
        ? Math.Min(PhysicalMaximum, CombatRules.AbsolutePhysicalResistanceMaximum)
        : Math.Min(type == DamageType.Void ? VoidMaximum : ElementalMaximum,
            CombatRules.AbsoluteElementalResistanceMaximum);
}

public readonly record struct AilmentResult(int EffectBasisPoints, int DurationMilliseconds,
    int AccumulationBasisPoints = 0);

public static class CombatRules
{
    public const int Basis = 10_000;
    public const int MinimumResistance = -50_000;
    public const int AbsolutePhysicalResistanceMaximum = 5_000;
    public const int AbsoluteElementalResistanceMaximum = 9_000;
    public const int DefaultBlockMaximum = 7_500;
    public const int AbsoluteBlockMaximum = 9_000;
    public const int DefaultSuppressionEffect = 7_000;
    public const int AbsoluteSuppressionEffectMaximum = 9_000;
    public const int SpiritBarrierReductionMaximum = 8_000;
    public const int DefaultLeechPerSecondMaximum = 3_500;
    public const int MaximumAttackFrequencyMilliPerSecond = 60_000;
    public const int AuthoritativeSimulationTicksPerSecond = 20;

    private static readonly DamageType[] ConversionOrder =
        [DamageType.Physical, DamageType.Lightning, DamageType.Cold, DamageType.Fire];

    public static int ApplyIncreased(int value, params int[] increasedBasisPoints) =>
        SaturatingScale(value, (long)Basis + increasedBasisPoints.Sum());

    public static int ApplyMore(int value, IEnumerable<int> multipliersBasisPoints)
    {
        int result = value;
        foreach (int multiplier in multipliersBasisPoints)
            result = SaturatingScale(result, multiplier);
        return result;
    }

    /// <summary>Combines two independently-worded more/less modifiers stored as deltas from 100%.</summary>
    public static int CombineMoreBasisPoints(int accumulatedMoreBasisPoints, int additionalMoreBasisPoints)
    {
        long left = Math.Max(0L, (long)Basis + accumulatedMoreBasisPoints);
        long right = Math.Max(0L, (long)Basis + additionalMoreBasisPoints);
        if (left == 0 || right == 0) return -Basis;
        if (left > long.MaxValue / right) return int.MaxValue;
        long multiplier = left * right / Basis;
        return (int)Math.Clamp(multiplier - Basis, int.MinValue, int.MaxValue);
    }

    public static int MaximumLife(int level, int physique, int flatLife, int increasedBasisPoints,
        IEnumerable<int>? moreMultipliers = null) => ApplyMore(
        ApplyIncreased(checked(80 + 8 * level + physique + flatLife), increasedBasisPoints),
        moreMultipliers ?? []);

    public static int MaximumMana(int level, int spirit, int flatMana, int increasedBasisPoints,
        IEnumerable<int>? moreMultipliers = null) => ApplyMore(
        ApplyIncreased(checked(40 + 2 * level + 2 * spirit + flatMana), increasedBasisPoints),
        moreMultipliers ?? []);

    public static int MaximumShield(int equipmentShield, int energy, int flatShield, int increasedBasisPoints,
        IEnumerable<int>? moreMultipliers = null) => ApplyMore(
        ApplyIncreased(checked(equipmentShield + 2 * energy + flatShield), increasedBasisPoints),
        moreMultipliers ?? []);

    public static int NaturalSpiritBarrier(int level, int spirit) => checked(2 * level + 4 * spirit);

    public static int SpiritBarrier(int level, int spirit, int equipmentBarrier, int flatBarrier,
        int increasedBasisPoints, IEnumerable<int>? moreMultipliers = null) => ApplyMore(
        ApplyIncreased(checked(NaturalSpiritBarrier(level, spirit) + equipmentBarrier + flatBarrier),
            increasedBasisPoints), moreMultipliers ?? []);

    public static int SpiritBarrierReduction(int barrier, int preBarrierDamagePerSecond)
    {
        if (barrier <= 0 || preBarrierDamagePerSecond <= 0) return 0;
        long denominator = checked((long)barrier * 2 + (long)preBarrierDamagePerSecond * 3);
        int raw = checked((int)((long)barrier * 2 * Basis / denominator));
        return Math.Min(SpiritBarrierReductionMaximum, raw);
    }

    public static int HitChance(int accuracy, int evasion, bool spell = false, bool alwaysHit = false)
    {
        if (spell || alwaysHit) return Basis;
        double effectiveEvasion = Math.Pow(Math.Max(0, evasion) / 4d, .8d);
        double denominator = Math.Max(0, accuracy) + effectiveEvasion;
        double baseChance = denominator <= 0 ? .05d : Math.Max(0, accuracy) / denominator;
        return Math.Clamp((int)Math.Floor(baseChance / .98d * Basis), 500, Basis);
    }

    public static int CriticalChance(int baseCriticalBasisPoints, int increasedBasisPoints,
        int flatBasisPoints = 0) => Math.Clamp(
        checked(ApplyIncreased(baseCriticalBasisPoints, increasedBasisPoints) + flatBasisPoints), 0, Basis);

    public static int AttackFrequencyMilliPerSecond(int attacksPerSecondMilli, int increasedSpeedBasisPoints,
        IEnumerable<int>? moreSpeedMultipliers = null) => Math.Clamp(
        ApplyMore(ApplyIncreased(attacksPerSecondMilli, increasedSpeedBasisPoints),
            moreSpeedMultipliers ?? []),
        0,
        MaximumAttackFrequencyMilliPerSecond);

    /// <summary>
    /// The simulation remains deterministic at 20 Hz. Frequencies above 20 attacks per second are represented by
    /// multiple attacks in one simulation tick, with the remainder carried to the next scheduled tick.
    /// </summary>
    public static int AttacksForScheduledSimulationTick(int attackFrequencyMilliPerSecond,
        ref int carriedFrequencyMilli)
    {
        int frequency = Math.Clamp(attackFrequencyMilliPerSecond, 0, MaximumAttackFrequencyMilliPerSecond);
        int frequencyPerSimulationTick = AuthoritativeSimulationTicksPerSecond * 1_000;
        if (frequency <= frequencyPerSimulationTick)
        {
            carriedFrequencyMilli = 0;
            return frequency > 0 ? 1 : 0;
        }

        int available = checked(carriedFrequencyMilli + frequency);
        int attacks = available / frequencyPerSimulationTick;
        carriedFrequencyMilli = available % frequencyPerSimulationTick;
        return attacks;
    }

    public static int AttackIntervalMilliseconds(int attacksPerSecondMilli, int increasedSpeedBasisPoints,
        IEnumerable<int>? moreSpeedMultipliers = null, int skillAttackTimeMultiplierBasisPoints = Basis)
    {
        long frequency = AttackFrequencyMilliPerSecond(attacksPerSecondMilli, increasedSpeedBasisPoints,
            moreSpeedMultipliers);
        if (frequency <= 0) return int.MaxValue;
        return checked((int)Math.Max(1, (1_000_000L * skillAttackTimeMultiplierBasisPoints +
            frequency * Basis - 1) / (frequency * Basis)));
    }

    public static int CastTimeMilliseconds(int baseCastTimeMilliseconds, int increasedSpeedBasisPoints,
        IEnumerable<int>? moreSpeedMultipliers = null)
    {
        int speed = ApplyMore(Basis + increasedSpeedBasisPoints, moreSpeedMultipliers ?? []);
        return speed <= 0 ? int.MaxValue : checked((int)Math.Max(1,
            ((long)baseCastTimeMilliseconds * Basis + speed - 1) / speed));
    }

    public static int CooldownMilliseconds(int baseCooldownMilliseconds, int increasedRecoveryBasisPoints,
        IEnumerable<int>? moreRecoveryMultipliers = null)
    {
        int recovery = ApplyMore(Basis + increasedRecoveryBasisPoints, moreRecoveryMultipliers ?? []);
        return recovery <= 0 ? int.MaxValue : checked((int)Math.Max(1,
            ((long)baseCooldownMilliseconds * Basis + recovery - 1) / recovery));
    }

    public static int ArmorReduction(int armor, int preArmorPhysicalDamage)
    {
        if (armor <= 0 || preArmorPhysicalDamage <= 0) return 0;
        long denominator = checked((long)armor + 5L * preArmorPhysicalDamage);
        return Math.Min(9_000, checked((int)((long)armor * Basis / denominator)));
    }

    public static int PhysicalDotArmorReduction(int armor, int preArmorPhysicalDamagePerSecond) =>
        ArmorReduction(checked(Math.Max(0, armor) * 3 / 10), preArmorPhysicalDamagePerSecond);

    public static int EffectiveResistance(int uncappedResistance, int maximumResistance,
        int penetrationBasisPoints = 0, bool ignoreResistance = false)
    {
        if (ignoreResistance) return 0;
        int panel = Math.Clamp(uncappedResistance, MinimumResistance,
            Math.Min(maximumResistance, AbsoluteElementalResistanceMaximum));
        return Math.Clamp(panel - penetrationBasisPoints, MinimumResistance,
            Math.Min(maximumResistance, AbsoluteElementalResistanceMaximum));
    }

    public static int MitigateByResistance(int damage, int resistanceBasisPoints) => damage <= 0 ? 0 :
        Math.Max(resistanceBasisPoints < Basis ? 1 : 0,
            SaturatingScale(damage, (long)Basis - resistanceBasisPoints));

    public static int BlockChance(int rawBasisPoints, int maximumBasisPoints = DefaultBlockMaximum) =>
        Math.Clamp(rawBasisPoints, 0, Math.Min(maximumBasisPoints, AbsoluteBlockMaximum));

    public static int SuppressedDamage(int damage, int suppressionEffectBasisPoints = DefaultSuppressionEffect) =>
        checked((int)((long)Math.Max(0, damage) *
            (Basis - Math.Clamp(suppressionEffectBasisPoints, 0, AbsoluteSuppressionEffectMaximum)) / Basis));

    public static int FortificationMultiplier(int layers, int maximumLayers = 20) =>
        Basis - Math.Clamp(layers, 0, Math.Min(30, maximumLayers)) * 100;

    public static int CorrosionResistanceReduction(int layers, int maximumLayers = 5) =>
        Math.Clamp(layers, 0, Math.Min(10, maximumLayers)) * 1_000;

    public static int WitherMultiplier(int layers, int maximumLayers = 10)
    {
        int count = Math.Clamp(layers, 0, Math.Min(15, maximumLayers));
        int result = Basis;
        for (int i = 0; i < count; i++) result = checked(result * 10_800 / Basis);
        return result;
    }

    public static int ArmorAfterBreak(int armor, int stacks, int maximumStacks = 5,
        int additionalReductionBasisPoints = 0)
    {
        int reduction = Math.Min(9_000,
            Math.Clamp(stacks, 0, Math.Min(9, maximumStacks)) * 1_000 + additionalReductionBasisPoints);
        return checked(Math.Max(0, armor) * (Basis - reduction) / Basis);
    }

    public static int ExposureReduction(int baseReductionBasisPoints = 1_500,
        int increasedEffectBasisPoints = 0) => Math.Min(5_000,
        ApplyIncreased(baseReductionBasisPoints, increasedEffectBasisPoints));

    public static int AilmentThreshold(int maximumLife, CombatRarity rarity) => rarity switch
    {
        CombatRarity.Normal => Math.Max(1, maximumLife),
        CombatRarity.Magic => Math.Max(1, (int)(maximumLife * 9L / 10)),
        CombatRarity.Rare => Math.Max(1, (int)(maximumLife * 8L / 10)),
        CombatRarity.MapBoss => Math.Max(1, maximumLife / 4),
        _ => Math.Max(1, (int)(maximumLife * 15L / 100)),
    };

    public static AilmentResult Chill(int coldHit, int threshold, int increasedDurationBasisPoints = 0,
        int maximumEffectBasisPoints = 3_000)
    {
        int effect = Math.Min(maximumEffectBasisPoints, PowerEffect(3_000, coldHit, threshold));
        if (effect < 500) return default;
        return new(effect, ApplyIncreased(2_000, increasedDurationBasisPoints));
    }

    public static AilmentResult Freeze(int coldHit, int threshold, int increasedDurationBasisPoints = 0,
        int maximumDurationMilliseconds = 3_000)
    {
        int duration = checked((int)(3_000d * Math.Pow(Math.Max(0, coldHit) / (double)Math.Max(1, threshold), .4d)));
        duration = ApplyIncreased(duration, increasedDurationBasisPoints);
        return duration < 300 ? default : new(10_000, Math.Min(maximumDurationMilliseconds, duration));
    }

    public static AilmentResult Shock(int lightningHit, int threshold, int maximumEffectBasisPoints = 5_000)
    {
        int effect = Math.Min(maximumEffectBasisPoints, PowerEffect(5_000, lightningHit, threshold));
        return effect < 500 ? default : new(effect, 2_000);
    }

    public static AilmentResult Paralysis(int lightningHit, int threshold,
        int increasedAccumulationBasisPoints = 0, int maximumDurationMilliseconds = 1_000)
    {
        int accumulation = ApplyIncreased(PowerEffect(4_000, lightningHit, threshold),
            increasedAccumulationBasisPoints);
        return new(0, maximumDurationMilliseconds, accumulation);
    }

    public static int StunChance(int finalHitDamage, int threshold, int thresholdReductionBasisPoints = 0,
        int chanceIncreaseBasisPoints = 0)
    {
        int effectiveThreshold = ApplyIncreased(Math.Max(1, threshold),
            -Math.Clamp(thresholdReductionBasisPoints, 0, 7_500));
        int raw = checked((int)Math.Min(Basis, 20_000L * Math.Max(0, finalHitDamage) / effectiveThreshold));
        return Math.Clamp(ApplyIncreased(raw, chanceIncreaseBasisPoints), 0, Basis);
    }

    public static DamagePacket ConvertAndScale(int baseDamage, DamageType baseType,
        IEnumerable<Conversion>? conversions, IEnumerable<ExtraDamage>? extras,
        DamageModifiers? modifiers = null, Action<IReadOnlyList<DamageBranch>>? captureSource = null)
    {
        modifiers ??= new DamageModifiers();
        var branches = new List<DamageBranch>
        {
            new(Math.Max(0, baseDamage), baseType, [baseType], [$"base:{baseType}={Math.Max(0, baseDamage)}"]),
        };
        Conversion[] conversionArray = (conversions ?? []).Where(IsAllowed).ToArray();
        ExtraDamage[] extraArray = (extras ?? []).Where(extra => IsAllowed(
            new Conversion(extra.Source, extra.Target, extra.BasisPoints, extra.StableId))).ToArray();

        foreach (DamageType source in ConversionOrder)
        {
            foreach (DamageBranch original in branches.Where(branch => branch.CurrentType == source).ToArray())
            {
                foreach (ExtraDamage extra in extraArray.Where(item => item.Source == source))
                {
                    int amount = checked((int)((long)original.BaseDamage * Math.Max(0, extra.BasisPoints) / Basis));
                    if (amount > 0) branches.Add(new(amount, extra.Target, [.. original.History, extra.Target],
                        [.. original.Trace, $"extra:{extra.StableId}:{amount}"]));
                }
                SplitBranch(branches, original, conversionArray.Where(item => item.Source == source).ToArray());
            }
        }

        captureSource?.Invoke(branches);
        var scaled = new List<DamageBranch>(branches.Count);
        foreach (DamageBranch branch in branches)
        {
            int value = branch.BaseDamage;
            var trace = branch.Trace.ToList();
            var appliedTypes = new HashSet<DamageType>();
            for (int index = 0; index < branch.History.Count; index++)
            {
                DamageType type = branch.History[index];
                if (!appliedTypes.Add(type)) continue;
                int increase = modifiers.IncreasedByType?.GetValueOrDefault(type) ?? 0;
                if (index == 0) increase = checked(increase + modifiers.InitialIncreasedBasisPoints);
                if (type is DamageType.Fire or DamageType.Cold or DamageType.Lightning &&
                    !branch.History.Take(index).Any(previous => previous is DamageType.Fire or DamageType.Cold or DamageType.Lightning))
                    increase = checked(increase + modifiers.ElementalIncreasedBasisPoints);
                value = ApplyIncreased(value, increase);
                trace.Add($"increase:{type}:{increase}=>{value}");
            }
            value = ApplyMore(value, (modifiers.MoreByStableId ?? new Dictionary<string, int>()).Values);
            scaled.Add(branch with { BaseDamage = value, Trace = trace });
        }
        return Packet(scaled);
    }

    public static DamagePacket Mitigate(DamagePacket packet, int armor,
        ResistanceProfile resistances, IReadOnlyDictionary<DamageType, int>? penetration = null,
        IReadOnlySet<DamageType>? ignoredResistances = null, int armorIgnoreBasisPoints = 0)
    {
        var result = new List<DamageBranch>(packet.Branches.Count);
        foreach (DamageBranch branch in packet.Branches)
        {
            int value = branch.BaseDamage;
            var trace = branch.Trace.ToList();
            if (branch.CurrentType == DamageType.Physical)
            {
                int usedArmor = SaturatingScale(Math.Max(0, armor),
                    Basis - Math.Clamp(armorIgnoreBasisPoints, 0, 9_000));
                int reduction = ArmorReduction(usedArmor, value);
                value = SaturatingScale(value, Basis - reduction);
                trace.Add($"armor:{usedArmor}:{reduction}=>{value}");
            }
            int resistance = EffectiveResistance(resistances.For(branch.CurrentType),
                resistances.MaximumFor(branch.CurrentType), penetration?.GetValueOrDefault(branch.CurrentType) ?? 0,
                ignoredResistances?.Contains(branch.CurrentType) == true);
            value = MitigateByResistance(value, resistance);
            if (branch.BaseDamage > 0 && value == 0) value = 1;
            trace.Add($"resistance:{branch.CurrentType}:{resistance}=>{value}");
            result.Add(branch with { BaseDamage = value, Trace = trace });
        }
        return Packet(result);
    }

    public static ResistanceProfile MonsterResistances(int mapTier, CombatRarity rarity)
    {
        int tier = Math.Clamp(mapTier, 1, 20);
        (int campaign, int e1, int economy, int e20) = rarity switch
        {
            CombatRarity.Normal => (0, 1_500, 400, 2_500),
            CombatRarity.Magic => (200, 2_000, 600, 3_000),
            CombatRarity.Rare => (400, 2_500, 800, 3_500),
            CombatRarity.MapBoss => (600, 3_000, 1_000, 4_000),
            _ => (800, 3_500, 1_200, 4_500),
        };
        int physical = LerpTier(campaign, economy, tier);
        int elemental = LerpTier(e1, e20, tier);
        return new(physical, elemental, elemental, elemental, elemental + 500);
    }

    public static int MonsterArmor(int mapTier, CombatRarity rarity)
    {
        int tier = Math.Clamp(mapTier, 1, 20);
        int referenceHit = checked((int)Math.Round(1_000d * Math.Pow(1.09d, tier - 1)));
        (int atOne, int atTwenty) = rarity switch
        {
            CombatRarity.Normal => (1_500, 2_000),
            CombatRarity.Magic => (2_000, 2_500),
            CombatRarity.Rare or CombatRarity.MapBoss => (2_500, 3_000),
            _ => (3_000, 3_500),
        };
        int reduction = LerpTier(atOne, atTwenty, tier);
        return checked((int)(5L * referenceHit * reduction / (Basis - reduction)));
    }

    private static int PowerEffect(int coefficientBasisPoints, int damage, int threshold) =>
        checked((int)(coefficientBasisPoints * Math.Pow(Math.Max(0, damage) / (double)Math.Max(1, threshold), .4d)));

    private static int LerpTier(int atOne, int atTwenty, int tier) =>
        checked(atOne + (atTwenty - atOne) * (tier - 1) / 19);

    private static bool IsAllowed(Conversion conversion) => conversion.Source switch
    {
        DamageType.Physical => conversion.Target is DamageType.Fire or DamageType.Cold or
            DamageType.Lightning or DamageType.Void,
        DamageType.Lightning or DamageType.Cold => conversion.Target is DamageType.Fire or DamageType.Void,
        DamageType.Fire => conversion.Target == DamageType.Void,
        _ => false,
    };

    private static void SplitBranch(List<DamageBranch> branches, DamageBranch original,
        IReadOnlyList<Conversion> requested)
    {
        if (requested.Count == 0 || original.BaseDamage <= 0) return;
        long total = requested.Sum(item => (long)Math.Max(0, item.BasisPoints));
        long divisor = Math.Max(Basis, total);
        int remaining = original.BaseDamage;
        int originalIndex = branches.IndexOf(original);
        branches.RemoveAt(originalIndex);
        var produced = new List<DamageBranch>();
        Conversion[] ordered = requested.Where(item => item.BasisPoints > 0).OrderBy(item => item.StableId, StringComparer.Ordinal).ToArray();
        for (int index = 0; index < ordered.Length; index++)
        {
            Conversion conversion = ordered[index];
            int weight = Math.Max(0, conversion.BasisPoints);
            int converted = total >= Basis && index == ordered.Length - 1 ? remaining :
                checked((int)((long)original.BaseDamage * weight / divisor));
            converted = Math.Min(remaining, converted);
            remaining -= converted;
            if (converted > 0) produced.Add(new(converted, conversion.Target,
                [.. original.History, conversion.Target], [.. original.Trace, $"convert:{conversion.StableId}:{converted}"]));
        }
        if (remaining > 0) produced.Insert(0, original with { BaseDamage = remaining });
        branches.InsertRange(originalIndex, produced);
    }

    private static DamagePacket Packet(IReadOnlyList<DamageBranch> branches)
    {
        int Sum(DamageType type) => (int)Math.Clamp(branches
            .Where(branch => branch.CurrentType == type).Sum(branch => (long)branch.BaseDamage), 0, int.MaxValue);
        return new(Sum(DamageType.Physical), Sum(DamageType.Fire), Sum(DamageType.Cold),
            Sum(DamageType.Lightning), Sum(DamageType.Void), branches,
            branches.SelectMany(branch => branch.Trace).ToArray());
    }

    private static int SaturatingScale(int value, long basisPoints)
    {
        if (value <= 0 || basisPoints <= 0) return 0;
        if (value > long.MaxValue / basisPoints) return int.MaxValue;
        return (int)Math.Clamp((long)value * basisPoints / Basis, 0, int.MaxValue);
    }
}
