using GameForWork.Core.P1;
using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P1.World;
using GameForWork.Core.P1.Progression;
using GameForWork.Core.P4;
using GameForWork.Core.Simulation;

namespace GameForWork.Core.P9;

public enum P9BuildingKind { Tavern, Workshop, Alchemy, Cartography, Storage, Reliquary, Teleporter }
public enum P9TownPolicy { Expansion, Expedition, Training }
public enum P9MercenaryPotential { Common, Promising, Exceptional, Legendary }
public enum P9MercenaryArchetype { Guardian, Ranger, Cantor, Arcanist }

public sealed record P9BuildingSnapshot(P9BuildingKind Kind, int Level);
public sealed record P9BuildingUpgradeCost(int TargetLevel, int Gold, int IronScraps, long DurationMilliseconds);
public sealed record P9MetalTransmutationRecipe(
    MetalCurrencyKind Output, MetalCurrencyKind Input, int InputCount, int GoldCost, int AlchemyLevel);
public sealed record P9ConstructionSnapshot(P9BuildingKind Kind, int TargetLevel, long RemainingMilliseconds);
public sealed record P9MercenaryCandidate(
    string StableId, string Name, P9MercenaryArchetype Archetype, P9MercenaryPotential Potential,
    int Level, CharacterAttributes FinalAttributes, string PositiveTrait, string NegativeTrait,
    string SkillSummary, string AiSummary, int RecruitmentCost);
public sealed record P9EquippedItemSnapshot(EquipmentSlot Slot, ItemInstance Item);
public sealed record P9MercenaryMemberSnapshot(
    P9MercenaryCandidate Identity, int Experience, IReadOnlyList<P9EquippedItemSnapshot> Equipment);
public sealed record P9TownSnapshot(
    IReadOnlyList<P9BuildingSnapshot> Buildings,
    IReadOnlyList<P9ConstructionSnapshot> Construction,
    P9TownPolicy Policy,
    long TavernRemainderMilliseconds,
    int TavernRefreshSequence,
    IReadOnlyList<P9MercenaryCandidate> Candidates,
    IReadOnlyList<string> LockedCandidates,
    IReadOnlyList<P9MercenaryMemberSnapshot> Roster,
    IReadOnlyList<string> Formation,
    long CartographyRemainderMilliseconds,
    long EventRemainderMilliseconds,
    int EventSequence,
    IReadOnlyList<string> EventLog,
    IReadOnlyList<string> Milestones,
    int CartographySequence = 0);

public sealed record P9TownAdvanceResult(int MapsGenerated, int TavernRefreshes, int EventsResolved, int ConstructionsCompleted);

public sealed class P9MercenaryMember
{
    public P9MercenaryMember(P9MercenaryCandidate identity, int experience, EquipmentLoadout equipment)
    {
        Identity = identity;
        Experience = experience;
        Equipment = equipment;
    }
    public P9MercenaryCandidate Identity { get; }
    public int Experience { get; private set; }
    public EquipmentLoadout Equipment { get; }
    public int Level => Math.Clamp(Identity.Level + Experience / 1_000, 1, 120);
    public void AddExperience(int amount) => Experience = checked(Experience + Math.Max(0, amount));
    public P9MercenaryMemberSnapshot Capture() => new(Identity, Experience,
        Equipment.Items.Select(pair => new P9EquippedItemSnapshot(pair.Key, pair.Value)).ToArray());
}

public sealed class P9TownState
{
    public const long TavernRefreshIntervalMilliseconds = 30 * 60 * 1_000;
    private static readonly long[] UpgradeDurations = [0, 3 * 60_000, 15 * 60_000, 60 * 60_000];
    private static readonly P9MetalTransmutationRecipe[] TransmutationRecipes =
    [
        new(MetalCurrencyKind.MutableMercury, MetalCurrencyKind.AwakeningCopper, 3, 25, 1),
        new(MetalCurrencyKind.AlchemicalGold, MetalCurrencyKind.MutableMercury, 5, 100, 2),
        new(MetalCurrencyKind.ChaosGold, MetalCurrencyKind.AlchemicalGold, 3, 300, 3),
        new(MetalCurrencyKind.DivineSilver, MetalCurrencyKind.ChaosGold, 5, 1_000, 3),
        new(MetalCurrencyKind.FractureSteel, MetalCurrencyKind.ExaltedGold, 3, 2_500, 4),
        new(MetalCurrencyKind.CorruptionIron, MetalCurrencyKind.DivineSilver, 3, 2_500, 4),
    ];
    private readonly Dictionary<P9BuildingKind, int> _buildings;
    private readonly List<P9ConstructionSnapshot> _construction = [];
    private readonly List<P9MercenaryCandidate> _candidates = [];
    private readonly HashSet<string> _lockedCandidates = new(StringComparer.Ordinal);
    private readonly List<P9MercenaryMember> _roster = [];
    private readonly string[] _formation = new string[6];
    private readonly List<string> _eventLog = [];
    private readonly HashSet<string> _milestones = new(StringComparer.Ordinal);
    private long _tavernRemainder;
    private int _tavernSequence;
    private long _cartographyRemainder;
    private int _cartographySequence;
    private long _eventRemainder;
    private int _eventSequence;
    private ulong _seed;

