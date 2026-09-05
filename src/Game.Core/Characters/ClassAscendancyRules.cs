using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Skills;
using GameForWork.Core.Ascendancies;

namespace GameForWork.Core.Characters;

public sealed record BranchSpec(
    string StableKey,
    string ReinforcementName,
    string ReinforcementEffect,
    string CoreName,
    string CoreEffect);

public sealed record PathSpec(
    Ascendancy Ascendancy,
    string StableKey,
    IReadOnlyList<BranchSpec> Branches);

public static class ClassNodeIds
{
    public const string MarksmanMultipleCore = "core.ascendancy.marksman.multiple.core";
    public const string SoulLegionCore = "core.ascendancy.soul_shepherd.legion.core";
    public const string CantorReservationCore = "core.ascendancy.spirit_cantor.reservation.core";
    public const string CantorAuraCore = "core.ascendancy.spirit_cantor.aura.core";
    public const string CantorBlessingCore = "core.ascendancy.spirit_cantor.blessing.core";
    public const string ElementalistConversionCore = "core.ascendancy.elementalist.conversion.core";
    public const string AegisMaximumCore = "core.ascendancy.aegis_mage.maximum.core";
    public const string AegisRechargeCore = "core.ascendancy.aegis_mage.recharge.core";
    public const string IdolTurretCore = "core.ascendancy.idol_forger.turret.core";
}

public static class ClassAscendancyCatalog
{
    private static BranchSpec B(string key, string smallName, string smallEffect, string coreName, string coreEffect) =>
        new(key, smallName, smallEffect, coreName, coreEffect);

