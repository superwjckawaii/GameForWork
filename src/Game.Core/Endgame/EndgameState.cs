using GameForWork.Core.Campaign.World;
using GameForWork.Core.Maps;
using GameForWork.Core.Ascendancies;
using GameForWork.Core.Economy;
using GameForWork.Core.Atlas;
using GameForWork.Core.Encounters;

namespace GameForWork.Core.Endgame;

public enum MapMechanic { Abyss, LifeGarden, RedAltar, BlueAltar, Warfront }
public enum AtlasTheme { MapBasics, MapSupply, Crafting, PacksAndElites, Boss, Abyss, LifeGarden, RedAltar, BlueAltar, Warfront }

public sealed record AtlasPassiveNode(
    string StableId,
    string DisplayName,
    AtlasTheme Theme,
    int Ring,
    int OrbitIndex,
    string? PrerequisiteId,
    int RewardBasisPoints,
    bool Notable,
    float X,
    float Y,
    int MechanicWeightBasisPoints = 0,
    bool BlocksCompetingMechanic = false,
    string SpecialRule = "",
    int GoldCost = 0,
    AtlasGate Gate = AtlasGate.Act5,
    int Position = 1);

public static class AtlasTree
{
    public const float LayoutExtent = 720f;

    private static readonly IReadOnlyDictionary<string, AtlasPassiveNode> NodeMap = Build()
        .ToDictionary(node => node.StableId, StringComparer.Ordinal);
    public static IReadOnlyCollection<AtlasPassiveNode> Nodes => NodeMap.Values.ToArray();
    public static AtlasPassiveNode Get(string id) => NodeMap.TryGetValue(id, out AtlasPassiveNode? node)
        ? node : throw new KeyNotFoundException($"Unknown atlas passive: {id}");

    private static IReadOnlyList<AtlasPassiveNode> Build()
    {
        return AtlasCatalog.All.Select(node =>
        {
            int lane = (int)node.Category;
            float x = -630 + lane * 140;
            float y = -550 + (node.Position - 1) * 100;
            AtlasTheme theme = node.Category switch
            {
                AtlasCategory.MapBasics => AtlasTheme.MapBasics,
                AtlasCategory.Supply => AtlasTheme.MapSupply,
                AtlasCategory.Crafting => AtlasTheme.Crafting,
                AtlasCategory.PacksAndElites => AtlasTheme.PacksAndElites,
                AtlasCategory.Boss => AtlasTheme.Boss,
                AtlasCategory.Abyss => AtlasTheme.Abyss,
                AtlasCategory.LifeGarden => AtlasTheme.LifeGarden,
                AtlasCategory.RedOath => AtlasTheme.RedAltar,
                AtlasCategory.BlueOath => AtlasTheme.BlueAltar,
                _ => AtlasTheme.Warfront,
            };
            return new AtlasPassiveNode(node.StableId, node.DisplayName, theme, node.Position / 4, lane * 12 + node.Position,
                node.PrerequisiteId, 0, node.Position is 4 or 8 or 11 or 12, x, y, 0, false, node.Effect,
                node.GoldCost, node.Gate, node.Position);
        }).ToArray();
    }
}

public sealed record EndgameSnapshot(
    IReadOnlyList<int> CompletedTiers,
    IReadOnlyList<string> AtlasPassives,
    IReadOnlyDictionary<MapMechanic, int> MechanicEncounters,
    int LifeForce,
    int RedFavor,
    int BlueFavor,
    int CitadelFragments,
    int CitadelTickets,
    bool CitadelDefeated,
    IReadOnlyList<string> AscendancyPassives,
    int BreakthroughPoints,
    bool FinalBreakthroughCompleted = false,
    IReadOnlyList<AtlasSchemeSnapshot>? AtlasSchemes = null,
    int ActiveAtlasSchemeIndex = 0,
    int CitadelVictories = 0,
    int MythicReforgeMaterials = 0,
    bool MythicGranted = false,
    int BreakthroughAttempts = 0,
    int BreakthroughVictories = 0,
    int BonusAtlasPoints = 0,
    Ascendancy SelectedAscendancy = Ascendancy.None,
    bool Act3AscendancyAwarded = false,
    bool Act5AscendancyAwarded = false,
    bool WarfrontDiscovered = false,
    int WarfrontMerit = 0,
    int WarfrontReputation = 0,
    bool WarfrontGuaranteeIssued = false,
    IReadOnlyDictionary<RewardPreference, int>? BlueMisses = null,
    long GameplayOperationSequence = 0,
    string LastWarfrontBaseId = "", CombatConfiguration? CombatConfiguration = null);

