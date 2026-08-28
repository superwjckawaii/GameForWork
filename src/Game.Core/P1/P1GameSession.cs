using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P1.Progression;
using GameForWork.Core.P1.World;
using GameForWork.Core.P2;
using GameForWork.Core.P4;
using GameForWork.Core.P5;
using GameForWork.Core.P6;

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
    P2CampaignSnapshot? Campaign = null);

public sealed class P1GameSession
{
    public const int CurrentFormatVersion = 9;
    private readonly P1WorldSimulator _simulator = new(new P1MapAttemptResolver());
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
        Seed = seed;
        SimulationSequence = simulationSequence;
        DebugTwentyTimes = debugTwentyTimes;
        Management.NormalizeSkillChains(P5SkillChainRules.Build(HeroEquipment));
        HeavyStrikeSupports = SupportsFor("core.skill_stone.heavy_strike");
        _heroBuild = AssembleHero();
        RefreshHeroTeamBuild();
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
    public bool IsExpeditionUnlocked => Campaign.Completed;
    public bool DebugTwentyTimes { get; set; }
    public ulong Seed { get; }
    public int SimulationSequence { get; private set; }
    public AssembledCharacterBuild HeroBuild => _heroBuild;
    public int SimulationSpeed => DebugTwentyTimes ? 20 : 1;

    public static P1GameSession CreateNew(PlayerIdentity player, ulong seed)
    {
        var equipment = new EquipmentLoadout();
        EquipStarter(equipment, EquipmentSlot.MainHand, "core.base.rusted_greatsword", seed + 1);
        EquipStarter(equipment, EquipmentSlot.Chest, "core.base.crude_chainmail", seed + 2);
        EquipStarter(equipment, EquipmentSlot.Helmet, "core.base.iron_helmet", seed + 3);
        EquipStarter(equipment, EquipmentSlot.Gloves, "core.base.iron_gauntlets", seed + 6);
        EquipStarter(equipment, EquipmentSlot.RingLeft, "core.base.life_ring", seed + 4);
        EquipStarter(equipment, EquipmentSlot.Flask1, "core.base.life_flask", seed + 5);
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
        var world = new P1WorldState(
            ToTeamBuild(build, SkillSupport.Bleed, HeroAiConfiguration.Balanced),
            mercenary.CreateTeamBuild(),
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
            seed,
            simulationSequence: 0,
            debugTwentyTimes: false);
    }

    public static P1GameSession Restore(P1GameSessionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.FormatVersion is < 1 or > CurrentFormatVersion || snapshot.SimulationSequence < 0)
        {
            throw new InvalidDataException("P1 session snapshot version is unsupported.");
        }

        EquipmentLoadout equipment = EquipmentLoadout.Restore(
            snapshot.HeroEquipment.Select(entry =>
                new KeyValuePair<EquipmentSlot, ItemInstance>(entry.Slot, entry.Item)));
        P1MercenaryProfile restoredMercenary = P1MercenaryFactory.GenerateCantor(snapshot.Seed ^ 0xa5a5a5a5UL);
        EquipmentLoadout mercenaryEquipment = snapshot.MercenaryEquipment is null
            ? restoredMercenary.Equipment
            : EquipmentLoadout.Restore(snapshot.MercenaryEquipment.Select(entry =>
                new KeyValuePair<EquipmentSlot, ItemInstance>(entry.Slot, entry.Item)));
        PassiveTreeAllocation passives = PassiveTreeAllocation.Restore(
            snapshot.AllocatedPassives,
            snapshot.MemoryAshes);
        P1WorldState world = P1WorldSnapshots.Restore(snapshot.World);
        if (snapshot.FormatVersion == 1)
        {
            P1MercenaryProfile upgradedMercenary = P1MercenaryFactory.GenerateCantor(snapshot.Seed ^ 0xa5a5a5a5UL);
            world.Mercenaries.UpdateBuild(upgradedMercenary.CreateTeamBuild(world.Mercenaries.Progression.Level));
        }

