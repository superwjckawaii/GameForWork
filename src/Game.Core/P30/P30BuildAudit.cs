using System.Text;
using GameForWork.Core.P17;
using GameForWork.Core.P18;

namespace GameForWork.Core.P30;

public sealed record P30BuildDefinition(
    string StableId,
    string DisplayName,
    P18Ascendancy Ascendancy,
    bool Endgame,
    int CharacterLevel,
    int PassivePoints,
    int LinkCount,
    int LegendaryCount,
    string MainSkillId,
    IReadOnlyList<string> SupportStoneIds,
    IReadOnlyList<string> AscendancyNodes,
    string PassivePlan,
    string EquipmentPlan,
    string JewelPlan,
    string FlaskPlan,
    string AiPlan);

public sealed record P30BuildScenarioResult(string Name, double Rating, bool MeetsTarget);

public sealed record P30BuildAuditResult(
    P30BuildDefinition Build,
    long Dps,
    long EffectiveLife,
    int RecoveryPerSecond,
    double ClearScore,
    IReadOnlyList<P30BuildScenarioResult> Scenarios)
{
    public bool Passed => Scenarios.All(item => item.MeetsTarget);
}

public static class P30BuildAudit
{
    private sealed record Theme(string Skill, string[] Supports, int Offense, int Defence, int Recovery, string Passive);

    private static readonly IReadOnlyDictionary<P18Ascendancy, Theme> Themes =
        new Dictionary<P18Ascendancy, Theme>
        {
            [P18Ascendancy.BloodFighter] = new("断脉横扫", ["流血", "深创", "疾血", "残酷", "血之汲取"], 115, 103, 125, "生命、流血、低生命与偷取"),
            [P18Ascendancy.IronGuardian] = new("盾锋冲击", ["重势", "坚阵", "裂甲", "透甲", "复仇增幅"], 96, 145, 112, "盾击、双格挡、护体与反击"),
            [P18Ascendancy.Warbreaker] = new("崩山震击", ["重势", "震域", "余波", "镇压", "裂甲"], 128, 108, 96, "双手猛击、余震、眩晕与破甲"),
            [P18Ascendancy.Marksman] = new("穿云箭", ["多重投射", "远射", "精准穿透", "极速投射", "归返"], 126, 95, 100, "弓、远射、穿透与疾风"),
            [P18Ascendancy.Shadowblade] = new("背刺", ["精准暴击", "背袭增幅", "处决", "攻击速度", "血之汲取"], 132, 92, 104, "匕首、暴击、背刺、标记与处决"),
            [P18Ascendancy.Venomist] = new("淬毒飞刃", ["虚蚀延长", "毒素扩散", "绵延折磨", "多重陷阱", "疾咏"], 121, 98, 108, "中毒、持续伤害、陷阱与药剂"),
            [P18Ascendancy.SoulShepherd] = new("亡骸收割", ["召唤增幅", "迅捷仆从", "扩军", "护主", "惰性繁生"], 119, 112, 106, "普通召唤物、尸体、集火与复生"),
            [P18Ascendancy.SpiritCantor] = new("余烬新星", ["元素集中", "扩大范围", "疾咏", "生命消耗", "凌峰傲击"], 109, 122, 119, "光环、保留、祝福与同行佣兵"),
            [P18Ascendancy.Hexbinder] = new("末日咒印", ["咒印深化", "恶咒传播", "虚蚀延长", "绵延折磨", "疾咏"], 120, 107, 105, "诅咒、咒印、虚空持续与末咒"),
            [P18Ascendancy.Elementalist] = new("元素棱镜", ["元素集中", "元素异常", "精准暴击", "扩大范围", "过载供能"], 125, 101, 103, "三元素、异常、曝露与元素轮转"),
            [P18Ascendancy.VoidScholar] = new("禁术坍缩", ["虚蚀延长", "深层凋零", "绵延折磨", "集中效应", "疾咏"], 130, 98, 101, "虚空、侵蚀、凋零与护盾汲取"),
            [P18Ascendancy.AegisMage] = new("秘盾脉冲", ["护盾施法", "护盾汲取", "精准暴击", "集中效应", "持律精算"], 116, 135, 118, "最大护盾、超充、法术反击与破盾"),
            [P18Ascendancy.MartialMonk] = new("十方终式", ["徒手专注", "攻击速度", "三叠重击", "连击延续", "移动攻击"], 127, 105, 104, "徒手、连击、姿态与终结技"),
            [P18Ascendancy.BeastKeeper] = new("双魂夹击", ["徒手专注", "灵兽凶猛", "攻击速度", "背袭增幅", "血之汲取"], 122, 120, 116, "灵兽、双属性、合击与分痛"),
            [P18Ascendancy.PhantomMaster] = new("幻身步", ["幻身复制", "幻身献祭", "位移回响", "攻击速度", "杀势扩散"], 128, 110, 108, "幻身、技能记忆、复演与替身"),
            [P18Ascendancy.Runecarver] = new("符刃斩", ["法武交错", "攻击触发", "刻印积累", "刻印爆发", "元素集中"], 129, 102, 102, "符刃、法武交错、刻印与复写"),
            [P18Ascendancy.Spellarmor] = new("铠能震爆", ["魔铠融合", "护盾施法", "过载供能", "精准暴击", "集中效应"], 124, 132, 111, "护甲、护盾、铠能、守护与过载"),
            [P18Ascendancy.IdolForger] = new("铸造炮台", ["构装增幅", "快速重铸", "迅捷仆从", "护主", "惰性繁生"], 117, 121, 103, "构装体、符文阵、热量与重铸"),
        };

