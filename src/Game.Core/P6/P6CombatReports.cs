using GameForWork.Core.P1.Combat;
using GameForWork.Core.P3;
using GameForWork.Core.P14;

namespace GameForWork.Core.P6;

public sealed record P6SkillCombatStat(string Skill, int Damage, int Uses, int DamageBasisPoints);
public sealed record P6SupportCombatStat(string Support, int Triggers, int EstimatedDamageContribution);
public sealed record P6DamageSourceStat(string Source, int Damage, int DamageBasisPoints);

public sealed record P6CombatReport(
    string StableId,
    string Context,
    P1BattleOutcome Outcome,
    long DurationMilliseconds,
    int DamageDealt,
    int DamageTaken,
    IReadOnlyList<P6SkillCombatStat> Skills,
    IReadOnlyList<P6SupportCombatStat> Supports,
    IReadOnlyList<P6DamageSourceStat> DamageSources,
    int WarCryCoverageBasisPoints,
    int BannerCoverageBasisPoints,
    int ResourceFailureCount,
    int FlaskUses,
    int FlaskRecovery,
    int ShieldCoverageBasisPoints,
    IReadOnlyList<string> LastFiveSeconds,
    string TimeoutReason,
    bool Offline = false,
    P14DeathReport? DeathReport = null);

public static class P6CombatReportBuilder
{
    private static readonly IReadOnlyDictionary<P3SceneEventKind, string> SkillNames =
        new Dictionary<P3SceneEventKind, string>
        {
            [P3SceneEventKind.HeavyStrike] = "重击",
            [P3SceneEventKind.Aftershock] = "余震",
            [P3SceneEventKind.EarthCleave] = "裂地横扫",
            [P3SceneEventKind.SpiritBlade] = "幽魂飞刃",
            [P3SceneEventKind.Chain] = "追加连锁",
            [P3SceneEventKind.SeismicCharge] = "震地冲锋",
            [P3SceneEventKind.BloodTideSpin] = "血潮旋斩",
            [P3SceneEventKind.AshJavelin] = "灰烬投枪",
            [P3SceneEventKind.EmberNova] = "余烬新星",
            [P3SceneEventKind.StormBrand] = "风暴烙印",
            [P3SceneEventKind.Bleed] = "流血",
        };

    public static P6CombatReport Build(P3SceneTimeline timeline, string context, bool offline = false)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        P3SceneEvent[] outgoing = timeline.Events.Where(IsOutgoingDamage).ToArray();
        int dealt = outgoing.Sum(item => Math.Max(0, item.Value));
        P6SkillCombatStat[] skills = outgoing.GroupBy(item => SkillNames[item.Kind])
            .Select(group => new P6SkillCombatStat(group.Key, group.Sum(item => Math.Max(0, item.Value)), group.Count(),
                BasisPoints(group.Sum(item => Math.Max(0, item.Value)), dealt)))
            .OrderByDescending(item => item.Damage).ThenBy(item => item.Skill, StringComparer.Ordinal).ToArray();
        P6SupportCombatStat[] supports = BuildSupportStats(outgoing);

        P3SceneEvent[] incoming = timeline.Events.Where(IsIncomingDamage).ToArray();
        int taken = incoming.Sum(item => Math.Max(0, item.Value));
        P6DamageSourceStat[] sources = incoming.GroupBy(SourceName)
            .Select(group => new P6DamageSourceStat(group.Key, group.Sum(item => Math.Max(0, item.Value)),
                BasisPoints(group.Sum(item => Math.Max(0, item.Value)), taken)))
            .OrderByDescending(item => item.Damage).ThenBy(item => item.Source, StringComparer.Ordinal).ToArray();