        if (snapshot.FormatVersion < CurrentFormatVersion)
        {
            world.Hero.Progression.MigrateToMinimumLevel(CharacterProgression.MaximumLevel);
            world.Mercenaries.Progression.MigrateToMinimumLevel(CharacterProgression.MaximumLevel);
            if (snapshot.FormatVersion < 7)
            {
                world.Economy.AddMetal(MetalCurrencyKind.TemperingIron, 3);
                world.Economy.AddMetal(MetalCurrencyKind.WardSteel, 3);
                world.Economy.AddMetal(MetalCurrencyKind.VitalSilver, 3);
            }

            if (snapshot.FormatVersion < 8)
            {
                MigrateLegacyDispatch(world, world.Hero);
                MigrateLegacyDispatch(world, world.Mercenaries);
            }
        }

        bool legacyP1Migration = snapshot.FormatVersion < 5;
        P2ManagementState management = P2ManagementState.Restore(snapshot.Management, legacyP1Migration);
        if (snapshot.FormatVersion < 8)
        {
            SynchronizeLegacyHeavySupports(management, snapshot.HeavyStrikeSupports);
        }
        return new P1GameSession(
            snapshot.Player,
            snapshot.MercenaryName,
            world,
            equipment,
            mercenaryEquipment,
            passives,
            snapshot.HeavyStrikeSupports,
            snapshot.HeroAi ?? HeroAiConfiguration.Balanced,
            management,
            P2CampaignState.Restore(legacyP1Migration ? null : snapshot.Campaign, legacyP1Migration),
            snapshot.Seed,
            snapshot.SimulationSequence,
            snapshot.DebugTwentyTimes);
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
        Campaign.Capture());

    public P1OfflineResult Advance(long realElapsedMilliseconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(realElapsedMilliseconds);
        long maximum = GameForWork.Core.Offline.OfflineTime.MaximumMilliseconds;
        long simulated = realElapsedMilliseconds > maximum / SimulationSpeed
            ? maximum
            : realElapsedMilliseconds * SimulationSpeed;
        return AdvanceSimulated(simulated);
    }

    public P1OfflineResult AdvanceOffline(long elapsedMilliseconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(elapsedMilliseconds);
        return AdvanceSimulated(Math.Min(
            elapsedMilliseconds,
            GameForWork.Core.Offline.OfflineTime.MaximumMilliseconds));
    }

    public int AdvanceTownOnly(long realElapsedMilliseconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(realElapsedMilliseconds);
        long maximum = GameForWork.Core.Offline.OfflineTime.MaximumMilliseconds;
        long simulated = realElapsedMilliseconds > maximum / SimulationSpeed
            ? maximum
            : realElapsedMilliseconds * SimulationSpeed;
        return World.Economy.AdvanceProduction(simulated);
    }

    private P1OfflineResult AdvanceSimulated(long simulatedMilliseconds)
    {
        if (!Campaign.Completed)
        {
            P2CampaignAdvanceResult campaignResult = new P2CampaignSimulator().Simulate(
                Campaign,
                World,
                Management,
                simulatedMilliseconds,
                Seed);
            SimulationSequence = checked(SimulationSequence + campaignResult.NodesCompleted);
            RefreshHeroBuild();
            return new P1OfflineResult(
                campaignResult.EffectiveMilliseconds,
                campaignResult.WasClamped,
                campaignResult.SuppliesProduced,
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
        P1OfflineResult result = _simulator.Simulate(
            World,
            simulatedMilliseconds,
            Seed);
        SimulationSequence = checked(
            SimulationSequence + result.TotalMapsCompleted + result.TotalMapsFailed);
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
        }

        RefreshHeroTeamBuild();
        return result;
    }

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
        team.Resume();
        World.Expedition.PrepareNext(World, team);
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
        }

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
        }

        return result;
    }

    public bool TryExchangeLegendary()
    {
        if (!World.Economy.TryExchangeLegendary(out ItemInstance? item) || item is null)
        {
            return false;
        }

        return World.Storage.TryStore(item);
    }

    public int EnqueueInventoryMaps()
    {
        int moved = 0;
        for (int index = World.MapInventory.Count - 1; index >= 0; index--)
        {
            P1TeamExpeditionState team = (moved & 1) == 0 ? World.Hero : World.Mercenaries;
            if (!team.Queue.TryEnqueue(World.MapInventory[index]))
            {
                team = team == World.Hero ? World.Mercenaries : World.Hero;
                if (!team.Queue.TryEnqueue(World.MapInventory[index]))
                {
                    continue;
                }
            }

            World.MapInventory.RemoveAt(index);
            moved++;
        }

        return moved;
    }

    public CombatPreview GetCombatPreview() => CombatPreviewRules.Calculate(
        _heroBuild.Sheet,
        _heroBuild.Equipment.Weapon!,
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
            RequirementsMet: true,
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

    private AssembledCharacterBuild AssembleHero() => CharacterBuildAssembler.Assemble(
        World.Hero.Progression.Level,
        CharacterAttributes.IronOathStarting,
        HeroEquipment,
        Passives,
        new SkillConfiguration(P1SkillIds.HeavyStrike, HeavyStrikeSupports));

    private static CombatPreview Preview(AssembledCharacterBuild build) => CombatPreviewRules.Calculate(
        build.Sheet,
        build.Equipment.Weapon!,
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
        ToTeamBuild(_heroBuild, HeavyStrikeSupports, HeroAi, BuildActiveSkills()));

    private void RefreshMercenaryBuild()
    {
        P1MercenaryProfile generated = P1MercenaryFactory.GenerateCantor(Seed ^ 0xa5a5a5a5UL);
        var profile = new P1MercenaryProfile(
            generated.StableId,
            MercenaryName,
            generated.Archetype,
            generated.FinalAttributes,
            generated.Traits,
            generated.AutonomousConfiguration,
            MercenaryEquipment);
        World.Mercenaries.UpdateBuild(profile.CreateTeamBuild(World.Mercenaries.Progression.Level));
    }

    private static P1TeamBuild ToTeamBuild(
        AssembledCharacterBuild build,
        SkillSupport supports,
        HeroAiConfiguration ai,
        IReadOnlyList<SkillConfiguration>? activeSkills = null) => new P1TeamBuild(
        build.Sheet,
        build.Equipment.Weapon ?? throw new InvalidOperationException("Hero weapon is missing."),
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
        MovementSpeedBasisPoints: checked(10_000 + build.Passives.IncreasedMovementSpeedBasisPoints),
        ActiveSkills: activeSkills ??
        [
            new SkillConfiguration(P1SkillIds.HeavyStrike, supports),
            new SkillConfiguration(P1SkillIds.EarthCleave, SkillSupport.IncreasedArea),
            new SkillConfiguration(P1SkillIds.SpiritBlade, SkillSupport.Chain),
        ]) with
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
        .Where(entry => entry.Stone.DefinitionId != "core.skill_stone.iron_oath_banner" || entry.Link.ReservationEnabled)
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
            string definition = Management.SkillStones.Single(item => item.InstanceId == supportId).DefinitionId;
            supports |= definition switch
            {
                "core.skill_stone.increased_area" => SkillSupport.IncreasedArea,
                "core.skill_stone.attack_speed" => SkillSupport.AttackSpeed,
                "core.skill_stone.bleed" => SkillSupport.Bleed,
                "core.skill_stone.life_cost" => SkillSupport.LifeCost,
                "core.skill_stone.chain" => SkillSupport.Chain,
                "core.skill_stone.brutality" => SkillSupport.Brutality,
                "core.skill_stone.multiple_projectiles" => SkillSupport.MultipleProjectiles,
                "core.skill_stone.faster_projectiles" => SkillSupport.FasterProjectiles,
                "core.skill_stone.urgent_war_cry" => SkillSupport.UrgentWarCry,
                "core.skill_stone.life_leech" => SkillSupport.LifeLeech,
                "core.skill_stone.execution" => SkillSupport.Execution,
                _ => SkillSupport.None,
            };
        }

        return supports;
    }

    private static string ToCombatSkillId(string definitionId) => definitionId switch
    {
        "core.skill_stone.heavy_strike" => P1SkillIds.HeavyStrike,
        "core.skill_stone.earth_cleave" => P1SkillIds.EarthCleave,
        "core.skill_stone.spirit_blade" => P1SkillIds.SpiritBlade,
        "core.skill_stone.war_cry" => P1SkillIds.WarCry,
        "core.skill_stone.seismic_charge" => P1SkillIds.SeismicCharge,
        "core.skill_stone.blood_tide_spin" => P1SkillIds.BloodTideSpin,
        "core.skill_stone.iron_oath_banner" => P1SkillIds.IronOathBanner,
        _ => string.Empty,
    };

    private static IEnumerable<string> SupportDefinitionIds(SkillSupport supports)
    {
        if (supports.HasFlag(SkillSupport.IncreasedArea)) yield return "core.skill_stone.increased_area";
        if (supports.HasFlag(SkillSupport.AttackSpeed)) yield return "core.skill_stone.attack_speed";
        if (supports.HasFlag(SkillSupport.Bleed)) yield return "core.skill_stone.bleed";
        if (supports.HasFlag(SkillSupport.LifeCost)) yield return "core.skill_stone.life_cost";
        if (supports.HasFlag(SkillSupport.Chain)) yield return "core.skill_stone.chain";
        if (supports.HasFlag(SkillSupport.Brutality)) yield return "core.skill_stone.brutality";
        if (supports.HasFlag(SkillSupport.MultipleProjectiles)) yield return "core.skill_stone.multiple_projectiles";
        if (supports.HasFlag(SkillSupport.FasterProjectiles)) yield return "core.skill_stone.faster_projectiles";
        if (supports.HasFlag(SkillSupport.UrgentWarCry)) yield return "core.skill_stone.urgent_war_cry";
        if (supports.HasFlag(SkillSupport.LifeLeech)) yield return "core.skill_stone.life_leech";
        if (supports.HasFlag(SkillSupport.Execution)) yield return "core.skill_stone.execution";
    }

    private static void SynchronizeLegacyHeavySupports(P2ManagementState management, SkillSupport supports)
    {
        SkillStoneInstance active = management.SkillStones.Single(
            item => item.DefinitionId == "core.skill_stone.heavy_strike");
        string[] supportIds = SupportDefinitionIds(supports)
            .Select(definition => management.SkillStones.FirstOrDefault(item => item.DefinitionId == definition)?.InstanceId)
            .Where(id => id is not null)
            .Cast<string>()
            .ToArray();
        management.ReplaceSupports(active.InstanceId, supportIds);
    }

    private P1TeamExpeditionState Team(ExpeditionTeamKind kind) =>
        kind == ExpeditionTeamKind.Hero ? World.Hero : World.Mercenaries;

    private void ReturnQueuedMaps(P1TeamExpeditionState team)
    {
        while (team.Queue.Count > 0)
        {
            P1MapItem? map = team.Queue.TakeAt(0);
            if (map is not null && !P5ExpeditionDirector.IsBoss(map) && !P5ExpeditionDirector.IsPractice(map))
            {
                World.MapInventory.Add(map);
            }
        }
    }

    private static void MigrateLegacyDispatch(P1WorldState world, P1TeamExpeditionState team)
    {
        if (team.ActiveMap is null && team.Queue.Count == 0)
        {
            return;
        }

        P5ExpeditionTarget target = team.Policy.PreferredRoute == MapRoute.Safe
            ? P5ExpeditionTarget.SafeMaps
            : P5ExpeditionTarget.AbyssMaps;
        world.Expedition.Assign(team.Kind, target, P5DispatchMode.Repeat);
    }
}
