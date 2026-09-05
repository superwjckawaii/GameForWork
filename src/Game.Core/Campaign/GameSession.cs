using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Campaign.Progression;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Management;
using GameForWork.Core.Spatial;
using GameForWork.Core.Expeditions;
using GameForWork.Core.Skills;
using GameForWork.Core.Town;
using GameForWork.Core.Endgame;
using GameForWork.Core.Maps;
using GameForWork.Core.Content;
using GameForWork.Core.SkillCatalog;
using GameForWork.Core.Ascendancies;
using GameForWork.Core.Characters;
using GameForWork.Core.Archetypes;
using GameForWork.Core.Atlas;
using GameForWork.Core.Encounters;
using GameForWork.Core.Resources;
using GameForWork.Core.Builds;

namespace GameForWork.Core.Campaign;

public sealed record BossChallengeAvailability(bool Unlocked, int AvailableRuns, string Requirement);

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

public sealed record PlayerIdentity(
    string Name,
    CharacterGender Gender,
    CharacterSkinTone SkinTone,
    CharacterHairStyle HairStyle,
    BaseClass BaseClass)
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

public sealed record GameSessionSnapshot(
    int FormatVersion,
    PlayerIdentity Player,
    string MercenaryName,
    WorldSnapshot World,
    IReadOnlyList<EquippedItemSnapshot> HeroEquipment,
    IReadOnlyList<string> AllocatedPassives,
    int MemoryAshes,
    SkillSupport HeavyStrikeSupports,
    HeroAiConfiguration? HeroAi,
    bool DebugTwentyTimes,
    ulong Seed,
    int SimulationSequence,
    ManagementSnapshot? Management = null,
    IReadOnlyList<EquippedItemSnapshot>? MercenaryEquipment = null,
    CampaignSnapshot? Campaign = null,
    DemoJourneySnapshot? Journey = null,
    TownSnapshot? Town = null,
    EndgameSnapshot? Endgame = null,
    IReadOnlyDictionary<string, int>? MasterySelections = null,
    IReadOnlyDictionary<string, PassiveJewelKind>? SocketedJewels = null,
    JewelStateSnapshot? Jewels = null,
    bool CitadelDropCompensationGranted = false);

public sealed class GameSession
{
    public const int CurrentFormatVersion = 25;
    private readonly WorldSimulator _simulator = new(new MapAttemptResolver());
    private readonly CampaignSimulator _campaignSimulator = new();
    private AssembledCharacterBuild _heroBuild;

    private GameSession(
        PlayerIdentity player,
        string mercenaryName,
        WorldState world,
        EquipmentLoadout heroEquipment,
        EquipmentLoadout mercenaryEquipment,
        PassiveTreeAllocation passives,
        SkillSupport heavyStrikeSupports,
        HeroAiConfiguration heroAi,
        ManagementState management,
        CampaignState campaign,
        DemoJourney journey,
        TownState town,
        EndgameState endgame,
        JewelState jewels,
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
        Jewels = jewels;
        World.Hero.Progression.SynchronizeStoryPassivePoints(Campaign.CompletedNodeIds.Count);
        Seed = seed;
        SimulationSequence = simulationSequence;
        DebugTwentyTimes = debugTwentyTimes;
        Management.NormalizeSkillChains(SkillChainRules.Build(HeroEquipment));
        HeavyStrikeSupports = SupportsFor("core.skill_stone.heavy_strike");
        SynchronizeCampaignAscendancyPoints();
        _heroBuild = AssembleHero();
        RefreshHeroTeamBuild();
        RefreshMercenaryPartyBuild();
        World.Expedition.BossMapFactory = CreateBossMap;
    }

    public PlayerIdentity Player { get; }
    public string MercenaryName { get; }
    public WorldState World { get; }
    public EquipmentLoadout HeroEquipment { get; }
    public EquipmentLoadout MercenaryEquipment { get; }
    public PassiveTreeAllocation Passives { get; private set; }
    public SkillSupport HeavyStrikeSupports { get; private set; }
    public HeroAiConfiguration HeroAi { get; private set; }
    public ManagementState Management { get; }
    public CampaignState Campaign { get; }
    public DemoJourney Journey { get; }
    public TownState Town { get; }
    public EndgameState Endgame { get; }
    public JewelState Jewels { get; }
    public int UnlockedFlaskSlots => Math.Clamp(2 + Town.Level(BuildingKind.Teleporter),
        Flasks.InitialSlots, Flasks.MaximumSlots);
    public bool IsExpeditionUnlocked => Campaign.Completed;
    public bool DebugTwentyTimes { get; set; }
    public ulong Seed { get; }
    public int SimulationSequence { get; private set; }
    public bool CitadelDropCompensationGranted { get; private set; }
    public AssembledCharacterBuild HeroBuild => _heroBuild;
    public int SimulationSpeed => DebugTwentyTimes ? 20 : 1;

    public static GameSession CreateNew(PlayerIdentity player, ulong seed, bool tutorialEnabled = true)
    {
        ClassDefinition classDefinition = ClassCatalog.Get(player.BaseClass);
        var equipment = new EquipmentLoadout();
        EquipStarter(equipment, EquipmentSlot.MainHand, classDefinition.StarterWeaponBaseId, seed + 1);
        EquipStarter(equipment, EquipmentSlot.Chest, classDefinition.StarterChestBaseId, seed + 2);
        EquipStarter(equipment, EquipmentSlot.Helmet, classDefinition.StarterHelmetBaseId, seed + 3);
        EquipStarter(equipment, EquipmentSlot.Gloves, "core.base.iron_gauntlets", seed + 6);
        EquipStarter(equipment, EquipmentSlot.RingLeft, "core.base.life_ring", seed + 4);
        EquipStarter(equipment, EquipmentSlot.Flask1, "core.base.life_flask", seed + 5);
        EquipStarter(equipment, EquipmentSlot.Flask2, "core.base.mana_flask", seed + 7);
        var passives = new PassiveTreeAllocation(start: classDefinition.PassiveStart);
        AssembledCharacterBuild build = CharacterBuildAssembler.Assemble(
            1,
            classDefinition.StartingAttributes,
            equipment,
            passives,
            new SkillConfiguration(SkillIds.HeavyStrike, SkillSupport.Bleed));
        MercenaryProfile mercenary = StartingMercenaryFactory.GenerateCantor(seed ^ 0xa5a5a5a5UL);
        var economy = new TownEconomyState(
            memoryAshes: 0,
            metalCurrencies: Enum.GetValues<MetalCurrencyKind>().ToDictionary(
                kind => kind,
                kind => kind is MetalCurrencyKind.TemperingIron or MetalCurrencyKind.WardSteel or MetalCurrencyKind.VitalSilver ? 3 : 0));
        TownState town = TownState.CreateNew(seed ^ 0x7039746f776eUL, mercenary.Equipment);
        var world = new WorldState(
            ToTeamBuild(build, SkillSupport.Bleed, HeroAiConfiguration.Balanced),
            town.BuildMercenaryParty(1),
            economy);
        return new GameSession(
            player,
            mercenary.Name,
            world,
            equipment,
            mercenary.Equipment,
            passives,
            SkillSupport.Bleed,
            HeroAiConfiguration.Balanced with { Preset = classDefinition.AiPreset },
            ManagementState.CreateNew(player.BaseClass),
            CampaignState.CreateNew(),
            DemoJourney.CreateNew(tutorialEnabled),
            town,
            new EndgameState(),
            new JewelState(),
            seed,
            simulationSequence: 0,
            debugTwentyTimes: false);
    }