public sealed record AtlasSchemeSnapshot(string Name, IReadOnlyList<string> AllocatedPassives);

public sealed class EndgameState
{
    public const int CitadelFragmentsPerTicket = 8;
    public const string CitadelMapPrefix = "endgame-ashen-citadel-";
    public const string CitadelPracticeMapPrefix = "content-ashen-practice-";
    public const string BreakthroughMapPrefix = "content-gate-trial-";
    private readonly HashSet<int> _completedTiers = [];
    private readonly HashSet<string> _atlas = new(StringComparer.Ordinal);
    private readonly Dictionary<MapMechanic, int> _mechanics = Enum.GetValues<MapMechanic>().ToDictionary(kind => kind, _ => 0);
    private readonly HashSet<string> _ascendancy = new(StringComparer.Ordinal);
    public IReadOnlySet<int> CompletedTiers => _completedTiers;
    public IReadOnlySet<string> AtlasPassives => _atlas;
    public IReadOnlyDictionary<MapMechanic, int> MechanicEncounters => _mechanics;
    public IReadOnlySet<string> AscendancyPassives => _ascendancy;
    public int EarnedAtlasPoints => AtlasCatalog.MaximumNodes;
    public int LifeForce { get; private set; }
    public int RedFavor { get; private set; }
    public int BlueFavor { get; private set; }
    public int CitadelFragments { get; private set; }
    public int CitadelTickets { get; private set; }
    public bool CitadelDefeated { get; private set; }
    public int BreakthroughPoints { get; private set; }
    public bool FinalBreakthroughCompleted { get; private set; }
    public int CitadelVictories { get; private set; }
    public int MythicReforgeMaterials { get; private set; }
    public bool MythicGranted { get; private set; }
    public int BreakthroughAttempts { get; private set; }
    public int BreakthroughVictories { get; private set; }
    public int BonusAtlasPoints { get; private set; }
    public Ascendancy SelectedAscendancy { get; private set; }
    public CombatConfiguration CombatConfiguration { get; private set; } = new();
    public bool ConfigureCombat(CombatConfiguration configuration)
    {
        if (!configuration.Valid || SelectedAscendancy is not (Ascendancy.PhantomMaster or Ascendancy.IdolForger)) return false;
        CombatConfiguration = configuration.Snapshot();
        return true;
    }
    public bool Act3AscendancyAwarded { get; private set; }
    public bool Act5AscendancyAwarded { get; private set; }
    public bool WarfrontDiscovered { get; private set; }
    public int WarfrontMerit { get; private set; }
    public int WarfrontReputation { get; private set; }
    public bool WarfrontGuaranteeIssued { get; private set; }
    private readonly Dictionary<RewardPreference, int> _blueMisses = [];
    public IReadOnlyDictionary<RewardPreference, int> BlueMisses => _blueMisses;
    public long GameplayOperationSequence { get; private set; }
    public string LastWarfrontBaseId { get; private set; } = string.Empty;
    public void CompleteGameplayOperation() => GameplayOperationSequence = checked(GameplayOperationSequence + 1);
    public void RecordWarfrontBase(string stableId) { LastWarfrontBaseId = stableId; CompleteGameplayOperation(); }
    public void DiscoverWarfront() => WarfrontDiscovered = true;
    public int SupplyTier => WarfrontReputation >= 60 ? 3 : WarfrontReputation >= 15 ? 2 : 1;
    public bool TrySpendWarfrontMerit(int amount)
    {
        if (amount <= 0 || WarfrontMerit < amount) return false;
        WarfrontMerit -= amount; return true;
    }
    public bool RecordGameplay(RewardLedger earned, bool bluePityUnlocked)
    {
        LifeForce = checked(LifeForce + earned.LifeForce); RedFavor = checked(RedFavor + earned.RedFavor);
        BlueFavor = checked(BlueFavor + earned.BlueFavor); WarfrontMerit = checked(WarfrontMerit + earned.Merit);
        WarfrontReputation = checked(WarfrontReputation + earned.Reputation);
        AddFragments(earned.Fragments);
        foreach (Mechanic mechanic in earned.Encounters.Where(e => e.Kills > 0).Select(e => e.Node.Gameplay!.Mechanic).Distinct())
        {
            MapMechanic? kind = mechanic switch { Mechanic.Abyss => MapMechanic.Abyss,
                Mechanic.Garden => MapMechanic.LifeGarden, Mechanic.Red => MapMechanic.RedAltar,
                Mechanic.Blue => MapMechanic.BlueAltar, Mechanic.Warfront => MapMechanic.Warfront, _ => null };
            if (kind is not null) _mechanics[kind.Value]++;
        }
        if (!bluePityUnlocked || earned.BlueTarget is null) return false;
        RewardPreference target = earned.BlueTarget.Value;
        if (earned.BlueTargetHit) { _blueMisses[target] = 0; return false; }
        int misses = _blueMisses.GetValueOrDefault(target) + 1;
        _blueMisses[target] = misses >= 10 ? 0 : misses;
        return misses >= 10;
    }
    private void AddFragments(int count)
    {
        CitadelFragments = checked(CitadelFragments + count);
        CitadelTickets = checked(CitadelTickets + CitadelFragments / CitadelFragmentsPerTicket);
        CitadelFragments %= CitadelFragmentsPerTicket;
    }
    public const int MaximumAscendancyPoints = 8;

