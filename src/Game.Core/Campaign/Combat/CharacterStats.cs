using GameForWork.Core.Builds;

namespace GameForWork.Core.Campaign.Combat;

public sealed record CharacterAttributes(int Physique, int Dexterity, int Spirit, int Energy)
{
    public static CharacterAttributes IronOathStarting => new(20, 10, 10, 10);
}

public sealed record DefensiveEquipment(int Armor, int Evasion, int Shield);

public sealed record CharacterSheet(
    int Level,
    CharacterAttributes Attributes,
    DefensiveEquipment Equipment,
    int FlatMaximumLife = 0,
    int IncreasedMaximumLifeBasisPoints = 0,
    int FlatMaximumMana = 0,
    int IncreasedArmorBasisPoints = 0,
    int IncreasedEvasionBasisPoints = 0,
    int IncreasedShieldBasisPoints = 0,
    int IncreasedManaRegenerationBasisPoints = 0,
    int FireResistanceBasisPoints = 0,
    int ColdResistanceBasisPoints = 0,
    int LightningResistanceBasisPoints = 0,
    int VoidResistanceBasisPoints = 0,
    int BlockChanceBasisPoints = 0,
    int SpellSuppressionBasisPoints = 0,
    int FlatLifeRegeneration = 0,
    int IncreasedMovementSpeedBasisPoints = 0,
    int IncreasedMaximumManaBasisPoints = 0,
    int FlatMaximumShield = 0,
    int EquipmentSpiritBarrier = 0,
    int FlatSpiritBarrier = 0,
    int IncreasedSpiritBarrierBasisPoints = 0,
    int MaximumPhysicalResistanceBasisPoints = 3_500,
    int MaximumElementalResistanceBasisPoints = 7_500,
    int MaximumVoidResistanceBasisPoints = 7_500,
    int MaximumBlockChanceBasisPoints = 7_500,
    int SpellSuppressionEffectBasisPoints = 7_000,
    int SpellBlockChanceBasisPoints = 0,
    int MaximumSpellBlockChanceBasisPoints = 7_500,
    int IncreasedRecoveryRateBasisPoints = 0,
    int MaximumLifeMultiplierBasisPoints = 10_000,
    int MaximumManaMultiplierBasisPoints = 10_000,
    int MaximumShieldMultiplierBasisPoints = 10_000,
    int IncreasedLifeLeechRecoverySpeedBasisPoints = 0,
    int MaximumFireResistanceBonusBasisPoints = 0,
    int MaximumColdResistanceBonusBasisPoints = 0,
    int MaximumLightningResistanceBonusBasisPoints = 0,
    int MaximumLifeRegenerationBasisPoints = 0,
    int MaximumShieldRegenerationBasisPoints = 0,
    int ReducedShieldRechargeDelayBasisPoints = 0,
    int IncreasedLeechRecoveryRateBasisPoints = 0,
    int IncreasedMaximumLeechRateBasisPoints = 0,
    int LifeRecoveryMultiplierBasisPoints = 10_000)
{
    public int ResistanceMaximum(EnemyDamageType type) => type switch
    {
        EnemyDamageType.Fire => Math.Min(9_000, MaximumElementalResistanceBasisPoints + MaximumFireResistanceBonusBasisPoints),
        EnemyDamageType.Cold => Math.Min(9_000, MaximumElementalResistanceBasisPoints + MaximumColdResistanceBonusBasisPoints),
        EnemyDamageType.Lightning => Math.Min(9_000, MaximumElementalResistanceBasisPoints + MaximumLightningResistanceBonusBasisPoints),
        EnemyDamageType.Void => Math.Min(9_000, MaximumVoidResistanceBasisPoints),
        _ => MaximumPhysicalResistanceBasisPoints,
    };
    public int CappedResistance(int value) => Math.Clamp(value, CombatRules.MinimumResistance,
        MaximumElementalResistanceBasisPoints);

    public int CappedPhysicalResistance(int value) => Math.Clamp(value, CombatRules.MinimumResistance,
        Math.Min(MaximumPhysicalResistanceBasisPoints, CombatRules.AbsolutePhysicalResistanceMaximum));

    public int CappedVoidResistance(int value) => Math.Clamp(value, CombatRules.MinimumResistance,
        Math.Min(MaximumVoidResistanceBasisPoints, CombatRules.AbsoluteElementalResistanceMaximum));

    public int EffectiveBlockChanceBasisPoints => CombatRules.BlockChance(BlockChanceBasisPoints,
        MaximumBlockChanceBasisPoints);

    public int EffectiveSpellBlockChanceBasisPoints => CombatRules.BlockChance(SpellBlockChanceBasisPoints,
        MaximumSpellBlockChanceBasisPoints);

    public int EffectiveSpellSuppressionBasisPoints => Math.Clamp(SpellSuppressionBasisPoints, 0, 10_000);
    public CalculatedValue MaximumLife()
    {
        var trace = new FormulaTraceBuilder();
        int baseLife = checked(80 + (8 * Level) + Attributes.Physique + FlatMaximumLife);
        trace.Add("基础最大生命", $"80 + 8 × {Level} + {Attributes.Physique} + {FlatMaximumLife}", baseLife);
        int increased = CombatRules.MaximumLife(Level, Attributes.Physique, FlatMaximumLife,
            IncreasedMaximumLifeBasisPoints);
        trace.Add("最大生命增加", $"{baseLife} × (10000 + {IncreasedMaximumLifeBasisPoints}) / 10000", increased);
        int final = CombatRules.ApplyMore(increased, [MaximumLifeMultiplierBasisPoints]);
        trace.Add("最大生命更多/更少", $"× {MaximumLifeMultiplierBasisPoints} / 10000", final);
        return trace.Build(final);
    }

    public CalculatedValue MaximumMana()
    {
        int value = CombatRules.MaximumMana(Level, Attributes.Spirit, FlatMaximumMana,
            IncreasedMaximumManaBasisPoints, [MaximumManaMultiplierBasisPoints]);
        return CalculatedValue.Single("最大法力",
            $"(40 + 2 × {Level} + 2 × {Attributes.Spirit} + {FlatMaximumMana}) × (10000 + {IncreasedMaximumManaBasisPoints}) / 10000 × {MaximumManaMultiplierBasisPoints} / 10000",
            value);
    }

    public CalculatedValue MaximumShield()
    {
        var trace = new FormulaTraceBuilder();
        int baseShield = checked(Equipment.Shield + (2 * Attributes.Energy) + FlatMaximumShield);
        trace.Add("基础最大护盾", $"{Equipment.Shield} + 2 × {Attributes.Energy} + {FlatMaximumShield}", baseShield);
        int increased = CombatRules.MaximumShield(Equipment.Shield, Attributes.Energy, FlatMaximumShield,
            IncreasedShieldBasisPoints);
        trace.Add("最大护盾增加", $"{baseShield} × (10000 + {IncreasedShieldBasisPoints}) / 10000", increased);
        int final = CombatRules.ApplyMore(increased, [MaximumShieldMultiplierBasisPoints]);
        trace.Add("最大护盾更多/更少", $"× {MaximumShieldMultiplierBasisPoints} / 10000", final);
        return trace.Build(final);
    }

    public CalculatedValue Armor(bool lowLife = false, bool tenacious = false)
    {
        int increased = IncreasedArmorBasisPoints + (lowLife && tenacious ? 3_000 : 0);
        int value = ApplyIncreased(Equipment.Armor, increased);
        return CalculatedValue.Single(
            "护甲",
            $"{Equipment.Armor} × (10000 + {increased}) / 10000",
            value);
    }

    public CalculatedValue Evasion()
    {
        int baseEvasion = checked(Equipment.Evasion + Attributes.Dexterity);
        int value = ApplyIncreased(baseEvasion, IncreasedEvasionBasisPoints);
        return CalculatedValue.Single(
            "闪避",
            $"({Equipment.Evasion} + {Attributes.Dexterity}) × (10000 + {IncreasedEvasionBasisPoints}) / 10000",
            value);
    }

    public CalculatedValue SpiritBarrier()
    {
        int value = CombatRules.SpiritBarrier(Level, Attributes.Spirit, EquipmentSpiritBarrier,
            FlatSpiritBarrier, IncreasedSpiritBarrierBasisPoints);
        return CalculatedValue.Single("灵障",
            $"(2 × {Level} + 4 × {Attributes.Spirit} + {EquipmentSpiritBarrier} + {FlatSpiritBarrier}) × (10000 + {IncreasedSpiritBarrierBasisPoints}) / 10000",
            value);
    }

    public CalculatedValue Accuracy(int flatAccuracy = 0)
    {
        int value = checked((2 * Attributes.Dexterity) + flatAccuracy);
        return CalculatedValue.Single("命中", $"2 × {Attributes.Dexterity} + {flatAccuracy}", value);
    }

    public CalculatedValue AttackDamageIncreaseFromPhysique()
    {
        int value = checked(Attributes.Physique * 20);
        return CalculatedValue.Single("体魄攻击伤害增加", $"{Attributes.Physique} × 0.2%", value);
    }

    public CalculatedValue AilmentDurationReductionBasisPoints()
    {
        int value = checked(Attributes.Spirit * 20);
        return CalculatedValue.Single("异常持续时间缩短", $"{Attributes.Spirit} × 0.2%", value);
    }

    public CalculatedValue ShieldRecoverySpeedIncreaseBasisPoints()
    {
        int value = checked(Attributes.Energy * 50);
        return CalculatedValue.Single("护盾恢复速度增加", $"{Attributes.Energy} × 0.5%", value);
    }

    public CalculatedValue ManaRegenerationPerSecond()
    {
        int maximumMana = MaximumMana().Value;
        int baseRegeneration = checked(maximumMana * 600 / 10_000);
        int increased = checked(IncreasedManaRegenerationBasisPoints + IncreasedRecoveryRateBasisPoints);
        int value = ApplyIncreased(baseRegeneration, increased);
        return CalculatedValue.Single(
            "每秒法力恢复",
            $"{maximumMana} × 6% × (10000 + {increased}) / 10000",
            value);
    }

    public CalculatedValue LifeRegenerationPerSecond()
    {
        int value = ApplyIncreased(Math.Max(0, FlatLifeRegeneration) +
            (int)((long)MaximumLife().Value * MaximumLifeRegenerationBasisPoints / 10_000), IncreasedRecoveryRateBasisPoints);
        return CalculatedValue.Single("每秒生命恢复",
            $"{Math.Max(0, FlatLifeRegeneration)} × (10000 + {IncreasedRecoveryRateBasisPoints}) / 10000", value);
    }

    public CalculatedValue ShieldRecoveryPerSecond()
    {
        int maximumShield = MaximumShield().Value;
        int energySpeedIncrease = ShieldRecoverySpeedIncreaseBasisPoints().Value;
        int baseRecovery = checked(maximumShield * 2_000 / 10_000);
        int increased = checked(energySpeedIncrease + IncreasedRecoveryRateBasisPoints);
        int value = ApplyIncreased(baseRecovery, increased);
        return CalculatedValue.Single(
            "每秒护盾恢复",
            $"{maximumShield} × 20% × (10000 + {increased}) / 10000",
            value);
    }

    private static int ApplyIncreased(int value, int increasedBasisPoints) =>
        checked((int)((long)value * (10_000 + increasedBasisPoints) / 10_000));
}

