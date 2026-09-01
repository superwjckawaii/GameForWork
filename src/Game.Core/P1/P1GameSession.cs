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
using GameForWork.Core.P23;
using GameForWork.Core.P24;
using GameForWork.Core.P26;
using GameForWork.Core.P28;
using GameForWork.Core.P29;
using GameForWork.Core.P30;

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

public sealed record PlayerIdentity(
    string Name,
    CharacterGender Gender,
    CharacterSkinTone SkinTone,
    CharacterHairStyle HairStyle,
    P23BaseClass BaseClass)
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
    IReadOnlyDictionary<string, PassiveJewelKind>? SocketedJewels = null,
    P30JewelStateSnapshot? P30Jewels = null);

public sealed class P1GameSession
{
    public const int CurrentFormatVersion = 22;
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
        P30JewelState jewels,
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
    public P30JewelState Jewels { get; }
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
        P23ClassDefinition classDefinition = P23ClassCatalog.Get(player.BaseClass);
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
            HeroAiConfiguration.Balanced with { Preset = classDefinition.AiPreset },
            P2ManagementState.CreateNew(player.BaseClass),
            P2CampaignState.CreateNew(),
            P8DemoJourney.CreateNew(tutorialEnabled),
            town,
            new P10EndgameState(),
            new P30JewelState(),
            seed,
            simulationSequence: 0,
            debugTwentyTimes: false);
    }

    public static P1GameSession Restore(P1GameSessionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        bool migratingV18 = snapshot.FormatVersion == 18;
        bool migratingV19 = snapshot.FormatVersion == 19;
        bool migratingV20 = snapshot.FormatVersion == 20;
        bool migratingV21 = snapshot.FormatVersion == 21;
        if ((!migratingV18 && !migratingV19 && !migratingV20 && !migratingV21 && snapshot.FormatVersion != CurrentFormatVersion) || snapshot.SimulationSequence < 0)
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
        P23BaseClass baseClass = migratingV18 ? P23BaseClass.Fighter : snapshot.Player.BaseClass;
        P23ClassDefinition classDefinition = P23ClassCatalog.Get(baseClass);
        PlayerIdentity player = snapshot.Player with { BaseClass = baseClass };
        PassiveTreeAllocation passives = migratingV18 || migratingV19 || migratingV20 || migratingV21
            ? new PassiveTreeAllocation(snapshot.MemoryAshes, classDefinition.PassiveStart)
            : PassiveTreeAllocation.Restore(snapshot.AllocatedPassives, snapshot.MemoryAshes,
                snapshot.MasterySelections, snapshot.SocketedJewels, classDefinition.PassiveStart);
        P1WorldState world = P1WorldSnapshots.Restore(snapshot.World);
        if (migratingV20)
        {
            int oldEarnedPoints = Math.Min(25, (snapshot.Endgame?.CompletedTiers.Count ?? 0) + (snapshot.Endgame?.BonusAtlasPoints ?? 0));
            world.Economy.AddDispositionProceeds(Math.Min(100_000, oldEarnedPoints * 4_000), 0);
        }
        P9TownState town = P9TownState.Restore(snapshot.Town, snapshot.Seed ^ 0x7039746f776eUL, mercenaryEquipment);
        if (town.Roster.Count > 0) mercenaryEquipment = town.Roster[0].Equipment;
        P2ManagementState management = P2ManagementState.Restore(snapshot.Management, legacyMigration: migratingV18);
        P10EndgameState endgame = P10EndgameState.Restore(snapshot.Endgame);
        if (endgame.SelectedAscendancy != P18Ascendancy.None &&
            (!P23ClassCatalog.Allows(baseClass, endgame.SelectedAscendancy) ||
             !P18AscendancyCatalog.IsImplemented(endgame.SelectedAscendancy)))
            throw new InvalidDataException("Saved ascendancy does not belong to the selected base class.");
        var session = new P1GameSession(
            player,
            snapshot.MercenaryName,
            world,
            equipment,
            mercenaryEquipment,
            passives,
            snapshot.HeavyStrikeSupports,
            snapshot.HeroAi ?? HeroAiConfiguration.Balanced with { Preset = classDefinition.AiPreset },
            management,
            P2CampaignState.Restore(snapshot.Campaign, legacyMigration: false),
            P8DemoJourney.Restore(snapshot.Journey, legacy: false),
            town,
            endgame,
            P30JewelState.Restore(snapshot.P30Jewels),
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
        new Dictionary<string, PassiveJewelKind>(Passives.SocketedJewels),
        Jewels.Capture());

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
            _campaignSimulator.NodeResolved = () => { SynchronizeCampaignPassivePoints(); SynchronizeCampaignAscendancyPoints(); RefreshHeroBuild(); Journey.Synchronize(this); };
            P2CampaignAdvanceResult campaignResult = _campaignSimulator.Simulate(
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

        _simulator.MapStarted = (_, map, route) => { if (route == MapRoute.Warfront && !P5ExpeditionDirector.IsPractice(map)) Endgame.DiscoverWarfront(); };
        _simulator.PrepareMap = map => map with { AtlasSnapshot = Endgame.AtlasPassives.Order(StringComparer.Ordinal).ToArray() };
        _simulator.MapResolved = ResolveGameplay;
        P1OfflineResult result = _simulator.Simulate(
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
            Town.RecordMilestone("p9.milestone.abyss_warden", World.Economy);
        if (result.TotalMapsCompleted > 0 || ashes > 0)
            RefreshHeroTeamBuild();
        if (result.TotalMapsCompleted > 0) RefreshMercenaryPartyBuild();
        return result;
    }

    private void ResolveGameplay(P1TeamExpeditionState team, P1MapRunResult run, ulong seed, int baseStones, ExpeditionPolicy policy)
    {
        SimulationSequence = checked(SimulationSequence + 1);
        for (int i = 0; i < baseStones; i++) Management.AddDroppedSkillStone(seed ^ (uint)i ^ 0x703238baUL);
        bool special = P10EndgameState.IsCitadel(run.Map) || P10EndgameState.IsCitadelPractice(run.Map) ||
            P10EndgameState.IsBreakthroughTrial(run.Map) || P5ExpeditionDirector.IsPractice(run.Map);
        if (!special)
        {
            P28RewardLedger rewards = P28Rewards.Roll(run, seed);
            P28Mechanic? rewardMechanic = rewards.Encounters.Where(encounter => encounter.Kills > 0)
                .Select(encounter => (P28Mechanic?)encounter.Node.Gameplay?.Mechanic).FirstOrDefault(mechanic => mechanic is not null);
            IReadOnlySet<string>? themedSkills = rewardMechanic is null ? null : P29SkillDropCatalog.For(rewardMechanic.Value);
            bool pity = Endgame.RecordGameplay(rewards, P28Gameplay.Has(run.Map.AtlasSnapshot, "blue", 11));
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
                if (target == P28RewardPreference.SkillStones) Management.AddDroppedSkillStone(seed ^ 0xb1eeUL);
                else
                {
                    ItemInstance item = target == P28RewardPreference.Legendary
                        ? P14UniqueItems.Create("core.unique.blue_vow", run.Map.MonsterLevel, $"p28-pity-{run.Map.InstanceId}")
                        : P28Rewards.Equipment(target, Math.Min(120, run.Map.MonsterLevel + 2), true, seed, $"p28-pity-{run.Map.InstanceId}");
                    if (!World.Storage.TryStore(item)) Management.AddToRecovery(item, "苍誓保底奖励");
                }
            }
            if (run.Succeeded) RollP30Jewels(run.Map, seed);
            if (rewards.Encounters.Any(e => e.Kills > 0)) Management.AddHistory(
                $"P28 T{run.Map.Tier} {run.Route}：命能+{rewards.LifeForce} 战功+{rewards.Merit} 声望+{rewards.Reputation}；" +
                (run.Succeeded ? "已完成" : "保留已击败怪物与已兑现奖励；未完成/苍誓承诺不发放"));
            if (run.Succeeded) Endgame.RecordMapCompletion(run.Map, run.Route, seed);
        }
        if (!run.Succeeded) { Journey.Synchronize(this); return; }
        Management.AddSkillExperience(120); Town.AddActiveExperience(120);
        if (P10EndgameState.IsBreakthroughTrial(run.Map)) RecordFinalBreakthroughTrialVictory();
        if (P10EndgameState.IsCitadel(run.Map))
        {
            Endgame.RecordCitadelVictory();
            if (Endgame.TryClaimCitadelMythic())
            {
                ItemInstance mythic = P14UniqueItems.Create("core.mythic.heart_of_ash", 120, $"mythic-{run.Map.InstanceId}");
                if (!World.Storage.TryStore(mythic)) Management.AddToRecovery(mythic, "灰烬天垒首杀奖励");
            }
        }
        EnsureWarfrontDiscoveryMap(); SynchronizeWarfrontRouteCandidates();
        RefreshHeroTeamBuild(); RefreshMercenaryPartyBuild(); Journey.Synchronize(this);
    }

    private void RollP30Jewels(P1MapItem map, ulong seed)
    {
        int tier = Math.Clamp(map.Tier, 1, 20);
        int itemLevel = Math.Clamp(map.MonsterLevel, 1, 100);
        TryRoll(P30Jewels.MapCompletionDropChanceBasisPoints(tier), seed ^ 0x30a11ceUL, itemLevel, "map");
        TryRoll(P30Jewels.BossDropChanceBasisPoints(tier), seed ^ 0x30b055UL, Math.Min(100, itemLevel + 2), "boss");

        if (tier >= 6)
        {
            int memoryChance = tier <= 10 ? 15 : tier <= 15 ? 25 : 40;
            ulong legendaryRoll = Mix(seed ^ 0x30cafeUL);
            if (legendaryRoll % 10_000 < (ulong)memoryChance)
            {
                string[] pool = ["crimson_memory", "verdant_memory", "golden_memory", "azure_memory"];
                AddJewel(P30Jewels.CreateLegendary(pool[(int)((legendaryRoll >> 16) % 4)], itemLevel,
                    $"p30-jewel-{SimulationSequence:000000}-memory"));
            }
        }

        void TryRoll(int chance, ulong rollSeed, int level, string source)
        {
            ulong roll = Mix(rollSeed);
            if (roll % 10_000 >= (ulong)chance) return;
            AddJewel(P30Jewels.RollPrismatic(level, roll, $"p30-jewel-{SimulationSequence:000000}-{source}"));
        }
        void AddJewel(P30JewelInstance jewel)
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

    public bool TryExchangeWarfrontSupply(P28RewardPreference preference)
    {
        if (preference is not (P28RewardPreference.Weapons or P28RewardPreference.Armor or P28RewardPreference.Jewelry or P28RewardPreference.Materials)) return false;
        return TryExchangeWarfrontSupply(Endgame.SupplyTier);
    }

    public bool TryExchangeWarfrontSupply(int tier)
    {
        if (!Endgame.WarfrontDiscovered || tier is < 1 or > 3 || tier > Endgame.SupplyTier) return false;
        int cost = tier * 50;
        if (Endgame.WarfrontMerit < cost) return false;
        ulong seed = Seed ^ (ulong)Endgame.GameplayOperationSequence * 0x9e3779b97f4a7c15UL;
        ItemInstance item = P29WarfrontRewards.Create(tier, seed, Endgame.LastWarfrontBaseId,
            $"p29-supply-{Endgame.GameplayOperationSequence}");
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
            P1MapItem guaranteed = new P1MapItem($"p27-warfront-discovery-{SimulationSequence:000000}", 6)
                .EnsureFormal(Seed ^ (ulong)SimulationSequence ^ 0x703237776172UL);
            World.AddMap(guaranteed);
            index = World.MapInventory.FindIndex(map => map.InstanceId == guaranteed.InstanceId);
        }
        if (index >= 0)
        {
            P1MapItem map = World.MapInventory[index];
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
            P1MapItem map = World.MapInventory[index];
            if (map.Tier < 6 || map.EffectiveRouteCandidates.Contains(MapRoute.Warfront)) continue;
            byte[] hash = System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes($"{Seed}|{map.InstanceId}|p27-warfront"));
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
        P5SkillChainDefinition? chain = GetSkillChains().FirstOrDefault(item => item.StableId == activeLink.ChainId);
        Management.ReplaceSupports(active.InstanceId, supportIds, chain?.SupportCapacity ?? 5);
        HeavyStrikeSupports = SupportsFor("core.skill_stone.heavy_strike");
        RefreshHeroBuild();
    }

    public void SyncHeavyStrikeFromSkillStones()
    {
        HeavyStrikeSupports = SupportsFor("core.skill_stone.heavy_strike");
        RefreshHeroBuild();
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
        P1MapItem map = World.MapInventory[mapIndex].EnsureFormal(Seed ^ (ulong)mapIndex);
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

    public P12MapCraftResult CraftMap(int mapIndex, P12MapCraftOperation operation)
    {
        if (mapIndex < 0 || mapIndex >= World.MapInventory.Count)
            return new(false, new P1MapItem("invalid", 1), default, 0, "map_required");
        ulong seed = Seed ^ (ulong)SimulationSequence++ ^ (ulong)(mapIndex + 1) * 0x9e3779b97f4a7c15UL;
        P12MapCraftResult result = P12MapCrafting.Apply(World.Economy, World.MapInventory[mapIndex], operation,
            seed, World.MaximumUnlockedMapTier,
            P26AtlasEffects.Has(Endgame.AtlasPassives.ToArray(), "p26.atlas.craft.02") ? 10 : 5);
        if (result.Succeeded && result.Destroyed) World.MapInventory.RemoveAt(mapIndex);
        else if (result.Succeeded) World.MapInventory[mapIndex] = result.Map!;
        return result;
    }

    public P12MapBatchResult BatchCraftMaps(P12MapBatchRule requestedRule, P26MapFilter? requestedFilter = null)
    {
        P12MapBatchRule rule = requestedRule.Validate();
        P26MapFilter filter = (requestedFilter ?? World.MapCraftFilter).Validate();
        World.MapCraftFilter = filter;
        string[] selectedIds = filter.Select(World.MapInventory).Where(map => !map.IsProtected)
            .Select(map => map.InstanceId).ToArray();
        int processed = 0, completed = 0, skipped = 0, destroyed = 0, spent = 0;
        bool stopped = false;
        foreach (string selectedId in selectedIds)
        {
            int index = World.MapInventory.FindIndex(map => map.InstanceId == selectedId);
            if (index < 0) continue;
            P1MapItem map = World.MapInventory[index].EnsureFormal(Seed ^ (ulong)index);
            int mapSpent = 0;
            bool failed = false, mapDestroyed = false;

            bool Apply(P12MapCraftOperation operation)
            {
                (_, int cost) = P12MapCrafting.Cost(operation);
                if (mapSpent + cost > rule.MaximumMetalSpendPerMap) return false;
                ulong operationSeed = Seed ^ (ulong)SimulationSequence++ ^ (ulong)(index + 1) * 0x517cc1b727220a95UL;
                P12MapCraftResult result = P12MapCrafting.Apply(World.Economy, map, operation, operationSeed, World.MaximumUnlockedMapTier,
                    P26AtlasEffects.Has(Endgame.AtlasPassives.ToArray(), "p26.atlas.craft.02") ? 10 : 5);
                if (!result.Succeeded) return false;
                mapSpent += result.Cost;
                if (result.Destroyed) { mapDestroyed = true; return true; }
                map = result.Map!;
                return true;
            }

            while (!failed && !mapDestroyed && map.Quality < rule.MinimumQuality)
                if (!Apply(P12MapCraftOperation.PolishQuality)) failed = true;
            if (!failed && map.Rarity < rule.TargetRarity)
            {
                P12MapCraftOperation upgrade = rule.TargetRarity == P12MapRarity.Rare
                    ? P12MapCraftOperation.AlchemicalRare : P12MapCraftOperation.AwakenMagic;
                if (!Apply(upgrade)) failed = true;
            }
            while (!failed && !mapDestroyed && (rule.ExcludedAffixes?.Any(kind => map.EffectiveAffixes.Any(affix => affix.Kind == kind)) ?? false))
            {
                if (map.Rarity != P12MapRarity.Rare || !Apply(P12MapCraftOperation.ChaosReroll)) failed = true;
            }
            if (!failed && !mapDestroyed && rule.Corrupt && !map.IsCorrupted && !Apply(P12MapCraftOperation.Corrupt)) failed = true;

            processed++; spent += mapSpent;
            if (mapDestroyed) { World.MapInventory.RemoveAt(index); destroyed++; }
            else World.MapInventory[index] = map;
            if (!failed) completed++;
            else
            {
                skipped++;
                if (rule.FailureBehavior == P12BatchFailureBehavior.Stop) { stopped = true; break; }
            }
        }
        return new(processed, completed, skipped, spent, stopped,
            $"处理 {processed} 张，完成 {completed} 张，腐化摧毁 {destroyed} 张，跳过 {skipped} 张，消耗金属 {spent}。");
    }

    public (int Sold, int Gold) SellMaps(P26MapFilter requestedFilter)
    {
        P26MapFilter filter = requestedFilter.Validate();
        World.MapSaleFilter = filter;
        P1MapItem[] selected = filter.Select(World.MapInventory).Where(map => !map.IsProtected).ToArray();
        int gold = selected.Sum(P26MapRules.SaleGold);
        gold = gold * (10_000 + P26AtlasEffects.MapSaleIncrease(Endgame.AtlasPassives.ToArray())) / 10_000;
        HashSet<string> ids = selected.Select(map => map.InstanceId).ToHashSet(StringComparer.Ordinal);
        World.MapInventory.RemoveAll(map => ids.Contains(map.InstanceId));
        World.Economy.AddDispositionProceeds(gold, 0);
        return (selected.Length, gold);
    }

    public void SetMapAutoSellFilter(P26MapFilter filter) => World.AutoSellMapFilter = filter.Validate();

    public void SetExpeditionPolicy(ExpeditionTeamKind kind, ExpeditionPolicy policy) => Team(kind).ApplyPolicy(policy);

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
            P23ClassCatalog.Get(Player.BaseClass).StartingAttributes,
            hypothetical,
            Passives,
            new SkillConfiguration(P1SkillIds.HeavyStrike, HeavyStrikeSupports),
            Jewels);
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
            P23ClassCatalog.Get(Player.BaseClass).StartingAttributes,
            HeroEquipment,
            Passives,
            new SkillConfiguration(P1SkillIds.HeavyStrike, HeavyStrikeSupports),
            Jewels);
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

    public bool TrySocketP30Jewel(string stableId, string instanceId, out string reason)
    {
        if (!Passives.Allocated.Contains(stableId))
        {
            reason = "需要先分配该记忆棱孔。";
            return false;
        }
        bool changed = Jewels.TrySocket(stableId, instanceId, World.Hero.Progression.Level, out reason);
        if (changed) RefreshHeroBuild();
        return changed;
    }

    public bool TryUnsocketP30Jewel(string stableId)
    {
        bool changed = Jewels.TryUnsocket(stableId);
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
        if (!P23ClassCatalog.Allows(Player.BaseClass, ascendancy) || !P18AscendancyCatalog.IsImplemented(ascendancy))
            return false;
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
        IncreasedWarCryRangeBasisPoints: build.Passives.IncreasedWarCryRangeBasisPoints,
        VirtueViceLoadout: build.VirtueViceLoadout) with
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
        .Where(entry => ActiveRole(entry.Stone.DefinitionId) != P17SkillRole.Reservation || entry.Link.ReservationEnabled)
        .Select(entry => new SkillConfiguration(
            ToCombatSkillId(entry.Stone.DefinitionId),
            SupportsFor(entry.Stone.DefinitionId),
            entry.Link.Priority,
            entry.Link.AiRule ?? GlobalSkillRule(),
            entry.Stone.Level + (entry.Stone.Mutated ? 1 : 0),
            entry.Stone.InstanceId,
            P24SupportsFor(entry.Stone.DefinitionId),
            entry.Stone.Quality + entry.Link.SupportStoneInstanceIds.Sum(id => Management.SkillStones.FirstOrDefault(s => s.InstanceId == id)?.Quality ?? 0)))
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

    private IReadOnlyList<P24SupportMechanic> P24SupportsFor(string activeDefinitionId)
    {
        SkillStoneInstance? active = Management.SkillStones.FirstOrDefault(item => item.DefinitionId == activeDefinitionId);
        SkillLinkConfiguration? link = active is null
            ? null
            : Management.SkillLinks.FirstOrDefault(item => item.ActiveStoneInstanceId == active.InstanceId);
        return (link?.SupportStoneInstanceIds ?? [])
            .Select(id => Management.SkillStones.Single(item => item.InstanceId == id).Definition.P24Support)
            .Where(value => value != P24SupportMechanic.None)
            .Distinct()
            .ToArray();
    }

    private static P17SkillRole ActiveRole(string stoneId)
    {
        if (P24SkillCatalog.TryActiveForStone(stoneId, out P24ActiveSkillDefinition? p24)) return p24!.Combat.Role;
        return P17SkillCatalog.ActiveForStone(stoneId).Role;
    }

    private static string ToCombatSkillId(string definitionId) => definitionId.StartsWith("core.skill_stone.", StringComparison.Ordinal)
        ? definitionId.Replace("core.skill_stone.", "core.skill.", StringComparison.Ordinal)
        : definitionId.StartsWith("p24.skill_stone.", StringComparison.Ordinal)
            ? definitionId.Replace("p24.skill_stone.", "p24.skill.", StringComparison.Ordinal)
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
                World.AddMap(map);
            }
        }
    }

}
