using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Spatial;
using GameForWork.Core.Expeditions;
using GameForWork.Core.Skills;
using GameForWork.Core.Town;
using GameForWork.Core.Endgame;

namespace GameForWork.Core.Campaign;

public enum JourneyStep
{
    ObserveBattle,
    EquipItem,
    CompleteActOne,
    AllocatePassive,
    InspectSkills,
    ConfigureSkillTarget,
    CraftItem,
    CompleteCampaign,
    ViewCombatReport,
    DispatchHero,
    DispatchMercenaries,
    CompleteSafeMap,
    CompleteAbyssMap,
    EarnBossTicket,
    DefeatAbyssWarden,
    CompleteGardenMap,
    ChooseAltar,
    CompleteTier16,
    ReachLevel100,
    CompleteBreakthrough,
    CompleteTier20,
    EnterCitadel,
    DefeatCitadel,
}

public enum JourneyEvent
{
    EquippedItem,
    InspectedSkills,
    ConfiguredSkillTarget,
    CraftedItem,
    ViewedCombatReport,
}

public enum JourneyDestination
{
    Overview,
    Story,
    Equipment,
    Skills,
    Passives,
    Town,
    Expedition,
}

public sealed record JourneyStepDefinition(
    JourneyStep Step,
    string Title,
    string Instruction,
    JourneyDestination Destination,
    string HelpText);

public sealed record DemoJourneySnapshot(
    bool TutorialEnabled,
    IReadOnlyList<JourneyEvent> Events,
    IReadOnlyList<JourneyStep> PresentedSteps,
    bool CompletionShown,
    long RealPlayMilliseconds,
    long OfflineMilliseconds,
    IReadOnlyList<JourneyStep>? RewardedSteps = null,
    int TutorialReplayCount = 0);

public sealed record DemoSummary(
    long RealPlayMilliseconds,
    long OfflineMilliseconds,
    int ActsCompleted,
    int MapsCompleted,
    int MapsFailed,
    int BossAttempts,
    int Level,
    string MainSkill,
    int MainSkillLinks,
    int HighestDamage,
    int EquipmentScore,
    int LegendaryItems,
    string SaveHash,
    int HighestMapTier = 0,
    int MechanicEncounters = 0,
    int CitadelVictories = 0,
    int MythicItems = 0,
    int TownLevelTotal = 0);

