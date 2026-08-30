using GameForWork.Core.P1.Combat;
using GameForWork.Core.P12;
using GameForWork.Core.P14;
using GameForWork.Core.Simulation;

namespace GameForWork.Core.P27;

public sealed record P27MonsterFamilyDefinition(
    EnemyFamily Family,
    string StableId,
    string DisplayName,
    string Identity,
    EnemyDamageType PrimaryDamageType);

public static class P27MonsterCatalog
{
    public static IReadOnlyList<P27MonsterFamilyDefinition> Families { get; } =
    [
        new(EnemyFamily.AshenLegion, "p27.family.ashen_legion", "烬骸军团", "物理与火焰、冲锋和战旗", EnemyDamageType.Fire),
        new(EnemyFamily.FrostwildPack, "p27.family.frostwild_pack", "霜原猎群", "冰霜、包围和冰缓区域", EnemyDamageType.Cold),
        new(EnemyFamily.DrownedDead, "p27.family.drowned_dead", "沉墓亡者", "尸体利用、骨矢和钟波", EnemyDamageType.Physical),
        new(EnemyFamily.BloodforgeConstruct, "p27.family.bloodforge_construct", "血炉造物", "高护甲、熔炉和修复构装", EnemyDamageType.Lightning),
        new(EnemyFamily.VoidCult, "p27.family.void_cult", "虚空教团", "诅咒、裂隙地面和召唤", EnemyDamageType.Void),
        new(EnemyFamily.RiftBeast, "p27.family.rift_beast", "裂渊异兽", "钻地、追猎和递增强度", EnemyDamageType.Void),
        new(EnemyFamily.LifeGarden, "p27.family.life_garden", "命能孽种", "缠根、孢子和治疗花", EnemyDamageType.Cold),
        new(EnemyFamily.RedOath, "p27.family.red_oath", "赤誓刑团", "流血、献祭和处决", EnemyDamageType.Fire),
        new(EnemyFamily.BlueOath, "p27.family.blue_oath", "苍誓星侍", "延迟法术、护盾链接和雷霜", EnemyDamageType.Lightning),
        new(EnemyFamily.Warfront, "p27.family.warfront", "亡旗军阵", "盾墙、压制齐射和军官号令", EnemyDamageType.Physical),
    ];

