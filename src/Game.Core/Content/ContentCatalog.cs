using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Equipment;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Endgame;
using GameForWork.Core.Maps;
using GameForWork.Core.Simulation;

namespace GameForWork.Core.Content;

public sealed record FlaskDefinition(
    FlaskKind Kind, string StableId, string DisplayName, int MaximumCharges, int ChargesPerUse,
    int DurationTicks, int MagnitudeBasisPoints, string EffectDescription, string AutoCondition);

public static class Flasks
{
    public const int InitialSlots = 3;
    public const int MaximumSlots = 5;
    public static IReadOnlyList<FlaskDefinition> All { get; } =
    [
        new(FlaskKind.Life, "core.flask.life", "生命药剂", 30, 10, 60, 5_000,
            "3秒内恢复50%最大生命，受药剂恢复量与恢复速度修正", "生命低于角色AI设置的药剂阈值"),
        new(FlaskKind.Mana, "core.flask.mana", "法力药剂", 30, 10, 60, 3_000,
            "3秒内恢复30%最大法力，不恢复已保留的法力", "可用法力低于35%"),
        new(FlaskKind.Armor, "core.flask.armor", "玄铁药剂", 40, 20, 100, 3_000,
            "持续5秒，受到的物理击中伤害降低30%", "受到击中后自动使用"),
        new(FlaskKind.Movement, "core.flask.movement", "疾行药剂", 40, 20, 100, 3_000,
            "持续5秒，移动速度提高30%", "与目标距离大于6米"),
        new(FlaskKind.Resistance, "core.flask.resistance", "棱彩药剂", 40, 20, 100, 2_500,
            "持续5秒，受到的法术击中伤害降低25%", "受到击中后自动使用"),
    ];
}

public sealed record LegendaryAffixDefinition(string StableId, string Text);

public sealed record UniqueDefinition
{
    public UniqueDefinition(string stableId, string displayName, string baseStableId,
        string ruleText, bool mythic = false)
        : this(stableId, displayName, baseStableId, BuildAffixes(stableId, ruleText), mythic)
    {
    }

    public UniqueDefinition(string stableId, string displayName, string baseStableId,
        IReadOnlyList<LegendaryAffixDefinition> legendaryAffixes, bool mythic = false)
    {
        StableId = stableId;
        DisplayName = displayName;
        BaseStableId = baseStableId;
        LegendaryAffixes = legendaryAffixes;
        Mythic = mythic;
    }

    public string StableId { get; init; }
    public string DisplayName { get; init; }
    public string BaseStableId { get; init; }
    public IReadOnlyList<LegendaryAffixDefinition> LegendaryAffixes { get; init; }
    public bool Mythic { get; init; }
    public string RuleText => string.Join("；", LegendaryAffixes.Select(affix => affix.Text));

    private static IReadOnlyList<LegendaryAffixDefinition> BuildAffixes(string stableId, string ruleText) =>
        ruleText.Split('；', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select((text, index) => new LegendaryAffixDefinition($"{stableId}.legendary.{index + 1}", text))
            .ToArray();
}

public static class UniqueItems
{
    private static IReadOnlyDictionary<string, string> LegacyIds { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["回响破誓者"] = "core.unique.echoing_oathbreaker", ["无尽行军"] = "core.unique.march_without_end",
        ["鸦群答卷"] = "core.unique.ravens_answer", ["赤誓之环"] = "core.unique.red_vow",
        ["苍誓之环"] = "core.unique.blue_vow", ["园丁筋络"] = "core.unique.gardeners_sinew",
        ["监守甲壳"] = "core.unique.warden_shell", ["琉璃地平线"] = "core.unique.glass_horizon",
        ["葬钟"] = "core.unique.funeral_bell", ["黑潮披挂"] = "core.unique.black_tide",
        ["无星祷衣"] = "core.unique.starless_prayer", ["末旗护符"] = "core.unique.last_banner",
        ["铁月"] = "core.unique.iron_moon", ["空洞守卫"] = "core.unique.hollow_guard",
        ["荆棘行列"] = "core.unique.thorn_procession", ["朝圣者之债"] = "core.unique.pilgrims_debt",
        ["余烬锁链"] = "core.unique.cinder_chain", ["第四圣约"] = "core.unique.fourth_testament",
        ["沉默铁砧"] = "core.unique.silent_anvil", ["猎手蚀影"] = "core.unique.hunters_eclipse",
        ["灰烬记忆"] = "core.unique.ashes_memory", ["墓门重甲"] = "core.unique.grave_plate",
        ["饥馑指环"] = "core.unique.famine_ring", ["终夜守望"] = "core.unique.last_watch",
        ["裂渊獠牙"] = "resources.unique.rift_fang", ["深层回音"] = "resources.unique.deep_echo",
        ["复生之种"] = "resources.unique.seed_of_rebirth", ["荆生树皮"] = "resources.unique.thorned_bark",
        ["行刑者之偿"] = "resources.unique.executioners_due", ["血税契据"] = "resources.unique.blood_tithe",
        ["凝滞一刻"] = "resources.unique.frozen_moment", ["坠星透镜"] = "resources.unique.starfall_lens",
        ["统帅之负"] = "resources.unique.commanders_burden", ["折断军旗"] = "resources.unique.broken_standard",
        ["界行罗盘"] = "resources.unique.wayfarers_compass", ["虚空天平"] = "resources.unique.void_balance",
        ["无名谦冠"] = "builds.unique.humility_crown", ["傲慢之握"] = "builds.unique.arrogance_grasp",
        ["怒节同契"] = "builds.unique.rage_temperance_carapace", ["两极德印"] = "builds.unique.paired_virtue_girdle",
        ["灰烬之心"] = "core.mythic.heart_of_ash",
    };