    public static GameSession Restore(GameSessionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        snapshot = GameForWork.Core.Persistence.SaveIdentifierMigration.Upgrade(snapshot);
        bool migratingV18 = snapshot.FormatVersion == 18;
        bool migratingV19 = snapshot.FormatVersion == 19;
        bool migratingV20 = snapshot.FormatVersion == 20;
        bool migratingV21 = snapshot.FormatVersion == 21;
        bool migratingV22 = snapshot.FormatVersion == 22;
        bool migratingV23 = snapshot.FormatVersion == 23;
        if (snapshot.FormatVersion is < 18 or > CurrentFormatVersion || snapshot.SimulationSequence < 0)
        {
            throw new InvalidDataException(
                $"Campaign session snapshot version {snapshot.FormatVersion} is unsupported; expected {CurrentFormatVersion}.");
        }

        EquipmentLoadout equipment = EquipmentLoadout.Restore(
            snapshot.HeroEquipment.Select(entry =>
                new KeyValuePair<EquipmentSlot, ItemInstance>(entry.Slot, entry.Item)));
        MercenaryProfile restoredMercenary = StartingMercenaryFactory.GenerateCantor(snapshot.Seed ^ 0xa5a5a5a5UL);
        EquipmentLoadout mercenaryEquipment = snapshot.MercenaryEquipment is null
            ? restoredMercenary.Equipment
            : EquipmentLoadout.Restore(snapshot.MercenaryEquipment.Select(entry =>
                new KeyValuePair<EquipmentSlot, ItemInstance>(entry.Slot, entry.Item)));
        BaseClass baseClass = migratingV18 ? BaseClass.Fighter : snapshot.Player.BaseClass;
        ClassDefinition classDefinition = ClassCatalog.Get(baseClass);
        PlayerIdentity player = snapshot.Player with { BaseClass = baseClass };
        JewelState jewels = JewelState.Restore(snapshot.Jewels);
        PassiveTreeAllocation passives = migratingV18 || migratingV19 || migratingV20 || migratingV21
            ? new PassiveTreeAllocation(snapshot.MemoryAshes, classDefinition.PassiveStart)
            : PassiveTreeAllocation.Restore(snapshot.AllocatedPassives, snapshot.MemoryAshes,
                snapshot.MasterySelections, snapshot.SocketedJewels, classDefinition.PassiveStart, jewels);
        WorldState world = WorldSnapshots.Restore(snapshot.World);
        if (migratingV20)
        {
            int oldEarnedPoints = Math.Min(25, (snapshot.Endgame?.CompletedTiers.Count ?? 0) + (snapshot.Endgame?.BonusAtlasPoints ?? 0));
            world.Economy.AddDispositionProceeds(Math.Min(100_000, oldEarnedPoints * 4_000), 0);
        }
        TownState town = TownState.Restore(snapshot.Town, snapshot.Seed ^ 0x7039746f776eUL, mercenaryEquipment);
        if (town.Roster.Count > 0) mercenaryEquipment = town.Roster[0].Equipment;
        ManagementState management = ManagementState.Restore(snapshot.Management, legacyMigration: migratingV18);
        EndgameState endgame = EndgameState.Restore(snapshot.Endgame);
        if (endgame.SelectedAscendancy != Ascendancy.None &&
            (!ClassCatalog.Allows(baseClass, endgame.SelectedAscendancy) ||
             !WarriorAscendancyCatalog.IsImplemented(endgame.SelectedAscendancy)))
            throw new InvalidDataException("Saved ascendancy does not belong to the selected base class.");
        var session = new GameSession(
            player,
            snapshot.MercenaryName,
            world,
            equipment,
            mercenaryEquipment,
            passives,
            snapshot.HeavyStrikeSupports,
            snapshot.HeroAi ?? HeroAiConfiguration.Balanced with { Preset = classDefinition.AiPreset },
            management,
            CampaignState.Restore(snapshot.Campaign, legacyMigration: false),
            DemoJourney.Restore(snapshot.Journey, legacy: false),
            town,
            endgame,
            jewels,
            snapshot.Seed,
            snapshot.SimulationSequence,
            snapshot.DebugTwentyTimes);
        session.CitadelDropCompensationGranted = snapshot.CitadelDropCompensationGranted;
        if (migratingV23 && session.Endgame.CitadelVictories > 0 && !session.HasMythic("equipment.legendary.52.44a586da1f"))
        {
            ItemInstance compensation = UniqueItems.Create(
                "equipment.legendary.52.44a586da1f", 100,
                $"citadel-pool-compensation-{snapshot.Seed:x16}");
            if (!session.World.Storage.TryStore(compensation))
                session.Management.AddToRecovery(compensation, "灰烬天垒掉落池修复补偿");
            session.Management.AddHistory($"灰烬天垒掉落池修复：补偿百骸噬界（已记录 {session.Endgame.CitadelVictories:N0} 次天垒胜利）。");
            session.CitadelDropCompensationGranted = true;
        }
        session.ApplyTownBuildingEffects();
        if (session.Endgame.FinalBreakthroughCompleted)
        {
            session.World.UnlockFinalMapTiers();
            session.World.Hero.Progression.UnlockFinalBreakthrough();
        }
        session.Journey.Synchronize(session);
        return session;
    }

    public GameSessionSnapshot Capture() => new(
        CurrentFormatVersion,
        Player,
        MercenaryName,
        WorldSnapshots.Capture(World),
        HeroEquipment.Items.Select(pair => new EquippedItemSnapshot(pair.Key, pair.Value)).ToArray(),
        PassiveTree.Nodes
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
        new Dictionary<string, PassiveJewelKind>(Passives.SocketedJewels),
        Jewels.Capture(),
        CitadelDropCompensationGranted);

    private bool HasMythic(string catalogId) =>
        HeroEquipment.Items.Values.Concat(MercenaryEquipment.Items.Values).Concat(World.Storage.Items)
            .Concat(Management.SortingBag).Concat(Management.Recovery)
            .Any(item => item.LegendaryCatalogId == catalogId);

    public OfflineResult Advance(long realElapsedMilliseconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(realElapsedMilliseconds);
        long maximum = GameForWork.Core.Offline.OfflineTime.MaximumMilliseconds;
        long simulated = realElapsedMilliseconds > maximum / SimulationSpeed
            ? maximum
            : realElapsedMilliseconds * SimulationSpeed;
        Journey.AddElapsed(realElapsedMilliseconds, offline: false);
        OfflineResult result = AdvanceSimulated(simulated, offline: false, asyncPreparation: false);
        Journey.Synchronize(this);
        return result;
    }

    public OfflineResult AdvanceResponsive(long realElapsedMilliseconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(realElapsedMilliseconds);
        long maximum = GameForWork.Core.Offline.OfflineTime.MaximumMilliseconds;
        long simulated = realElapsedMilliseconds > maximum / SimulationSpeed
            ? maximum
            : realElapsedMilliseconds * SimulationSpeed;
        Journey.AddElapsed(realElapsedMilliseconds, offline: false);
        OfflineResult result = AdvanceSimulated(simulated, offline: false, asyncPreparation: true);
        Journey.Synchronize(this);
        return result;
    }

