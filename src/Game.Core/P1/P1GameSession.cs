using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P1.Progression;
using GameForWork.Core.P1.World;
using GameForWork.Core.P2;
using GameForWork.Core.P4;
using GameForWork.Core.P5;
using GameForWork.Core.P6;
using GameForWork.Core.P9;
using GameForWork.Core.P10;
using GameForWork.Core.P12;
using GameForWork.Core.P14;
using GameForWork.Core.P17;
using GameForWork.Core.P18;

namespace GameForWork.Core.P1;

public enum CharacterGender
{
    Woman,
    Man,
    Androgynous,
}

public enum CharacterSkinTone
{
    Pale,
    Fair,
    Umber,
    Deep,
}

public enum CharacterHairStyle
{
    Cropped,
    Long,
    Braided,
    Shaved,
}

public enum P1Ascendancy
{
    IronOath,
    Linebreaker,
}

public sealed record PlayerIdentity(
    string Name,
    CharacterGender Gender,
    CharacterSkinTone SkinTone,
    CharacterHairStyle HairStyle,
    P1Ascendancy Ascendancy)
{
    public PlayerIdentity Validate()
    {
        if (string.IsNullOrWhiteSpace(Name) || Name.Trim().Length is < 2 or > 16)
        {
            throw new ArgumentException("Character name must contain 2 through 16 characters.", nameof(Name));
        }

        return this with { Name = Name.Trim() };
    }
}

public sealed record EquippedItemSnapshot(EquipmentSlot Slot, ItemInstance Item);

public enum AiRuleMatchMode
{
    All,
    Any,
}

public sealed record HeroAiConfiguration(
    string Preset,
    bool UseWarCry,
    int LifeFlaskThresholdBasisPoints,
    AiRuleMatchMode MatchMode = AiRuleMatchMode.All,
    int MinimumEnemyCount = 1,
    string EnemyRarity = "任意",
    int MaximumEnemyDistance = 8,
    bool BossPriority = false,
    int DangerThreshold = 50)
{
    public static HeroAiConfiguration Balanced => new("均衡", true, 5_000);

    public HeroAiConfiguration Validate()
    {
        if (string.IsNullOrWhiteSpace(Preset) || LifeFlaskThresholdBasisPoints is < 1_000 or > 9_000 ||
            MinimumEnemyCount is < 1 or > 20 || MaximumEnemyDistance is < 1 or > 30 ||
            DangerThreshold is < 0 or > 100 || string.IsNullOrWhiteSpace(EnemyRarity))
        {
            throw new ArgumentOutOfRangeException(nameof(LifeFlaskThresholdBasisPoints));
        }

        return this;
    }
}

public sealed record P1GameSessionSnapshot(
    int FormatVersion,
    PlayerIdentity Player,
    string MercenaryName,
    P1WorldSnapshot World,
    IReadOnlyList<EquippedItemSnapshot> HeroEquipment,
    IReadOnlyList<string> AllocatedPassives,
    int MemoryAshes,
    SkillSupport HeavyStrikeSupports,
    HeroAiConfiguration? HeroAi,
    bool DebugTwentyTimes,
    ulong Seed,
    int SimulationSequence,
    P2ManagementSnapshot? Management = null,
    IReadOnlyList<EquippedItemSnapshot>? MercenaryEquipment = null,
    P2CampaignSnapshot? Campaign = null,
    P8DemoJourneySnapshot? Journey = null,
    P9TownSnapshot? Town = null,
    P10EndgameSnapshot? Endgame = null,
    IReadOnlyDictionary<string, int>? MasterySelections = null,
    IReadOnlyDictionary<string, PassiveJewelKind>? SocketedJewels = null);

public sealed class P1GameSession
{
    public const int CurrentFormatVersion = 18;
    private readonly P1WorldSimulator _simulator = new(new P1MapAttemptResolver());
    private readonly P2CampaignSimulator _campaignSimulator = new();
    private AssembledCharacterBuild _heroBuild;

    private P1GameSession(
        PlayerIdentity player,
        string mercenaryName,
        P1WorldState world,
        EquipmentLoadout heroEquipment,
        EquipmentLoadout mercenaryEquipment,
        PassiveTreeAllocation passives,
        SkillSupport heavyStrikeSupports,
        HeroAiConfiguration heroAi,
        P2ManagementState management,
        P2CampaignState campaign,
        P8DemoJourney journey,
        P9TownState town,
        P10EndgameState endgame,
        ulong seed,
        int simulationSequence,
        bool debugTwentyTimes)
    {
        Player = player.Validate();
        MercenaryName = mercenaryName;
        World = world;
        HeroEquipment = heroEquipment;
        MercenaryEquipment = mercenaryEquipment;
        Passives = passives;
        HeavyStrikeSupports = heavyStrikeSupports;
        HeroAi = heroAi.Validate();
        Management = management;
        Campaign = campaign;
        Journey = journey;
        Town = town;
        Endgame = endgame;
        Seed = seed;
        SimulationSequence = simulationSequence;
        DebugTwentyTimes = debugTwentyTimes;
        Management.NormalizeSkillChains(P5SkillChainRules.Build(HeroEquipment));
        HeavyStrikeSupports = SupportsFor("core.skill_stone.heavy_strike");
        SynchronizeCampaignAscendancyPoints();
        _heroBuild = AssembleHero();
        RefreshHeroTeamBuild();
        RefreshMercenaryPartyBuild();
    }

    public PlayerIdentity Player { get; }
    public string MercenaryName { get; }
    public P1WorldState World { get; }
    public EquipmentLoadout HeroEquipment { get; }
    public EquipmentLoadout MercenaryEquipment { get; }
    public PassiveTreeAllocation Passives { get; private set; }
    public SkillSupport HeavyStrikeSupports { get; private set; }
    public HeroAiConfiguration HeroAi { get; private set; }
    public P2ManagementState Management { get; }
    public P2CampaignState Campaign { get; }
    public P8DemoJourney Journey { get; }
    public P9TownState Town { get; }
    public P10EndgameState Endgame { get; }
    public int UnlockedFlaskSlots => Math.Clamp(2 + Town.Level(P9BuildingKind.Teleporter),
        P14Flasks.InitialSlots, P14Flasks.MaximumSlots);
    public bool IsExpeditionUnlocked => Campaign.Completed;
    public bool DebugTwentyTimes { get; set; }
    public ulong Seed { get; }
    public int SimulationSequence { get; private set; }
    public AssembledCharacterBuild HeroBuild => _heroBuild;
    public int SimulationSpeed => DebugTwentyTimes ? 20 : 1;

