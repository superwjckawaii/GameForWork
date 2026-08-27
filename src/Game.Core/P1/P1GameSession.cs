using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P1.Progression;
using GameForWork.Core.P1.World;

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

public sealed record HeroAiConfiguration(
    string Preset,
    bool UseWarCry,
    int LifeFlaskThresholdBasisPoints)
{
    public static HeroAiConfiguration Balanced => new("均衡", true, 5_000);

    public HeroAiConfiguration Validate()
    {
        if (string.IsNullOrWhiteSpace(Preset) || LifeFlaskThresholdBasisPoints is < 1_000 or > 9_000)
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
    int SimulationSequence);

public sealed class P1GameSession
{
    public const int CurrentFormatVersion = 3;
    private readonly P1WorldSimulator _simulator = new(new P1MapAttemptResolver());
    private AssembledCharacterBuild _heroBuild;

    private P1GameSession(
        PlayerIdentity player,
        string mercenaryName,
        P1WorldState world,
        EquipmentLoadout heroEquipment,
        PassiveTreeAllocation passives,
        SkillSupport heavyStrikeSupports,
        HeroAiConfiguration heroAi,
        ulong seed,
        int simulationSequence,
        bool debugTwentyTimes)
    {
        Player = player.Validate();
        MercenaryName = mercenaryName;
        World = world;
        HeroEquipment = heroEquipment;
        Passives = passives;
        HeavyStrikeSupports = heavyStrikeSupports;
        HeroAi = heroAi.Validate();
        Seed = seed;
        SimulationSequence = simulationSequence;
        DebugTwentyTimes = debugTwentyTimes;
        _heroBuild = AssembleHero();
        RefreshHeroTeamBuild();
    }

    public PlayerIdentity Player { get; }
    public string MercenaryName { get; }
    public P1WorldState World { get; }
    public EquipmentLoadout HeroEquipment { get; }
    public PassiveTreeAllocation Passives { get; private set; }
    public SkillSupport HeavyStrikeSupports { get; private set; }
    public HeroAiConfiguration HeroAi { get; private set; }
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
        var economy = new TownEconomyState(memoryAshes: 0);
        var world = new P1WorldState(
            ToTeamBuild(build, SkillSupport.Bleed, HeroAiConfiguration.Balanced),
            mercenary.CreateTeamBuild(),
            economy);
        world.AddInitialMaps();
        return new P1GameSession(
            player,
            mercenary.Name,
            world,
            equipment,
            passives,
            SkillSupport.Bleed,
            HeroAiConfiguration.Balanced,
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
        PassiveTreeAllocation passives = PassiveTreeAllocation.Restore(
            snapshot.AllocatedPassives,
            snapshot.MemoryAshes);
        P1WorldState world = P1WorldSnapshots.Restore(snapshot.World);
        if (snapshot.FormatVersion == 1)
        {
            P1MercenaryProfile upgradedMercenary = P1MercenaryFactory.GenerateCantor(snapshot.Seed ^ 0xa5a5a5a5UL);
            world.Mercenaries.UpdateBuild(upgradedMercenary.CreateTeamBuild(world.Mercenaries.Progression.Level));
        }

        return new P1GameSession(
            snapshot.Player,
            snapshot.MercenaryName,
            world,
            equipment,
            passives,
            snapshot.HeavyStrikeSupports,
            snapshot.HeroAi ?? HeroAiConfiguration.Balanced,
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
        SimulationSequence);

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

        RefreshHeroTeamBuild();
        return result;
    }

    public void SetHeavyStrikeSupports(SkillSupport supports)
    {
        HeavyStrikeSupports = supports;
        RefreshHeroBuild();
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
        bool changed = Passives.TryReset();
        if (changed)
        {
            RefreshHeroBuild();
        }

        return changed;
    }

    public bool TryEquipFromStorage(int storageIndex, EquipmentSlot slot)
    {
        if (storageIndex < 0 || storageIndex >= World.Storage.Items.Count)
        {
            return false;
        }

        bool equipped = HeroEquipment.TryEquip(slot, World.Storage.Items[storageIndex]);
        if (equipped)
        {
            RefreshHeroBuild();
        }

        return equipped;
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

    private void RefreshHeroTeamBuild() => World.Hero.UpdateBuild(ToTeamBuild(_heroBuild, HeavyStrikeSupports, HeroAi));

    private static P1TeamBuild ToTeamBuild(
        AssembledCharacterBuild build,
        SkillSupport supports,
        HeroAiConfiguration ai) => new(
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
        WeaponLegendaryRule: build.Equipment.WeaponLegendaryRule);
}
