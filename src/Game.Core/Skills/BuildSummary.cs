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
    int CriticalMultiplierBasisPoints, bool IsDirectHitEstimate = true)
{
    public static OffenseBreakdown Empty { get; } = new(0, 0, 0, 0, 0, false, 0, 0, 0, 0, 10_000, false);
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
        DefenseBreakdown defense = CalculateDefense(Combat.AuraCombatProfile.Resolve(build).Build);
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
            "估算假设：直接击中使用实战的点伤、转换、辅助、专精及速度规则；目标为1米处满生命稀有敌人，护甲25、闪避20、抗性0。按连续使用估算，不计资源中断、返程、多次命中和条件装备收益；周期、召唤及触发技能请查看战斗报告。防御按同等级代表性敌人估算。",
            defense,
            offense);
    }

    public static OffenseBreakdown CalculateOffense(TeamBuild build, SkillConfiguration configuration)
    {
        build = Combat.AuraCombatProfile.Resolve(build).Build;
        ResolvedSkill skill = CombatSkillRules.Resolve(configuration, build.Sheet.MaximumLife().Value, build.PassiveProfile);
        SkillTag tags = SkillDefinitions.Get(configuration.SkillId).Tags;
        bool spell = tags.HasFlag(SkillTag.Spell);
        var mechanic = Archetypes.ArchetypeSkillDefinitions.Active.FirstOrDefault(active => active.Combat.SkillId == skill.SkillId)?.Mechanic;
        if (ActiveSkillCatalog.ActiveForSkill(skill.SkillId).Curve is SkillCurve.Unit or SkillCurve.DamageOverTime or SkillCurve.None ||
            mechanic is Archetypes.SkillMechanic.Minion or Archetypes.SkillMechanic.Companion or Archetypes.SkillMechanic.Phantom or Archetypes.SkillMechanic.Construct or Archetypes.SkillMechanic.Rebuild ||
            skill.Role is SkillRole.Reservation or SkillRole.Guard or SkillRole.WarCry or SkillRole.Counter or SkillRole.DamageOverTime ||
            (configuration.Supports & (SkillSupport.BlockTrigger | SkillSupport.CastWhenDamaged)) != 0 ||
            LinkedSupportRules.Support(configuration, Archetypes.SupportMechanic.AttackTrigger) ||
            (tags & (SkillTag.Buff | SkillTag.Trigger | SkillTag.Counter)) != 0 ||
            skill.SkillId is SkillIds.StormBrand or "archetypes.skill.thunderstorm" or "archetypes.skill.doom_brand")
            return OffenseBreakdown.Empty with { IsSpell = spell };
        var passive = build.PassiveProfile ?? PassiveModifiers.Empty;
        var local = tags.HasFlag(SkillTag.Attack) ? build.LocalWeaponStats : null;
        int Average(LocalDamageRange? range) => range is { } value ? (value.Minimum + value.Maximum) / 2 : 0;
        var added = new AddedWeaponDamage(Average(local?.Fire), Average(local?.Cold), Average(local?.Lightning), Average(local?.Void));
        var spellRange = SpellHitRules.DamageRange(skill, configuration.Level);
        int raw = spell ? (spellRange.Minimum + spellRange.Maximum) / 2 :
            CombatSkillRules.BaseDamage(skill, tags, build.Weapon, build.AddedPhysicalDamage);
        var increases = CombatSkillRules.OffensiveIncreases(build, tags, skill.Role == SkillRole.DamageOverTime);
        int accuracy = build.Sheet.Accuracy(build.FlatAccuracy).Value;
        int hitChance = build.AlwaysHit || spell ? 10_000 : DamageRules.HitChance(accuracy, 20, false).Value;
        var criticalSupport = configuration.Supports.HasFlag(SkillSupport.CriticalStrikes) ? CombatSkillRules.SupportLink(configuration, SkillSupport.CriticalStrikes) : null;
        int criticalChance = build.CannotCrit || MasteryRuntime.CannotCrit(passive) || skill.Role == SkillRole.DamageOverTime ? 0 :
            CombatRules.CriticalChance((spell ? SpellHitRules.BaseCriticalChance(skill.SkillId, 1_000, configuration.Quality) : build.Weapon.CriticalChanceBasisPoints) +
                (criticalSupport is null ? 0 : CombatSkillRules.SupportValue(configuration, SkillSupport.CriticalStrikes) * 100), build.IncreasedCriticalChanceBasisPoints);
        int criticalMultiplier = build.CriticalMultiplierBasisPoints + (criticalSupport is null ? 0 :
            ActiveSkillCatalog.Interpolate(1_500, 3_000, criticalSupport.Level, false) + criticalSupport.Quality * 50);
        DamageBreakdown Packet(int armor, DamageModifiers? modifiers, bool scale, int critical = 10_000) => DamagePacketRules.ResolveMixed(
            raw, skill.DamageType, added, configuration.Supports, armor, 0, 0, 0, 0,
            equipment: build.CombatEquipment?.Modifiers, modifiers: modifiers,
            scaleBranch: scale ? branch => ScaleToInt(CombatSkillRules.ScaleOffensiveDamage(branch.BaseDamage, skill, configuration,
                build, tags, 100_000, 100_000, targetRareOrBoss: true, applyIncreased: false, damageHistory: branch.History), critical) : null,
            configuration: configuration, addedDamageEffectiveness: spell ? SpellHitRules.Effectiveness(skill.SkillId) : 10_000);
        int armor = configuration.Supports.HasFlag(SkillSupport.ArmorPierce) ? 17 : 25;
        int hit = Packet(armor, increases, true).Total;
        int criticalHit = Packet(armor, increases, true, criticalMultiplier).Total;
        long expected = ((long)hit * (10_000 - criticalChance) + (long)criticalHit * criticalChance) / 10_000;
        expected = expected * hitChance / 10_000;
        int frequency = CombatSkillRules.ActionFrequencyMilliPerSecond(build, skill.CastTimeTicks, skill.CooldownTicks, tags);
        long dps = expected * frequency / 1_000;
        int baseTotal = Packet(0, null, false).Total;
        int increasedTotal = Packet(0, increases, false).Total;
        int scaledTotal = Packet(0, increases, true).Total;
        int baseMultiplier = tags.HasFlag(SkillTag.Attack) ? skill.BaseDamageBasisPoints : 10_000;
        int increased = baseTotal == 0 ? 0 : (int)Math.Clamp((long)increasedTotal * 10_000 / baseTotal - 10_000, int.MinValue, int.MaxValue);
        int more = increasedTotal == 0 ? 0 : (int)Math.Clamp((long)scaledTotal * 100_000_000 /
            Math.Max(1, (long)increasedTotal * baseMultiplier) - 10_000, int.MinValue, int.MaxValue);
        int minimum = spell ? spellRange.Minimum : ScaleToInt((long)build.Weapon.MinimumPhysicalDamage + build.AddedPhysicalDamage +
            (local?.Fire.Minimum ?? 0) + (local?.Cold.Minimum ?? 0) + (local?.Lightning.Minimum ?? 0) + (local?.Void.Minimum ?? 0), baseMultiplier);
        int maximum = spell ? spellRange.Maximum : ScaleToInt((long)build.Weapon.MaximumPhysicalDamage + build.AddedPhysicalDamage +
            (local?.Fire.Maximum ?? 0) + (local?.Cold.Maximum ?? 0) + (local?.Lightning.Maximum ?? 0) + (local?.Void.Maximum ?? 0), baseMultiplier);
        return new((int)Math.Clamp(dps, 0, int.MaxValue), Math.Max(1, minimum), Math.Max(1, maximum), increased, more,
            spell, frequency, accuracy, hitChance, criticalChance, criticalMultiplier);
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
        return checked((int)Math.Clamp((long)singleTargetDps * coverage, 0, int.MaxValue));
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
