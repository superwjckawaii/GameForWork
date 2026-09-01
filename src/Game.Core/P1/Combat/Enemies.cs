using GameForWork.Core.Simulation;
using GameForWork.Core.P27;
using GameForWork.Core.P30;

namespace GameForWork.Core.P1.Combat;

public enum EnemyFamily
{
    AshenLegion,
    FrostwildPack,
    DrownedDead,
    BloodforgeConstruct,
    VoidCult,
    RiftBeast,
    Boss,
    LifeGarden,
    RedOath,
    BlueOath,
    Warfront,
}
public enum EnemyRole { Melee, Ranged, Caster, Charger, Summoner, Support }
public enum EnemyRarity { Normal, Magic, Rare, Boss }
public enum EnemySkillKind
{
    BasicStrike,
    HeavySlam,
    Charge,
    Volley,
    ArcaneBolt,
    GroundHazard,
    CorpseBurst,
    SummonSwarm,
    WarAura,
    RootSnare,
    HealingBloom,
    Sacrifice,
    Execution,
    DelayedNova,
    ChainLightning,
    ShieldLink,
    Burrow,
    SuppressingVolley,
    Artillery,
    RepairPulse,
}
public enum EnemyDamageType { Physical, Fire, Cold, Lightning, Void }

public sealed record EnemySkillProfile(
    EnemySkillKind Kind,
    string DisplayName,
    EnemyDamageType DamageType,
    int DamageMultiplierBasisPoints,
    int CooldownMultiplierBasisPoints = 10_000,
    int RangeRaw = 0,
    bool Area = false,
    string Telegraph = "",
    bool Avoidable = true,
    bool IsSpell = false);

public sealed record EnemyProfile(
    string StableId, string DisplayName, int Life, int MinimumPhysicalDamage, int MaximumPhysicalDamage,
    int Armor, int Evasion, int Accuracy, int MovementSpeedRawPerSecond, int AttacksPerSecondMilli, int ThreatPoints,
    EnemyFamily Family = EnemyFamily.AshenLegion, EnemyRole Role = EnemyRole.Melee,
    EnemySkillKind Skill = EnemySkillKind.BasicStrike, int AttackRangeRaw = 1_200,
    IReadOnlyList<EnemySkillProfile>? Skills = null)
{
    public IReadOnlyList<EnemySkillProfile> EffectiveSkills => Skills is { Count: > 0 }
        ? Skills
        : [P27MonsterCatalog.DefaultSkill(Skill, Family, AttackRangeRaw)];
}

public static class P1Enemies
{
    public static readonly EnemyProfile CorruptedWorker = E("corrupted_worker", "腐化工役", EnemyFamily.AshenLegion, EnemyRole.Melee, EnemySkillKind.BasicStrike, 35, 4, 6, 2, 5, 50, 2_200, 1_100, 1);
    public static readonly EnemyProfile GateHound = E("gate_hound", "门扉猎犬", EnemyFamily.AshenLegion, EnemyRole.Charger, EnemySkillKind.Charge, 25, 3, 5, 0, 20, 65, 3_500, 1_400, 1);
    public static readonly EnemyProfile OathlessGuard = E("oathless_guard", "失誓守卫", EnemyFamily.AshenLegion, EnemyRole.Melee, EnemySkillKind.HeavySlam, 70, 7, 10, 25, 3, 55, 1_800, 800, 2);
    public static readonly EnemyProfile AbyssWarden = new("core.enemy.abyss_warden", "裂渊监守者", 250, 8, 12, 20, 5, 70, 2_000, 1_000, 0, EnemyFamily.Boss, EnemyRole.Melee, EnemySkillKind.HeavySlam, 2_000);

