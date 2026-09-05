using GameForWork.Core.Campaign.World;
using GameForWork.Core.Campaign.Combat;

namespace GameForWork.Core.Atlas;

public enum AtlasCategory
{
    MapBasics,
    Supply,
    Crafting,
    PacksAndElites,
    Boss,
    Abyss,
    LifeGarden,
    RedOath,
    BlueOath,
    Warfront,
}

public enum AtlasGate
{
    Act5,
    Tier5,
    Tier10,
    Tier16,
    FinalBreakthrough,
    Tier20,
}

public sealed record AtlasMapNode(
    string StableId,
    string DisplayName,
    AtlasCategory Category,
    int Position,
    int GoldCost,
    AtlasGate Gate,
    string Effect,
    string? PrerequisiteId,
    bool OptionalDifficulty = false,
    bool HiddenUntilWarfront = false);

public static class AtlasCatalog
{
    public const int MaximumNodes = 120;
    public const int TotalGoldCost = 477_000;
    public static readonly IReadOnlyList<int> Costs = [100, 200, 300, 500, 800, 1_200, 1_800, 2_800, 4_000, 6_000, 10_000, 20_000];

    private static readonly (AtlasCategory Category, string Prefix, string CategoryName, string[] Names, string[] Effects, int[] Optional)[] Source =
    [
        (AtlasCategory.MapBasics, "map", "地图基础",
            ["历练路印", "掘金路印", "冶炼路印", "勘探宝箱", "装备增产", "技能石增产", "珍品搜寻", "岔路勘察", "机制增产", "偏爱地图", "深度偏爱", "道路全览"],
            ["地图经验提高 20%", "地图金币获取提高 20%", "地图金属获取提高 20%", "完成地图有 40% 概率生成基础预算的勘探宝箱", "装备数量提高 20%", "技能石数量提高 40%", "非传奇装备稀有度提高 40%", "出现第二个非安全玩法候选的概率提高 75%", "玩法资源获取提高 40%", "偏爱地图权重提高 200%", "偏爱地图额外提高 300%，合计 500%", "新地图至少拥有 2 个非安全玩法候选；不足时使用全部已解锁候选"], []),
        (AtlasCategory.Supply, "supply", "地图续航",
            ["地图增产", "同阶偏向", "升阶偏向", "首领补给", "额外地图", "充足供给", "道路回响增幅", "续航保底", "玩法地图", "腐化地图", "偏爱补给", "同阶保障"],
            ["地图掉落数量提高 20%", "同阶地图掉落权重提高 40%", "+1 阶地图掉落权重提高 25%", "Boss 至少掉落 T-1 地图", "额外地图掉落概率提高 30%", "地图额外掉落提高 30%，与前置合计 50%", "道路回响效果提高 100%", "地图掉落保底由连续 3 次未掉落缩短为 2 次", "玩法怪物掉落地图提高 50%", "腐化地图掉落提高 80%", "每完成 3 张地图获得 1 张偏爱地图", "首领补给升级为保证掉落同阶地图"], []),
        (AtlasCategory.Crafting, "craft", "地图打造",
            ["精良地图", "强化抛光", "打造材料", "地图商人", "节材", "双重炼金", "锁词混沌", "定向重铸", "腐化储备", "腐化残渣", "双重腐化", "腐化抉择"],
            ["掉落地图品质 +4", "抛光操作改为品质 +10", "地图打造材料掉落提高 30%", "出售地图获得的金币提高 50%", "非腐化打造有 25% 概率返还材料", "稀有升级独立掷 2 次，保留更符合当前打造筛选器的结果", "混沌重铸可锁定 1 条词缀，材料消耗为 2", "定向重铸必含指定词缀组，材料消耗为 3", "腐化铁掉落提高 50%", "腐化摧毁地图时返还其出售金币与 1 份普通地图材料", "腐化存活时独立掷 2 次规则，按腐化偏好自动选择", "腐化存活时显示 2 个规则供玩家选择；无人值守时按偏好自动选择"], []),
        (AtlasCategory.PacksAndElites, "pack", "怪群精英",
            ["普通怪预算", "魔法怪预算", "稀有怪预算", "密集战利品", "魔法珍品", "稀有珍品", "群雄增幅", "精锐增援", "高阶稀有", "底材追猎", "精英挑战", "稀有献礼"],
            ["普通怪掉落预算提高 20%", "魔法怪掉落预算提高 40%", "稀有怪掉落预算提高 60%", "每个怪物词缀使该怪物掉落预算提高 10%", "魔法怪掉落装备稀有度提高 50%", "稀有怪掉落装备稀有度提高 80%", "群雄的怪群升级效果提高 100%（仍受上限约束）", "精锐额外增加 1 名稀有怪，计入怪物数量", "稀有怪掉落物品等级 +1", "指定底材掉落权重提高 100%", "可选：魔法与稀有怪额外拥有 1 条词缀，奖励造成 100% 更多收益", "每只稀有怪保证掉落 1 件稀有装备和 1 份高阶金属"], [11]),
        (AtlasCategory.Boss, "boss", "Boss攻坚",
            ["首领装备", "首领财宝", "首领地图", "首领宝箱", "碎片增产", "专属传奇", "高阶战利品", "王座增幅", "首领技能石", "传奇保底", "专属增幅", "双重追猎"],
            ["Boss 装备数量提高 40%", "Boss 金币与金属获取提高 50%", "Boss 地图掉落提高 50%", "Boss 生成基础预算 50% 的奖励宝箱", "Boss 碎片掉落提高 100%", "Boss 专属传奇基础概率增加 4 个百分点", "Boss 掉落物品等级额外 +2（合计 +4）", "Boss 形态收益后缀的额外奖励提高 100%", "Boss 技能石掉落提高 100%", "连续 10 次未获得专属传奇时保证掉落", "专属传奇概率再增加 4 个百分点（基础 8% 提高到 16%）", "进行第二次独立专属传奇判定，概率为当前概率的一半；总概率 22.72%"], []),
        (AtlasCategory.Abyss, "abyss", "深渊",
            ["裂渊收益", "裂渊技能石", "危险金属", "深渊强度三", "裂渊稀有", "技能异变", "守望者踪迹", "深渊强度四", "裂渊碎片", "裂渊传奇", "深渊强度五", "最终守望者"],
            ["深渊奖励提高 40%", "深渊技能石掉落提高 60%", "深渊危险金属掉落提高 60%", "解锁强度 3：怪物 50% 更多生命、30% 更多伤害，奖励 75% 更多", "深渊稀有怪奖励提高 80%", "深渊技能品质与异变权重提高 100%", "深渊守望者出现权重提高 50%", "解锁强度 4：怪物 100% 更多生命、60% 更多伤害，奖励 150% 更多", "深渊碎片掉落提高 100%", "深渊专属传奇掉落提高 100%", "解锁强度 5：怪物 150% 更多生命、90% 更多伤害，奖励 250% 更多", "强度 5 最终遭遇必定是守望者，最终奖励 100% 更多；深渊内禁用救援"], [4, 8, 11, 12]),
        (AtlasCategory.LifeGarden, "garden", "命能花园",
            ["命能增产", "额外选项", "花园战利品", "初次刷新", "标签培育", "底材培育", "高阶收获", "二次刷新", "保留选项", "花园首领", "双生地块", "三重丰收"],
            ["命能获取提高 50%", "花园奖励选项 +1", "花园装备与金属获取提高 50%", "每座花园可刷新奖励 1 次", "所选词缀标签权重提高 100%", "打造底材掉落权重提高 100%", "花园奖励等级 +1", "每座花园获得第二次刷新", "刷新时可锁定保留 1 个选项", "花园 Boss 出现权重提高 50%", "可选：同时培育 2 个地块；怪物 75% 更多生命、45% 更多伤害，奖励 100% 更多", "可选：同时培育 3 个共享词缀地块；怪物 150% 更多生命、90% 更多伤害，奖励 250% 更多，并替代双生地块"], [11, 12]),
        (AtlasCategory.RedOath, "red", "赤誓祭坛",
            ["赤誓收益", "赤誓金币", "赤誓金属", "四重选择", "赤誓地图", "祭坛重掷", "额外祭坛", "赤誓稀有", "高阶材料", "赤誓高压", "处刑者", "赤誓极限"],
            ["赤誓奖励提高 50%", "赤誓金币获取提高 80%", "赤誓金属获取提高 80%", "祭坛奖励选项增加到 4 个", "赤誓地图掉落提高 80%", "每座祭坛可重掷 1 次", "每张地图额外生成 1 座祭坛", "赤誓稀有装备掉落提高 80%", "高阶材料权重提高 150%", "可选：祭坛惩罚效果提高 75%，奖励 150% 更多", "处刑者出现权重提高 50%，并启用专属掉落池", "可选：至少生成 3 座祭坛；惩罚效果提高 150%，奖励 300% 更多，并替代赤誓高压"], [10, 12]),
        (AtlasCategory.BlueOath, "blue", "苍誓祭坛",
            ["苍誓收益", "高阶底材", "苍誓技能石", "四重选择", "传奇追寻", "祭坛重掷", "延迟增幅", "首领增幅", "苍誓物等", "苍誓高压", "成功保底", "苍誓极限"],
            ["苍誓奖励提高 50%", "高阶底材掉落提高 80%", "苍誓技能石掉落提高 80%", "祭坛奖励选项增加到 4 个", "苍誓传奇池权重提高 100%", "每座祭坛可重掷 1 次", "延迟奖励提高 80%", "Boss 苍誓奖励提高 80%", "苍誓高阶底材物品等级 +2", "可选：Boss 75% 更多生命、50% 更多伤害，奖励 150% 更多", "连续 10 次成功但未获得目标奖励时保证出现", "可选：Boss 150% 更多生命、100% 更多伤害，所有奖励延迟到完成后，奖励 300% 更多；失败失去苍誓额外奖励，并替代苍誓高压"], [10, 12]),
        (AtlasCategory.Warfront, "warfront", "战阵前线",
            ["战功增产", "前线装备", "前线金属", "四重军需", "军官奖励", "军需偏向", "指挥官", "声望增产", "军官底材", "前线碎片", "扩大战线", "决战前线"],
            ["战功获取提高 50%", "战阵装备数量提高 50%", "战阵金属获取提高 50%", "军需奖励选项增加到 4 个", "军官奖励提高 80%", "可选择武器、护甲、首饰或材料作为军需偏向", "精锐军官出现权重提高 50%（最终统帅固定出现）", "战阵声望获取提高 100%", "军官掉落底材权重提高 100%", "战阵 Boss 碎片掉落提高 100%", "可选：战线增加 2 个节点，军官数量翻倍，奖励 150% 更多", "可选：战线增加 4 个节点；敌人 150% 更多生命、90% 更多伤害，禁用救援，奖励 300% 更多，并替代扩大战线"], [11, 12]),
    ];