    public static P1GameSession CreateNew(PlayerIdentity player, ulong seed, bool tutorialEnabled = true)
    {
        var equipment = new EquipmentLoadout();
        EquipStarter(equipment, EquipmentSlot.MainHand, "core.base.rusted_greatsword", seed + 1);
        EquipStarter(equipment, EquipmentSlot.Chest, "core.base.crude_chainmail", seed + 2);
        EquipStarter(equipment, EquipmentSlot.Helmet, "core.base.iron_helmet", seed + 3);
        EquipStarter(equipment, EquipmentSlot.Gloves, "core.base.iron_gauntlets", seed + 6);
        EquipStarter(equipment, EquipmentSlot.RingLeft, "core.base.life_ring", seed + 4);
        EquipStarter(equipment, EquipmentSlot.Flask1, "core.base.life_flask", seed + 5);
        EquipStarter(equipment, EquipmentSlot.Flask2, "core.base.mana_flask", seed + 7);
        var passives = new PassiveTreeAllocation();
        AssembledCharacterBuild build = CharacterBuildAssembler.Assemble(
            1,
            CharacterAttributes.IronOathStarting,
            equipment,
            passives,
            new SkillConfiguration(P1SkillIds.HeavyStrike, SkillSupport.Bleed));
        P1MercenaryProfile mercenary = P1MercenaryFactory.GenerateCantor(seed ^ 0xa5a5a5a5UL);
        var economy = new TownEconomyState(
            memoryAshes: 0,
            metalCurrencies: Enum.GetValues<MetalCurrencyKind>().ToDictionary(
                kind => kind,
                kind => kind is MetalCurrencyKind.TemperingIron or MetalCurrencyKind.WardSteel or MetalCurrencyKind.VitalSilver ? 3 : 0));
        P9TownState town = P9TownState.CreateNew(seed ^ 0x7039746f776eUL, mercenary.Equipment);
        var world = new P1WorldState(
            ToTeamBuild(build, SkillSupport.Bleed, HeroAiConfiguration.Balanced),
            town.BuildMercenaryParty(1),
            economy);
        return new P1GameSession(
            player,
            mercenary.Name,
            world,
            equipment,
            mercenary.Equipment,
            passives,
            SkillSupport.Bleed,
            HeroAiConfiguration.Balanced,
            P2ManagementState.CreateNew(),
            P2CampaignState.CreateNew(),
            P8DemoJourney.CreateNew(tutorialEnabled),
            town,
            new P10EndgameState(),
            seed,
            simulationSequence: 0,
            debugTwentyTimes: false);
    }

    public static P1GameSession Restore(P1GameSessionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.FormatVersion != CurrentFormatVersion || snapshot.SimulationSequence < 0)
        {
            throw new InvalidDataException(
                $"P1 session snapshot version {snapshot.FormatVersion} is unsupported; expected {CurrentFormatVersion}.");
        }

