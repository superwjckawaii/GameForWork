using System.Security.Cryptography;
using System.Text;
using GameForWork.Core.Offline;
using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P1.Progression;
using GameForWork.Core.P1.World;
using GameForWork.Core.P3;
using GameForWork.Core.P6;

namespace GameForWork.Core.P2;

public enum CampaignNodeKind
{
    NormalCombat,
    StoryEvent,
    EliteCombat,
    ActBoss,
}

public sealed record CampaignNodeDefinition(
    string StableId,
    int Act,
    int IndexInAct,
    string DisplayName,
    CampaignNodeKind Kind,
    long DurationMilliseconds,
    string StoryText);

public static class P2CampaignCatalog
{
    public static readonly string[] ActNames = ["余烬营地", "饥饿边境", "沉没圣城", "无光之路", "门后之物"];

    private static readonly IReadOnlyList<CampaignNodeDefinition> Catalog = Build();

    public static IReadOnlyList<CampaignNodeDefinition> Nodes => Catalog;

    public static CampaignNodeDefinition Get(string stableId) => Catalog.FirstOrDefault(node => node.StableId == stableId)
        ?? throw new KeyNotFoundException($"Unknown campaign node: {stableId}");

    private static IReadOnlyList<CampaignNodeDefinition> Build()
    {
        var result = new List<CampaignNodeDefinition>();
        string[][] names =
        [
            ["熄火的哨站", "灰烬猎场", "失踪的运粮队", "门扉低语", "焦骨执刑者", "余烬守门人"],
            ["饥民之路", "腐坏麦田", "断桥营地", "最后一份口粮", "饥饿塑形者", "谷仓吞噬者"],
            ["淹没前庭", "钟塔水道", "旧教会墓园", "沉钟的忏悔", "盐壳主教", "溺亡圣徒"],
            ["无灯矿径", "扭曲驿站", "裂界隧道", "门扉研究", "虚影追猎者", "无光领路人"],
            ["碎片回廊", "倒悬祭坛", "门后荒原", "断界之夜", "门扉监军", "界外之物"],
        ];
        for (int act = 1; act <= 5; act++)
        {
            for (int index = 1; index <= 6; index++)
            {
                CampaignNodeKind kind = index switch
                {
                    <= 3 => CampaignNodeKind.NormalCombat,
                    4 => CampaignNodeKind.StoryEvent,
                    5 => CampaignNodeKind.EliteCombat,
                    _ => CampaignNodeKind.ActBoss,
                };
                long duration = kind switch
                {
                    CampaignNodeKind.NormalCombat => 120_000,
                    CampaignNodeKind.StoryEvent => 30_000,
                    CampaignNodeKind.EliteCombat => 150_000,
                    CampaignNodeKind.ActBoss => 180_000,
                    _ => 120_000,
                };
                result.Add(new CampaignNodeDefinition(
                    $"core.campaign.act{act}.node{index}",
                    act,
                    index,
                    names[act - 1][index - 1],
                    kind,
                    duration,
                    Story(act, index)));
            }
        }

        return result;
    }

    private static string Story(int act, int index) => (act, index) switch
    {
        (1, 4) => "古代门扉在灰烬下重新发出低鸣，军锋镇开始恢复秩序。",
        (2, 4) => "粮仓的腐败并非天灾；有人把异界碎片埋进了土地。",
        (3, 4) => "旧教会曾用沉钟压制门扉，而钟声如今来自水下。",
        (4, 4) => "门扉研究完成：异界地图开始出现，但远征装置仍不稳定。",
        (5, 4) => "所谓断界之夜不是过去的灾难，而是一场仍在继续的侵入。",
        (_, 6) => $"第 {act} 幕的阻断者已经出现。",
        _ => "主角沿着门扉留下的裂痕继续前进。",
    };
}

public static class P16CampaignLevels
{
    private static readonly int[][] Levels =
    [
        [2, 4, 6, 8, 10, 12],
        [13, 16, 19, 22, 25, 27],
        [28, 31, 34, 37, 40, 43],
        [44, 47, 50, 52, 55, 57],
        [58, 60, 62, 65, 67, 69],
    ];

    public static int MonsterLevel(CampaignNodeDefinition node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (node.Act is < 1 or > 5 || node.IndexInAct is < 1 or > 6)
            throw new ArgumentOutOfRangeException(nameof(node));
        return Levels[node.Act - 1][node.IndexInAct - 1];
    }
}