    public static IReadOnlyList<EnemyProfile> AdditionalEnemies { get; } =
    [
        // 命能花园
        E("life_spore", "愈生孢子", EnemyFamily.LifeGarden, EnemyRole.Support, 48, 4, 7, 3, 18, 64, 1_700, 850, 1,
            S(EnemySkillKind.HealingBloom, "命能绽放", EnemyDamageType.Cold, 2_000, 13_000, 5_500, true, "绿色花环"),
            S(EnemySkillKind.ArcaneBolt, "孢子弹", EnemyDamageType.Cold, 10_500, 10_000, 6_500)),
        E("root_mauler", "缠根掠兽", EnemyFamily.LifeGarden, EnemyRole.Charger, 71, 8, 13, 12, 8, 68, 2_900, 950, 2,
            S(EnemySkillKind.Charge, "根须突袭", EnemyDamageType.Physical, 14_000, 11_000, 2_000),
            S(EnemySkillKind.RootSnare, "绞足藤", EnemyDamageType.Cold, 11_500, 13_000, 4_500, true, "蔓藤圆环")),
        E("carapace_bloomguard", "甲壳花卫", EnemyFamily.LifeGarden, EnemyRole.Melee, 96, 9, 15, 34, 2, 58, 1_500, 700, 3,
            S(EnemySkillKind.HeavySlam, "花甲震击", EnemyDamageType.Physical, 15_000, 12_000, 1_800, true)),
        E("spore_spitter", "命能喷吐者", EnemyFamily.LifeGarden, EnemyRole.Ranged, 52, 6, 10, 4, 16, 72, 1_800, 950, 2,
            S(EnemySkillKind.Volley, "腐育孢雨", EnemyDamageType.Cold, 9_500, 9_000, 6_500),
            S(EnemySkillKind.GroundHazard, "菌毯", EnemyDamageType.Cold, 12_000, 14_000, 6_000, true, "青绿落点")),
        E("brood_vine", "育种母株", EnemyFamily.LifeGarden, EnemyRole.Summoner, 83, 6, 11, 15, 3, 60, 1_300, 650, 3,
            S(EnemySkillKind.SummonSwarm, "播种幼体", EnemyDamageType.Physical, 6_000, 15_000, 5_000),
            S(EnemySkillKind.HealingBloom, "母株回春", EnemyDamageType.Cold, 1_500, 16_000, 5_500, true)),
        E("thorn_crown_keeper", "荆冠看守", EnemyFamily.LifeGarden, EnemyRole.Support, 78, 7, 12, 20, 6, 66, 1_500, 760, 3,
            S(EnemySkillKind.WarAura, "荆冠共生", EnemyDamageType.Physical, 4_000, 14_000, 5_500),
            S(EnemySkillKind.RootSnare, "荆棘禁足", EnemyDamageType.Physical, 11_000, 13_000, 4_500, true)),
        E("grafted_aberration", "嫁接畸兽", EnemyFamily.LifeGarden, EnemyRole.Melee, 112, 11, 18, 27, 4, 61, 1_700, 650, 4,
            S(EnemySkillKind.HeavySlam, "嫁接重砸", EnemyDamageType.Physical, 16_000, 13_000, 2_000, true),
            S(EnemySkillKind.GroundHazard, "命能溢流", EnemyDamageType.Cold, 12_500, 15_000, 4_000, true)),
        E("harvest_avatar", "丰收化身", EnemyFamily.LifeGarden, EnemyRole.Caster, 88, 8, 14, 12, 9, 76, 1_800, 720, 4,
            S(EnemySkillKind.DelayedNova, "丰收轮转", EnemyDamageType.Cold, 15_000, 14_000, 6_000, true, "三层花瓣环"),
            S(EnemySkillKind.HealingBloom, "收割回春", EnemyDamageType.Cold, 2_500, 15_000, 6_000, true)),

        // 赤誓
        E("red_thrall", "赤誓奴兵", EnemyFamily.RedOath, EnemyRole.Melee, 55, 7, 11, 10, 6, 66, 2_000, 1_000, 1,
            S(EnemySkillKind.BasicStrike, "血刃", EnemyDamageType.Physical, 10_500)),
        E("bloodhound", "放血猎犬", EnemyFamily.RedOath, EnemyRole.Charger, 49, 6, 10, 3, 20, 73, 3_300, 1_200, 1,
            S(EnemySkillKind.Charge, "放血扑咬", EnemyDamageType.Physical, 14_500, 10_500, 2_000)),
        E("blood_banner", "血旗侍从", EnemyFamily.RedOath, EnemyRole.Support, 69, 5, 9, 14, 5, 62, 1_500, 720, 2,
            S(EnemySkillKind.WarAura, "赤誓战旗", EnemyDamageType.Fire, 4_000, 15_000, 5_500),
            S(EnemySkillKind.Sacrifice, "献血号令", EnemyDamageType.Fire, 12_000, 14_000, 5_500, true)),
        E("pyre_crossbow", "火刑弩手", EnemyFamily.RedOath, EnemyRole.Ranged, 50, 7, 12, 5, 14, 75, 1_700, 950, 2,
            S(EnemySkillKind.SuppressingVolley, "火刑齐射", EnemyDamageType.Fire, 10_500, 10_000, 7_000),
            S(EnemySkillKind.GroundHazard, "焚刑地带", EnemyDamageType.Fire, 12_000, 14_000, 6_000, true)),
        E("sacrifice_magus", "献祭术士", EnemyFamily.RedOath, EnemyRole.Caster, 61, 7, 13, 4, 12, 78, 1_700, 800, 2,
            S(EnemySkillKind.Sacrifice, "血焰献祭", EnemyDamageType.Fire, 16_000, 13_000, 6_000, true, "血色收缩环"),
            S(EnemySkillKind.ArcaneBolt, "誓火", EnemyDamageType.Fire, 12_000, 10_000, 7_000)),
        E("armor_executioner", "裂甲处刑者", EnemyFamily.RedOath, EnemyRole.Melee, 102, 12, 19, 30, 2, 65, 1_500, 650, 4,
            S(EnemySkillKind.Execution, "断首处决", EnemyDamageType.Physical, 18_000, 15_000, 1_900, true, "赤色扇面"),
            S(EnemySkillKind.HeavySlam, "裂甲横扫", EnemyDamageType.Physical, 15_500, 12_000, 2_100, true)),
        E("oathblood_rider", "誓血骑士", EnemyFamily.RedOath, EnemyRole.Charger, 91, 10, 17, 24, 7, 70, 2_700, 800, 3,
            S(EnemySkillKind.Charge, "誓血冲阵", EnemyDamageType.Physical, 16_000, 12_000, 2_200),
            S(EnemySkillKind.Sacrifice, "燃命", EnemyDamageType.Fire, 14_000, 15_000, 3_500, true)),
        E("red_crown_arbiter", "赤冠裁决官", EnemyFamily.RedOath, EnemyRole.Caster, 89, 9, 16, 18, 5, 80, 1_600, 700, 4,
            S(EnemySkillKind.DelayedNova, "赤冠裁决", EnemyDamageType.Fire, 17_000, 15_000, 6_500, true, "赤金审判环"),
            S(EnemySkillKind.Execution, "终刑", EnemyDamageType.Physical, 16_500, 14_000, 4_000, true)),

        // 苍誓
        E("blue_acolyte", "苍誓卫徒", EnemyFamily.BlueOath, EnemyRole.Melee, 59, 6, 10, 12, 8, 67, 1_900, 950, 1,
            S(EnemySkillKind.BasicStrike, "星钢斩", EnemyDamageType.Physical, 10_500)),
        E("star_arrow", "星矢射手", EnemyFamily.BlueOath, EnemyRole.Ranged, 48, 6, 11, 3, 18, 77, 1_800, 950, 2,
            S(EnemySkillKind.Volley, "星矢齐射", EnemyDamageType.Cold, 9_500, 9_000, 7_000),
            S(EnemySkillKind.DelayedNova, "坠星标记", EnemyDamageType.Lightning, 13_500, 14_000, 7_000, true, "蓝色落星圈")),
        E("time_frozen_deacon", "冻时执事", EnemyFamily.BlueOath, EnemyRole.Support, 72, 5, 9, 8, 10, 72, 1_500, 700, 3,
            S(EnemySkillKind.RootSnare, "冻时祷文", EnemyDamageType.Cold, 10_500, 13_000, 5_500, true),
            S(EnemySkillKind.ShieldLink, "苍誓护链", EnemyDamageType.Lightning, 2_000, 14_000, 6_000)),
        E("storm_ring_magus", "雷环术士", EnemyFamily.BlueOath, EnemyRole.Caster, 58, 7, 13, 4, 14, 81, 1_700, 820, 2,
            S(EnemySkillKind.ChainLightning, "雷环连锁", EnemyDamageType.Lightning, 13_000, 11_000, 7_000),
            S(EnemySkillKind.DelayedNova, "延时雷暴", EnemyDamageType.Lightning, 15_000, 15_000, 6_000, true, "闪烁双环")),
        E("mirror_shield", "镜盾侍卫", EnemyFamily.BlueOath, EnemyRole.Support, 96, 7, 12, 32, 4, 61, 1_400, 650, 4,
            S(EnemySkillKind.ShieldLink, "镜盾链接", EnemyDamageType.Cold, 2_000, 13_000, 5_000),
            S(EnemySkillKind.HeavySlam, "盾镜冲击", EnemyDamageType.Physical, 13_500, 12_000, 1_700, true)),
        E("delayed_oracle", "延时预言者", EnemyFamily.BlueOath, EnemyRole.Caster, 66, 8, 14, 4, 13, 82, 1_600, 750, 3,
            S(EnemySkillKind.DelayedNova, "预言回响", EnemyDamageType.Void, 16_000, 16_000, 6_500, true, "三段倒计时环"),
            S(EnemySkillKind.ArcaneBolt, "苍星碎片", EnemyDamageType.Cold, 11_500, 10_000, 7_000)),
        E("star_gate_caller", "星门召集者", EnemyFamily.BlueOath, EnemyRole.Summoner, 75, 6, 11, 10, 8, 74, 1_400, 650, 3,
            S(EnemySkillKind.SummonSwarm, "开启星门", EnemyDamageType.Void, 6_000, 15_000, 5_500),
            S(EnemySkillKind.ShieldLink, "星门屏障", EnemyDamageType.Lightning, 2_000, 15_000, 6_000)),
        E("sky_arbiter", "苍穹审判官", EnemyFamily.BlueOath, EnemyRole.Caster, 94, 10, 17, 15, 7, 84, 1_600, 680, 4,
            S(EnemySkillKind.DelayedNova, "苍穹审判", EnemyDamageType.Lightning, 18_000, 16_000, 7_000, true, "苍白全环"),
            S(EnemySkillKind.ChainLightning, "群星连裁", EnemyDamageType.Lightning, 14_000, 12_000, 7_000)),

        // 亡旗战阵
        E("fallen_spearman", "亡旗枪兵", EnemyFamily.Warfront, EnemyRole.Melee, 67, 8, 13, 20, 5, 70, 1_900, 900, 2,
            S(EnemySkillKind.BasicStrike, "列阵突刺", EnemyDamageType.Physical, 11_000, 9_500, 1_700)),
        E("breach_axeman", "破阵斧手", EnemyFamily.Warfront, EnemyRole.Melee, 91, 11, 18, 25, 3, 64, 1_600, 700, 3,
            S(EnemySkillKind.HeavySlam, "破阵斩", EnemyDamageType.Physical, 16_000, 13_000, 2_000, true)),
        E("trench_crossbow", "战壕弩兵", EnemyFamily.Warfront, EnemyRole.Ranged, 54, 7, 12, 8, 13, 78, 1_600, 900, 2,
            S(EnemySkillKind.SuppressingVolley, "压制齐射", EnemyDamageType.Physical, 10_500, 9_500, 7_500)),
        E("firepot_thrower", "火罐投手", EnemyFamily.Warfront, EnemyRole.Ranged, 58, 7, 13, 6, 12, 72, 1_500, 780, 2,
            S(EnemySkillKind.Artillery, "火罐抛击", EnemyDamageType.Fire, 14_000, 14_000, 8_000, true, "橙色落点")),
        E("war_drummer", "鼓令官", EnemyFamily.Warfront, EnemyRole.Support, 76, 5, 9, 18, 5, 66, 1_400, 650, 3,
            S(EnemySkillKind.WarAura, "战鼓号令", EnemyDamageType.Physical, 4_000, 14_000, 6_000),
            S(EnemySkillKind.BasicStrike, "鼓槌", EnemyDamageType.Physical, 9_000)),
        E("shieldwall_guard", "盾墙卫士", EnemyFamily.Warfront, EnemyRole.Support, 112, 7, 12, 42, 1, 58, 1_300, 600, 4,
            S(EnemySkillKind.ShieldLink, "盾墙", EnemyDamageType.Physical, 2_000, 13_000, 5_500),
            S(EnemySkillKind.HeavySlam, "盾墙推进", EnemyDamageType.Physical, 13_000, 12_000, 1_600, true)),
        E("headhunt_officer", "猎首军官", EnemyFamily.Warfront, EnemyRole.Charger, 103, 11, 19, 30, 7, 78, 2_500, 750, 4,
            S(EnemySkillKind.Charge, "猎首突进", EnemyDamageType.Physical, 16_500, 12_000, 2_200),
            S(EnemySkillKind.Execution, "军官处决", EnemyDamageType.Physical, 17_000, 15_000, 2_000, true)),
        E("siege_engineer", "攻城术师", EnemyFamily.Warfront, EnemyRole.Caster, 82, 8, 15, 16, 6, 80, 1_400, 650, 4,
            S(EnemySkillKind.Artillery, "炮击标记", EnemyDamageType.Fire, 17_000, 16_000, 9_000, true, "三枚炮击落点"),
            S(EnemySkillKind.RepairPulse, "战地修复", EnemyDamageType.Lightning, 2_000, 15_000, 6_000, true)),
    ];

