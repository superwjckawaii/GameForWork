using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.SkillCatalog;
using GameForWork.Core.Characters;

namespace GameForWork.Core.Archetypes;

public enum SkillMechanic
{
    Projectile, Barrage, ReturningProjectile, MobileAttack, Mark, Backstab, Poison, Trap, Minion,
    Aura, Blessing, WarSong, Curse, Hex, ElementalSpell, VoidSpell, EnergyShield, Counter,
    Combo, Stance, Finisher, Companion, CompanionStance, Phantom, Rune, Trigger, SpellArmor,
    Construct, Rebuild,
}

public enum SupportMechanic
{
    None,
    FarShot, PrecisionPierce, SeekingChain, MobileAttack, ToxinSpread, MultipleTraps, MarkAmplify, BackstabAmplify,
    MinionAmplify, SwiftMinions, ExpandedArmy, Bodyguard, AuraAmplify, LastingBlessing, HexSpread, DeepHex,
    FirePenetration, ColdPenetration, LightningPenetration, ElementalAilment, VoidDuration, DeepWither,
    ShieldLeech, ShieldCasting, UnarmedFocus, ComboDuration, StanceAmplify, MovementEcho, FerociousBeast,
    GuardianBeast, PhantomCopy, PhantomSacrifice, Spellblade, AttackTrigger, ImprintGain, ImprintBurst,
    SpellArmorFusion, ShieldBreakAmplify, ConstructAmplify, RapidRebuild,
}

public sealed record ArchetypeSkillDefinition(
    BaseClass Theme,
    SkillMechanic Mechanic,
    SkillCombatDefinition Combat);

public sealed record ArchetypeSupportDefinition(
    string StoneId,
    string DisplayName,
    SupportMechanic Mechanic,
    SkillCapability RequiredAll,
    SkillCapability RequiredAny,
    SkillCapability Excluded,
    string Description);

public static class ArchetypeSkillDefinitions
{
    private const SkillCapability AttackHit = SkillCapability.Damage | SkillCapability.Hit |
        SkillCapability.Attack | SkillCapability.CanCrit | SkillCapability.HasCost;
    private const SkillCapability SpellHit = SkillCapability.Damage | SkillCapability.Hit |
        SkillCapability.Spell | SkillCapability.CanCrit | SkillCapability.HasCost;

