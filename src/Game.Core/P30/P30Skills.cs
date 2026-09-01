using GameForWork.Core.P1.Combat;
using GameForWork.Core.P17;
using GameForWork.Core.P24;

namespace GameForWork.Core.P30;

public enum P30SkillCurve
{
    None,
    WeaponAttack,
    UnarmedAttack,
    ShieldAttack,
    HitSpell,
    DamageOverTime,
    Unit,
    Linear,
}

public sealed record P30ActiveSkillDefinition(
    P17ActiveSkillDefinition Combat,
    P30SkillCurve Curve,
    int LevelOneDamage,
    int LevelTwentyOneDamage,
    int LevelOneMana,
    int LevelTwentyOneMana,
    string QualityTwenty,
    bool P30Added = false)
{
    public int DamageAt(int level) => P30SkillCatalog.Interpolate(LevelOneDamage, LevelTwentyOneDamage, level, Curve != P30SkillCurve.Linear);
    public int ManaAt(int level) => P30SkillCatalog.Interpolate(LevelOneMana, LevelTwentyOneMana, level, false);
}

public sealed record P30SupportSkillDefinition(
    string StoneId,
    string DisplayName,
    string MechanicKey,
    P17SkillCapability RequiredAll,
    P17SkillCapability RequiredAny,
    P17SkillCapability Excluded,
    int LevelOneValue,
    int LevelTwentyOneValue,
    int ResourceMultiplierBasisPoints,
    string Effect,
    string QualityTwenty,
    SkillSupport LegacySupport = SkillSupport.None,
    P24SupportMechanic LegacyP24Support = P24SupportMechanic.None,
    P17SupportConflict ProvidesConflict = P17SupportConflict.None,
    P17SupportConflict ConflictsWith = P17SupportConflict.None,
    bool StarterGranted = false)
{
    public int ValueAt(int level, int quality = 0)
    {
        _ = quality; // Quality effects are bespoke and are kept in QualityTwenty, not folded into the level curve.
        return P30SkillCatalog.Interpolate(LevelOneValue, LevelTwentyOneValue, level, false);
    }
}

public sealed record P30SupportRuntimeProfile(
    IReadOnlyList<P30SupportSkillDefinition> Supports,
    int ResourceMultiplierBasisPoints,
    int DamageMultiplierBasisPoints,
    bool SingleTargetOnly,
    bool ExplodesOnKill,
    bool OverloadRepeatsEveryThirdUse,
    int TemperanceLevelPerLayer,
    int TemperanceQualityPerLayer);

public sealed record P30LinkedSupport(string StoneId, int Level, int Quality);

public static class P30SkillCatalog
{
    private static readonly IReadOnlyDictionary<string, (int One, int TwentyOne)> DamageOverrides =
        new Dictionary<string, (int, int)>(StringComparer.Ordinal)
        {
            ["重击"] = (16_000, 42_500), ["裂地横扫"] = (12_500, 33_200), ["震地冲锋"] = (11_000, 29_200),
            ["血潮旋斩"] = (5_500, 14_600), ["碎甲猛击"] = (14_000, 37_100), ["处刑裂斩"] = (13_000, 34_500),
            ["崩山震击"] = (19_000, 50_400), ["余震连锤"] = (20_000, 53_000), ["断脉横扫"] = (8_500, 22_600),
            ["血爆处决"] = (6_500, 17_200), ["跃震"] = (11_500, 30_500), ["影袭"] = (13_500, 35_800),
            ["背刺"] = (18_000, 47_800), ["符刃斩"] = (14_000, 37_100), ["幽魂飞刃"] = (9_000, 23_900),
            ["烬矛"] = (13_000, 34_500), ["血痕飞斧"] = (8_000, 21_200), ["铁钩牵引"] = (6_000, 15_900),
            ["穿云箭"] = (14_000, 37_100), ["疾风连射"] = (7_000, 18_600), ["折返箭"] = (10_500, 27_900),
            ["风行射击"] = (9_500, 25_200), ["淬毒飞刃"] = (9_500, 25_200), ["腐蚀陷阱"] = (8_500, 22_600),
            ["连环拳"] = (7_000, 22_500), ["震空掌"] = (13_000, 41_700), ["追风踢"] = (11_000, 35_300),
            ["十方终式"] = (19_000, 60_900), ["双魂夹击"] = (15_000, 48_100), ["复仇反震"] = (17_000, 49_600),
            ["余烬新星"] = (4_500, 17_400), ["寒星飞刃"] = (3_600, 13_900), ["链雷"] = (4_400, 17_000),
            ["瘟疫引爆"] = (5_250, 20_350), ["熔火弹"] = (4_800, 18_550), ["冰矛"] = (5_500, 21_250),
            ["元素棱镜"] = (4_800, 18_600), ["禁术坍缩"] = (8_750, 33_850), ["秘盾脉冲"] = (3_800, 14_700),
            ["六重刻爆"] = (7_250, 28_050), ["雷痕烙印"] = (4_500, 17_450), ["雷暴"] = (3_000, 11_600),
            ["凋零射线"] = (1_200, 4_650), ["护盾汲取"] = (1_000, 3_850), ["炽焰穿行"] = (4_500, 15_900),
            ["虚蚀领域"] = (4_200, 14_800), ["虚空裂隙"] = (6_700, 22_100), ["末日咒印"] = (6_000, 23_250),
            ["镜雷反制"] = (5_600, 21_650), ["应答术式"] = (3_600, 13_950), ["铠能震爆"] = (8_000, 21_200),
            ["碎盾回震"] = (18_000, 47_800), ["符文阵列"] = (1_500, 5_800),
        };