    private static readonly IReadOnlyDictionary<string, EnemyFamily[]> AreaFamilies =
        new Dictionary<string, EnemyFamily[]>(StringComparer.Ordinal)
        {
            ["core.map.cinder_road"] = [EnemyFamily.AshenLegion, EnemyFamily.RedOath],
            ["core.map.sunken_crypt"] = [EnemyFamily.DrownedDead, EnemyFamily.VoidCult],
            ["core.map.iron_orchard"] = [EnemyFamily.LifeGarden, EnemyFamily.RiftBeast],
            ["core.map.broken_bastion"] = [EnemyFamily.Warfront, EnemyFamily.AshenLegion],
            ["core.map.blood_marsh"] = [EnemyFamily.RiftBeast, EnemyFamily.LifeGarden],
            ["core.map.glass_mine"] = [EnemyFamily.BloodforgeConstruct, EnemyFamily.BlueOath],
            ["core.map.hollow_cloister"] = [EnemyFamily.VoidCult, EnemyFamily.DrownedDead],
            ["core.map.black_tide_port"] = [EnemyFamily.DrownedDead, EnemyFamily.Warfront],
            ["core.map.ashen_garden"] = [EnemyFamily.AshenLegion, EnemyFamily.LifeGarden],
            ["core.map.withered_observatory"] = [EnemyFamily.BlueOath, EnemyFamily.VoidCult],
            ["core.map.furnace_depths"] = [EnemyFamily.BloodforgeConstruct, EnemyFamily.RedOath],
            ["core.map.oathbreaker_throne"] = [EnemyFamily.Warfront, EnemyFamily.VoidCult],
        };