public sealed class DemoJourney
{
    private static readonly JourneyStepDefinition[] Definitions =
    [
        new(JourneyStep.ObserveBattle, "观察一场战斗", "在总览中观察主角完成至少一个战斗节点。", JourneyDestination.Overview,
            "战斗以每秒 20 个模拟刻结算，画面以更高帧率插值显示。移动速度、范围和连锁都会影响真实清图过程。"),
        new(JourneyStep.EquipItem, "完成第一次换装", "前往角色与物品，将一件掉落装备装到主角身上。", JourneyDestination.Equipment,
            "双击可以快速换装，拖曳可以精确选择装备槽，右键可出售、锁定或标记制作底材。"),
        new(JourneyStep.CompleteActOne, "完成第一幕", "继续自动推进主线，击败第一幕 Boss。", JourneyDestination.Story,
            "主线不会因为教学停下；战败后可以调整构筑并在主线页继续。"),
        new(JourneyStep.AllocatePassive, "分配一个天赋点", "在天赋页选择可达节点并确认分配。", JourneyDestination.Passives,
            "规划路径不会消耗点数；真正分配后才会改变角色构筑。"),
        new(JourneyStep.InspectSkills, "查看技能孔组", "打开技能页，检查装备提供的连接孔与技能石。", JourneyDestination.Skills,
            "一个孔组最多有一个主动技能；辅助技能必须与主动技能标签兼容。"),
        new(JourneyStep.ConfigureSkillTarget, "配置攻击目标", "为一个攻击技能选择仅 Boss、精英与 Boss或所有敌人。", JourneyDestination.Skills,
            "目标限制只约束对应技能，其他技能仍可处理普通敌人。"),
        new(JourneyStep.CraftItem, "完成一次金属制作", "选中背包、仓库或已装备物品，消耗对应金属加工一次。", JourneyDestination.Equipment,
            "制作前会显示材料、适用性和结果；锁定物品不能被制作。"),
        new(JourneyStep.CompleteCampaign, "完成五幕主线", "继续强化构筑并击败第五幕 Boss，稳定古代门扉。", JourneyDestination.Story,
            "第五幕完成前远征入口保持隐藏，离线时间同样会推进主线。"),
        new(JourneyStep.ViewCombatReport, "查看战斗报告", "在远征页展开最近战斗报告，检查输出、承伤和技能表现。", JourneyDestination.Expedition,
            "报告使用权威战斗时间线生成，可帮助判断伤害、资源或生存问题。"),
        new(JourneyStep.DispatchHero, "派遣主角", "为主角选择一个地图目标并开始派遣。", JourneyDestination.Expedition,
            "安全探索更稳定，裂渊追猎风险更高但奖励更好。"),
        new(JourneyStep.DispatchMercenaries, "派遣佣兵队", "为佣兵队选择目标并开始第二支派遣。", JourneyDestination.Expedition,
            "佣兵技能和 AI 自主成长，玩家通过装备和远征目标影响队伍。"),
        new(JourneyStep.CompleteSafeMap, "完成安全地图", "让任意队伍成功完成一张安全探索地图。", JourneyDestination.Expedition,
            "地图耗尽或仓库条件不足时，队伍会停止并给出明确原因。"),
        new(JourneyStep.CompleteAbyssMap, "完成裂渊地图", "让任意队伍成功完成一张裂渊追猎地图。", JourneyDestination.Expedition,
            "裂渊敌人更强，并推进深渊监守者碎片进度。"),
        new(JourneyStep.EarnBossTicket, "获得监守者门票", "完成地图 Boss，集齐四枚碎片并自动合成门票。", JourneyDestination.Expedition,
            "每完成三张成功地图获得一枚碎片，四枚碎片自动合成一张门票。"),
        new(JourneyStep.DefeatAbyssWarden, "击败深渊监守者", "消耗门票派遣主角挑战深渊监守者，完成 Demo。", JourneyDestination.Expedition,
            "Boss 练习不消耗门票，但练习胜利不会产生正式奖励。"),
        new(JourneyStep.CompleteGardenMap, "收割命能花园", "完成一张命能花园地图并收割三块苗圃。", JourneyDestination.Expedition,
            "每块苗圃选择敌人与收益；命能可进行保前缀、保后缀和定向加工。"),
        new(JourneyStep.ChooseAltar, "承担一次祭坛代价", "在地图中选择赤誓或苍誓祭坛的风险收益。", JourneyDestination.Expedition,
            "同一张地图只出现一个祭坛阵营，选择效果持续到本图结束。"),
        new(JourneyStep.CompleteTier16, "完成 T16 地图", "使用加工后的构筑击败一名 T16 地图 Boss。", JourneyDestination.Expedition,
            "T16 是常规异界终点。完成后继续派遣 T16 地图，把主角练到 100 级；此时升华 4/4 是正常进度。"),
        new(JourneyStep.ReachLevel100, "达到 100 级", "继续地图远征，让主角达到首次等级上限。", JourneyDestination.Overview,
            "建议重复最高阶地图获取经验。达到 100 级后，前往‘远征 → 异界与突破’挑战免费的门扉突破试炼。"),
        new(JourneyStep.CompleteBreakthrough, "完成门扉突破", "击败门扉化身，开放等级 101～120 与 T17～T20。", JourneyDestination.Expedition,
            "在‘远征 → 异界与突破’点击门扉突破试炼。失败只损失本次挑战进度；胜利获得 2 个升华点，使上限从 4 提高到 6。"),
        new(JourneyStep.CompleteTier20, "完成 T20 地图", "适应终局回响规则并完成一张 T20 地图。", JourneyDestination.Expedition,
            "T17～T20 各有独立终局规则，需要完整攻防构筑。"),
        new(JourneyStep.EnterCitadel, "取得天垒门票", "完成 T11+ 地图，使用 8 枚碎片合成灰烬天垒门票。", JourneyDestination.Expedition,
            "正式模式消耗门票；练习模式免费但没有奖励。"),
        new(JourneyStep.DefeatCitadel, "击败灰烬天垒", "连续突破城墙、双卫和核心，完成 Demo 主旅程。", JourneyDestination.Expedition,
            "三阶段资源连续保留。首次胜利奖励5个异界点和2个升华点；当前版本神话装备由天垒Boss概率掉落。"),
    ];