    public static IReadOnlyList<PathSpec> Paths { get; } =
    [
        new(Ascendancy.Marksman, "marksman",
        [
            B("distance", "远眺校准", "投射物伤害提高15%，投射物速度提高15%", "天际猎杀", "对距离6米及以上的敌人造成35%更多投射物伤害"),
            B("mobile", "游击步法", "移动速度提高12%，移动时闪避提高15%", "行射无痕", "允许远程攻击时移动；最近2秒移动2米后，远程攻击造成20%更多伤害"),
            B("multiple", "分裂弹道", "投射物速度提高15%，投射物伤害提高12%", "多重投射", "额外发射2个投射物，投射物速度提高30%，投射物造成35%更少伤害；同次释放可重复命中同一目标"),
            B("pierce", "贯穿瞄具", "投射物额外穿透2个目标", "一线洞穿", "每剩余1次穿透使投射物造成12%更多伤害，最多36%"),
            B("chain", "折射箭路", "投射物追加1次连锁，连锁距离提高25%", "追魂折返", "最后一次连锁后返回施放者；连锁与返回命中造成25%更多伤害"),
            B("gale", "逐风命中", "投射物命中获得1层疾风，持续4秒，最多10层", "风暴射界", "每层疾风使攻击和移动速度提高2%；满层时每4秒避开首次击中并清空疾风"),
        ]),
        new(Ascendancy.Shadowblade, "shadowblade",
        [
            B("critical", "冷刃磨砺", "匕首基础暴击率提高2%，暴击伤害提高20%", "致命精算", "暴击时获得1层杀意，最多5层；每层使暴击造成10%更多伤害，未暴击时清空"),
            B("mark", "猎物刻痕", "对被标记敌人的命中伤害提高20%", "孤猎印记", "同时只能标记1个敌人；标记稀有怪或Boss时，其承受15%更多伤害且无法闪避你的攻击"),
            B("backstab", "绕后步", "从目标背面攻击时暴击率提高40%", "背影绝杀", "从目标背面命中造成45%更多伤害，并使第一次暴击必定造成最高伤害"),
            B("evasion", "偏锋闪身", "闪避提高20%，闪避后2秒内攻击速度提高15%", "影返", "闪避击中后立刻触发一次不占动作时间的武器反击，冷却1秒"),
            B("stealth", "敛息", "4秒未造成或受到伤害后进入隐匿", "无声猎手", "隐匿时不会成为普通敌人的优先目标；破除隐匿的首次命中造成80%更多伤害"),
            B("execute", "割喉线", "对生命低于30%的敌人伤害提高25%", "无赦处决", "命中生命低于15%的非Boss敌人直接处决；对Boss改为造成50%更多伤害"),
        ]),
        new(Ascendancy.Venomist, "venomist",
        [
            B("poison", "淬毒锋", "中毒概率提高30%，中毒持续时间提高20%", "千毒同蚀", "同一敌人最多承受20层中毒；每层使其受到的中毒伤害提高3%"),
            B("spread", "疫雾蔓延", "中毒敌人死亡时将最强中毒扩散给3米内1个敌人", "毒潮传染", "扩散改为影响3个敌人，并复制最强中毒的全部剩余伤害"),
            B("trap", "毒阱工艺", "陷阱上限提高1，陷阱布置速度提高25%", "连环毒阱", "陷阱触发时使附近其他陷阱立即获得50%触发进度；毒陷阱造成35%更多伤害"),
            B("flask", "药剂回流", "药剂持续时间提高25%，击杀中毒敌人额外获得1点药剂充能", "永续毒剂", "药剂生效期间中毒伤害提高40%，药剂满充能时不会继续浪费获得的充能"),
            B("corrosion", "腐蚀配方", "中毒命中施加腐蚀，最多5层；每层降低2%全抗性", "蚀骨", "腐蚀上限提高至10层；满层敌人受到25%更多持续伤害"),
            B("explosion", "毒囊压缩", "中毒敌人死亡时造成其最大生命3%的毒爆伤害", "万疫爆裂", "毒爆范围提高50%，伤害提高至最大生命6%；Boss每损失20%生命触发一次"),
        ]),
        new(Ascendancy.SoulShepherd, "soul_shepherd",
        [
            B("legion", "扩军仪式", "召唤物持续时间提高25%，召唤速度提高20%", "军团", "召唤物上限+2；在场召唤物每超过8只，召唤物伤害提高15%"),
            B("corpse", "拾骨者", "尸体持续时间提高50%，消耗尸体时回复2%最大法力", "亡骸循环", "每消耗1具尸体，使召唤物攻击与施法速度提高8%，持续6秒，最多5层"),
            B("inheritance", "灵魂拓印", "召唤物继承主角20%的提高伤害与攻击、施法速度", "完全继承", "召唤物改为继承主角50%的提高伤害、攻击与施法速度，并继承100%抗性"),
            B("command", "魂群号令", "召唤物移动速度提高20%，切换目标速度提高50%", "王魂敕令", "获得集火指令；被指令的稀有怪或Boss受到召唤物30%更多伤害"),
            B("rebirth", "残魂收束", "召唤物死亡后4秒自动复生，单个召唤物冷却12秒", "不灭军势", "自动复生延迟缩短至2秒，复生后4秒内造成50%更多伤害"),
            B("sacrifice", "献魂护主", "召唤物死亡时主角回复2%最大生命和护盾", "军势代偿", "主角受到致命伤害时牺牲1只召唤物，恢复20%最大生命与护盾，冷却12秒"),
        ]),
        new(Ascendancy.SpiritCantor, "spirit_cantor",
        [
            B("reservation", "节律调息", "光环保留消耗总降15%", "光环保留", "光环保留消耗总降40%"),
            B("aura", "共鸣扩幅", "光环效果提高20%，光环范围提高25%", "光环效果", "对主角、佣兵和召唤单位的光环效果提高50%"),
            B("blessing", "祝祷延续", "祝福持续时间提高30%，祝福冷却恢复提高20%", "祝福", "允许1名未被派遣的佣兵与主角编入同一队伍；双方都倒下时战斗失败"),
            B("war_song", "战歌起拍", "战吼与战歌效果提高20%，持续时间提高25%", "不息战歌", "战歌不占用动作时间；每次施放使全队伤害提高8%，持续6秒，最多3层"),
            B("mercenary", "同袍训练", "佣兵攻击、施法和移动速度提高15%", "英雄合奏", "同行佣兵继承主角50%的光环效果，并对主角当前目标造成30%更多伤害"),
            B("protection", "守护和声", "光环影响的友军受到击中伤害降低8%", "终曲庇护", "任一队友降至低生命时，全队获得20%最大生命与护盾屏障，冷却10秒"),
        ]),
        new(Ascendancy.Hexbinder, "hexbinder",
        [
            B("curse", "重叠咒文", "诅咒效果提高20%，持续时间提高30%", "三重恶契", "诅咒上限+2；每个额外诅咒使敌人受到8%更多持续伤害"),
            B("mana", "耗能咒式", "诅咒技能法力消耗提高50%，效果提高25%", "魔力献契", "施放诅咒消耗10%当前法力，使该诅咒效果提高100%"),
            B("spread", "恶言回响", "被诅咒敌人死亡时向3米内1个敌人扩散诅咒", "万口同咒", "扩散影响范围内全部敌人并刷新持续时间，每个诅咒每秒最多扩散1次"),
            B("weaken", "衰弱刻印", "被诅咒敌人造成的伤害降低10%", "绝望囚笼", "同时承受3个诅咒的敌人造成25%更少伤害且移动速度总降30%"),
            B("soul", "摄魂偿付", "被诅咒敌人死亡时获得1层灵魂，最多10层", "灵魂契约", "每层灵魂使诅咒效果提高3%；受到致命伤害时消耗全部灵魂，每层恢复3%生命"),
            B("doom", "末咒", "诅咒每持续1秒积累1层咒印，最多5层", "咒印处决", "满层咒印敌人低于20%生命时被处决；Boss改为承受40%更多咒术伤害"),
        ]),
        new(Ascendancy.Elementalist, "elementalist",
        [
            B("conversion", "元素择形", "可在城镇选择火焰、寒霜或闪电作为主要元素", "元素转化", "额外获得原始物理伤害50%为当前主要元素伤害"),
            B("fire", "炽焰亲和", "火焰伤害提高25%，点燃概率提高20%", "焚界", "点燃可叠加2层；对点燃敌人造成30%更多火焰伤害"),
            B("cold", "寒霜亲和", "寒霜伤害提高25%，冰缓效果提高20%", "绝对冻结", "冻结Boss所需积累降低40%；冻结敌人承受30%更多寒霜伤害"),
            B("lightning", "雷霆亲和", "闪电伤害提高25%，感电效果提高20%", "无界电势", "感电效果上限提高至75%；对感电敌人的暴击造成30%更多伤害"),
            B("ailment", "异常编织", "元素异常持续时间提高25%，施加概率提高20%", "元素灾变", "每种元素异常分别使目标承受10%更多元素伤害，最多30%"),
            B("resonance", "三相轮转", "使用不同元素命中获得对应共鸣，持续6秒", "三元素共鸣", "同时拥有火、寒霜、闪电共鸣时，元素伤害造成50%更多伤害并消耗三种共鸣"),
        ]),
        new(Ascendancy.VoidScholar, "void_scholar",
        [
            B("decay", "虚蚀研习", "虚空持续伤害提高25%，持续时间提高20%", "无光腐域", "虚空持续伤害可叠加2层，每层造成原伤害的75%"),
            B("erosion", "侵蚀标本", "虚空命中施加侵蚀，最多5层，每层降低2%虚空抗性", "防线归零", "侵蚀上限提高至10层，满层敌人虚空抗性视为0"),
            B("wither", "凋零触须", "虚空伤害施加凋零，使受到的虚空伤害提高3%，最多10层", "深层凋零", "凋零上限提高至15层，满层时虚空持续伤害造成30%更多伤害"),
            B("forbidden", "禁术代价", "虚空技能消耗提高25%，伤害提高25%", "越界施法", "虚空技能可消耗5%最大生命代替法力，并造成50%更多伤害"),
            B("sacrifice", "理智献祭", "每秒失去1%最大护盾，虚空伤害提高20%", "空壳学者", "护盾为0时不再失去护盾，虚空伤害造成40%更多且免疫虚空异常"),
            B("burst", "坍缩预兆", "虚空持续伤害结束时造成剩余伤害30%的爆发", "终末奇点", "对满层凋零敌人施放虚空技能引爆全部虚空持续伤害，造成剩余伤害120%，冷却4秒"),
        ]),
        new(Ascendancy.AegisMage, "aegis_mage",
        [
            B("maximum", "秘能扩容", "最大能量护盾提高20%", "最大护盾", "获得30%更多最大能量护盾"),
            B("recharge", "快速归流", "能量护盾充能速度提高20%", "护盾充能", "充能延迟由2秒降至1秒，充能速度提高50%"),
            B("casting", "盾能施法", "护盾高于50%时施法速度提高15%", "秘盾供能", "技能优先消耗能量护盾；以护盾支付的技能造成35%更多法术伤害"),
            B("absorb", "过量吸收", "护盾充满时获得最大护盾10%的屏障", "无损容器", "屏障上限提高至30%；屏障存在时护盾充能不会被普通命中打断"),
            B("counter", "法术偏振", "受到法术击中时获得20%法术格挡，持续2秒", "镜式反击", "格挡法术时向施法者释放一次不消耗资源的当前法术，冷却1秒"),
            B("break", "破盾回响", "护盾耗尽时法术伤害提高25%，持续4秒", "零界爆发", "护盾耗尽时获得2秒伤害免疫并立刻完成一次免费施法，冷却10秒"),
        ]),
        new(Ascendancy.MartialMonk, "martial_monk",
        [
            B("unarmed", "空手锻体", "未装备武器时攻击速度提高20%，闪避提高15%", "百炼拳身", "徒手攻击获得每10点灵巧和精神1%提高伤害，并造成35%更多伤害"),
            B("combo", "连势", "连续命中获得连击，最多10层，每层攻击速度提高1%", "无断连环", "连击不会因切换目标消失；满层时徒手攻击造成50%更多伤害"),
            B("stance", "双式呼吸", "可切换攻势与守势；攻势伤害提高15%，守势受到伤害降低10%", "阴阳轮转", "切换姿态保留上一姿态效果3秒，冷却3秒"),
            B("movement", "踏空", "位移攻击冷却恢复提高30%，范围提高20%", "步步生威", "位移攻击命中后下一次徒手攻击造成60%更多伤害且必定暴击"),
            B("counter", "听劲", "闪避或格挡后反击伤害提高40%", "借力返身", "反击复制被避免击中的基础伤害，造成其中150%并使敌人眩晕"),
            B("finisher", "收势", "终结技每层连击伤害提高6%", "十方终式", "终结技消耗全部连击，每层造成12%更多伤害；满层后立即恢复5层"),
        ]),
        new(Ascendancy.BeastKeeper, "beast_keeper",
        [
            B("companion", "灵兽契约", "核心灵兽上限保持1只，灵兽伤害和生命提高25%", "共生灵核", "灵兽继承主角50%提高伤害、100%抗性和30%最大生命"),
            B("forms", "兽相训练", "灵兽可在猛攻、守护、追猎三种形态间切换", "三相本能", "形态效果提高100%，切换形态不占用动作且冷却缩短至2秒"),
            B("sync", "协同狩猎", "主角和灵兽攻击同一目标时伤害提高15%", "双魂夹击", "双方在2秒内先后命中同一目标时触发一次合击，造成双方伤害总和的150%"),
            B("share", "分痛纽带", "主角受到的15%击中伤害由灵兽承担", "同生契", "伤害分担提高至30%；任一方生命偷取同时治疗另一方50%"),
            B("rescue", "护主咆哮", "主角进入低生命时灵兽嘲讽附近敌人，冷却8秒", "濒死救援", "主角受到致命伤害时由灵兽承受并将主角恢复至30%生命，冷却20秒"),
            B("apex", "野性积累", "灵兽击杀或命中Boss获得野性，最多10层", "原初兽王", "满层时灵兽进入原初形态8秒，体型和范围提高50%，造成80%更多伤害"),
        ]),
        new(Ascendancy.PhantomMaster, "phantom_master",
        [
            B("spawn", "残影步", "每完成4个合法技能动作生成幻身，上限2个", "万相分身", "每完成3个合法技能动作生成幻身，持续8秒，上限4个"),
            B("copy", "招式映刻", "幻身复演攻击，造成快照伤害30%；同一幻身同一技能每秒至多一次", "镜技同施", "幻身也可复演法术，比例提高至50%；不能偷取、击回或获得施放收益"),
            B("swap", "移形印", "敌方单次击中实际损失达到最大生命与护盾之和20%后，与最远幻身换位，冷却5秒", "无定真身", "换位冷却2秒，换位后1秒不能被选为新目标，已开始的攻击仍可命中"),
            B("afterimage", "追忆成列", "技能记忆长度提高至4，相邻复演基础间隔降至0.4秒", "轮回演武", "城镇选择顺演、聚焦或回溯；聚焦最新技能3次且伤害总降40%；回溯伤害总增20%、间隔延长50%"),
            B("sustain", "虚实轮转", "幻身到期或规则移除恢复2%最大生命和护盾，每0.5秒至多一次", "替身受难", "最近幻身替代下一次敌方击中并消失，冷却3秒；不触发恢复或受击收益"),
            B("unity", "同调", "每个幻身使主角伤害提高6%、移动速度提高2%，最多4个；幻身对主角当前稀有或Boss目标伤害总增30%", "百影合击", "4个幻身在场且自行命中稀有或Boss时消耗全部幻身，各复演最新合法记忆的75%快照伤害；冷却8秒，不触发消失恢复或复演链"),
        ]),
        new(Ascendancy.Runecarver, "runecarver",
        [
            B("infusion", "武器灌注", "攻击后使下一次法术获得20%武器伤害", "符刃同源", "攻击与法术交替使用时，下一次技能获得50%前一次技能伤害为额外伤害"),
            B("trigger", "应答刻印", "攻击暴击时有20%概率触发连接法术", "必应符文", "每第三次攻击必定触发连接法术，触发法术造成30%更少伤害且不消耗资源"),
            B("conversion", "元素刻刀", "可选择将25%物理伤害转化为火焰、寒霜或闪电", "全纹转化", "转化比例提高至100%，转化后的元素伤害造成30%更多伤害"),
            B("stacks", "叠纹", "攻击或施法获得对应刻印，最多6层", "六重刻阵", "满6层时下一次技能额外重复1次并消耗全部刻印"),
            B("spellblade", "术刃护持", "施法后武器伤害提高25%，攻击后施法速度提高15%", "交错战法", "两种效果可同时存在且效果翻倍，持续4秒"),
            B("burst", "爆纹", "消耗刻印时造成每层20%武器伤害的范围爆炸", "天地刻爆", "爆炸改为每层40%，并采用当前主要元素，可重复命中同一目标"),
        ]),
        new(Ascendancy.Spellarmor, "spellarmor",
        [
            B("hybrid", "魔铠根基", "体魄+80、能量+80，体魄和能量分别提高5%", "刚能并铸", "每完整100最终体魄获得200护甲、攻击伤害提高50%；每完整100最终能量获得200最大护盾、法术伤害提高50%"),
            B("charge", "受击蓄能", "受到击中获得1层铠能，每0.25秒最多1层，上限10层，每层护甲提高3%", "蓄能魔铠", "每层同时使法术伤害提高4%；下一个非触发自施法消耗全部铠能，每层8%更多伤害合为一个线性乘区，10层为80%更多"),
            B("absorb", "铠能吸收", "护盾实际承受敌方伤害后，恢复护盾损失5%的生命", "逆流装甲", "恢复提高至25%；满生命时改为逆流屏障，上限最大生命30%、持续4秒并刷新；过量恢复不再转换"),
            B("overload", "热机", "护盾高于50%时攻击和施法速度提高15%", "魔铠过载", "主动消耗50%当前护盾进入过载6秒，攻击和法术造成50%更多伤害，冷却8秒；支付不触发受击、吸收或破盾"),
            B("break", "裂甲电弧", "敌方伤害耗尽护盾时造成最大护盾30%的闪电范围击中，冷却6秒；受通用、闪电和范围修正，不受攻击/法术修正且不能暴击", "破盾反击", "伤害提高至100%，必定眩晕普通敌人并为Boss施加5层破甲；共用6秒冷却，支付护盾不触发"),
            B("guard", "奥术护甲", "护卫技能同时获得最大护盾15%的屏障，持续时间跟随护卫", "永动壁甲", "护卫期间受到敌方击中获得1层铠能，首次达到10层刷新护卫至完整基础持续时间，每次激活限1次"),
        ]),
        new(Ascendancy.IdolForger, "idol_forger",
        [
            B("construct", "增殖铸模", "构装体上限+1，构装体生命提高25%", "群像工坊", "构装体上限再+2；每个在场构装使所有构装行动速度提高6%，最多6个"),
            B("turret", "远射校具", "远程构装射程提高30%，投射物速度提高25%，命中提高300", "炮台协议", "优先稀有和Boss，对其造成50%更多伤害；热量至少50时额外1个投射物，同动作不能重复命中"),
            B("rune_field", "阵地刻线", "构装体周围形成符文阵，使友军护甲和护盾提高15%", "重叠阵列", "符文阵可以叠加3层，每层使友军伤害提高10%、受到伤害降低5%"),
            B("command", "操偶指令", "切换目标所需时间降低50%，移动速度提高20%", "全机集火", "优先主角当前目标，对该目标造成50%更多伤害；没有合法目标时恢复普通AI"),
            B("detonate", "过载核心", "过热前最后动作伤害总增80%、范围提高40%；稳压模块改为每第10个动作强化", "自毁协议", "死亡爆炸为最大生命100%，物理火焰各半，对Boss总降50%；替代爆芯，不叠加；规则移除不触发"),
            B("rebuild", "备用铸件", "被摧毁8秒后重铸；回炉模块缩短为6秒", "永续工坊", "重铸延迟统一4秒；重铸后4秒伤害总增50%、承伤总降50%，热量归零"),
        ]),
    ];