    private P9TownState(ulong seed)
    {
        _seed = seed;
        _buildings = Enum.GetValues<P9BuildingKind>().ToDictionary(kind => kind, _ => 1);
    }

    public P9TownPolicy Policy { get; private set; } = P9TownPolicy.Expansion;
    public IReadOnlyDictionary<P9BuildingKind, int> Buildings => _buildings;
    public IReadOnlyList<P9ConstructionSnapshot> Construction => _construction;
    public IReadOnlyList<P9MercenaryCandidate> Candidates => _candidates;
    public IReadOnlySet<string> LockedCandidates => _lockedCandidates;
    public IReadOnlyList<P9MercenaryMember> Roster => _roster;
    public IReadOnlyList<string> Formation => _formation;
    public IReadOnlyList<string> EventLog => _eventLog;
    public IReadOnlySet<string> Milestones => _milestones;
    public long TavernUntilRefreshMilliseconds => TavernRefreshIntervalMilliseconds - _tavernRemainder;
    public int Level(P9BuildingKind kind) => _buildings[kind];
    public int ConstructionSlots => _buildings.Values.Count(level => level >= 3) >= 4 ? 2 : 1;
    public int RosterCapacity => Level(P9BuildingKind.Tavern) switch { 1 => 6, 2 => 8, 3 => 10, _ => 12 };
    public int MercenaryCapacity => Level(P9BuildingKind.Teleporter) + 2;
    public static IReadOnlyList<P9MetalTransmutationRecipe> AlchemyRecipes => TransmutationRecipes;

    public static P9BuildingUpgradeCost? NextUpgradeCost(int currentLevel)
    {
        if (currentLevel is < 1 or >= 4) return null;
        int target = currentLevel + 1;
        return new P9BuildingUpgradeCost(
            target,
            target switch { 2 => 150, 3 => 1_000, _ => 5_000 },
            target switch { 2 => 20, 3 => 100, _ => 500 },
            UpgradeDurations[target - 1]);
    }

    public static P9TownState CreateNew(ulong seed, EquipmentLoadout legacyEquipment)
    {
        var state = new P9TownState(seed);
        for (int index = 0; index < 3; index++)
        {
            P9MercenaryCandidate candidate = P9MercenaryFactory.Generate(seed, index, starter: true);
            EquipmentLoadout equipment = index == 0 ? legacyEquipment : P9MercenaryFactory.StarterEquipment(seed ^ (ulong)(index + 1) * 0x9e3779b9UL);
            state._roster.Add(new P9MercenaryMember(candidate, 0, equipment));
            state._formation[index] = candidate.StableId;
        }
        state.RefreshCandidates();
        return state;
    }

