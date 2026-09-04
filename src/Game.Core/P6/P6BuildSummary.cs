using GameForWork.Core.P1;
using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.Progression;
using GameForWork.Core.P1.World;
using GameForWork.Core.P2;
using GameForWork.Core.P17;
using GameForWork.Core.P30;

namespace GameForWork.Core.P6;

public sealed record P6DefenseBreakdown(
    int MaximumLife,
    int MaximumShield,
    int MaximumMana,
    CharacterAttributes Attributes,
    int Armor,
    int PhysicalDamageReductionBasisPoints,
    int Evasion,
    int EvasionChanceBasisPoints,
    int FireResistanceBasisPoints,
    int EffectiveFireResistanceBasisPoints,
    int ColdResistanceBasisPoints,
    int EffectiveColdResistanceBasisPoints,
    int LightningResistanceBasisPoints,
    int EffectiveLightningResistanceBasisPoints,
    int VoidResistanceBasisPoints,
    int EffectiveVoidResistanceBasisPoints,
    int PhysicalBlockChanceBasisPoints,
    int EffectivePhysicalBlockChanceBasisPoints,
    int SpellBlockChanceBasisPoints,
    int EffectiveSpellBlockChanceBasisPoints,
    int SpellSuppressionBasisPoints,
    int EffectiveSpellSuppressionBasisPoints);

