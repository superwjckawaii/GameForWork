using GameForWork.Core.Campaign;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Campaign.Progression;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Management;
using GameForWork.Core.SkillCatalog;
using GameForWork.Core.Builds;

namespace GameForWork.Core.Skills;

public sealed record DefenseBreakdown(
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

public sealed record OffenseBreakdown(
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
    public static OffenseBreakdown Empty { get; } = new(0, 0, 0, 0, 0, false, 0, 0, 0, 0, 10_000);
}

public sealed record BuildSummary(
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
    DefenseBreakdown Defense,
    OffenseBreakdown Offense);

public static class BuildSummaryRules
{
    public static BuildSummary Calculate(GameSession session)
    {
        SkillConfiguration? configuration = session.GetPreviewSkill();
        SkillLinkConfiguration? main = configuration is null ? null : session.Management.SkillLinks
            .FirstOrDefault(link => link.ActiveStoneInstanceId == configuration.StoneInstanceId);
        SkillStoneInstance? active = configuration is null ? null : session.Management.SkillStones
            .FirstOrDefault(stone => stone.InstanceId == configuration.StoneInstanceId);
        int links = main?.SocketStoneInstanceIds?.Count(id => !string.IsNullOrEmpty(id)) ??
                    (main is null ? 0 : 1 + main.SupportStoneInstanceIds.Count);
        TeamBuild build = session.World.Hero.Build;
        OffenseBreakdown offense = configuration is null
            ? OffenseBreakdown.Empty
            : CalculateOffense(build, configuration);
        int clear = configuration is null ? 0 : CalculateClearDps(offense.DamagePerSecond,
            CombatSkillRules.Resolve(configuration, build.Sheet.MaximumLife().Value, build.PassiveProfile));
        CharacterSheet sheet = build.Sheet;
        CombatPreview preview = session.GetCombatPreview();
        DefenseBreakdown defense = CalculateDefense(build);
        var issues = BuildIssues(session, active);
        return new BuildSummary(
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

    private static OffenseBreakdown CalculateOffense(TeamBuild build, SkillConfiguration configuration)
    {
        ResolvedSkill skill = CombatSkillRules.Resolve(configuration,
            build.Sheet.MaximumLife().Value, build.PassiveProfile);
        SkillTag tags = SkillDefinitions.Get(configuration.SkillId).Tags;
        int raw = CombatSkillRules.BaseDamage(skill, tags, build.Weapon, build.AddedPhysicalDamage);
        int hit = CombatSkillRules.ScaleOffensiveDamage(raw, skill, configuration, build,
            tags, targetLife: 100_000, targetMaximumLife: 100_000, targetRareOrBoss: true,
            damageType: skill.DamageType);
        if (skill.DamageType == SkillDamageType.Physical)
        {
            int reduction = DamageRules.ArmorReduction(25, Math.Max(1, hit)).Value;
            hit = (int)Math.Clamp((long)hit * (10_000 - reduction) / 10_000, 0, int.MaxValue);
        }
        LocalWeaponStats? localWeapon = tags.HasFlag(SkillTag.Attack) ? build.LocalWeaponStats : null;
        int addedMinimum = localWeapon is null ? 0 : checked(localWeapon.Fire.Minimum + localWeapon.Cold.Minimum +
            localWeapon.Lightning.Minimum + localWeapon.Void.Minimum);
        int addedMaximum = localWeapon is null ? 0 : checked(localWeapon.Fire.Maximum + localWeapon.Cold.Maximum +
            localWeapon.Lightning.Maximum + localWeapon.Void.Maximum);
        if (localWeapon is not null)
        {
            foreach ((LocalDamageRange range, SkillDamageType type) in new[]
                     {
                         (localWeapon.Fire, SkillDamageType.Fire),
                         (localWeapon.Cold, SkillDamageType.Cold),
                         (localWeapon.Lightning, SkillDamageType.Lightning),
                         (localWeapon.Void, SkillDamageType.Void),
                     })
            {
                int average = checked((range.Minimum + range.Maximum) / 2);
                if (average <= 0) continue;
                hit = checked(hit + CombatSkillRules.ScaleOffensiveDamage(average, skill, configuration, build,
                    tags, targetLife: 100_000, targetMaximumLife: 100_000, targetRareOrBoss: true,
                    damageType: type));
            }
        }
        int accuracy = build.Sheet.Accuracy(build.FlatAccuracy).Value;
        int hitChance = build.AlwaysHit || tags.HasFlag(SkillTag.Spell)
            ? 10_000 : DamageRules.HitChance(accuracy, 20, false).Value;
        PassiveModifiers passive = build.PassiveProfile ?? PassiveModifiers.Empty;
        int criticalChance = build.CannotCrit || MasteryRuntime.CannotCrit(passive) ? 0 :
            CombatRules.CriticalChance(build.Weapon.CriticalChanceBasisPoints,
                build.IncreasedCriticalChanceBasisPoints);
        long expectedCriticalMultiplier = 10_000L +
            (long)criticalChance * (build.CriticalMultiplierBasisPoints - 10_000) / 10_000;
        long expectedHit = (long)hit * hitChance / 10_000 * expectedCriticalMultiplier / 10_000;
        int frequencyMilliPerSecond = CombatSkillRules.ActionFrequencyMilliPerSecond(build,
            skill.CastTimeTicks, skill.CooldownTicks, tags);
        long dps = expectedHit * frequencyMilliPerSecond / 1_000;
        if (skill.Returns) dps *= 2;

        int baseMinimum = tags.HasFlag(SkillTag.Attack)
            ? ScaleToInt((long)build.Weapon.MinimumPhysicalDamage + build.AddedPhysicalDamage,
                skill.BaseDamageBasisPoints) + ScaleToInt(addedMinimum, skill.BaseDamageBasisPoints)
            : skill.BaseDamageBasisPoints;
        int baseMaximum = tags.HasFlag(SkillTag.Attack)
            ? ScaleToInt((long)build.Weapon.MaximumPhysicalDamage + build.AddedPhysicalDamage,
                skill.BaseDamageBasisPoints) + ScaleToInt(addedMaximum, skill.BaseDamageBasisPoints)
            : skill.BaseDamageBasisPoints;
        int increased = checked(build.IncreasedDamageBasisPoints + passive.DamageFor(tags) + configuration.Quality * 100);
        if (tags.HasFlag(SkillTag.Attack))
            increased = checked(increased + Ascendancies.WarriorAscendancyRules.IncreasedAttackDamageBasisPoints(
                build.Ascendancy ?? Ascendancies.CombatProfile.Empty, build.Sheet.Attributes.Physique));
        if (tags.HasFlag(SkillTag.Spell)) increased = checked(increased + build.IncreasedSpellDamageBasisPoints);
        long more = Math.Max(0, 10_000L + passive.MoreDamageBasisPoints);
        if (skill.Role == SkillRole.DamageOverTime)
            more = ScaleMultiplier(more, 10_000L + build.MoreDamageOverTimeBasisPoints);
        if (tags.HasFlag(SkillTag.Attack))
            more = ScaleMultiplier(more, 10_000L + build.MoreAttackDamageBasisPoints);
        if (tags.HasFlag(SkillTag.Spell))
            more = ScaleMultiplier(more, 10_000L + build.MoreSpellDamageBasisPoints);
        if ((tags & SkillTag.Elemental) != 0)
            more = ScaleMultiplier(more, 10_000L + build.MoreElementalDamageBasisPoints);
        if (tags.HasFlag(SkillTag.Void))
            more = ScaleMultiplier(more, 10_000L + build.MoreVoidDamageBasisPoints);
        more = ScaleMultiplier(more, 10_000L + build.MoreRareBossDamageBasisPoints);
        more = ScaleMultiplier(more, CombatSkillRules.DamageMultiplier(skill, 100_000, 100_000));
        more = ScaleMultiplier(more,
            MasteryRuntime.OffensiveMultiplier(passive, tags, build.Weapon, 100_000, 100_000,
                hasOffHand: build.HasOffHand, hit: skill.Role != SkillRole.DamageOverTime));
        return new OffenseBreakdown(
            checked((int)Math.Clamp(dps, 0, int.MaxValue)),
            Math.Max(1, baseMinimum),
            Math.Max(1, baseMaximum),
            increased,
            checked((int)Math.Clamp(more - 10_000, int.MinValue, int.MaxValue)),
            tags.HasFlag(SkillTag.Spell),
            frequencyMilliPerSecond,
            accuracy,
            hitChance,
            criticalChance,
            build.CriticalMultiplierBasisPoints);
    }

    private static int CalculateClearDps(int singleTargetDps, ResolvedSkill skill)
    {
        int coverage = skill.Shape switch
        {
            SkillShape.Circle or SkillShape.Cone or SkillShape.GroundArea => 3,
            SkillShape.Projectile or SkillShape.Chain =>
                Math.Clamp(skill.ProjectileCount + skill.MaximumChains, 1, 5),
            _ => 1,
        };
        return checked((int)Math.Clamp((long)singleTargetDps * coverage / (skill.Returns ? 2 : 1), 0, int.MaxValue));
    }

    private static DefenseBreakdown CalculateDefense(TeamBuild build)
    {
        CharacterSheet sheet = build.Sheet;
        int armor = sheet.Armor().Value;
        int evasion = sheet.Evasion().Value;
        int representativeHit = Math.Max(10, sheet.Level * 10);
        int representativeAccuracy = Math.Max(20, sheet.Level * 2);
        Ascendancies.CombatProfile ascendancy = build.Ascendancy ?? new Ascendancies.CombatProfile(Ascendancies.Ascendancy.None, []);
        int physicalBlock = Ascendancies.WarriorAscendancyRules.AttackBlockChanceBasisPoints(
            build.BlockChanceBasisPoints, ascendancy, build.HasShield);
        int physicalBlockMaximum = Ascendancies.WarriorAscendancyRules.AttackBlockMaximumBasisPoints(
            sheet.MaximumBlockChanceBasisPoints, ascendancy, build.HasShield);
        int finalPhysicalBlock = build.HasShield ? Math.Clamp(physicalBlock, 0, physicalBlockMaximum) : 0;
        int spellBlock = Ascendancies.WarriorAscendancyRules.SpellBlockChanceBasisPoints(
            sheet.SpellBlockChanceBasisPoints, finalPhysicalBlock, ascendancy, build.HasShield);
        return new DefenseBreakdown(
            sheet.MaximumLife().Value,
            sheet.MaximumShield().Value,
            sheet.MaximumMana().Value,
            sheet.Attributes,
            armor,
            DamageRules.ArmorReduction(armor, representativeHit).Value,
            evasion,
            10_000 - DamageRules.HitChance(representativeAccuracy, evasion, false).Value,
            sheet.FireResistanceBasisPoints,
            CombatRules.EffectiveResistance(sheet.FireResistanceBasisPoints, sheet.ResistanceMaximum(EnemyDamageType.Fire)),
            sheet.ColdResistanceBasisPoints,
            CombatRules.EffectiveResistance(sheet.ColdResistanceBasisPoints, sheet.ResistanceMaximum(EnemyDamageType.Cold)),
            sheet.LightningResistanceBasisPoints,
            CombatRules.EffectiveResistance(sheet.LightningResistanceBasisPoints, sheet.ResistanceMaximum(EnemyDamageType.Lightning)),
            sheet.VoidResistanceBasisPoints,
            CombatRules.EffectiveResistance(sheet.VoidResistanceBasisPoints, sheet.MaximumVoidResistanceBasisPoints),
            physicalBlock,
            finalPhysicalBlock,
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

    private static IReadOnlyList<string> BuildIssues(GameSession session, SkillStoneInstance? active)
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
                    if (support is not null && !SkillCompatibility.Check(groupActive.Definition, support.Definition).Compatible)
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
