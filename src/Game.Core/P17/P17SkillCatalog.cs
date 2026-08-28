using GameForWork.Core.P1.Combat;

namespace GameForWork.Core.P17;

public enum P17DamageType { None, Physical, Fire, Cold, Lightning, Void }
public enum P17Ailment { None, Bleed, Ignite, Chill, Freeze, Shock, Paralysis, Erosion, Wither, Stun, ArmorBreak }
public enum P17SkillRole { SingleTarget, Clear, Movement, WarCry, Reservation, Guard, Counter, DamageOverTime }
public enum P17SkillShape { Single, Cone, Circle, Projectile, Chain, MovementCircle, GroundArea, Self }

[Flags]
public enum P17SkillCapability : ulong
{
    None = 0,
    Damage = 1UL << 0,
    Hit = 1UL << 1,
    Attack = 1UL << 2,
    Spell = 1UL << 3,
    Melee = 1UL << 4,
    Projectile = 1UL << 5,
    Area = 1UL << 6,
    Chain = 1UL << 7,
    Movement = 1UL << 8,
    WarCry = 1UL << 9,
    Reservation = 1UL << 10,
    Guard = 1UL << 11,
    Counter = 1UL << 12,
    Duration = 1UL << 13,
    Channelling = 1UL << 14,
    CanBleed = 1UL << 15,
    CanStun = 1UL << 16,
    CanArmorBreak = 1UL << 17,
    CanCrit = 1UL << 18,
    HasCost = 1UL << 19,
    Repeatable = 1UL << 20,
    Slam = 1UL << 21,
    Triggerable = 1UL << 22,
    RequiresShield = 1UL << 23,
    PhysicalDamage = 1UL << 24,
    FireDamage = 1UL << 25,
    ColdDamage = 1UL << 26,
    LightningDamage = 1UL << 27,
    VoidDamage = 1UL << 28,
    ElementalDamage = FireDamage | ColdDamage | LightningDamage,
}

[Flags]
public enum P17SupportConflict : uint
{
    None = 0,
    PhysicalOnly = 1 << 0,
    NonPhysical = 1 << 1,
    GrantsBleed = 1 << 2,
    PreventsBleed = 1 << 3,
    Trigger = 1 << 4,
}

public sealed record P17ActiveSkillDefinition(
    string StoneId,
    string SkillId,
    string DisplayName,
    SkillTag Tags,
    P17SkillCapability Capabilities,
    P17SkillRole Role,
    P17DamageType DamageType,
    P17SkillShape Shape,
    int ManaCost,
    int RangeRaw,
    int CastTimeTicks,
    int CooldownTicks,
    int DamageBasisPoints,
    P17Ailment Ailment,
    int AilmentChanceBasisPoints,
    string Description,
    bool StarterGranted = false);

public sealed record P17SupportSkillDefinition(
    string StoneId,
    string DisplayName,
    SkillSupport Support,
    SkillTag SupportedTags,
    P17SkillCapability RequiredAll,
    P17SkillCapability RequiredAny,
    P17SkillCapability Excluded,
    string Description,
    P17SupportConflict ProvidesConflict = P17SupportConflict.None,
    P17SupportConflict ConflictsWith = P17SupportConflict.None,
    bool StarterGranted = false);

public static class P17SkillCatalog
{
    private const P17SkillCapability AttackHit = P17SkillCapability.Damage | P17SkillCapability.Hit |
        P17SkillCapability.Attack | P17SkillCapability.CanCrit | P17SkillCapability.HasCost;
    private const P17SkillCapability PhysicalAttack = AttackHit | P17SkillCapability.PhysicalDamage |
        P17SkillCapability.CanBleed | P17SkillCapability.CanStun;
    private const P17SkillCapability SpellHit = P17SkillCapability.Damage | P17SkillCapability.Hit |
        P17SkillCapability.Spell | P17SkillCapability.CanCrit | P17SkillCapability.HasCost;