    public static IReadOnlyList<ArchetypeSkillDefinition> Active { get; } =
    [
        // 侠客：投射、标记、背袭、毒素和陷阱。
        A(BaseClass.Rogue, "cloudpiercer_arrow", "穿云箭", SkillMechanic.Projectile, SkillTag.Attack | SkillTag.Projectile,
            AttackHit | SkillCapability.Projectile, SkillRole.SingleTarget, SkillDamageType.Physical, SkillShape.Projectile, 10, 10_000, 2, 12, 14_000, "贯穿两名敌人的高威力箭矢。"),
        A(BaseClass.Rogue, "gale_barrage", "疾风连射", SkillMechanic.Barrage, SkillTag.Attack | SkillTag.Projectile | SkillTag.Channelling,
            AttackHit | SkillCapability.Projectile | SkillCapability.Channelling | SkillCapability.Repeatable, SkillRole.Clear, SkillDamageType.Physical, SkillShape.Projectile, 12, 9_000, 2, 10, 7_000, "快速射出三支箭，允许集中攻击同一目标。"),
        A(BaseClass.Rogue, "returning_arrow", "折返箭", SkillMechanic.ReturningProjectile, SkillTag.Attack | SkillTag.Projectile | SkillTag.Returning,
            AttackHit | SkillCapability.Projectile, SkillRole.Clear, SkillDamageType.Physical, SkillShape.Projectile, 11, 9_000, 3, 16, 10_500, "箭矢抵达终点后折返并再次命中。"),
        A(BaseClass.Rogue, "windwalk_shot", "风行射击", SkillMechanic.MobileAttack, SkillTag.Attack | SkillTag.Projectile | SkillTag.Movement,
            AttackHit | SkillCapability.Projectile | SkillCapability.Movement, SkillRole.Movement, SkillDamageType.Physical, SkillShape.Projectile, 13, 8_000, 1, 30, 9_500, "向后滑步并朝目标射击。"),
        A(BaseClass.Rogue, "death_mark", "死亡标记", SkillMechanic.Mark, SkillTag.Spell | SkillTag.Duration,
            SkillCapability.Spell | SkillCapability.Duration | SkillCapability.HasCost, SkillRole.DamageOverTime, SkillDamageType.None, SkillShape.Single, 16, 12_000, 4, 80, 0, "标记优先目标，使其承受更多来自你的命中伤害。"),
        A(BaseClass.Rogue, "shadow_assault", "影袭", SkillMechanic.Backstab, SkillTag.Attack | SkillTag.Melee | SkillTag.Movement,
            AttackHit | SkillCapability.Melee | SkillCapability.Movement, SkillRole.Movement, SkillDamageType.Physical, SkillShape.Single, 12, 6_000, 1, 24, 13_500, "闪至目标背后发动近战攻击。"),
        A(BaseClass.Rogue, "backstab", "背刺", SkillMechanic.Backstab, SkillTag.Attack | SkillTag.Melee | SkillTag.Strike,
            AttackHit | SkillCapability.Melee, SkillRole.SingleTarget, SkillDamageType.Physical, SkillShape.Single, 10, 1_500, 1, 12, 18_000, "从背后命中时获得独立的更多伤害。"),
        A(BaseClass.Rogue, "venom_blades", "淬毒飞刃", SkillMechanic.Poison, SkillTag.Attack | SkillTag.Projectile | SkillTag.Void,
            AttackHit | SkillCapability.Projectile | SkillCapability.VoidDamage | SkillCapability.Duration, SkillRole.Clear, SkillDamageType.Void, SkillShape.Projectile, 11, 8_500, 2, 14, 9_500, "投出淬毒飞刃并叠加腐蚀毒素。", Ailment.Erosion, 6_000),
        A(BaseClass.Rogue, "corrosive_trap", "腐蚀陷阱", SkillMechanic.Trap, SkillTag.Attack | SkillTag.Area | SkillTag.Duration | SkillTag.Void,
            AttackHit | SkillCapability.Area | SkillCapability.Duration | SkillCapability.Triggerable | SkillCapability.VoidDamage, SkillRole.DamageOverTime, SkillDamageType.Void, SkillShape.GroundArea, 14, 7_000, 3, 28, 8_500, "布置持续八秒、敌人进入两米时触发的腐蚀陷阱。", Ailment.Erosion, 8_000),
        A(BaseClass.Rogue, "plague_detonation", "瘟疫引爆", SkillMechanic.Poison, SkillTag.Spell | SkillTag.Area | SkillTag.Void,
            SpellHit | SkillCapability.Area | SkillCapability.VoidDamage, SkillRole.Clear, SkillDamageType.Void, SkillShape.Circle, 18, 8_000, 5, 36, 12_000, "消耗目标的毒素层数并造成范围爆发。"),

        // 灵能使：召唤、光环、祝福、战歌与诅咒。
        A(BaseClass.Psion, "summon_boneguard", "召唤骸卫", SkillMechanic.Minion, SkillTag.Spell | SkillTag.Duration,
            SkillCapability.Spell | SkillCapability.Duration | SkillCapability.HasCost, SkillRole.Guard, SkillDamageType.Physical, SkillShape.Self, 18, 9_000, 8, 40, 8_000, "召唤持盾骸卫吸引近身敌人。"),
        A(BaseClass.Psion, "summon_soulbow", "召唤魂弓", SkillMechanic.Minion, SkillTag.Spell | SkillTag.Projectile | SkillTag.Duration,
            SkillCapability.Spell | SkillCapability.Projectile | SkillCapability.Duration | SkillCapability.HasCost, SkillRole.Clear, SkillDamageType.Void, SkillShape.Self, 18, 9_000, 8, 40, 9_000, "召唤优先攻击远处目标的魂弓。"),
        A(BaseClass.Psion, "bone_harvest", "亡骸收割", SkillMechanic.Minion, SkillTag.Spell | SkillTag.Area | SkillTag.Void,
            SpellHit | SkillCapability.Area | SkillCapability.VoidDamage, SkillRole.Clear, SkillDamageType.Void, SkillShape.Circle, 16, 7_000, 4, 24, 11_500, "命令全部召唤物突袭同一目标并收割周围亡骸。"),
        A(BaseClass.Psion, "king_soul_command", "王魂号令", SkillMechanic.Minion, SkillTag.Spell | SkillTag.Buff | SkillTag.Area,
            SkillCapability.Spell | SkillCapability.Area | SkillCapability.Duration | SkillCapability.HasCost, SkillRole.WarCry, SkillDamageType.None, SkillShape.Self, 20, 10_000, 5, 100, 0, "号令召唤军团集火稀有怪与Boss。"),
        A(BaseClass.Psion, "courage_hymn", "勇气颂歌", SkillMechanic.Aura, SkillTag.Spell | SkillTag.Aura | SkillTag.Reservation | SkillTag.Buff,
            SkillCapability.Spell | SkillCapability.Reservation | SkillCapability.Duration, SkillRole.Reservation, SkillDamageType.None, SkillShape.Self, 0, 9_000, 3, 0, 0, "保留法力，为实际参战单位提供防御与抗控。"),
        A(BaseClass.Psion, "fellowship_blessing", "同行祝福", SkillMechanic.Blessing, SkillTag.Spell | SkillTag.Aura | SkillTag.Duration | SkillTag.Buff,
            SkillCapability.Spell | SkillCapability.Duration | SkillCapability.HasCost, SkillRole.Reservation, SkillDamageType.None, SkillShape.Self, 24, 9_000, 5, 100, 0, "短时间强化主角、允许参战的佣兵与召唤单位。"),
        A(BaseClass.Psion, "soul_warsong", "灵魂战歌", SkillMechanic.WarSong, SkillTag.Spell | SkillTag.Area | SkillTag.Buff,
            SkillCapability.Spell | SkillCapability.Area | SkillCapability.Duration | SkillCapability.HasCost, SkillRole.WarCry, SkillDamageType.None, SkillShape.Self, 20, 10_000, 4, 80, 0, "战歌在场时提高召唤物攻击与施法速度。"),
        A(BaseClass.Psion, "enfeeble_hex", "衰弱咒", SkillMechanic.Curse, SkillTag.Spell | SkillTag.Area | SkillTag.Duration | SkillTag.Void,
            SkillCapability.Spell | SkillCapability.Area | SkillCapability.Duration | SkillCapability.HasCost, SkillRole.DamageOverTime, SkillDamageType.None, SkillShape.Circle, 16, 9_000, 4, 32, 0, "使范围内敌人造成的命中伤害降低。"),
        A(BaseClass.Psion, "elemental_hex", "元素咒", SkillMechanic.Curse, SkillTag.Spell | SkillTag.Area | SkillTag.Duration,
            SkillCapability.Spell | SkillCapability.Area | SkillCapability.Duration | SkillCapability.HasCost, SkillRole.DamageOverTime, SkillDamageType.None, SkillShape.Circle, 16, 9_000, 4, 32, 0, "降低范围内敌人的火焰、寒霜与闪电抗性。"),
        A(BaseClass.Psion, "doom_brand", "末日咒印", SkillMechanic.Hex, SkillTag.Spell | SkillTag.Duration | SkillTag.Void,
            SpellHit | SkillCapability.Duration | SkillCapability.VoidDamage | SkillCapability.Triggerable, SkillRole.DamageOverTime, SkillDamageType.Void, SkillShape.Single, 22, 10_000, 6, 50, 15_000, "咒印到期或目标死亡时爆炸并传播。", Ailment.Wither, 10_000),

        // 秘术师：元素、虚空与能量护盾。
        A(BaseClass.Occultist, "molten_orb", "熔火弹", SkillMechanic.ElementalSpell, SkillTag.Spell | SkillTag.Projectile | SkillTag.Fire,
            SpellHit | SkillCapability.Projectile | SkillCapability.FireDamage, SkillRole.Clear, SkillDamageType.Fire, SkillShape.Projectile, 12, 9_500, 5, 18, 13_000, "投射熔火弹，命中后造成小范围爆炸。", Ailment.Ignite, 4_000),
        A(BaseClass.Occultist, "ice_lance", "冰矛", SkillMechanic.ElementalSpell, SkillTag.Spell | SkillTag.Projectile | SkillTag.Cold,
            SpellHit | SkillCapability.Projectile | SkillCapability.ColdDamage, SkillRole.SingleTarget, SkillDamageType.Cold, SkillShape.Projectile, 13, 11_000, 5, 18, 14_500, "远距离获得更高暴击率的寒霜长矛。", Ailment.Freeze, 2_500),
        A(BaseClass.Occultist, "thunderstorm", "雷暴", SkillMechanic.ElementalSpell, SkillTag.Spell | SkillTag.Area | SkillTag.Lightning | SkillTag.Duration,
            SpellHit | SkillCapability.Area | SkillCapability.Duration | SkillCapability.LightningDamage, SkillRole.Clear, SkillDamageType.Lightning, SkillShape.GroundArea, 17, 10_000, 7, 32, 9_000, "召唤持续落雷的区域。", Ailment.Shock, 5_000),
        A(BaseClass.Occultist, "elemental_prism", "元素棱镜", SkillMechanic.ElementalSpell, SkillTag.Spell | SkillTag.Projectile | SkillTag.Elemental,
            SpellHit | SkillCapability.Projectile | SkillCapability.ElementalDamage, SkillRole.Clear, SkillDamageType.Lightning, SkillShape.Projectile, 16, 10_000, 6, 24, 12_000, "按当前主要元素发射棱镜射线。"),
        A(BaseClass.Occultist, "void_rift", "虚空裂隙", SkillMechanic.VoidSpell, SkillTag.Spell | SkillTag.Area | SkillTag.Void | SkillTag.Duration,
            SpellHit | SkillCapability.Area | SkillCapability.VoidDamage | SkillCapability.Duration, SkillRole.DamageOverTime, SkillDamageType.Void, SkillShape.GroundArea, 18, 9_000, 7, 36, 8_500, "撕开持续造成虚空伤害的裂隙。", Ailment.Wither, 4_000),
        A(BaseClass.Occultist, "withering_ray", "凋零射线", SkillMechanic.VoidSpell, SkillTag.Spell | SkillTag.Channelling | SkillTag.Void,
            SpellHit | SkillCapability.Channelling | SkillCapability.VoidDamage | SkillCapability.Repeatable, SkillRole.SingleTarget, SkillDamageType.Void, SkillShape.Projectile, 10, 9_000, 2, 8, 7_500, "持续射线快速叠加凋零。", Ailment.Wither, 7_000),
        A(BaseClass.Occultist, "forbidden_collapse", "禁术坍缩", SkillMechanic.VoidSpell, SkillTag.Spell | SkillTag.Area | SkillTag.Void,
            SpellHit | SkillCapability.Area | SkillCapability.VoidDamage, SkillRole.SingleTarget, SkillDamageType.Void, SkillShape.Circle, 26, 9_000, 10, 70, 21_000, "消耗附近凋零层数造成禁术坍缩。"),
        A(BaseClass.Occultist, "aegis_pulse", "秘盾脉冲", SkillMechanic.EnergyShield, SkillTag.Spell | SkillTag.Area | SkillTag.Guard,
            SpellHit | SkillCapability.Area | SkillCapability.Guard, SkillRole.Guard, SkillDamageType.Lightning, SkillShape.Circle, 18, 5_000, 4, 45, 10_500, "消耗少量能量护盾释放脉冲并获得吸收屏障。"),
        A(BaseClass.Occultist, "shield_drain", "护盾汲取", SkillMechanic.EnergyShield, SkillTag.Spell | SkillTag.Channelling | SkillTag.Void,
            SpellHit | SkillCapability.Channelling | SkillCapability.VoidDamage, SkillRole.SingleTarget, SkillDamageType.Void, SkillShape.Projectile, 8, 8_000, 2, 8, 6_500, "造成的部分伤害恢复能量护盾。"),
        A(BaseClass.Occultist, "mirror_counter", "镜式反击", SkillMechanic.Counter, SkillTag.Spell | SkillTag.Counter | SkillTag.Guard,
            SpellHit | SkillCapability.Counter | SkillCapability.Guard | SkillCapability.Triggerable, SkillRole.Counter, SkillDamageType.Lightning, SkillShape.Single, 0, 10_000, 1, 50, 16_000, "能量护盾被命中时反射镜式法术。"),

        // 僧侣：徒手连击、姿态、灵兽和幻身。
        A(BaseClass.Monk, "chain_fists", "连环拳", SkillMechanic.Combo, SkillTag.Attack | SkillTag.Melee | SkillTag.Strike,
            AttackHit | SkillCapability.Melee | SkillCapability.Repeatable, SkillRole.SingleTarget, SkillDamageType.Physical, SkillShape.Single, 7, 1_500, 0, 7, 7_000, "快速徒手连击并积累连击层数。"),
        A(BaseClass.Monk, "skyquake_palm", "震空掌", SkillMechanic.Combo, SkillTag.Attack | SkillTag.Melee | SkillTag.Area,
            AttackHit | SkillCapability.Melee | SkillCapability.Area, SkillRole.Clear, SkillDamageType.Physical, SkillShape.Cone, 12, 3_500, 2, 20, 13_000, "掌风震击前方敌人，连击层数提高范围。", Ailment.Stun, 4_000),
        A(BaseClass.Monk, "gale_kick", "追风踢", SkillMechanic.Combo, SkillTag.Attack | SkillTag.Melee | SkillTag.Movement,
            AttackHit | SkillCapability.Melee | SkillCapability.Movement, SkillRole.Movement, SkillDamageType.Physical, SkillShape.MovementCircle, 10, 5_000, 1, 20, 11_000, "追向远处目标并维持连击。"),
        A(BaseClass.Monk, "yin_yang_stance", "阴阳架势", SkillMechanic.Stance, SkillTag.Attack | SkillTag.Buff | SkillTag.Duration,
            SkillCapability.Attack | SkillCapability.Duration, SkillRole.Reservation, SkillDamageType.None, SkillShape.Self, 0, 1_500, 1, 8, 0, "在进攻阳式与防守阴式间切换。"),
        A(BaseClass.Monk, "tenfold_finisher", "十方终式", SkillMechanic.Finisher, SkillTag.Attack | SkillTag.Melee | SkillTag.Area,
            AttackHit | SkillCapability.Melee | SkillCapability.Area, SkillRole.SingleTarget, SkillDamageType.Physical, SkillShape.Circle, 20, 4_000, 5, 40, 19_000, "消耗全部连击层数发动十方终式。"),
        A(BaseClass.Monk, "summon_spirit_beast", "召唤灵兽", SkillMechanic.Companion, SkillTag.Spell | SkillTag.Duration,
            SkillCapability.Spell | SkillCapability.Duration | SkillCapability.HasCost, SkillRole.Guard, SkillDamageType.Physical, SkillShape.Self, 22, 9_000, 8, 80, 10_000, "召唤唯一灵兽伙伴；灵兽不占召唤物上限。"),
        A(BaseClass.Monk, "beast_shapeshift", "灵兽变相", SkillMechanic.CompanionStance, SkillTag.Spell | SkillTag.Buff | SkillTag.Duration,
            SkillCapability.Spell | SkillCapability.Duration | SkillCapability.HasCost, SkillRole.WarCry, SkillDamageType.None, SkillShape.Self, 16, 9_000, 4, 50, 0, "在凶猛与守护形态间切换灵兽。"),
        A(BaseClass.Monk, "twin_soul_pincer", "双魂夹击", SkillMechanic.Companion, SkillTag.Attack | SkillTag.Melee,
            AttackHit | SkillCapability.Melee, SkillRole.SingleTarget, SkillDamageType.Physical, SkillShape.Single, 14, 6_000, 2, 22, 15_000, "与灵兽从两侧夹击同一目标。"),
        A(BaseClass.Monk, "phantom_step", "幻身步", SkillMechanic.Phantom, SkillTag.Attack | SkillTag.Movement | SkillTag.Duration,
            SkillCapability.Attack | SkillCapability.Movement | SkillCapability.Duration, SkillRole.Movement, SkillDamageType.Physical, SkillShape.MovementCircle, 12, 6_000, 1, 24, 7_500, "位移后留下短暂复现本次攻击的幻身。"),
        A(BaseClass.Monk, "hundred_shadows", "百影合击", SkillMechanic.Phantom, SkillTag.Attack | SkillTag.Melee | SkillTag.Area,
            AttackHit | SkillCapability.Melee | SkillCapability.Area, SkillRole.Clear, SkillDamageType.Physical, SkillShape.Circle, 24, 5_000, 5, 60, 17_000, "命令最多六个幻身同时攻击。"),

        // 隐士：符文、触发、魔铠与构装。
        A(BaseClass.Hermit, "runeblade_slash", "符刃斩", SkillMechanic.Rune, SkillTag.Attack | SkillTag.Melee | SkillTag.Elemental,
            AttackHit | SkillCapability.Melee | SkillCapability.ElementalDamage, SkillRole.SingleTarget, SkillDamageType.Fire, SkillShape.Cone, 10, 2_500, 2, 14, 14_000, "按当前刻印元素进行符刃斩击。"),
        A(BaseClass.Hermit, "elemental_imprint", "元素刻印", SkillMechanic.Rune, SkillTag.Spell | SkillTag.Buff | SkillTag.Elemental,
            SkillCapability.Spell | SkillCapability.Duration | SkillCapability.HasCost, SkillRole.Reservation, SkillDamageType.None, SkillShape.Self, 8, 2_000, 2, 12, 0, "切换主要元素并积累对应刻印。"),
        A(BaseClass.Hermit, "answering_formula", "应答术式", SkillMechanic.Trigger, SkillTag.Spell | SkillTag.Trigger | SkillTag.Elemental,
            SpellHit | SkillCapability.Triggerable | SkillCapability.ElementalDamage, SkillRole.Counter, SkillDamageType.Lightning, SkillShape.Projectile, 0, 8_000, 1, 22, 9_000, "攻击命中后自动释放当前元素术式。"),
        A(BaseClass.Hermit, "sixfold_burst", "六重刻爆", SkillMechanic.Rune, SkillTag.Spell | SkillTag.Area | SkillTag.Elemental,
            SpellHit | SkillCapability.Area | SkillCapability.ElementalDamage, SkillRole.Clear, SkillDamageType.Fire, SkillShape.Circle, 22, 7_000, 6, 45, 18_000, "消耗六层刻印引爆元素环。"),
        A(BaseClass.Hermit, "spellarmor_activate", "魔铠启动", SkillMechanic.SpellArmor, SkillTag.Spell | SkillTag.Guard | SkillTag.Buff,
            SkillCapability.Spell | SkillCapability.Guard | SkillCapability.Duration | SkillCapability.HasCost, SkillRole.Guard, SkillDamageType.None, SkillShape.Self, 18, 2_000, 4, 80, 0, "启动魔铠，将部分能量护盾转为铠能。"),
        A(BaseClass.Hermit, "spellarmor_overload", "魔铠过载", SkillMechanic.SpellArmor, SkillTag.Attack | SkillTag.Area | SkillTag.Elemental,
            AttackHit | SkillCapability.Area | SkillCapability.ElementalDamage, SkillRole.Clear, SkillDamageType.Lightning, SkillShape.Circle, 16, 5_000, 4, 40, 16_000, "消耗铠能强化下一次攻击并释放冲击波。"),
        A(BaseClass.Hermit, "shieldbreak_counter", "破盾反击", SkillMechanic.Counter, SkillTag.Attack | SkillTag.Counter | SkillTag.Area,
            AttackHit | SkillCapability.Counter | SkillCapability.Area | SkillCapability.Triggerable, SkillRole.Counter, SkillDamageType.Physical, SkillShape.Circle, 0, 4_000, 1, 50, 18_000, "能量护盾耗尽时自动反击并恢复铠能。"),
        A(BaseClass.Hermit, "forge_turret", "铸造炮台", SkillMechanic.Construct, SkillTag.Spell | SkillTag.Projectile | SkillTag.Duration,
            SkillCapability.Spell | SkillCapability.Projectile | SkillCapability.Duration | SkillCapability.HasCost, SkillRole.Clear, SkillDamageType.Physical, SkillShape.Self, 20, 10_000, 16, 80, 9_000, "部署静止炮台，基础优先最近目标；对应升华可优先稀有与首领。"),
        A(BaseClass.Hermit, "rune_array", "符文阵列", SkillMechanic.Construct, SkillTag.Spell | SkillTag.Area | SkillTag.Duration | SkillTag.Elemental,
            SpellHit | SkillCapability.Area | SkillCapability.Duration | SkillCapability.ElementalDamage, SkillRole.Clear, SkillDamageType.Lightning, SkillShape.GroundArea, 18, 8_000, 5, 32, 9_500, "构装体之间形成造成元素伤害的符文阵列。"),
        A(BaseClass.Hermit, "selfdestruct_rebuild", "自毁重铸", SkillMechanic.Rebuild, SkillTag.Spell | SkillTag.Area | SkillTag.Fire,
            SpellHit | SkillCapability.Area | SkillCapability.FireDamage | SkillCapability.Triggerable, SkillRole.Clear, SkillDamageType.Fire, SkillShape.Circle, 20, 8_000, 4, 60, 20_000, "引爆受损构装并在短暂延迟后重铸。"),
    ];