    private readonly HashSet<JourneyEvent> _events = [];
    private readonly HashSet<JourneyStep> _presentedSteps = [];
    private readonly HashSet<JourneyStep> _rewardedSteps = [];

    private DemoJourney(bool tutorialEnabled)
    {
        TutorialEnabled = tutorialEnabled;
        if (!tutorialEnabled) CompleteManualTrainingEvents();
    }

    public bool TutorialEnabled { get; }
    public bool CompletionShown { get; private set; }
    public long RealPlayMilliseconds { get; private set; }
    public long OfflineMilliseconds { get; private set; }
    public int CurrentStepIndex { get; private set; }
    public bool DemoCompleted { get; private set; }
    public int TutorialReplayCount { get; private set; }
    public JourneyStepDefinition? CurrentStep => CurrentStepIndex < Definitions.Length ? Definitions[CurrentStepIndex] : null;
    public IReadOnlyList<JourneyStepDefinition> AllSteps => Definitions;

    public bool TutorialAllowsPage(JourneyStep gate, bool requireGateCompletion = false)
    {
        if (!TutorialEnabled) return true;
        int gateIndex = Array.FindIndex(Definitions, definition => definition.Step == gate);
        if (gateIndex < 0) throw new ArgumentOutOfRangeException(nameof(gate));
        return requireGateCompletion ? CurrentStepIndex > gateIndex : CurrentStepIndex >= gateIndex;
    }

    public static DemoJourney CreateNew(bool tutorialEnabled) => new(tutorialEnabled);

    public static DemoJourney Restore(DemoJourneySnapshot? snapshot, bool legacy)
    {
        if (snapshot is null)
        {
            var migrated = new DemoJourney(tutorialEnabled: false);
            migrated.CompleteManualTrainingEvents();
            migrated._rewardedSteps.UnionWith(Enum.GetValues<JourneyStep>());
            return migrated;
        }
        if (snapshot.RealPlayMilliseconds < 0 || snapshot.OfflineMilliseconds < 0 || snapshot.Events is null ||
            snapshot.PresentedSteps is null || snapshot.Events.Any(item => !Enum.IsDefined(item)) ||
            snapshot.PresentedSteps.Any(item => !Enum.IsDefined(item)) ||
            (snapshot.RewardedSteps?.Any(item => !Enum.IsDefined(item)) ?? false))
            throw new InvalidDataException("Journey journey snapshot is invalid.");
        var result = new DemoJourney(snapshot.TutorialEnabled)
        {
            CompletionShown = snapshot.CompletionShown,
            RealPlayMilliseconds = snapshot.RealPlayMilliseconds,
            OfflineMilliseconds = snapshot.OfflineMilliseconds,
            TutorialReplayCount = Math.Max(0, snapshot.TutorialReplayCount),
        };
        result._events.UnionWith(snapshot.Events);
        result._presentedSteps.UnionWith(snapshot.PresentedSteps);
        result._rewardedSteps.UnionWith(snapshot.RewardedSteps ?? []);
        if (legacy) result.CompleteManualTrainingEvents();
        return result;
    }

    public DemoJourneySnapshot Capture() => new(
        TutorialEnabled,
        _events.Order().ToArray(),
        _presentedSteps.Order().ToArray(),
        CompletionShown,
        RealPlayMilliseconds,
        OfflineMilliseconds,
        _rewardedSteps.Order().ToArray(), TutorialReplayCount);

    public void ReplayTutorial()
    {
        TutorialReplayCount++;
        _presentedSteps.Clear();
    }