    private static IReadOnlyList<EnemyProfile> LegacyEnemies { get; } =
    [
        CorruptedWorker,
        GateHound,
        OathlessGuard,
        E("ash_bone_archer", "烬骨弓手", EnemyFamily.AshenLegion, EnemyRole.Ranged, EnemySkillKind.Volley, 31, 4, 7, 1, 14, 62, 2_100, 1_050, 1, 6_000),
        E("cinder_confessor", "余烬告解者", EnemyFamily.AshenLegion, EnemyRole.Caster, EnemySkillKind.ArcaneBolt, 38, 5, 8, 1, 10, 66, 2_000, 950, 1, 7_000),
        E("charred_banner", "焦旗侍从", EnemyFamily.AshenLegion, EnemyRole.Support, EnemySkillKind.WarAura, 51, 4, 7, 10, 5, 58, 1_700, 800, 2, 5_500),
        E("ember_brute", "烬壳蛮卒", EnemyFamily.AshenLegion, EnemyRole.Melee, EnemySkillKind.HeavySlam, 84, 8, 12, 28, 1, 52, 1_500, 700, 3),
        E("ash_crow", "灰烬鸦", EnemyFamily.AshenLegion, EnemyRole.Charger, EnemySkillKind.Charge, 27, 4, 6, 0, 28, 74, 3_800, 1_350, 1),

        E("frostfang_hound", "霜牙猎犬", EnemyFamily.FrostwildPack, EnemyRole.Charger, EnemySkillKind.Charge, 39, 5, 8, 3, 22, 68, 3_400, 1_250, 1),
        E("rime_archer", "霜痕弓手", EnemyFamily.FrostwildPack, EnemyRole.Ranged, EnemySkillKind.Volley, 43, 6, 9, 3, 18, 70, 2_100, 1_000, 1, 6_000),
        E("winter_shaman", "寒原萨满", EnemyFamily.FrostwildPack, EnemyRole.Caster, EnemySkillKind.GroundHazard, 48, 6, 10, 2, 10, 72, 1_900, 900, 2, 7_000),
        E("icehide_aurochs", "冰皮原牛", EnemyFamily.FrostwildPack, EnemyRole.Melee, EnemySkillKind.HeavySlam, 92, 9, 14, 30, 2, 55, 1_500, 650, 3),
        E("hunger_matron", "饥群母兽", EnemyFamily.FrostwildPack, EnemyRole.Summoner, EnemySkillKind.SummonSwarm, 73, 7, 11, 12, 6, 60, 1_600, 720, 3, 5_500),
        E("snow_stalker", "雪幕潜猎者", EnemyFamily.FrostwildPack, EnemyRole.Melee, EnemySkillKind.BasicStrike, 52, 8, 12, 5, 30, 78, 2_800, 1_100, 2),
        E("rimehorn_charger", "霜角冲兽", EnemyFamily.FrostwildPack, EnemyRole.Charger, EnemySkillKind.Charge, 81, 9, 15, 22, 5, 62, 3_000, 850, 3),
        E("frost_totem_keeper", "冻柱守卫", EnemyFamily.FrostwildPack, EnemyRole.Support, EnemySkillKind.WarAura, 64, 5, 9, 20, 4, 60, 1_400, 700, 2, 5_500),

        E("drowned_corpse", "溺尸", EnemyFamily.DrownedDead, EnemyRole.Melee, EnemySkillKind.BasicStrike, 48, 5, 8, 8, 2, 48, 1_600, 850, 1),
        E("crypt_beetle", "墓穴甲虫", EnemyFamily.DrownedDead, EnemyRole.Melee, EnemySkillKind.HeavySlam, 39, 4, 6, 18, 4, 45, 1_900, 950, 1),
        E("salt_corpse", "盐尸", EnemyFamily.DrownedDead, EnemyRole.Melee, EnemySkillKind.CorpseBurst, 59, 6, 9, 16, 2, 50, 1_600, 800, 1),
        E("bell_wraith", "钟灵", EnemyFamily.DrownedDead, EnemyRole.Caster, EnemySkillKind.ArcaneBolt, 42, 5, 8, 0, 16, 72, 2_500, 1_100, 1, 7_000),
        E("tide_raider", "潮盗", EnemyFamily.DrownedDead, EnemyRole.Charger, EnemySkillKind.Charge, 65, 7, 11, 11, 8, 67, 2_300, 1_050, 2),
        E("crypt_cantor", "墓潮咏者", EnemyFamily.DrownedDead, EnemyRole.Support, EnemySkillKind.WarAura, 58, 5, 9, 8, 8, 68, 1_700, 760, 2, 5_500),
        E("bone_tide_archer", "骨潮射手", EnemyFamily.DrownedDead, EnemyRole.Ranged, EnemySkillKind.Volley, 47, 6, 10, 4, 15, 71, 1_900, 980, 2, 6_000),
        E("grave_broodmother", "墓穴育母", EnemyFamily.DrownedDead, EnemyRole.Summoner, EnemySkillKind.SummonSwarm, 88, 7, 12, 18, 3, 58, 1_500, 680, 3, 5_500),

        E("mine_thrall", "矿奴", EnemyFamily.BloodforgeConstruct, EnemyRole.Melee, EnemySkillKind.BasicStrike, 57, 6, 10, 14, 4, 52, 1_700, 900, 1),
        E("chain_hammer", "链锤工", EnemyFamily.BloodforgeConstruct, EnemyRole.Melee, EnemySkillKind.HeavySlam, 86, 10, 16, 32, 2, 58, 1_500, 650, 3),
        E("furnace_sentry", "熔炉哨机", EnemyFamily.BloodforgeConstruct, EnemyRole.Ranged, EnemySkillKind.Volley, 61, 7, 12, 24, 5, 75, 1_400, 900, 2, 6_000),
        E("slag_caster", "炉渣咒机", EnemyFamily.BloodforgeConstruct, EnemyRole.Caster, EnemySkillKind.GroundHazard, 55, 7, 12, 12, 8, 73, 1_600, 850, 2, 7_000),
        E("blood_press", "血压机偶", EnemyFamily.BloodforgeConstruct, EnemyRole.Charger, EnemySkillKind.Charge, 96, 11, 17, 38, 1, 60, 2_500, 700, 3),
        E("gear_marshal", "齿轮监军", EnemyFamily.BloodforgeConstruct, EnemyRole.Support, EnemySkillKind.WarAura, 77, 6, 10, 28, 4, 65, 1_400, 650, 3, 5_500),
        E("spark_drone", "火花浮械", EnemyFamily.BloodforgeConstruct, EnemyRole.Caster, EnemySkillKind.ArcaneBolt, 46, 6, 11, 8, 22, 78, 2_700, 1_050, 2, 7_000),
        E("foundry_brood", "铸巢母机", EnemyFamily.BloodforgeConstruct, EnemyRole.Summoner, EnemySkillKind.SummonSwarm, 82, 7, 12, 30, 2, 62, 1_300, 600, 3, 5_500),

        E("penitent", "赎罪者", EnemyFamily.VoidCult, EnemyRole.Melee, EnemySkillKind.BasicStrike, 54, 5, 9, 6, 9, 64, 2_000, 1_000, 1),
        E("void_zealot", "虚空狂信徒", EnemyFamily.VoidCult, EnemyRole.Charger, EnemySkillKind.Charge, 63, 8, 13, 8, 16, 72, 2_800, 1_050, 2),
        E("rift_oracle", "裂隙谕者", EnemyFamily.VoidCult, EnemyRole.Caster, EnemySkillKind.GroundHazard, 57, 8, 14, 3, 14, 80, 1_900, 900, 2, 7_000),
        E("oathless_crossbow", "失誓弩手", EnemyFamily.VoidCult, EnemyRole.Ranged, EnemySkillKind.Volley, 44, 6, 10, 4, 12, 70, 1_900, 1_000, 1, 6_000),
        E("night_deacon", "无光执事", EnemyFamily.VoidCult, EnemyRole.Support, EnemySkillKind.WarAura, 72, 7, 11, 13, 8, 68, 1_600, 720, 3, 5_500),
        E("shard_summoner", "碎界唤徒", EnemyFamily.VoidCult, EnemyRole.Summoner, EnemySkillKind.SummonSwarm, 68, 6, 11, 7, 10, 69, 1_500, 700, 2, 5_500),
        E("black_sun_guard", "黑日禁卫", EnemyFamily.VoidCult, EnemyRole.Melee, EnemySkillKind.HeavySlam, 104, 12, 19, 36, 3, 63, 1_500, 650, 4),
        E("void_lance", "虚矛祭兵", EnemyFamily.VoidCult, EnemyRole.Ranged, EnemySkillKind.ArcaneBolt, 59, 8, 14, 9, 15, 78, 2_000, 900, 2, 7_000),

        E("thorn_beast", "棘兽", EnemyFamily.RiftBeast, EnemyRole.Charger, EnemySkillKind.Charge, 62, 7, 11, 12, 5, 58, 2_400, 900, 2),
        E("iron_dryad", "铁皮树妖", EnemyFamily.RiftBeast, EnemyRole.Support, EnemySkillKind.WarAura, 76, 6, 9, 24, 2, 52, 1_500, 780, 2, 5_500),
        E("bog_beast", "泥沼兽", EnemyFamily.RiftBeast, EnemyRole.Melee, EnemySkillKind.HeavySlam, 68, 7, 12, 10, 3, 55, 1_800, 850, 2),
        E("blood_leech", "血蛭", EnemyFamily.RiftBeast, EnemyRole.Melee, EnemySkillKind.CorpseBurst, 28, 3, 6, 0, 22, 68, 3_200, 1_300, 1),
        E("crystal_scarab", "晶壳虫", EnemyFamily.RiftBeast, EnemyRole.Melee, EnemySkillKind.BasicStrike, 52, 5, 9, 20, 8, 56, 2_000, 950, 1),
        E("cinder_raven", "烟羽鸦", EnemyFamily.RiftBeast, EnemyRole.Ranged, EnemySkillKind.Volley, 33, 4, 7, 0, 25, 75, 3_600, 1_350, 1, 6_000),
        E("starved_aberration", "饥星畸兽", EnemyFamily.RiftBeast, EnemyRole.Caster, EnemySkillKind.GroundHazard, 74, 9, 16, 9, 12, 82, 2_100, 850, 3, 7_000),
        E("rift_broodmother", "裂界育母", EnemyFamily.RiftBeast, EnemyRole.Summoner, EnemySkillKind.SummonSwarm, 98, 8, 14, 18, 6, 66, 1_500, 620, 4, 5_500),
    ];

