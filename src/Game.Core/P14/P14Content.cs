using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P1.World;
using GameForWork.Core.P10;
using GameForWork.Core.P12;
using GameForWork.Core.Simulation;

namespace GameForWork.Core.P14;

public sealed record P14FlaskDefinition(
    P1FlaskKind Kind, string StableId, string DisplayName, int MaximumCharges, int ChargesPerUse,
    int DurationTicks, int MagnitudeBasisPoints, string EffectDescription, string AutoCondition);

public static class P14Flasks
{
    public const int InitialSlots = 3;
    public const int MaximumSlots = 5;
    public static IReadOnlyList<P14FlaskDefinition> All { get; } =
    [
        new(P1FlaskKind.Life, "core.flask.life", "生命药剂", 30, 10, 0, 40,
            "立即恢复基础40点生命，受生命药剂效果修正", "生命低于角色AI设置的药剂阈值"),
        new(P1FlaskKind.Mana, "core.flask.mana", "法力药剂", 30, 10, 80, 3_000,
            "立即恢复30%最大法力；再次自动使用间隔4秒", "法力低于35%"),
        new(P1FlaskKind.Armor, "core.flask.armor", "玄铁药剂", 40, 20, 100, 3_000,
            "持续5秒，受到的物理击中伤害降低30%", "受到击中后自动使用"),
        new(P1FlaskKind.Movement, "core.flask.movement", "疾行药剂", 40, 20, 100, 3_000,
            "持续5秒，移动速度提高30%", "与目标距离大于6米"),
        new(P1FlaskKind.Resistance, "core.flask.resistance", "棱彩药剂", 40, 20, 100, 2_500,
            "持续5秒，受到的法术击中伤害降低25%", "受到击中后自动使用"),
    ];
}

public sealed record P14UniqueDefinition(
    string StableId, string DisplayName, string BaseStableId, string RuleText, bool Mythic = false);

public static class P14UniqueItems
{
    public static IReadOnlyList<P14UniqueDefinition> All { get; } =
    [
        new("core.unique.echoing_oathbreaker", "回响破誓者", "core.base.heavy_battleaxe", "重击攻击速度总降30%；命中后产生一次造成原伤害70%的余震"),
        new("core.unique.march_without_end", "无尽行军", "core.base.march_boots", "移动时每秒护甲提高8%，最多80%；停止移动2秒后清空"),
        new("core.unique.ravens_answer", "鸦群答卷", "core.base.raven_mask", "投射物额外连锁2次并返回；返回命中造成30%更多伤害"),
        new("core.unique.red_vow", "赤誓之环", "core.base.ember_ring", "赤誓祭坛奖励提高25%；承担祭坛代价时流血造成60%更多伤害"),
        new("core.unique.blue_vow", "苍誓之环", "core.base.focus_ring", "苍誓延迟奖励翻倍；Boss最大生命和伤害各提高25%"),
        new("core.unique.gardeners_sinew", "园丁筋络", "core.base.chain_belt", "命能加工额外保留1条随机词缀，命能消耗降低30%"),
        new("core.unique.warden_shell", "监守甲壳", "core.base.bastion_plate", "格挡重击后释放一次等于盾牌护甲250%的震波，冷却1秒"),
        new("core.unique.glass_horizon", "琉璃地平线", "core.base.glass_greatblade", "每2米攻击距离获得1%基础暴击率，最多6%；远距命中造成35%更多伤害"),
        new("core.unique.funeral_bell", "葬钟", "core.base.oracle_crown", "战吼使附近敌人6秒内承受30%更多伤害"),
        new("core.unique.black_tide", "黑潮披挂", "core.base.gloom_raiment", "击败敌人获得4秒20%移动速度，最多叠加3层"),
        new("core.unique.starless_prayer", "无星祷衣", "core.base.starweave_robe", "法术压制成功时恢复8%最大能量护盾，冷却1秒"),
        new("core.unique.last_banner", "末旗护符", "core.base.ember_amulet", "旗帜不再保留资源，旗帜效果提高80%"),
        new("core.unique.iron_moon", "铁月", "core.base.rusted_greatsword", "满生命时猛击造成70%更多伤害"),
        new("core.unique.hollow_guard", "空洞守卫", "core.base.ash_iron_shield", "格挡后2秒内法术压制率提高100%，冷却3秒"),
        new("core.unique.thorn_procession", "荆棘行列", "core.base.ritual_gloves", "流血敌人被击败时，将最强剩余流血的150%扩散给附近最多5个敌人"),
        new("core.unique.pilgrims_debt", "朝圣者之债", "core.base.shadow_treads", "每个未消耗的药剂使用次数使移动速度提高3%，最多45%"),
        new("core.unique.cinder_chain", "余烬锁链", "core.base.ration_belt", "药剂充能获取提高100%、效果提高30%，持续时间总降25%"),
        new("core.unique.fourth_testament", "第四圣约", "core.base.spirit_amulet", "精神与能量各有20%转化为额外最大能量护盾"),
        new("core.unique.silent_anvil", "沉默铁砧", "core.base.rusted_warhammer", "攻击频率低于1.50/秒时，每少0.10获得8%更多攻击伤害，最多80%"),
        new("core.unique.hunters_eclipse", "猎手蚀影", "core.base.hunter_hood", "闪避后下一次攻击必定暴击，且该次暴击造成50%更多伤害"),
        new("core.unique.ashes_memory", "灰烬记忆", "core.base.ash_circlet", "护盾开始充能时每秒恢复10%最大法力，护盾充能速度提高60%"),
        new("core.unique.grave_plate", "墓门重甲", "core.base.crude_chainmail", "护甲的20%同时用于降低法术击中伤害"),
        new("core.unique.famine_ring", "饥馑指环", "core.base.iron_ring", "击杀获得的药剂充能翻倍，药剂效果提高50%，生命恢复总降30%"),
        new("core.unique.last_watch", "终夜守望", "core.base.warlord_helm", "受到未格挡击中获得1层壁垒，最多5层；每层受到击中伤害降低5%"),
        new("core.mythic.heart_of_ash", "灰烬之心", "core.base.triune_carapace", "三阶段继承药剂与战吼状态；每场战斗首次濒死以50%生命和护盾重燃，随后8秒造成30%更多伤害", true),
    ];