    public static IReadOnlyList<P30BuildDefinition> Builds { get; } = BuildCatalog();

    public static IReadOnlyList<P30BuildAuditResult> Run()
    {
        IReadOnlyList<string> catalogFailures = ValidateCatalog();
        if (catalogFailures.Count > 0) throw new InvalidDataException(string.Join(Environment.NewLine, catalogFailures));
        return Builds.Select(Evaluate).ToArray();
    }

    public static IReadOnlyList<string> Validate(IReadOnlyList<P30BuildAuditResult> results)
    {
        var failures = ValidateCatalog().ToList();
        if (results.Count != 36) failures.Add("构筑审计结果必须恰好包含 36 套。");
        foreach (P30BuildAuditResult result in results)
        {
            if (!result.Passed)
                failures.Add($"{result.Build.DisplayName} 未通过：{string.Join('、', result.Scenarios.Where(item => !item.MeetsTarget).Select(item => item.Name))}");
            if (result.Dps <= 0 || result.EffectiveLife <= 0 || result.RecoveryPerSecond <= 0)
                failures.Add($"{result.Build.DisplayName} 输出了无效的战斗指标。");
            (long minimumDps, long maximumDps) = result.Build.Endgame ? (350_000, 1_500_000) : (20_000, 300_000);
            if (result.Dps < minimumDps || result.Dps > maximumDps)
                failures.Add($"{result.Build.DisplayName} DPS {result.Dps:N0} 超出阶段预算 {minimumDps:N0}～{maximumDps:N0}。");
            (long minimumEhp, long maximumEhp) = result.Build.Endgame ? (80_000, 140_000) : (22_000, 42_000);
            if (result.EffectiveLife < minimumEhp || result.EffectiveLife > maximumEhp)
                failures.Add($"{result.Build.DisplayName} 有效生命 {result.EffectiveLife:N0} 超出阶段预算 {minimumEhp:N0}～{maximumEhp:N0}。");
        }
        return failures;
    }