    public static P9TownState Restore(P9TownSnapshot? snapshot, ulong seed, EquipmentLoadout legacyEquipment)
    {
        if (snapshot is null) return CreateNew(seed, legacyEquipment);
        var state = new P9TownState(seed) { Policy = snapshot.Policy, _tavernRemainder = snapshot.TavernRemainderMilliseconds,
            _tavernSequence = snapshot.TavernRefreshSequence, _cartographyRemainder = snapshot.CartographyRemainderMilliseconds,
            _cartographySequence = snapshot.CartographySequence,
            _eventRemainder = snapshot.EventRemainderMilliseconds, _eventSequence = snapshot.EventSequence };
        foreach (P9BuildingSnapshot building in snapshot.Buildings)
        {
            if (!Enum.IsDefined(building.Kind) || building.Level is < 1 or > 4) throw new InvalidDataException("P9 building snapshot is invalid.");
            state._buildings[building.Kind] = building.Level;
        }
        state._construction.AddRange(snapshot.Construction ?? []);
        state._candidates.AddRange(snapshot.Candidates ?? []);
        state._lockedCandidates.UnionWith(snapshot.LockedCandidates ?? []);
        foreach (P9MercenaryMemberSnapshot member in snapshot.Roster ?? [])
        {
            EquipmentLoadout equipment = EquipmentLoadout.Restore(member.Equipment.Select(item =>
                new KeyValuePair<EquipmentSlot, ItemInstance>(item.Slot, item.Item)));
            state._roster.Add(new P9MercenaryMember(member.Identity, member.Experience, equipment));
        }
        IReadOnlyList<string> formation = snapshot.Formation ?? [];
        for (int index = 0; index < Math.Min(6, formation.Count); index++) state._formation[index] = formation[index];
        state._eventLog.AddRange((snapshot.EventLog ?? []).TakeLast(50));
        state._milestones.UnionWith(snapshot.Milestones ?? []);
        if (state._roster.Count == 0) return CreateNew(seed, legacyEquipment);
        if (state._candidates.Count == 0) state.RefreshCandidates();
        return state;
    }

    public P9TownSnapshot Capture() => new(
        _buildings.Select(pair => new P9BuildingSnapshot(pair.Key, pair.Value)).ToArray(), _construction.ToArray(), Policy,
        _tavernRemainder, _tavernSequence, _candidates.ToArray(), _lockedCandidates.ToArray(),
        _roster.Select(member => member.Capture()).ToArray(), _formation.ToArray(), _cartographyRemainder,
        _eventRemainder, _eventSequence, _eventLog.ToArray(), _milestones.ToArray(), _cartographySequence);

    public void SetPolicy(P9TownPolicy policy)
    {
        if (!Enum.IsDefined(policy)) throw new ArgumentOutOfRangeException(nameof(policy));
        Policy = policy;
    }

    public bool TryStartUpgrade(P9BuildingKind kind, TownEconomyState economy, out string message)
    {
        int current = Level(kind);
        if (current >= 4) { message = "建筑已经达到最高等级。"; return false; }
        if (_construction.Any(job => job.Kind == kind)) { message = "该建筑正在升级。"; return false; }
        if (_construction.Count >= ConstructionSlots) { message = "施工队列已满。"; return false; }
        P9BuildingUpgradeCost cost = NextUpgradeCost(current)!;
        if (!economy.TryPay(cost.Gold, cost.IronScraps)) { message = $"需要 {cost.Gold} 金币和 {cost.IronScraps} 铁屑。"; return false; }
        long duration = cost.DurationMilliseconds;
        if (Policy == P9TownPolicy.Expansion) duration = duration * 80 / 100;
        _construction.Add(new P9ConstructionSnapshot(kind, cost.TargetLevel, duration));
        message = $"{DisplayName(kind)}开始升级至 Lv.{cost.TargetLevel}。";
        return true;
    }

    public P9TownAdvanceResult Advance(long milliseconds, TownEconomyState economy, Action<P1MapItem> addMap)
    {
        if (milliseconds <= 0) return new(0, 0, 0, 0);
        int completed = AdvanceConstruction(milliseconds);
        int refreshes = AdvanceTavern(milliseconds);
        int maps = AdvanceCartography(milliseconds, addMap);
        int events = AdvanceEvents(milliseconds, economy);
        int benchExperience = checked((int)Math.Min(int.MaxValue, milliseconds / 60_000) * (Policy == P9TownPolicy.Training ? 35 : 25));
        foreach (P9MercenaryMember member in _roster.Where(member => !_formation.Contains(member.Identity.StableId))) member.AddExperience(benchExperience);
        return new(maps, refreshes, events, completed);
    }

    public bool ToggleCandidateLock(string stableId)
    {
        if (_lockedCandidates.Remove(stableId)) return true;
        if (_lockedCandidates.Count >= 2 || _candidates.All(candidate => candidate.StableId != stableId)) return false;
        return _lockedCandidates.Add(stableId);
    }

    public bool TryManualRefresh(TownEconomyState economy)
    {
        if (!economy.TrySpendGold(100)) return false;
        _tavernRemainder = 0;
        _tavernSequence++;
        RefreshCandidates();
        return true;
    }