    public static IReadOnlyList<P17ActiveSkillDefinition> Active { get; } =
    [
        A("heavy_strike", "重击", SkillTag.Attack | SkillTag.Melee | SkillTag.Strike | SkillTag.Physical,
            PhysicalAttack | P17SkillCapability.Melee | P17SkillCapability.Repeatable, P17SkillRole.SingleTarget,
            P17DamageType.Physical, P17SkillShape.Single, 8, 1_500, 0, 0, 16_000, P17Ailment.Stun, 5_000,
            "单体重击，造成160%武器伤害并提高50%眩晕积累。", true),
        A("war_cry", "战吼", SkillTag.WarCry | SkillTag.Buff | SkillTag.Area,
            P17SkillCapability.WarCry | P17SkillCapability.Area | P17SkillCapability.Duration | P17SkillCapability.HasCost,
            P17SkillRole.WarCry, P17DamageType.None, P17SkillShape.Self, 12, 6_000, 10, 120, 0, P17Ailment.None, 0,
            "强化接下来三次近战攻击，敌人较多或面对Boss时使用。", true),
        A("earth_cleave", "裂地横扫", SkillTag.Attack | SkillTag.Melee | SkillTag.Slam | SkillTag.Area | SkillTag.Physical,
            PhysicalAttack | P17SkillCapability.Melee | P17SkillCapability.Area | P17SkillCapability.Slam,
            P17SkillRole.Clear, P17DamageType.Physical, P17SkillShape.Cone, 10, 2_800, 4, 24, 12_500,
            P17Ailment.Stun, 2_000, "扇区猛击并击退普通敌人。", true),
        A("spirit_blade", "幽魂飞刃", SkillTag.Attack | SkillTag.Projectile | SkillTag.Chaining | SkillTag.Physical,
            PhysicalAttack | P17SkillCapability.Projectile | P17SkillCapability.Chain, P17SkillRole.Clear,
            P17DamageType.Physical, P17SkillShape.Chain, 9, 8_000, 3, 20, 9_000, P17Ailment.None, 0,
            "基于武器伤害的飞刃，基础连锁两次，按攻击命中计算。", true),
        A("seismic_charge", "震地冲锋", SkillTag.Attack | SkillTag.Melee | SkillTag.Area | SkillTag.Physical | SkillTag.Movement,
            PhysicalAttack | P17SkillCapability.Melee | P17SkillCapability.Area | P17SkillCapability.Movement,
            P17SkillRole.Movement, P17DamageType.Physical, P17SkillShape.MovementCircle, 14, 5_000, 5, 80, 11_000,
            P17Ailment.Stun, 2_000, "冲向目标并在落点造成范围伤害。"),
        A("blood_tide_spin", "血潮旋斩", SkillTag.Attack | SkillTag.Melee | SkillTag.Area | SkillTag.Physical | SkillTag.Bleed | SkillTag.Channelling,
            PhysicalAttack | P17SkillCapability.Melee | P17SkillCapability.Area | P17SkillCapability.Channelling | P17SkillCapability.Repeatable,
            P17SkillRole.Clear, P17DamageType.Physical, P17SkillShape.Circle, 12, 2_500, 5, 18, 5_500,
            P17Ailment.Bleed, 3_500, "持续旋斩周围敌人并施加流血。"),
        A("iron_oath_banner", "铁誓战旗", SkillTag.Buff | SkillTag.Area | SkillTag.Reservation | SkillTag.Aura | SkillTag.Guard,
            P17SkillCapability.Reservation | P17SkillCapability.Area | P17SkillCapability.Duration,
            P17SkillRole.Reservation, P17DamageType.None, P17SkillShape.Self, 0, 8_000, 4, 0, 0,
            P17Ailment.None, 0, "保留15%法力，提高物理伤害与护甲。"),
        A("ash_javelin", "烬矛", SkillTag.Attack | SkillTag.Projectile | SkillTag.Physical | SkillTag.Fire,
            PhysicalAttack | P17SkillCapability.Projectile | P17SkillCapability.FireDamage,
            P17SkillRole.Clear, P17DamageType.Fire, P17SkillShape.Projectile, 11, 9_500, 4, 18, 13_000,
            P17Ailment.Ignite, 2_000, "投掷烬矛，获得额外火焰伤害并穿透首个目标。"),
        A("ember_nova", "余烬新星", SkillTag.Spell | SkillTag.Area | SkillTag.Fire,
            SpellHit | P17SkillCapability.Area | P17SkillCapability.FireDamage, P17SkillRole.Clear,
            P17DamageType.Fire, P17SkillShape.Circle, 16, 4_200, 8, 32, 11_000, P17Ailment.Ignite, 3_500,
            "以自身为中心释放火焰新星。"),
        A("storm_brand", "雷痕烙印", SkillTag.Spell | SkillTag.Area | SkillTag.Duration | SkillTag.Chaining | SkillTag.Lightning,
            SpellHit | P17SkillCapability.Area | P17SkillCapability.Duration | P17SkillCapability.Chain |
            P17SkillCapability.LightningDamage, P17SkillRole.SingleTarget, P17DamageType.Lightning,
            P17SkillShape.Chain, 14, 10_000, 7, 40, 8_500, P17Ailment.Shock, 3_000,
            "附着目标并持续脉冲，脉冲可连锁并施加感电。"),
        A("armor_break_strike", "碎甲猛击", SkillTag.Attack | SkillTag.Melee | SkillTag.Strike | SkillTag.Physical | SkillTag.ArmorBreak,
            PhysicalAttack | P17SkillCapability.Melee | P17SkillCapability.CanArmorBreak, P17SkillRole.SingleTarget,
            P17DamageType.Physical, P17SkillShape.Single, 10, 1_600, 4, 20, 14_000, P17Ailment.ArmorBreak, 10_000,
            "命中施加一层破甲，暴击额外施加一层。"),
        A("execution_cleave", "处刑裂斩", SkillTag.Attack | SkillTag.Melee | SkillTag.Area | SkillTag.Physical,
            PhysicalAttack | P17SkillCapability.Melee | P17SkillCapability.Area, P17SkillRole.SingleTarget,
            P17DamageType.Physical, P17SkillShape.Cone, 13, 2_400, 5, 30, 13_000, P17Ailment.None, 0,
            "半圆裂斩，对低于25%生命的敌人造成50%更多伤害。"),
        A("mountain_slam", "崩山震击", SkillTag.Attack | SkillTag.Melee | SkillTag.Slam | SkillTag.Area | SkillTag.Physical | SkillTag.Stun,
            PhysicalAttack | P17SkillCapability.Melee | P17SkillCapability.Area | P17SkillCapability.Slam,
            P17SkillRole.Clear, P17DamageType.Physical, P17SkillShape.Circle, 18, 3_600, 18, 48, 19_000,
            P17Ailment.Stun, 10_000, "缓慢的大范围猛击，眩晕积累翻倍。"),
        A("aftershock_maul", "余震连锤", SkillTag.Attack | SkillTag.Melee | SkillTag.Slam | SkillTag.Area | SkillTag.Physical | SkillTag.Duration,
            PhysicalAttack | P17SkillCapability.Melee | P17SkillCapability.Area | P17SkillCapability.Slam | P17SkillCapability.Duration,
            P17SkillRole.Clear, P17DamageType.Physical, P17SkillShape.Circle, 15, 3_000, 10, 36, 20_000,
            P17Ailment.Stun, 3_000, "首击后延迟产生余震，总计200%武器伤害。"),
        A("vein_rend", "断脉横扫", SkillTag.Attack | SkillTag.Melee | SkillTag.Area | SkillTag.Physical | SkillTag.Bleed,
            PhysicalAttack | P17SkillCapability.Melee | P17SkillCapability.Area, P17SkillRole.DamageOverTime,
            P17DamageType.Physical, P17SkillShape.Cone, 11, 2_600, 6, 24, 8_500, P17Ailment.Bleed, 10_000,
            "较低命中伤害，必定流血且流血总伤提高60%。"),
        A("blood_burst", "血爆处决", SkillTag.Attack | SkillTag.Melee | SkillTag.Area | SkillTag.Physical | SkillTag.Duration,
            AttackHit | P17SkillCapability.Melee | P17SkillCapability.Area | P17SkillCapability.PhysicalDamage,
            P17SkillRole.Clear, P17DamageType.Physical, P17SkillShape.Circle, 16, 3_500, 8, 40, 6_500,
            P17Ailment.None, 0, "清除目标流血，将65%剩余流血伤害引爆为范围物理伤害。"),
        A("blood_mark_axe", "血痕飞斧", SkillTag.Attack | SkillTag.Projectile | SkillTag.Physical | SkillTag.Bleed | SkillTag.Returning,
            PhysicalAttack | P17SkillCapability.Projectile | P17SkillCapability.Duration, P17SkillRole.Clear,
            P17DamageType.Physical, P17SkillShape.Projectile, 12, 8_500, 6, 26, 8_000, P17Ailment.Bleed, 5_000,
            "飞出与返回各命中一次，自动穿透正在流血的敌人。"),
        A("iron_hook", "铁钩牵引", SkillTag.Attack | SkillTag.Projectile | SkillTag.Physical,
            PhysicalAttack | P17SkillCapability.Projectile | P17SkillCapability.CanArmorBreak, P17SkillRole.Movement,
            P17DamageType.Physical, P17SkillShape.Projectile, 8, 7_000, 5, 30, 6_000, P17Ailment.ArmorBreak, 10_000,
            "拉近普通敌人；面对稀有怪和Boss时改为将角色拉向目标。"),
        A("breaker_cry", "破阵怒吼", SkillTag.WarCry | SkillTag.Area | SkillTag.ArmorBreak | SkillTag.Stun,
            P17SkillCapability.WarCry | P17SkillCapability.Area | P17SkillCapability.CanArmorBreak | P17SkillCapability.Duration | P17SkillCapability.HasCost,
            P17SkillRole.WarCry, P17DamageType.None, P17SkillShape.Self, 16, 8_000, 12, 140, 0,
            P17Ailment.ArmorBreak, 10_000, "对范围敌人施加两层破甲，并强化接下来两次猛击的眩晕。"),
        A("defiant_cry", "不屈战吼", SkillTag.WarCry | SkillTag.Area | SkillTag.Buff | SkillTag.Guard,
            P17SkillCapability.WarCry | P17SkillCapability.Area | P17SkillCapability.Guard | P17SkillCapability.Duration | P17SkillCapability.HasCost,
            P17SkillRole.WarCry, P17DamageType.None, P17SkillShape.Self, 18, 8_000, 12, 160, 0,
            P17Ailment.None, 0, "恢复10%已损失生命并在3秒内降低20%承受伤害。"),
        A("iron_guard", "钢铁护卫", SkillTag.Guard | SkillTag.Buff | SkillTag.Duration,
            P17SkillCapability.Guard | P17SkillCapability.Duration | P17SkillCapability.Triggerable |
            P17SkillCapability.RequiresShield | P17SkillCapability.HasCost, P17SkillRole.Guard,
            P17DamageType.None, P17SkillShape.Self, 14, 0, 4, 120, 0, P17Ailment.None, 0,
            "需要盾牌；3秒内降低40%承受伤害。"),
        A("vengeful_counter", "复仇反震", SkillTag.Attack | SkillTag.Counter | SkillTag.Melee | SkillTag.Area | SkillTag.Physical,
            PhysicalAttack | P17SkillCapability.Counter | P17SkillCapability.Melee | P17SkillCapability.Area |
            P17SkillCapability.Triggerable | P17SkillCapability.RequiresShield, P17SkillRole.Counter,
            P17DamageType.Physical, P17SkillShape.Circle, 0, 2_800, 0, 24, 17_000, P17Ailment.Stun, 3_000,
            "需要盾牌；成功格挡后自动反击周围敌人。"),
        A("breach_banner", "裂阵战旗", SkillTag.Aura | SkillTag.Reservation | SkillTag.Area | SkillTag.ArmorBreak | SkillTag.Stun,
            P17SkillCapability.Reservation | P17SkillCapability.Area | P17SkillCapability.Duration,
            P17SkillRole.Reservation, P17DamageType.None, P17SkillShape.Self, 0, 8_000, 4, 0, 0,
            P17Ailment.None, 0, "保留10%法力，降低敌人护甲并提高己方眩晕积累。"),
        A("leap_quake", "跃震", SkillTag.Attack | SkillTag.Melee | SkillTag.Slam | SkillTag.Movement | SkillTag.Area | SkillTag.Physical,
            PhysicalAttack | P17SkillCapability.Melee | P17SkillCapability.Slam | P17SkillCapability.Movement | P17SkillCapability.Area,
            P17SkillRole.Movement, P17DamageType.Physical, P17SkillShape.MovementCircle, 14, 6_000, 8, 70, 11_500,
            P17Ailment.Stun, 2_500, "跃过地面危险并在落点震击。"),
        A("frost_shard", "寒星飞刃", SkillTag.Spell | SkillTag.Projectile | SkillTag.Cold,
            SpellHit | P17SkillCapability.Projectile | P17SkillCapability.ColdDamage, P17SkillRole.Clear,
            P17DamageType.Cold, P17SkillShape.Projectile, 12, 9_000, 6, 22, 10_500, P17Ailment.Chill, 10_000,
            "寒霜投射物，施加缓速并积累冻结。"),
        A("chain_lightning", "链雷", SkillTag.Spell | SkillTag.Chaining | SkillTag.Lightning,
            SpellHit | P17SkillCapability.Chain | P17SkillCapability.LightningDamage, P17SkillRole.Clear,
            P17DamageType.Lightning, P17SkillShape.Chain, 15, 9_000, 7, 28, 10_000, P17Ailment.Shock, 3_000,
            "命中最多四个目标并施加感电。"),
        A("flame_step", "炽焰穿行", SkillTag.Spell | SkillTag.Movement | SkillTag.Fire | SkillTag.Duration,
            P17SkillCapability.Spell | P17SkillCapability.Movement | P17SkillCapability.Duration |
            P17SkillCapability.Area | P17SkillCapability.Damage | P17SkillCapability.FireDamage | P17SkillCapability.HasCost,
            P17SkillRole.Movement, P17DamageType.Fire, P17SkillShape.MovementCircle, 18, 5_000, 4, 80, 7_000,
            P17Ailment.Ignite, 2_500, "穿行并留下持续2.5秒的灼烧地面。"),
        A("void_decay_field", "虚蚀领域", SkillTag.Spell | SkillTag.Area | SkillTag.Duration | SkillTag.Void,
            P17SkillCapability.Spell | P17SkillCapability.Area | P17SkillCapability.Duration |
            P17SkillCapability.Damage | P17SkillCapability.VoidDamage | P17SkillCapability.HasCost,
            P17SkillRole.DamageOverTime, P17DamageType.Void, P17SkillShape.GroundArea, 20, 6_000, 10, 80, 8_000,
            P17Ailment.Erosion, 10_000, "持续造成虚空伤害，每秒施加一层枯萎。"),
        A("prismatic_guard", "棱光护卫", SkillTag.Spell | SkillTag.Guard | SkillTag.Duration | SkillTag.Elemental | SkillTag.Void,
            P17SkillCapability.Spell | P17SkillCapability.Guard | P17SkillCapability.Duration |
            P17SkillCapability.Triggerable | P17SkillCapability.HasCost, P17SkillRole.Guard,
            P17DamageType.None, P17SkillShape.Self, 18, 0, 6, 140, 0, P17Ailment.None, 0,
            "降低25%元素与虚空伤害，并减少50%异常积累。"),
        A("elemental_resonance", "元素共鸣", SkillTag.Aura | SkillTag.Reservation | SkillTag.Area | SkillTag.Elemental,
            P17SkillCapability.Reservation | P17SkillCapability.Area | P17SkillCapability.Duration |
            P17SkillCapability.ElementalDamage, P17SkillRole.Reservation, P17DamageType.None,
            P17SkillShape.Self, 0, 8_000, 4, 0, 0, P17Ailment.None, 0,
            "保留20%法力，提高三元素伤害与三元素抗性。"),
    ];

