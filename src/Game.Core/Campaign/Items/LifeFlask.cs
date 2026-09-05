using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Equipment;
using GameForWork.Core.Builds;

namespace GameForWork.Core.Campaign.Items;

public enum FlaskKind { Life, Mana, Armor, Movement, Resistance }

public static class FlaskRules
{
    public static FlaskKind? KindForBase(string stableId)
    {
        ItemBaseDefinition itemBase;
        try { itemBase = EquipmentCatalog.GetBase(stableId); }
        catch (KeyNotFoundException) { return null; }
        return itemBase.DisplayName switch
        {
            "生命药剂" => FlaskKind.Life,
            "法力药剂" => FlaskKind.Mana,
            "玄铁药剂" => FlaskKind.Armor,
            "疾行药剂" => FlaskKind.Movement,
            "棱彩药剂" => FlaskKind.Resistance,
            _ => null,
        };
    }
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

public static class LegendaryRules
{
    public static SkillUseProfile ApplyToHeavyStrike(SkillUseProfile profile, LegendaryRule? rule)
    {
        if (rule is null || profile.SkillId != SkillIds.HeavyStrike)
        {
            return profile;
        }

        int uncappedFrequency = CombatRules.ApplyMore(profile.UncappedAttackFrequencyMilliPerSecond,
            [rule.HeavyStrikeAttackSpeedMultiplierBasisPoints]);
        int frequency = Math.Clamp(uncappedFrequency, 1, CombatRules.MaximumAttackFrequencyMilliPerSecond);
        int slowerInterval = DivideRoundUp(20_000, frequency);
        return profile with
        {
            AttackIntervalTicks = slowerInterval,
            UncappedAttackFrequencyMilliPerSecond = uncappedFrequency,
        };
    }

    public static int CalculateAftershockDamage(int heavyStrikeDamage, LegendaryRule? rule) =>
        rule is null
            ? 0
            : checked((int)((long)heavyStrikeDamage * rule.AftershockDamageMultiplierBasisPoints / 10_000));

    private static int DivideRoundUp(int numerator, int denominator) =>
        checked((numerator + denominator - 1) / denominator);
}