    public bool TryRecruit(string stableId, TownEconomyState economy, out string message)
    {
        P9MercenaryCandidate? candidate = _candidates.FirstOrDefault(item => item.StableId == stableId);
        if (candidate is null) { message = "候选佣兵已经离开。"; return false; }
        if (_roster.Count >= RosterCapacity) { message = "佣兵名册已满。"; return false; }
        if (!economy.TrySpendGold(candidate.RecruitmentCost)) { message = $"需要 {candidate.RecruitmentCost} 金币。"; return false; }
        _roster.Add(new P9MercenaryMember(candidate, 0,
            P9MercenaryFactory.StarterEquipment(_seed ^ StableHash(candidate.StableId))));
        _candidates.Remove(candidate);
        _lockedCandidates.Remove(stableId);
        message = $"{candidate.Name} 已加入佣兵名册。";
        return true;
    }

    public bool TryDismiss(string stableId, EquipmentStorage storage, out string message)
    {
        P9MercenaryMember? member = _roster.FirstOrDefault(item => item.Identity.StableId == stableId);
        if (member is null || _roster.Count <= 3) { message = "至少保留三名佣兵。"; return false; }
        if (_formation.Contains(stableId)) { message = "请先将该佣兵移出阵型。"; return false; }
        ItemInstance[] items = member.Equipment.Items.Values.ToArray();
        if (storage.Capacity - storage.Count < items.Length) { message = "仓库空间不足，无法返还装备。"; return false; }
        foreach (ItemInstance item in items) storage.TryStore(item);
        _roster.Remove(member);
        message = $"{member.Identity.Name} 已离开；装备已返回仓库。";
        return true;
    }

    public bool TryPlaceFormation(string stableId, int slot)
    {
        if (slot is < 0 or >= 6 || slot >= MercenaryCapacity || _roster.All(member => member.Identity.StableId != stableId)) return false;
        int current = Array.IndexOf(_formation, stableId);
        string displaced = _formation[slot];
        _formation[slot] = stableId;
        if (current >= 0 && current != slot) _formation[current] = displaced;
        return true;
    }

    public bool ClearFormationSlot(int slot)
    {
        if (slot is < 0 or >= 6 || _formation.Count(id => !string.IsNullOrEmpty(id)) <= 3) return false;
        _formation[slot] = string.Empty;
        return true;
    }

    public bool TryAddPartyMember(string stableId)
    {
        if (_formation.Contains(stableId) || _roster.All(member => member.Identity.StableId != stableId)) return false;
        int slot = Array.FindIndex(_formation, 0, MercenaryCapacity, string.IsNullOrEmpty);
        if (slot < 0) return false;
        _formation[slot] = stableId;
        return true;
    }

    public bool TryRemovePartyMember(string stableId)
    {
        int slot = Array.IndexOf(_formation, stableId);
        if (slot < 0 || ActiveMembers().Count <= 3) return false;
        _formation[slot] = string.Empty;
        CompactParty();
        return true;
    }

    public IReadOnlyList<P9MercenaryMember> ActiveMembers() => _formation.Where(id => !string.IsNullOrEmpty(id))
        .Select(id => _roster.First(member => member.Identity.StableId == id)).ToArray();

    public void AddActiveExperience(int amount)
    {
        foreach (P9MercenaryMember member in ActiveMembers()) member.AddExperience(amount);
    }