public sealed record P2CampaignSnapshot(
    int CurrentNodeIndex,
    long CurrentNodeElapsedMilliseconds,
    bool Defeated,
    bool Completed,
    IReadOnlyList<string> CompletedNodeIds,
    IReadOnlyList<string> ClaimedRewardNodeIds,
    IReadOnlyList<string> StoryLog,
    P3SceneTimeline? ActiveTimeline = null);

public sealed record P2CampaignAdvanceResult(
    long EffectiveMilliseconds,
    bool WasClamped,
    int NodesCompleted,
    bool Defeated,
    bool CampaignCompleted,
    string FinalHash);

public sealed class P2CampaignState
{
    private readonly HashSet<string> _completedNodeIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _claimedRewardNodeIds = new(StringComparer.Ordinal);
    private readonly List<string> _storyLog = [];

    public int CurrentNodeIndex { get; private set; }
    public long CurrentNodeElapsedMilliseconds { get; private set; }
    public bool Defeated { get; private set; }
    public bool Completed { get; private set; }
    public P3SceneTimeline? ActiveTimeline { get; private set; }
    public IReadOnlySet<string> CompletedNodeIds => _completedNodeIds;
    public IReadOnlySet<string> ClaimedRewardNodeIds => _claimedRewardNodeIds;
    public IReadOnlyList<string> StoryLog => _storyLog;
    public CampaignNodeDefinition? CurrentNode => Completed ? null : P2CampaignCatalog.Nodes[CurrentNodeIndex];
    public int CurrentAct => Completed ? 5 : CurrentNode?.Act ?? 1;

    public static P2CampaignState CreateNew()
    {
        var state = new P2CampaignState();
        state.AddLog("第一幕 · 余烬营地：主角从熄火的哨站出发。");
        return state;
    }

    public static P2CampaignState CreateLegacyCompleted()
    {
        var state = new P2CampaignState
        {
            CurrentNodeIndex = P2CampaignCatalog.Nodes.Count,
            Completed = true,
        };
        state._completedNodeIds.UnionWith(P2CampaignCatalog.Nodes.Select(node => node.StableId));
        state._claimedRewardNodeIds.UnionWith(state._completedNodeIds);
        state.AddLog("旧存档迁移：五幕主线已标记完成，远征保持开放。");
        return state;
    }

    public static P2CampaignState Restore(P2CampaignSnapshot? snapshot, bool legacyMigration)
    {
        if (snapshot is null)
        {
            return legacyMigration ? CreateLegacyCompleted() : CreateNew();
        }

        CampaignNodeDefinition? currentNode = snapshot.CurrentNodeIndex >= 0 &&
            snapshot.CurrentNodeIndex < P2CampaignCatalog.Nodes.Count
            ? P2CampaignCatalog.Nodes[snapshot.CurrentNodeIndex]
            : null;
        HashSet<string> validIds = P2CampaignCatalog.Nodes.Select(node => node.StableId).ToHashSet(StringComparer.Ordinal);
        if (snapshot.CurrentNodeIndex < 0 || snapshot.CurrentNodeIndex > P2CampaignCatalog.Nodes.Count ||
            snapshot.CurrentNodeElapsedMilliseconds < 0 ||
            currentNode is not null && snapshot.CurrentNodeElapsedMilliseconds >=
                (snapshot.ActiveTimeline?.DurationMilliseconds ?? currentNode.DurationMilliseconds) ||
            snapshot.Completed != (snapshot.CurrentNodeIndex == P2CampaignCatalog.Nodes.Count) ||
            snapshot.Defeated && snapshot.Completed || snapshot.CompletedNodeIds is null ||
            snapshot.ClaimedRewardNodeIds is null || snapshot.StoryLog is null ||
            snapshot.CompletedNodeIds.Any(id => !validIds.Contains(id)) ||
            snapshot.ClaimedRewardNodeIds.Any(id => !validIds.Contains(id)) ||
            snapshot.ClaimedRewardNodeIds.Any(id => !snapshot.CompletedNodeIds.Contains(id, StringComparer.Ordinal)) ||
            snapshot.CompletedNodeIds.Distinct(StringComparer.Ordinal).Count() != snapshot.CurrentNodeIndex ||
            P2CampaignCatalog.Nodes.Take(snapshot.CurrentNodeIndex)
                .Any(node => !snapshot.CompletedNodeIds.Contains(node.StableId, StringComparer.Ordinal)) ||
            snapshot.ActiveTimeline is not null &&
            (currentNode is null || currentNode.Kind == CampaignNodeKind.StoryEvent ||
             snapshot.ActiveTimeline.StableId != $"campaign:{currentNode.StableId}"))
        {
            throw new InvalidDataException("Campaign snapshot is invalid.");
        }

        var state = new P2CampaignState
        {
            CurrentNodeIndex = snapshot.CurrentNodeIndex,
            CurrentNodeElapsedMilliseconds = snapshot.CurrentNodeElapsedMilliseconds,
            Defeated = snapshot.Defeated,
            Completed = snapshot.Completed,
            ActiveTimeline = snapshot.ActiveTimeline,
        };
        state._completedNodeIds.UnionWith(snapshot.CompletedNodeIds);
        state._claimedRewardNodeIds.UnionWith(snapshot.ClaimedRewardNodeIds);
        state._storyLog.AddRange(snapshot.StoryLog.TakeLast(200));
        return state;
    }

