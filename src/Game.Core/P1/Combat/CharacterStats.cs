namespace GameForWork.Core.P1.Combat;

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
    int IncreasedManaRegenerationBasisPoints = 0)
{
    public CalculatedValue MaximumLife()
    {
        var trace = new FormulaTraceBuilder();
        int baseLife = checked(80 + (8 * Level) + Attributes.Physique + FlatMaximumLife);
        trace.Add("基础最大生命", $"80 + 8 × {Level} + {Attributes.Physique} + {FlatMaximumLife}", baseLife);
        int final = ApplyIncreased(baseLife, IncreasedMaximumLifeBasisPoints);
        trace.Add("最大生命增加", $"{baseLife} × (10000 + {IncreasedMaximumLifeBasisPoints}) / 10000", final);
        return trace.Build(final);
    }

    public CalculatedValue MaximumMana()
    {
        int value = checked(40 + (2 * Level) + (2 * Attributes.Spirit) + FlatMaximumMana);
        return CalculatedValue.Single(
            "最大法力",
            $"40 + 2 × {Level} + 2 × {Attributes.Spirit} + {FlatMaximumMana}",
            value);
    }

    public CalculatedValue MaximumShield()
    {
        var trace = new FormulaTraceBuilder();
        int baseShield = checked(Equipment.Shield + (2 * Attributes.Energy));
        trace.Add("基础最大护盾", $"{Equipment.Shield} + 2 × {Attributes.Energy}", baseShield);
        int final = ApplyIncreased(baseShield, IncreasedShieldBasisPoints);
        trace.Add("最大护盾增加", $"{baseShield} × (10000 + {IncreasedShieldBasisPoints}) / 10000", final);
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
        int value = ApplyIncreased(baseRegeneration, IncreasedManaRegenerationBasisPoints);
        return CalculatedValue.Single(
            "每秒法力恢复",
            $"{maximumMana} × 6% × (10000 + {IncreasedManaRegenerationBasisPoints}) / 10000",
            value);
    }

    public CalculatedValue ShieldRecoveryPerSecond()
    {
        int maximumShield = MaximumShield().Value;
        int energySpeedIncrease = ShieldRecoverySpeedIncreaseBasisPoints().Value;
        int baseRecovery = checked(maximumShield * 2_000 / 10_000);
        int value = ApplyIncreased(baseRecovery, energySpeedIncrease);
        return CalculatedValue.Single(
            "每秒护盾恢复",
            $"{maximumShield} × 20% × (10000 + {energySpeedIncrease}) / 10000",
            value);
    }

    private static int ApplyIncreased(int value, int increasedBasisPoints) =>
        checked((int)((long)value * (10_000 + increasedBasisPoints) / 10_000));
}

public sealed class ResourceState
{
    private int _manaRecoveryRemainder;
    private int _shieldRecoveryRemainder;

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
    public int MaximumLife { get; }
    public int MaximumMana { get; }
    public int MaximumShield { get; }
    public int Life { get; private set; }
    public int Mana { get; private set; }
    public int Shield { get; private set; }
    public int LastDamageTick { get; private set; } = int.MinValue / 2;
    public bool IsAlive => Life > 0;

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
    }

    public int HealLife(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        int previous = Life;
        Life = Math.Min(MaximumLife, checked(Life + amount));
        return Life - previous;
    }

    public int RestoreMana(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        int previous = Mana;
        Mana = Math.Min(MaximumMana, checked(Mana + amount));
        return Mana - previous;
    }

    public void AdvanceRegenerationTick(int tick)
    {
        const int ticksPerSecond = 20;
        int manaPerSecond = Sheet.ManaRegenerationPerSecond().Value;
        _manaRecoveryRemainder += manaPerSecond;
        Mana = Math.Min(MaximumMana, Mana + (_manaRecoveryRemainder / ticksPerSecond));
        _manaRecoveryRemainder %= ticksPerSecond;

        if (tick - LastDamageTick < 2 * ticksPerSecond)
        {
            _shieldRecoveryRemainder = 0;
            return;
        }

        int shieldPerSecond = Sheet.ShieldRecoveryPerSecond().Value;
        _shieldRecoveryRemainder += shieldPerSecond;
        Shield = Math.Min(MaximumShield, Shield + (_shieldRecoveryRemainder / ticksPerSecond));
        _shieldRecoveryRemainder %= ticksPerSecond;
    }
}