    public static IReadOnlyList<EnemyProfile> NormalEnemies { get; } = LegacyEnemies
        .Select(P27MonsterCatalog.EnrichLegacy)
        .Concat(P27MonsterCatalog.AdditionalEnemies)
        .ToArray();

    public static IReadOnlyList<EnemyProfile> ForMonsterLevel(int monsterLevel)
    {
        EnemyFamily family = monsterLevel switch
        {
            <= 12 => EnemyFamily.AshenLegion,
            <= 27 => EnemyFamily.FrostwildPack,
            <= 43 => EnemyFamily.DrownedDead,
            <= 57 => EnemyFamily.BloodforgeConstruct,
            <= 69 => EnemyFamily.VoidCult,
            _ => EnemyFamily.RiftBeast,
        };
        return monsterLevel >= 70 ? NormalEnemies : NormalEnemies.Where(enemy => enemy.Family == family).ToArray();
    }

    public static IReadOnlyList<EnemyProfile> ForEncounter(int monsterLevel, EnemyFamily? family)
    {
        IReadOnlyList<EnemyProfile> pool = family is null ? ForMonsterLevel(monsterLevel) :
            NormalEnemies.Where(enemy => enemy.Family == family).ToArray();
        return pool.Count > 0 ? pool : ForMonsterLevel(monsterLevel);
    }