    public P1TeamBuild BuildMercenaryParty(int fallbackLevel)
    {
        IReadOnlyList<P9MercenaryMember> active = ActiveMembers();
        if (active.Count == 0) throw new InvalidOperationException("Mercenary formation is empty.");
        P9MercenaryMember leader = active[0];
        var skill = new SkillConfiguration(P1SkillIds.HeavyStrike, SkillSupport.Bleed);
        AssembledCharacterBuild assembled = CharacterBuildAssembler.Assemble(
            Math.Max(fallbackLevel, leader.Level), leader.Identity.FinalAttributes, leader.Equipment,
            new PassiveTreeAllocation(), skill);
        int extraLife = 0;
        for (int index = 1; index < active.Count; index++)
        {
            P9MercenaryMember member = active[index];
            AssembledCharacterBuild other = CharacterBuildAssembler.Assemble(
                Math.Max(fallbackLevel, member.Level), member.Identity.FinalAttributes, member.Equipment,
                new PassiveTreeAllocation(), skill);
            extraLife = checked(extraLife + other.Sheet.MaximumLife().Value);
        }
        int frontline = Math.Clamp(active.Count(member => member.Identity.Archetype == P9MercenaryArchetype.Guardian), 1, active.Count);
        DefensiveEquipment formationDefense = assembled.Sheet.Equipment with
        {
            Armor = checked(assembled.Sheet.Equipment.Armor + frontline * 20),
            Evasion = checked(assembled.Sheet.Equipment.Evasion + (active.Count - frontline) * 12),
        };
        CharacterSheet partySheet = assembled.Sheet with
        {
            FlatMaximumLife = checked(assembled.Sheet.FlatMaximumLife + extraLife),
            Equipment = formationDefense,
        };
        return new P1TeamBuild(
            partySheet,
            assembled.EffectiveWeapon,
            skill,
            FlatAccuracy: 70 + assembled.FlatAccuracy,
            IncreasedDamageBasisPoints: checked(assembled.IncreasedAttackDamageBasisPoints + (active.Count - 1) * 6_000),
            IncreasedCriticalChanceBasisPoints: assembled.IncreasedCriticalChanceBasisPoints,
            IncreasedBleedChanceBasisPoints: assembled.IncreasedBleedChanceBasisPoints,
            UseWarCry: true,
            AiSummary: $"{active.Count} 人佣兵队；AI 自动安排 {frontline} 名前排；技能和目标由成员自主决定。",
            LifeFlask: new LifeFlaskDefinition(40, 30, 10),
            IncreasedLifeFlaskEffectBasisPoints: assembled.Equipment.Modifiers.IncreasedLifeFlaskEffectBasisPoints,
            AddedPhysicalDamage: checked(assembled.AddedPhysicalDamage + (active.Count - 1) * 2),
            HeavyStrikeProfile: assembled.HeavyStrike,
            MovementSpeedBasisPoints: 10_000,
            ActiveSkills: [skill, new SkillConfiguration(P1SkillIds.WarCry, SkillSupport.IncreasedArea)],
            PartySize: active.Count,
            FrontlineCount: frontline,
            HasShield: assembled.Equipment.HasShield,
            BlockChanceBasisPoints: assembled.Equipment.HasShield ? 2_000 : 0,
            HasUsableWeapon: assembled.HasUsableWeapon,
            LocalWeaponStats: assembled.Equipment.LocalWeapon,
            CombatEquipment: assembled.CombatEquipment,
            VirtueViceLoadout: assembled.VirtueViceLoadout);
    }

    private void CompactParty()
    {
        string[] active = _formation.Where(id => !string.IsNullOrEmpty(id)).Take(MercenaryCapacity).ToArray();
        Array.Clear(_formation);
        for (int index = 0; index < active.Length; index++) _formation[index] = active[index];
    }

    public bool TryTransmute(TownEconomyState economy, MetalCurrencyKind output)
    {
        P9MetalTransmutationRecipe recipe = TransmutationRecipes.FirstOrDefault(item => item.Output == output)
            ?? throw new ArgumentOutOfRangeException(nameof(output));
        if (Level(P9BuildingKind.Alchemy) < recipe.AlchemyLevel ||
            economy.MetalAmount(recipe.Input) < recipe.InputCount || economy.Gold < recipe.GoldCost) return false;
        if (!economy.TrySpendMetal(recipe.Input, recipe.InputCount) || !economy.TrySpendGold(recipe.GoldCost))
            throw new InvalidOperationException("Alchemy payment became inconsistent.");
        economy.AddMetal(output, 1);
        return true;
    }

    public bool RecordMilestone(string stableId, TownEconomyState economy)
    {
        if (!_milestones.Add(stableId)) return false;
        int level = Level(P9BuildingKind.Reliquary);
        economy.AddDispositionProceeds(25 * level, 0);
        return true;
    }

    private int AdvanceConstruction(long milliseconds)
    {
        int completed = 0;
        for (int index = _construction.Count - 1; index >= 0; index--)
        {
            P9ConstructionSnapshot job = _construction[index];
            long remaining = job.RemainingMilliseconds - milliseconds;
            if (remaining > 0) { _construction[index] = job with { RemainingMilliseconds = remaining }; continue; }
            _buildings[job.Kind] = job.TargetLevel;
            _construction.RemoveAt(index);
            AddEvent($"{DisplayName(job.Kind)}已升级至 Lv.{job.TargetLevel}。", false);
            completed++;
        }
        return completed;
    }

