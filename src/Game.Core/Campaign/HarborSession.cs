using GameForWork.Core.Campaign.World;
using GameForWork.Core.Harbor;
using GameForWork.Core.Equipment;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Spatial;
using GameForWork.Core.Economy;

namespace GameForWork.Core.Campaign;

public sealed partial class GameSession
{
    private readonly List<LootChest> _lootChests = [];
    public IReadOnlyList<LootChest> LootChests => _lootChests;

    public bool MarkLootChestViewed(string chestId)
    {
        int index = _lootChests.FindIndex(chest => chest.Id == chestId);
        if (index < 0 || !_lootChests[index].IsNew) return false;
        _lootChests[index] = _lootChests[index] with { IsNew = false };
        return true;
    }

    public bool ClaimLootChest(string chestId)
    {
        LootChest? chest = _lootChests.FirstOrDefault(value => value.Id == chestId);
        if (chest is null) return false;
        HashSet<string> existingIds = World.Storage.Items.Concat(Management.Recovery).Concat(Management.SortingBag)
            .Concat(HeroEquipment.Items.Values).Concat(MercenaryEquipment.Items.Values)
            .Select(item => item.InstanceId).ToHashSet(StringComparer.Ordinal);
        if (chest.Equipment.Any(item => !existingIds.Add(item.InstanceId))) return false;
        int gold = chest.Gold;
        int ironScraps = chest.IronScraps;
        foreach (ItemInstance item in chest.Equipment)
        {
            LootDisposition disposition = World.Storage.IsFirstDiscovery(item) || item.IsLocked
                ? LootDisposition.Keep
                : World.Filter.Evaluate(item);
            if (disposition == LootDisposition.Keep)
            {
                if (!World.Storage.TryStore(item)) Management.AddToRecovery(item, "普通战利品箱");
            }
            else if (disposition == LootDisposition.Sell)
            {
                gold = checked(gold + ItemValue.SalePrice(item));
            }
            else if (disposition == LootDisposition.Dismantle)
            {
                ironScraps = checked(ironScraps + LootProcessor.BasicDismantleIronScraps);
            }
        }
        if (gold > 0 || ironScraps > 0)
            World.Economy.AddDispositionProceeds(gold, ironScraps);
        foreach (MetalCurrencyStack stack in chest.Metals ?? [])
            World.Economy.AddMetal(stack.Kind, stack.Amount);
        _lootChests.Remove(chest);
        return true;
    }

    public int ClaimLootChests(int maximum = int.MaxValue)
    {
        if (maximum < 1) return 0;
        int claimed = 0;
        foreach (string chestId in _lootChests.Select(chest => chest.Id).ToArray())
        {
            if (claimed >= maximum || !ClaimLootChest(chestId)) break;
            claimed++;
        }
        return claimed;
    }

    private long _harborSequence;
    private readonly Dictionary<ExpeditionTeamKind, HarborDispatch> _harborDispatches = [];
    private readonly List<HarborChest> _harborChests = [];
    public IReadOnlyList<HarborChest> HarborChests => _harborChests;
    public HarborDispatch? HarborDispatchFor(ExpeditionTeamKind team) => _harborDispatches.GetValueOrDefault(team);
    public bool IsHarborActive(ExpeditionTeamKind team) => HarborDispatchFor(team)?.ActiveRun is not null;

    public bool StartHarbor(ExpeditionTeamKind kind, int difficulty, int runs = 1, bool continueOnFailure = false)
    {
        if (!Enum.IsDefined(kind) || difficulty is < 1 or > 3 || runs is < 1 or > 999 || !Endgame.FinalBreakthroughCompleted ||
            !Campaign.Completed || IsHarborActive(kind)) return false;
        TeamExpeditionState team = Team(kind);
        if (team.ActiveMap is not null || team.Queue.Count > 0 || World.Expedition.Get(kind)?.Enabled == true) return false;
        return BeginHarbor(kind, difficulty, runs, continueOnFailure);
    }