    public OfflineResult AdvanceOffline(long elapsedMilliseconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(elapsedMilliseconds);
        long effective = Math.Min(elapsedMilliseconds, GameForWork.Core.Offline.OfflineTime.MaximumMilliseconds);
        Journey.AddElapsed(effective, offline: true);
        OfflineResult result = AdvanceSimulated(effective, offline: true, asyncPreparation: false);
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

    private OfflineResult AdvanceSimulated(long simulatedMilliseconds, bool offline, bool asyncPreparation)
    {
        AdvanceTownSystems(simulatedMilliseconds);
        if (!Campaign.Completed)
        {
            _campaignSimulator.NodeResolved = () => { SynchronizeCampaignPassivePoints(); SynchronizeCampaignAscendancyPoints(); RefreshHeroBuild(); Journey.Synchronize(this); };
            CampaignAdvanceResult campaignResult = _campaignSimulator.Simulate(
                Campaign,
                World,
                Management,
                simulatedMilliseconds,
                Seed,
                offline,
                asyncPreparation);
            SynchronizeCampaignAscendancyPoints();
            SynchronizeCampaignPassivePoints();
            SimulationSequence = checked(SimulationSequence + campaignResult.NodesCompleted);
            if (campaignResult.NodesCompleted > 0) RefreshHeroBuild();
            return new OfflineResult(
                campaignResult.EffectiveMilliseconds,
                campaignResult.WasClamped,
                0,
                0,
                World.Teams.Select(team => new OfflineTeamSummary(
                    team.Kind,
                    team.MapsCompleted,
                    team.MapsFailed,
                    team.Queue.Count,
                    team.IsStopped,
                    team.StopReason)).ToArray(),
                campaignResult.FinalHash);
        }

        _simulator.MapStarted = (_, map, route) => { if (route == MapRoute.Warfront && !ExpeditionDirector.IsPractice(map)) Endgame.DiscoverWarfront(); };
        _simulator.PrepareMap = map => map with { AtlasSnapshot = Endgame.AtlasPassives.Order(StringComparer.Ordinal).ToArray() };
        _simulator.MapResolved = ResolveGameplay;
        OfflineResult result = _simulator.Simulate(
            World,
            simulatedMilliseconds,
            Seed,
            offline,
            asyncPreparation);
        EnsureWarfrontDiscoveryMap();
        SynchronizeWarfrontRouteCandidates();
        int ashes = World.Economy.TakeMemoryAshes();
        if (ashes > 0)
        {
            Passives.AddMemoryAshes(ashes);
        }


        if (World.Expedition.Reports.Any(report => report.Context.Contains("深渊监守者", StringComparison.Ordinal)))
            Town.RecordMilestone("town.milestone.abyss_warden", World.Economy);
        if (result.TotalMapsCompleted > 0 || ashes > 0)
            RefreshHeroTeamBuild();
        if (result.TotalMapsCompleted > 0) RefreshMercenaryPartyBuild();
        return result;
    }

    private void ResolveGameplay(TeamExpeditionState team, MapRunResult run, ulong seed, int baseStones, ExpeditionPolicy policy)
    {
        SimulationSequence = checked(SimulationSequence + 1);
        for (int i = 0; i < baseStones; i++) Management.AddDroppedSkillStone(seed ^ (uint)i ^ 0x703238baUL);
        bool special = EndgameState.IsCitadel(run.Map) || EndgameState.IsCitadelPractice(run.Map) ||
            EndgameState.IsBreakthroughTrial(run.Map) || ExpeditionDirector.IsPractice(run.Map);
        if (!special)
        {
            RewardLedger rewards = Rewards.Roll(run, seed);
            Mechanic? rewardMechanic = rewards.Encounters.Where(encounter => encounter.Kills > 0)
                .Select(encounter => (Mechanic?)encounter.Node.Gameplay?.Mechanic).FirstOrDefault(mechanic => mechanic is not null);
            IReadOnlySet<string>? themedSkills = rewardMechanic is null ? null : SkillDropCatalog.For(rewardMechanic.Value);
            bool pity = Endgame.RecordGameplay(rewards, Gameplay.Has(run.Map.AtlasSnapshot, "blue", 11));
            World.Economy.AddRewards(rewards.Stackables);
            World.AddMaps(rewards.Maps);
            LootProcessingResult processed = LootProcessor.Process(rewards.Equipment, World.Storage, World.Filter,
                policy.StorageFullBehavior);
            World.Economy.AddDispositionProceeds(processed.GoldGained, processed.IronScrapsGained);
            team.Backpack.Replace(team.Backpack.Items.Concat(processed.NotableItems));
            if (processed.ExpeditionMustStop) team.Stop("storage_full");
            for (int i = 0; i < rewards.Stackables.SkillStones - rewards.QualityStones - rewards.MutatedStones; i++) Management.AddDroppedSkillStone(seed ^ (uint)i ^ 0x703238ccUL, preferredDefinitions: themedSkills);
            for (int i = 0; i < rewards.QualityStones + rewards.MutatedStones; i++)
                Management.AddDroppedSkillStone(seed ^ (uint)i ^ 0x703238abUL, quality: 20,
                    mutated: i < rewards.MutatedStones, preferredDefinitions: themedSkills);
            if (pity && rewards.BlueTarget is { } target)
            {
                if (target == RewardPreference.SkillStones) Management.AddDroppedSkillStone(seed ^ 0xb1eeUL);
                else
                {
                    ItemInstance item = target == RewardPreference.Legendary
                        ? UniqueItems.Create("core.unique.blue_vow", run.Map.MonsterLevel, $"encounters-pity-{run.Map.InstanceId}")
                        : Rewards.Equipment(target, Math.Min(120, run.Map.MonsterLevel + 2), true, seed, $"encounters-pity-{run.Map.InstanceId}");
                    if (!World.Storage.TryStore(item)) Management.AddToRecovery(item, "苍誓保底奖励");
                }
            }
            if (run.Succeeded) RollBuildsJewels(run.Map, seed);
            if (rewards.Encounters.Any(e => e.Kills > 0)) Management.AddHistory(
                $"T{run.Map.Tier} {run.Route}：命能+{rewards.LifeForce} 战功+{rewards.Merit} 声望+{rewards.Reputation}；" +
                (run.Succeeded ? "已完成" : "保留已击败怪物与已兑现奖励；未完成/苍誓承诺不发放"));
            if (run.Succeeded) Endgame.RecordMapCompletion(run.Map, run.Route, seed);
        }
        if (!run.Succeeded) { Journey.Synchronize(this); return; }
        Management.AddSkillExperience(120); Town.AddActiveExperience(120);
        if (EndgameState.IsBreakthroughTrial(run.Map)) RecordFinalBreakthroughTrialVictory();
        if (EndgameState.IsCitadel(run.Map))
        {
            Endgame.RecordCitadelVictory();
            JewelInstance? jewel = JewelCatalog.RollCitadelLegendary(run.Map.MonsterLevel,
                seed, $"builds-jewel-{SimulationSequence:000000}-citadel");
            if (jewel is not null)
            {
                if (Jewels.TryAdd(jewel)) Management.AddHistory(
                    $"灰烬天垒掉落传奇珠宝：{jewel.DisplayName}（半径 {jewel.EffectiveRadius}）");
                else Management.AddHistory($"珠宝仓已满，{jewel.DisplayName}进入恢复记录。");
            }
        }
        EnsureWarfrontDiscoveryMap(); SynchronizeWarfrontRouteCandidates();
        RefreshHeroTeamBuild(); RefreshMercenaryPartyBuild(); Journey.Synchronize(this);
    }

    private void RollBuildsJewels(MapItem map, ulong seed)
    {
        int tier = Math.Clamp(map.Tier, 1, 20);
        int itemLevel = Math.Clamp(map.MonsterLevel, 1, 100);
        TryRoll(JewelCatalog.MapCompletionDropChanceBasisPoints(tier), seed ^ 0x30a11ceUL, itemLevel, "map");
        TryRoll(JewelCatalog.BossDropChanceBasisPoints(tier), seed ^ 0x30b055UL, Math.Min(100, itemLevel + 2), "boss");

        if (tier >= 6)
        {
            int memoryChance = tier <= 10 ? 15 : tier <= 15 ? 25 : 40;
            ulong legendaryRoll = Mix(seed ^ 0x30cafeUL);
            if (legendaryRoll % 10_000 < (ulong)memoryChance)
            {
                string[] pool = ["crimson_memory", "verdant_memory", "golden_memory", "azure_memory"];
                AddJewel(JewelCatalog.CreateLegendary(pool[(int)((legendaryRoll >> 16) % 4)], itemLevel,
                    $"builds-jewel-{SimulationSequence:000000}-memory"));
            }
        }

        void TryRoll(int chance, ulong rollSeed, int level, string source)
        {
            ulong roll = Mix(rollSeed);
            if (roll % 10_000 >= (ulong)chance) return;
            AddJewel(JewelCatalog.RollPrismatic(level, roll, $"builds-jewel-{SimulationSequence:000000}-{source}"));
        }
        void AddJewel(JewelInstance jewel)
        {
            if (Jewels.TryAdd(jewel)) Management.AddHistory($"获得珠宝：{jewel.DisplayName}（物品等级 {jewel.ItemLevel}）");
            else Management.AddHistory($"珠宝仓已满，{jewel.DisplayName}进入恢复记录。");
        }
    }

    private static ulong Mix(ulong value)
    {
        value += 0x9e3779b97f4a7c15UL;
        value = (value ^ (value >> 30)) * 0xbf58476d1ce4e5b9UL;
        value = (value ^ (value >> 27)) * 0x94d049bb133111ebUL;
        return value ^ (value >> 31);
    }

    public bool TryExchangeWarfrontSupply(RewardPreference preference)
    {
        if (preference is not (RewardPreference.Weapons or RewardPreference.Armor or RewardPreference.Jewelry or RewardPreference.Materials)) return false;
        return TryExchangeWarfrontSupply(Endgame.SupplyTier);
    }

    public bool TryExchangeWarfrontSupply(int tier)
    {
        if (!Endgame.WarfrontDiscovered || tier is < 1 or > 3 || tier > Endgame.SupplyTier) return false;
        int cost = tier * 50;
        if (Endgame.WarfrontMerit < cost) return false;
        ulong seed = Seed ^ (ulong)Endgame.GameplayOperationSequence * 0x9e3779b97f4a7c15UL;
        ItemInstance item = WarfrontRewards.Create(tier, seed, Endgame.LastWarfrontBaseId,
            $"resources-supply-{Endgame.GameplayOperationSequence}");
        if (!Endgame.TrySpendWarfrontMerit(cost)) return false;
        Endgame.RecordWarfrontBase(item.Base.StableId);
        if (!World.Storage.TryStore(item)) Management.AddToRecovery(item, "战功军需兑换：仓库已满");
        Management.AddHistory($"兑换 {tier} 阶战功基底 {item.Base.DisplayName}，消耗 {cost} 战功。");
        return true;
    }

    private void EnsureWarfrontDiscoveryMap()
    {
        if (Endgame.WarfrontGuaranteeIssued || !Endgame.CompletedTiers.Any(tier => tier >= 5)) return;
        int index = World.MapInventory.FindIndex(map => map.Tier >= 6);
        if (index < 0)
        {
            MapItem guaranteed = new MapItem($"monsters-warfront-discovery-{SimulationSequence:000000}", 6)
                .EnsureFormal(Seed ^ (ulong)SimulationSequence ^ 0x703237776172UL);
            World.AddMap(guaranteed);
            index = World.MapInventory.FindIndex(map => map.InstanceId == guaranteed.InstanceId);
        }
        if (index >= 0)
        {
            MapItem map = World.MapInventory[index];
            MapRoute[] routes = map.EffectiveRouteCandidates.Append(MapRoute.Warfront).Distinct().TakeLast(3).ToArray();
            World.MapInventory[index] = map with { RouteCandidates = routes };
            Endgame.MarkWarfrontGuaranteeIssued();
        }
    }

    private void SynchronizeWarfrontRouteCandidates()
    {
        if (!Endgame.WarfrontDiscovered) return;
        for (int index = 0; index < World.MapInventory.Count; index++)
        {
            MapItem map = World.MapInventory[index];
            if (map.Tier < 6 || map.EffectiveRouteCandidates.Contains(MapRoute.Warfront)) continue;
            byte[] hash = System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes($"{Seed}|{map.InstanceId}|monsters-warfront"));
            if (BitConverter.ToUInt32(hash, 0) % 10 != 0) continue;
            World.MapInventory[index] = map with
            {
                RouteCandidates = map.EffectiveRouteCandidates.Append(MapRoute.Warfront).Distinct().TakeLast(3).ToArray(),
            };
        }
    }

