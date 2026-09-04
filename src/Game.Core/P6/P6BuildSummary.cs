using GameForWork.Core.P1;
using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.Progression;
using GameForWork.Core.P2;
using GameForWork.Core.P17;
using GameForWork.Core.P30;

namespace GameForWork.Core.P6;

public sealed record P6BuildSummary(
    string MainSkill,
    int MainSkillLinks,
    int EstimatedSingleTargetDamage,
    int EstimatedClearDamage,
    int EffectiveLife,
    int Armor,
    int Evasion,
    int Shield,
    string Recovery,
    string BuffCoverage,
    IReadOnlyList<string> Issues,
    string Assumptions);

public static class P6BuildSummaryRules
{
    public static P6BuildSummary Calculate(P1GameSession session)
    {
        SkillConfiguration? configuration = session.GetPreviewSkill();
        SkillLinkConfiguration? main = configuration is null ? null : session.Management.SkillLinks
            .FirstOrDefault(link => link.ActiveStoneInstanceId == configuration.StoneInstanceId);
        SkillStoneInstance? active = configuration is null ? null : session.Management.SkillStones
            .FirstOrDefault(stone => stone.InstanceId == configuration.StoneInstanceId);
        int links = main?.SocketStoneInstanceIds?.Count(id => !string.IsNullOrEmpty(id)) ??
                    (main is null ? 0 : 1 + main.SupportStoneInstanceIds.Count);
        int single = configuration is null ? 0 : EstimateDps(session, configuration, singleTarget: true);
        int clear = configuration is null ? 0 : EstimateDps(session, configuration, singleTarget: false);
        CharacterSheet sheet = session.World.Hero.Build.Sheet;
        CombatPreview defense = session.GetCombatPreview();
        var issues = BuildIssues(session, active);
        return new P6BuildSummary(
            active?.Definition.DisplayName ?? "无",
            links,
            single,
            clear,
            defense.EffectiveLife.Value,
            sheet.Armor().Value,
            sheet.Evasion().Value,
            sheet.MaximumShield().Value,
            "生命药剂 + 命中汲取（若已连接）",
            session.World.Hero.Build.UseWarCry ? "战吼按技能 AI 循环" : "无战吼覆盖",
            issues,
            "估算假设：使用与实战相同的主动技能等级、P24/P30 辅助解析、资源倍率、武器/法术基础伤害、命中与暴击入口；单体按满生命目标，清图额外计入技能形状、投射物和范围覆盖。条件触发与走位仍以战斗报告为准。 ");
    }

    private static int EstimateDps(P1GameSession session, SkillConfiguration configuration, bool singleTarget)
    {
        P6ResolvedSkill skill = P6CombatSkillRules.Resolve(configuration,
            session.World.Hero.Build.Sheet.MaximumLife().Value, session.World.Hero.Build.PassiveProfile);
        SkillTag tags = P1Skills.Get(configuration.SkillId).Tags;
        int raw = P6CombatSkillRules.BaseDamage(skill, tags, session.World.Hero.Build.Weapon,
            session.World.Hero.Build.AddedPhysicalDamage);
        int hit = P6CombatSkillRules.ScaleOffensiveDamage(raw, skill, configuration, session.World.Hero.Build,
            tags, targetLife: 100_000, targetMaximumLife: 100_000);
        if (skill.DamageType == P17DamageType.Physical)
        {
            int reduction = DamageRules.ArmorReduction(25, Math.Max(1, hit)).Value;
            hit = checked(hit * (10_000 - reduction) / 10_000);
        }
        int accuracy = session.World.Hero.Build.Sheet.Accuracy(session.World.Hero.Build.FlatAccuracy).Value;
        int hitChance = session.World.Hero.Build.AlwaysHit || tags.HasFlag(SkillTag.Spell)
            ? 10_000 : DamageRules.HitChance(accuracy, 20, false).Value;
        int criticalChance = session.World.Hero.Build.CannotCrit ||
            P30MasteryRuntime.CannotCrit(session.World.Hero.Build.PassiveProfile ?? P205PassiveModifiers.Empty) ? 0 : P30CombatRules.CriticalChance(
            session.World.Hero.Build.Weapon.CriticalChanceBasisPoints,
            session.World.Hero.Build.IncreasedCriticalChanceBasisPoints);
        long expectedCriticalMultiplier = 10_000L +
            (long)criticalChance * (session.World.Hero.Build.CriticalMultiplierBasisPoints - 10_000) / 10_000;
        long expectedHit = (long)hit * hitChance / 10_000 * expectedCriticalMultiplier / 10_000;
        int actionSpeed = P30MasteryRuntime.ActionSpeedMultiplier(
            session.World.Hero.Build.PassiveProfile ?? P205PassiveModifiers.Empty, tags, session.World.Hero.Build.Weapon);
        int actionTicks = Math.Max(1, Math.Max(skill.CastTimeTicks * 10_000 / Math.Max(1, actionSpeed), skill.CooldownTicks));
        long dps = expectedHit * 20 / actionTicks;
        if (singleTarget && skill.Returns) dps *= 2;
        if (!singleTarget)
        {
            int coverage = skill.Shape switch
            {
                P17SkillShape.Circle or P17SkillShape.Cone or P17SkillShape.GroundArea => 3,
                P17SkillShape.Projectile or P17SkillShape.Chain => Math.Clamp(skill.ProjectileCount + skill.MaximumChains, 1, 5),
                _ => 1,
            };
            dps *= coverage;
        }
        return checked((int)Math.Clamp(dps, 0, int.MaxValue));
    }

    private static IReadOnlyList<string> BuildIssues(P1GameSession session, SkillStoneInstance? active)
    {
        var issues = new List<string>();
        foreach (SkillLinkConfiguration link in session.Management.SkillLinks.Where(link => !string.IsNullOrEmpty(link.ChainId)))
        {
            if (string.IsNullOrEmpty(link.ActiveStoneInstanceId) && link.SupportStoneInstanceIds.Count > 0)
            {
                issues.Add($"{link.ChainId} 等待主动技能");
            }
            SkillStoneInstance? groupActive = string.IsNullOrEmpty(link.ActiveStoneInstanceId) ? null :
                session.Management.SkillStones.FirstOrDefault(stone => stone.InstanceId == link.ActiveStoneInstanceId);
            if (groupActive is not null)
            {
                foreach (string supportId in link.SupportStoneInstanceIds)
                {
                    SkillStoneInstance? support = session.Management.SkillStones.FirstOrDefault(stone => stone.InstanceId == supportId);
                    if (support is not null && !P6SkillCompatibility.Check(groupActive.Definition, support.Definition).Compatible)
                    {
                        issues.Add($"{support.Definition.DisplayName} 不支持 {groupActive.Definition.DisplayName}");
                    }
                }
            }
        }
        if (active is null) issues.Add("没有有效主技能");
        return issues;
    }
}