    public void AddElapsed(long milliseconds, bool offline)
    {
        if (milliseconds <= 0) return;
        if (offline) OfflineMilliseconds = checked(OfflineMilliseconds + milliseconds);
        else RealPlayMilliseconds = checked(RealPlayMilliseconds + milliseconds);
    }

    public void Record(JourneyEvent journeyEvent)
    {
        if (!Enum.IsDefined(journeyEvent)) throw new ArgumentOutOfRangeException(nameof(journeyEvent));
        _events.Add(journeyEvent);
    }

    public void Synchronize(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        while (CurrentStepIndex < Definitions.Length && IsSatisfied(Definitions[CurrentStepIndex].Step, session))
        {
            GrantReward(Definitions[CurrentStepIndex].Step, session);
            CurrentStepIndex++;
        }
        DemoCompleted = session.Endgame.CitadelDefeated;
    }

    public bool TryPresentCurrentStep()
    {
        JourneyStepDefinition? current = CurrentStep;
        return TutorialEnabled && current is not null && _presentedSteps.Add(current.Step);
    }

    public bool TryMarkCompletionShown()
    {
        if (!DemoCompleted || CompletionShown) return false;
        CompletionShown = true;
        return true;
    }

    public DemoSummary BuildSummary(GameSession session, string saveHash)
    {
        BuildSummary build = session.GetBuildSummary();
        IReadOnlyList<ItemInstance> equipment = session.HeroEquipment.Items.Values.ToArray();
        int score = equipment.Sum(item => item.ItemLevel * 10 + (int)item.Rarity * 25 + item.Affixes.Count * 5 + item.LinkedSocketCount * 4);
        int legendary = equipment.Count(item => item.Rarity == ItemRarity.Legendary) +
                        session.World.Storage.Items.Count(item => item.Rarity == ItemRarity.Legendary);
        int highestDamage = session.World.Expedition.Reports.SelectMany(report => report.Skills)
            .Select(skill => skill.Damage).DefaultIfEmpty().Max();
        IReadOnlySet<string> mythicIds = Content.UniqueItems.All.Where(item => item.Mythic)
            .Select(item => item.StableId).ToHashSet(StringComparer.Ordinal);
        return new DemoSummary(
            RealPlayMilliseconds, OfflineMilliseconds, CompletedActs(session),
            session.World.Teams.Sum(team => team.MapsCompleted), session.World.Teams.Sum(team => team.MapsFailed),
            session.World.Expedition.Reports.Count(report => report.Context.Contains("深渊监守者", StringComparison.Ordinal)),
            session.World.Hero.Progression.Level,
            build.MainSkill, build.MainSkillLinks, highestDamage, score, legendary, saveHash,
            session.Endgame.CompletedTiers.DefaultIfEmpty().Max(),
            session.Endgame.MechanicEncounters.Values.Sum(), session.Endgame.CitadelVictories,
            equipment.Concat(session.World.Storage.Items).Count(item =>
                item.LegendaryRule is not null && mythicIds.Contains(item.LegendaryRule.StableId)),
            Enum.GetValues<BuildingKind>().Sum(session.Town.Level));
    }