    private bool BeginHarbor(ExpeditionTeamKind kind, int difficulty, int runs, bool continueOnFailure)
    {
        if (World.Economy.Gold < HarborDifficulty.Get(difficulty).Fee) return false;
        long sequence = checked(_harborSequence + 1);
        HarborRun run = HarborRunner.Run($"harbor.run.{Seed:x16}.{sequence}", difficulty, Team(kind).Build,
            Seed ^ (ulong)sequence * 0x9e3779b97f4a7c15UL);
        if (!World.Economy.TrySpendGold(run.PaidGold)) return false;
        _harborSequence = sequence;
        _harborDispatches[kind] = new(kind, run, 0, runs - 1, continueOnFailure, "进行中");
        Team(kind).IsHarborOccupied = true;
        Team(kind).Stop("harbor_active");
        return true;
    }

    public bool StopHarborRepeat(ExpeditionTeamKind team)
    {
        if (HarborDispatchFor(team) is not { ActiveRun: not null } dispatch) return false;
        _harborDispatches[team] = dispatch with { RemainingRuns = 0 };
        return true;
    }

    public bool CancelHarbor(ExpeditionTeamKind team)
    {
        if (HarborDispatchFor(team) is not { ActiveRun: not null } dispatch) return false;
        _harborDispatches[team] = dispatch with { ActiveRun = null, RemainingRuns = 0, Status = "已放弃（不退费、无奖励）" };
        Team(team).IsHarborOccupied = false;
        Team(team).Stop("harbor_cancelled");
        return true;
    }

    public bool MarkHarborChestViewed(string chestId)
    {
        int index = _harborChests.FindIndex(chest => chest.Id == chestId);
        if (index < 0 || !_harborChests[index].IsNew) return false;
        _harborChests[index] = _harborChests[index] with { IsNew = false };
        return true;
    }

    public bool ClaimHarborChest(string chestId, int candidateIndex)
    {
        HarborChest? chest = _harborChests.FirstOrDefault(value => value.Id == chestId);
        if (chest is null || candidateIndex < 0 || candidateIndex >= chest.Candidates.Count) return false;
        var item = EquipmentItemRebinder.Rebind(chest.Candidates[candidateIndex]) with { IsLocked = true };
        if (World.Storage.Items.Concat(Management.Recovery).Concat(Management.SortingBag)
            .Concat(HeroEquipment.Items.Values).Concat(MercenaryEquipment.Items.Values)
            .Any(existing => existing.InstanceId == item.InstanceId)) return false;
        // Recovery is unbounded in the existing domain; never invoke automatic disposition for manual choice.
        if (!World.Storage.TryStore(item)) Management.AddToRecovery(item, "沉金港手选奖励");
        _harborChests.Remove(chest);
        return true;
    }

    private HarborSnapshot CaptureHarbor() => new(_harborSequence, _harborDispatches.Values.OrderBy(value => value.Team).ToArray(), _harborChests.ToArray());

    private LootChestSnapshot CaptureLootChests() => new(_lootChests.ToArray());

    private void RestoreLootChests(LootChestSnapshot? snapshot)
    {
        if (snapshot is null) return;
        if (snapshot.Chests is null || snapshot.Chests.Select(chest => chest.Id).Distinct(StringComparer.Ordinal).Count() != snapshot.Chests.Count)
            throw new InvalidDataException("Invalid ordinary loot chest identities.");
        _lootChests.AddRange(snapshot.Chests.Select(chest => chest.Validate() with
        { Equipment = chest.Equipment.Select(EquipmentItemRebinder.Rebind).ToArray() }));
    }