    public P2CampaignSnapshot Capture() => new(
        CurrentNodeIndex,
        CurrentNodeElapsedMilliseconds,
        Defeated,
        Completed,
        _completedNodeIds.Order(StringComparer.Ordinal).ToArray(),
        _claimedRewardNodeIds.Order(StringComparer.Ordinal).ToArray(),
        _storyLog.ToArray(),
        ActiveTimeline);

    internal void BeginTimeline(P3SceneTimeline timeline)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        CampaignNodeDefinition? currentNode = CurrentNode;
        if (Completed || currentNode is null || currentNode.Kind == CampaignNodeKind.StoryEvent ||
            timeline.StableId != $"campaign:{currentNode.StableId}")
        {
            throw new InvalidOperationException("The timeline does not match the current campaign node.");
        }

        ActiveTimeline = timeline;
    }

    public void ResumeAfterDefeat()
    {
        if (!Completed)
        {
            Defeated = false;
            CurrentNodeElapsedMilliseconds = 0;
            AddLog("主角调整构筑后重新尝试当前节点。");
        }
    }

    internal void AddElapsed(long milliseconds) =>
        CurrentNodeElapsedMilliseconds = checked(CurrentNodeElapsedMilliseconds + milliseconds);

    internal void CompleteCurrentNode(bool rewardClaimed)
    {
        CampaignNodeDefinition node = CurrentNode ?? throw new InvalidOperationException("Campaign is already complete.");
        _completedNodeIds.Add(node.StableId);
        if (rewardClaimed)
        {
            _claimedRewardNodeIds.Add(node.StableId);
        }

        AddLog($"完成：第 {node.Act} 幕 · {node.DisplayName}。{node.StoryText}");
        CurrentNodeIndex++;
        CurrentNodeElapsedMilliseconds = 0;
        ActiveTimeline = null;
        Defeated = false;
        if (CurrentNodeIndex >= P2CampaignCatalog.Nodes.Count)
        {
            CurrentNodeIndex = P2CampaignCatalog.Nodes.Count;
            Completed = true;
            AddLog("第五幕完成：古代门扉已经稳定，远征功能正式开放。");
        }
        else if (CurrentNode!.IndexInAct == 1)
        {
            AddLog($"第 {CurrentNode.Act} 幕 · {P2CampaignCatalog.ActNames[CurrentNode.Act - 1]} 开始。");
        }
    }

    internal void RecordDefeat(string reason)
    {
        Defeated = true;
        CurrentNodeElapsedMilliseconds = 0;
        ActiveTimeline = null;
        AddLog($"主线推进停止：{reason}。请调整构筑后继续。");
    }

    internal void AddLog(string text)
    {
        _storyLog.Add(text);
        if (_storyLog.Count > 200)
        {
            _storyLog.RemoveRange(0, _storyLog.Count - 200);
        }
    }
}

public sealed class P2CampaignSimulator
{
    private sealed record TimelinePreparation(string NodeId, int BuildHash, Task<P3SceneTimeline> Task);
    private TimelinePreparation? _timelinePreparation;
    private static readonly string[] DropBases =
    [
        "core.base.iron_gauntlets",
        "core.base.march_boots",
        "core.base.chain_belt",
        "core.base.ember_amulet",
        "core.base.spirit_amulet",
        "core.base.shadow_treads",
    ];