    public static IReadOnlyList<UniqueDefinition> All { get; } = BuildFormal();

    public static ItemInstance Create(string stableId, int itemLevel, string instanceId)
    {
        UniqueDefinition definition = All.Single(item => item.StableId == stableId);
        return EquipmentLegendaryFactory.CreateByName(definition.DisplayName, itemLevel, instanceId);
    }

    private static IReadOnlyList<UniqueDefinition> BuildFormal() => EquipmentCatalog.LegendaryItems.Select(entry =>
    {
        string baseName = entry.BaseAndSource.Split('；', StringSplitOptions.TrimEntries)[0]
            .Replace("（新增）", string.Empty, StringComparison.Ordinal);
        string baseId = EquipmentCatalog.Bases.Single(value => value.DisplayName == baseName).StableId;
        return new UniqueDefinition(LegacyIds.GetValueOrDefault(entry.DisplayName, entry.Id), entry.DisplayName, baseId,
            entry.FixedAffixesText + "；" + entry.RuleText, entry.Rarity == "Mythic");
    }).ToArray();
}

public enum MapNodeKind
{
    Entrance, Encounter, RouteChoice, AbyssFissure, GardenPlot, Altar, Elite, Boss,
    WarfrontEncounter, WarfrontOfficer, WarfrontCommander,
}
public sealed record MapNode(int Index, MapNodeKind Kind, string DisplayName, int EnemyCount, bool Optional = false,
    string BossStableId = "", GameForWork.Core.Encounters.EncounterRule? Gameplay = null);
public sealed record MapPlan(
    string MapInstanceId, MapRoute Route, IReadOnlyList<MapNode> Nodes, MapAltar Altar,
    IReadOnlyList<string> AtlasSnapshot, int RouteChoiceIndex, string FinalBossStableId);

public static class MapPlanner
{
    public static MapPlan Build(MapItem map, MapRoute route, IEnumerable<string> atlasPassives, ulong seed)
    {
        map.Validate();
        string[] atlas = (map.AtlasSnapshot ?? atlasPassives).Order(StringComparer.Ordinal).ToArray();
        if (EndgameState.IsBreakthroughTrial(map))
            return new(map.InstanceId, route,
            [new(1, MapNodeKind.Entrance, "门扉前庭", 0), new(2, MapNodeKind.Encounter, "百级试炼", 12),
                new(3, MapNodeKind.Elite, "守门双影", 8),
                new(4, MapNodeKind.Boss, Bosses.Breakthrough.DisplayName, 5, BossStableId: Bosses.Breakthrough.StableId)],
                MapAltar.None, atlas, 1, Bosses.Breakthrough.StableId);
        if (EndgameState.IsCitadel(map) || EndgameState.IsCitadelPractice(map))
            return new(map.InstanceId, route,
                [new(1, MapNodeKind.Entrance, "天垒门前", 0),
                    new(2, MapNodeKind.Boss, Bosses.CitadelStages[0].DisplayName, 9, BossStableId: Bosses.CitadelStages[0].StableId),
                    new(3, MapNodeKind.Boss, Bosses.CitadelStages[1].DisplayName, 7, BossStableId: Bosses.CitadelStages[1].StableId),
                    new(4, MapNodeKind.Boss, Bosses.CitadelStages[2].DisplayName, 5, BossStableId: Bosses.CitadelStages[2].StableId)],
                MapAltar.None, atlas, 1, Bosses.CitadelStages[2].StableId);
        return GameForWork.Core.Encounters.Gameplay.Build(map, route, atlas, seed);
    }

}

public sealed record BossSkill(string DisplayName, string DamageType, string Telegraph, bool Avoidable);
public sealed record BossDefinition(
    string StableId, string DisplayName, string AreaStableId, IReadOnlyList<BossSkill> Skills,
    int PhaseThresholdBasisPoints, int EnrageSeconds, string SpecialRule);

public static class Bosses
{
    public static IReadOnlyList<BossDefinition> CampaignBosses { get; } =
    [
        CampaignBoss(1, "余烬守门人", "火墙分割战场"),
        CampaignBoss(2, "谷仓吞噬者", "吞食尸体恢复生命"),
        CampaignBoss(3, "溺亡圣徒", "钟波与潮汐交替"),
        CampaignBoss(4, "无光领路人", "熄灭视野后发动追猎"),
        CampaignBoss(5, "界外之物", "三阶段重组技能序列"),
    ];