    private static EnemyProfile E(string id, string name, EnemyFamily family, EnemyRole role, EnemySkillKind skill,
        int life, int minimumDamage, int maximumDamage, int armor, int evasion, int accuracy, int speed,
        int attacksPerSecond, int threat, int range = 1_200) => new($"core.enemy.{id}", name, life,
        minimumDamage, maximumDamage, armor, evasion, accuracy, speed, attacksPerSecond, threat, family, role, skill, range);
}

public enum EliteAffix
{
    Massive, Swift, IronSkin, Lacerating, CorpseExplosion, ArcaneWard,
    Vampiric, Resistant, Accurate, Frenzied, Suppressor, Regenerating,
    HastedAura, FortifiedAura, FlameTouched, FrostTouched, StormTouched, VoidTouched,
}

public sealed record ScaledEnemy(EnemyProfile Base, int AreaLevel, EnemyRarity Rarity, int Life,
    int MinimumPhysicalDamage, int MaximumPhysicalDamage, int Armor, int Evasion, int AttacksPerSecondMilli,
    IReadOnlyList<EliteAffix> EliteAffixes, bool AbyssRoute,
    int FireResistanceBasisPoints = 0, int ColdResistanceBasisPoints = 0,
    int LightningResistanceBasisPoints = 0, int VoidResistanceBasisPoints = 0,
    int PhysicalResistanceBasisPoints = 0);