    private int AdvanceTavern(long milliseconds)
    {
        _tavernRemainder += milliseconds;
        int refreshes = 0;
        while (_tavernRemainder >= TavernRefreshIntervalMilliseconds)
        {
            _tavernRemainder -= TavernRefreshIntervalMilliseconds;
            _tavernSequence++;
            RefreshCandidates();
            refreshes++;
        }
        return refreshes;
    }

    private int AdvanceCartography(long milliseconds, Action<P1MapItem> addMap)
    {
        _cartographyRemainder += milliseconds;
        long interval = 30 * 60_000 / Level(P9BuildingKind.Cartography);
        if (Policy == P9TownPolicy.Expedition) interval = interval * 80 / 100;
        int maps = 0;
        while (_cartographyRemainder >= interval)
        {
            _cartographyRemainder -= interval;
            int sequence = _cartographySequence++;
            int tier = Math.Min(10, Level(P9BuildingKind.Cartography) * 2 + sequence % 3);
            addMap(new P1MapItem($"p9-cartography-{sequence}", Math.Max(1, tier)));
            maps++;
        }
        return maps;
    }

    private int AdvanceEvents(long milliseconds, TownEconomyState economy)
    {
        _eventRemainder += milliseconds;
        int count = 0;
        while (_eventRemainder >= EventInterval(_eventSequence))
        {
            _eventRemainder -= EventInterval(_eventSequence);
            var random = new Pcg32(_seed ^ (ulong)_eventSequence++ * 0x517cc1b727220a95UL);
            int kind = (int)(random.NextUInt() % 6);
            switch (kind)
            {
                case 0: economy.AddDispositionProceeds(15 * Level(P9BuildingKind.Tavern), 0); AddEvent("酒馆商队带来一笔安全交易。", true); break;
                case 1: economy.AddMetal(MetalCurrencyKind.AwakeningCopper, 1); AddEvent("拾荒者交付了一块启灵铜。", true); break;
                case 2: economy.AddDispositionProceeds(20, 0); AddEvent("门扉巡逻队带回了遗失的金币。", true); break;
                case 3: economy.AddDispositionProceeds(0, 3); AddEvent("工匠学徒回收了铁屑。", true); break;
                case 4: economy.AddMetal(MetalCurrencyKind.PolishingCobalt, 1); AddEvent("炼金所冷凝出一份精磨钴。", true); break;
                default: economy.AddDispositionProceeds(10, 0); AddEvent("遗物馆展览吸引了旅人。", true); break;
            }
            count++;
        }
        return count;
    }

    private void RefreshCandidates()
    {
        P9MercenaryCandidate[] locked = _candidates.Where(candidate => _lockedCandidates.Contains(candidate.StableId)).ToArray();
        _candidates.Clear();
        _candidates.AddRange(locked);
        int index = 0;
        while (_candidates.Count < 4)
        {
            P9MercenaryCandidate candidate = P9MercenaryFactory.Generate(_seed ^ (ulong)_tavernSequence * 0x9e3779b97f4a7c15UL, index++, starter: false);
            if (_candidates.All(item => item.StableId != candidate.StableId)) _candidates.Add(candidate);
        }
    }

    private void AddEvent(string text, bool includePolicy)
    {
        _eventLog.Add(includePolicy ? $"{text}（{PolicyName(Policy)}方针自动处理）" : text);
        if (_eventLog.Count > 50) _eventLog.RemoveAt(0);
    }

    private static long EventInterval(int sequence) => (30 + sequence * 17 % 61) * 60_000L;
    private static ulong StableHash(string value)
    {
        ulong hash = 14_695_981_039_346_656_037UL;
        foreach (char character in value)
        {
            hash ^= character;
            hash *= 1_099_511_628_211UL;
        }
        return hash;
    }
    public static string DisplayName(P9BuildingKind kind) => kind switch
    {
        P9BuildingKind.Tavern => "酒馆", P9BuildingKind.Workshop => "工坊", P9BuildingKind.Alchemy => "炼金所",
        P9BuildingKind.Cartography => "制图室", P9BuildingKind.Storage => "仓库", P9BuildingKind.Reliquary => "遗物馆",
        P9BuildingKind.Teleporter => "传送装置", _ => kind.ToString(),
    };
    public static string PolicyName(P9TownPolicy policy) => policy switch
    { P9TownPolicy.Expansion => "开拓", P9TownPolicy.Expedition => "远征", P9TownPolicy.Training => "练兵", _ => policy.ToString() };
}