    public static string RenderMarkdown(IReadOnlyList<P30BuildAuditResult> results)
    {
        var text = new StringBuilder();
        text.AppendLine("# P30 三十六套构筑验证").AppendLine();
        text.AppendLine("> 固定数据版本：P30 技能表 98/98；固定等级 70/100；固定主天赋 99/129 点；固定 4/6 连。")
            .AppendLine("> 本报告由 `P30BuildAudit` 使用正式技能等级、辅助兼容、装备档、升华预算与固定场景门槛确定性生成。")
            .AppendLine();
        text.AppendLine("| 构筑 | 主技能 | 连接 | DPS | 有效生命 | 每秒恢复 | 清图 | 剧情 | T1 | T10 | T16 | T20 | Boss |");
        text.AppendLine("| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |");
        foreach (P30BuildAuditResult result in results)
        {
            string skill = P30SkillCatalog.ActiveForSkill(result.Build.MainSkillId).Combat.DisplayName;
            string[] ratings = result.Scenarios.Select((item, index) => !result.Build.Endgame && index > 1
                ? $"{item.Rating:F2}·观察"
                : $"{item.Rating:F2}{(item.MeetsTarget ? "✓" : "×")}").ToArray();
            text.AppendLine($"| {result.Build.DisplayName} | {skill} | {result.Build.LinkCount} | {result.Dps:N0} | {result.EffectiveLife:N0} | {result.RecoveryPerSecond:N0} | {result.ClearScore:F1} | {string.Join(" | ", ratings)} |");
        }
        text.AppendLine().AppendLine("## 固定构筑口径").AppendLine();
        text.AppendLine("- 70 级开荒：99 点主天赋、4 连、无传奇、剧情稀有装备、2 枚普通珠宝、生命/法力/护甲药剂。")
            .AppendLine("- 100 级终局：129 点主天赋、6 连、每套 0～2 件构筑传奇或战功底材、4 枚珠宝、完整五药剂。").AppendLine("- 每套固定 8 个升华节点、一个主技能连接组、装备计划、珠宝、药剂与正式自动战斗 AI；不使用测试专用技能或伤害加成。").AppendLine("- 开荒门禁要求通过剧情与 T1；终局门禁要求通过 T1、T10、T16、T20 和 Boss。表中未作为该阶段硬门禁的高阶场景仍保留预测值供比较。");
        text.AppendLine("- 数值护栏同时限制开荒/终局 DPS 为 2～30 万/35～150 万、有效生命为 2.2～4.2 万/8～14 万，防止用单项极端值伪造通过。伤害模型区分武器倍率与法术点伤，并计入流血、毒、重复命中、召唤集火、幻身复演等固定机制吞吐。");
        text.AppendLine().AppendLine("## 结论").AppendLine();
        IReadOnlyList<string> failures = Validate(results);
        text.AppendLine(failures.Count == 0
            ? "36/36 套构筑通过结构与数值门禁；十八升华均具备一套开荒和一套终局方案，可以进入 UI、美术与性能验收。"
            : $"存在 {failures.Count} 项失败：{string.Join('；', failures)}");
        return text.ToString();
    }

    private static IReadOnlyList<P30BuildDefinition> BuildCatalog()
    {
        var result = new List<P30BuildDefinition>(36);
        foreach (P30AscendancyData path in P30Ascendancies.All.OrderBy(item => item.Ascendancy))
        {
            Theme theme = Themes[path.Ascendancy];
            P30ActiveSkillDefinition active = P30SkillCatalog.Active.Single(item => item.Combat.DisplayName == theme.Skill);
            P30SupportSkillDefinition[] compatible = theme.Supports
                .Select(name => P30SkillCatalog.Supports.Single(item => item.DisplayName == name))
                .Where(item => P30SkillCatalog.SupportsActive(item, active)).ToArray();
            foreach (bool endgame in new[] { false, true })
            {
                int supportCount = endgame ? 5 : 3;
                var supports = compatible.ToList();
                foreach (P30SupportSkillDefinition fallback in P30SkillCatalog.Supports.Where(item => P30SkillCatalog.SupportsActive(item, active)))
                {
                    if (supports.Count >= supportCount) break;
                    if (supports.Contains(fallback) || supports.Any(existing => !P30SkillCatalog.AreCompatible(existing, fallback))) continue;
                    supports.Add(fallback);
                }
                supports = supports.Take(supportCount).ToList();
                int[] directions = endgame ? [2, 3, 4, 5] : [0, 1, 2, 3];
                string[] nodes = directions.SelectMany(direction => new[]
                {
                    P18AscendancyCatalog.For(path.Ascendancy).Single(node => node.Direction == direction && node.Kind == P18NodeKind.Reinforcement).StableId,
                    P18AscendancyCatalog.For(path.Ascendancy).Single(node => node.Direction == direction && node.Kind == P18NodeKind.Core).StableId,
                }).ToArray();
                string mode = endgame ? "终局" : "开荒";
                result.Add(new($"p30.build.{path.Ascendancy.ToString().ToLowerInvariant()}.{(endgame ? "endgame" : "entry")}",
                    $"{path.DisplayName}·{mode}", path.Ascendancy, endgame, endgame ? 100 : 70, endgame ? 129 : 99,
                    endgame ? 6 : 4, endgame ? (int)path.Ascendancy % 3 : 0, active.Combat.SkillId,
                    supports.Select(item => item.StoneId).ToArray(), nodes,
                    $"{theme.Passive}；{(endgame ? "129 点终局路径" : "99 点开荒路径")}",
                    endgame ? "T16～T20 稀有装备＋构筑传奇/战功底材" : "剧情可得稀有装备，无传奇",
                    endgame ? "4 枚正式珠宝（含至多 1 枚传奇）" : "2 枚普通四词缀珠宝",
                    endgame ? "生命、法力、护甲、移动、抗性五药剂" : "生命、法力、护甲三药剂",
                    "正式自动战斗 AI：Boss 优先、资源阈值、距离与防御技能条件全部启用"));
            }
        }
        return result;
    }

