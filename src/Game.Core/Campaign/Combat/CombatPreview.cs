using GameForWork.Core.Builds;

namespace GameForWork.Core.Campaign.Combat;

public sealed record CombatPreview(
    CalculatedValue AverageHitDamage,
    CalculatedValue AttacksPerSecondMilli,
    CalculatedValue HitChanceBasisPoints,
    CalculatedValue CriticalChanceBasisPoints,
    CalculatedValue ExpectedBleedDamagePerSecond,
    CalculatedValue EffectiveLife,
    CalculatedValue ArmorReductionAgainstMinimumHit,
    CalculatedValue ArmorReductionAgainstMaximumHit,
    CalculatedValue ShieldRecoveryPerSecond);

public static class CombatPreviewRules
{
    public static CombatPreview Calculate(
        CharacterSheet character,
        WeaponProfile weapon,
        SkillUseProfile heavyStrike,
        int accuracy,
        int targetEvasion,
        int targetArmor,
        int representativeIncomingPhysicalHit,
        int addedPhysicalDamage = 0,
        int increasedDamageBasisPoints = 0,
        int increasedCriticalChanceBasisPoints = 0,
        int increasedBleedChanceBasisPoints = 0)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(weapon);
        ArgumentNullException.ThrowIfNull(heavyStrike);

        int minimumDamage = checked(weapon.MinimumPhysicalDamage + addedPhysicalDamage);
        int maximumDamage = checked(weapon.MaximumPhysicalDamage + addedPhysicalDamage);
        int averageWeaponDamage = checked((minimumDamage + maximumDamage) / 2);
        int physiqueIncrease = character.AttackDamageIncreaseFromPhysique().Value;
        var hitTrace = new FormulaTraceBuilder();
        hitTrace.Add(
            "武器平均物理伤害",
            $"({minimumDamage} + {maximumDamage}) / 2",
            averageWeaponDamage);
        int combinedIncrease = checked(physiqueIncrease + increasedDamageBasisPoints);
        int averageHit = checked((int)((long)averageWeaponDamage * (10_000 + combinedIncrease) / 10_000));
        hitTrace.Add("伤害增加总和", $"{averageWeaponDamage} × (10000 + {combinedIncrease}) / 10000", averageHit);
        foreach (int multiplier in heavyStrike.MoreDamageMultipliersBasisPoints)
        {
            averageHit = checked((int)((long)averageHit * multiplier / 10_000));
            hitTrace.Add("技能总增/总降", $"previous × {multiplier} / 10000", averageHit);
        }

        CalculatedValue armorReduction = DamageRules.ArmorReduction(targetArmor, averageHit);
        averageHit = Math.Max(1, checked((int)((long)averageHit * (10_000 - armorReduction.Value) / 10_000)));
        hitTrace.Add("目标护甲", $"previous × (10000 - {armorReduction.Value}) / 10000", averageHit);

        int attacksPerSecondMilli = heavyStrike.AttackFrequencyMilliPerSecond;
        CalculatedValue hitChance = DamageRules.HitChance(accuracy, targetEvasion, false);
        int criticalChance = Math.Clamp(
            checked((int)((long)weapon.CriticalChanceBasisPoints * (10_000 + increasedCriticalChanceBasisPoints) / 10_000)),
            0,
            10_000);
        int bleedChance = Math.Clamp(
            checked(heavyStrike.BleedChanceBasisPoints + increasedBleedChanceBasisPoints),
            0,
            10_000);
        int expectedBleedPerSecond = checked((int)(
            (long)averageHit * 7_000 / 10_000 *
            bleedChance / 10_000 *
            hitChance.Value / 10_000 *
            attacksPerSecondMilli / 1_000 / 5));

        int maximumLife = character.MaximumLife().Value;
        int maximumShield = character.MaximumShield().Value;
        CalculatedValue incomingReduction = DamageRules.ArmorReduction(character.Armor().Value, representativeIncomingPhysicalHit);
        int damageTakenBasisPoints = Math.Max(1, 10_000 - incomingReduction.Value);
        int effectiveLife = checked((int)((long)(maximumLife + maximumShield) * 10_000 / damageTakenBasisPoints));

        return new CombatPreview(
            hitTrace.Build(averageHit),
            CalculatedValue.Single(
                "预计攻击频率（千分之一/秒）",
                $"min({heavyStrike.UncappedAttackFrequencyMilliPerSecond}, {CombatRules.MaximumAttackFrequencyMilliPerSecond})",
                attacksPerSecondMilli),
            hitChance,
            CalculatedValue.Single("预计暴击率", weapon.CriticalChanceBasisPoints.ToString(System.Globalization.CultureInfo.InvariantCulture), criticalChance),
            CalculatedValue.Single(
                "预计流血每秒伤害",
                "平均命中 × 70% × 流血概率 × 命中率 × 攻击频率 / 5秒",
                expectedBleedPerSecond),
            CalculatedValue.Single(
                "有效生命",
                $"({maximumLife} + {maximumShield}) × 10000 / {damageTakenBasisPoints}",
                effectiveLife),
            DamageRules.ArmorReduction(targetArmor, minimumDamage),
            DamageRules.ArmorReduction(targetArmor, maximumDamage),
            character.ShieldRecoveryPerSecond());
    }
}
