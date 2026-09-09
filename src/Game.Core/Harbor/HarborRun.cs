using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Spatial;

namespace GameForWork.Core.Harbor;

public sealed record HarborDifficulty(int Level, int Fee, int ItemLevel, int LifeMultiplier, int DamageMultiplier)
{
    public static IReadOnlyList<HarborDifficulty> All { get; } =
        [new(1, 1_000, 100, 10_000, 10_000), new(2, 3_000, 110, 15_000, 12_500), new(3, 8_000, 120, 22_000, 16_000)];
    public static HarborDifficulty Get(int level) => All.Single(value => value.Level == level);
}

public sealed record HarborRegion(string Name, long StartsAtMilliseconds, NodeCombatResult Combat);
public sealed record HarborRun(string Id, ulong Seed, int Difficulty, int PaidGold, long DurationMilliseconds,
    bool Succeeded, IReadOnlyList<HarborRegion> Regions, IReadOnlyList<ItemInstance> Candidates, string Checksum)
{
    public string CalculateChecksum() => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(this with { Checksum = "" }))));
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsValid => !string.IsNullOrWhiteSpace(Id) && Difficulty is >= 1 and <= 3 && PaidGold == HarborDifficulty.Get(Difficulty).Fee &&
        DurationMilliseconds is > 0 and <= 300_000 && Regions is { Count: >= 1 and <= 3 } && Candidates is { Count: 3 } &&
        Regions.All(region => region is not null && region.Combat is not null) && Candidates.All(item => item is not null && item.Base is not null) &&
        Candidates.Select(item => item.Base.StableId).Distinct().Count() == 3 && Checksum == CalculateChecksum();
}

public static class HarborRunner
{
    public static IReadOnlyList<string> CandidateBaseIds { get; } =
    [
        "harbor.base.tidewalker_rapier", "harbor.base.cablecleaver_axe", "harbor.base.returning_tide_sword",
        "harbor.base.sunken_anchor_maul", "harbor.base.tideskimmer_bow", "harbor.base.tidal_wand",
        "harbor.base.deep_tide_staff", "harbor.base.returning_tide_shield", "harbor.base.downstream_quiver",
        "harbor.base.still_tide_hood", "harbor.base.ballast_plate", "harbor.base.wavebreaker_gloves",
        "harbor.base.tidewading_boots", "harbor.base.wavechaser_ring", "harbor.base.harbor_guard_ring",
        "harbor.base.three_tides_amulet", "harbor.base.backflow_belt", "harbor.base.anchored_belt",
    ];
    public static IReadOnlyList<string> RegionNames { get; } = ["外港栈桥", "沉没仓区", "宝库内港"];

    public static HarborRun Run(string id, int difficulty, TeamBuild build, ulong seed)
    {
        HarborDifficulty config = HarborDifficulty.Get(difficulty);
        var regions = new List<HarborRegion>();
        long elapsed = 0;
        int? life = null, mana = null, shield = null;
        bool succeeded = true;
        for (int region = 0; region < 3; region++)
        {
            var request = new NodeCombatRequest(build, region + 1, config.ItemLevel, 3 + difficulty + region,
                true, true, false, 1, life, mana, shield, (int)((300_000 - elapsed) / 50),
                EnemyLifeBasisPoints: config.LifeMultiplier, EnemyDamageBasisPoints: config.DamageMultiplier,
                Objective: new(new(6_000, 2_000), new(11_000, 2_000),
                    HazardIntervalTicks: 160 - difficulty * 20,
                    HazardDamage: 100 * config.DamageMultiplier / 10_000));
            NodeCombatResult combat = new SpatialCombatRunner().Run(request, seed + (ulong)region);
            regions.Add(new(RegionNames[region], elapsed, combat));
            elapsed += Math.Max(1, combat.Ticks) * 50L;
            life = combat.HeroLife; mana = combat.HeroMana; shield = combat.HeroShield;
            if (combat.Outcome != BattleOutcome.HeroVictory || elapsed >= 300_000)
            {
                succeeded = false;
                break;
            }
        }
        var random = new GameForWork.Core.Simulation.Pcg32(seed ^ 0x686172626f72UL);
        string[] selectedBases = SelectCandidateBaseIds(random);
        ItemInstance[] candidates = selectedBases.Select((baseId, index) => ItemGenerator.Generate(baseId,
            config.ItemLevel, ItemRarity.Rare, seed + (ulong)index, $"{id}.candidate.{index}") with
            { DropSource = "沉金港", Quality = random.NextBasisPoints() < (difficulty switch { 1 => 1000, 2 => 2500, _ => 5000 }) ? 20 : 0 }).ToArray();
        for (int index = candidates.Length - 1; index > 0; index--)
        {
            int swap = (int)(random.NextUInt() % (uint)(index + 1));
            (candidates[index], candidates[swap]) = (candidates[swap], candidates[index]);
        }
        var run = new HarborRun(id, seed, difficulty, config.Fee, elapsed, succeeded, regions, candidates, "");
        return run with { Checksum = run.CalculateChecksum() };
    }

    public static string[] SelectCandidateBaseIds(ulong seed) =>
        SelectCandidateBaseIds(new GameForWork.Core.Simulation.Pcg32(seed ^ 0x686172626f72UL));

    private static string[] SelectCandidateBaseIds(GameForWork.Core.Simulation.Pcg32 random)
    {
        string[] pool = CandidateBaseIds.ToArray();
        for (int index = pool.Length - 1; index > 0; index--)
        {
            int selected = (int)(random.NextUInt() % (uint)(index + 1));
            (pool[index], pool[selected]) = (pool[selected], pool[index]);
        }
        return pool.Take(3).ToArray();
    }
}

public sealed record HarborChest(string Id, int Difficulty, IReadOnlyList<ItemInstance> Candidates, bool IsNew = true);
public sealed record HarborDispatch(ExpeditionTeamKind Team, HarborRun? ActiveRun, long ElapsedMilliseconds,
    int RemainingRuns, bool ContinueOnFailure, string Status);
public sealed record HarborSnapshot(long Sequence, IReadOnlyList<HarborDispatch> Dispatches, IReadOnlyList<HarborChest> Chests);
