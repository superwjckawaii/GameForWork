using GameForWork.Core.P1;
using GameForWork.Core.P1.Combat;
using GameForWork.Core.P2;

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
        SkillLinkConfiguration? main = session.Management.SkillLinks
            .Where(link => !string.IsNullOrEmpty(link.ChainId) && !string.IsNullOrEmpty(link.ActiveStoneInstanceId))
            .OrderBy(link => link.Priority).FirstOrDefault();
        SkillStoneInstance? active = main is null ? null : session.Management.SkillStones
            .FirstOrDefault(stone => stone.InstanceId == main.ActiveStoneInstanceId);
        int links = main?.SocketStoneInstanceIds?.Count(id => !string.IsNullOrEmpty(id)) ??
                    (main is null ? 0 : 1 + main.SupportStoneInstanceIds.Count);
        int averageWeapon = (session.World.Hero.Build.Weapon.MinimumPhysicalDamage +
                             session.World.Hero.Build.Weapon.MaximumPhysicalDamage) / 2;
        int supportMore = Math.Max(10_000, 10_000 + (links - 1) * 1_200);
        int single = checked(averageWeapon * supportMore / 10_000 *
            session.World.Hero.Build.Weapon.AttacksPerSecondMilli / 1_000);
        int clear = active?.Definition.Tags.HasFlag(SkillTag.Area) == true ? single * 2 :
            active?.Definition.Tags.HasFlag(SkillTag.Projectile) == true ? single * 3 / 2 : single;
        CharacterSheet sheet = session.World.Hero.Build.Sheet;
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
        return new P6BuildSummary(
            active?.Definition.DisplayName ?? "无",
            links,
            single,
            clear,
            sheet.MaximumLife().Value + sheet.MaximumShield().Value,
            sheet.Armor().Value,
            sheet.Evasion().Value,
            sheet.MaximumShield().Value,
            "生命药剂 + 命中汲取（若已连接）",
            session.World.Hero.Build.UseWarCry ? "战吼按技能 AI 循环" : "无战吼覆盖",
            issues,
            "估算假设：同级普通敌人、持续命中、不计条件性处决/Boss阶段；不是木桩模拟。 ");
    }
}