    private static readonly IReadOnlyList<AtlasMapNode> Nodes = Build();
    private static readonly IReadOnlyDictionary<string, AtlasMapNode> ById = Nodes.ToDictionary(node => node.StableId, StringComparer.Ordinal);
    public static IReadOnlyList<AtlasMapNode> All => Nodes;
    public static AtlasMapNode Get(string id) => ById.TryGetValue(id, out AtlasMapNode? node)
        ? node : throw new KeyNotFoundException($"Unknown Atlas atlas node: {id}");

    public static bool GateSatisfied(AtlasMapNode node, int maximumCompletedTier, bool finalBreakthroughCompleted,
        bool warfrontDiscovered)
    {
        if (node.HiddenUntilWarfront && !warfrontDiscovered) return false;
        return node.Gate switch
        {
            AtlasGate.Act5 => true,
            AtlasGate.Tier5 => maximumCompletedTier >= 5,
            AtlasGate.Tier10 => maximumCompletedTier >= 10,
            AtlasGate.Tier16 => maximumCompletedTier >= 16,
            AtlasGate.FinalBreakthrough => finalBreakthroughCompleted,
            AtlasGate.Tier20 => maximumCompletedTier >= 20,
            _ => false,
        };
    }

    private static IReadOnlyList<AtlasMapNode> Build()
    {
        var result = new List<AtlasMapNode>(MaximumNodes);
        foreach ((AtlasCategory category, string prefix, string _, string[] names, string[] effects, int[] optional) in Source)
        {
            for (int index = 0; index < 12; index++)
            {
                int position = index + 1;
                AtlasGate gate = position switch
                {
                    <= 3 => AtlasGate.Act5,
                    <= 6 => AtlasGate.Tier5,
                    <= 8 => AtlasGate.Tier10,
                    <= 10 => AtlasGate.Tier16,
                    11 => AtlasGate.FinalBreakthrough,
                    _ => AtlasGate.Tier20,
                };
                string id = $"atlas.atlas.{prefix}.{position:00}";
                result.Add(new AtlasMapNode(id, names[index], category, position, Costs[index], gate, effects[index],
                    position == 1 ? null : $"atlas.atlas.{prefix}.{position - 1:00}", optional.Contains(position),
                    category == AtlasCategory.Warfront));
            }
        }
        if (result.Count != MaximumNodes || result.Sum(node => node.GoldCost) != TotalGoldCost)
            throw new InvalidOperationException("Atlas atlas catalog contract is invalid.");
        return result;
    }
}