    private void AdvanceTownSystems(long simulatedMilliseconds)
    {
        Town.Advance(simulatedMilliseconds, World.Economy, World.AddMap);
        ApplyTownBuildingEffects();
    }

    private void ApplyTownBuildingEffects()
    {
        int storageCapacity = Town.Level(BuildingKind.Storage) switch { 1 => 100, 2 => 150, 3 => 225, _ => 325 };
        World.Storage.TrySetCapacity(Math.Max(storageCapacity, World.Storage.Count));
        World.Teleporter.TrySetLevel(Town.Level(BuildingKind.Teleporter));
    }

    private void SynchronizeCampaignAscendancyPoints()
    {
        if (Campaign.CompletedNodeIds.Contains("core.campaign.act3.node6"))
            Endgame.AwardCampaignAscendancyPoints(3);
        if (Campaign.CompletedNodeIds.Contains("core.campaign.act5.node6"))
            Endgame.AwardCampaignAscendancyPoints(5);
    }

    public bool TryUpgradeTownBuilding(BuildingKind kind, out string message) =>
        Town.TryStartUpgrade(kind, World.Economy, out message);

    public void SetTownPolicy(TownPolicy policy) => Town.SetPolicy(policy);

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
        bool replayed = new CampaignSimulator().Replay(
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
        SkillLinkConfiguration? activeLink = Management.SkillLinks.FirstOrDefault(
            link => link.ActiveStoneInstanceId == active.InstanceId);
        if (activeLink is null)
        {
            HeavyStrikeSupports = SkillSupport.None;
            RefreshHeroBuild();
            return;
        }
        string[] supportIds = SupportDefinitionIds(supports)
            .Select(definition => Management.SkillStones.FirstOrDefault(item => item.DefinitionId == definition)?.InstanceId)
            .Where(id => id is not null)
            .Cast<string>()
            .ToArray();
        SkillChainDefinition? chain = GetSkillChains().FirstOrDefault(item => item.StableId == activeLink.ChainId);
        Management.ReplaceSupports(active.InstanceId, supportIds, chain?.SupportCapacity ?? 5);
        HeavyStrikeSupports = SupportsFor("core.skill_stone.heavy_strike");
        RefreshHeroBuild();
    }

    public void SyncHeavyStrikeFromSkillStones()
    {
        HeavyStrikeSupports = SupportsFor("core.skill_stone.heavy_strike");
        RefreshHeroBuild();
    }