    private static readonly IReadOnlyDictionary<string, int> ManaTwentyOne = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["重击"] = 12, ["裂地横扫"] = 15, ["震地冲锋"] = 21, ["血潮旋斩"] = 18, ["碎甲猛击"] = 15,
        ["处刑裂斩"] = 20, ["崩山震击"] = 27, ["余震连锤"] = 23, ["断脉横扫"] = 17, ["血爆处决"] = 24,
        ["跃震"] = 21, ["影袭"] = 18, ["背刺"] = 15, ["符刃斩"] = 15, ["幽魂飞刃"] = 14,
        ["烬矛"] = 17, ["血痕飞斧"] = 18, ["铁钩牵引"] = 12, ["穿云箭"] = 15, ["疾风连射"] = 18,
        ["折返箭"] = 17, ["风行射击"] = 20, ["淬毒飞刃"] = 17, ["腐蚀陷阱"] = 21, ["连环拳"] = 11,
        ["震空掌"] = 18, ["追风踢"] = 15, ["十方终式"] = 30, ["双魂夹击"] = 21, ["余烬新星"] = 24,
        ["寒星飞刃"] = 18, ["链雷"] = 23, ["瘟疫引爆"] = 27, ["熔火弹"] = 18, ["冰矛"] = 20,
        ["元素棱镜"] = 24, ["禁术坍缩"] = 39, ["秘盾脉冲"] = 27, ["六重刻爆"] = 33,
    };

    public static IReadOnlyList<P30ActiveSkillDefinition> Active { get; } = BuildActive();
    public static IReadOnlyList<P30SupportSkillDefinition> Supports { get; } = BuildSupports();
    private static readonly IReadOnlyDictionary<string, P30ActiveSkillDefinition> ActiveBySkill =
        Active.ToDictionary(item => item.Combat.SkillId, StringComparer.Ordinal);
    private static readonly IReadOnlyDictionary<string, P30ActiveSkillDefinition> ActiveByStone =
        Active.ToDictionary(item => item.Combat.StoneId, StringComparer.Ordinal);
    private static readonly IReadOnlyDictionary<string, P30SupportSkillDefinition> SupportByStone =
        Supports.ToDictionary(item => item.StoneId, StringComparer.Ordinal);
    private static readonly IReadOnlyDictionary<SkillSupport, P30SupportSkillDefinition> SupportByLegacy =
        Supports.Where(item => item.LegacySupport != SkillSupport.None).ToDictionary(item => item.LegacySupport);
    private static readonly IReadOnlyDictionary<P24SupportMechanic, P30SupportSkillDefinition> SupportByP24 =
        Supports.Where(item => item.LegacyP24Support != P24SupportMechanic.None).ToDictionary(item => item.LegacyP24Support);

    static P30SkillCatalog()
    {
        if (Active.Count != 86 || ActiveBySkill.Count != 86 || ActiveByStone.Count != 86)
            throw new InvalidDataException("P30 requires 86 unique active skills including shield bash and five high-reservation auras.");
        if (Supports.Count != 98 || SupportByStone.Count != 98)
            throw new InvalidDataException("P30 requires 98 unique support skills.");
    }

    public static P30ActiveSkillDefinition ActiveForSkill(string id) => ActiveBySkill.TryGetValue(id, out P30ActiveSkillDefinition? value)
        ? value : throw new KeyNotFoundException($"Unknown P30 active skill: {id}");
    public static P30ActiveSkillDefinition ActiveForStone(string id) => ActiveByStone.TryGetValue(id, out P30ActiveSkillDefinition? value)
        ? value : throw new KeyNotFoundException($"Unknown P30 active stone: {id}");
    public static bool TryActiveForStone(string id, out P30ActiveSkillDefinition? value) => ActiveByStone.TryGetValue(id, out value);
    public static P30SupportSkillDefinition SupportForStone(string id) => SupportByStone.TryGetValue(id, out P30SupportSkillDefinition? value)
        ? value : throw new KeyNotFoundException($"Unknown P30 support stone: {id}");
    public static P30SupportSkillDefinition SupportFor(SkillSupport support) => SupportByLegacy[support];
    public static P30SupportSkillDefinition SupportFor(P24SupportMechanic support) => SupportByP24[support];

    public static bool SupportsActive(P30SupportSkillDefinition support, P30ActiveSkillDefinition active)
    {
        P17SkillCapability capabilities = active.Combat.Capabilities;
        return (capabilities & support.RequiredAll) == support.RequiredAll &&
               (support.RequiredAny == P17SkillCapability.None || (capabilities & support.RequiredAny) != 0) &&
               (capabilities & support.Excluded) == 0;
    }

    public static bool AreCompatible(P30SupportSkillDefinition left, P30SupportSkillDefinition right)
    {
        if ((left.ProvidesConflict & right.ConflictsWith) != 0 || (right.ProvidesConflict & left.ConflictsWith) != 0) return false;
        string[] exclusive = ["chain", "pierce", "trigger"];
        return !exclusive.Any(group => left.MechanicKey.StartsWith(group, StringComparison.Ordinal) &&
            right.MechanicKey.StartsWith(group, StringComparison.Ordinal));
    }

    public static P30SupportRuntimeProfile ResolveSupports(P30ActiveSkillDefinition active,
        IEnumerable<string> stoneIds, int level, int quality)
        => ResolveSupports(active, stoneIds.Select(id => new P30LinkedSupport(id, level, quality)));

    public static P30SupportRuntimeProfile ResolveSupports(P30ActiveSkillDefinition active,
        IEnumerable<P30LinkedSupport> supportLinks)
    {
        P30LinkedSupport[] links = supportLinks.GroupBy(item => item.StoneId, StringComparer.Ordinal)
            .Select(group => group.First()).ToArray();
        P30SupportSkillDefinition[] linked = links.Select(item => SupportForStone(item.StoneId)).ToArray();
        foreach (P30SupportSkillDefinition support in linked)
            if (!SupportsActive(support, active))
                throw new InvalidDataException($"{support.DisplayName} cannot support {active.Combat.DisplayName}.");
        for (int left = 0; left < linked.Length; left++)
        for (int right = left + 1; right < linked.Length; right++)
            if (!AreCompatible(linked[left], linked[right]))
                throw new InvalidDataException($"{linked[left].DisplayName} conflicts with {linked[right].DisplayName}.");

        int cost = 10_000;
        int damage = 10_000;
        bool single = false;
        bool explosion = false;
        bool overload = false;
        int temperanceLevel = 0;
        int temperanceQuality = 0;
        for (int index = 0; index < linked.Length; index++)
        {
            P30SupportSkillDefinition support = linked[index];
            P30LinkedSupport link = links[index];
            cost = checked(cost * support.ResourceMultiplierBasisPoints / 10_000);
            switch (support.MechanicKey)
            {
                case "target.single":
                    damage = checked(damage * (10_000 + support.ValueAt(link.Level, link.Quality) * 100) / 10_000);
                    single = true;
                    break;
                case "kill.explosion": explosion = true; break;
                case "dot.prolonged":
                    damage = active.Combat.Role == P17SkillRole.DamageOverTime
                        ? checked(damage * (10_000 + support.ValueAt(link.Level, link.Quality) * 100) / 10_000)
                        : checked(damage * 7_500 / 10_000);
                    break;
                case "cycle.overload":
                    // Two normal uses followed by one use plus a replay: 4 hits per 3 actions.
                    damage = checked(damage * 13_333 / 10_000);
                    overload = true;
                    break;
                case "virtue.temperance":
                    temperanceLevel = 1;
                    temperanceQuality = 5;
                    break;
            }
        }
        return new(linked, cost, damage, single, explosion, overload, temperanceLevel, temperanceQuality);
    }

    public static int Interpolate(int one, int twentyOne, int level, bool geometric)
    {
        int clamped = Math.Clamp(level, 1, 21);
        if (clamped == 1 || one == twentyOne) return one;
        if (clamped == 21) return twentyOne;
        double ratio = (clamped - 1) / 20d;
        double value = geometric && one > 0 && twentyOne > 0
            ? one * Math.Pow((double)twentyOne / one, ratio)
            : one + (twentyOne - one) * ratio;
        return checked((int)Math.Round(value, MidpointRounding.AwayFromZero));
    }

    private static IReadOnlyList<P30ActiveSkillDefinition> BuildActive()
    {
        P17ActiveSkillDefinition[] legacy = P17SkillCatalog.Active
            .Concat(P24SkillCatalog.Active.Select(item => item.Combat)).ToArray();
        var result = legacy.Select(active => Convert(active)).ToList();
        result.Add(Convert(NewActive("p30.skill_stone.shield_bash", "p30.skill.shield_bash", "盾锋冲击",
            SkillTag.Attack | SkillTag.Melee | SkillTag.Area | SkillTag.Physical,
            P17SkillCapability.Damage | P17SkillCapability.Hit | P17SkillCapability.Attack |
            P17SkillCapability.Melee | P17SkillCapability.Area | P17SkillCapability.PhysicalDamage |
            P17SkillCapability.CanCrit | P17SkillCapability.CanStun | P17SkillCapability.RequiresShield,
            P17SkillRole.SingleTarget, P17DamageType.Physical, P17SkillShape.Cone, 10, 2_200, 0, 0, 12_000,
            "盾牌防御换算点伤的主动盾击。"), true, 12_000, 35_000, 15, "范围效果提高 20%"));
        result.AddRange(new[]
        {
            Aura("swift_war_rhythm", "迅行战律", 35, "行动、移动与冷却恢复光环"),
            Aura("hunter_banner", "猎王战旗", 40, "稀有与 Boss 攻坚战旗"),
            Aura("hundred_soul_army", "百魂军势", 45, "普通召唤物、灵兽与构装光环"),
            Aura("undying_sanctuary", "不灭圣域", 50, "击中减伤、最大抗性与恢复光环"),
            Aura("primal_reflection", "原初映照", 50, "物理额外获得当前主要元素光环"),
        });
        return result;
    }

    private static P30ActiveSkillDefinition Convert(P17ActiveSkillDefinition active, bool added = false,
        int? oneOverride = null, int? twentyOneOverride = null, int? manaTwentyOneOverride = null, string quality = "品质效果见技能说明")
    {
        string p30Name = active.DisplayName switch
        {
            "镜式反击" => "镜雷反制",
            "魔铠过载" => "铠能震爆",
            "破盾反击" => "碎盾回震",
            _ => active.DisplayName,
        };
        if (p30Name != active.DisplayName) active = active with { DisplayName = p30Name };
        P30SkillCurve curve = Curve(active);
        (int one, int twentyOne) = DamageOverrides.TryGetValue(active.DisplayName, out var damage)
            ? damage : (active.DamageBasisPoints, DefaultTwentyOne(active.DamageBasisPoints, curve));
        one = oneOverride ?? one;
        twentyOne = twentyOneOverride ?? twentyOne;
        int mana21 = manaTwentyOneOverride ?? (ManaTwentyOne.TryGetValue(active.DisplayName, out int mana)
            ? mana : checked((int)Math.Round(active.ManaCost * 1.5, MidpointRounding.AwayFromZero)));
        return new(active, curve, one, twentyOne, active.ManaCost, mana21, quality, added);
    }

    private static P30ActiveSkillDefinition Aura(string suffix, string name, int reservationPercent, string description)
    {
        P17ActiveSkillDefinition active = NewActive($"p30.skill_stone.{suffix}", $"p30.skill.{suffix}", name,
            SkillTag.Aura | SkillTag.Reservation | SkillTag.Buff | SkillTag.Area,
            P17SkillCapability.Reservation | P17SkillCapability.Area | P17SkillCapability.Duration,
            P17SkillRole.Reservation, P17DamageType.None, P17SkillShape.Self, reservationPercent, 10_000, 0, 0, 0, description);
        return Convert(active, true, 0, 0, reservationPercent, "范围或对应核心效果提高 20%");
    }

    private static P17ActiveSkillDefinition NewActive(string stone, string skill, string name, SkillTag tags,
        P17SkillCapability capabilities, P17SkillRole role, P17DamageType damageType, P17SkillShape shape,
        int mana, int range, int cast, int cooldown, int damage, string description) =>
        new(stone, skill, name, tags, capabilities, role, damageType, shape, mana, range, cast, cooldown, damage,
            P17Ailment.None, 0, description);

    private static P30SkillCurve Curve(P17ActiveSkillDefinition active)
    {
        if (active.DamageBasisPoints == 0) return P30SkillCurve.Linear;
        if (active.Role == P17SkillRole.DamageOverTime) return P30SkillCurve.DamageOverTime;
        if (active.Tags.HasFlag(SkillTag.Attack))
            return active.DisplayName is "连环拳" or "震空掌" or "追风踢" or "十方终式" or "双魂夹击"
                ? P30SkillCurve.UnarmedAttack : P30SkillCurve.WeaponAttack;
        if (active.Tags.HasFlag(SkillTag.Spell)) return P30SkillCurve.HitSpell;
        return P30SkillCurve.Linear;
    }

    private static int DefaultTwentyOne(int one, P30SkillCurve curve) => curve switch
    {
        P30SkillCurve.WeaponAttack => checked((int)Math.Round(one * Math.Pow(1.05, 20))),
        P30SkillCurve.UnarmedAttack or P30SkillCurve.Unit => checked((int)Math.Round(one * Math.Pow(1.06, 20))),
        P30SkillCurve.ShieldAttack => checked((int)Math.Round(one * Math.Pow(1.055, 20))),
        P30SkillCurve.HitSpell => checked((int)Math.Round(one * Math.Pow(1.07, 20))),
        P30SkillCurve.DamageOverTime => checked((int)Math.Round(one * Math.Pow(1.065, 20))),
        _ => one,
    };

    private static IReadOnlyList<P30SupportSkillDefinition> BuildSupports()
    {
        var result = P17SkillCatalog.Supports.Select(Convert).Concat(P24SkillCatalog.Supports.Select(Convert)).ToList();
        P17SkillCapability cost = P17SkillCapability.HasCost;
        P17SkillCapability hit = P17SkillCapability.Hit | P17SkillCapability.Damage;
        result.AddRange(new[]
        {
            NewSupport("mercy_expansion", "仁心扩界", "virtue.mercy", P17SkillCapability.None, P17SkillCapability.Area | P17SkillCapability.Duration | P17SkillCapability.Guard, P17SkillCapability.None, 15, 25, 12_000, "每层慈悲强化范围、持续、恢复与守护。", "每层范围再提高 3%"),
            NewSupport("temperance_calculus", "持律精算", "virtue.temperance", cost, P17SkillCapability.None, P17SkillCapability.Reservation, 4, 6, 10_000, "每层节制使资源消耗总降，并使主动技能有效等级 +1、品质提高 5%。", "每层资源消耗再总降 1%"),
            NewSupport("humility_guard", "俯身守式", "virtue.humility", P17SkillCapability.None, P17SkillCapability.Attack | P17SkillCapability.Spell | P17SkillCapability.WarCry, P17SkillCapability.Triggerable, 3, 5, 11_500, "动作期间每层谦逊提供击中总降与眩晕防护。", "动作后保护延长至 1 秒"),
            NewSupport("rage_acceleration", "怒潮催行", "vice.rage", P17SkillCapability.None, P17SkillCapability.Attack | P17SkillCapability.Spell | P17SkillCapability.WarCry, P17SkillCapability.Triggerable, 6, 10, 12_500, "每层暴怒使动作速度总增，技能造成 20% 更少伤害。", "更少伤害降至 15%"),
            NewSupport("sloth_proliferation", "惰性繁生", "vice.sloth", P17SkillCapability.None, P17SkillCapability.Projectile | P17SkillCapability.Duration, P17SkillCapability.None, 10, 18, 13_000, "每层懒惰强化投射物、陷阱、普通召唤物与持续时间。", "每层持续时间再提高 5%"),
            NewSupport("arrogance_critical", "凌峰傲击", "vice.arrogance", P17SkillCapability.Hit | P17SkillCapability.CanCrit, P17SkillCapability.None, P17SkillCapability.None, 6, 10, 13_000, "每层傲慢强化暴击率与暴击更多伤害，非暴击造成更少伤害。", "非暴击更少伤害降至 15%"),
            NewSupport("lone_focus", "孤锋专注", "target.single", hit, P17SkillCapability.None, P17SkillCapability.None, 40, 60, 14_000, "每次使用最多伤害一个敌人，禁用多目标弹道和溅射。", "对稀有与 Boss 再造成 10% 更多伤害"),
            NewSupport("kill_spread", "杀势扩散", "kill.explosion", hit, P17SkillCapability.None, P17SkillCapability.None, 35, 55, 13_000, "击杀产生保留原伤害类型的爆炸，每次使用最多三次。", "爆炸范围提高 20%"),
            NewSupport("prolonged_torment", "绵延折磨", "dot.prolonged", P17SkillCapability.Duration | P17SkillCapability.Damage, P17SkillCapability.None, P17SkillCapability.None, 30, 50, 13_000, "持续伤害更多且持续更久，击中造成 25% 更少伤害。", "持续伤害生效速度提高 10%"),
            NewSupport("overload_supply", "过载供能", "cycle.overload", P17SkillCapability.Hit | P17SkillCapability.HasCost, P17SkillCapability.Attack | P17SkillCapability.Spell, P17SkillCapability.Channelling | P17SkillCapability.Triggerable, 20, 50, 15_000, "积蓄两次，第三次以 200% 消耗过载复行一次。", "复行延迟降至 0.10 秒"),
        });
        return result;
    }

    private static P30SupportSkillDefinition Convert(P17SupportSkillDefinition legacy)
    {
        (int one, int twentyOne, int cost, string quality) = Tuning(legacy.DisplayName);
        return new(legacy.StoneId, legacy.DisplayName, MechanicKey(legacy.DisplayName), legacy.RequiredAll,
            legacy.RequiredAny, legacy.Excluded, one, twentyOne, cost,
            $"{legacy.Description} P30 核心数值 {one}%→{twentyOne}%。", quality, legacy.Support,
            ProvidesConflict: legacy.ProvidesConflict, ConflictsWith: legacy.ConflictsWith,
            StarterGranted: legacy.StarterGranted);
    }

    private static P30SupportSkillDefinition Convert(P24SupportSkillDefinition legacy)
    {
        (int one, int twentyOne, int cost, string quality) = Tuning(legacy.DisplayName);
        return new(legacy.StoneId, legacy.DisplayName, MechanicKey(legacy.DisplayName), legacy.RequiredAll,
            legacy.RequiredAny, legacy.Excluded, one, twentyOne, cost,
            $"{legacy.Description} P30 核心数值 {one}%→{twentyOne}%。", quality,
            LegacyP24Support: legacy.Mechanic);
    }

    private static P30SupportSkillDefinition NewSupport(string suffix, string name, string key,
        P17SkillCapability all, P17SkillCapability any, P17SkillCapability excluded, int one, int twentyOne,
        int cost, string effect, string quality) =>
        new($"p30.skill_stone.support.{suffix}", name, key, all, any, excluded, one, twentyOne, cost, effect, quality);

    private static string MechanicKey(string name) => name switch
    {
        "追加连锁" => "chain.standard", "追踪连锁" => "chain.seeking",
        "贯穿" => "pierce.clear", "精准穿透" => "pierce.precision",
        "格挡触发" => "trigger.block", "受创触发" => "trigger.damage", "攻击触发" => "trigger.attack",
        _ => name,
    };

    private static (int One, int TwentyOne, int Cost, string Quality) Tuning(string name) => name switch
    {
        "扩大范围" => (35, 60, 12_000, "范围再提高 10%"), "攻击速度" => (20, 40, 12_500, "攻击速度再提高 10%"),
        "流血" => (60, 100, 12_500, "流血持续时间提高 20%"), "生命消耗" => (20, 35, 10_000, "更多伤害再 +5 个百分点"),
        "追加连锁" => (2, 3, 15_000, "连锁索敌距离提高 20%"), "残暴" => (35, 55, 13_500, "物理伤害再造成 5% 更多"),
        "多重投射" => (20, 40, 15_000, "投射物速度再提高 20%"), "极速投射" => (60, 100, 11_000, "投射物速度再提高 20%"),
        "急促战吼" => (35, 60, 12_000, "效果总降改为 5%"), "血之汲取" => (150, 300, 12_000, "偷取恢复速度提高 20%"),
        "处决" => (30, 50, 12_500, "处决阈值提高至 25%"), "法术回响" => (20, 10, 15_000, "整组施法速度提高 10%"),
        "元素集中" => (30, 50, 13_500, "元素伤害再造成 5% 更多"), "附加火焰" => (20, 35, 12_500, "点燃积累提高 20%"),
        "附加寒霜" => (8, 46, 12_500, "冻结积累提高 20%"), "附加闪电" => (3, 93, 12_500, "感电积累提高 20%"),
        "精准暴击" => (3, 5, 13_000, "暴击伤害倍率再 +10 个百分点"), "集中效应" => (30, 50, 13_500, "范围总降改为 20%"),
        "重势" => (45, 70, 14_000, "攻击速度总降改为 10%"), "三叠重击" => (100, 180, 13_000, "第三击更多伤害再 +20 个百分点"),
        "震域" => (25, 45, 14_000, "猛击范围再提高 15%"), "余波" => (60, 120, 15_000, "余波冷却恢复提高 20%"),
        "贴身搏杀" => (40, 70, 13_000, "满额距离延长至 2 米"), "裂甲" => (6, 10, 13_000, "破甲持续时间提高 40%"),
        "透甲" => (30, 50, 13_000, "物理抗性穿透再 +3 个百分点"), "镇压" => (80, 140, 12_500, "眩晕积累再提高 30%"),
        "震荡蔓延" => (40, 70, 12_000, "传播范围再 +1 米"), "深创" => (30, 55, 13_500, "流血持续伤害倍率 +10 个百分点"),
        "疾血" => (35, 60, 12_500, "流血再加快 10%"), "血痕播散" => (70, 100, 12_000, "最多传播目标 +2"),
        "残酷" => (20, 50, 13_000, "残酷持续时间延长至 6 秒"), "嗜血" => (40, 70, 14_000, "更多伤害再 +10 个百分点"),
        "创伤积压" => (5, 8, 13_000, "创伤上限 +2"), "坚阵" => (2, 4, 12_000, "每次额外获得 1 层护体"),
        "复仇增幅" => (40, 70, 13_000, "反击冷却恢复再提高 15%"), "格挡触发" => (35, 20, 15_000, "内部冷却降至 0.85 秒"),
        "号令增幅" => (35, 60, 14_000, "战吼范围提高 20%"), "回声战吼" => (60, 90, 15_000, "重复延迟降至 0.4 秒"),
        "誓旗增幅" => (40, 70, 10_000, "战旗效果再提高 10%"), "贯穿" => (2, 4, 12_000, "贯穿后伤害总降改为 6%"),
        "裂射" => (2, 4, 14_000, "子投射物更少伤害降至 15%"), "归返" => (30, 10, 14_000, "返回速度提高 30%"),
        "疾咏" => (20, 40, 12_500, "施法速度再提高 10%"), "雷铸转化" => (15, 30, 13_000, "感电积累提高 20%"),
        "霜流转化" => (15, 30, 13_000, "冻结积累提高 20%"), "焰化转化" => (15, 30, 13_000, "点燃积累提高 20%"),
        "虚蚀转化" => (15, 30, 13_000, "中毒概率 +20 个百分点"), "受创触发" => (20, 12, 15_000, "触发阈值再降低 2 个百分点"),
        "远射" => (35, 60, 13_000, "最大更多伤害再 +10 个百分点"), "精准穿透" => (10, 18, 13_000, "每次剩余穿透再 +3 个百分点"),
        "追踪连锁" => (25, 15, 15_000, "追踪索敌距离提高 30%"), "移动攻击" => (60, 100, 12_500, "不再造成更少伤害"),
        "毒素扩散" => (10, 20, 12_500, "最多复制层数 +5"), "多重陷阱" => (10, 25, 16_000, "武装时间总降 20%"),
        "标记增幅" => (35, 60, 13_000, "标记效果再提高 10%"), "背袭增幅" => (40, 70, 13_000, "背后判定角度扩大 15 度"),
        "召唤增幅" => (30, 55, 14_000, "最大生命总降改为 5%"), "迅捷仆从" => (30, 50, 13_000, "三种速度再提高 10%"),
        "扩军" => (1, 2, 14_000, "更少伤害降至 10%"), "护主" => (10, 20, 13_000, "召唤物最大生命再提高 20%"),
        "光环增幅" => (25, 45, 12_000, "光环范围提高 20%"), "祝福延续" => (50, 100, 13_000, "祝福冷却恢复提高 20%"),
        "恶咒传播" => (3, 5, 12_000, "传播范围再 +1 米"), "咒印深化" => (30, 55, 14_000, "诅咒持续时间提高 20%"),
        "火焰穿透" or "寒霜穿透" or "闪电穿透" => (20, 30, 13_000, "对应伤害提高 10%"),
        "元素异常" => (40, 80, 12_000, "元素异常效果再提高 10%"), "虚蚀延长" => (45, 90, 13_000, "持续时间再提高 20%"),
        "深层凋零" => (50, 100, 13_000, "凋零持续时间再提高 20%"), "护盾汲取" => (150, 300, 12_000, "护盾偷取恢复速度提高 20%"),
        "护盾施法" => (25, 45, 12_000, "护盾支付消耗总降 10%"), "徒手专注" => (40, 70, 13_500, "徒手基础暴击率 +2 个百分点"),
        "连击延续" => (60, 120, 12_000, "连击保留时间再提高 30%"), "姿态增幅" => (30, 50, 12_000, "切换冷却恢复提高 20%"),
        "位移回响" => (40, 20, 15_000, "重复更少伤害降至 10%"), "灵兽凶猛" => (35, 60, 14_000, "灵兽再造成 10% 更多"),
        "灵兽守护" => (15, 30, 13_000, "灵兽最大生命再提高 20%"), "幻身复制" => (25, 10, 15_000, "复制伤害总降改为 5%"),
        "幻身献祭" => (60, 100, 13_000, "爆发范围提高 20%"), "法武交错" => (30, 50, 13_000, "转用比例再 +10 个百分点"),
        "攻击触发" => (40, 25, 15_000, "触发冷却恢复提高 20%"), "刻印积累" => (0, 100, 12_000, "更少伤害降至 15%"),
        "刻印爆发" => (8, 12, 14_000, "每层更多伤害再 +1 个百分点"), "魔铠融合" => (15, 25, 15_000, "护卫容量提高 10%"),
        "破盾增幅" => (40, 70, 13_000, "冷却恢复速度再提高 20%"), "构装增幅" => (30, 55, 14_000, "构装最大生命再提高 20%"),
        "快速重铸" => (50, 70, 13_000, "重铸延迟再总降 10%"),
        _ => (0, 0, 10_000, "无额外品质效果"),
    };
}