public static class AtlasPurchase
{
    public static bool TryPurchase(ISet<string> allocated, AtlasMapNode node, TownEconomyState economy,
        int maximumCompletedTier, bool finalBreakthroughCompleted, bool warfrontDiscovered)
    {
        ArgumentNullException.ThrowIfNull(allocated);
        ArgumentNullException.ThrowIfNull(economy);
        if (allocated.Contains(node.StableId) || allocated.Count >= AtlasCatalog.MaximumNodes ||
            node.PrerequisiteId is not null && !allocated.Contains(node.PrerequisiteId) ||
            !AtlasCatalog.GateSatisfied(node, maximumCompletedTier, finalBreakthroughCompleted, warfrontDiscovered) ||
            !economy.TrySpendGold(node.GoldCost)) return false;
        return allocated.Add(node.StableId);
    }
}

public static class AtlasEffects
{
    public static bool Has(IReadOnlyList<string>? allocated, string id) => allocated?.Contains(id, StringComparer.Ordinal) == true;

    public static int EquipmentQuantityIncrease(IReadOnlyList<string>? allocated, bool boss) =>
        (Has(allocated, "atlas.atlas.map.05") ? 2_000 : 0) + (boss && Has(allocated, "atlas.atlas.boss.01") ? 4_000 : 0);