    private void RestoreHarbor(HarborSnapshot? snapshot)
    {
        if (snapshot is null) return;
        if (snapshot.Sequence < 0 || snapshot.Chests is null || snapshot.Dispatches is null ||
            snapshot.Chests.Select(value => value.Id).Distinct().Count() != snapshot.Chests.Count ||
            snapshot.Dispatches.Select(value => value.Team).Distinct().Count() != snapshot.Dispatches.Count ||
            snapshot.Chests.Any(value => value.Difficulty is < 1 or > 3 || value.Candidates is not { Count: 3 } ||
                value.Candidates.Select(item => item.Base.StableId).Distinct().Count() != 3))
            throw new InvalidDataException("Invalid harbor snapshot identities or chest contents.");
        _harborSequence = snapshot.Sequence;
        string[] activeIds = snapshot.Dispatches.Where(value => value.ActiveRun is not null).Select(value => value.ActiveRun!.Id).ToArray();
        if (activeIds.Distinct().Count() != activeIds.Length || snapshot.Chests.Any(chest => activeIds.Contains(chest.Id)))
            throw new InvalidDataException("Harbor action was already settled or assigned to multiple teams.");
        _harborChests.AddRange(snapshot.Chests.Select(chest => chest with
        { Candidates = chest.Candidates.Select(EquipmentItemRebinder.Rebind).ToArray() }));
        foreach (HarborDispatch dispatch in snapshot.Dispatches)
        {
            if (!Enum.IsDefined(dispatch.Team)) throw new InvalidDataException("Unknown harbor team.");
            HarborRun? run = dispatch.ActiveRun;
            if (run is not null && (!run.IsValid || dispatch.ElapsedMilliseconds < 0 || dispatch.ElapsedMilliseconds >= run.DurationMilliseconds))
            {
                if (run.Difficulty is >= 1 and <= 3 && run.PaidGold == HarborDifficulty.Get(run.Difficulty).Fee)
                    World.Economy.AddDispositionProceeds(run.PaidGold, 0);
                _harborDispatches[dispatch.Team] = dispatch with { ActiveRun = null, RemainingRuns = 0, Status = "行动损坏，已终止并退费" };
                Team(dispatch.Team).Stop("harbor_recovered");
                continue;
            }
            _harborDispatches[dispatch.Team] = dispatch;
            if (run is not null)
            {
                if (Team(dispatch.Team).ActiveMap is not null || Team(dispatch.Team).Queue.Count > 0)
                    throw new InvalidDataException("Harbor and map activities overlap.");
                Team(dispatch.Team).IsHarborOccupied = true;
                Team(dispatch.Team).Stop("harbor_active");
            }
        }
    }

    private OfflineResult AdvanceSimulated(long milliseconds, bool offline, bool asyncPreparation)
    {
        if (!_harborDispatches.Values.Any(value => value.ActiveRun is not null))
            return AdvanceWorldSimulated(milliseconds, offline, asyncPreparation);
        long remaining = milliseconds;
        int completed = 0, failed = 0;
        var segments = new List<OfflineSegment>();
        OfflineResult? result = null;
        do
        {
            long untilCompletion = _harborDispatches.Values.Where(value => value.ActiveRun is not null)
                .Select(value => value.ActiveRun!.DurationMilliseconds - value.ElapsedMilliseconds).DefaultIfEmpty(remaining).Min();
            long step = Math.Min(remaining, untilCompletion);
            result = AdvanceWorldSimulated(step, offline, asyncPreparation);
            segments.AddRange((result.Segments ?? []).Select(segment => segment with
            { StartMilliseconds = segment.StartMilliseconds + milliseconds - remaining }));
            completed += result.TotalMapsCompleted; failed += result.TotalMapsFailed;
            foreach (HarborDispatch dispatch in _harborDispatches.Values.OrderBy(value => value.Team).ToArray())
            {
                if (dispatch.ActiveRun is not { } run) continue;
                long elapsed = dispatch.ElapsedMilliseconds + step;
                if (elapsed < run.DurationMilliseconds)
                {
                    _harborDispatches[dispatch.Team] = dispatch with { ElapsedMilliseconds = elapsed };
                    continue;
                }
                if (run.Succeeded) _harborChests.Add(new(run.Id, run.Difficulty, run.Candidates));
                _harborDispatches[dispatch.Team] = dispatch with { ActiveRun = null, RemainingRuns = 0,
                    ElapsedMilliseconds = run.DurationMilliseconds, Status = run.Succeeded ? "成功，宝箱待开启" : "失败（无奖励）" };
                Team(dispatch.Team).Stop(run.Succeeded ? "harbor_complete" : "harbor_failed");
                Team(dispatch.Team).IsHarborOccupied = false;
                if (dispatch.RemainingRuns > 0 && (run.Succeeded || dispatch.ContinueOnFailure) &&
                    !BeginHarbor(dispatch.Team, run.Difficulty, dispatch.RemainingRuns, dispatch.ContinueOnFailure))
                    _harborDispatches[dispatch.Team] = _harborDispatches[dispatch.Team] with { Status = "金币不足，停止派遣" };
            }
            remaining -= step;
        } while (remaining > 0);
        return result with { EffectiveMilliseconds = milliseconds, TotalMapsCompleted = completed, TotalMapsFailed = failed, Segments = segments };
    }
}