    public static IReadOnlyList<AscendancyNode> Nodes { get; } = Build();

    public static string Id(Ascendancy ascendancy, string branch, NodeKind kind)
    {
        PathSpec path = Paths.Single(item => item.Ascendancy == ascendancy);
        return $"core.ascendancy.{path.StableKey}.{branch}.{(kind == NodeKind.Core ? "core" : "small")}";
    }

    private static IReadOnlyList<AscendancyNode> Build()
    {
        var nodes = new List<AscendancyNode>(180);
        foreach (PathSpec path in Paths)
        {
            if (path.Branches.Count != 6) throw new InvalidOperationException($"{path.Ascendancy} must have six branches.");
            for (int direction = 0; direction < path.Branches.Count; direction++)
            {
                BranchSpec branch = path.Branches[direction];
                string smallId = $"core.ascendancy.{path.StableKey}.{branch.StableKey}.small";
                string coreId = $"core.ascendancy.{path.StableKey}.{branch.StableKey}.core";
                (int smallX, int smallY, int coreX, int coreY) = Coordinates(direction);
                nodes.Add(new(smallId, path.Ascendancy, direction, NodeKind.Reinforcement,
                    branch.ReinforcementName, branch.ReinforcementEffect, null, smallX, smallY));
                nodes.Add(new(coreId, path.Ascendancy, direction, NodeKind.Core,
                    branch.CoreName, branch.CoreEffect, smallId, coreX, coreY));
            }
        }
        return nodes;
    }