    public static int GoldIncrease(IReadOnlyList<string>? allocated, bool boss) =>
        (Has(allocated, "atlas.atlas.map.02") ? 2_000 : 0) + (boss && Has(allocated, "atlas.atlas.boss.02") ? 5_000 : 0);

    public static int MetalIncrease(IReadOnlyList<string>? allocated, bool boss) =>
        (Has(allocated, "atlas.atlas.map.03") ? 2_000 : 0) + (boss && Has(allocated, "atlas.atlas.boss.02") ? 5_000 : 0);

    public static int SkillStoneIncrease(IReadOnlyList<string>? allocated, bool boss) =>
        (Has(allocated, "atlas.atlas.map.06") ? 4_000 : 0) + (boss && Has(allocated, "atlas.atlas.boss.09") ? 10_000 : 0);

    public static int EquipmentRarityIncrease(IReadOnlyList<string>? allocated, EnemyRarity source) =>
        (Has(allocated, "atlas.atlas.map.07") ? 4_000 : 0) +
        (source == EnemyRarity.Magic && Has(allocated, "atlas.atlas.pack.05") ? 5_000 : 0) +
        (source == EnemyRarity.Rare && Has(allocated, "atlas.atlas.pack.06") ? 8_000 : 0);

    public static int MapQuantityIncrease(IReadOnlyList<string>? allocated, bool boss) =>
        (Has(allocated, "atlas.atlas.supply.01") ? 2_000 : 0) +
        (Has(allocated, "atlas.atlas.supply.05") ? 3_000 : 0) +
        (Has(allocated, "atlas.atlas.supply.06") ? 3_000 : 0) +
        (boss && Has(allocated, "atlas.atlas.boss.03") ? 5_000 : 0);

    public static int EnemyBudgetIncrease(IReadOnlyList<string>? allocated, EnemyRarity rarity) => rarity switch
    {
        EnemyRarity.Normal when Has(allocated, "atlas.atlas.pack.01") => 2_000,
        EnemyRarity.Magic when Has(allocated, "atlas.atlas.pack.02") => 4_000,
        EnemyRarity.Rare when Has(allocated, "atlas.atlas.pack.03") => 6_000,
        _ => 0,
    };

    public static int ExperienceIncrease(IReadOnlyList<string>? allocated) => Has(allocated, "atlas.atlas.map.01") ? 2_000 : 0;
    public static int MapSaleIncrease(IReadOnlyList<string>? allocated) => Has(allocated, "atlas.atlas.craft.04") ? 5_000 : 0;
}