    public static IReadOnlyList<P17SupportSkillDefinition> Supports { get; } =
    [
        S("increased_area", "扩大范围", SkillSupport.IncreasedArea, SkillTag.Area, any: P17SkillCapability.Area,
            description: "范围提高35%，最终伤害总降10%。", starter: true),
        S("attack_speed", "攻击速度", SkillSupport.AttackSpeed, SkillTag.Attack,
            all: P17SkillCapability.Attack | P17SkillCapability.Repeatable, excluded: P17SkillCapability.Counter,
            description: "攻击速度提高25%。", starter: true),
        S("bleed", "流血", SkillSupport.Bleed, SkillTag.Attack | SkillTag.Physical,
            all: P17SkillCapability.Hit | P17SkillCapability.CanBleed, description: "流血概率提高60%。",
            provides: P17SupportConflict.GrantsBleed, conflicts: P17SupportConflict.PreventsBleed, starter: true),
        S("life_cost", "生命消耗", SkillSupport.LifeCost, SkillTag.Attack | SkillTag.Spell,
            all: P17SkillCapability.HasCost, excluded: P17SkillCapability.Reservation,
            description: "改为消耗150%原法力数值的生命，伤害总增20%。", starter: true),
        S("chain", "追加连锁", SkillSupport.Chain, SkillTag.Projectile | SkillTag.Chaining,
            any: P17SkillCapability.Projectile | P17SkillCapability.Chain, description: "追加两次连锁。", starter: true),
        S("brutality", "残暴", SkillSupport.Brutality, SkillTag.Physical, any: P17SkillCapability.PhysicalDamage,
            description: "物理伤害总增35%，无法造成非物理伤害。", provides: P17SupportConflict.PhysicalOnly,
            conflicts: P17SupportConflict.NonPhysical),
        S("multiple_projectiles", "多重投射", SkillSupport.MultipleProjectiles, SkillTag.Projectile,
            all: P17SkillCapability.Projectile, description: "额外两个投射物，单发伤害总降20%。"),
        S("faster_projectiles", "极速投射", SkillSupport.FasterProjectiles, SkillTag.Projectile,
            all: P17SkillCapability.Projectile, description: "投射物速度提高50%，距离提高15%。"),
        S("urgent_war_cry", "急促战吼", SkillSupport.UrgentWarCry, SkillTag.WarCry,
            all: P17SkillCapability.WarCry, description: "冷却恢复提高30%，效果总降15%。"),
        S("life_leech", "血之汲取", SkillSupport.LifeLeech, SkillTag.Attack,
            all: P17SkillCapability.Attack | P17SkillCapability.Hit, description: "命中伤害的2%转为生命，消耗提高20%。"),
        S("execution", "处决", SkillSupport.Execution, SkillTag.Attack | SkillTag.Spell,
            all: P17SkillCapability.Hit, description: "低于20%生命时伤害总增40%，否则总降10%。"),
        S("spell_echo", "法术回响", SkillSupport.SpellEcho, SkillTag.Spell,
            all: P17SkillCapability.Spell | P17SkillCapability.Hit,
            excluded: P17SkillCapability.Movement | P17SkillCapability.Guard | P17SkillCapability.Reservation |
                      P17SkillCapability.WarCry | P17SkillCapability.Counter,
            description: "重复施放一次，每次伤害总降18%。"),
        S("elemental_focus", "元素集中", SkillSupport.ElementalFocus, SkillTag.Elemental,
            any: P17SkillCapability.ElementalDamage, description: "元素伤害总增28%，无法施加元素异常。"),
        S("added_fire", "附加火焰", SkillSupport.AddedFire, SkillTag.Attack | SkillTag.Spell,
            all: P17SkillCapability.Hit | P17SkillCapability.PhysicalDamage,
            description: "获得18%物理伤害的额外火焰伤害。", provides: P17SupportConflict.NonPhysical,
            conflicts: P17SupportConflict.PhysicalOnly),
        S("added_cold", "附加寒霜", SkillSupport.AddedCold, SkillTag.Attack | SkillTag.Spell,
            all: P17SkillCapability.Hit, description: "附加寒霜伤害并获得15%缓速概率。",
            provides: P17SupportConflict.NonPhysical, conflicts: P17SupportConflict.PhysicalOnly),
        S("added_lightning", "附加闪电", SkillSupport.AddedLightning, SkillTag.Attack | SkillTag.Spell,
            all: P17SkillCapability.Hit, description: "附加闪电伤害并获得15%感电概率。",
            provides: P17SupportConflict.NonPhysical, conflicts: P17SupportConflict.PhysicalOnly),
        S("critical_strikes", "精准暴击", SkillSupport.CriticalStrikes, SkillTag.Attack | SkillTag.Spell,
            all: P17SkillCapability.Hit | P17SkillCapability.CanCrit, description: "基础暴击率提高4%，暴击伤害总增12%。"),
        S("concentrated_effect", "集中效应", SkillSupport.ConcentratedEffect, SkillTag.Area,
            all: P17SkillCapability.Area | P17SkillCapability.Damage, description: "范围缩小25%，范围伤害总增32%。"),
        S("heavy_momentum", "重势", SkillSupport.HeavyMomentum, SkillTag.Melee,
            all: P17SkillCapability.Melee | P17SkillCapability.Hit, excluded: P17SkillCapability.Channelling,
            description: "命中伤害总增45%，攻击速度总降15%。"),
        S("triple_impact", "三叠重击", SkillSupport.TripleImpact, SkillTag.Attack,
            all: P17SkillCapability.Attack | P17SkillCapability.Repeatable, excluded: P17SkillCapability.Counter,
            description: "每第三次伤害总增80%，眩晕积累翻倍。"),
        S("tremor_field", "震域", SkillSupport.TremorField, SkillTag.Slam,
            all: P17SkillCapability.Slam | P17SkillCapability.Area, description: "范围提高30%、命中总增25%，速度总降15%。"),
        S("shockwave", "余波", SkillSupport.Shockwave, SkillTag.Melee | SkillTag.Area,
            all: P17SkillCapability.Melee | P17SkillCapability.Hit, description: "首次命中产生60%武器倍率余波，冷却1秒。"),
        S("close_combat", "贴身搏杀", SkillSupport.CloseCombat, SkillTag.Melee,
            all: P17SkillCapability.Melee | P17SkillCapability.Attack, excluded: P17SkillCapability.Projectile,
            description: "贴身时伤害最多总增35%。"),
        S("armor_shatter", "裂甲", SkillSupport.ArmorShatter, SkillTag.Physical | SkillTag.ArmorBreak,
            all: P17SkillCapability.Hit | P17SkillCapability.CanArmorBreak, description: "命中施加一层破甲，命中总降10%。"),
        S("armor_pierce", "透甲", SkillSupport.ArmorPierce, SkillTag.Physical,
            all: P17SkillCapability.Hit | P17SkillCapability.PhysicalDamage, description: "忽略30%护甲，持续伤害总降15%。"),
        S("suppression", "镇压", SkillSupport.Suppression, SkillTag.Stun,
            all: P17SkillCapability.Hit | P17SkillCapability.CanStun, description: "眩晕积累提高80%，命中总降15%。"),
        S("stun_spread", "震荡蔓延", SkillSupport.StunSpread, SkillTag.Stun | SkillTag.Area,
            all: P17SkillCapability.CanStun, description: "眩晕时向三米内普通敌人传播较弱眩晕。"),
        S("deep_wound", "深创", SkillSupport.DeepWound, SkillTag.Physical | SkillTag.Bleed,
            all: P17SkillCapability.Hit | P17SkillCapability.CanBleed, description: "流血概率提高50%、流血总增25%，命中总降10%。",
            provides: P17SupportConflict.GrantsBleed, conflicts: P17SupportConflict.PreventsBleed),
        S("swift_bleed", "疾血", SkillSupport.SwiftBleed, SkillTag.Bleed | SkillTag.Duration,
            all: P17SkillCapability.CanBleed, description: "流血造成伤害速度提高35%，持续时间总降25%。",
            provides: P17SupportConflict.GrantsBleed, conflicts: P17SupportConflict.PreventsBleed),
        S("bleed_spread", "血痕播散", SkillSupport.BleedSpread, SkillTag.Bleed | SkillTag.Area,
            all: P17SkillCapability.CanBleed, description: "击杀时传播70%最强剩余流血。",
            provides: P17SupportConflict.GrantsBleed, conflicts: P17SupportConflict.PreventsBleed),
        S("cruelty", "残酷", SkillSupport.Cruelty, SkillTag.Attack | SkillTag.Spell | SkillTag.Duration,
            all: P17SkillCapability.Hit | P17SkillCapability.Duration, description: "强力命中使持续伤害最多总增30%。"),
        S("bloodlust", "嗜血", SkillSupport.Bloodlust, SkillTag.Physical,
            all: P17SkillCapability.Hit | P17SkillCapability.PhysicalDamage, description: "对流血目标总增35%，被辅助技能无法流血。",
            provides: P17SupportConflict.PreventsBleed, conflicts: P17SupportConflict.GrantsBleed),
        S("trauma", "创伤积压", SkillSupport.Trauma, SkillTag.Attack | SkillTag.Melee,
            all: P17SkillCapability.Melee | P17SkillCapability.Repeatable, description: "累积创伤换取物理增伤并承受自伤。"),
        S("fortification", "坚阵", SkillSupport.Fortification, SkillTag.Attack | SkillTag.Melee,
            all: P17SkillCapability.Melee | P17SkillCapability.Hit, description: "近战命中累积最多10%命中减伤。"),
        S("vengeance", "复仇增幅", SkillSupport.Vengeance, SkillTag.Counter,
            all: P17SkillCapability.Counter, description: "反击总增40%，冷却恢复提高25%。"),
        S("block_trigger", "格挡触发", SkillSupport.BlockTrigger, SkillTag.Trigger | SkillTag.Guard | SkillTag.Counter,
            all: P17SkillCapability.Triggerable, excluded: P17SkillCapability.Reservation | P17SkillCapability.WarCry,
            description: "成功格挡时自动使用，效果总降25%，冷却1.5秒。",
            provides: P17SupportConflict.Trigger, conflicts: P17SupportConflict.Trigger),
        S("war_cry_potency", "号令增幅", SkillSupport.WarCryPotency, SkillTag.WarCry,
            all: P17SkillCapability.WarCry, description: "战吼效果提高30%、持续提高25%，消耗提高20%。"),
        S("war_cry_echo", "回声战吼", SkillSupport.WarCryEcho, SkillTag.WarCry,
            all: P17SkillCapability.WarCry, description: "0.6秒后以60%效果重复，基础冷却提高40%。"),
        S("banner_potency", "誓旗增幅", SkillSupport.BannerPotency, SkillTag.Aura | SkillTag.Reservation,
            all: P17SkillCapability.Reservation, description: "光环效果提高30%，额外保留5%法力。"),
        S("pierce", "贯穿", SkillSupport.Pierce, SkillTag.Projectile,
            all: P17SkillCapability.Projectile, description: "额外贯穿两个敌人，每次贯穿后总降8%。"),
        S("fork", "裂射", SkillSupport.Fork, SkillTag.Projectile,
            all: P17SkillCapability.Projectile, description: "首次命中分裂成两个伤害总降30%的子投射物。"),
        S("return", "归返", SkillSupport.Return, SkillTag.Projectile | SkillTag.Returning,
            all: P17SkillCapability.Projectile, description: "投射物返回，返回伤害总降25%。"),
        S("faster_casting", "疾咏", SkillSupport.FasterCasting, SkillTag.Spell,
            all: P17SkillCapability.Spell | P17SkillCapability.HasCost, description: "施法速度提高25%，法力消耗提高10%。"),
        S("physical_to_lightning", "雷铸转化", SkillSupport.PhysicalToLightning, SkillTag.Physical | SkillTag.Lightning,
            all: P17SkillCapability.PhysicalDamage, description: "50%物理伤害转化为闪电。",
            provides: P17SupportConflict.NonPhysical, conflicts: P17SupportConflict.PhysicalOnly),
        S("lightning_to_cold", "霜流转化", SkillSupport.LightningToCold, SkillTag.Lightning | SkillTag.Cold,
            all: P17SkillCapability.LightningDamage, description: "50%闪电伤害转化为寒霜。",
            provides: P17SupportConflict.NonPhysical, conflicts: P17SupportConflict.PhysicalOnly),
        S("cold_to_fire", "焰化转化", SkillSupport.ColdToFire, SkillTag.Cold | SkillTag.Fire,
            all: P17SkillCapability.ColdDamage, description: "50%寒霜伤害转化为火焰。",
            provides: P17SupportConflict.NonPhysical, conflicts: P17SupportConflict.PhysicalOnly),
        S("fire_to_void", "虚蚀转化", SkillSupport.FireToVoid, SkillTag.Fire | SkillTag.Void,
            all: P17SkillCapability.FireDamage, description: "50%火焰伤害转化为虚空。",
            provides: P17SupportConflict.NonPhysical, conflicts: P17SupportConflict.PhysicalOnly),
        S("cast_when_damaged", "受创触发", SkillSupport.CastWhenDamaged, SkillTag.Trigger | SkillTag.Spell | SkillTag.Guard,
            all: P17SkillCapability.Triggerable, excluded: P17SkillCapability.Movement | P17SkillCapability.Reservation |
                P17SkillCapability.WarCry | P17SkillCapability.Counter,
            description: "累计承受20%最大生命命中伤害后自动使用，效果总降30%。",
            provides: P17SupportConflict.Trigger, conflicts: P17SupportConflict.Trigger),
    ];