    public static IReadOnlyList<BossDefinition> MapBosses { get; } = MapCatalog.Areas.Select((area, index) =>
        new BossDefinition($"core.boss.map.{index + 1:00}", area.BossName, area.StableId,
        [
            new(index % 3 == 0 ? "断阵重击" : "裂界挥扫", "物理", "红边扇形蓄力", true),
            new(index % 3 == 1 ? "追猎冲锋" : "回响投射", index % 2 == 0 ? "火焰" : "冰霜", "箭头与闪烁路径", true),
            new(index % 3 == 2 ? "星骸坠落" : "区域爆发", index % 2 == 0 ? "火焰" : "虚空", "环形描边收缩", true),
        ], 5_000, 90 + index * 3, index % 2 == 0 ? "召唤区域同族并改变站位" : "生成持续危险地面")).ToArray();

    public static IReadOnlyList<BossDefinition> WarfrontOfficers { get; } =
    [
        new("monsters.boss.warfront.iron_banner", "铁旗校尉", "monsters.warfront",
            [new("盾墙推进", "物理", "长方形推进区", true), new("猎首号令", "物理", "红色锁定箭头", true)],
            5_000, 105, "半血后召集盾墙卫士"),
        new("monsters.boss.warfront.ember_cannon", "烬炮监军", "monsters.warfront",
            [new("三点炮击", "火焰", "三枚橙色落点", true), new("压制齐射", "物理", "平行箭道", true)],
            5_000, 105, "交替封锁近场与远场"),
    ];

    public static BossDefinition WarfrontCommander { get; } = new(
        "monsters.boss.warfront.last_marshal", "末旗统帅", "monsters.warfront",
        [new("全军突击", "物理", "多条冲锋箭道", true), new("亡旗炮阵", "火焰", "五枚递进落点", true),
            new("战阵处决", "物理", "赤色收缩扇面", true)], 4_000, 150, "军官阵亡后继承其技能并进入末旗阶段");

    public static BossDefinition Breakthrough { get; } = new("core.boss.gate_trial", "百级门扉化身", "core.endgame.gate_trial",
        [new("门扉碾压", "物理", "交叉重线", true), new("灵能浪潮", "法术", "蓝色双环", true), new("终末审判", "混合", "全屏倒计时", true)],
        5_000, 120, "失败不消耗门票，可重复挑战");

    public static IReadOnlyList<BossDefinition> CitadelStages { get; } =
    [
        new("core.boss.citadel.wall", "活化城墙", "core.endgame.ashen_citadel", [new("落石阵", "物理", "方格阴影", true), new("城垛齐射", "物理", "平行箭头", true), new("熔油", "火焰", "橙色地面边框", true)], 6_000, 120, "破坏三处城垛"),
        new("core.boss.citadel.guards", "灰烬双卫", "core.endgame.ashen_citadel", [new("交叉斩", "物理", "交叉亮线", true), new("誓火链", "火焰", "两点连线", true), new("替身护卫", "法术", "盾形描边", false)], 5_000, 150, "两名守卫相隔过远会复苏"),
        new("core.boss.citadel.core", "天垒核心", "core.endgame.ashen_citadel", [new("核心脉冲", "法术", "三重扩散环", true), new("灰烬坠落", "火焰", "闪烁落点", true), new("誓约抹除", "混合", "黑白全屏边框", true)], 4_000, 180, "资源继承并在低生命进入终局回响"),
    ];

