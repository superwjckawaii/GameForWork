using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Scenes;
using GameForWork.Core.Content;
using GameForWork.Core.SkillCatalog;
using GameForWork.Core.Archetypes;

namespace GameForWork.Core.Skills;

public sealed record SkillCombatStat(string Skill, int Damage, int Uses, int DamageBasisPoints);
public sealed record SupportCombatStat(string Support, int Triggers, int EstimatedDamageContribution);
public sealed record DamageSourceStat(string Source, int Damage, int DamageBasisPoints);

public sealed record CombatReport(
    string StableId,
    string Context,
    BattleOutcome Outcome,
    long DurationMilliseconds,
    int DamageDealt,
    int DamageTaken,
    IReadOnlyList<SkillCombatStat> Skills,
    IReadOnlyList<SupportCombatStat> Supports,
    IReadOnlyList<DamageSourceStat> DamageSources,
    int WarCryCoverageBasisPoints,
    int BannerCoverageBasisPoints,
    int ResourceFailureCount,
    int FlaskUses,
    int FlaskRecovery,
    int ShieldCoverageBasisPoints,
    IReadOnlyList<string> LastFiveSeconds,
    string TimeoutReason,
    bool Offline = false,
    DeathReport? DeathReport = null,
    int FlaskChargesGained = 0);

public static class CombatReportBuilder
{
    private static readonly IReadOnlyDictionary<SceneEventKind, string> SkillNames =
        new Dictionary<SceneEventKind, string>
        {
            [SceneEventKind.HeavyStrike] = "重击",
            [SceneEventKind.Aftershock] = "余震",
            [SceneEventKind.EarthCleave] = "裂地横扫",
            [SceneEventKind.SpiritBlade] = "幽魂飞刃",
            [SceneEventKind.Chain] = "追加连锁",
            [SceneEventKind.SeismicCharge] = "震地冲锋",
            [SceneEventKind.BloodTideSpin] = "血潮旋斩",
            [SceneEventKind.AshJavelin] = "灰烬投枪",
            [SceneEventKind.EmberNova] = "余烬新星",
            [SceneEventKind.StormBrand] = "风暴烙印",
            [SceneEventKind.Bleed] = "流血",
            [SceneEventKind.Ascendancy] = "升华效果",
        };

    public static CombatReport Build(SceneTimeline timeline, string context, bool offline = false)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        SceneEvent[] outgoing = timeline.Events.Where(IsOutgoingDamage).ToArray();
        int dealt = outgoing.Sum(item => Math.Max(0, item.Value));
        SkillCombatStat[] skills = outgoing.GroupBy(SkillName)
            .Select(group => new SkillCombatStat(group.Key, group.Sum(item => Math.Max(0, item.Value)), group.Count(),
                BasisPoints(group.Sum(item => Math.Max(0, item.Value)), dealt)))
            .OrderByDescending(item => item.Damage).ThenBy(item => item.Skill, StringComparer.Ordinal).ToArray();
        SupportCombatStat[] supports = BuildSupportStats(outgoing);

        SceneEvent[] incoming = timeline.Events.Where(IsIncomingDamage).ToArray();
        int taken = incoming.Sum(item => Math.Max(0, item.Value));
        DamageSourceStat[] sources = incoming.GroupBy(SourceName)
            .Select(group => new DamageSourceStat(group.Key, group.Sum(item => Math.Max(0, item.Value)),
                BasisPoints(group.Sum(item => Math.Max(0, item.Value)), taken)))
            .OrderByDescending(item => item.Damage).ThenBy(item => item.Source, StringComparer.Ordinal).ToArray();