    public static ItemInstance Create(string stableId, int itemLevel, string instanceId)
    {
        P14UniqueDefinition definition = All.Single(item => item.StableId == stableId);
        ItemBaseDefinition itemBase = P1ItemBases.Get(definition.BaseStableId);
        var rule = new LegendaryRule(definition.StableId,
            definition.StableId == "core.unique.echoing_oathbreaker" ? 7_000 : 10_000,
            definition.StableId == "core.unique.echoing_oathbreaker" ? 7_000 : definition.Mythic ? 15_000 : 10_000,
            definition.RuleText);
        return new ItemInstance(instanceId, itemBase, Math.Clamp(itemLevel, 1, 120),
            ItemRarity.Legendary, GameForWork.Core.P25.P25LegendaryCatalog.CreateAffixes(itemBase), rule,
            ImplicitValue: itemBase.ImplicitMaximumValue, LinkedSocketCount: definition.Mythic ? 6 : 5,
            Quality: definition.Mythic ? 20 : 10,
            RolledName: definition.DisplayName);
    }
}

public enum P14MapNodeKind { Entrance, Encounter, RouteChoice, AbyssFissure, GardenPlot, Altar, Elite, Boss }
public sealed record P14MapNode(int Index, P14MapNodeKind Kind, string DisplayName, int EnemyCount, bool Optional = false,
    string BossStableId = "");
public sealed record P14MapPlan(
    string MapInstanceId, MapRoute Route, IReadOnlyList<P14MapNode> Nodes, P12MapAltar Altar,
    IReadOnlyList<string> AtlasSnapshot, int RouteChoiceIndex, string FinalBossStableId);