    private static (int SmallX, int SmallY, int CoreX, int CoreY) Coordinates(int direction) => direction switch
    {
        0 => (0, -92, 0, -190),
        1 => (80, -46, 165, -95),
        2 => (80, 46, 165, 95),
        3 => (0, 92, 0, 190),
        4 => (-80, 46, -165, 95),
        _ => (-80, -46, -165, -95),
    };
}

public enum PrimaryElement { Fire, Cold, Lightning }

public readonly record struct ProjectileProfile(
    int AdditionalProjectiles,
    int IncreasedProjectileSpeedBasisPoints,
    int MoreProjectileDamageBasisPoints,
    bool CanRepeatHitSameTarget);

public readonly record struct AuraProfile(
    int ReservationMultiplierBasisPoints,
    int IncreasedEffectBasisPoints,
    int AdditionalHeroPartyMercenaries);

public readonly record struct EnergyShieldProfile(
    int MoreMaximumBasisPoints,
    int RechargeDelayTicks,
    int IncreasedRechargeRateBasisPoints);

public static class ModifierMath
{
    public static int ApplyIncreased(int value, params int[] increasedBasisPoints) =>
        checked((int)((long)value * (10_000 + increasedBasisPoints.Sum()) / 10_000));

    public static int ApplyMore(int value, params int[] moreMultipliersBasisPoints)
    {
        long result = value;
        foreach (int multiplier in moreMultipliersBasisPoints) result = checked(result * multiplier / 10_000);
        return checked((int)result);
    }

