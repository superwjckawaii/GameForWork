using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Skills;

namespace GameForWork.Core.Campaign.Combat;

public sealed record CombatPreview(
    CalculatedValue AverageHitDamage,
    CalculatedValue AttacksPerSecondMilli,
    CalculatedValue HitChanceBasisPoints,
    CalculatedValue CriticalChanceBasisPoints,
    CalculatedValue EffectiveLife);

public static class CombatPreviewRules
{
    public static CombatPreview Calculate(TeamBuild build, SkillConfiguration configuration)
    {
        OffenseBreakdown offense = BuildSummaryRules.CalculateOffense(build, configuration);
        DefenseBreakdown defense = BuildSummaryRules.CalculateDefense(GameForWork.Core.Combat.AuraCombatProfile.Resolve(build).Build);
        var hitTrace = new FormulaTraceBuilder();
        hitTrace.Add("基础伤害区间", $"{offense.BaseMinimumDamage}～{offense.BaseMaximumDamage}",
            (int)(((long)offense.BaseMinimumDamage + offense.BaseMaximumDamage) / 2));
        hitTrace.Add("伤害增加总和", $"转换分支加权提高：{offense.EffectiveIncreaseBasisPoints / 100.0}%", offense.EffectiveIncreaseBasisPoints);
        hitTrace.Add("直接击中", "共用实战伤害包；目标护甲25、抗性0，不含暴击和命中概率", offense.AverageHitDamage);
        int taken = Math.Max(1, 10_000 - defense.PhysicalDamageReductionBasisPoints);
        int effectiveLife = (int)Math.Clamp(((long)defense.MaximumLife + defense.MaximumShield) * 10_000 / taken, 0, int.MaxValue);
        return new(hitTrace.Build(offense.AverageHitDamage),
            CalculatedValue.Single("预计动作频率（千分之一/秒）", "共用技能动作速度与冷却", offense.FrequencyMilliPerSecond),
            CalculatedValue.Single("预计命中率", "目标闪避20", offense.HitChanceBasisPoints),
            CalculatedValue.Single("预计暴击率", "共用技能来源与辅助暴击", offense.CriticalChanceBasisPoints),
            CalculatedValue.Single("有效生命", $"({defense.MaximumLife} + {defense.MaximumShield}) × 10000 / {taken}", effectiveLife));
    }
}