public static class P14MapPlanner
{
    public static P14MapPlan Build(P1MapItem map, MapRoute route, IEnumerable<string> atlasPassives, ulong seed)
    {
        map.Validate();
        string[] atlas = (map.AtlasSnapshot ?? atlasPassives).Order(StringComparer.Ordinal).ToArray();
        if (P10EndgameState.IsBreakthroughTrial(map))
            return new(map.InstanceId, route,
            [new(1, P14MapNodeKind.Entrance, "门扉前庭", 0), new(2, P14MapNodeKind.Encounter, "百级试炼", 12),
                new(3, P14MapNodeKind.Elite, "守门双影", 8),
                new(4, P14MapNodeKind.Boss, P14Bosses.Breakthrough.DisplayName, 5, BossStableId: P14Bosses.Breakthrough.StableId)],
                P12MapAltar.None, atlas, 1, P14Bosses.Breakthrough.StableId);
        if (P10EndgameState.IsCitadel(map) || P10EndgameState.IsCitadelPractice(map))
            return new(map.InstanceId, route,
                [new(1, P14MapNodeKind.Entrance, "天垒门前", 0),
                    new(2, P14MapNodeKind.Boss, P14Bosses.CitadelStages[0].DisplayName, 9, BossStableId: P14Bosses.CitadelStages[0].StableId),
                    new(3, P14MapNodeKind.Boss, P14Bosses.CitadelStages[1].DisplayName, 7, BossStableId: P14Bosses.CitadelStages[1].StableId),
                    new(4, P14MapNodeKind.Boss, P14Bosses.CitadelStages[2].DisplayName, 5, BossStableId: P14Bosses.CitadelStages[2].StableId)],
                P12MapAltar.None, atlas, 1, P14Bosses.CitadelStages[2].StableId);
        var random = new Pcg32(seed ^ (ulong)map.Tier << 32);
        int count = 5 + (int)(random.NextUInt() % 4);
        int choice = 2 + (int)(random.NextUInt() % 2);
        int mechanicBudget = Math.Clamp(count - 3, 2, 4);
        var nodes = new List<P14MapNode>(count) { new(1, P14MapNodeKind.Entrance, "地图入口", 0) };
        for (int index = 2; index < count; index++)
        {
            if (index == choice)
            {
                nodes.Add(new(index, P14MapNodeKind.RouteChoice, "收益路线抉择", 0));
                continue;
            }
            P14MapNodeKind kind = route switch
            {
                MapRoute.Abyss when mechanicBudget-- > 0 => P14MapNodeKind.AbyssFissure,
                MapRoute.LifeGarden when mechanicBudget-- > 0 => P14MapNodeKind.GardenPlot,
                _ when map.Altar != P12MapAltar.None && nodes.All(node => node.Kind != P14MapNodeKind.Altar) => P14MapNodeKind.Altar,
                _ => index == count - 1 ? P14MapNodeKind.Elite : P14MapNodeKind.Encounter,
            };
            int enemies = kind is P14MapNodeKind.RouteChoice or P14MapNodeKind.Altar ? 0 :
                8 + map.Tier / 2 + (int)(random.NextUInt() % 7);
            nodes.Add(new(index, kind, NodeName(kind, map.Altar), enemies, kind == P14MapNodeKind.Altar));
        }
        P12MapArea area = P12MapCatalog.TryGet(map.AreaId, out P12MapArea found) ? found : P12MapCatalog.Areas[0];
        string bossId = P14Bosses.ForArea(area.StableId).StableId;
        nodes.Add(new(count, P14MapNodeKind.Boss, area.BossName, 5, BossStableId: bossId));
        return new(map.InstanceId, route, nodes, map.Altar, atlas,
            choice, bossId);
    }

    private static string NodeName(P14MapNodeKind kind, P12MapAltar altar) => kind switch
    {
        P14MapNodeKind.AbyssFissure => "移动裂隙追猎",
        P14MapNodeKind.GardenPlot => "命能苗圃",
        P14MapNodeKind.Altar => altar == P12MapAltar.RedOath ? "赤誓祭坛" : "苍誓祭坛",
        P14MapNodeKind.Elite => "精英据点",
        _ => "地图遭遇",
    };
}

public sealed record P14BossSkill(string DisplayName, string DamageType, string Telegraph, bool Avoidable);
public sealed record P14BossDefinition(
    string StableId, string DisplayName, string AreaStableId, IReadOnlyList<P14BossSkill> Skills,
    int PhaseThresholdBasisPoints, int EnrageSeconds, string SpecialRule);

public static class P14Bosses
{
    private static readonly string[] Names = ["灼痕督军", "沉棺祭司", "绞枝母体", "无旗将军", "苔冠巨兽", "碎光监工", "默祷院长", "黑帆船长"];
    public static IReadOnlyList<P14BossDefinition> MapBosses { get; } = Enumerable.Range(0, 8).Select(index =>
        new P14BossDefinition($"core.boss.map.{index + 1:00}", Names[index], P12MapCatalog.Areas[index].StableId,
        [
            new("扇形重击", "物理", "红边扇形蓄力", true),
            new("追猎冲锋", "物理", "箭头与闪烁路径", true),
            new("余烬爆发", "火焰", "环形描边收缩", true),
        ], 5_000, 90, index % 2 == 0 ? "召唤同族" : "生成危险地面")).ToArray();

    public static P14BossDefinition Breakthrough { get; } = new("core.boss.gate_trial", "百级门扉化身", "core.endgame.gate_trial",
        [new("门扉碾压", "物理", "交叉重线", true), new("灵能浪潮", "法术", "蓝色双环", true), new("终末审判", "混合", "全屏倒计时", true)],
        5_000, 120, "失败不消耗门票，可重复挑战");