    public static BossDefinition ForArea(string areaId) => MapBosses.FirstOrDefault(boss => boss.AreaStableId == areaId) ?? MapBosses[0];
    private static IEnumerable<BossDefinition> AllBosses => CampaignBosses.Concat(MapBosses)
        .Concat(WarfrontOfficers).Append(WarfrontCommander).Append(Breakthrough).Concat(CitadelStages);

    public static BossDefinition? TryGet(string stableId) => AllBosses
        .FirstOrDefault(item => item.StableId == stableId);

    public static EnemyProfile CombatProfile(string stableId)
    {
        BossDefinition boss = AllBosses
            .FirstOrDefault(item => item.StableId == stableId) ?? MapBosses[0];
        bool endgame = boss == Breakthrough || CitadelStages.Contains(boss) || WarfrontOfficers.Contains(boss) || boss == WarfrontCommander;
        EnemySkillProfile[] skills = boss.Skills.Select((skill, index) => new EnemySkillProfile(
            (index % 3) switch { 0 => EnemySkillKind.HeavySlam, 1 => EnemySkillKind.Charge, _ => EnemySkillKind.DelayedNova },
            skill.DisplayName,
            skill.DamageType switch
            {
                "火焰" => EnemyDamageType.Fire,
                "冰霜" => EnemyDamageType.Cold,
                "闪电" or "法术" => EnemyDamageType.Lightning,
                "虚空" or "混合" => EnemyDamageType.Void,
                _ => EnemyDamageType.Physical,
            },
            13_500 + index * 1_500, 11_000 + index * 1_500, index == 1 ? 5_500 : 2_400,
            index != 1, skill.Telegraph, skill.Avoidable, IsSpell: index == 2 || skill.DamageType == "法术")).ToArray();
        if (boss == WarfrontCommander)
            skills = skills.Concat(WarfrontOfficers.SelectMany(officer => CombatProfile(officer.StableId).EffectiveSkills)).ToArray();
        if (boss == WarfrontOfficers[0]) skills[0] = skills[0] with { Kind = EnemySkillKind.ShieldLink };
        if (boss == WarfrontOfficers[1]) skills[0] = skills[0] with { Kind = EnemySkillKind.Artillery };
        return new EnemyProfile(boss.StableId, boss.DisplayName, endgame ? 520 : 310,
            endgame ? 13 : 9, endgame ? 21 : 15, endgame ? 38 : 24, 8, 78, 2_100, 1_000, 0,
            EnemyFamily.Boss, EnemyRole.Melee, skills[0].Kind, skills[0].RangeRaw, skills);
    }