    public IReadOnlyList<MapMechanic> RecordMapCompletion(MapItem map, MapRoute route, ulong seed)
    {
        map.Validate();
        _completedTiers.Add(map.Tier);
        var selected = new List<MapMechanic>(2);
        if (route == MapRoute.Abyss) selected.Add(MapMechanic.Abyss);
        if (route == MapRoute.LifeGarden) selected.Add(MapMechanic.LifeGarden);
        if (route == MapRoute.Warfront) selected.Add(MapMechanic.Warfront);
        if (map.Altar == MapAltar.RedOath) selected.Add(MapMechanic.RedAltar);
        if (map.Altar == MapAltar.BlueOath) selected.Add(MapMechanic.BlueAltar);
        MapMechanic[] result = selected.Distinct().Take(3).ToArray();
        // Progression only. Mechanic rewards require a combat ledger, never a random phantom encounter.
        if (map.Tier >= 11) AddFragments(1);
        return result;
    }

    public void RecordWarfrontAttempt(int tier, bool succeeded)
    {
        if (tier is < 1 or > 20) throw new ArgumentOutOfRangeException(nameof(tier));
        WarfrontDiscovered = true;
        WarfrontMerit = checked(WarfrontMerit + tier * (succeeded ? 10 : 0));
        if (succeeded) WarfrontReputation = checked(WarfrontReputation + 1 + tier / 5);
    }

    public void MarkWarfrontGuaranteeIssued() => WarfrontGuaranteeIssued = true;

    public bool TryPurchaseAtlas(string id, TownEconomyState economy, bool warfrontDiscovered = false) =>
        AtlasPurchase.TryPurchase(_atlas, AtlasCatalog.Get(id), economy,
            _completedTiers.DefaultIfEmpty(0).Max(), FinalBreakthroughCompleted, warfrontDiscovered);

    public bool TryCompleteFinalBreakthrough(int level, bool trialWon)
    {
        if (level < 100) return false;
        BreakthroughAttempts++;
        if (!trialWon) return false;
        BreakthroughVictories++;
        if (FinalBreakthroughCompleted) return true;
        FinalBreakthroughCompleted = true;
        AwardBreakthroughPoint(2);
        return true;
    }

    public bool TrySelectAscendancy(Ascendancy ascendancy)
    {
        if (ascendancy == Ascendancy.None || SelectedAscendancy != Ascendancy.None || BreakthroughPoints <= 0)
            return false;
        SelectedAscendancy = ascendancy;
        return true;
    }

    public bool TryAllocateAscendancy(string id)
    {
        AscendancyNode node = WarriorAscendancyCatalog.Get(id);
        if (SelectedAscendancy == Ascendancy.None || node.Ascendancy != SelectedAscendancy) return false;
        if (_ascendancy.Contains(id) || _ascendancy.Count >= BreakthroughPoints || node.PrerequisiteId is not null && !_ascendancy.Contains(node.PrerequisiteId)) return false;
        return _ascendancy.Add(id);
    }