public static class P9MercenaryFactory
{
    private static readonly string[] Names = ["伊莱娅", "赫恩", "米蕾", "奥兰", "塞芙", "塔维", "洛迦", "奈芙", "柯林", "芮安"];
    private static readonly string[] Positives = ["沉着", "守序", "嘹亮", "坚韧", "迅捷", "洞察", "不屈"];
    private static readonly string[] Negatives = ["寡言", "旧伤", "固执", "畏暗", "鲁莽", "迟疑", "孤僻"];

    public static P9MercenaryCandidate Generate(ulong seed, int sequence, bool starter)
    {
        var random = new Pcg32(seed ^ (ulong)sequence * 0xd1b54a32d192ed03UL);
        P9MercenaryArchetype archetype = (P9MercenaryArchetype)(random.NextUInt() % 4);
        P9MercenaryPotential potential = starter ? P9MercenaryPotential.Promising : RollPotential(random);
        int level = starter ? 1 : 1 + (int)(random.NextUInt() % 10);
        int bonus = (int)potential * 2;
        CharacterAttributes attributes = archetype switch
        {
            P9MercenaryArchetype.Guardian => new(20 + bonus, 10, 10, 12),
            P9MercenaryArchetype.Ranger => new(12, 20 + bonus, 10, 10),
            P9MercenaryArchetype.Cantor => new(12, 10, 20 + bonus, 12),
            _ => new(10, 12, 12, 20 + bonus),
        };
        string name = Names[(int)(random.NextUInt() % (uint)Names.Length)];
        string positive = Positives[(int)(random.NextUInt() % (uint)Positives.Length)];
        string negative = random.NextBasisPoints() < 4_000 ? Negatives[(int)(random.NextUInt() % (uint)Negatives.Length)] : string.Empty;
        int cost = 50 + level * 10 + (int)potential * 150;
        string id = $"p9-merc-{seed:x16}-{sequence}-{(int)archetype}";
        return new(id, name, archetype, potential, level, attributes, positive, negative,
            SkillSummary(archetype), AiSummary(archetype), starter ? 0 : cost);
    }

    public static EquipmentLoadout StarterEquipment(ulong seed)
    {
        var equipment = new EquipmentLoadout();
        Equip(equipment, EquipmentSlot.MainHand, "core.base.rusted_greatsword", seed);
        Equip(equipment, EquipmentSlot.Chest, "core.base.crude_chainmail", seed + 1);
        Equip(equipment, EquipmentSlot.Helmet, "core.base.iron_helmet", seed + 2);
        Equip(equipment, EquipmentSlot.Flask1, "core.base.life_flask", seed + 3);
        return equipment;
    }

    private static void Equip(EquipmentLoadout equipment, EquipmentSlot slot, string baseId, ulong seed)
    {
        ItemInstance item = ItemGenerator.Generate(baseId, 1, ItemRarity.Basic, seed, $"p9-merc-item-{seed:x16}-{slot}");
        if (!equipment.TryEquip(slot, item)) throw new InvalidOperationException("P9 starter equipment is invalid.");
    }
    private static P9MercenaryPotential RollPotential(Pcg32 random) => random.NextBasisPoints() switch
    { < 6_000 => P9MercenaryPotential.Common, < 9_000 => P9MercenaryPotential.Promising, < 9_900 => P9MercenaryPotential.Exceptional, _ => P9MercenaryPotential.Legendary };
    private static string SkillSummary(P9MercenaryArchetype kind) => kind switch
    { P9MercenaryArchetype.Guardian => "重击 · 战吼 · 护卫", P9MercenaryArchetype.Ranger => "幽魂飞刃 · 连锁 · 位移", P9MercenaryArchetype.Cantor => "战吼 · 范围增幅 · 回复", _ => "灵能投射 · 护盾 · 范围压制" };
    private static string AiSummary(P9MercenaryArchetype kind) => kind switch
    { P9MercenaryArchetype.Guardian => "优先前排接敌并保护后排", P9MercenaryArchetype.Ranger => "保持距离并优先精英", P9MercenaryArchetype.Cantor => "覆盖队友后处理密集敌群", _ => "维持护盾并攻击最远目标" };
}