    private static BossDefinition CampaignBoss(int act, string name, string rule) => new(
        $"monsters.boss.campaign.act{act}", name, $"core.campaign.act{act}",
        [new("阶段重击", "物理", "扇形蓄力", true), new("幕终异象", act is 1 or 2 ? "火焰" : "虚空", "收缩双环", true),
            new("追猎技", "物理", "闪烁路径", true)], 5_000, 90 + act * 8, rule);
}

public sealed record AltarChoice(string StableId, string Cost, string Reward, int RiskStacks);
public static class Altars
{
    public static IReadOnlyList<AltarChoice> Choices(MapAltar altar, int tier) => altar switch
    {
        MapAltar.RedOath =>
        [new("red.blood_price", "最大生命降低 8%", "立即获得金币与金属", 1), new("red.hunted", "玩家受伤提高 12%", "额外稀有物品", 1), new("red.frail", "药剂恢复降低 20%", "地图数量提高", 2)],
        MapAltar.BlueOath =>
        [new("blue.hardened", "敌人生命提高 20%", "Boss 后结算通货", 1), new("blue.swift", "敌人速度提高 18%", "Boss 后结算传奇几率", 1), new("blue.overlord", "Boss 伤害提高 25%", "Boss 后奖励翻倍", 2)],
        _ => [],
    };
}

public sealed record AtlasEffect(int MechanicWeightBasisPoints, int RewardBasisPoints, bool BlocksMechanic, bool NotableRule);
public static class AtlasRules
{
    public static AtlasEffect Resolve(IEnumerable<string> allocated, AtlasTheme theme)
    {
        AtlasPassiveNode[] nodes = allocated.Select(AtlasTree.Get).Where(node => node.Theme == theme).ToArray();
        return new(10_000 + nodes.Sum(node => node.MechanicWeightBasisPoints), 10_000 + nodes.Sum(node => node.RewardBasisPoints),
            nodes.Any(node => node.BlocksCompetingMechanic), nodes.Any(node => !string.IsNullOrEmpty(node.SpecialRule)));
    }
}

public enum GardenCraft
{
    KeepPrefixes, KeepSuffixes,
    BiasLife, BiasDefense, BiasAttack, BiasSpell, BiasSpeed, BiasCritical,
    ReplaceLife, ReplaceDefense, ReplaceAttack, ReplaceSpell, ReplaceSpeed, ReplaceCritical,
}
public static class GardenCrafting
{
    public static int Cost(GardenCraft craft) => IsReplacement(craft) ? 200 :
        craft is GardenCraft.KeepPrefixes or GardenCraft.KeepSuffixes ? 80 : 40;
    public static IReadOnlyList<AffixRoll> SelectRetained(ItemInstance item, GardenCraft craft) => craft switch
    {
        GardenCraft.KeepPrefixes => item.Affixes.Where(affix => affix.Definition.Position == AffixPosition.Prefix).ToArray(),
        GardenCraft.KeepSuffixes => item.Affixes.Where(affix => affix.Definition.Position == AffixPosition.Suffix).ToArray(),
        GardenCraft.BiasLife => item.Affixes.Where(affix => HasAny(affix.Definition,
            ItemModifierKind.FlatMaximumLife, ItemModifierKind.IncreasedMaximumLifeBasisPoints,
            ItemModifierKind.MaximumLifeRegenerationBasisPoints, ItemModifierKind.IncreasedLifeFlaskEffectBasisPoints)).ToArray(),
        GardenCraft.BiasDefense => item.Affixes.Where(affix => HasAny(affix.Definition,
            ItemModifierKind.FlatArmor, ItemModifierKind.FlatEvasion, ItemModifierKind.FlatShield, ItemModifierKind.FlatSpiritBarrier,
            ItemModifierKind.IncreasedArmorBasisPoints, ItemModifierKind.IncreasedEvasionBasisPoints,
            ItemModifierKind.IncreasedShieldBasisPoints, ItemModifierKind.IncreasedSpiritBarrierBasisPoints)).ToArray(),
        _ => item.Affixes.Where(affix => HasAny(affix.Definition,
            ItemModifierKind.AddedPhysicalDamage, ItemModifierKind.AddedMinimumPhysicalDamage,
            ItemModifierKind.IncreasedPhysicalDamageBasisPoints, ItemModifierKind.IncreasedAttackDamageBasisPoints,
            ItemModifierKind.IncreasedAttackSpeedBasisPoints)).ToArray(),
    };

    public static ItemInstance Apply(ItemInstance item, GardenCraft craft, ulong seed)
    {
        if (!CanApply(item, craft)) throw new InvalidOperationException("命能加工要求可修改的稀有装备及至少一条合法目标词缀。");
        var random = new Pcg32(seed);
        if (IsReplacement(craft)) return Replace(item, craft, random);
        bool keep = craft is GardenCraft.KeepPrefixes or GardenCraft.KeepSuffixes;
        var affixes = keep ? SelectRetained(item, craft).ToList() : new List<AffixRoll>();
        int target = keep ? affixes.Count + 3 : 4 + (int)(random.NextUInt() % 3);
        while (affixes.Count < target)
        {
            AffixDefinition[] pool = Legal(item).Where(d =>
                (!keep || d.Position != (craft == GardenCraft.KeepPrefixes ? AffixPosition.Prefix : AffixPosition.Suffix)) &&
                affixes.Count(a => a.Definition.Position == d.Position) < 3 &&
                affixes.All(a => a.Definition.StableFamilyId != d.StableFamilyId && a.Definition.MutualExclusionGroup != d.MutualExclusionGroup) &&
                (keep || affixes.Count > 0 || Tagged(d, craft))).ToArray();
            if (pool.Length == 0) break;
            int total = pool.Sum(d => d.WeightFor(item.Base));
            int roll = (int)(random.NextUInt() % (uint)total);
            AffixDefinition selected = pool[^1];
            foreach (AffixDefinition candidate in pool) { roll -= candidate.WeightFor(item.Base); if (roll < 0) { selected = candidate; break; } }
            affixes.Add(Roll(selected, random));
        }
        return item with { Affixes = affixes };
    }