public sealed class ResourceState
{
    private sealed class LeechInstance(int remaining, int basePerSecond)
    {
        public int Remaining { get; set; } = remaining;
        public int BasePerSecond { get; } = basePerSecond;
        public int Remainder { get; set; }
    }

    private int _manaRecoveryRemainder;
    private int _lifeRecoveryRemainder;
    private int _shieldRecoveryRemainder;
    private int _shieldRegenerationRemainder;
    private long _lifeMultiplierRemainder;
    private readonly List<LeechInstance> _lifeLeech = [];
    private readonly List<LeechInstance> _manaLeech = [];
    private readonly List<LeechInstance> _shieldLeech = [];

    public ResourceState(
        CharacterSheet sheet,
        int? initialLife = null,
        int? initialMana = null,
        int? initialShield = null)
    {
        Sheet = sheet;
        MaximumLife = sheet.MaximumLife().Value;
        MaximumMana = sheet.MaximumMana().Value;
        MaximumShield = sheet.MaximumShield().Value;
        Life = Math.Clamp(initialLife ?? MaximumLife, 0, MaximumLife);
        Mana = Math.Clamp(initialMana ?? MaximumMana, 0, MaximumMana);
        Shield = Math.Clamp(initialShield ?? MaximumShield, 0, MaximumShield);
    }