    private bool IsSatisfied(JourneyStep step, GameSession session) => step switch
    {
        JourneyStep.ObserveBattle => session.Campaign.CompletedNodeIds.Count > 0 || session.Campaign.ActiveTimeline is not null,
        JourneyStep.EquipItem => _events.Contains(JourneyEvent.EquippedItem),
        JourneyStep.CompleteActOne => session.Campaign.CompletedNodeIds.Count >= 6,
        JourneyStep.AllocatePassive => session.Passives.Allocated.Count > 0,
        JourneyStep.InspectSkills => _events.Contains(JourneyEvent.InspectedSkills),
        JourneyStep.ConfigureSkillTarget => _events.Contains(JourneyEvent.ConfiguredSkillTarget),
        JourneyStep.CraftItem => _events.Contains(JourneyEvent.CraftedItem),
        JourneyStep.CompleteCampaign => session.Campaign.Completed,
        JourneyStep.ViewCombatReport => _events.Contains(JourneyEvent.ViewedCombatReport),
        JourneyStep.DispatchHero => session.World.Expedition.Get(ExpeditionTeamKind.Hero) is not null,
        JourneyStep.DispatchMercenaries => session.World.Expedition.Get(ExpeditionTeamKind.Mercenaries) is not null,
        JourneyStep.CompleteSafeMap => session.World.Expedition.Reports.Any(report =>
            report.Outcome == Combat.BattleOutcome.HeroVictory && report.Context.Contains(" Safe", StringComparison.Ordinal)),
        JourneyStep.CompleteAbyssMap => session.World.Expedition.Reports.Any(report =>
            report.Outcome == Combat.BattleOutcome.HeroVictory && report.Context.Contains(" Abyss", StringComparison.Ordinal) &&
            !report.Context.Contains("深渊监守者", StringComparison.Ordinal)),
        JourneyStep.EarnBossTicket => session.World.Expedition.AbyssWardenTickets > 0 || session.World.Expedition.BossSequence > 0,
        JourneyStep.DefeatAbyssWarden => HasAbyssVictory(session),
        JourneyStep.CompleteGardenMap => session.Endgame.MechanicEncounters.GetValueOrDefault(MapMechanic.LifeGarden) > 0,
        JourneyStep.ChooseAltar => session.Endgame.MechanicEncounters.GetValueOrDefault(MapMechanic.RedAltar) > 0 ||
                                     session.Endgame.MechanicEncounters.GetValueOrDefault(MapMechanic.BlueAltar) > 0,
        JourneyStep.CompleteTier16 => session.Endgame.CompletedTiers.Contains(16),
        JourneyStep.ReachLevel100 => session.World.Hero.Progression.Level >= 100,
        JourneyStep.CompleteBreakthrough => session.Endgame.FinalBreakthroughCompleted,
        JourneyStep.CompleteTier20 => session.Endgame.CompletedTiers.Contains(20),
        JourneyStep.EnterCitadel => session.Endgame.CitadelTickets > 0 || session.Endgame.CitadelVictories > 0,
        JourneyStep.DefeatCitadel => session.Endgame.CitadelDefeated,
        _ => false,
    };

    private static bool HasAbyssVictory(GameSession session) => session.World.Expedition.Reports.Any(report =>
        report.Outcome == Combat.BattleOutcome.HeroVictory && report.Context.Contains("深渊监守者", StringComparison.Ordinal));

    private static int CompletedActs(GameSession session) => Math.Clamp(session.Campaign.CompletedNodeIds.Count / 6, 0, 5);

    private void GrantReward(JourneyStep step, GameSession session)
    {
        if (!_rewardedSteps.Add(step)) return;
        int gold = step switch
        {
            JourneyStep.CompleteActOne => 10,
            JourneyStep.CompleteCampaign => 30,
            JourneyStep.CompleteSafeMap or JourneyStep.CompleteAbyssMap => 10,
            JourneyStep.EarnBossTicket => 20,
            JourneyStep.DefeatAbyssWarden => 50,
            JourneyStep.CompleteGardenMap or JourneyStep.ChooseAltar => 30,
            JourneyStep.CompleteTier16 or JourneyStep.CompleteBreakthrough => 80,
            JourneyStep.CompleteTier20 or JourneyStep.EnterCitadel => 120,
            JourneyStep.DefeatCitadel => 300,
            _ => 5,
        };
        session.World.Economy.AddDispositionProceeds(gold, 0);
        if (step == JourneyStep.EquipItem) session.World.Economy.AddMetal(MetalCurrencyKind.TemperingIron, 1);
        if (step == JourneyStep.InspectSkills) session.World.Economy.AddMetal(MetalCurrencyKind.ChainSteel, 1);
        if (step == JourneyStep.ConfigureSkillTarget) session.World.Economy.AddMetal(MetalCurrencyKind.VitalSilver, 1);
        JourneyStepDefinition definition = Definitions.Single(item => item.Step == step);
        session.Management.AddHistory($"旅程目标完成：{definition.Title}；自动领取 {gold} 金币。");
    }

    private void CompleteManualTrainingEvents()
    {
        _events.UnionWith(Enum.GetValues<JourneyEvent>());
    }
}