    public P2CampaignAdvanceResult Simulate(
        P2CampaignState campaign,
        P1WorldState world,
        P2ManagementState management,
        long elapsedMilliseconds,
        ulong seed,
        bool offline = false,
        bool asyncPreparation = false)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(management);
        long effective = Math.Clamp(elapsedMilliseconds, 0, OfflineTime.MaximumMilliseconds);
        long remaining = effective;
        int nodesCompleted = 0;
        while (remaining > 0 && !campaign.Completed && !campaign.Defeated)
        {
            CampaignNodeDefinition node = campaign.CurrentNode!;
            long duration;
            if (node.Kind == CampaignNodeKind.StoryEvent)
            {
                duration = node.DurationMilliseconds;
            }
            else
            {
                if (campaign.ActiveTimeline is null)
                {
                    P1TeamBuild currentBuild = world.Hero.Build with
                    {
                        Sheet = world.Hero.Build.Sheet with { Level = world.Hero.Progression.Level },
                    };
                    world.Hero.UpdateBuild(currentBuild);
                    ulong nodeSeed = DeriveNodeSeed(seed, node);
                    if (asyncPreparation)
                    {
                        int buildHash = currentBuild.GetHashCode();
                        if (_timelinePreparation?.NodeId != node.StableId || _timelinePreparation.BuildHash != buildHash)
                        {
                            _timelinePreparation = new TimelinePreparation(node.StableId, buildHash,
                                Task.Run(() => P3SceneTimelineBuilder.BuildCampaign(currentBuild, node, nodeSeed)));
                        }
                        if (!_timelinePreparation.Task.IsCompleted)
                        {
                            remaining = 0;
                            break;
                        }
                        Task<P3SceneTimeline> completed = _timelinePreparation.Task;
                        _timelinePreparation = null;
                        campaign.BeginTimeline(completed.GetAwaiter().GetResult());
                    }
                    else
                    {
                        _timelinePreparation = null;
                        campaign.BeginTimeline(P3SceneTimelineBuilder.BuildCampaign(currentBuild, node, nodeSeed));
                    }
                }

                duration = campaign.ActiveTimeline!.DurationMilliseconds;
            }

            long needed = duration - campaign.CurrentNodeElapsedMilliseconds;
            long step = Math.Min(remaining, needed);
            campaign.AddElapsed(step);
            remaining -= step;
            if (campaign.CurrentNodeElapsedMilliseconds < duration)
            {
                break;
            }

            if (campaign.ActiveTimeline is not null &&
                campaign.ActiveTimeline.Outcome != P1BattleOutcome.HeroVictory)
            {
                world.Expedition.AddCombatReport(P6CombatReportBuilder.Build(
                    campaign.ActiveTimeline, $"主线 · {node.DisplayName}", offline));
                campaign.RecordDefeat($"{node.DisplayName} 战斗失败：{campaign.ActiveTimeline.Outcome}");
                break;
            }

            if (campaign.ActiveTimeline is not null)
            {
                world.Expedition.AddCombatReport(P6CombatReportBuilder.Build(
                    campaign.ActiveTimeline, $"主线 · {node.DisplayName}", offline));
            }

            GrantRewards(campaign, world, management, node, seed);
            campaign.CompleteCurrentNode(rewardClaimed: true);
            nodesCompleted++;
        }