    public CharacterSheet Sheet { get; }
    public GameForWork.Core.Combat.HarmfulStatus HarmfulStatus { get; } = new();
    public int MaximumLife { get; }
    public int MaximumMana { get; }
    public int ReservedMana { get; private set; }
    public int AvailableMaximumMana => MaximumMana - ReservedMana;
    public bool ReserveMana(int amount)
    {
        if (amount < 0 || amount + ReservedMana > MaximumMana) return false;
        ReservedMana += amount;
        Mana = Math.Min(Mana, AvailableMaximumMana);
        return true;
    }
    public int MaximumShield { get; }
    public int Life { get; private set; }
    public int Mana { get; private set; }
    public int Shield { get; private set; }
    public int LastDamageTick { get; private set; } = int.MinValue / 2;
    public bool IsAlive => Life > 0;
    public Action? LifeDepleted { get; set; }

    public void SetLifeAndShield(int basisPoints)
    {
        Life = Math.Max(1, (int)((long)MaximumLife * Math.Clamp(basisPoints, 0, 10_000) / 10_000));
        Shield = (int)((long)MaximumShield * Math.Clamp(basisPoints, 0, 10_000) / 10_000);
        _lifeLeech.Clear(); _manaLeech.Clear(); _shieldLeech.Clear();
    }