    static P27MonsterCatalog()
    {
        if (Families.Count != 10 || Families.Select(item => item.Family).Distinct().Count() != 10)
            throw new InvalidDataException("P27 requires ten distinct monster families.");
        if (AdditionalEnemies.Count != 32 || AdditionalEnemies.Select(item => item.StableId).Distinct().Count() != 32)
            throw new InvalidDataException("P27 requires thirty-two new non-boss monsters.");
    }

    public static EnemyProfile EnrichLegacy(EnemyProfile profile)
    {
        EnemySkillProfile primary = DefaultSkill(profile.Skill, profile.Family, profile.AttackRangeRaw);
        EnemySkillProfile signature = SignatureSkill(profile.Family, profile.Role, profile.AttackRangeRaw);
        return profile with { Skills = primary.Kind == signature.Kind ? [primary] : [primary, signature] };
    }

    public static EnemySkillProfile DefaultSkill(EnemySkillKind kind, EnemyFamily family, int rangeRaw)
    {
        EnemyDamageType damage = Families.FirstOrDefault(item => item.Family == family)?.PrimaryDamageType ?? EnemyDamageType.Physical;
        return kind switch
        {
            EnemySkillKind.HeavySlam => S(kind, "蓄力重击", EnemyDamageType.Physical, 15_000, 12_000, rangeRaw, true, "扇形蓄力"),
            EnemySkillKind.Charge => S(kind, "冲锋", EnemyDamageType.Physical, 16_000, 11_000, rangeRaw),
            EnemySkillKind.Volley => S(kind, "齐射", damage, 9_000, 9_000, rangeRaw),
            EnemySkillKind.ArcaneBolt => S(kind, "秘术投射", damage, 12_000, 10_000, rangeRaw),
            EnemySkillKind.GroundHazard => S(kind, "持续危险地面", damage, 13_500, 14_000, rangeRaw, true, "地面描边"),
            EnemySkillKind.CorpseBurst => S(kind, "尸体爆发", damage, 12_500, 14_000, rangeRaw, true, "尸体红环"),
            EnemySkillKind.SummonSwarm => S(kind, "召唤兽群", damage, 6_000, 15_000, rangeRaw),
            EnemySkillKind.WarAura => S(kind, "战斗光环", damage, 4_000, 14_000, rangeRaw),
            _ => S(kind, "基础攻击", EnemyDamageType.Physical, 10_000, 10_000, rangeRaw),
        };
    }