public static class EnemyRules
{
    private static readonly IReadOnlyList<HashSet<EliteAffix>> IncompatibleGroups =
    [
        [EliteAffix.FlameTouched, EliteAffix.FrostTouched, EliteAffix.StormTouched, EliteAffix.VoidTouched],
        [EliteAffix.HastedAura, EliteAffix.FortifiedAura],
        [EliteAffix.Swift, EliteAffix.Frenzied],
    ];

    public static int ThreatBudget(int monsterLevel) => checked(3 + ((ValidateMonsterLevel(monsterLevel) - 1) / 5));

    public static ScaledEnemy Scale(EnemyProfile profile, int monsterLevel,
        IReadOnlyList<EliteAffix>? eliteAffixes = null, bool abyssRoute = false,
        EnemyRarity? rarity = null)
    {
        ValidateMonsterLevel(monsterLevel);
        EliteAffix[] affixes = eliteAffixes?.Distinct().OrderBy(value => value).ToArray() ?? [];
        rarity ??= affixes.Length switch { 0 => EnemyRarity.Normal, <= 2 => EnemyRarity.Magic, _ => EnemyRarity.Rare };
        int maximumAffixes = rarity switch { EnemyRarity.Normal => 0, EnemyRarity.Magic => 2, _ => 4 };
        if (affixes.Length > maximumAffixes || HasIncompatibleAffixes(affixes))
            throw new ArgumentException("Enemy affixes are invalid for the selected rarity.", nameof(eliteAffixes));

        int endgame = Math.Max(0, monsterLevel - 60);
        int lifeMultiplier = checked(10_000 + 1_800 * (monsterLevel - 1) + 50 * endgame * endgame);
        int damageMultiplier = checked(10_000 + 1_100 * (monsterLevel - 1) + 25 * endgame * endgame);
        int defenseMultiplier = checked(10_000 + 1_300 * (monsterLevel - 1) + 30 * endgame * endgame);
        int life = ScaleAtLeastOne(profile.Life, lifeMultiplier);
        int minimumDamage = ScaleAtLeastOne(profile.MinimumPhysicalDamage, damageMultiplier);
        int maximumDamage = ScaleAtLeastOne(profile.MaximumPhysicalDamage, damageMultiplier);
        int armor = ScaleNonNegative(profile.Armor, defenseMultiplier);
        int evasion = ScaleNonNegative(profile.Evasion, defenseMultiplier);
        int physicalResistance = 0;
        int fireResistance = profile.Family == EnemyFamily.AshenLegion ? 2_000 : 500;
        int coldResistance = profile.Family == EnemyFamily.FrostwildPack ? 2_000 : 500;
        int lightningResistance = profile.Family == EnemyFamily.BloodforgeConstruct ? 2_000 : 500;
        int voidResistance = profile.Family == EnemyFamily.VoidCult ? 2_000 : 500;
        if (monsterLevel >= 70)
        {
            int mapTier = Math.Clamp(1 + (monsterLevel - 70) * 19 / 30, 1, 20);
            P30CombatRarity p30Rarity = rarity.Value switch
            {
                EnemyRarity.Magic => P30CombatRarity.Magic,
                EnemyRarity.Rare => P30CombatRarity.Rare,
                EnemyRarity.Boss => P30CombatRarity.MapBoss,
                _ => P30CombatRarity.Normal,
            };
            P30ResistanceProfile resistance = P30CombatRules.MonsterResistances(mapTier, p30Rarity);
            physicalResistance = resistance.Physical;
            fireResistance = resistance.Fire;
            coldResistance = resistance.Cold;
            lightningResistance = resistance.Lightning;
            voidResistance = resistance.Void;
            armor = P30CombatRules.MonsterArmor(mapTier, p30Rarity);
        }
        int attackRate = profile.AttacksPerSecondMilli;
        (int rarityLife, int rarityDamage) = rarity.Value switch
        {
            EnemyRarity.Magic => (18_000, 11_500),
            EnemyRarity.Rare => (50_000, 14_500),
            EnemyRarity.Boss => (22_000, 13_000),
            _ => (10_000, 10_000),
        };
        life = ScaleAtLeastOne(life, rarityLife);
        minimumDamage = ScaleAtLeastOne(minimumDamage, rarityDamage);
        maximumDamage = ScaleAtLeastOne(maximumDamage, rarityDamage);
        if (abyssRoute)
        {
            life = ScaleAtLeastOne(life, 12_000);
            minimumDamage = ScaleAtLeastOne(minimumDamage, 11_500);
            maximumDamage = ScaleAtLeastOne(maximumDamage, 11_500);
        }

        foreach (EliteAffix affix in affixes)
        {
            switch (affix)
            {
                case EliteAffix.Massive: life = ScaleAtLeastOne(life, 15_000); break;
                case EliteAffix.Swift:
                case EliteAffix.HastedAura: attackRate = ScaleAtLeastOne(attackRate, 12_500); break;
                case EliteAffix.Frenzied:
                    attackRate = ScaleAtLeastOne(attackRate, 11_500);
                    minimumDamage = ScaleAtLeastOne(minimumDamage, 11_500);
                    maximumDamage = ScaleAtLeastOne(maximumDamage, 11_500);
                    break;
                case EliteAffix.IronSkin:
                case EliteAffix.FortifiedAura: armor = ScaleNonNegative(armor, 16_000); break;
                case EliteAffix.ArcaneWard:
                case EliteAffix.Suppressor:
                    life = ScaleAtLeastOne(life, 12_500);
                    evasion = ScaleNonNegative(evasion + 8, 13_000);
                    break;
                case EliteAffix.Accurate:
                    minimumDamage = ScaleAtLeastOne(minimumDamage, 11_000);
                    maximumDamage = ScaleAtLeastOne(maximumDamage, 11_000);
                    break;
                case EliteAffix.FlameTouched:
                    fireResistance += 1_500;
                    minimumDamage = ScaleAtLeastOne(minimumDamage, 12_000);
                    maximumDamage = ScaleAtLeastOne(maximumDamage, 12_000);
                    break;
                case EliteAffix.FrostTouched:
                    coldResistance += 1_500;
                    minimumDamage = ScaleAtLeastOne(minimumDamage, 12_000);
                    maximumDamage = ScaleAtLeastOne(maximumDamage, 12_000);
                    break;
                case EliteAffix.StormTouched:
                    lightningResistance += 1_500;
                    minimumDamage = ScaleAtLeastOne(minimumDamage, 12_000);
                    maximumDamage = ScaleAtLeastOne(maximumDamage, 12_000);
                    break;
                case EliteAffix.VoidTouched:
                    voidResistance += 1_500;
                    minimumDamage = ScaleAtLeastOne(minimumDamage, 12_000);
                    maximumDamage = ScaleAtLeastOne(maximumDamage, 12_000);
                    break;
                case EliteAffix.Resistant:
                    fireResistance += 1_500;
                    coldResistance += 1_500;
                    lightningResistance += 1_500;
                    voidResistance += 1_500;
                    life = ScaleAtLeastOne(life, 13_000);
                    break;
                case EliteAffix.Regenerating: life = ScaleAtLeastOne(life, 13_000); break;
                case EliteAffix.Lacerating:
                case EliteAffix.CorpseExplosion:
                case EliteAffix.Vampiric: break;
                default: throw new ArgumentOutOfRangeException(nameof(eliteAffixes), affix, "Unknown elite affix.");
            }
        }

        return new ScaledEnemy(profile, monsterLevel, rarity.Value, life, minimumDamage, maximumDamage, armor, evasion,
            attackRate, affixes, abyssRoute,
            Math.Clamp(fireResistance, P30CombatRules.MinimumResistance, 9_000),
            Math.Clamp(coldResistance, P30CombatRules.MinimumResistance, 9_000),
            Math.Clamp(lightningResistance, P30CombatRules.MinimumResistance, 9_000),
            Math.Clamp(voidResistance, P30CombatRules.MinimumResistance, 9_000),
            Math.Clamp(physicalResistance, P30CombatRules.MinimumResistance, 5_000));
    }