    public static bool CanApply(ItemInstance item, GardenCraft craft) => Enum.IsDefined(craft) && item.CanModify && item.Rarity == ItemRarity.Rare &&
        (craft is GardenCraft.KeepPrefixes or GardenCraft.KeepSuffixes || Legal(item).Any(d => Tagged(d, BiasOf(craft)))) &&
        (!IsReplacement(craft) || CanReplace(item, craft));
    private static IEnumerable<AffixDefinition> Legal(ItemInstance item) => Affixes.For(item.Base, item.ItemLevel)
        .Where(d => d.WeightFor(item.Base) > 0);
    public static bool Tagged(AffixDefinition d, GardenCraft craft) => BiasOf(craft) switch
    {
        GardenCraft.BiasLife => d.ModTags?.Contains("life") == true || HasAny(d, ItemModifierKind.FlatMaximumLife,
            ItemModifierKind.IncreasedMaximumLifeBasisPoints, ItemModifierKind.MaximumLifeRegenerationBasisPoints),
        GardenCraft.BiasDefense => d.ModTags?.Contains("defences") == true || HasAny(d, ItemModifierKind.FlatArmor,
            ItemModifierKind.FlatEvasion, ItemModifierKind.FlatShield, ItemModifierKind.FlatSpiritBarrier,
            ItemModifierKind.IncreasedArmorBasisPoints, ItemModifierKind.IncreasedEvasionBasisPoints,
            ItemModifierKind.IncreasedShieldBasisPoints, ItemModifierKind.IncreasedSpiritBarrierBasisPoints),
        GardenCraft.BiasSpell => d.ModTags?.Contains("caster") == true || d.ModTags?.Contains("spell") == true ||
            HasAny(d, ItemModifierKind.IncreasedSpellDamageBasisPoints, ItemModifierKind.IncreasedCastSpeedBasisPoints),
        GardenCraft.BiasSpeed => d.ModTags?.Contains("speed") == true || HasAny(d,
            ItemModifierKind.IncreasedAttackSpeedBasisPoints, ItemModifierKind.IncreasedCastSpeedBasisPoints,
            ItemModifierKind.IncreasedMovementSpeedBasisPoints, ItemModifierKind.IncreasedCooldownRecoveryBasisPoints,
            ItemModifierKind.ProjectileSpeedBasisPoints),
        GardenCraft.BiasCritical => d.ModTags?.Contains("critical") == true || HasAny(d,
            ItemModifierKind.IncreasedCriticalChanceBasisPoints, ItemModifierKind.IncreasedCriticalMultiplierBasisPoints),
        _ => d.ModTags?.Contains("attack") == true || HasAny(d, ItemModifierKind.AddedPhysicalDamage,
            ItemModifierKind.AddedMinimumPhysicalDamage, ItemModifierKind.IncreasedPhysicalDamageBasisPoints,
            ItemModifierKind.IncreasedAttackDamageBasisPoints, ItemModifierKind.IncreasedAttackSpeedBasisPoints),
    };

    public static bool IsReplacement(GardenCraft craft) => craft is >= GardenCraft.ReplaceLife and <= GardenCraft.ReplaceCritical;

    private static GardenCraft BiasOf(GardenCraft craft) => craft switch
    {
        GardenCraft.ReplaceLife => GardenCraft.BiasLife,
        GardenCraft.ReplaceDefense => GardenCraft.BiasDefense,
        GardenCraft.ReplaceAttack => GardenCraft.BiasAttack,
        GardenCraft.ReplaceSpell => GardenCraft.BiasSpell,
        GardenCraft.ReplaceSpeed => GardenCraft.BiasSpeed,
        GardenCraft.ReplaceCritical => GardenCraft.BiasCritical,
        _ => craft,
    };

    private static bool CanReplace(ItemInstance item, GardenCraft craft) => item.Affixes
        .Where(removed => !removed.Crafted && !item.IsFractured(removed)).Any(removed =>
        {
            AffixRoll[] remaining = item.Affixes.Where(affix => !ReferenceEquals(affix, removed)).ToArray();
            return Legal(item).Any(definition => Tagged(definition, BiasOf(craft)) &&
                remaining.Count(affix => affix.Definition.Position == definition.Position) < 3 &&
                remaining.All(affix => affix.Definition.MutualExclusionGroup != definition.MutualExclusionGroup));
        });