    public static IReadOnlyList<ArchetypeSupportDefinition> Supports { get; } =
    [
        S("far_shot", "远射", SupportMechanic.FarShot, any: SkillCapability.Projectile, description: "距离越远伤害越高，近距离伤害降低。"),
        S("precision_pierce", "精准穿透", SupportMechanic.PrecisionPierce, any: SkillCapability.Projectile, description: "投射物穿透2个目标，投射物伤害总降10%。"),
        S("seeking_chain", "追踪连锁", SupportMechanic.SeekingChain, any: SkillCapability.Projectile, description: "投射物获得2次追踪连锁，造成20%更少伤害。"),
        S("mobile_attack", "移动攻击", SupportMechanic.MobileAttack, all: SkillCapability.Attack, description: "攻击施放期间允许移动，造成12%更少伤害。"),
        S("toxin_spread", "毒素扩散", SupportMechanic.ToxinSpread, any: SkillCapability.Duration | SkillCapability.VoidDamage, description: "中毒目标死亡时向附近敌人扩散一半层数。"),
        S("multiple_traps", "多重陷阱", SupportMechanic.MultipleTraps, all: SkillCapability.Triggerable | SkillCapability.Duration, description: "一次额外布置2个陷阱，陷阱伤害总降25%。"),
        S("mark_amplify", "标记增幅", SupportMechanic.MarkAmplify, any: SkillCapability.Duration, description: "标记效果提高35%，持续时间降低20%。"),
        S("backstab_amplify", "背袭增幅", SupportMechanic.BackstabAmplify, all: SkillCapability.Melee, description: "背后命中造成30%更多伤害，正面命中造成15%更少伤害。"),
        S("minion_amplify", "召唤增幅", SupportMechanic.MinionAmplify, any: SkillCapability.Duration, description: "召唤单位造成30%更多伤害，最大生命总降15%。"),
        S("swift_minions", "迅捷仆从", SupportMechanic.SwiftMinions, any: SkillCapability.Duration, description: "召唤单位移动和攻击速度提高30%。"),
        S("expanded_army", "扩军", SupportMechanic.ExpandedArmy, any: SkillCapability.Duration, description: "召唤物上限+1，召唤物造成15%更少伤害。"),
        S("bodyguard", "护主", SupportMechanic.Bodyguard, any: SkillCapability.Duration, description: "召唤单位优先拦截靠近主角的敌人。"),
        S("aura_amplify", "光环增幅", SupportMechanic.AuraAmplify, all: SkillCapability.Reservation, description: "光环效果提高25%，保留总量提高15%。"),
        S("lasting_blessing", "祝福延续", SupportMechanic.LastingBlessing, all: SkillCapability.Duration, description: "祝福持续时间提高50%，冷却恢复降低20%。"),
        S("hex_spread", "恶咒传播", SupportMechanic.HexSpread, all: SkillCapability.Duration, description: "被诅咒敌人死亡时向附近传播诅咒。"),
        S("deep_hex", "咒印深化", SupportMechanic.DeepHex, all: SkillCapability.Duration, description: "诅咒效果提高30%，施法速度总降15%。"),
        S("fire_penetration_archetypes", "火焰穿透", SupportMechanic.FirePenetration, all: SkillCapability.FireDamage, description: "穿透25%火焰抗性。"),
        S("cold_penetration_archetypes", "寒霜穿透", SupportMechanic.ColdPenetration, all: SkillCapability.ColdDamage, description: "穿透25%寒霜抗性。"),
        S("lightning_penetration_archetypes", "闪电穿透", SupportMechanic.LightningPenetration, all: SkillCapability.LightningDamage, description: "穿透25%闪电抗性。"),
        S("elemental_ailment", "元素异常", SupportMechanic.ElementalAilment, any: SkillCapability.ElementalDamage, description: "元素异常积累提高40%，命中伤害总降10%。"),
        S("void_duration", "虚蚀延长", SupportMechanic.VoidDuration, all: SkillCapability.VoidDamage, description: "虚空持续效果延长45%。"),
        S("deep_wither", "深层凋零", SupportMechanic.DeepWither, all: SkillCapability.VoidDamage, description: "凋零效果提高30%，施法速度总降10%。"),
        S("shield_leech", "护盾汲取", SupportMechanic.ShieldLeech, all: SkillCapability.Spell | SkillCapability.Hit, description: "法术伤害的2%转为能量护盾恢复。"),
        S("shield_casting", "护盾施法", SupportMechanic.ShieldCasting, all: SkillCapability.Spell | SkillCapability.HasCost, description: "优先支付能量护盾，技能造成20%更多伤害。"),
        S("unarmed_focus", "徒手专注", SupportMechanic.UnarmedFocus, all: SkillCapability.Attack | SkillCapability.Melee, description: "徒手时造成35%更多伤害，装备武器时失效。"),
        S("combo_duration", "连击延续", SupportMechanic.ComboDuration, all: SkillCapability.Attack, description: "连击保留时间提高60%。"),
        S("stance_amplify", "姿态增幅", SupportMechanic.StanceAmplify, any: SkillCapability.Duration, description: "姿态效果提高30%，切换冷却增加1秒。"),
        S("movement_echo", "位移回响", SupportMechanic.MovementEcho, all: SkillCapability.Movement, description: "位移技能在终点重复一次攻击，重复伤害总降40%。"),
        S("ferocious_beast", "灵兽凶猛", SupportMechanic.FerociousBeast, any: SkillCapability.Duration, description: "灵兽造成35%更多伤害，承受20%更多伤害。"),
        S("guardian_beast", "灵兽守护", SupportMechanic.GuardianBeast, any: SkillCapability.Duration, description: "灵兽为主角分担15%命中伤害。"),
        S("phantom_copy", "幻身复制", SupportMechanic.PhantomCopy, any: SkillCapability.Attack, description: "额外产生1个幻身，幻身伤害总降25%。"),
        S("phantom_sacrifice", "幻身献祭", SupportMechanic.PhantomSacrifice, any: SkillCapability.Attack, description: "幻身到期时爆发，持续时间总降30%。"),
        S("spellblade", "法武交错", SupportMechanic.Spellblade, all: SkillCapability.Attack, description: "获得法术伤害的30%为攻击伤害提高。"),
        S("attack_trigger", "攻击触发", SupportMechanic.AttackTrigger, all: SkillCapability.Spell | SkillCapability.Triggerable, description: "攻击命中时触发法术，触发法术效果总降35%。"),
        S("imprint_gain", "刻印积累", SupportMechanic.ImprintGain, any: SkillCapability.ElementalDamage, description: "刻印获得量+1，技能伤害总降15%。"),
        S("imprint_burst", "刻印爆发", SupportMechanic.ImprintBurst, any: SkillCapability.ElementalDamage, description: "消耗刻印时造成25%更多伤害。"),
        S("spellarmor_fusion", "魔铠融合", SupportMechanic.SpellArmorFusion, any: SkillCapability.Guard, description: "护甲与能量护盾共同提高魔铠效果。"),
        S("shieldbreak_amplify", "破盾增幅", SupportMechanic.ShieldBreakAmplify, any: SkillCapability.Counter | SkillCapability.Guard, description: "破盾事件触发的技能造成40%更多伤害。"),
        S("construct_amplify", "构装增幅", SupportMechanic.ConstructAmplify, any: SkillCapability.Duration, description: "构装造成30%更多伤害，构装上限不变。"),
        S("rapid_rebuild", "快速重铸", SupportMechanic.RapidRebuild, any: SkillCapability.Triggerable | SkillCapability.Duration, description: "重铸延迟总降50%，重铸后最大生命总降20%。"),
    ];