    public static IReadOnlyList<EliteAffix> RollEliteAffixes(Pcg32 random) =>
        RollAffixes(random, EnemyRarity.Magic, forceMaximum: true);

    public static IReadOnlyList<EliteAffix> RollAffixes(Pcg32 random, EnemyRarity rarity, bool forceMaximum = false)
    {
        ArgumentNullException.ThrowIfNull(random);
        int minimum = rarity switch { EnemyRarity.Magic => 1, EnemyRarity.Rare => 3, _ => 0 };
        int maximum = rarity switch { EnemyRarity.Magic => 2, EnemyRarity.Rare => 4, _ => 0 };
        int count = forceMaximum ? maximum : minimum + (maximum > minimum ? (int)(random.NextUInt() % 2) : 0);
        var available = Enum.GetValues<EliteAffix>().ToList();
        var selected = new List<EliteAffix>(count);
        while (selected.Count < count && available.Count > 0)
        {
            int choice = (int)(random.NextUInt() % (uint)available.Count);
            EliteAffix candidate = available[choice];
            available.RemoveAt(choice);
            if (HasIncompatibleAffixes(selected.Append(candidate))) continue;
            selected.Add(candidate);
        }
        return selected.OrderBy(value => value).ToArray();
    }

    private static bool HasIncompatibleAffixes(IEnumerable<EliteAffix> affixes)
    {
        HashSet<EliteAffix> set = affixes.ToHashSet();
        return IncompatibleGroups.Any(group => group.Count(set.Contains) > 1);
    }

