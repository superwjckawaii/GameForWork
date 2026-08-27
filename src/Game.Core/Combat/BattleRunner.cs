namespace GameForWork.Core.Combat;

public sealed record BattleRun(
    ulong Seed,
    BattleState InitialState,
    BattleState FinalState,
    IReadOnlyList<BattleCommand> Commands,
    IReadOnlyList<BattleEvent> Events,
    string FinalHash);

public sealed record BattleOutcomeResult(BattleOutcome Outcome, int Tick, string FinalHash);

public sealed class BattleRunner
{
    private readonly BattleEngine _engine = new();

    public BattleRun RunAutomatic(ulong seed)
    {
        BattleState state = P0BattleFactory.Create(seed);
        BattleState initial = state.Clone();
        var commands = new List<BattleCommand>();
        var events = new List<BattleEvent>();
        while (!state.IsFinished)
        {
            IReadOnlyList<BattleCommand> tickCommands = _engine.BuildAutomaticCommands(state);
            commands.AddRange(tickCommands);
            events.AddRange(_engine.Step(state, tickCommands));
        }

        return new BattleRun(seed, initial, state, commands, events, BattleStateCodec.Hash(state));
    }

    public BattleOutcomeResult RunOutcomeOnly(ulong seed)
    {
        BattleState state = P0BattleFactory.Create(seed);
        while (!state.IsFinished)
        {
            IReadOnlyList<BattleCommand> commands = _engine.BuildAutomaticCommands(state);
            _ = _engine.Step(state, commands);
        }

        return new BattleOutcomeResult(state.Outcome, state.Tick, BattleStateCodec.Hash(state));
    }

    public BattleRun Replay(BattleState initial, IEnumerable<BattleCommand> commands)
    {
        BattleState state = initial.Clone();
        BattleState original = initial.Clone();
        BattleCommand[] commandArray = commands
            .OrderBy(command => command.Tick)
            .ThenBy(command => command.ActorId)
            .ThenBy(command => command.Kind)
            .ToArray();
        var events = new List<BattleEvent>();
        while (!state.IsFinished)
        {
            BattleCommand[] tickCommands = commandArray.Where(command => command.Tick == state.Tick).ToArray();
            events.AddRange(_engine.Step(state, tickCommands));
        }

        return new BattleRun(state.Seed, original, state, commandArray, events, BattleStateCodec.Hash(state));
    }
}