    private static readonly IReadOnlyDictionary<string, ArchetypeSkillDefinition> ActiveByStone =
        Active.ToDictionary(value => value.Combat.StoneId, StringComparer.Ordinal);
    private static readonly IReadOnlyDictionary<string, ArchetypeSkillDefinition> ActiveBySkill =
        Active.ToDictionary(value => value.Combat.SkillId, StringComparer.Ordinal);
    private static readonly IReadOnlyDictionary<string, ArchetypeSupportDefinition> SupportByStone =
        Supports.ToDictionary(value => value.StoneId, StringComparer.Ordinal);

    static ArchetypeSkillDefinitions()
    {
        if (Active.Count != 50 || Supports.Count != 40 || ActiveByStone.Count != 50 || ActiveBySkill.Count != 50 || SupportByStone.Count != 40)
            throw new InvalidDataException("Archetypes skill catalog must contain 50 unique active and 40 unique support stones.");
        foreach (BaseClass theme in Enum.GetValues<BaseClass>().Where(value => value != BaseClass.Fighter))
            if (Active.Count(value => value.Theme == theme) != 10)
                throw new InvalidDataException($"Archetypes class {theme} must own ten thematic active skills.");
    }

    public static bool TryActiveForSkill(string skillId, out ArchetypeSkillDefinition? value) => ActiveBySkill.TryGetValue(skillId, out value);
    public static bool TryActiveForStone(string stoneId, out ArchetypeSkillDefinition? value) => ActiveByStone.TryGetValue(stoneId, out value);
    public static bool TrySupportForStone(string stoneId, out ArchetypeSupportDefinition? value) => SupportByStone.TryGetValue(stoneId, out value);

    private static ArchetypeSkillDefinition A(BaseClass theme, string suffix, string name, SkillMechanic mechanic,
        SkillTag tags, SkillCapability capabilities, SkillRole role, SkillDamageType damageType, SkillShape shape,
        int mana, int range, int cast, int cooldown, int damage, string description,
        Ailment ailment = Ailment.None, int ailmentChance = 0) =>
        new(theme, mechanic, new SkillCombatDefinition($"archetypes.skill_stone.{suffix}", $"archetypes.skill.{suffix}", name,
            tags, capabilities, role, damageType, shape, mana, range, cast, cooldown, damage, ailment, ailmentChance,
            description, false));

    private static ArchetypeSupportDefinition S(string suffix, string name, SupportMechanic mechanic,
        SkillCapability all = SkillCapability.None, SkillCapability any = SkillCapability.None,
        SkillCapability excluded = SkillCapability.None, string description = "") =>
        new($"archetypes.skill_stone.support.{suffix}", name, mechanic, all, any, excluded, description);
}