    public static IReadOnlyList<P14BossDefinition> CitadelStages { get; } =
    [
        new("core.boss.citadel.wall", "活化城墙", "core.endgame.ashen_citadel", [new("落石阵", "物理", "方格阴影", true), new("城垛齐射", "物理", "平行箭头", true), new("熔油", "火焰", "橙色地面边框", true)], 6_000, 120, "破坏三处城垛"),
        new("core.boss.citadel.guards", "灰烬双卫", "core.endgame.ashen_citadel", [new("交叉斩", "物理", "交叉亮线", true), new("誓火链", "火焰", "两点连线", true), new("替身护卫", "法术", "盾形描边", false)], 5_000, 150, "两名守卫相隔过远会复苏"),
        new("core.boss.citadel.core", "天垒核心", "core.endgame.ashen_citadel", [new("核心脉冲", "法术", "三重扩散环", true), new("灰烬坠落", "火焰", "闪烁落点", true), new("誓约抹除", "混合", "黑白全屏边框", true)], 4_000, 180, "资源继承并在低生命进入终局回响"),
    ];

    public static P14BossDefinition ForArea(string areaId) => MapBosses.FirstOrDefault(boss => boss.AreaStableId == areaId) ?? MapBosses[0];
    public static P14BossDefinition? TryGet(string stableId) => MapBosses.Concat([Breakthrough]).Concat(CitadelStages)
        .FirstOrDefault(item => item.StableId == stableId);

    public static EnemyProfile CombatProfile(string stableId)
    {
        P14BossDefinition boss = MapBosses.Concat([Breakthrough]).Concat(CitadelStages)
            .FirstOrDefault(item => item.StableId == stableId) ?? MapBosses[0];
        bool endgame = boss == Breakthrough || CitadelStages.Contains(boss);
        return new EnemyProfile(boss.StableId, boss.DisplayName, endgame ? 520 : 310,
            endgame ? 13 : 9, endgame ? 21 : 15, endgame ? 38 : 24, 8, 78, 2_100, 1_000, 0);
    }
}

public sealed record P14AltarChoice(string StableId, string Cost, string Reward, int RiskStacks);
public static class P14Altars
{
    public static IReadOnlyList<P14AltarChoice> Choices(P12MapAltar altar, int tier) => altar switch
    {
        P12MapAltar.RedOath =>
        [new("red.blood_price", "最大生命降低 8%", "立即获得金币与金属", 1), new("red.hunted", "玩家受伤提高 12%", "额外稀有物品", 1), new("red.frail", "药剂恢复降低 20%", "地图数量提高", 2)],
        P12MapAltar.BlueOath =>
        [new("blue.hardened", "敌人生命提高 20%", "Boss 后结算通货", 1), new("blue.swift", "敌人速度提高 18%", "Boss 后结算传奇几率", 1), new("blue.overlord", "Boss 伤害提高 25%", "Boss 后奖励翻倍", 2)],
        _ => [],
    };
}

public sealed record P14AtlasEffect(int MechanicWeightBasisPoints, int RewardBasisPoints, bool BlocksMechanic, bool NotableRule);
public static class P14AtlasRules
{
    public static P14AtlasEffect Resolve(IEnumerable<string> allocated, P10AtlasTheme theme)
    {
        P10AtlasNode[] nodes = allocated.Select(P10AtlasTree.Get).Where(node => node.Theme == theme).ToArray();
        return new(10_000 + nodes.Sum(node => node.MechanicWeightBasisPoints), 10_000 + nodes.Sum(node => node.RewardBasisPoints),
            nodes.Any(node => node.BlocksCompetingMechanic), nodes.Any(node => !string.IsNullOrEmpty(node.SpecialRule)));
    }
}

public enum P14GardenCraft { KeepPrefixes, KeepSuffixes, BiasLife, BiasDefense, BiasAttack }
public static class P14GardenCrafting
{
    public static int Cost(P14GardenCraft craft) => craft is P14GardenCraft.KeepPrefixes or P14GardenCraft.KeepSuffixes ? 80 : 40;
    public static IReadOnlyList<AffixRoll> SelectRetained(ItemInstance item, P14GardenCraft craft) => craft switch
    {
        P14GardenCraft.KeepPrefixes => item.Affixes.Where(affix => affix.Definition.Position == AffixPosition.Prefix).ToArray(),
        P14GardenCraft.KeepSuffixes => item.Affixes.Where(affix => affix.Definition.Position == AffixPosition.Suffix).ToArray(),
        P14GardenCraft.BiasLife => item.Affixes.Where(affix => affix.Definition.ModifierKind is ItemModifierKind.FlatMaximumLife or ItemModifierKind.IncreasedLifeFlaskEffectBasisPoints).ToArray(),
        P14GardenCraft.BiasDefense => item.Affixes.Where(affix => affix.Definition.ModifierKind is ItemModifierKind.IncreasedArmorBasisPoints or ItemModifierKind.IncreasedEvasionBasisPoints or ItemModifierKind.IncreasedShieldBasisPoints).ToArray(),
        _ => item.Affixes.Where(affix => affix.Definition.ModifierKind is ItemModifierKind.AddedPhysicalDamage or ItemModifierKind.IncreasedPhysicalDamageBasisPoints or ItemModifierKind.IncreasedAttackSpeedBasisPoints).ToArray(),
    };