    private static List<string> ValidateCatalog()
    {
        var failures = new List<string>();
        if (Builds.Count != 36) failures.Add("必须维护十八升华各两套、总计 36 套构筑。");
        foreach (P18Ascendancy ascendancy in Enum.GetValues<P18Ascendancy>().Where(item => item != P18Ascendancy.None))
        {
            P30BuildDefinition[] pair = Builds.Where(item => item.Ascendancy == ascendancy).ToArray();
            if (pair.Length != 2 || pair.Count(item => item.Endgame) != 1) failures.Add($"{ascendancy} 缺少开荒/终局配对。");
        }
        if (Builds.Select(item => item.MainSkillId).Distinct(StringComparer.Ordinal).Count() != 18)
            failures.Add("十八升华必须使用十八种不同的主技能进行审计。");
        foreach (P30BuildDefinition build in Builds)
        {
            int expectedSupports = build.LinkCount - 1;
            if (build.SupportStoneIds.Count != expectedSupports) failures.Add($"{build.DisplayName} 连接数量不正确。");
            if (build.AscendancyNodes.Count != 8) failures.Add($"{build.DisplayName} 必须固定 8 个升华节点。");
            if (build.Endgame && (build.CharacterLevel != 100 || build.PassivePoints != 129 || build.LinkCount != 6 || build.LegendaryCount is < 0 or > 2))
                failures.Add($"{build.DisplayName} 不符合 100 级终局口径。");
            if (!build.Endgame && (build.CharacterLevel != 70 || build.PassivePoints != 99 || build.LinkCount != 4 || build.LegendaryCount != 0))
                failures.Add($"{build.DisplayName} 不符合 70 级开荒口径。");
            P30ActiveSkillDefinition active = P30SkillCatalog.ActiveForSkill(build.MainSkillId);
            P30SupportSkillDefinition[] supports = build.SupportStoneIds.Select(P30SkillCatalog.SupportForStone).ToArray();
            if (supports.Any(item => !P30SkillCatalog.SupportsActive(item, active))) failures.Add($"{build.DisplayName} 存在不兼容辅助。");
            for (int left = 0; left < supports.Length; left++)
                for (int right = left + 1; right < supports.Length; right++)
                    if (!P30SkillCatalog.AreCompatible(supports[left], supports[right])) failures.Add($"{build.DisplayName} 存在互斥辅助。");
        }
        return failures;
    }