    public static EnemyFamily FamilyForEncounter(string areaId, P14MapNodeKind? nodeKind, P12MapAltar altar, int nodeIndex, ulong seed)
    {
        if (nodeKind == P14MapNodeKind.AbyssFissure) return EnemyFamily.RiftBeast;
        if (nodeKind == P14MapNodeKind.GardenPlot) return EnemyFamily.LifeGarden;
        if (nodeKind is P14MapNodeKind.WarfrontEncounter or P14MapNodeKind.WarfrontOfficer or P14MapNodeKind.WarfrontCommander)
            return EnemyFamily.Warfront;
        if (nodeKind == P14MapNodeKind.Altar)
            return altar == P12MapAltar.RedOath ? EnemyFamily.RedOath : EnemyFamily.BlueOath;
        EnemyFamily[] pool = AreaFamilies.GetValueOrDefault(areaId) ??
            [EnemyFamily.AshenLegion, EnemyFamily.RiftBeast];
        return pool[(int)((seed + (ulong)nodeIndex * 17) % (ulong)pool.Length)];
    }

    public static string FamilyName(EnemyFamily family) => Families.FirstOrDefault(item => item.Family == family)?.DisplayName ?? "首领";

    public static IReadOnlyList<EnemyProfile> SelectPackPool(IReadOnlyList<EnemyProfile> pool, Pcg32 random)
    {
        ArgumentNullException.ThrowIfNull(pool);
        if (pool.Count == 0) throw new ArgumentException("Monster pack pool cannot be empty.", nameof(pool));
        uint shape = random.NextUInt() % 100;
        if (shape < 25)
            return [pool[(int)(random.NextUInt() % (uint)pool.Count)]];
        if (shape < 50)
        {
            EnemyRole role = pool[(int)(random.NextUInt() % (uint)pool.Count)].Role;
            EnemyProfile[] sameRole = pool.Where(item => item.Role == role).ToArray();
            if (sameRole.Length > 0) return sameRole;
        }
        return pool.ToArray();
    }