    public bool TryRefundAscendancy(string id)
    {
        if (!_ascendancy.Contains(id) || WarriorAscendancyCatalog.For(SelectedAscendancy)
                .Any(node => node.PrerequisiteId == id && _ascendancy.Contains(node.StableId))) return false;
        return _ascendancy.Remove(id);
    }

    public void ResetAscendancy(bool clearSelection)
    {
        _ascendancy.Clear();
        if (clearSelection) { SelectedAscendancy = Ascendancy.None; CombatConfiguration = new(); }
    }

    public bool AwardCampaignAscendancyPoints(int act)
    {
        if (act == 3 && !Act3AscendancyAwarded)
        {
            Act3AscendancyAwarded = true;
            AwardBreakthroughPoint(2);
            return true;
        }
        if (act == 5 && !Act5AscendancyAwarded)
        {
            Act5AscendancyAwarded = true;
            AwardBreakthroughPoint(2);
            return true;
        }
        return false;
    }

    public void AwardBreakthroughPoint(int count = 1) => BreakthroughPoints = Math.Min(MaximumAscendancyPoints, BreakthroughPoints + Math.Max(0, count));
    public bool TrySpendLifeForce(int amount)
    {
        if (amount <= 0 || LifeForce < amount) return false;
        LifeForce -= amount;
        return true;
    }
    public bool TrySpendRedFavor(int amount)
    {
        if (amount <= 0 || RedFavor < amount) return false;
        RedFavor -= amount; return true;
    }
    public bool TrySpendBlueFavor(int amount)
    {
        if (amount <= 0 || BlueFavor < amount) return false;
        BlueFavor -= amount; return true;
    }
    public bool TryConsumeCitadelTicket()
    {
        if (CitadelTickets <= 0) return false;
        CitadelTickets--;
        return true;
    }

    public bool RecordCitadelVictory()
    {
        bool first = !CitadelDefeated;
        CitadelDefeated = true;
        CitadelVictories++;
        if (first)
        {
            BonusAtlasPoints += 5;
            AwardBreakthroughPoint(2);
        }
        else
        {
            MythicReforgeMaterials++;
        }
        return first;
    }

    public static bool IsCitadel(MapItem map) => Persistence.SaveIdentifierMigration.Normalize(map.InstanceId).StartsWith(CitadelMapPrefix, StringComparison.Ordinal);
    public static bool IsCitadelPractice(MapItem map) => Persistence.SaveIdentifierMigration.Normalize(map.InstanceId).StartsWith(CitadelPracticeMapPrefix, StringComparison.Ordinal);
    public static bool IsBreakthroughTrial(MapItem map) => Persistence.SaveIdentifierMigration.Normalize(map.InstanceId).StartsWith(BreakthroughMapPrefix, StringComparison.Ordinal);

    public EndgameSnapshot Capture() => new(_completedTiers.Order().ToArray(), AtlasPassives.Order().ToArray(),
        new Dictionary<MapMechanic, int>(_mechanics), LifeForce, RedFavor, BlueFavor, CitadelFragments,
        CitadelTickets, CitadelDefeated, _ascendancy.Order().ToArray(), BreakthroughPoints,
        FinalBreakthroughCompleted,
        null,
        0, CitadelVictories, MythicReforgeMaterials, MythicGranted,
        BreakthroughAttempts, BreakthroughVictories, BonusAtlasPoints, SelectedAscendancy,
        Act3AscendancyAwarded, Act5AscendancyAwarded, WarfrontDiscovered,
        WarfrontMerit, WarfrontReputation, WarfrontGuaranteeIssued, new Dictionary<RewardPreference, int>(_blueMisses), GameplayOperationSequence, LastWarfrontBaseId, CombatConfiguration.Snapshot());