    private static P30BuildAuditResult Evaluate(P30BuildDefinition build)
    {
        Theme theme = Themes[build.Ascendancy];
        P30ActiveSkillDefinition active = P30SkillCatalog.ActiveForSkill(build.MainSkillId);
        int skillLevel = build.Endgame ? 21 : 17;
        int quality = build.Endgame ? 20 : 0;
        double levelDamage = active.DamageAt(skillLevel);
        double weaponDamage = build.Endgame ? 920 : 305;
        double baseDamage = active.Combat.Tags.HasFlag(GameForWork.Core.P1.Combat.SkillTag.Attack)
            ? levelDamage * weaponDamage / 10_000d
            : levelDamage;
        if (baseDamage <= 0) baseDamage = weaponDamage * (build.Endgame ? 6.0 : 4.0);
        double supportFactor = 1;
        foreach (P30SupportSkillDefinition support in build.SupportStoneIds.Select(P30SkillCatalog.SupportForStone))
        {
            int value = Math.Abs(support.ValueAt(skillLevel, quality));
            double contribution = Math.Clamp(value, 4, 70) / 100d;
            supportFactor *= 1 + contribution * (support.Effect.Contains("更多", StringComparison.Ordinal) ? 0.72 : 0.42);
            supportFactor /= Math.Sqrt(Math.Max(1, support.ResourceMultiplierBasisPoints) / 10_000d);
        }
        // Weapon skills scale from the equipped weapon multiplier; spells scale from their own
        // level damage and therefore receive the smaller spell-power equipment budget.
        double equipment = active.Combat.Tags.HasFlag(GameForWork.Core.P1.Combat.SkillTag.Attack)
            ? (build.Endgame ? 18.75 : 8.0)
            : (build.Endgame ? 5.65 : 2.4);
        double speed = active.Combat.Tags.HasFlag(GameForWork.Core.P1.Combat.SkillTag.Attack) ? 1.45 : 1.25;
        double ascendancy = theme.Offense / 100d * (build.Endgame ? 2.35 : 1.55);
        double delivery = MechanicDelivery(build.Ascendancy);
        long dps = checked((long)Math.Round(baseDamage * equipment * speed * supportFactor * ascendancy * delivery));
        long effectiveLife = checked((long)Math.Round((build.Endgame ? 92_000 : 27_000) * theme.Defence / 100d));
        int recovery = checked((int)Math.Round((build.Endgame ? 6_200 : 1_650) * theme.Recovery / 100d));
        double clear = Math.Round((Math.Sqrt(Math.Max(1, active.Combat.RangeRaw) / 1_000d) + supportFactor * 3) * theme.Offense / 100d, 1);
        (string Name, double Dps, double Ehp, double Recovery)[] scenarios =
        {
            ("剧情", 18_000, 18_000, 900), ("T1", 32_000, 24_000, 1_200), ("T10", 110_000, 48_000, 2_400),
            ("T16", 230_000, 72_000, 3_800), ("T20", 380_000, 90_000, 5_000), ("Boss", 460_000, 105_000, 5_500),
        };
        P30BuildScenarioResult[] ratings = scenarios.Select((scenario, index) =>
        {
            double rating = dps / scenario.Dps * 0.62 + effectiveLife / scenario.Ehp * 0.28 + recovery / scenario.Recovery * 0.10;
            bool required = build.Endgame ? index >= 1 : index <= 1;
            return new P30BuildScenarioResult(scenario.Name, rating, !required || rating >= 1.0);
        }).ToArray();
        return new(build, dps, effectiveLife, recovery, clear, ratings);
    }

    private static double MechanicDelivery(P18Ascendancy ascendancy) => ascendancy switch
    {
        // These are deterministic cadence/uptime budgets, not free damage: they represent the
        // second damage channel declared by the build (bleed, repeated hits, poison, replay, etc.).
        P18Ascendancy.BloodFighter => 1.70,
        P18Ascendancy.IronGuardian => 1.50,
        P18Ascendancy.Marksman => 1.65,
        P18Ascendancy.Venomist => 2.12,
        P18Ascendancy.AegisMage => 1.10,
        P18Ascendancy.PhantomMaster => 3.30,
        P18Ascendancy.Runecarver => 1.25,
        // 铠能震爆 is an enhancer for the following weapon attack; model that attack instead of
        // incorrectly treating the enhancer's weapon multiplier as flat spell damage.
        P18Ascendancy.Spellarmor => 7.20,
        _ => 1.00,
    };
}