        int warCries = timeline.Events.Count(item => item.Kind == P3SceneEventKind.WarCry);
        int warCryCoverage = timeline.DurationMilliseconds <= 0 ? 0 :
            (int)Math.Min(10_000, warCries * 4_000L * 10_000 / timeline.DurationMilliseconds);
        P3SceneEvent? banner = timeline.Events.FirstOrDefault(item => item.Kind == P3SceneEventKind.Banner);
        int bannerCoverage = banner is null || timeline.DurationMilliseconds <= 0 ? 0 :
            (int)Math.Clamp((timeline.DurationMilliseconds - banner.AtMilliseconds) * 10_000 / timeline.DurationMilliseconds, 0, 10_000);
        int failures = timeline.Events.Count(item => item.Kind == P3SceneEventKind.SkillFailed);
        P3SceneEvent[] flasks = timeline.Events.Where(item => item.Kind == P3SceneEventKind.Flask).ToArray();
        int shieldCoverage = timeline.Events.Count == 0 ? 0 :
            timeline.Events.Count(item => item.HeroShield > 0) * 10_000 / timeline.Events.Count;
        long cutoff = Math.Max(0, timeline.DurationMilliseconds - 5_000);
        string[] last = timeline.Events.Where(item => item.AtMilliseconds >= cutoff)
            .TakeLast(40)
            .Select(item => $"{item.AtMilliseconds / 1_000.0:0.00}s {EventName(item.Kind)}" +
                            (item.Value == 0 ? string.Empty : $" {item.Value}"))
            .ToArray();
        return new P6CombatReport(
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
            P14DeathReports.Build(timeline));
    }

    private static P6SupportCombatStat[] BuildSupportStats(IEnumerable<P3SceneEvent> outgoing)
    {
        var triggers = new Dictionary<SkillSupport, (int Count, int Contribution)>();
        foreach (P3SceneEvent item in outgoing)
        {
            string marker = item.Detail.Split('|').FirstOrDefault(part => part.StartsWith("supports:", StringComparison.Ordinal)) ?? string.Empty;
            if (marker.Length <= "supports:".Length ||
                !int.TryParse(marker.AsSpan("supports:".Length), out int raw)) continue;
            SkillSupport flags = (SkillSupport)raw;
            foreach (SkillSupport support in Enum.GetValues<SkillSupport>().Where(flag => flag != SkillSupport.None && flags.HasFlag(flag)))
            {
                (int count, int contribution) = triggers.GetValueOrDefault(support);
                triggers[support] = (count + 1, contribution + EstimatedContribution(support, item.Value));
            }
        }
        return triggers.Select(pair => new P6SupportCombatStat(SupportName(pair.Key), pair.Value.Count, pair.Value.Contribution))
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

    private static string SupportName(SkillSupport support) => support switch
    {
        SkillSupport.IncreasedArea => "扩大范围",
        SkillSupport.AttackSpeed => "攻击速度",
        SkillSupport.Bleed => "流血",
        SkillSupport.LifeCost => "生命消耗",
        SkillSupport.Chain => "追加连锁",
        SkillSupport.Brutality => "残暴",
        SkillSupport.MultipleProjectiles => "多重投射",
        SkillSupport.FasterProjectiles => "极速投射",
        SkillSupport.UrgentWarCry => "急促战吼",
        SkillSupport.LifeLeech => "血之汲取",
        SkillSupport.Execution => "处决",
        SkillSupport.SpellEcho => "法术回响",
        SkillSupport.ElementalFocus => "元素集中",
        SkillSupport.AddedFire => "附加火焰",
        SkillSupport.AddedCold => "附加冰霜",
        SkillSupport.AddedLightning => "附加闪电",
        SkillSupport.CriticalStrikes => "提高暴击",
        SkillSupport.ConcentratedEffect => "集中效应",
        _ => support.ToString(),
    };

    private static bool IsOutgoingDamage(P3SceneEvent item)
    {
        if (!SkillNames.ContainsKey(item.Kind) || item.Value <= 0) return false;
        if (item.Kind != P3SceneEventKind.Bleed) return true;
        string[] detail = item.Detail.Split('|');
        return detail.Length < 2 || detail[1] != "hero";
    }

    private static bool IsIncomingDamage(P3SceneEvent item)
    {
        if (item.Kind == P3SceneEventKind.EnemyAttack) return item.Value > 0;
        if (item.Kind != P3SceneEventKind.Bleed || item.Value <= 0) return false;
        string[] detail = item.Detail.Split('|');
        return detail.Length >= 2 && detail[1] == "hero" || item.Detail == "hero";
    }

    private static string SourceName(P3SceneEvent item)
    {
        string source = item.Detail.Split('|').FirstOrDefault() ?? string.Empty;
        if (item.Kind == P3SceneEventKind.Bleed) return "敌方流血";
        return string.IsNullOrWhiteSpace(source) ? "敌人攻击" : source;
    }

    private static int BasisPoints(int value, int total) => total <= 0 ? 0 : (int)Math.Min(10_000, value * 10_000L / total);

    private static string TimeoutReason(P3SceneTimeline timeline, int damage, int failures)
    {
        if (timeline.Outcome != P1BattleOutcome.Timeout) return string.Empty;
        if (failures > 0) return "资源不足导致技能停转";
        int moves = timeline.Events.Count(item => item.Kind == P3SceneEventKind.UnitMoved);
        int hits = timeline.Events.Count(IsOutgoingDamage);
        if (moves > Math.Max(6, hits * 3)) return "接敌时间过长或攻击范围不足";
        return damage <= 0 ? "没有形成有效伤害" : "伤害不足，未在演算上限内清场";
    }

    private static string EventName(P3SceneEventKind kind) => kind switch
    {
        P3SceneEventKind.EnemyAttack => "受到攻击",
        P3SceneEventKind.EnemyDefeated => "敌人倒下",
        P3SceneEventKind.UnitMoved => "移动",
        P3SceneEventKind.SkillFailed => "技能资源失败",
        _ => SkillNames.GetValueOrDefault(kind, kind.ToString()),
    };
}