    public static int ApplyTotalReduction(int value, params int[] reductionMultipliersBasisPoints)
    {
        long result = value;
        foreach (int multiplier in reductionMultipliersBasisPoints) result = checked(result * multiplier / 10_000);
        return checked((int)result);
    }
}

public static class ClassAscendancyRules
{
    public static ProjectileProfile Projectile(CombatProfile profile) =>
        profile.Has(ClassNodeIds.MarksmanMultipleCore)
            ? new(2, 3_000, 6_500, true)
            : new(0, 0, 10_000, false);

    public static ResolvedSkill ApplyResolvedSkill(ResolvedSkill skill, SkillTag tags, CombatProfile profile)
    {
        if (!tags.HasFlag(SkillTag.Projectile)) return skill;
        ProjectileProfile projectile = Projectile(profile);
        if (projectile.AdditionalProjectiles == 0) return skill;
        return skill with
        {
            ProjectileCount = checked(skill.ProjectileCount + projectile.AdditionalProjectiles),
            ProjectileSpeedRawPerSecond = ModifierMath.ApplyIncreased(
                skill.ProjectileSpeedRawPerSecond, projectile.IncreasedProjectileSpeedBasisPoints),
            DamageMultiplierBasisPoints = ModifierMath.ApplyMore(
                skill.DamageMultiplierBasisPoints, projectile.MoreProjectileDamageBasisPoints),
        };
    }