        int warCries = timeline.Events.Count(item => item.Kind == SceneEventKind.WarCry);
        int warCryCoverage = timeline.DurationMilliseconds <= 0 ? 0 :
            (int)Math.Min(10_000, warCries * 4_000L * 10_000 / timeline.DurationMilliseconds);
        SceneEvent? banner = timeline.Events.FirstOrDefault(item => item.Kind == SceneEventKind.Banner);
        int bannerCoverage = banner is null || timeline.DurationMilliseconds <= 0 ? 0 :
            (int)Math.Clamp((timeline.DurationMilliseconds - banner.AtMilliseconds) * 10_000 / timeline.DurationMilliseconds, 0, 10_000);
        int failures = timeline.Events.Count(item => item.Kind == SceneEventKind.SkillFailed);
        SceneEvent[] flasks = timeline.Events.Where(item => item.Kind == SceneEventKind.Flask).ToArray();
        int flaskCharges = timeline.Events.Where(item => item.Kind == SceneEventKind.FlaskCharge).Sum(item => Math.Max(0, item.Value));
        int shieldCoverage = timeline.Events.Count == 0 ? 0 :
            timeline.Events.Count(item => item.HeroShield > 0) * 10_000 / timeline.Events.Count;
        long cutoff = Math.Max(0, timeline.DurationMilliseconds - 5_000);
        string[] last = timeline.Events.Where(item => item.AtMilliseconds >= cutoff)
            .TakeLast(40)
            .Select(item => $"{item.AtMilliseconds / 1_000.0:0.00}s {EventName(item.Kind)}" +
                            (item.Value == 0 ? string.Empty : $" {item.Value}"))
            .ToArray();
        return new CombatReport(
            timeline.FinalHash,
            context,
            timeline.Outcome,
            timeline.DurationMilliseconds,
            dealt,
            taken,
            skills,
            supports,
            sources,
            warCryCoverage,
            bannerCoverage,
            failures,
            flasks.Length,
            flasks.Sum(item => Math.Max(0, item.Value)),
            shieldCoverage,
            last,
            TimeoutReason(timeline, dealt, failures),
            offline,
            DeathReports.Build(timeline),
            flaskCharges);
    }

    private static SupportCombatStat[] BuildSupportStats(IEnumerable<SceneEvent> outgoing)
    {
        var triggers = new Dictionary<SkillSupport, (int Count, int Contribution)>();
        foreach (SceneEvent item in outgoing)
        {
            string marker = item.Detail.Split('|').FirstOrDefault(part => part.StartsWith("supports:", StringComparison.Ordinal)) ?? string.Empty;
            if (marker.Length <= "supports:".Length ||
                !ulong.TryParse(marker.AsSpan("supports:".Length), out ulong raw)) continue;
            SkillSupport flags = (SkillSupport)raw;
            foreach (SkillSupport support in Enum.GetValues<SkillSupport>().Where(flag => flag != SkillSupport.None && flags.HasFlag(flag)))
            {
                (int count, int contribution) = triggers.GetValueOrDefault(support);
                triggers[support] = (count + 1, contribution + EstimatedContribution(support, item.Value));
            }
        }
        return triggers.Select(pair => new SupportCombatStat(SupportName(pair.Key), pair.Value.Count, pair.Value.Contribution))
            .OrderByDescending(item => item.Triggers).ThenBy(item => item.Support, StringComparer.Ordinal).ToArray();
    }

    private static int EstimatedContribution(SkillSupport support, int finalDamage)
    {
        int multiplier = support switch
        {
            SkillSupport.IncreasedArea => 9_000,
            SkillSupport.LifeCost => 13_000,
            SkillSupport.Brutality => 13_500,
            SkillSupport.MultipleProjectiles => 8_000,
            _ => 10_000,
        };
        return multiplier == 10_000 ? 0 : finalDamage - (int)(finalDamage * 10_000L / multiplier);
    }

    private static string SupportName(SkillSupport support) =>
        CoreSkillDefinitions.Supports.FirstOrDefault(item => item.Support == support)?.DisplayName ?? support.ToString();

    private static bool IsOutgoingDamage(SceneEvent item)
    {
        if (item.Kind is not SceneEventKind.SkillEffect and not SceneEventKind.Ailment &&
            !SkillNames.ContainsKey(item.Kind) || item.Value <= 0) return false;
        if (item.Kind != SceneEventKind.Bleed) return true;
        string[] detail = item.Detail.Split('|');
        return detail.Length < 2 || detail[1] != "hero";
    }

    private static bool IsIncomingDamage(SceneEvent item)
    {
        if (item.Kind == SceneEventKind.EnemyAttack) return item.Value > 0;
        if (item.Kind != SceneEventKind.Bleed || item.Value <= 0) return false;
        string[] detail = item.Detail.Split('|');
        return detail.Length >= 2 && detail[1] == "hero" || item.Detail == "hero";
    }

    private static string SourceName(SceneEvent item)
    {
        string source = item.Detail.Split('|').FirstOrDefault() ?? string.Empty;
        if (item.Kind == SceneEventKind.Bleed) return "敌方流血";
        return string.IsNullOrWhiteSpace(source) ? "敌人攻击" : source;
    }

    private static int BasisPoints(int value, int total) => total <= 0 ? 0 : (int)Math.Min(10_000, value * 10_000L / total);

    private static string SkillName(SceneEvent item)
    {
        if (SkillNames.TryGetValue(item.Kind, out string? name)) return name;
        string marker = item.Detail.Split('|').FirstOrDefault(part => part.StartsWith("skill:", StringComparison.Ordinal)) ?? string.Empty;
        if (marker.Length > "skill:".Length)
        {
            string stableId = marker["skill:".Length..];
            return ArchetypeSkillDefinitions.TryActiveForSkill(stableId, out ArchetypeSkillDefinition? archetypes)
                ? archetypes!.Combat.DisplayName
                : CoreSkillDefinitions.Active.FirstOrDefault(active => active.SkillId == stableId)?.DisplayName ?? stableId;
        }
        string dot = item.Detail.Split('|').FirstOrDefault(part => part.StartsWith("dot:", StringComparison.Ordinal)) ?? string.Empty;
        return dot.Length > 4 ? $"持续伤害·{dot[4..]}" : "异常伤害";
    }

    private static string TimeoutReason(SceneTimeline timeline, int damage, int failures)
    {
        if (timeline.Outcome != BattleOutcome.Timeout) return string.Empty;
        if (failures > 0) return "资源不足导致技能停转";
        int moves = timeline.Events.Count(item => item.Kind == SceneEventKind.UnitMoved);
        int hits = timeline.Events.Count(IsOutgoingDamage);
        if (moves > Math.Max(6, hits * 3)) return "接敌时间过长或攻击范围不足";
        return damage <= 0 ? "没有形成有效伤害" : "伤害不足，未在演算上限内清场";
    }

    private static string EventName(SceneEventKind kind) => kind switch
    {
        SceneEventKind.EnemyAttack => "受到攻击",
        SceneEventKind.EnemyDefeated => "敌人倒下",
        SceneEventKind.UnitMoved => "移动",
        SceneEventKind.SkillFailed => "技能资源失败",
        _ => SkillNames.GetValueOrDefault(kind, kind.ToString()),
    };
}
