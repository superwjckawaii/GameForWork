using GameForWork.Core.P1.Combat;

namespace GameForWork.Core.P1.Items;

public enum P1FlaskKind { Life, Mana, Armor, Movement, Resistance }

public static class P1FlaskRules
{
    public static P1FlaskKind? KindForBase(string stableId) => stableId switch
    {
        "core.base.life_flask" => P1FlaskKind.Life,
        "core.base.mana_flask" => P1FlaskKind.Mana,
        "core.base.armor_flask" => P1FlaskKind.Armor,
        "core.base.movement_flask" => P1FlaskKind.Movement,
        "core.base.resistance_flask" => P1FlaskKind.Resistance,
        _ => null,
    };
}

public sealed class P1UtilityFlaskState
{
    private readonly int _maximumCharges;
    private readonly int _chargesPerUse;
    private readonly int _durationTicks;
    public P1UtilityFlaskState(int maximumCharges = 40, int chargesPerUse = 20, int durationTicks = 100)
    {
        _maximumCharges = maximumCharges;
        _chargesPerUse = chargesPerUse;
        _durationTicks = durationTicks;
        Charges = maximumCharges;
    }
    public int Charges { get; private set; }
    public int RemainingTicks { get; private set; }
    public bool Active => RemainingTicks > 0;
    public bool TryUse()
    {
        if (Active || Charges < _chargesPerUse) return false;
        Charges -= _chargesPerUse;
        RemainingTicks = _durationTicks;
        return true;
    }
    public void AdvanceTick() => RemainingTicks = Math.Max(0, RemainingTicks - 1);
    public void GainCharges(int amount) => Charges = Math.Min(_maximumCharges, checked(Charges + Math.Max(0, amount)));
}

public sealed record LifeFlaskDefinition(int BaseRecovery, int MaximumCharges, int ChargesPerUse);

public sealed class LifeFlaskState
{
    public LifeFlaskState(LifeFlaskDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.BaseRecovery < 0 || definition.MaximumCharges < 0 || definition.ChargesPerUse <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(definition));
        }

        Definition = definition;
        Charges = definition.MaximumCharges;
    }

    public LifeFlaskDefinition Definition { get; }
    public int Charges { get; private set; }

    public int TryUse(int missingLife, int increasedEffectBasisPoints)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(missingLife);
        if (Charges < Definition.ChargesPerUse || missingLife == 0)
        {
            return 0;
        }

        Charges -= Definition.ChargesPerUse;
        int recovery = checked((int)((long)Definition.BaseRecovery * (10_000 + increasedEffectBasisPoints) / 10_000));
        return Math.Min(missingLife, recovery);
    }

    public void GainCharges(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        Charges = Math.Min(Definition.MaximumCharges, checked(Charges + amount));
    }
}

public static class P1LegendaryRules
{
    public static SkillUseProfile ApplyToHeavyStrike(SkillUseProfile profile, LegendaryRule? rule)
    {
        if (rule is null || profile.SkillId != P1SkillIds.HeavyStrike)
        {
            return profile;
        }

        int slowerInterval = DivideRoundUp(
            checked(profile.AttackIntervalTicks * 10_000),
            rule.HeavyStrikeAttackSpeedMultiplierBasisPoints);
        return profile with { AttackIntervalTicks = slowerInterval };
    }

    public static int CalculateAftershockDamage(int heavyStrikeDamage, LegendaryRule? rule) =>
        rule is null
            ? 0
            : checked((int)((long)heavyStrikeDamage * rule.AftershockDamageMultiplierBasisPoints / 10_000));

    private static int DivideRoundUp(int numerator, int denominator) =>
        checked((numerator + denominator - 1) / denominator);
}
