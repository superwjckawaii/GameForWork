namespace GameForWork.Core.Harbor;

public enum HarborDifficulty { First, Second, Third }
public enum HarborRegion { OuterPier, SunkenWarehouse, TreasuryHarbor }
public enum HarborStatus { Running, Succeeded, Failed, Cancelled }
public enum HarborCommand { Advance, Engage, Avoid, Interact, Extract }

public sealed record HarborConfig(int Fee, int EnemyLevel, double HealthMultiplier, double DamageMultiplier, int TimeLimitSeconds)
{
    public static HarborConfig For(HarborDifficulty d) => d switch
    {
        HarborDifficulty.First => new(1000,100,1.0,1.0,300),
        HarborDifficulty.Second => new(3000,110,1.5,1.25,300),
        _ => new(8000,120,2.2,1.6,300)
    };
}

public sealed record HarborEvent(string ActionId, HarborCommand Command, HarborRegion Region, double X, double Y, double Direction, double Radius, long StartedAtMs, long EndedAtMs);
public sealed record HarborChest(string ChestId, HarborDifficulty Difficulty, IReadOnlyList<string> Candidates, bool Claimed, int? SelectedIndex);
public sealed record HarborSnapshot(string ActionId, int TeamId, HarborDifficulty Difficulty, HarborRegion Region, HarborStatus Status, ulong Seed, int CommandIndex, double X, double Y, double Health, double ElapsedSeconds, int PaidFee, HarborChest? Chest, IReadOnlyList<HarborEvent> Events);

public sealed class HarborAction
{
    private readonly List<HarborEvent> _events;
    private readonly List<HarborCommand> _commands = [];
    private HarborAction(HarborSnapshot s) { Snapshot = s; _events = s.Events.ToList(); }
    public HarborSnapshot Snapshot { get; private set; }
    public IReadOnlyList<HarborEvent> Events => _events;
    public static HarborAction Start(string actionId, int teamId, HarborDifficulty difficulty, ulong seed, int gold, long nowMs = 0)
    {
        var config = HarborConfig.For(difficulty);
        if (gold < config.Fee) throw new InvalidOperationException("insufficient_gold");
        return new HarborAction(new(actionId, teamId, difficulty, HarborRegion.OuterPier, HarborStatus.Running, seed, 0, 0, 0, 100, 0, config.Fee, null, []));
    }
    public static HarborAction Restore(HarborSnapshot snapshot) => new(snapshot);
    public HarborSnapshot Capture() => Snapshot with { Events = _events.ToArray() };
    public bool Step(HarborCommand command, double movementSpeed, double incomingDamage, long nowMs)
    {
        if (Snapshot.Status != HarborStatus.Running) return false;
        if (movementSpeed < 0 || incomingDamage < 0) throw new ArgumentOutOfRangeException();
        var config = HarborConfig.For(Snapshot.Difficulty);
        double dt = command == HarborCommand.Advance ? 1.0 / Math.Max(0.1, movementSpeed) : 0.25;
        double health = Math.Max(0, Snapshot.Health - incomingDamage * config.DamageMultiplier);
        HarborRegion region = Snapshot.Region;
        double x = Snapshot.X + (command == HarborCommand.Advance ? movementSpeed : 0);
        int index = Snapshot.CommandIndex + 1;
        if (command == HarborCommand.Engage && region < HarborRegion.TreasuryHarbor) region++;
        HarborStatus status = health <= 0 || Snapshot.ElapsedSeconds + dt > config.TimeLimitSeconds ? HarborStatus.Failed : Snapshot.Status;
        if (command == HarborCommand.Extract && region == HarborRegion.TreasuryHarbor && health > 0 && Snapshot.ElapsedSeconds + dt <= config.TimeLimitSeconds) status = HarborStatus.Succeeded;
        var ev = new HarborEvent(Snapshot.ActionId, command, region, x, 0, 0, 1, nowMs, nowMs + (long)(dt * 1000));
        _events.Add(ev);
        Snapshot = Snapshot with { Region = region, Status = status, CommandIndex = index, X = x, Health = health, ElapsedSeconds = Snapshot.ElapsedSeconds + dt };
        if (status == HarborStatus.Succeeded && Snapshot.Chest is null)
        {
            var candidates = Enumerable.Range(0,3).Select(i => $"harbor.placeholder.{Snapshot.Seed + (ulong)i}").ToArray();
            Snapshot = Snapshot with { Chest = new HarborChest($"{Snapshot.ActionId}:chest", Snapshot.Difficulty, candidates, false, null) };
        }
        return true;
    }
    public bool Cancel() { if (Snapshot.Status != HarborStatus.Running) return false; Snapshot = Snapshot with { Status = HarborStatus.Cancelled }; return true; }
    public HarborChest? Claim(int index)
    {
        var chest = Snapshot.Chest;
        if (chest is null || chest.Claimed || index < 0 || index >= chest.Candidates.Count) return null;
        chest = chest with { Claimed = true, SelectedIndex = index }; Snapshot = Snapshot with { Chest = chest }; return chest;
    }
}
