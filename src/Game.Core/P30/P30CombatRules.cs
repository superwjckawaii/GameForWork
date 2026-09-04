using GameForWork.Core.P17;

namespace GameForWork.Core.P30;

public enum P30DamageType { Physical, Fire, Cold, Lightning, Void }

public enum P30CombatRarity { Normal, Magic, Rare, MapBoss, PinnacleBoss }

public readonly record struct P30Conversion(P30DamageType Source, P30DamageType Target, int BasisPoints,
    string StableId);

public readonly record struct P30ExtraDamage(P30DamageType Source, P30DamageType Target, int BasisPoints,
    string StableId);

public sealed record P30DamageModifiers(
    IReadOnlyDictionary<P30DamageType, int>? IncreasedByType = null,
    int InitialIncreasedBasisPoints = 0,
    int ElementalIncreasedBasisPoints = 0,
    IReadOnlyDictionary<string, int>? MoreByStableId = null);

public sealed record P30DamageBranch(
    int BaseDamage,
    P30DamageType CurrentType,
    IReadOnlyList<P30DamageType> History,
    IReadOnlyList<string> Trace);

public sealed record P30DamagePacket(
    int Physical,
    int Fire,
    int Cold,
    int Lightning,
    int Void,
    IReadOnlyList<P30DamageBranch> Branches,
    IReadOnlyList<string> Trace)
{
    public int Total => (int)Math.Clamp(
        (long)Physical + Fire + Cold + Lightning + Void, 0, int.MaxValue);
}

public readonly record struct P30ResistanceProfile(
    int Physical,
    int Fire,
    int Cold,
    int Lightning,
    int Void,
    int PhysicalMaximum = 3_500,
    int ElementalMaximum = 7_500,
    int VoidMaximum = 7_500)
{
    public int For(P30DamageType type) => type switch
    {
        P30DamageType.Physical => Physical,
        P30DamageType.Fire => Fire,
        P30DamageType.Cold => Cold,
        P30DamageType.Lightning => Lightning,
        _ => Void,
    };

    public int MaximumFor(P30DamageType type) => type == P30DamageType.Physical
        ? Math.Min(PhysicalMaximum, P30CombatRules.AbsolutePhysicalResistanceMaximum)
        : Math.Min(type == P30DamageType.Void ? VoidMaximum : ElementalMaximum,
            P30CombatRules.AbsoluteElementalResistanceMaximum);
}

public readonly record struct P30AilmentResult(int EffectBasisPoints, int DurationMilliseconds,
    int AccumulationBasisPoints = 0);