    public static int MaximumMinions(int externalAdditionalMaximum, CombatProfile profile) => checked(
        CombatLimits.MaximumMinions + Math.Max(0, externalAdditionalMaximum) +
        (profile.Has(ClassNodeIds.SoulLegionCore) ? 2 : 0));

    public static int IncreasedMinionDamageBasisPoints(int livingMinions, CombatProfile profile) =>
        profile.Has(ClassNodeIds.SoulLegionCore) ? checked(Math.Max(0, livingMinions - 8) * 1_500) : 0;

    public static AuraProfile Aura(CombatProfile profile) => new(
        profile.Has(ClassNodeIds.CantorReservationCore) ? 6_000 : 10_000,
        profile.Has(ClassNodeIds.CantorAuraCore) ? 5_000 : 0,
        profile.Has(ClassNodeIds.CantorBlessingCore) ? 1 : 0);

    public static int ExtraPhysicalAsPrimaryElement(int originalPhysicalDamage, PrimaryElement element,
        CombatProfile profile)
    {
        _ = element;
        return profile.Has(ClassNodeIds.ElementalistConversionCore)
            ? checked(Math.Max(0, originalPhysicalDamage) * 5_000 / 10_000)
            : 0;
    }

    public static EnergyShieldProfile EnergyShield(CombatProfile profile) => new(
        profile.Has(ClassNodeIds.AegisMaximumCore) ? 13_000 : 10_000,
        profile.Has(ClassNodeIds.AegisRechargeCore) ? 20 : EnergyShieldState.RechargeDelayTicks,
        profile.Has(ClassNodeIds.AegisRechargeCore) ? 5_000 : 0);