    public bool TryPayMana(int amount)
    {
        if (amount < 0 || Mana < amount)
        {
            return false;
        }

        Mana -= amount;
        return true;
    }

    public bool TryPayLifeCost(int amount)
    {
        if (amount < 0 || Life <= amount)
        {
            return false;
        }

        Life -= amount;
        return true;
    }

    public bool TryPayShield(int amount)
    {
        if (amount < 0 || Shield < amount || !IsAlive) return false;
        Shield -= amount;
        return true;
    }

    public void ApplyDamage(int amount, int tick)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        if (amount == 0)
        {
            return;
        }

        LastDamageTick = tick;
        int shieldDamage = Math.Min(Shield, amount);
        Shield -= shieldDamage;
        Life = Math.Max(0, Life - (amount - shieldDamage));
        if (Life == 0) LifeDepleted?.Invoke();
    }

    public int HealLife(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        if (!IsAlive) return 0;
        int previous = Life;
        _lifeMultiplierRemainder += (long)amount * Sheet.LifeRecoveryMultiplierBasisPoints;
        amount = (int)Math.Clamp(_lifeMultiplierRemainder / 10_000, 0, int.MaxValue);
        _lifeMultiplierRemainder %= 10_000;
        Life = (int)Math.Min(MaximumLife, (long)Life + amount);
        return Life - previous;
    }

    public int RestoreMana(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        if (!IsAlive) return 0;
        int previous = Mana;
        Mana = (int)Math.Min(AvailableMaximumMana, (long)Mana + amount);
        return Mana - previous;
    }

    public int RestoreShield(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        if (!IsAlive) return 0;
        int previous = Shield;
        Shield = (int)Math.Min(MaximumShield, (long)Shield + amount);
        return Shield - previous;
    }

    public void AddLifeLeech(int amount) => AddLeech(_lifeLeech, amount,
        Sheet.IncreasedLifeLeechRecoverySpeedBasisPoints + Sheet.IncreasedLeechRecoveryRateBasisPoints);
    public void AddManaLeech(int amount) => AddLeech(_manaLeech, amount, Sheet.IncreasedLeechRecoveryRateBasisPoints);
    public void AddShieldLeech(int amount) => AddLeech(_shieldLeech, amount, Sheet.IncreasedLeechRecoveryRateBasisPoints);

    public void AdvanceRegenerationTick(int tick)
    {
        const int ticksPerSecond = 20;
        int manaPerSecond = Sheet.ManaRegenerationPerSecond().Value;
        _manaRecoveryRemainder += manaPerSecond;
        Mana = Math.Min(AvailableMaximumMana, Mana + (_manaRecoveryRemainder / ticksPerSecond));
        _manaRecoveryRemainder %= ticksPerSecond;

        int lifePerSecond = Sheet.LifeRegenerationPerSecond().Value;
        _lifeRecoveryRemainder += lifePerSecond;
        HealLife(_lifeRecoveryRemainder / ticksPerSecond);
        _lifeRecoveryRemainder %= ticksPerSecond;

        _shieldRegenerationRemainder += (int)((long)MaximumShield * Sheet.MaximumShieldRegenerationBasisPoints / 10_000 *
            Math.Max(0L, 10_000L + Sheet.IncreasedRecoveryRateBasisPoints) / 10_000);
        RestoreShield(_shieldRegenerationRemainder / ticksPerSecond);
        _shieldRegenerationRemainder %= ticksPerSecond;

        AdvanceLeech(_lifeLeech, MaximumLife, HealLife);
        AdvanceLeech(_manaLeech, MaximumMana, RestoreMana);
        AdvanceLeech(_shieldLeech, MaximumShield, RestoreShield);

        int rechargeDelay = Math.Max(0, 2 * ticksPerSecond * (10_000 - Sheet.ReducedShieldRechargeDelayBasisPoints) / 10_000);
        if (tick - LastDamageTick < rechargeDelay)
        {
            _shieldRecoveryRemainder = 0;
            return;
        }

        int shieldPerSecond = Sheet.ShieldRecoveryPerSecond().Value;
        _shieldRecoveryRemainder += shieldPerSecond;
        Shield = Math.Min(MaximumShield, Shield + (_shieldRecoveryRemainder / ticksPerSecond));
        _shieldRecoveryRemainder %= ticksPerSecond;
    }

    private static void AddLeech(ICollection<LeechInstance> instances, int amount,
        int increasedRecoverySpeedBasisPoints = 0)
    {
        if (amount <= 0) return;
        long basePerSecond = ((long)amount + 1) / 2;
        int scaledPerSecond = (int)Math.Clamp(basePerSecond *
            Math.Max(0L, 10_000L + increasedRecoverySpeedBasisPoints) / 10_000, 1, int.MaxValue);
        instances.Add(new(amount, scaledPerSecond));
    }

    private void AdvanceLeech(List<LeechInstance> instances, int maximum, Func<int, int> restore)
    {
        const int ticksPerSecond = 20;
        if (maximum <= 0 || instances.Count == 0) return;
        int budget = Math.Max(1, (int)((long)maximum * CombatRules.DefaultLeechPerSecondMaximum / 10_000 *
            Math.Max(0L, 10_000L + Sheet.IncreasedMaximumLeechRateBasisPoints) / 10_000 / ticksPerSecond));
        foreach (LeechInstance instance in instances.ToArray())
        {
            if (budget <= 0) break;
            instance.Remainder += instance.BasePerSecond;
            int available = Math.Min(instance.Remaining, instance.Remainder / ticksPerSecond);
            int consumed = Math.Min(budget, available);
            if (consumed <= 0) continue;
            restore(consumed);
            instance.Remaining -= consumed;
            instance.Remainder -= consumed * ticksPerSecond;
            budget -= consumed;
        }
        instances.RemoveAll(instance => instance.Remaining <= 0);
    }
}