    public static EndgameState Restore(EndgameSnapshot? snapshot)
    {
        var state = new EndgameState();
        if (snapshot is null) return state;
        if (snapshot.CompletedTiers.Any(tier => tier is < 1 or > 20) || snapshot.AtlasPassives.Count > AtlasCatalog.MaximumNodes ||
            snapshot.CitadelFragments is < 0 or >= CitadelFragmentsPerTicket || snapshot.CitadelTickets < 0 ||
            snapshot.BreakthroughPoints is < 0 or > MaximumAscendancyPoints || snapshot.AscendancyPassives.Count > snapshot.BreakthroughPoints ||
            snapshot.CitadelVictories < 0 || snapshot.MythicReforgeMaterials < 0 || snapshot.BreakthroughAttempts < 0 ||
            snapshot.BreakthroughVictories < 0 || snapshot.BonusAtlasPoints is < 0 or > 5 || !Enum.IsDefined(snapshot.SelectedAscendancy) ||
            snapshot.WarfrontMerit < 0 || snapshot.WarfrontReputation < 0 || snapshot.GameplayOperationSequence < 0 ||
            (snapshot.BlueMisses?.Any(p => !Enum.IsDefined(p.Key) || p.Value is < 0 or > 9) ?? false))
            throw new InvalidDataException("Endgame endgame snapshot is invalid.");
        foreach (int tier in snapshot.CompletedTiers) state._completedTiers.Add(tier);
        IEnumerable<string> migratedAtlas = snapshot.AtlasPassives
            .Concat(snapshot.AtlasSchemes?.SelectMany(scheme => scheme.AllocatedPassives) ?? [])
            .Where(id => id.StartsWith("atlas.atlas.", StringComparison.Ordinal)).Distinct(StringComparer.Ordinal);
        foreach (string id in migratedAtlas) { AtlasTree.Get(id); state._atlas.Add(id); }
        foreach (MapMechanic kind in Enum.GetValues<MapMechanic>()) state._mechanics[kind] = snapshot.MechanicEncounters.GetValueOrDefault(kind);
        state.LifeForce = snapshot.LifeForce; state.RedFavor = snapshot.RedFavor; state.BlueFavor = snapshot.BlueFavor;
        state.CitadelFragments = snapshot.CitadelFragments; state.CitadelTickets = snapshot.CitadelTickets; state.CitadelDefeated = snapshot.CitadelDefeated;
        state.BreakthroughPoints = snapshot.BreakthroughPoints;
        state.FinalBreakthroughCompleted = snapshot.FinalBreakthroughCompleted;
        state.CitadelVictories = snapshot.CitadelVictories;
        state.MythicReforgeMaterials = snapshot.MythicReforgeMaterials;
        state.MythicGranted = snapshot.MythicGranted;
        state.BreakthroughAttempts = snapshot.BreakthroughAttempts;
        state.BreakthroughVictories = snapshot.BreakthroughVictories;
        state.BonusAtlasPoints = snapshot.BonusAtlasPoints > 0 ? snapshot.BonusAtlasPoints : snapshot.CitadelDefeated ? 5 : 0;
        state.SelectedAscendancy = snapshot.SelectedAscendancy;
        if (snapshot.CombatConfiguration is { } configuration)
        {
            if (!configuration.Valid) throw new InvalidDataException("Invalid ascendancy combat configuration.");
            state.CombatConfiguration = configuration.Snapshot();
        }
        state.Act3AscendancyAwarded = snapshot.Act3AscendancyAwarded;
        state.Act5AscendancyAwarded = snapshot.Act5AscendancyAwarded;
        state.WarfrontDiscovered = snapshot.WarfrontDiscovered;
        state.WarfrontMerit = snapshot.WarfrontMerit;
        state.WarfrontReputation = snapshot.WarfrontReputation;
        state.WarfrontGuaranteeIssued = snapshot.WarfrontGuaranteeIssued;
        state.GameplayOperationSequence = snapshot.GameplayOperationSequence;
        state.LastWarfrontBaseId = snapshot.LastWarfrontBaseId ?? string.Empty;
        foreach (var pair in snapshot.BlueMisses ?? new Dictionary<RewardPreference, int>()) state._blueMisses[pair.Key] = pair.Value;
        foreach (string id in snapshot.AscendancyPassives)
        {
            if (id.StartsWith("core.ascendancy.iron_oath.", StringComparison.Ordinal)) continue;
            AscendancyNode node = WarriorAscendancyCatalog.Get(id);
            if (node.Ascendancy != state.SelectedAscendancy) throw new InvalidDataException("Ascendancies ascendancy node belongs to another path.");
            state._ascendancy.Add(id);
        }
        return state;
    }
}