    private static readonly IReadOnlyDictionary<string, P17ActiveSkillDefinition> ActiveByStone =
        Active.ToDictionary(item => item.StoneId, StringComparer.Ordinal);
    private static readonly IReadOnlyDictionary<string, P17ActiveSkillDefinition> ActiveBySkill =
        Active.ToDictionary(item => item.SkillId, StringComparer.Ordinal);
    private static readonly IReadOnlyDictionary<string, P17SupportSkillDefinition> SupportByStone =
        Supports.ToDictionary(item => item.StoneId, StringComparer.Ordinal);

    static P17SkillCatalog()
    {
        if (Active.Count != 30 || Supports.Count != 48 ||
            ActiveByStone.Count != Active.Count || ActiveBySkill.Count != Active.Count || SupportByStone.Count != Supports.Count)
            throw new InvalidDataException("P17 skill catalog must contain 30 unique active and 48 unique support skills.");
    }

    public static P17ActiveSkillDefinition ActiveForStone(string stoneId) => ActiveByStone.TryGetValue(stoneId, out var value)
        ? value : throw new KeyNotFoundException($"Unknown active skill stone: {stoneId}");
    public static P17ActiveSkillDefinition ActiveForSkill(string skillId) => ActiveBySkill.TryGetValue(skillId, out var value)
        ? value : throw new KeyNotFoundException($"Unknown active skill: {skillId}");
    public static P17SupportSkillDefinition SupportForStone(string stoneId) => SupportByStone.TryGetValue(stoneId, out var value)
        ? value : throw new KeyNotFoundException($"Unknown support skill stone: {stoneId}");

    private static P17ActiveSkillDefinition A(string suffix, string name, SkillTag tags, P17SkillCapability capabilities,
        P17SkillRole role, P17DamageType damageType, P17SkillShape shape, int mana, int range, int cast, int cooldown,
        int damage, P17Ailment ailment, int ailmentChance, string description, bool starter = false) =>
        new($"core.skill_stone.{suffix}", $"core.skill.{suffix}", name, tags, capabilities, role, damageType, shape,
            mana, range, cast, cooldown, damage, ailment, ailmentChance, description, starter);

    private static P17SupportSkillDefinition S(string suffix, string name, SkillSupport support, SkillTag tags,
        P17SkillCapability all = P17SkillCapability.None, P17SkillCapability any = P17SkillCapability.None,
        P17SkillCapability excluded = P17SkillCapability.None, string description = "",
        P17SupportConflict provides = P17SupportConflict.None, P17SupportConflict conflicts = P17SupportConflict.None,
        bool starter = false) => new($"core.skill_stone.{suffix}", name, support, tags, all, any, excluded,
            description, provides, conflicts, starter);
}