    private static ItemInstance Replace(ItemInstance item, GardenCraft craft, Pcg32 random)
    {
        AffixRoll[] removable = item.Affixes.Where(affix => !affix.Crafted && !item.IsFractured(affix)).ToArray();
        var choices = removable.Select(removed =>
        {
            List<AffixRoll> remaining = item.Affixes.Where(affix => !ReferenceEquals(affix, removed)).ToList();
            AffixDefinition[] candidates = Legal(item).Where(definition => Tagged(definition, BiasOf(craft)) &&
                remaining.Count(affix => affix.Definition.Position == definition.Position) < 3 &&
                remaining.All(affix => affix.Definition.MutualExclusionGroup != definition.MutualExclusionGroup)).ToArray();
            return (remaining, candidates);
        }).Where(choice => choice.candidates.Length > 0).ToArray();
        if (choices.Length == 0) throw new InvalidOperationException("移除后没有合法的偏向替换词缀。");
        (List<AffixRoll> affixes, AffixDefinition[] pool) = choices[(int)(random.NextUInt() % (uint)choices.Length)];
        int total = pool.Sum(definition => definition.WeightFor(item.Base));
        int roll = (int)(random.NextUInt() % (uint)total);
        AffixDefinition selected = pool[^1];
        foreach (AffixDefinition definition in pool)
        {
            roll -= definition.WeightFor(item.Base);
            if (roll < 0) { selected = definition; break; }
        }
        affixes.Add(Roll(selected, random));
        return item with { Affixes = affixes };
    }

    private static AffixRoll Roll(AffixDefinition definition, Pcg32 random)
    {
        RolledAffixComponent[] components = definition.EffectComponents.Select(component =>
            new RolledAffixComponent(component.Kind,
                component.MinimumValue == component.MaximumValue ? component.MinimumValue :
                    component.MinimumValue + (int)(random.NextUInt() % (uint)(component.MaximumValue - component.MinimumValue + 1)),
                component.Scope, component.DisplayText)).ToArray();
        return new AffixRoll(definition, components[0].Value, Components: components);
    }

    private static bool HasAny(AffixDefinition definition, params ItemModifierKind[] kinds) =>
        definition.EffectComponents.Any(component => kinds.Contains(component.Kind));
}

public sealed record GardenCraftResult(bool Succeeded, string Summary, ItemInstance? Result, int Cost);

public sealed record TierRule(int Tier, string StableId, string DisplayName, string Effect);
public static class TierRules
{
    public static IReadOnlyList<TierRule> FinalTiers { get; } =
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

public sealed record MechanicResult(
    bool Completed, bool MapMayContinue, int RewardMultiplierBasisPoints, int LifeForce,
    int CurrencyBundles, string Summary);

public static class MechanicRules
{
    public static MechanicResult ResolveAbyss(int wavesCleared, int requiredWaves, int remainingSeconds)
    {
        bool completed = wavesCleared >= requiredWaves;
        return new(completed, true, completed ? 14_000 : 10_000,
            0, completed ? 1 + requiredWaves / 2 : 0,
            completed ? "裂隙已追至宝箱，获得通货与天垒碎片机会。" : "裂隙闭合；额外奖励丢失，地图继续。");
    }

    public static MechanicResult ResolveGarden(IReadOnlyList<int> selectedPlotRisks, int tier)
    {
        if (selectedPlotRisks.Count != 3 || selectedPlotRisks.Any(risk => risk is < 0 or > 2))
            throw new ArgumentOutOfRangeException(nameof(selectedPlotRisks));
        int risk = selectedPlotRisks.Sum();
        return new(true, true, 10_000 + risk * 900, Math.Max(3, tier * (3 + risk)), 0,
            $"三块苗圃已收割，获得 {Math.Max(3, tier * (3 + risk))} 命能。");
    }

    public static AltarChoice SelectAltar(MapAltar altar, int tier, int choiceIndex)
    {
        IReadOnlyList<AltarChoice> choices = Altars.Choices(altar, tier);
        if (choiceIndex < 0 || choiceIndex >= choices.Count) throw new ArgumentOutOfRangeException(nameof(choiceIndex));
        return choices[choiceIndex];
    }
}
