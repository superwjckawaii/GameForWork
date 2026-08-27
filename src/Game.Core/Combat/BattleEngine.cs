using GameForWork.Core.Simulation;

namespace GameForWork.Core.Combat;

public sealed class BattleEngine
{
    public IReadOnlyList<BattleCommand> BuildAutomaticCommands(BattleState state)
    {
        var commands = new List<BattleCommand>();
        foreach (ActorState actor in state.Actors.Values)
        {
            if (!actor.IsAlive || actor.IsCasting)
            {
                commands.Add(new BattleCommand(state.Tick, actor.Id, BattleCommandKind.Wait));
                continue;
            }

            ActorState? target = state.Actors.Values
                .Where(candidate => candidate.IsAlive && candidate.Team != actor.Team)
                .OrderBy(candidate => IntegerMath.Distance(actor.XRaw, actor.YRaw, candidate.XRaw, candidate.YRaw))
                .ThenBy(candidate => candidate.Id)
                .FirstOrDefault();

            if (target is null)
            {
                commands.Add(new BattleCommand(state.Tick, actor.Id, BattleCommandKind.Wait));
                continue;
            }

            int distance = IntegerMath.Distance(actor.XRaw, actor.YRaw, target.XRaw, target.YRaw);
            if (distance <= actor.RangeRaw && actor.CooldownRemainingTicks == 0)
            {
                commands.Add(new BattleCommand(
                    state.Tick,
                    actor.Id,
                    BattleCommandKind.UseSkill,
                    TargetActorId: target.Id));
            }
            else if (distance > actor.RangeRaw)
            {
                commands.Add(new BattleCommand(
                    state.Tick,
                    actor.Id,
                    BattleCommandKind.MoveTo,
                    target.XRaw,
                    target.YRaw,
                    target.Id));
            }
            else
            {
                commands.Add(new BattleCommand(state.Tick, actor.Id, BattleCommandKind.Wait));
            }
        }

        return commands;
    }

    public IReadOnlyList<BattleEvent> Step(BattleState state, IEnumerable<BattleCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(commands);
        if (state.IsFinished)
        {
            return Array.Empty<BattleEvent>();
        }

        var events = new List<BattleEvent>();
        foreach (ActorState actor in state.Actors.Values)
        {
            actor.CooldownRemainingTicks = Math.Max(0, actor.CooldownRemainingTicks - 1);
        }

        BattleCommand[] orderedCommands = commands
            .Where(command => command.Tick == state.Tick)
            .OrderBy(command => command.Tick)
            .ThenBy(command => command.ActorId)
            .ThenBy(command => command.Kind)
            .ToArray();

        foreach (BattleCommand command in orderedCommands)
        {
            if (!state.Actors.TryGetValue(command.ActorId, out ActorState? actor) ||
                !actor.IsAlive || actor.IsCasting)
            {
                continue;
            }

            switch (command.Kind)
            {
                case BattleCommandKind.UseSkill:
                    TryStartCast(state, actor, command, events);
                    break;
                case BattleCommandKind.MoveTo:
                    MoveActor(actor, command.XRaw, command.YRaw, events, state.Tick);
                    break;
                case BattleCommandKind.Wait:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(commands), command.Kind, "Unknown command kind.");
            }
        }

        ResolveCompletedCasts(state, events);
        EvaluateEnd(state, events);
        state.Tick++;
        if (!state.IsFinished && state.Tick >= BattleState.MaxTicks)
        {
            state.Outcome = BattleOutcome.Timeout;
            events.Add(new BattleEvent(state.Tick - 1, BattleEventKind.BattleEnded, Detail: BattleOutcome.Timeout.ToString()));
        }