        return new P2CampaignAdvanceResult(
            effective,
            elapsedMilliseconds > OfflineTime.MaximumMilliseconds,
            nodesCompleted,
            campaign.Defeated,
            campaign.Completed,
            Hash(campaign, world, effective, seed));
    }

    public bool Replay(
        P2CampaignState campaign,
        P1WorldState world,
        P2ManagementState management,
        string stableId,
        ulong seed)
    {
        CampaignNodeDefinition node = P2CampaignCatalog.Get(stableId);
        if (!campaign.CompletedNodeIds.Contains(stableId) || node.Kind == CampaignNodeKind.StoryEvent)
        {
            return false;
        }

        P3SceneTimeline replay = P3SceneTimelineBuilder.BuildCampaign(world.Hero.Build, node, seed);
        world.Expedition.AddCombatReport(P6CombatReportBuilder.Build(replay, $"主线重放 · {node.DisplayName}"));
        if (replay.Outcome != P1BattleOutcome.HeroVictory)
        {
            return false;
        }

        world.Hero.Progression.AddExperience(Math.Max(10, 20 * node.Act));
        management.AddSkillExperience(10 * node.Act);
        ItemInstance drop = GenerateDrop(node, seed ^ 0x8b8b8b8bUL, "replay");
        if (!world.Storage.TryStore(drop))
        {
            management.AddToRecovery(drop, "重玩节点掉落时仓库已满");
        }

        management.AddHistory($"已重玩 {node.DisplayName}，固定剧情奖励未重复发放。");
        return true;
    }

    private static void GrantRewards(
        P2CampaignState campaign,
        P1WorldState world,
        P2ManagementState management,
        CampaignNodeDefinition node,
        ulong seed)
    {
        int absoluteIndex = (node.Act - 1) * 6 + node.IndexInAct;
        int targetLevel = Math.Min(CharacterProgression.MaximumLevel, 1 + absoluteIndex * 2);
        int targetExperience = CharacterProgression.CumulativeExperienceForLevel(targetLevel);
        world.Hero.Progression.AddExperience(Math.Max(0, targetExperience - world.Hero.Progression.Experience));
        world.Hero.UpdateBuild(world.Hero.Build with
        {
            Sheet = world.Hero.Build.Sheet with { Level = world.Hero.Progression.Level },
        });
        management.AddSkillExperience(60 + node.Act * 10);
        int gold = 4 * node.Act + (node.Kind == CampaignNodeKind.ActBoss ? 12 : 0);
        int scraps = node.Kind is CampaignNodeKind.EliteCombat or CampaignNodeKind.ActBoss ? node.Act : 0;
        world.Economy.AddDispositionProceeds(gold, scraps);
        if (node.Kind == CampaignNodeKind.ActBoss)
        {
            world.Hero.Progression.ClaimFirstBossPassivePoint();
        }

        if (node.Kind != CampaignNodeKind.StoryEvent)
        {
            ItemInstance drop = GenerateDrop(node, seed, "first");
            if (!world.Storage.TryStore(drop))
            {
                management.AddToRecovery(drop, "主线掉落时仓库已满");
            }
        }

        if (node.Act >= 4 && node.Kind != CampaignNodeKind.StoryEvent)
        {
            world.MapInventory.Add(new P1MapItem($"campaign-map-{node.Act}-{node.IndexInAct}", Math.Min(10, node.Act - 3)));
        }

        if (node.Act == 4 && node.IndexInAct == 4)
        {
            campaign.AddLog("地图仓库已开放；远征导航仍会保持隐藏，直到第五幕完成。");
        }
    }

    private static ItemInstance GenerateDrop(CampaignNodeDefinition node, ulong seed, string suffix)
    {
        int absoluteIndex = (node.Act - 1) * 6 + node.IndexInAct;
        string baseId = DropBases[absoluteIndex % DropBases.Length];
        ItemRarity rarity = node.Kind switch
        {
            CampaignNodeKind.ActBoss => ItemRarity.Rare,
            CampaignNodeKind.EliteCombat => ItemRarity.Magic,
            _ => ItemRarity.Basic,
        };
        return ItemGenerator.Generate(
            baseId,
            P16CampaignLevels.MonsterLevel(node) + (node.Kind == CampaignNodeKind.ActBoss ? 2 : node.Kind == CampaignNodeKind.EliteCombat ? 1 : 0),
            rarity,
            seed ^ (ulong)absoluteIndex * 0x9e3779b97f4a7c15UL,
            $"campaign-{node.Act}-{node.IndexInAct}-{suffix}");
    }

    private static string Hash(P2CampaignState campaign, P1WorldState world, long elapsed, ulong seed)
    {
        string source = $"{seed}|{elapsed}|{campaign.CurrentNodeIndex}|{campaign.CurrentNodeElapsedMilliseconds}|" +
            $"{campaign.Defeated}|{campaign.Completed}|{world.Hero.Progression.Level}|{world.Hero.Progression.Experience}|" +
            $"{world.Storage.Count}|{world.MapInventory.Count}|{world.Economy.Gold}|{world.Economy.IronScraps}|" +
            campaign.ActiveTimeline?.FinalHash;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
    }

    private static ulong DeriveNodeSeed(ulong seed, CampaignNodeDefinition node)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{seed}|{node.StableId}"));
        return BitConverter.ToUInt64(hash, 0);
    }
}