    public static bool ConstructPrioritizes(EnemyRarity rarity, CombatProfile profile) =>
        profile.Has(ClassNodeIds.IdolTurretCore) && rarity is EnemyRarity.Rare or EnemyRarity.Boss;

    public static int ConstructDamageMultiplier(EnemyRarity rarity, CombatProfile profile) =>
        ConstructPrioritizes(rarity, profile) ? 15_000 : 10_000;
}

public static class ClassBenchmarkBuilds
{
    public static IReadOnlyList<BenchmarkBuild> All { get; } = ClassAscendancyCatalog.Paths
        .SelectMany(path => new[]
        {
            Create(path, false, [0, 1, 2, 3]),
            Create(path, true, [2, 3, 4, 5]),
        }).ToArray();

    private static BenchmarkBuild Create(PathSpec path, bool endgame, IReadOnlyList<int> directions)
    {
        string mode = endgame ? "endgame" : "entry";
        string displayMode = endgame ? "终局" : "开荒";
        string[] nodes = directions.SelectMany(direction => new[]
        {
            ClassAscendancyCatalog.Id(path.Ascendancy, path.Branches[direction].StableKey, NodeKind.Reinforcement),
            ClassAscendancyCatalog.Id(path.Ascendancy, path.Branches[direction].StableKey, NodeKind.Core),
        }).ToArray();
        return new($"core.benchmark.{path.StableKey}.{mode}",
            $"{WarriorAscendancyCatalog.DisplayName(path.Ascendancy)}·{displayMode}", path.Ascendancy, endgame,
            nodes, [], string.Join('、', directions.Select(direction => path.Branches[direction].CoreName)));
    }
}
