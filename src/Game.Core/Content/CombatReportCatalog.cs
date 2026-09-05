using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Scenes;
using GameForWork.Core.Maps;

namespace GameForWork.Core.Content;

public sealed record PreflightReport(
    string EncounterName, IReadOnlyList<string> DamageTypes, IReadOnlyList<string> Ailments,
    IReadOnlyList<string> Requirements, string EnrageCondition, int RiskScore);

public static class Preflight
{
    public static PreflightReport ForMap(MapItem map, BossDefinition boss)
    {
        string[] damageTypes = boss.Skills.Select(skill => skill.DamageType).Distinct(StringComparer.Ordinal).ToArray();
        string[] ailments = map.EffectiveAffixes.Select(affix => affix.Kind switch
        {
            MapAffixKind.ReducedRecovery => "枯竭",
            MapAffixKind.MonsterSpeed => "迅猎",
            MapAffixKind.ElementalShell => "元素甲壳",
            MapAffixKind.VoidShroud => "虚界遮蔽",
            _ => string.Empty,
        }).Where(text => text.Length > 0).Distinct(StringComparer.Ordinal).ToArray();
        int risk = Math.Clamp(map.Tier * 4 + map.MonsterQuantityBasisPoints / 200 + (map.IsCorrupted ? 20 : 0), 0, 100);
        string[] requirements = map.Tier switch
        {
            <= 10 => ["基础生命与主抗性", "至少一项稳定恢复"],
            <= 16 => ["完成装备加工", "法术压制或等效防御", "Boss 单体技能组"],
            _ => ["完整攻防构筑", "异常处理", "应对该阶级特殊规则"],
        };
        return new(boss.DisplayName, damageTypes, ailments, requirements,
            "无战斗限时；耗时仅用于效率报告", risk);
    }
}

public sealed record DeathEvent(long BeforeDeathMilliseconds, string Source, string Skill, int Damage, string Detail);
public sealed record DeathReport(
    string FatalSkill, int FatalDamage, string RawDamageType, IReadOnlyList<string> DefensiveLayers,
    IReadOnlyList<string> Ailments, bool Avoidable, IReadOnlyList<DeathEvent> LastFiveSeconds);

public static class DeathReports
{
    public static DeathReport? Build(SceneTimeline timeline)
    {
        if (timeline.Outcome == BattleOutcome.HeroVictory) return null;
        long end = timeline.Events.LastOrDefault()?.AtMilliseconds ?? timeline.DurationMilliseconds;
        SceneEvent[] recent = timeline.Events.Where(item => item.AtMilliseconds >= end - 5_000)
            .OrderBy(item => item.AtMilliseconds).ToArray();
        SceneEvent? fatal = recent.LastOrDefault(item => item.Kind is SceneEventKind.EnemyAttack or SceneEventKind.Bleed && item.Value > 0);
        string detail = fatal?.Detail ?? "unknown|unknown|未记录技能";
        string[] parts = detail.Split('|');
        string skill = parts.LastOrDefault(text => !string.IsNullOrWhiteSpace(text)) ?? "未知攻击";
        return new(skill, fatal?.Value ?? 0,
            skill.Contains("Caster", StringComparison.OrdinalIgnoreCase) ? "法术" : "物理",
            ["护甲", "闪避", "护盾", "法术压制"],
            recent.Where(item => item.Kind == SceneEventKind.Bleed).Any() ? ["流血"] : [],
            fatal is not null,
            recent.Select(item => new DeathEvent(end - item.AtMilliseconds,
                item.Detail.Split('|').FirstOrDefault() ?? "unknown", item.Kind.ToString(), item.Value, item.Detail)).ToArray());
    }
}