        return events;
    }

    private static void TryStartCast(
        BattleState state,
        ActorState actor,
        BattleCommand command,
        ICollection<BattleEvent> events)
    {
        if (actor.CooldownRemainingTicks != 0 || command.SkillId != "core.p0.basic_attack" ||
            !state.Actors.TryGetValue(command.TargetActorId, out ActorState? target) ||
            !target.IsAlive || target.Team == actor.Team)
        {
            return;
        }

        int distance = IntegerMath.Distance(actor.XRaw, actor.YRaw, target.XRaw, target.YRaw);
        if (distance > actor.RangeRaw)
        {
            return;
        }

        actor.CastTargetId = target.Id;
        actor.CastResolveTick = checked(state.Tick + actor.WindupTicks);
        events.Add(new BattleEvent(state.Tick, BattleEventKind.CastStarted, actor.Id, target.Id, actor.WindupTicks));
    }

    private static void MoveActor(
        ActorState actor,
        int destinationX,
        int destinationY,
        ICollection<BattleEvent> events,
        int tick)
    {
        int distance = IntegerMath.Distance(actor.XRaw, actor.YRaw, destinationX, destinationY);
        if (distance == 0)
        {
            return;
        }

        int step = actor.SpeedRawPerSecond / BattleState.TicksPerSecond;
        int oldX = actor.XRaw;
        int oldY = actor.YRaw;
        if (distance <= step)
        {
            actor.XRaw = destinationX;
            actor.YRaw = destinationY;
        }
        else
        {
            long dx = (long)destinationX - actor.XRaw;
            long dy = (long)destinationY - actor.YRaw;
            actor.XRaw = checked(actor.XRaw + (int)(dx * step / distance));
            actor.YRaw = checked(actor.YRaw + (int)(dy * step / distance));
        }

        if (oldX != actor.XRaw || oldY != actor.YRaw)
        {
            events.Add(new BattleEvent(tick, BattleEventKind.ActorMoved, actor.Id, Value: actor.XRaw, Detail: actor.YRaw.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }
    }

    private static void ResolveCompletedCasts(BattleState state, ICollection<BattleEvent> events)
    {
        var damage = new SortedDictionary<ulong, int>();
        foreach (ActorState actor in state.Actors.Values.Where(actor => actor.IsAlive && actor.CastResolveTick == state.Tick))
        {
            ulong targetId = actor.CastTargetId;
            actor.CastResolveTick = -1;
            actor.CastTargetId = 0;
            actor.CooldownRemainingTicks = actor.CooldownTicks;

            if (!state.Actors.TryGetValue(targetId, out ActorState? target) || !target.IsAlive)
            {
                events.Add(new BattleEvent(state.Tick, BattleEventKind.HitResolved, actor.Id, targetId, Success: false, Detail: "target_unavailable"));
                continue;
            }

            int roll = state.Random.NextBasisPoints();
            bool hit = roll < actor.HitChanceBasisPoints;
            events.Add(new BattleEvent(state.Tick, BattleEventKind.HitResolved, actor.Id, target.Id, roll, hit));
            if (hit)
            {
                int amount = Math.Max(1, actor.Damage - target.Armor);
                damage[target.Id] = damage.GetValueOrDefault(target.Id) + amount;
            }
        }

        foreach ((ulong targetId, int amount) in damage)
        {
            ActorState target = state.Actors[targetId];
            target.Life = Math.Max(0, target.Life - amount);
            events.Add(new BattleEvent(state.Tick, BattleEventKind.DamageApplied, TargetActorId: target.Id, Value: amount));
        }

        foreach (ActorState dead in state.Actors.Values.Where(actor => actor.Life == 0))
        {
            bool alreadyReported = events.Any(item => item.Kind == BattleEventKind.ActorDied && item.ActorId == dead.Id);
            if (!alreadyReported && damage.ContainsKey(dead.Id))
            {
                events.Add(new BattleEvent(state.Tick, BattleEventKind.ActorDied, dead.Id));
            }
        }
    }

    private static void EvaluateEnd(BattleState state, ICollection<BattleEvent> events)
    {
        bool heroAlive = state.Actors.Values.Any(actor => actor.Team == Team.Hero && actor.IsAlive);
        bool enemyAlive = state.Actors.Values.Any(actor => actor.Team == Team.Enemy && actor.IsAlive);
        BattleOutcome outcome = (heroAlive, enemyAlive) switch
        {
            (true, true) => BattleOutcome.Running,
            (true, false) => BattleOutcome.HeroVictory,
            (false, true) => BattleOutcome.EnemyVictory,
            (false, false) => BattleOutcome.Draw,
        };

        if (outcome != BattleOutcome.Running)
        {
            state.Outcome = outcome;
            events.Add(new BattleEvent(state.Tick, BattleEventKind.BattleEnded, Detail: outcome.ToString()));
        }
    }
}