public sealed record P6OffenseBreakdown(
    int DamagePerSecond,
    int BaseMinimumDamage,
    int BaseMaximumDamage,
    int EffectiveIncreaseBasisPoints,
    int EffectiveMoreBasisPoints,
    bool IsSpell,
    int FrequencyMilliPerSecond,
    int Accuracy,
    int HitChanceBasisPoints,
    int CriticalChanceBasisPoints,
    int CriticalMultiplierBasisPoints)
{
    public static P6OffenseBreakdown Empty { get; } = new(0, 0, 0, 0, 0, false, 0, 0, 0, 0, 10_000);
}

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
    string Assumptions,
    P6DefenseBreakdown Defense,
    P6OffenseBreakdown Offense);

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
        P1TeamBuild build = session.World.Hero.Build;
        P6OffenseBreakdown offense = configuration is null
            ? P6OffenseBreakdown.Empty
            : CalculateOffense(build, configuration);
        int clear = configuration is null ? 0 : CalculateClearDps(offense.DamagePerSecond,
            P6CombatSkillRules.Resolve(configuration, build.Sheet.MaximumLife().Value, build.PassiveProfile));
        CharacterSheet sheet = build.Sheet;
        CombatPreview preview = session.GetCombatPreview();
        P6DefenseBreakdown defense = CalculateDefense(build);
        var issues = BuildIssues(session, active);
        return new P6BuildSummary(
            active?.Definition.DisplayName ?? "无",
            links,
            offense.DamagePerSecond,
            clear,
            preview.EffectiveLife.Value,
            defense.Armor,
            defense.Evasion,
            defense.MaximumShield,
            "生命药剂 + 命中汲取（若已连接）",
            build.UseWarCry ? "战吼按技能 AI 循环" : "无战吼覆盖",
            issues,
            "估算假设：DPS 使用与实战相同的主动技能、辅助、专精、伤害、动作速度、命中与暴击入口；单体按满生命目标。护甲减伤按角色等级对应的代表性物理击中估算，闪避率按同等级代表性敌人估算。条件触发与走位仍以战斗报告为准。",
            defense,
            offense);
    }

    private static P6OffenseBreakdown CalculateOffense(P1TeamBuild build, SkillConfiguration configuration)
    {
        P6ResolvedSkill skill = P6CombatSkillRules.Resolve(configuration,
            build.Sheet.MaximumLife().Value, build.PassiveProfile);
        SkillTag tags = P1Skills.Get(configuration.SkillId).Tags;
        int raw = P6CombatSkillRules.BaseDamage(skill, tags, build.Weapon, build.AddedPhysicalDamage);
        int hit = P6CombatSkillRules.ScaleOffensiveDamage(raw, skill, configuration, build,
            tags, targetLife: 100_000, targetMaximumLife: 100_000);
        if (skill.DamageType == P17DamageType.Physical)
        {
            int reduction = DamageRules.ArmorReduction(25, Math.Max(1, hit)).Value;
            hit = (int)Math.Clamp((long)hit * (10_000 - reduction) / 10_000, 0, int.MaxValue);
        }
        int accuracy = build.Sheet.Accuracy(build.FlatAccuracy).Value;
        int hitChance = build.AlwaysHit || tags.HasFlag(SkillTag.Spell)
            ? 10_000 : DamageRules.HitChance(accuracy, 20, false).Value;
        P205PassiveModifiers passive = build.PassiveProfile ?? P205PassiveModifiers.Empty;
        int criticalChance = build.CannotCrit || P30MasteryRuntime.CannotCrit(passive) ? 0 :
            P30CombatRules.CriticalChance(build.Weapon.CriticalChanceBasisPoints,
                build.IncreasedCriticalChanceBasisPoints);
        long expectedCriticalMultiplier = 10_000L +
            (long)criticalChance * (build.CriticalMultiplierBasisPoints - 10_000) / 10_000;
        long expectedHit = (long)hit * hitChance / 10_000 * expectedCriticalMultiplier / 10_000;
        int actionTicks = Math.Max(P6CombatSkillRules.ActionDelay(build, skill.CastTimeTicks, tags), skill.CooldownTicks);
        long dps = expectedHit * 20 / actionTicks;
        if (skill.Returns) dps *= 2;

        int baseMinimum = tags.HasFlag(SkillTag.Attack)
            ? ScaleToInt((long)build.Weapon.MinimumPhysicalDamage + build.AddedPhysicalDamage,
                skill.BaseDamageBasisPoints)
            : skill.BaseDamageBasisPoints;
        int baseMaximum = tags.HasFlag(SkillTag.Attack)
            ? ScaleToInt((long)build.Weapon.MaximumPhysicalDamage + build.AddedPhysicalDamage,
                skill.BaseDamageBasisPoints)
            : skill.BaseDamageBasisPoints;
        int increased = checked(build.IncreasedDamageBasisPoints + passive.DamageFor(tags) + configuration.Quality * 100);
        if (tags.HasFlag(SkillTag.Spell)) increased = checked(increased + build.IncreasedSpellDamageBasisPoints);
        int jewelMore = skill.Role == P17SkillRole.DamageOverTime ? build.MoreDamageOverTimeBasisPoints : 0;
        if (tags.HasFlag(SkillTag.Attack)) jewelMore += build.MoreAttackDamageBasisPoints;
        if (tags.HasFlag(SkillTag.Spell)) jewelMore += build.MoreSpellDamageBasisPoints;
        long more = Math.Max(0, 10_000L + passive.MoreDamageBasisPoints);
        more = ScaleMultiplier(more, 10_000L + jewelMore);
        more = ScaleMultiplier(more, P6CombatSkillRules.DamageMultiplier(skill, 100_000, 100_000));
        more = ScaleMultiplier(more,
            P30MasteryRuntime.OffensiveMultiplier(passive, tags, build.Weapon, 100_000, 100_000));
        return new P6OffenseBreakdown(
            checked((int)Math.Clamp(dps, 0, int.MaxValue)),
            Math.Max(1, baseMinimum),
            Math.Max(1, baseMaximum),
            increased,
            checked((int)Math.Clamp(more - 10_000, int.MinValue, int.MaxValue)),
            tags.HasFlag(SkillTag.Spell),
            checked(20_000 / actionTicks),
            accuracy,
            hitChance,
            criticalChance,
            build.CriticalMultiplierBasisPoints);
    }

    private static int CalculateClearDps(int singleTargetDps, P6ResolvedSkill skill)
    {
        int coverage = skill.Shape switch
        {
            P17SkillShape.Circle or P17SkillShape.Cone or P17SkillShape.GroundArea => 3,
            P17SkillShape.Projectile or P17SkillShape.Chain =>
                Math.Clamp(skill.ProjectileCount + skill.MaximumChains, 1, 5),
            _ => 1,
        };
        return checked((int)Math.Clamp((long)singleTargetDps * coverage / (skill.Returns ? 2 : 1), 0, int.MaxValue));
    }

    private static P6DefenseBreakdown CalculateDefense(P1TeamBuild build)
    {
        CharacterSheet sheet = build.Sheet;
        int armor = sheet.Armor().Value;
        int evasion = sheet.Evasion().Value;
        int representativeHit = Math.Max(10, sheet.Level * 10);
        int representativeAccuracy = Math.Max(20, sheet.Level * 2);
        int physicalBlock = build.BlockChanceBasisPoints;
        int spellBlock = checked(build.BlockChanceBasisPoints + sheet.SpellBlockChanceBasisPoints);
        P18.P18CombatProfile ascendancy = build.Ascendancy ?? new P18.P18CombatProfile(P18.P18Ascendancy.None, []);
        if (build.HasShield && ascendancy.Has(P18.P18NodeIds.BastionAttackBlockSmall)) physicalBlock += 800;
        if (build.HasShield && ascendancy.Has(P18.P18NodeIds.BastionAttackBlockCore)) physicalBlock += 1_200;
        if (build.HasShield && ascendancy.Has(P18.P18NodeIds.BastionSpellBlockSmall)) spellBlock += 800;
        if (build.HasShield && ascendancy.Has(P18.P18NodeIds.BastionSpellBlockCore))
            spellBlock += build.BlockChanceBasisPoints * 6 / 10;
        int physicalBlockMaximum = ascendancy.Has(P18.P18NodeIds.BastionAttackBlockCore)
            ? 8_000 : sheet.MaximumBlockChanceBasisPoints;
        return new P6DefenseBreakdown(
            sheet.MaximumLife().Value,
            sheet.MaximumShield().Value,
            sheet.MaximumMana().Value,
            sheet.Attributes,
            armor,
            DamageRules.ArmorReduction(armor, representativeHit).Value,
            evasion,
            10_000 - DamageRules.HitChance(representativeAccuracy, evasion, false).Value,
            sheet.FireResistanceBasisPoints,
            P30CombatRules.EffectiveResistance(sheet.FireResistanceBasisPoints, sheet.MaximumElementalResistanceBasisPoints),
            sheet.ColdResistanceBasisPoints,
            P30CombatRules.EffectiveResistance(sheet.ColdResistanceBasisPoints, sheet.MaximumElementalResistanceBasisPoints),
            sheet.LightningResistanceBasisPoints,
            P30CombatRules.EffectiveResistance(sheet.LightningResistanceBasisPoints, sheet.MaximumElementalResistanceBasisPoints),
            sheet.VoidResistanceBasisPoints,
            P30CombatRules.EffectiveResistance(sheet.VoidResistanceBasisPoints, sheet.MaximumVoidResistanceBasisPoints),
            physicalBlock,
            build.HasShield ? Math.Clamp(physicalBlock, 0, physicalBlockMaximum) : 0,
            spellBlock,
            build.HasShield ? Math.Clamp(spellBlock, 0, sheet.MaximumSpellBlockChanceBasisPoints) : 0,
            sheet.SpellSuppressionBasisPoints,
            sheet.EffectiveSpellSuppressionBasisPoints);
    }

    private static int ScaleToInt(long value, long basisPoints) =>
        (int)Math.Clamp(ScaleMultiplier(value, basisPoints), 0, int.MaxValue);

    private static long ScaleMultiplier(long value, long basisPoints)
    {
        if (value <= 0 || basisPoints <= 0) return 0;
        if (value > long.MaxValue / basisPoints) return long.MaxValue;
        return value * basisPoints / 10_000;
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