        EquipmentLoadout equipment = EquipmentLoadout.Restore(
            snapshot.HeroEquipment.Select(entry =>
                new KeyValuePair<EquipmentSlot, ItemInstance>(entry.Slot, entry.Item)));
        P1MercenaryProfile restoredMercenary = P1MercenaryFactory.GenerateCantor(snapshot.Seed ^ 0xa5a5a5a5UL);
        EquipmentLoadout mercenaryEquipment = snapshot.MercenaryEquipment is null
            ? restoredMercenary.Equipment
            : EquipmentLoadout.Restore(snapshot.MercenaryEquipment.Select(entry =>
                new KeyValuePair<EquipmentSlot, ItemInstance>(entry.Slot, entry.Item)));
        PassiveTreeAllocation passives = PassiveTreeAllocation.Restore(snapshot.AllocatedPassives, snapshot.MemoryAshes,
            snapshot.MasterySelections, snapshot.SocketedJewels);
        P1WorldState world = P1WorldSnapshots.Restore(snapshot.World);
        P9TownState town = P9TownState.Restore(snapshot.Town, snapshot.Seed ^ 0x7039746f776eUL, mercenaryEquipment);
        if (town.Roster.Count > 0) mercenaryEquipment = town.Roster[0].Equipment;
        P2ManagementState management = P2ManagementState.Restore(snapshot.Management, legacyMigration: false);
        var session = new P1GameSession(
            snapshot.Player,
            snapshot.MercenaryName,
            world,
            equipment,
            mercenaryEquipment,
            passives,
            snapshot.HeavyStrikeSupports,
            snapshot.HeroAi ?? HeroAiConfiguration.Balanced,
            management,
            P2CampaignState.Restore(snapshot.Campaign, legacyMigration: false),
            P8DemoJourney.Restore(snapshot.Journey, legacy: false),
            town,
            P10EndgameState.Restore(snapshot.Endgame),
            snapshot.Seed,
            snapshot.SimulationSequence,
            snapshot.DebugTwentyTimes);
        session.ApplyTownBuildingEffects();
        if (session.Endgame.FinalBreakthroughCompleted)
        {
            session.World.UnlockFinalMapTiers();
            session.World.Hero.Progression.UnlockFinalBreakthrough();
        }
        session.Journey.Synchronize(session);
        return session;
    }

    public P1GameSessionSnapshot Capture() => new(
        CurrentFormatVersion,
        Player,
        MercenaryName,
        P1WorldSnapshots.Capture(World),
        HeroEquipment.Items.Select(pair => new EquippedItemSnapshot(pair.Key, pair.Value)).ToArray(),
        P1PassiveTree.Nodes
            .Where(node => Passives.Allocated.Contains(node.StableId))
            .Select(node => node.StableId)
            .ToArray(),
        Passives.MemoryAshes,
        HeavyStrikeSupports,
        HeroAi,
        DebugTwentyTimes,
        Seed,
        SimulationSequence,
        Management.Capture(),
        MercenaryEquipment.Items.Select(pair => new EquippedItemSnapshot(pair.Key, pair.Value)).ToArray(),
        Campaign.Capture(),
        Journey.Capture(),
        Town.Capture(),
        Endgame.Capture(),
        new Dictionary<string, int>(Passives.MasterySelections),
        new Dictionary<string, PassiveJewelKind>(Passives.SocketedJewels));

    public P1OfflineResult Advance(long realElapsedMilliseconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(realElapsedMilliseconds);
        long maximum = GameForWork.Core.Offline.OfflineTime.MaximumMilliseconds;
        long simulated = realElapsedMilliseconds > maximum / SimulationSpeed
            ? maximum
            : realElapsedMilliseconds * SimulationSpeed;
        Journey.AddElapsed(realElapsedMilliseconds, offline: false);
        P1OfflineResult result = AdvanceSimulated(simulated, offline: false, asyncPreparation: false);
        Journey.Synchronize(this);
        return result;
    }

    public P1OfflineResult AdvanceResponsive(long realElapsedMilliseconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(realElapsedMilliseconds);
        long maximum = GameForWork.Core.Offline.OfflineTime.MaximumMilliseconds;
        long simulated = realElapsedMilliseconds > maximum / SimulationSpeed
            ? maximum
            : realElapsedMilliseconds * SimulationSpeed;
        Journey.AddElapsed(realElapsedMilliseconds, offline: false);
        P1OfflineResult result = AdvanceSimulated(simulated, offline: false, asyncPreparation: true);
        Journey.Synchronize(this);
        return result;
    }

    public P1OfflineResult AdvanceOffline(long elapsedMilliseconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(elapsedMilliseconds);
        long effective = Math.Min(elapsedMilliseconds, GameForWork.Core.Offline.OfflineTime.MaximumMilliseconds);
        Journey.AddElapsed(effective, offline: true);
        P1OfflineResult result = AdvanceSimulated(effective, offline: true, asyncPreparation: false);
        Journey.Synchronize(this);
        return result;
    }

    public void AdvanceTownOnly(long realElapsedMilliseconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(realElapsedMilliseconds);
        long maximum = GameForWork.Core.Offline.OfflineTime.MaximumMilliseconds;
        long simulated = realElapsedMilliseconds > maximum / SimulationSpeed
            ? maximum
            : realElapsedMilliseconds * SimulationSpeed;
        Journey.AddElapsed(realElapsedMilliseconds, offline: false);
        AdvanceTownSystems(simulated);
        Journey.Synchronize(this);
    }

    private P1OfflineResult AdvanceSimulated(long simulatedMilliseconds, bool offline, bool asyncPreparation)
    {
        AdvanceTownSystems(simulatedMilliseconds);
        if (!Campaign.Completed)
        {
            P2CampaignAdvanceResult campaignResult = _campaignSimulator.Simulate(
                Campaign,
                World,
                Management,
                simulatedMilliseconds,
                Seed,
                offline,
                asyncPreparation);
            SynchronizeCampaignAscendancyPoints();
            SimulationSequence = checked(SimulationSequence + campaignResult.NodesCompleted);
            if (campaignResult.NodesCompleted > 0) RefreshHeroBuild();
            return new P1OfflineResult(
                campaignResult.EffectiveMilliseconds,
                campaignResult.WasClamped,
                0,
                0,
                World.Teams.Select(team => new P1OfflineTeamSummary(
                    team.Kind,
                    team.MapsCompleted,
                    team.MapsFailed,
                    team.Queue.Count,
                    team.IsStopped,
                    team.StopReason)).ToArray(),
                campaignResult.FinalHash);
        }

        int skillStoneRewardsBefore = World.Economy.SkillStones;
        Dictionary<ExpeditionTeamKind, int> completedBefore = World.Teams.ToDictionary(team => team.Kind, team => team.MapsCompleted);
        P1OfflineResult result = _simulator.Simulate(
            World,
            simulatedMilliseconds,
            Seed,
            offline,
            asyncPreparation);
        SimulationSequence = checked(
            SimulationSequence + result.TotalMapsCompleted + result.TotalMapsFailed);
        foreach (P1TeamExpeditionState team in World.Teams)
        {
            int completed = team.MapsCompleted - completedBefore[team.Kind];
            if (completed <= 0 || team.LastRun is not { Succeeded: true } run) continue;
            bool special = P10EndgameState.IsCitadel(run.Map) || P10EndgameState.IsCitadelPractice(run.Map) ||
                           P10EndgameState.IsBreakthroughTrial(run.Map);
            if (!special)
                for (int index = 0; index < completed; index++)
                    Endgame.RecordMapCompletion(run.Map, run.Route, Seed ^ (ulong)SimulationSequence ^ ((ulong)(int)team.Kind << 48) ^ (uint)index);
            if (P10EndgameState.IsBreakthroughTrial(run.Map)) RecordFinalBreakthroughTrialVictory();
            if (P10EndgameState.IsCitadel(run.Map))
            {
                Endgame.RecordCitadelVictory();
                if (Endgame.TryClaimCitadelMythic())
                {
                    ItemInstance mythic = P14UniqueItems.Create("core.mythic.heart_of_ash", 120,
                        $"mythic-heart-of-ash-{SimulationSequence:000000}");
                    if (!World.Storage.TryStore(mythic)) Management.AddToRecovery(mythic, "灰烬天垒首杀奖励；仓库已满");
                }
            }
        }
        int ashes = World.Economy.TakeMemoryAshes();
        if (ashes > 0)
        {
            Passives.AddMemoryAshes(ashes);
        }

        int newSkillStones = Math.Max(0, World.Economy.SkillStones - skillStoneRewardsBefore);
        for (int index = 0; index < newSkillStones; index++)
        {
            Management.AddDroppedSkillStone(Seed ^ ((ulong)SimulationSequence << 32) ^ (uint)index);
        }
        if (result.TotalMapsCompleted > 0)
        {
            Management.AddSkillExperience(checked(result.TotalMapsCompleted * 120));
            Town.AddActiveExperience(checked(result.TotalMapsCompleted * 120));
        }

        if (World.Expedition.Reports.Any(report => report.Context.Contains("深渊监守者", StringComparison.Ordinal)))
            Town.RecordMilestone("p9.milestone.abyss_warden", World.Economy);
        if (result.TotalMapsCompleted > 0 || ashes > 0 || newSkillStones > 0)
            RefreshHeroTeamBuild();
        if (result.TotalMapsCompleted > 0) RefreshMercenaryPartyBuild();
        return result;
    }

    private void AdvanceTownSystems(long simulatedMilliseconds)
    {
        Town.Advance(simulatedMilliseconds, World.Economy, map => World.MapInventory.Add(map));
        ApplyTownBuildingEffects();
    }

    private void ApplyTownBuildingEffects()
    {
        int storageCapacity = Town.Level(P9BuildingKind.Storage) switch { 1 => 100, 2 => 150, 3 => 225, _ => 325 };
        World.Storage.TrySetCapacity(Math.Max(storageCapacity, World.Storage.Count));
        World.Teleporter.TrySetLevel(Town.Level(P9BuildingKind.Teleporter));
    }

    private void SynchronizeCampaignAscendancyPoints()
    {
        if (Campaign.CompletedNodeIds.Contains("core.campaign.act3.node6"))
            Endgame.AwardCampaignAscendancyPoints(3);
        if (Campaign.CompletedNodeIds.Contains("core.campaign.act5.node6"))
            Endgame.AwardCampaignAscendancyPoints(5);
    }

    public bool TryUpgradeTownBuilding(P9BuildingKind kind, out string message) =>
        Town.TryStartUpgrade(kind, World.Economy, out message);

    public void SetTownPolicy(P9TownPolicy policy) => Town.SetPolicy(policy);

    public bool TryRefreshTavern() => Town.TryManualRefresh(World.Economy);

    public bool TryRecruitMercenary(string stableId, out string message)
    {
        bool changed = Town.TryRecruit(stableId, World.Economy, out message);
        if (changed) RefreshMercenaryPartyBuild();
        return changed;
    }

    public bool TryPlaceMercenary(string stableId, int slot)
    {
        if (World.Mercenaries.ActiveMap is not null) return false;
        bool changed = Town.TryPlaceFormation(stableId, slot);
        if (changed) RefreshMercenaryPartyBuild();
        return changed;
    }

    public bool TryClearMercenarySlot(int slot)
    {
        if (World.Mercenaries.ActiveMap is not null) return false;
        bool changed = Town.ClearFormationSlot(slot);
        if (changed) RefreshMercenaryPartyBuild();
        return changed;
    }

    public bool TryDismissMercenary(string stableId, out string message)
    {
        if (World.Mercenaries.ActiveMap is not null)
        {
            message = "远征中不能解雇佣兵。";
            return false;
        }
        bool changed = Town.TryDismiss(stableId, World.Storage, out message);
        if (changed) RefreshMercenaryPartyBuild();
        return changed;
    }

    public bool TryTransmuteMetal(MetalCurrencyKind output) => Town.TryTransmute(World.Economy, output);

    public void ResumeCampaignAfterDefeat()
    {
        Campaign.ResumeAfterDefeat();
        Management.AddHistory("主线当前节点已准备重新尝试。");
    }

    public bool ReplayCampaignNode(string stableId)
    {
        bool replayed = new P2CampaignSimulator().Replay(
            Campaign,
            World,
            Management,
            stableId,
            Seed ^ (ulong)SimulationSequence++);
        if (replayed)
        {
            RefreshHeroBuild();
        }

        return replayed;
    }

    public void SetHeavyStrikeSupports(SkillSupport supports)
    {
        SkillStoneInstance active = Management.SkillStones.Single(
            item => item.DefinitionId == "core.skill_stone.heavy_strike");
        string[] supportIds = SupportDefinitionIds(supports)
            .Select(definition => Management.SkillStones.FirstOrDefault(item => item.DefinitionId == definition)?.InstanceId)
            .Where(id => id is not null)
            .Cast<string>()
            .ToArray();
        P5SkillChainDefinition? chain = GetSkillChains().FirstOrDefault(item =>
            item.StableId == Management.SkillLinks.First(link => link.ActiveStoneInstanceId == active.InstanceId).ChainId);
        Management.ReplaceSupports(active.InstanceId, supportIds, chain?.SupportCapacity ?? 5);
        HeavyStrikeSupports = SupportsFor("core.skill_stone.heavy_strike");
        RefreshHeroBuild();
    }

    public void SyncHeavyStrikeFromSkillStones()
    {
        SetHeavyStrikeSupports(SupportsFor("core.skill_stone.heavy_strike"));
    }

    public IReadOnlyList<P5SkillChainDefinition> GetSkillChains() => P5SkillChainRules.Build(HeroEquipment);

    public bool TryAssignActiveSkill(string activeStoneInstanceId, string chainId)
    {
        bool changed = Management.TryAssignActiveToChain(activeStoneInstanceId, chainId, GetSkillChains());
        if (changed)
        {
            SyncHeavyStrikeFromSkillStones();
        }

        return changed;
    }

    public bool TryLinkSkillSupport(string activeStoneInstanceId, string supportStoneInstanceId)
    {
        SkillLinkConfiguration? link = Management.SkillLinks.FirstOrDefault(
            item => item.ActiveStoneInstanceId == activeStoneInstanceId);
        P5SkillChainDefinition? chain = GetSkillChains().FirstOrDefault(item => item.StableId == link?.ChainId);
        bool changed = chain is not null &&
            Management.TryLinkSupport(activeStoneInstanceId, supportStoneInstanceId, chain.SupportCapacity);
        if (changed)
        {
            SyncHeavyStrikeFromSkillStones();
        }

        return changed;
    }

    public bool UnlinkSkillSupport(string activeStoneInstanceId, string supportStoneInstanceId)
    {
        bool changed = Management.UnlinkSupport(activeStoneInstanceId, supportStoneInstanceId);
        if (changed)
        {
            SyncHeavyStrikeFromSkillStones();
        }

        return changed;
    }

    public bool TryPlaceSkillStone(string chainId, int socketIndex, string stoneInstanceId)
    {
        bool changed = Management.TryPlaceStone(chainId, socketIndex, stoneInstanceId, GetSkillChains());
        if (changed)
        {
            HeavyStrikeSupports = SupportsFor("core.skill_stone.heavy_strike");
            RefreshHeroBuild();
        }
        return changed;
    }

    public bool UnsocketSkillStone(string chainId, int socketIndex)
    {
        bool changed = Management.UnsocketStone(chainId, socketIndex, GetSkillChains());
        if (changed)
        {
            HeavyStrikeSupports = SupportsFor("core.skill_stone.heavy_strike");
            RefreshHeroBuild();
        }
        return changed;
    }

    public bool ConfigureActiveSkill(string activeStoneInstanceId, int priority, SkillAiRule aiRule, bool reservationEnabled)
    {
        bool changed = Management.ConfigureSkill(activeStoneInstanceId, priority, aiRule, reservationEnabled);
        if (changed)
        {
            RefreshHeroTeamBuild();
        }
        return changed;
    }

    public bool ConfigureSkillTarget(string activeStoneInstanceId, SkillTargetPolicy targetPolicy)
    {
        SkillLinkConfiguration? link = Management.SkillLinks.FirstOrDefault(
            item => item.ActiveStoneInstanceId == activeStoneInstanceId);
        if (link is null || !Enum.IsDefined(targetPolicy)) return false;
        SkillAiRule current = link.AiRule ?? new SkillAiRule();
        bool changed = Management.ConfigureSkill(
            activeStoneInstanceId,
            link.Priority,
            current with
            {
                TargetPolicy = targetPolicy,
                EnemyRarity = "任意",
                BossOnly = false,
                MatchAll = true,
                MinimumLifeBasisPoints = 0,
                MinimumManaBasisPoints = 0,
                MinimumEnemyCount = 1,
                MinimumDistanceRaw = 0,
                MaximumDistanceRaw = 30_000,
                DangerThreshold = 0,
            },
            link.ReservationEnabled);
        if (changed)
        {
            RefreshHeroTeamBuild();
            RecordJourneyEvent(P8JourneyEvent.ConfiguredSkillTarget);
        }
        return changed;
    }

    public bool CanSwitchSkillScheme => Campaign.Completed && World.Hero.ActiveMap is null;

    public void SaveSkillScheme(P6SkillSchemeKind kind) => Management.SaveSkillScheme(kind);

    public P6SchemeSwitchResult SwitchSkillScheme(P6SkillSchemeKind kind)
    {
        if (!CanSwitchSkillScheme)
        {
            return new P6SchemeSwitchResult(false, 0, 0, "只能在主线完成后的城镇或主角队空闲时切换方案。");
        }
        P6SchemeSwitchResult result = Management.SwitchSkillScheme(kind, GetSkillChains());
        if (result.Succeeded)
        {
            HeavyStrikeSupports = SupportsFor("core.skill_stone.heavy_strike");
            RefreshHeroBuild();
        }
        return result;
    }

    public void AssignExpedition(
        ExpeditionTeamKind teamKind,
        P5ExpeditionTarget target,
        P5DispatchMode mode)
    {
        P1TeamExpeditionState team = Team(teamKind);
        ReturnQueuedMaps(team);
        World.Expedition.Assign(teamKind, target, mode);
        team.ResumeForNewDispatch();
        World.Expedition.PrepareNext(World, team);
        if (team.Queue.Maps.FirstOrDefault() is { } queued)
            team.Queue.TryReplaceAt(0, queued with
            {
                AtlasSnapshot = Endgame.AtlasPassives.Order(StringComparer.Ordinal).ToArray(),
            });
        Journey.Synchronize(this);
    }

    public void RecordJourneyEvent(P8JourneyEvent journeyEvent)
    {
        Journey.Record(journeyEvent);
        Journey.Synchronize(this);
    }

    public void CancelExpedition(ExpeditionTeamKind teamKind)
    {
        P1TeamExpeditionState team = Team(teamKind);
        ReturnQueuedMaps(team);
        World.Expedition.Cancel(teamKind);
        team.Stop("manual_stop");
    }

    public void SetHeroAi(HeroAiConfiguration configuration)
    {
        HeroAi = configuration.Validate();
        RefreshHeroTeamBuild();
    }

    public bool TryAllocatePassive(string stableId)
    {
        bool changed = Passives.TryAllocate(stableId, World.Hero.Progression.EarnedPassivePoints);
        if (changed)
        {
            RefreshHeroBuild();
            Journey.Synchronize(this);
        }

        return changed;
    }

    public bool TryAllocatePassivePath(string stableId)
    {
        bool changed = Passives.TryAllocatePath(stableId, World.Hero.Progression.EarnedPassivePoints);
        if (changed)
        {
            RefreshHeroBuild();
            Journey.Synchronize(this);
        }
        return changed;
    }

    public bool TryAddMercenaryToParty(string stableId)
    {
        if (World.Mercenaries.ActiveMap is not null) return false;
        bool changed = Town.TryAddPartyMember(stableId);
        if (changed) RefreshMercenaryPartyBuild();
        return changed;
    }

    public bool TryRemoveMercenaryFromParty(string stableId)
    {
        if (World.Mercenaries.ActiveMap is not null) return false;
        bool changed = Town.TryRemovePartyMember(stableId);
        if (changed) RefreshMercenaryPartyBuild();
        return changed;
    }

    public bool TryRefundPassive(string stableId)
    {
        bool changed = Passives.TryRefund(stableId);
        if (changed)
        {
            RefreshHeroBuild();
        }

        return changed;
    }

    public bool TryResetPassives()
    {
        bool changed = Management.FreeFullRespecAvailable
            ? Passives.ForceReset() && Management.ConsumeFreeFullRespec()
            : Passives.TryReset();
        if (changed)
        {
            RefreshHeroBuild();
        }

        return changed;
    }

    public bool TryEquipFromStorage(int storageIndex, EquipmentSlot slot)
    {
        return new P2ItemCommandService(this).TryEquip(ItemContainerKind.Storage, storageIndex, slot).Succeeded;
    }

    public void NotifyEquipmentChanged(P2CharacterKind character = P2CharacterKind.Hero)
    {
        if (character == P2CharacterKind.Hero)
        {
            Management.NormalizeSkillChains(GetSkillChains());
            HeavyStrikeSupports = SupportsFor("core.skill_stone.heavy_strike");
            RefreshHeroBuild();
        }
        else
        {
            RefreshMercenaryBuild();
        }
    }

    public WorkshopResult CraftEquippedWeapon()
    {
        if (!HeroEquipment.Items.TryGetValue(EquipmentSlot.MainHand, out ItemInstance? weapon))
        {
            return new WorkshopResult(false, "weapon_required", null);
        }

        WorkshopResult result = P1Workshop.CraftPhysicalIncrease(World.Economy, weapon);
        if (result.Succeeded)
        {
            HeroEquipment.TryEquip(EquipmentSlot.MainHand, result.Item!);
            RefreshHeroBuild();
            RecordJourneyEvent(P8JourneyEvent.CraftedItem);
        }

        return result;
    }

    public P2WorkshopPreview CraftStorageItem(int storageIndex, P2WorkshopRecipe recipe)
    {
        if (storageIndex < 0 || storageIndex >= World.Storage.Items.Count)
        {
            return new P2WorkshopPreview(false, "item_required", null, 0, 0, "请先选择仓库物品。");
        }

        ItemInstance item = World.Storage.Items[storageIndex];
        P2WorkshopPreview result = P2Workshop.Craft(World.Economy, item, recipe);
        if (result.Succeeded)
        {
            World.Storage.TryReplaceAt(storageIndex, result.Result!);
            Management.AddHistory($"工坊完成：{result.Summary}。");
            RecordJourneyEvent(P8JourneyEvent.CraftedItem);
        }

        return result;
    }

    public bool TryExchangeLegendary(string? stableId = null)
    {
        if (World.Storage.IsFull)
        {
            return false;
        }
        string selected = stableId ?? P20.P20LegendaryDrops.ExchangePool[0].StableId;
        return World.Economy.TryExchangeLegendary(selected, out ItemInstance? item) && item is not null &&
            World.Storage.TryStore(item);
    }

    public int EnqueueInventoryMaps()
    {
        int moved = 0;
        for (int index = World.MapInventory.Count - 1; index >= 0; index--)
        {
            P1TeamExpeditionState team = (moved & 1) == 0 ? World.Hero : World.Mercenaries;
            P1MapItem map = World.MapInventory[index].EnsureFormal(Seed ^ (ulong)SimulationSequence ^ (ulong)index);
            map = map with { AtlasSnapshot = Endgame.AtlasPassives.Order(StringComparer.Ordinal).ToArray() };
            if (map.Tier > World.MaximumUnlockedMapTier) continue;
            if (!team.Queue.TryEnqueue(map))
            {
                team = team == World.Hero ? World.Mercenaries : World.Hero;
                if (!team.Queue.TryEnqueue(map))
                {
                    continue;
                }
            }

            World.MapInventory.RemoveAt(index);
            moved++;
        }

        return moved;
    }

    public bool TrySelectMapRoute(int mapIndex, MapRoute route)
    {
        if (mapIndex < 0 || mapIndex >= World.MapInventory.Count) return false;
        P1MapItem map = World.MapInventory[mapIndex].EnsureFormal(Seed ^ (ulong)mapIndex);
        if (!map.EffectiveRouteCandidates.Contains(route)) return false;
        World.MapInventory[mapIndex] = map with { SelectedRoute = route };
        return true;
    }

    public P12MapCraftResult CraftMap(int mapIndex, P12MapCraftOperation operation)
    {
        if (mapIndex < 0 || mapIndex >= World.MapInventory.Count)
            return new(false, new P1MapItem("invalid", 1), default, 0, "map_required");
        ulong seed = Seed ^ (ulong)SimulationSequence++ ^ (ulong)(mapIndex + 1) * 0x9e3779b97f4a7c15UL;
        P12MapCraftResult result = P12MapCrafting.Apply(World.Economy, World.MapInventory[mapIndex], operation,
            seed, World.MaximumUnlockedMapTier);
        if (result.Succeeded) World.MapInventory[mapIndex] = result.Map;
        return result;
    }

    public P12MapBatchResult BatchCraftMaps(P12MapBatchRule requestedRule)
    {
        P12MapBatchRule rule = requestedRule.Validate();
        int processed = 0, completed = 0, skipped = 0, spent = 0;
        bool stopped = false;
        for (int index = 0; index < World.MapInventory.Count; index++)
        {
            P1MapItem map = World.MapInventory[index].EnsureFormal(Seed ^ (ulong)index);
            int mapSpent = 0;
            bool failed = false;
            while (map.Quality < rule.MinimumQuality)
            {
                if (!ApplyBatchOperation(index, ref map, P12MapCraftOperation.PolishQuality, rule.MaximumMetalSpendPerMap, ref mapSpent))
                { failed = true; break; }
            }
            if (!failed && map.Rarity < rule.TargetRarity)
            {
                P12MapCraftOperation upgrade = rule.TargetRarity == P12MapRarity.Rare
                    ? P12MapCraftOperation.AlchemicalRare : P12MapCraftOperation.AwakenMagic;
                if (!ApplyBatchOperation(index, ref map, upgrade, rule.MaximumMetalSpendPerMap, ref mapSpent)) failed = true;
            }
            while (!failed && (rule.ExcludedAffixes?.Any(kind => map.EffectiveAffixes.Any(affix => affix.Kind == kind)) ?? false))
            {
                if (map.Rarity != P12MapRarity.Rare ||
                    !ApplyBatchOperation(index, ref map, P12MapCraftOperation.ChaosReroll, rule.MaximumMetalSpendPerMap, ref mapSpent)) failed = true;
            }
            if (!failed && rule.Corrupt && !map.IsCorrupted &&
                !ApplyBatchOperation(index, ref map, P12MapCraftOperation.Corrupt, rule.MaximumMetalSpendPerMap, ref mapSpent)) failed = true;

            processed++; spent += mapSpent;
            World.MapInventory[index] = map;
            if (!failed) completed++;
            else
            {
                skipped++;
                if (rule.FailureBehavior == P12BatchFailureBehavior.Stop) { stopped = true; break; }
            }
        }
        return new(processed, completed, skipped, spent, stopped,
            $"处理 {processed} 张，完成 {completed} 张，跳过 {skipped} 张，消耗金属 {spent}。" );
    }

    public void SetExpeditionPolicy(ExpeditionTeamKind kind, ExpeditionPolicy policy) => Team(kind).ApplyPolicy(policy);

    public bool TrySwitchAtlasScheme(int index)
    {
        if (index == Endgame.ActiveAtlasSchemeIndex || index is < 0 or > 2 || !World.Economy.TrySpendMemoryAshes(1)) return false;
        if (Endgame.TrySwitchAtlasScheme(index)) return true;
        World.Economy.AddRewards(new MapStackableRewards(0, 0, 1, 0, 0));
        return false;
    }

    public bool TryRenameAtlasScheme(int index, string name) => Endgame.TryRenameAtlasScheme(index, name);

    public bool RecordFinalBreakthroughTrialVictory()
    {
        if (!Endgame.TryCompleteFinalBreakthrough(World.Hero.Progression.Level, trialWon: true)) return false;
        World.UnlockFinalMapTiers();
        World.Hero.Progression.UnlockFinalBreakthrough();
        return true;
    }

    public bool TryChallengeFinalBreakthrough()
    {
        P1TeamExpeditionState team = World.Hero;
        if (World.Hero.Progression.Level < 100 || Endgame.FinalBreakthroughCompleted ||
            team.ActiveMap is not null || team.Queue.Count > 0) return false;
        World.Expedition.Cancel(ExpeditionTeamKind.Hero, "breakthrough_scheduled");
        team.Resume();
        team.ApplyPolicy(new ExpeditionPolicy(RouteSelectionMode.Automatic, MapRoute.Safe,
            QueueFailureBehavior.Stop, StorageFullBehavior.AcceptStackablesOnly, StopAfterConsecutiveFailures: 1));
        return team.Queue.TryEnqueue(new P1MapItem($"{P10EndgameState.BreakthroughMapPrefix}{SimulationSequence:000000}", 16,
            AtlasSnapshot: Endgame.AtlasPassives.Order(StringComparer.Ordinal).ToArray()));
    }

    private bool ApplyBatchOperation(int index, ref P1MapItem map, P12MapCraftOperation operation,
        int budget, ref int mapSpent)
    {
        (_, int cost) = P12MapCrafting.Cost(operation);
        if (mapSpent + cost > budget) return false;
        ulong seed = Seed ^ (ulong)SimulationSequence++ ^ (ulong)(index + 1) * 0x517cc1b727220a95UL;
        P12MapCraftResult result = P12MapCrafting.Apply(World.Economy, map, operation, seed, World.MaximumUnlockedMapTier);
        if (!result.Succeeded) return false;
        map = result.Map;
        mapSpent += result.Cost;
        return true;
    }

    public CombatPreview GetCombatPreview() => CombatPreviewRules.Calculate(
        _heroBuild.Sheet,
        _heroBuild.EffectiveWeapon,
        _heroBuild.HeavyStrike,
        _heroBuild.Sheet.Accuracy(_heroBuild.FlatAccuracy).Value,
        targetEvasion: 20,
        targetArmor: 25,
        representativeIncomingPhysicalHit: 10,
        addedPhysicalDamage: _heroBuild.AddedPhysicalDamage,
        increasedDamageBasisPoints: _heroBuild.IncreasedAttackDamageBasisPoints,
        increasedCriticalChanceBasisPoints: _heroBuild.IncreasedCriticalChanceBasisPoints,
        increasedBleedChanceBasisPoints: _heroBuild.IncreasedBleedChanceBasisPoints);

    public P6BuildSummary GetBuildSummary() => P6BuildSummaryRules.Calculate(this);

    public P2EquipmentComparison CompareHeroEquipment(ItemInstance candidate, EquipmentSlot slot)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (!EquipmentLoadout.CanEquip(slot, candidate.Base.Category))
        {
            throw new ArgumentException("Candidate cannot be equipped in the requested slot.", nameof(slot));
        }

        bool requirementsMet = candidate.Base.MeetsRequirements(
            World.Hero.Progression.Level,
            _heroBuild.Sheet.Attributes);
        EquipmentLoadout hypothetical = EquipmentLoadout.Restore(HeroEquipment.Items
            .Where(pair => pair.Key != slot)
            .Select(pair => new KeyValuePair<EquipmentSlot, ItemInstance>(pair.Key, pair.Value)));
        if (!hypothetical.TryEquip(slot, candidate))
        {
            throw new InvalidOperationException("Hypothetical equipment could not be assembled.");
        }

        AssembledCharacterBuild proposed = CharacterBuildAssembler.Assemble(
            World.Hero.Progression.Level,
            CharacterAttributes.IronOathStarting,
            hypothetical,
            Passives,
            new SkillConfiguration(P1SkillIds.HeavyStrike, HeavyStrikeSupports));
        CombatPreview currentPreview = GetCombatPreview();
        CombatPreview proposedPreview = Preview(proposed);
        EquipmentSummary current = HeroEquipment.CalculateSummary();
        EquipmentSummary next = hypothetical.CalculateSummary();
        SkillCapacityResult capacity = SkillCapacityRules.Validate(
            [new SkillConfiguration(P1SkillIds.HeavyStrike, HeavyStrikeSupports)], next);
        ItemInstance? currentItem = HeroEquipment.Items.GetValueOrDefault(slot);
        SkillLinkConfiguration? socketGroup = Management.SkillLinks.FirstOrDefault(link =>
            link.ChainId == GameForWork.Core.P6.P6SocketGroupIds.For(slot));
        string[] installed = socketGroup?.SocketStoneInstanceIds?.Where(id => !string.IsNullOrEmpty(id)).Cast<string>().ToArray() ?? [];
        int retainedCount = Math.Min(candidate.LinkedSocketCount, installed.Length);
        string NameOf(string id) => Management.SkillStones.FirstOrDefault(stone => stone.InstanceId == id)?.Definition.DisplayName ?? id;
        return new P2EquipmentComparison(
            proposed.Sheet.MaximumLife().Value - _heroBuild.Sheet.MaximumLife().Value,
            proposed.Sheet.MaximumMana().Value - _heroBuild.Sheet.MaximumMana().Value,
            next.Defense.Armor - current.Defense.Armor,
            next.Defense.Evasion - current.Defense.Evasion,
            next.Defense.Shield - current.Defense.Shield,
            next.CoreSkillCapacity - current.CoreSkillCapacity,
            next.SupportLinkCapacity - current.SupportLinkCapacity,
            proposedPreview.AverageHitDamage.Value - currentPreview.AverageHitDamage.Value,
            proposedPreview.EffectiveLife.Value - currentPreview.EffectiveLife.Value,
            RequirementsMet: requirementsMet,
            DisabledSkillLinks: capacity.IsValid ? 0 : Math.Max(0, capacity.RequiredSupportLinks - capacity.AvailableSupportLinks),
            slot,
            candidate.LinkedSocketCount - (currentItem?.LinkedSocketCount ?? 0),
            installed.Skip(retainedCount).Select(NameOf).ToArray(),
            installed.Take(retainedCount).Select(NameOf).ToArray());
    }

    private static void EquipStarter(EquipmentLoadout equipment, EquipmentSlot slot, string baseId, ulong seed)
    {
        ItemInstance item = ItemGenerator.Generate(baseId, 1, ItemRarity.Basic, seed, $"starter-{slot}");
        if (!equipment.TryEquip(slot, item))
        {
            throw new InvalidOperationException($"Starter item {baseId} could not be equipped.");
        }
    }

    private void RefreshHeroBuild()
    {
        _heroBuild = AssembleHero();
        RefreshHeroTeamBuild();
    }

    private AssembledCharacterBuild AssembleHero()
    {
        AssembledCharacterBuild build = CharacterBuildAssembler.Assemble(
            World.Hero.Progression.Level,
            CharacterAttributes.IronOathStarting,
            HeroEquipment,
            Passives,
            new SkillConfiguration(P1SkillIds.HeavyStrike, HeavyStrikeSupports));
        return build with { Sheet = P18AscendancyRules.ApplySheet(build.Sheet, AscendancyProfile()) };
    }

    private static CombatPreview Preview(AssembledCharacterBuild build) => CombatPreviewRules.Calculate(
        build.Sheet,
        build.EffectiveWeapon,
        build.HeavyStrike,
        build.Sheet.Accuracy(build.FlatAccuracy).Value,
        targetEvasion: 20,
        targetArmor: 25,
        representativeIncomingPhysicalHit: 10,
        addedPhysicalDamage: build.AddedPhysicalDamage,
        increasedDamageBasisPoints: build.IncreasedAttackDamageBasisPoints,
        increasedCriticalChanceBasisPoints: build.IncreasedCriticalChanceBasisPoints,
        increasedBleedChanceBasisPoints: build.IncreasedBleedChanceBasisPoints);

    private void RefreshHeroTeamBuild() => World.Hero.UpdateBuild(
        ToTeamBuild(_heroBuild, HeavyStrikeSupports, HeroAi, BuildActiveSkills(), AscendancyProfile()));

    private P18CombatProfile AscendancyProfile() => new(Endgame.SelectedAscendancy,
        Endgame.AscendancyPassives.Order(StringComparer.Ordinal).ToArray());

    private void RefreshMercenaryBuild()
    {
        RefreshMercenaryPartyBuild();
    }

    public bool TryAllocateAtlasPassive(string stableId) => Endgame.TryAllocateAtlas(stableId);

    public bool TrySelectMastery(string stableId, int option)
    {
        bool changed = Passives.TrySelectMastery(stableId, option);
        if (changed) RefreshHeroBuild();
        return changed;
    }

    public bool TrySocketJewel(string stableId, PassiveJewelKind jewel)
    {
        bool changed = Passives.TrySocketJewel(stableId, jewel);
        if (changed) RefreshHeroBuild();
        return changed;
    }

    public bool TryUnsocketJewel(string stableId)
    {
        bool changed = Passives.TryUnsocketJewel(stableId);
        if (changed) RefreshHeroBuild();
        return changed;
    }

    public bool TryAllocateAscendancyPassive(string stableId)
    {
        bool changed = Endgame.TryAllocateAscendancy(stableId);
        if (changed) RefreshHeroBuild();
        return changed;
    }

    public bool TrySelectAscendancy(P18Ascendancy ascendancy)
    {
        bool changed = Endgame.TrySelectAscendancy(ascendancy);
        if (changed) RefreshHeroBuild();
        return changed;
    }

    public bool TryRefundAscendancyPassive(string stableId)
    {
        P18AscendancyNode node = P18AscendancyCatalog.Get(stableId);
        int cost = node.Kind == P18NodeKind.Core ? 10_000 : 2_000;
        if (!Endgame.AscendancyPassives.Contains(stableId) || World.Economy.Gold < cost ||
            !Endgame.TryRefundAscendancy(stableId)) return false;
        _ = World.Economy.TrySpendGold(cost);
        RefreshHeroBuild();
        return true;
    }

    public bool TryResetAscendancy(bool changePath)
    {
        int cost = changePath ? 100_000 : 50_000;
        if ((!changePath && Endgame.AscendancyPassives.Count == 0) ||
            (changePath && Endgame.SelectedAscendancy == P18Ascendancy.None) ||
            !World.Economy.TrySpendGold(cost)) return false;
        Endgame.ResetAscendancy(changePath);
        RefreshHeroBuild();
        return true;
    }

    public bool TryChallengeCitadel()
    {
        P1TeamExpeditionState team = World.Hero;
        if (team.ActiveMap is not null || team.Queue.Count > 0 || !Endgame.TryConsumeCitadelTicket()) return false;
        World.Expedition.Cancel(ExpeditionTeamKind.Hero, "citadel_scheduled");
        team.Resume();
        team.ApplyPolicy(new ExpeditionPolicy(RouteSelectionMode.Automatic, MapRoute.Abyss,
            QueueFailureBehavior.Stop, StorageFullBehavior.AcceptStackablesOnly, StopAfterConsecutiveFailures: 1));
        return team.Queue.TryEnqueue(new P1MapItem($"{P10EndgameState.CitadelMapPrefix}{SimulationSequence:000000}", 20,
            AtlasSnapshot: Endgame.AtlasPassives.Order(StringComparer.Ordinal).ToArray()));
    }

    public bool TryPracticeCitadel()
    {
        P1TeamExpeditionState team = World.Hero;
        if (team.ActiveMap is not null || team.Queue.Count > 0) return false;
        World.Expedition.Cancel(ExpeditionTeamKind.Hero, "citadel_practice_scheduled");
        team.Resume();
        team.ApplyPolicy(new ExpeditionPolicy(RouteSelectionMode.Automatic, MapRoute.Abyss,
            QueueFailureBehavior.Stop, StorageFullBehavior.AcceptStackablesOnly, StopAfterConsecutiveFailures: 1));
        return team.Queue.TryEnqueue(new P1MapItem($"{P10EndgameState.CitadelPracticeMapPrefix}{SimulationSequence:000000}", 20,
            AtlasSnapshot: Endgame.AtlasPassives.Order(StringComparer.Ordinal).ToArray()));
    }

    private void RefreshMercenaryPartyBuild() => World.Mercenaries.UpdateBuild(
        Town.BuildMercenaryParty(World.Mercenaries.Progression.Level));

    private static P1TeamBuild ToTeamBuild(
        AssembledCharacterBuild build,
        SkillSupport supports,
        HeroAiConfiguration ai,
        IReadOnlyList<SkillConfiguration>? activeSkills = null,
        P18CombatProfile? ascendancy = null) => new P1TeamBuild(
        build.Sheet,
        build.EffectiveWeapon,
        new SkillConfiguration(P1SkillIds.HeavyStrike, supports),
        build.FlatAccuracy,
        build.IncreasedAttackDamageBasisPoints,
        build.IncreasedCriticalChanceBasisPoints,
        build.IncreasedBleedChanceBasisPoints,
        UseWarCry: ai.UseWarCry,
        EchoNotableAllocated: build.Passives.Echo,
        DeepWoundAllocated: build.Passives.DeepWound,
        FasterBleedingAllocated: build.Passives.FasterBleeding,
        AiSummary: $"{ai.Preset}：生命低于 {ai.LifeFlaskThresholdBasisPoints / 100}% 时使用药剂；" +
            $"{(ai.UseWarCry ? "资源允许时先战吼" : "不使用战吼")}，随后自动重击；策略变更在下一张地图生效。",
        LifeFlask: new LifeFlaskDefinition(BaseRecovery: 40, MaximumCharges: 30, ChargesPerUse: 10),
        IncreasedLifeFlaskEffectBasisPoints: checked(
            build.Equipment.Modifiers.IncreasedLifeFlaskEffectBasisPoints +
            build.Passives.IncreasedLifeFlaskEffectBasisPoints),
        LifeFlaskUseThresholdBasisPoints: ai.LifeFlaskThresholdBasisPoints,
        AddedPhysicalDamage: build.AddedPhysicalDamage,
        HeavyStrikeProfile: build.HeavyStrike,
        WeaponLegendaryRule: build.Equipment.WeaponLegendaryRule,
        MovementSpeedBasisPoints: checked(10_000 + build.Passives.IncreasedMovementSpeedBasisPoints +
            build.Equipment.Modifiers.IncreasedMovementSpeedBasisPoints),
        ActiveSkills: activeSkills ??
        [
            new SkillConfiguration(P1SkillIds.HeavyStrike, supports),
            new SkillConfiguration(P1SkillIds.EarthCleave, SkillSupport.IncreasedArea),
            new SkillConfiguration(P1SkillIds.SpiritBlade, SkillSupport.Chain),
        ],
        Flasks: build.Flasks,
        HasShield: build.Equipment.HasShield,
        BlockChanceBasisPoints: checked(build.Equipment.BaseBlockChanceBasisPoints +
            build.Equipment.Modifiers.BlockChanceBasisPoints + (build.Passives.Advanced?.BlockChanceBasisPoints ?? 0)),
        Ascendancy: ascendancy,
        HasUsableWeapon: build.HasUsableWeapon,
        PassiveProfile: build.Passives.Advanced,
        CriticalMultiplierBasisPoints: checked(15_000 + (build.Passives.Advanced?.IncreasedCriticalMultiplierBasisPoints ?? 0)),
        AlwaysHit: build.Passives.Advanced?.ResoluteTechnique == true,
        CannotCrit: build.Passives.Advanced?.ResoluteTechnique == true,
        IncreasedWarCryCooldownRecoveryBasisPoints: build.Passives.IncreasedWarCryCooldownRecoveryBasisPoints,
        IncreasedWarCryRangeBasisPoints: build.Passives.IncreasedWarCryRangeBasisPoints) with
        {
            AiSummary = $"{ai.Preset} · {(ai.MatchMode == AiRuleMatchMode.All ? "全部满足" : "任一满足")}：" +
                $"敌人≥{ai.MinimumEnemyCount}、稀有度 {ai.EnemyRarity}、距离≤{ai.MaximumEnemyDistance}、" +
                $"危险度≥{ai.DangerThreshold}{(ai.BossPriority ? "、Boss优先" : string.Empty)}；" +
                $"生命低于 {ai.LifeFlaskThresholdBasisPoints / 100}% 使用药剂。"
        };

    private IReadOnlyList<SkillConfiguration> BuildActiveSkills() => Management.SkillLinks
        .Where(link => !string.IsNullOrEmpty(link.ChainId) && !string.IsNullOrEmpty(link.ActiveStoneInstanceId))
        .OrderBy(link => link.Priority)
        .Select(link => (Link: link, Stone: Management.SkillStones.Single(stone => stone.InstanceId == link.ActiveStoneInstanceId)))
        .Where(entry => P17SkillCatalog.Active.First(active => active.StoneId == entry.Stone.DefinitionId).Role !=
                        P17SkillRole.Reservation || entry.Link.ReservationEnabled)
        .Select(entry => new SkillConfiguration(
            ToCombatSkillId(entry.Stone.DefinitionId),
            SupportsFor(entry.Stone.DefinitionId),
            entry.Link.Priority,
            entry.Link.AiRule ?? GlobalSkillRule(),
            entry.Stone.Level,
            entry.Stone.InstanceId))
        .Where(configuration => !string.IsNullOrEmpty(configuration.SkillId))
        .ToArray();

    private SkillAiRule GlobalSkillRule() => new(
        MatchAll: HeroAi.MatchMode == AiRuleMatchMode.All,
        MinimumEnemyCount: HeroAi.MinimumEnemyCount,
        EnemyRarity: HeroAi.EnemyRarity,
        MaximumDistanceRaw: HeroAi.MaximumEnemyDistance * 1_000,
        DangerThreshold: HeroAi.DangerThreshold,
        BossOnly: HeroAi.BossPriority);

    private SkillSupport SupportsFor(string activeDefinitionId)
    {
        SkillStoneInstance? active = Management.SkillStones.FirstOrDefault(item => item.DefinitionId == activeDefinitionId);
        SkillLinkConfiguration? link = active is null
            ? null
            : Management.SkillLinks.FirstOrDefault(item => item.ActiveStoneInstanceId == active.InstanceId);
        SkillSupport supports = SkillSupport.None;
        foreach (string supportId in link?.SupportStoneInstanceIds ?? [])
        {
            supports |= Management.SkillStones.Single(item => item.InstanceId == supportId).Definition.CombatSupport;
        }

        return supports;
    }

    private static string ToCombatSkillId(string definitionId) => definitionId.StartsWith("core.skill_stone.", StringComparison.Ordinal)
        ? definitionId.Replace("core.skill_stone.", "core.skill.", StringComparison.Ordinal)
        : string.Empty;

    private static IEnumerable<string> SupportDefinitionIds(SkillSupport supports)
    {
        foreach (SkillStoneDefinition definition in P2SkillStones.All
                     .Where(item => item.Kind == SkillStoneKind.Support && item.CombatSupport != SkillSupport.None &&
                                    supports.HasFlag(item.CombatSupport))
                     .OrderBy(item => item.StableId, StringComparer.Ordinal))
            yield return definition.StableId;
    }

    private P1TeamExpeditionState Team(ExpeditionTeamKind kind) =>
        kind == ExpeditionTeamKind.Hero ? World.Hero : World.Mercenaries;

    private void ReturnQueuedMaps(P1TeamExpeditionState team)
    {
        while (team.Queue.Count > 0)
        {
            P1MapItem? map = team.Queue.TakeAt(0);
            if (map is not null && !P5ExpeditionDirector.IsBoss(map) && !P5ExpeditionDirector.IsPractice(map) && !P10EndgameState.IsCitadel(map))
            {
                World.MapInventory.Add(map);
            }
        }
    }

}