    public IReadOnlyList<SkillChainDefinition> GetSkillChains() => SkillChainRules.Build(HeroEquipment);

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
        SkillChainDefinition? chain = GetSkillChains().FirstOrDefault(item => item.StableId == link?.ChainId);
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
            RecordJourneyEvent(JourneyEvent.ConfiguredSkillTarget);
        }
        return changed;
    }

    public bool CanSwitchSkillScheme => Campaign.Completed && World.Hero.ActiveMap is null;

    public void SaveSkillScheme(SkillSchemeKind kind) => Management.SaveSkillScheme(kind);

    public SchemeSwitchResult SwitchSkillScheme(SkillSchemeKind kind)
    {
        if (!CanSwitchSkillScheme)
        {
            return new SchemeSwitchResult(false, 0, 0, "只能在主线完成后的城镇或主角队空闲时切换方案。");
        }
        SchemeSwitchResult result = Management.SwitchSkillScheme(kind, GetSkillChains());
        if (result.Succeeded)
        {
            HeavyStrikeSupports = SupportsFor("core.skill_stone.heavy_strike");
            RefreshHeroBuild();
        }
        return result;
    }

    public void AssignExpedition(
        ExpeditionTeamKind teamKind,
        ExpeditionTarget target,
        DispatchMode mode,
        int requestedRuns = 1)
    {
        TeamExpeditionState team = Team(teamKind);
        ReturnQueuedMaps(team);
        World.Expedition.Assign(teamKind, target, mode, requestedRuns);
        team.ResumeForNewDispatch();
        World.Expedition.PrepareNext(World, team);
        if (team.Queue.Maps.FirstOrDefault() is { } queued)
            team.Queue.TryReplaceAt(0, queued with
            {
                AtlasSnapshot = Endgame.AtlasPassives.Order(StringComparer.Ordinal).ToArray(),
            });
        Journey.Synchronize(this);
    }

    public BossChallengeAvailability GetBossChallengeAvailability(ExpeditionTarget target) => target switch
    {
        ExpeditionTarget.AbyssWarden => new(true, World.Expedition.AbyssWardenTickets,
            "消耗 1 张深渊监守者门票"),
        ExpeditionTarget.AbyssWardenPractice => new(true, int.MaxValue, "练习模式，不消耗门票且不获得奖励"),
        ExpeditionTarget.AshenCitadel => new(true, Endgame.CitadelTickets, "消耗 1 张灰烬天垒门票"),
        ExpeditionTarget.AshenCitadelPractice => new(true, int.MaxValue, "练习模式，不消耗门票且不获得奖励"),
        ExpeditionTarget.FinalBreakthrough => new(World.Hero.Progression.Level >= 100 && !Endgame.FinalBreakthroughCompleted,
            World.Hero.Progression.Level >= 100 && !Endgame.FinalBreakthroughCompleted ? 1 : 0,
            Endgame.FinalBreakthroughCompleted ? "已经完成最终突破" : "需要角色达到 100 级"),
        _ => new(false, 0, "不是 Boss 挑战"),
    };

    public bool AssignBossChallenge(ExpeditionTarget target, DispatchMode mode, int requestedRuns = 1)
    {
        if (!ExpeditionDirector.IsBossTarget(target)) return false;
        TeamExpeditionState team = World.Hero;
        BossChallengeAvailability availability = GetBossChallengeAvailability(target);
        if (!availability.Unlocked || availability.AvailableRuns == 0 || team.ActiveMap is not null || team.Queue.Count > 0)
            return false;
        World.Expedition.BossMapFactory = CreateBossMap;
        World.Expedition.Assign(ExpeditionTeamKind.Hero, target, mode, requestedRuns);
        team.ResumeForNewDispatch();
        World.Expedition.PrepareNext(World, team);
        Journey.Synchronize(this);
        return team.Queue.Count > 0 || team.ActiveMap is not null;
    }

    public void RecordJourneyEvent(JourneyEvent journeyEvent)
    {
        Journey.Record(journeyEvent);
        Journey.Synchronize(this);
    }

    public void CancelExpedition(ExpeditionTeamKind teamKind)
    {
        TeamExpeditionState team = Team(teamKind);
        ReturnQueuedMaps(team);
        World.Expedition.Cancel(teamKind);
        team.Stop("manual_stop");
    }

    public bool AbandonExpedition(ExpeditionTeamKind teamKind)
    {
        TeamExpeditionState team = Team(teamKind);
        ReturnQueuedMaps(team);
        bool abandoned = team.AbandonActiveMap() is not null;
        World.Expedition.Cancel(teamKind, "abandoned");
        team.Stop("abandoned");
        return abandoned;
    }

    private BossScheduleResult CreateBossMap(ExpeditionTarget target)
    {
        SimulationSequence++;
        return target switch
        {
            ExpeditionTarget.AshenCitadel when !Endgame.TryConsumeCitadelTicket() => new(null, "citadel_ticket_missing"),
            ExpeditionTarget.AshenCitadel => new(new MapItem(
                $"{EndgameState.CitadelMapPrefix}{SimulationSequence:000000}", 20,
                RouteCandidates: [MapRoute.Abyss], SelectedRoute: MapRoute.Abyss,
                AtlasSnapshot: Endgame.AtlasPassives.Order(StringComparer.Ordinal).ToArray())),
            ExpeditionTarget.AshenCitadelPractice => new(new MapItem(
                $"{EndgameState.CitadelPracticeMapPrefix}{SimulationSequence:000000}", 20,
                RouteCandidates: [MapRoute.Abyss], SelectedRoute: MapRoute.Abyss,
                AtlasSnapshot: Endgame.AtlasPassives.Order(StringComparer.Ordinal).ToArray())),
            ExpeditionTarget.FinalBreakthrough when World.Hero.Progression.Level < 100 => new(null, "level_100_required"),
            ExpeditionTarget.FinalBreakthrough when Endgame.FinalBreakthroughCompleted => new(null, "breakthrough_completed"),
            ExpeditionTarget.FinalBreakthrough => new(new MapItem(
                $"{EndgameState.BreakthroughMapPrefix}{SimulationSequence:000000}", 16,
                RouteCandidates: [MapRoute.Safe], SelectedRoute: MapRoute.Safe,
                AtlasSnapshot: Endgame.AtlasPassives.Order(StringComparer.Ordinal).ToArray())),
            _ => new(null),
        };
    }

    public void SetHeroAi(HeroAiConfiguration configuration)
    {
        HeroAi = configuration.Validate();
        RefreshHeroTeamBuild();
    }

    public bool TryAllocatePassive(string stableId)
    {
        bool changed = Passives.TryAllocate(stableId, World.Hero.Progression.EarnedPassivePoints, Jewels);
        if (changed)
        {
            RefreshHeroBuild();
            Journey.Synchronize(this);
        }

        return changed;
    }

    public bool TryAllocatePassivePath(string stableId)
    {
        bool changed = Passives.TryAllocatePath(stableId, World.Hero.Progression.EarnedPassivePoints, Jewels);
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
        bool changed = Passives.TryRefund(stableId, Jewels);
        if (changed)
        {
            Jewels.TryUnsocket(stableId);
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
            Jewels.UnsocketAll();
            RefreshHeroBuild();
        }

        return changed;
    }

    public bool TryEquipFromStorage(int storageIndex, EquipmentSlot slot)
    {
        return new ItemCommandService(this).TryEquip(ItemContainerKind.Storage, storageIndex, slot).Succeeded;
    }

    public void NotifyEquipmentChanged(CharacterKind character = CharacterKind.Hero)
    {
        if (character == CharacterKind.Hero)
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

        WorkshopResult result = TownWorkshop.CraftPhysicalIncrease(World.Economy, weapon);
        if (result.Succeeded)
        {
            HeroEquipment.TryEquip(EquipmentSlot.MainHand, result.Item!);
            RefreshHeroBuild();
            RecordJourneyEvent(JourneyEvent.CraftedItem);
        }

        return result;
    }

    public WorkshopPreview CraftStorageItem(int storageIndex, WorkshopRecipe recipe)
    {
        if (storageIndex < 0 || storageIndex >= World.Storage.Items.Count)
        {
            return new WorkshopPreview(false, "item_required", null, 0, 0, "请先选择仓库物品。");
        }

        return new ItemCommandService(this).Craft(ItemContainerKind.Storage, storageIndex, recipe);
    }

    public bool TryExchangeLegendary(string? stableId = null)
    {
        if (World.Storage.IsFull)
        {
            return false;
        }
        string selected = stableId ?? Economy.LegendaryDrops.ExchangePool[0].StableId;
        return World.Economy.TryExchangeLegendary(selected, out ItemInstance? item) && item is not null && World.Storage.TryStore(item);
    }

    public int EnqueueInventoryMaps()
    {
        int moved = 0;
        for (int index = World.MapInventory.Count - 1; index >= 0; index--)
        {
            TeamExpeditionState team = (moved & 1) == 0 ? World.Hero : World.Mercenaries;
            MapItem map = World.MapInventory[index].EnsureFormal(Seed ^ (ulong)SimulationSequence ^ (ulong)index);
            map = map with { AtlasSnapshot = Endgame.AtlasPassives.Order(StringComparer.Ordinal).ToArray(), IsManualPriority = true };
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
        MapItem map = World.MapInventory[mapIndex].EnsureFormal(Seed ^ (ulong)mapIndex);
        if (!map.EffectiveRouteCandidates.Contains(route)) return false;
        World.MapInventory[mapIndex] = map with { SelectedRoute = route };
        return true;
    }

    public bool TrySetMapLocked(int mapIndex, bool locked)
    {
        if (mapIndex < 0 || mapIndex >= World.MapInventory.Count) return false;
        World.MapInventory[mapIndex] = World.MapInventory[mapIndex] with { IsLocked = locked };
        return true;
    }

    public MapCraftResult CraftMap(int mapIndex, MapCraftOperation operation)
    {
        if (mapIndex < 0 || mapIndex >= World.MapInventory.Count)
            return new(false, new MapItem("invalid", 1), default, 0, "map_required");
        ulong seed = Seed ^ (ulong)SimulationSequence++ ^ (ulong)(mapIndex + 1) * 0x9e3779b97f4a7c15UL;
        MapCraftResult result = MapCrafting.Apply(World.Economy, World.MapInventory[mapIndex], operation,
            seed, World.MaximumUnlockedMapTier,
            AtlasEffects.Has(Endgame.AtlasPassives.ToArray(), "atlas.atlas.craft.02") ? 10 : 5);
        if (result.Succeeded && result.Destroyed) World.MapInventory.RemoveAt(mapIndex);
        else if (result.Succeeded) World.MapInventory[mapIndex] = result.Map!;
        return result;
    }

    public MapBatchResult BatchCraftMaps(MapBatchRule requestedRule, MapFilter? requestedFilter = null)
    {
        MapBatchRule rule = requestedRule.Validate();
        MapFilter filter = (requestedFilter ?? World.MapCraftFilter).Validate();
        World.MapCraftFilter = filter;
        World.MapCraftRule = rule;
        string[] selectedIds = filter.Select(World.MapInventory).Where(map => !map.IsProtected)
            .Select(map => map.InstanceId).ToArray();
        int processed = 0, completed = 0, skipped = 0, destroyed = 0, sold = 0, saleGold = 0, spent = 0;
        bool stopped = false;
        foreach (string selectedId in selectedIds)
        {
            int index = World.MapInventory.FindIndex(map => map.InstanceId == selectedId);
            if (index < 0) continue;
            MapItem map = World.MapInventory[index].EnsureFormal(Seed ^ (ulong)index);
            bool failed = false, mapDestroyed = false;

            bool Apply(MapCraftOperation operation)
            {
                ulong operationSeed = Seed ^ (ulong)SimulationSequence++ ^ (ulong)(index + 1) * 0x517cc1b727220a95UL;
                MapCraftResult result = MapCrafting.Apply(World.Economy, map, operation, operationSeed, World.MaximumUnlockedMapTier,
                    AtlasEffects.Has(Endgame.AtlasPassives.ToArray(), "atlas.atlas.craft.02") ? 10 : 5);
                if (!result.Succeeded) return false;
                spent += result.Cost;
                if (result.Destroyed) { mapDestroyed = true; return true; }
                map = result.Map!;
                return true;
            }

            while (!failed && !mapDestroyed && map.Quality < rule.MinimumQuality)
                if (!Apply(MapCraftOperation.PolishQuality)) failed = true;
            if (!failed && map.Rarity < rule.TargetRarity)
            {
                MapCraftOperation upgrade = rule.TargetRarity == MapRarity.Rare
                    ? MapCraftOperation.AlchemicalRare : MapCraftOperation.AwakenMagic;
                if (!Apply(upgrade)) failed = true;
            }
            int rerolls = 0;
            while (!failed && !mapDestroyed && map.Rarity == MapRarity.Rare && rerolls++ < 1_000 &&
                   (rule.ExcludedAffixes?.Any(kind => map.EffectiveAffixes.Any(affix => affix.Kind == kind)) ?? false))
            {
                if (!Apply(MapCraftOperation.ChaosReroll)) failed = true;
            }
            while (!failed && !mapDestroyed && rule.FillAffixes && map.Rarity == MapRarity.Rare && map.EffectiveAffixes.Count < 6)
                if (!Apply(MapCraftOperation.ExaltedAdd)) failed = true;
            bool excludedAtEnd = rule.ExcludedAffixes?.Any(kind => map.EffectiveAffixes.Any(affix => affix.Kind == kind)) ?? false;
            if (!failed && !mapDestroyed && excludedAtEnd)
            {
                if (rule.ExcludedAffixBehavior == BatchFailureBehavior.Sell)
                {
                    saleGold += MapGenerationRules.SaleGold(map);
                    World.MapInventory.RemoveAt(index);
                    sold++;
                }
                else
                {
                    World.MapInventory[index] = map;
                    skipped++;
                }
                processed++;
                continue;
            }
            if (!failed && !mapDestroyed && rule.Corrupt && !map.IsCorrupted && !Apply(MapCraftOperation.Corrupt)) failed = true;

            processed++;
            if (mapDestroyed) { World.MapInventory.RemoveAt(index); destroyed++; }
            else World.MapInventory[index] = map;
            if (!failed) completed++;
            else
            {
                skipped++;
                stopped = true;
                break;
            }
        }
        if (saleGold > 0)
        {
            saleGold = saleGold * (10_000 + AtlasEffects.MapSaleIncrease(Endgame.AtlasPassives.ToArray())) / 10_000;
            World.Economy.AddDispositionProceeds(saleGold, 0);
        }
        return new(processed, completed, skipped, spent, stopped,
            $"处理 {processed} 张，完成 {completed} 张，出售 {sold} 张（{saleGold} 金币），腐化摧毁 {destroyed} 张，未达目标保留 {skipped} 张，消耗金属 {spent}。" +
            (stopped ? " 材料不足，批处理已停止。" : string.Empty));
    }

    public (int Sold, int Gold) SellMaps(MapFilter requestedFilter)
    {
        MapFilter filter = requestedFilter.Validate();
        World.MapSaleFilter = filter;
        MapItem[] selected = filter.Select(World.MapInventory).Where(map => !map.IsProtected).ToArray();
        int gold = selected.Sum(MapGenerationRules.SaleGold);
        gold = gold * (10_000 + AtlasEffects.MapSaleIncrease(Endgame.AtlasPassives.ToArray())) / 10_000;
        HashSet<string> ids = selected.Select(map => map.InstanceId).ToHashSet(StringComparer.Ordinal);
        World.MapInventory.RemoveAll(map => ids.Contains(map.InstanceId));
        World.Economy.AddDispositionProceeds(gold, 0);
        return (selected.Length, gold);
    }

    public void SetMapAutoSellFilter(MapFilter filter) => World.AutoSellMapFilter = filter.Validate();

    public void SetExpeditionPolicy(ExpeditionTeamKind kind, ExpeditionPolicy policy) => Team(kind).ApplyPolicy(policy);

    public bool RecordFinalBreakthroughTrialVictory()
    {
        if (!Endgame.TryCompleteFinalBreakthrough(World.Hero.Progression.Level, trialWon: true)) return false;
        World.UnlockFinalMapTiers();
        World.Hero.Progression.UnlockFinalBreakthrough();
        return true;
    }

    public bool TryChallengeFinalBreakthrough()
        => AssignBossChallenge(ExpeditionTarget.FinalBreakthrough, DispatchMode.Once);

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

    public BuildSummary GetBuildSummary() => BuildSummaryRules.Calculate(this);

    public IReadOnlyList<SkillConfiguration> GetPreviewSkillCandidates() => BuildActiveSkills()
        .Where(configuration => ActiveSkillCatalog.ActiveForSkill(configuration.SkillId).Combat.Capabilities
            .HasFlag(SkillCapability.Damage))
        .Where(configuration => ActiveSkillCatalog.ActiveForSkill(configuration.SkillId).Combat.Role is not
            (SkillRole.Reservation or SkillRole.WarCry or SkillRole.Guard or SkillRole.Movement))
        .OrderBy(configuration => configuration.Priority)
        .ThenBy(configuration => configuration.SkillId, StringComparer.Ordinal)
        .ToArray();

    public SkillConfiguration? GetPreviewSkill()
    {
        IReadOnlyList<SkillConfiguration> candidates = GetPreviewSkillCandidates();
        return candidates.FirstOrDefault(configuration =>
                   configuration.StoneInstanceId == Management.PreviewSkillStoneInstanceId) ??
               candidates.FirstOrDefault();
    }

    public bool SelectPreviewSkill(string stoneInstanceId) => Management.SelectPreviewSkill(stoneInstanceId);

    public EquipmentComparison CompareHeroEquipment(ItemInstance candidate, EquipmentSlot slot)
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
            ClassCatalog.Get(Player.BaseClass).StartingAttributes,
            hypothetical,
            Passives,
            new SkillConfiguration(SkillIds.HeavyStrike, HeavyStrikeSupports),
            Jewels);
        CombatPreview currentPreview = GetCombatPreview();
        CombatPreview proposedPreview = Preview(proposed);
        EquipmentSummary current = HeroEquipment.CalculateSummary();
        EquipmentSummary next = hypothetical.CalculateSummary();
        SkillCapacityResult capacity = SkillCapacityRules.Validate(
            [new SkillConfiguration(SkillIds.HeavyStrike, HeavyStrikeSupports)], next);
        ItemInstance? currentItem = HeroEquipment.Items.GetValueOrDefault(slot);
        SkillLinkConfiguration? socketGroup = Management.SkillLinks.FirstOrDefault(link =>
            link.ChainId == GameForWork.Core.Skills.SocketGroupIds.For(slot));
        string[] installed = socketGroup?.SocketStoneInstanceIds?.Where(id => !string.IsNullOrEmpty(id)).Cast<string>().ToArray() ?? [];
        int retainedCount = Math.Min(candidate.LinkedSocketCount, installed.Length);
        string NameOf(string id) => Management.SkillStones.FirstOrDefault(stone => stone.InstanceId == id)?.Definition.DisplayName ?? id;
        return new EquipmentComparison(
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
            ClassCatalog.Get(Player.BaseClass).StartingAttributes,
            HeroEquipment,
            Passives,
            new SkillConfiguration(SkillIds.HeavyStrike, HeavyStrikeSupports),
            Jewels);
        return build with
        {
            Sheet = WarriorAscendancyRules.ApplySheet(build.Sheet, AscendancyProfile(), build.Equipment.ShieldArmor),
        };
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

    private CombatProfile AscendancyProfile() => new(Endgame.SelectedAscendancy,
        Endgame.AscendancyPassives.Order(StringComparer.Ordinal).ToArray(), Endgame.CombatConfiguration.Snapshot());

    private void RefreshMercenaryBuild()
    {
        RefreshMercenaryPartyBuild();
    }

    public bool TryAllocateAtlasPassive(string stableId) => Endgame.TryPurchaseAtlas(
        stableId, World.Economy, Endgame.WarfrontDiscovered);

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

    private void SynchronizeCampaignPassivePoints() =>
        World.Hero.Progression.SynchronizeStoryPassivePoints(Campaign.CompletedNodeIds.Count);

    public bool TrySocketBuildsJewel(string stableId, string instanceId, out string reason)
    {
        if (!Passives.Allocated.Contains(stableId))
        {
            reason = "需要先分配该记忆棱孔。";
            return false;
        }
        JewelStateSnapshot previous = Jewels.Capture();
        bool changed = Jewels.TrySocket(stableId, instanceId, World.Hero.Progression.Level, out reason);
        if (changed && !Passives.IsValidAllocation(Jewels))
        {
            Jewels.RestoreSnapshot(previous);
            reason = "更换或移动该珠宝会使已配置天赋失去合法连接。";
            return false;
        }
        if (changed) RefreshHeroBuild();
        return changed;
    }

    public bool TryUnsocketBuildsJewel(string stableId)
    {
        if (!Passives.IsValidAllocation(Jewels, stableId)) return false;
        bool changed = Jewels.TryUnsocket(stableId);
        if (changed) RefreshHeroBuild();
        return changed;
    }

    public bool TryCraftBuildsJewel(string instanceId, JewelCraftOperation operation, out string message)
    {
        JewelInstance? jewel = Jewels.Items.FirstOrDefault(item => item.InstanceId == instanceId);
        if (jewel is null)
        {
            message = "珠宝不存在。";
            return false;
        }
        MetalCurrencyKind currency = JewelCatalog.CraftCurrency(operation);
        if (World.Economy.MetalAmount(currency) <= 0)
        {
            message = $"{MetalCurrencies.Get(currency).DisplayName}不足。";
            return false;
        }
        ulong operationSeed = Seed ^ (ulong)Endgame.GameplayOperationSequence * 0x9e3779b97f4a7c15UL ^ StableHash(instanceId);
        (bool succeeded, string result, JewelInstance? crafted, bool destroyed) =
            JewelCatalog.Craft(jewel, operation, operationSeed);
        if (!succeeded)
        {
            message = result;
            return false;
        }
        JewelStateSnapshot beforeCraft = Jewels.Capture();
        bool changed = destroyed ? Jewels.TryRemove(instanceId) : crafted is not null && Jewels.TryReplace(crafted);
        if (!changed) throw new InvalidOperationException("Jewel crafting result could not be persisted.");
        if (!Passives.IsValidAllocation(Jewels))
        {
            Jewels.RestoreSnapshot(beforeCraft);
            message = "该半径会使已配置天赋失去合法来源，本次重投已取消。";
            return false;
        }
        if (!World.Economy.TrySpendMetal(currency, 1))
            throw new InvalidOperationException("Validated jewel crafting currency could not be spent.");
        Endgame.CompleteGameplayOperation();
        RefreshHeroBuild();
        Management.AddHistory($"珠宝加工：{result}");
        message = result;
        return true;
    }

    public bool TryDismantleBuildsJewel(string instanceId, bool confirmed, out string message)
    {
        JewelInstance? jewel = Jewels.Items.FirstOrDefault(item => item.InstanceId == instanceId);
        if (jewel is null)
        {
            message = "珠宝不存在。";
            return false;
        }
        if (Jewels.Socketed.Values.Contains(instanceId, StringComparer.Ordinal))
        {
            message = "已镶嵌的珠宝不能分解，请先从天赋树取下。";
            return false;
        }
        if (jewel.Rarity >= JewelRarity.Rare && !confirmed)
        {
            message = $"分解{JewelCatalog.RarityName(jewel.Rarity)}珠宝需要确认。";
            return false;
        }
        int scraps = JewelCatalog.DismantleYield(jewel.Rarity);
        if (!Jewels.TryRemove(instanceId)) throw new InvalidOperationException("Validated jewel could not be removed.");
        World.Economy.AddDispositionProceeds(0, scraps);
        Management.AddHistory($"珠宝分解：{jewel.DisplayName}，获得 {scraps} 铁屑。");
        RefreshHeroBuild();
        message = $"已分解 {jewel.DisplayName}，获得 {scraps} 铁屑。";
        return true;
    }

    public bool TryDismantleBuildsJewels(JewelRarity maximumRarity, bool confirmed, out string message)
    {
        HashSet<string> socketed = Jewels.Socketed.Values.ToHashSet(StringComparer.Ordinal);
        JewelInstance[] targets = Jewels.Items
            .Where(item => item.Rarity <= maximumRarity && !socketed.Contains(item.InstanceId))
            .OrderBy(item => item.InstanceId, StringComparer.Ordinal)
            .ToArray();
        if (targets.Length == 0)
        {
            message = "没有符合条件且未镶嵌的珠宝。";
            return false;
        }
        if (!confirmed)
        {
            int previewScraps = targets.Sum(item => JewelCatalog.DismantleYield(item.Rarity));
            message = $"将分解 {targets.Length} 枚未镶嵌珠宝，获得 {previewScraps} 铁屑。";
            return false;
        }
        int scraps = targets.Sum(item => JewelCatalog.DismantleYield(item.Rarity));
        foreach (JewelInstance target in targets)
            if (!Jewels.TryRemove(target.InstanceId))
                throw new InvalidOperationException("Validated jewel batch could not be removed.");
        World.Economy.AddDispositionProceeds(0, scraps);
        Management.AddHistory($"批量分解珠宝：{targets.Length} 枚，获得 {scraps} 铁屑。");
        RefreshHeroBuild();
        message = $"已分解 {targets.Length} 枚珠宝，获得 {scraps} 铁屑。";
        return true;
    }

    private static ulong StableHash(string value)
    {
        ulong hash = 14695981039346656037UL;
        foreach (char character in value) hash = unchecked((hash ^ character) * 1099511628211UL);
        return hash;
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

    public bool TrySelectAscendancy(Ascendancy ascendancy)
    {
        if (!ClassCatalog.Allows(Player.BaseClass, ascendancy) || !WarriorAscendancyCatalog.IsImplemented(ascendancy))
            return false;
        bool changed = Endgame.TrySelectAscendancy(ascendancy);
        if (changed) RefreshHeroBuild();
        return changed;
    }
    public bool CanConfigureAscendancyCombat => Campaign.Completed && World.Hero.ActiveMap is null;
    public bool TryConfigureAscendancyCombat(CombatConfiguration configuration)
    {
        if (!CanConfigureAscendancyCombat || !Endgame.ConfigureCombat(configuration)) return false;
        RefreshHeroBuild();
        return true;
    }

    public bool TryRefundAscendancyPassive(string stableId)
    {
        AscendancyNode node = WarriorAscendancyCatalog.Get(stableId);
        int cost = node.Kind == NodeKind.Core ? 10_000 : 2_000;
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
            (changePath && Endgame.SelectedAscendancy == Ascendancy.None) ||
            !World.Economy.TrySpendGold(cost)) return false;
        Endgame.ResetAscendancy(changePath);
        RefreshHeroBuild();
        return true;
    }

    public bool TryChallengeCitadel()
        => AssignBossChallenge(ExpeditionTarget.AshenCitadel, DispatchMode.Once);

    public bool TryPracticeCitadel()
        => AssignBossChallenge(ExpeditionTarget.AshenCitadelPractice, DispatchMode.Once);

    private void RefreshMercenaryPartyBuild() => World.Mercenaries.UpdateBuild(
        Town.BuildMercenaryParty(World.Mercenaries.Progression.Level));

    private static TeamBuild ToTeamBuild(
        AssembledCharacterBuild build,
        SkillSupport supports,
        HeroAiConfiguration ai,
        IReadOnlyList<SkillConfiguration>? activeSkills = null,
        CombatProfile? ascendancy = null) => new TeamBuild(
        build.Sheet,
        build.EffectiveWeapon,
        new SkillConfiguration(SkillIds.HeavyStrike, supports),
        build.FlatAccuracy,
        checked(build.IncreasedAttackDamageBasisPoints + build.Sheet.AttackDamageIncreaseFromPhysique().Value),
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
            new SkillConfiguration(SkillIds.HeavyStrike, supports),
            new SkillConfiguration(SkillIds.EarthCleave, SkillSupport.IncreasedArea),
            new SkillConfiguration(SkillIds.SpiritBlade, SkillSupport.Chain),
        ],
        Flasks: build.Flasks,
        HasShield: build.Equipment.HasShield,
        BlockChanceBasisPoints: checked(build.Equipment.BaseBlockChanceBasisPoints +
            build.Equipment.Modifiers.BlockChanceBasisPoints + (build.Passives.Advanced?.BlockChanceBasisPoints ?? 0)),
        Ascendancy: ascendancy,
        HasUsableWeapon: build.HasUsableWeapon,
        PassiveProfile: build.Passives.Advanced,
        CriticalMultiplierBasisPoints: checked(15_000 + build.IncreasedCriticalMultiplierBasisPoints),
        AlwaysHit: build.Passives.Advanced?.ResoluteTechnique == true ||
            MasteryRuntime.AlwaysHits(build.Passives.Advanced ?? PassiveModifiers.Empty, SkillTag.Attack),
        CannotCrit: build.Passives.Advanced?.ResoluteTechnique == true ||
            MasteryRuntime.CannotCrit(build.Passives.Advanced ?? PassiveModifiers.Empty),
        IncreasedWarCryCooldownRecoveryBasisPoints: build.Passives.IncreasedWarCryCooldownRecoveryBasisPoints,
        IncreasedWarCryRangeBasisPoints: build.Passives.IncreasedWarCryRangeBasisPoints,
        VirtueViceLoadout: build.VirtueViceLoadout,
        MoreAttackDamageBasisPoints: build.MoreAttackDamageBasisPoints,
        MoreSpellDamageBasisPoints: build.MoreSpellDamageBasisPoints,
        MoreDamageOverTimeBasisPoints: build.MoreDamageOverTimeBasisPoints,
        IncreasedActionSpeedBasisPoints: build.IncreasedActionSpeedBasisPoints,
        InstantLifeLeechBasisPoints: build.InstantLifeLeechBasisPoints,
        LocalWeaponStats: build.Equipment.LocalWeapon,
        IncreasedSpellDamageBasisPoints: build.IncreasedSpellDamageBasisPoints,
        IncreasedAttackSpeedBasisPoints: build.IncreasedAttackSpeedBasisPoints,
        MoreElementalDamageBasisPoints: build.MoreElementalDamageBasisPoints,
        MoreVoidDamageBasisPoints: build.MoreVoidDamageBasisPoints,
        MoreRareBossDamageBasisPoints: build.MoreRareBossDamageBasisPoints,
        HasOffHand: build.HasOffHand,
        CombatEquipment: build.CombatEquipment) with
        {
            AiSummary = $"{ai.Preset} · {(ai.MatchMode == AiRuleMatchMode.All ? "全部满足" : "任一满足")}：" +
                $"敌人≥{ai.MinimumEnemyCount}、稀有度 {ai.EnemyRarity}、距离≤{ai.MaximumEnemyDistance}、" +
                $"威胁等级≥{ai.DangerThreshold}{(ai.BossPriority ? "、Boss优先" : string.Empty)}；" +
                $"生命低于 {ai.LifeFlaskThresholdBasisPoints / 100}% 使用药剂。"
        };

    private IReadOnlyList<SkillConfiguration> BuildActiveSkills() => Management.SkillLinks
        .Where(link => !string.IsNullOrEmpty(link.ChainId) && !string.IsNullOrEmpty(link.ActiveStoneInstanceId))
        .OrderBy(link => link.Priority)
        .Select(link => (Link: link, Stone: Management.SkillStones.Single(stone => stone.InstanceId == link.ActiveStoneInstanceId)))
        .Where(entry => ActiveRole(entry.Stone.DefinitionId) != SkillRole.Reservation || entry.Link.ReservationEnabled)
        .Select(entry => new SkillConfiguration(
            ToCombatSkillId(entry.Stone.DefinitionId),
            SupportsFor(entry.Stone.DefinitionId),
            entry.Link.Priority,
            entry.Link.AiRule ?? GlobalSkillRule(),
            Math.Clamp(entry.Stone.Level + (entry.Stone.Mutated ? 1 : 0) +
                _heroBuild.Equipment.Modifiers.Value(ItemModifierKind.ActiveSkillGemLevels) +
                _heroBuild.Equipment.Modifiers.Value(ItemModifierKind.AllActiveSkillGemLevels), 1, 40),
            entry.Stone.InstanceId,
            ArchetypesSupportsFor(entry.Stone.DefinitionId),
            entry.Stone.Quality,
            BuildsSupportsFor(entry.Stone.DefinitionId),
            BuildsSupportLinksFor(entry.Stone.DefinitionId)))
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

    private IReadOnlyList<SupportMechanic> ArchetypesSupportsFor(string activeDefinitionId)
    {
        SkillStoneInstance? active = Management.SkillStones.FirstOrDefault(item => item.DefinitionId == activeDefinitionId);
        SkillLinkConfiguration? link = active is null
            ? null
            : Management.SkillLinks.FirstOrDefault(item => item.ActiveStoneInstanceId == active.InstanceId);
        return (link?.SupportStoneInstanceIds ?? [])
            .Select(id => Management.SkillStones.Single(item => item.InstanceId == id).Definition.ArchetypeSupport)
            .Where(value => value != SupportMechanic.None)
            .Distinct()
            .ToArray();
    }

    private IReadOnlyList<string> BuildsSupportsFor(string activeDefinitionId)
    {
        SkillStoneInstance? active = Management.SkillStones.FirstOrDefault(item => item.DefinitionId == activeDefinitionId);
        SkillLinkConfiguration? link = active is null
            ? null
            : Management.SkillLinks.FirstOrDefault(item => item.ActiveStoneInstanceId == active.InstanceId);
        return (link?.SupportStoneInstanceIds ?? [])
            .Select(id => Management.SkillStones.Single(item => item.InstanceId == id).Definition.SupportId)
            .Where(value => !string.IsNullOrEmpty(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private IReadOnlyList<LinkedSupport> BuildsSupportLinksFor(string activeDefinitionId)
    {
        SkillStoneInstance? active = Management.SkillStones.FirstOrDefault(item => item.DefinitionId == activeDefinitionId);
        SkillLinkConfiguration? link = active is null
            ? null
            : Management.SkillLinks.FirstOrDefault(item => item.ActiveStoneInstanceId == active.InstanceId);
        return (link?.SupportStoneInstanceIds ?? [])
            .Select(id => Management.SkillStones.Single(item => item.InstanceId == id))
            .Where(stone => !string.IsNullOrEmpty(stone.Definition.SupportId))
            .Select(stone => new LinkedSupport(stone.Definition.SupportId,
                Math.Clamp(stone.Level + (stone.Mutated ? 1 : 0) +
                    _heroBuild.Equipment.Modifiers.Value(ItemModifierKind.SupportSkillGemLevels) +
                    _heroBuild.Equipment.Modifiers.Value(ItemModifierKind.AllSupportSkillGemLevels), 1, 40), stone.Quality))
            .GroupBy(item => item.StoneId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
    }

    private static SkillRole ActiveRole(string stoneId)
    {
        return ActiveSkillCatalog.ActiveForStone(stoneId).Combat.Role;
    }

    private static string ToCombatSkillId(string definitionId) => definitionId.StartsWith("core.skill_stone.", StringComparison.Ordinal)
        ? definitionId.Replace("core.skill_stone.", "core.skill.", StringComparison.Ordinal)
        : definitionId.StartsWith("archetypes.skill_stone.", StringComparison.Ordinal)
            ? definitionId.Replace("archetypes.skill_stone.", "archetypes.skill.", StringComparison.Ordinal)
            : definitionId.StartsWith("builds.skill_stone.", StringComparison.Ordinal)
                ? definitionId.Replace("builds.skill_stone.", "builds.skill.", StringComparison.Ordinal)
                : string.Empty;

    private static IEnumerable<string> SupportDefinitionIds(SkillSupport supports)
    {
        foreach (SkillStoneDefinition definition in SkillStoneCatalog.All
                     .Where(item => item.Kind == SkillStoneKind.Support && item.CombatSupport != SkillSupport.None &&
                                    supports.HasFlag(item.CombatSupport))
                     .OrderBy(item => item.StableId, StringComparer.Ordinal))
            yield return definition.StableId;
    }

    private TeamExpeditionState Team(ExpeditionTeamKind kind) =>
        kind == ExpeditionTeamKind.Hero ? World.Hero : World.Mercenaries;

    private void ReturnQueuedMaps(TeamExpeditionState team)
    {
        while (team.Queue.Count > 0)
        {
            MapItem? map = team.Queue.TakeAt(0);
            if (map is not null && !ExpeditionDirector.IsBoss(map) && !ExpeditionDirector.IsPractice(map) && !EndgameState.IsCitadel(map))
            {
                World.AddMap(map);
            }
        }
    }

}