public static class P30CombatRules
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

    private static readonly P30DamageType[] ConversionOrder =
        [P30DamageType.Physical, P30DamageType.Lightning, P30DamageType.Cold, P30DamageType.Fire];

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

    public static int AttackIntervalMilliseconds(int attacksPerSecondMilli, int increasedSpeedBasisPoints,
        IEnumerable<int>? moreSpeedMultipliers = null, int skillAttackTimeMultiplierBasisPoints = Basis)
    {
        long frequency = ApplyMore(ApplyIncreased(attacksPerSecondMilli, increasedSpeedBasisPoints),
            moreSpeedMultipliers ?? []);
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

    public static int AilmentThreshold(int maximumLife, P30CombatRarity rarity) => rarity switch
    {
        P30CombatRarity.Normal => Math.Max(1, maximumLife / 3),
        P30CombatRarity.Magic => Math.Max(1, maximumLife / 2),
        P30CombatRarity.Rare => Math.Max(1, maximumLife),
        P30CombatRarity.MapBoss => Math.Max(1, maximumLife * 2),
        _ => Math.Max(1, maximumLife * 4),
    };

    public static P30AilmentResult Chill(int coldHit, int threshold, int increasedDurationBasisPoints = 0,
        int maximumEffectBasisPoints = 3_000)
    {
        int effect = Math.Min(maximumEffectBasisPoints, PowerEffect(3_000, coldHit, threshold));
        if (effect < 500) return default;
        return new(effect, ApplyIncreased(2_000, increasedDurationBasisPoints));
    }

    public static P30AilmentResult Freeze(int coldHit, int threshold, int increasedDurationBasisPoints = 0,
        int maximumDurationMilliseconds = 3_000)
    {
        int duration = checked((int)(3_000d * Math.Pow(Math.Max(0, coldHit) / (double)Math.Max(1, threshold), .4d)));
        duration = ApplyIncreased(duration, increasedDurationBasisPoints);
        return duration < 300 ? default : new(10_000, Math.Min(maximumDurationMilliseconds, duration));
    }

    public static P30AilmentResult Shock(int lightningHit, int threshold, int maximumEffectBasisPoints = 5_000)
    {
        int effect = Math.Min(maximumEffectBasisPoints, PowerEffect(5_000, lightningHit, threshold));
        return effect < 500 ? default : new(effect, 2_000);
    }

    public static P30AilmentResult Paralysis(int lightningHit, int threshold,
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

    public static P30DamagePacket ConvertAndScale(int baseDamage, P30DamageType baseType,
        IEnumerable<P30Conversion>? conversions, IEnumerable<P30ExtraDamage>? extras,
        P30DamageModifiers? modifiers = null)
    {
        modifiers ??= new P30DamageModifiers();
        var branches = new List<P30DamageBranch>
        {
            new(Math.Max(0, baseDamage), baseType, [baseType], [$"base:{baseType}={Math.Max(0, baseDamage)}"]),
        };
        P30Conversion[] conversionArray = (conversions ?? []).Where(IsAllowed).ToArray();
        P30ExtraDamage[] extraArray = (extras ?? []).Where(extra => IsAllowed(
            new P30Conversion(extra.Source, extra.Target, extra.BasisPoints, extra.StableId))).ToArray();

        foreach (P30DamageType source in ConversionOrder)
        {
            foreach (P30DamageBranch original in branches.Where(branch => branch.CurrentType == source).ToArray())
            {
                foreach (P30ExtraDamage extra in extraArray.Where(item => item.Source == source))
                {
                    int amount = checked((int)((long)original.BaseDamage * Math.Max(0, extra.BasisPoints) / Basis));
                    if (amount > 0) branches.Add(new(amount, extra.Target, [.. original.History, extra.Target],
                        [.. original.Trace, $"extra:{extra.StableId}:{amount}"]));
                }
                SplitBranch(branches, original, conversionArray.Where(item => item.Source == source).ToArray());
            }
        }

        var scaled = new List<P30DamageBranch>(branches.Count);
        foreach (P30DamageBranch branch in branches)
        {
            int value = branch.BaseDamage;
            var trace = branch.Trace.ToList();
            var appliedTypes = new HashSet<P30DamageType>();
            for (int index = 0; index < branch.History.Count; index++)
            {
                P30DamageType type = branch.History[index];
                if (!appliedTypes.Add(type)) continue;
                int increase = modifiers.IncreasedByType?.GetValueOrDefault(type) ?? 0;
                if (index == 0) increase = checked(increase + modifiers.InitialIncreasedBasisPoints);
                if (type is P30DamageType.Fire or P30DamageType.Cold or P30DamageType.Lightning &&
                    !branch.History.Take(index).Any(previous => previous is P30DamageType.Fire or P30DamageType.Cold or P30DamageType.Lightning))
                    increase = checked(increase + modifiers.ElementalIncreasedBasisPoints);
                value = ApplyIncreased(value, increase);
                trace.Add($"increase:{type}:{increase}=>{value}");
            }
            value = ApplyMore(value, (modifiers.MoreByStableId ?? new Dictionary<string, int>()).Values);
            scaled.Add(branch with { BaseDamage = value, Trace = trace });
        }
        return Packet(scaled);
    }

    public static P30DamagePacket Mitigate(P30DamagePacket packet, int armor,
        P30ResistanceProfile resistances, IReadOnlyDictionary<P30DamageType, int>? penetration = null,
        IReadOnlySet<P30DamageType>? ignoredResistances = null, int armorIgnoreBasisPoints = 0)
    {
        var result = new List<P30DamageBranch>(packet.Branches.Count);
        foreach (P30DamageBranch branch in packet.Branches)
        {
            int value = branch.BaseDamage;
            var trace = branch.Trace.ToList();
            if (branch.CurrentType == P30DamageType.Physical)
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
            trace.Add($"resistance:{branch.CurrentType}:{resistance}=>{value}");
            result.Add(branch with { BaseDamage = value, Trace = trace });
        }
        return Packet(result);
    }

    public static P30ResistanceProfile MonsterResistances(int mapTier, P30CombatRarity rarity)
    {
        int tier = Math.Clamp(mapTier, 1, 20);
        (int p1, int e1, int p20, int e20) = rarity switch
        {
            P30CombatRarity.Normal => (0, 1_500, 400, 2_500),
            P30CombatRarity.Magic => (200, 2_000, 600, 3_000),
            P30CombatRarity.Rare => (400, 2_500, 800, 3_500),
            P30CombatRarity.MapBoss => (600, 3_000, 1_000, 4_000),
            _ => (800, 3_500, 1_200, 4_500),
        };
        int physical = LerpTier(p1, p20, tier);
        int elemental = LerpTier(e1, e20, tier);
        return new(physical, elemental, elemental, elemental, elemental + 500);
    }

    public static int MonsterArmor(int mapTier, P30CombatRarity rarity)
    {
        int tier = Math.Clamp(mapTier, 1, 20);
        int referenceHit = checked((int)Math.Round(1_000d * Math.Pow(1.09d, tier - 1)));
        (int atOne, int atTwenty) = rarity switch
        {
            P30CombatRarity.Normal => (1_500, 2_000),
            P30CombatRarity.Magic => (2_000, 2_500),
            P30CombatRarity.Rare or P30CombatRarity.MapBoss => (2_500, 3_000),
            _ => (3_000, 3_500),
        };
        int reduction = LerpTier(atOne, atTwenty, tier);
        return checked((int)(5L * referenceHit * reduction / (Basis - reduction)));
    }

    private static int PowerEffect(int coefficientBasisPoints, int damage, int threshold) =>
        checked((int)(coefficientBasisPoints * Math.Pow(Math.Max(0, damage) / (double)Math.Max(1, threshold), .4d)));

    private static int LerpTier(int atOne, int atTwenty, int tier) =>
        checked(atOne + (atTwenty - atOne) * (tier - 1) / 19);

    private static bool IsAllowed(P30Conversion conversion) => conversion.Source switch
    {
        P30DamageType.Physical => conversion.Target is P30DamageType.Fire or P30DamageType.Cold or
            P30DamageType.Lightning or P30DamageType.Void,
        P30DamageType.Lightning or P30DamageType.Cold => conversion.Target is P30DamageType.Fire or P30DamageType.Void,
        P30DamageType.Fire => conversion.Target == P30DamageType.Void,
        _ => false,
    };

    private static void SplitBranch(List<P30DamageBranch> branches, P30DamageBranch original,
        IReadOnlyList<P30Conversion> requested)
    {
        if (requested.Count == 0 || original.BaseDamage <= 0) return;
        int total = requested.Sum(item => Math.Max(0, item.BasisPoints));
        int divisor = Math.Max(Basis, total);
        int remaining = original.BaseDamage;
        int remainingWeight = total;
        int originalIndex = branches.IndexOf(original);
        branches.RemoveAt(originalIndex);
        var produced = new List<P30DamageBranch>();
        foreach (P30Conversion conversion in requested.OrderBy(item => item.StableId, StringComparer.Ordinal))
        {
            int weight = Math.Max(0, conversion.BasisPoints);
            int converted = remainingWeight <= 0 ? 0 : checked((int)((long)original.BaseDamage * weight / divisor));
            converted = Math.Min(remaining, converted);
            remaining -= converted;
            remainingWeight -= weight;
            if (converted > 0) produced.Add(new(converted, conversion.Target,
                [.. original.History, conversion.Target], [.. original.Trace, $"convert:{conversion.StableId}:{converted}"]));
        }
        if (remaining > 0) produced.Insert(0, original with { BaseDamage = remaining });
        branches.InsertRange(originalIndex, produced);
    }

    private static P30DamagePacket Packet(IReadOnlyList<P30DamageBranch> branches)
    {
        int Sum(P30DamageType type) => (int)Math.Clamp(branches
            .Where(branch => branch.CurrentType == type).Sum(branch => (long)branch.BaseDamage), 0, int.MaxValue);
        return new(Sum(P30DamageType.Physical), Sum(P30DamageType.Fire), Sum(P30DamageType.Cold),
            Sum(P30DamageType.Lightning), Sum(P30DamageType.Void), branches,
            branches.SelectMany(branch => branch.Trace).ToArray());
    }

    private static int SaturatingScale(int value, long basisPoints)
    {
        if (value <= 0 || basisPoints <= 0) return 0;
        if (value > long.MaxValue / basisPoints) return int.MaxValue;
        return (int)Math.Clamp((long)value * basisPoints / Basis, 0, int.MaxValue);
    }
}