    private static int ValidateMonsterLevel(int monsterLevel)
    {
        if (monsterLevel is < 1 or > 120)
            throw new ArgumentOutOfRangeException(nameof(monsterLevel), "Monster level must be 1 through 120.");
        return monsterLevel;
    }

    private static int ScaleAtLeastOne(int value, int multiplierBasisPoints) =>
        Math.Max(1, checked((int)((long)value * multiplierBasisPoints / 10_000)));
    private static int ScaleNonNegative(int value, int multiplierBasisPoints) =>
        Math.Max(0, checked((int)((long)value * multiplierBasisPoints / 10_000)));
}

public enum BossPhase { Opening, Summoning, Frenzy, Enraged }
public sealed record BossPhaseState(BossPhase Phase, int AttackSpeedMoreBasisPoints, int DamageMoreBasisPoints,
    bool SummonsWorkers, bool CreatesHazardZone);

public static class AbyssWardenRules
{
    public static BossPhaseState DeterminePhase(int currentLife, int maximumLife, int elapsedTicks)
    {
        if (maximumLife <= 0 || currentLife < 0 || currentLife > maximumLife)
            throw new ArgumentOutOfRangeException(nameof(currentLife), "Boss life values are invalid.");
        if (elapsedTicks >= 90 * 20) return new(BossPhase.Enraged, 10_000, 20_000, false, true);
        int lifeBasisPoints = checked((int)((long)currentLife * 10_000 / maximumLife));
        if (lifeBasisPoints < 3_500) return new(BossPhase.Frenzy, 13_000, 11_500, false, true);
        if (lifeBasisPoints < 7_000) return new(BossPhase.Summoning, 10_000, 10_000, true, true);
        return new(BossPhase.Opening, 10_000, 10_000, false, false);
    }
}