    public static ItemInstance Apply(ItemInstance item, P14GardenCraft craft, ulong seed)
    {
        if (!item.CanModify || item.Rarity != ItemRarity.Rare)
            throw new InvalidOperationException("命能加工要求未锁定、未腐化的稀有装备。");
        IReadOnlyList<AffixRoll> retained = SelectRetained(item, craft);
        ItemInstance rolled = ItemGenerator.Generate(item.Base.StableId, item.ItemLevel, ItemRarity.Rare, seed,
            item.InstanceId);
        AffixRoll[] affixes = retained.Concat(rolled.Affixes)
            .GroupBy(affix => affix.Definition.StableFamilyId, StringComparer.Ordinal)
            .Select(group => group.First()).Take(6).ToArray();
        return item with { Affixes = affixes };
    }
}

public sealed record P14GardenCraftResult(bool Succeeded, string Summary, ItemInstance? Result, int Cost);

public sealed record P14TierRule(int Tier, string StableId, string DisplayName, string Effect);
public static class P14TierRules
{
    public static IReadOnlyList<P14TierRule> FinalTiers { get; } =
    [
        new(17, "core.tier_rule.suppression_hex", "压制诅咒", "未压制法术伤害提高，成功压制仍降低该次伤害 70%"),
        new(18, "core.tier_rule.withered_ground", "枯竭地面", "移动裂隙留下阻碍恢复的危险地面"),
        new(19, "core.tier_rule.composite_elites", "复合精英", "精英固定拥有两种互补基型能力"),
        new(20, "core.tier_rule.endgame_echo", "终局回响", "Boss 阶段技能在延迟后回响一次"),
    ];

    public static string BuildGateAssessment(int tier, bool craftedGear, bool completeBuild) => tier switch
    {
        <= 10 => "普通构筑可稳定完成",
        <= 16 when craftedGear => "加工构筑满足门槛",
        <= 16 => "建议完成装备加工",
        _ when completeBuild => "完整攻防构筑满足终局门槛",
        _ => "终局攻防构筑不完整",
    };
}

public sealed record P14MechanicResult(
    bool Completed, bool MapMayContinue, int RewardMultiplierBasisPoints, int LifeForce,
    int CurrencyBundles, string Summary);

public static class P14MechanicRules
{
    public static P14MechanicResult ResolveAbyss(int wavesCleared, int requiredWaves, int remainingSeconds)
    {
        bool completed = wavesCleared >= requiredWaves && remainingSeconds >= 0;
        return new(completed, true, completed ? 14_000 + remainingSeconds * 50 : 10_000,
            0, completed ? 1 + requiredWaves / 2 : 0,
            completed ? "裂隙已追至宝箱，获得通货与天垒碎片机会。" : "裂隙闭合；额外奖励丢失，地图继续。" );
    }

    public static P14MechanicResult ResolveGarden(IReadOnlyList<int> selectedPlotRisks, int tier)
    {
        if (selectedPlotRisks.Count != 3 || selectedPlotRisks.Any(risk => risk is < 0 or > 2))
            throw new ArgumentOutOfRangeException(nameof(selectedPlotRisks));
        int risk = selectedPlotRisks.Sum();
        return new(true, true, 10_000 + risk * 900, Math.Max(3, tier * (3 + risk)), 0,
            $"三块苗圃已收割，获得 {Math.Max(3, tier * (3 + risk))} 命能。" );
    }

    public static P14AltarChoice SelectAltar(P12MapAltar altar, int tier, int choiceIndex)
    {
        IReadOnlyList<P14AltarChoice> choices = P14Altars.Choices(altar, tier);
        if (choiceIndex < 0 || choiceIndex >= choices.Count) throw new ArgumentOutOfRangeException(nameof(choiceIndex));
        return choices[choiceIndex];
    }
}