    private static EnemySkillProfile SignatureSkill(EnemyFamily family, EnemyRole role, int range) => family switch
    {
        EnemyFamily.AshenLegion => S(EnemySkillKind.GroundHazard, "余烬燃地", EnemyDamageType.Fire, 11_500, 15_000, Math.Max(range, 4_500), true),
        EnemyFamily.FrostwildPack => S(EnemySkillKind.RootSnare, "霜原冰缚", EnemyDamageType.Cold, 11_000, 14_000, Math.Max(range, 4_500), true),
        EnemyFamily.DrownedDead => S(EnemySkillKind.CorpseBurst, "墓潮尸爆", EnemyDamageType.Physical, 12_500, 15_000, Math.Max(range, 4_000), true),
        EnemyFamily.BloodforgeConstruct => role is EnemyRole.Support or EnemyRole.Summoner
            ? S(EnemySkillKind.RepairPulse, "血炉修复", EnemyDamageType.Lightning, 2_000, 15_000, Math.Max(range, 5_000), true)
            : S(EnemySkillKind.GroundHazard, "熔炉地带", EnemyDamageType.Fire, 12_000, 15_000, Math.Max(range, 4_500), true),
        EnemyFamily.VoidCult => S(EnemySkillKind.DelayedNova, "虚空回响", EnemyDamageType.Void, 13_500, 15_000, Math.Max(range, 5_500), true),
        EnemyFamily.RiftBeast => S(EnemySkillKind.Burrow, "裂渊钻袭", EnemyDamageType.Void, 14_000, 14_000, Math.Max(range, 4_000), true),
        _ => DefaultSkill(EnemySkillKind.BasicStrike, family, range),
    };

    private static EnemyProfile E(string id, string name, EnemyFamily family, EnemyRole role,
        int life, int min, int max, int armor, int evasion, int accuracy, int speed, int attacks, int threat,
        params EnemySkillProfile[] skills)
    {
        EnemySkillProfile primary = skills[0];
        return new EnemyProfile($"p27.enemy.{id}", name, life, min, max, armor, evasion, accuracy, speed, attacks,
            threat, family, role, primary.Kind, primary.RangeRaw > 0 ? primary.RangeRaw : role is EnemyRole.Ranged or EnemyRole.Caster ? 6_000 : 1_500,
            skills);
    }

    private static EnemySkillProfile S(EnemySkillKind kind, string name, EnemyDamageType damage, int multiplier,
        int cooldown = 10_000, int range = 0, bool area = false, string telegraph = "", bool avoidable = true) =>
        new(kind, name, damage, multiplier, cooldown, range, area, telegraph, avoidable,
            kind is EnemySkillKind.ArcaneBolt or EnemySkillKind.GroundHazard or EnemySkillKind.CorpseBurst or
                EnemySkillKind.RootSnare or EnemySkillKind.DelayedNova or EnemySkillKind.ChainLightning);
}
