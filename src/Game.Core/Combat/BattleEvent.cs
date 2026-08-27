namespace GameForWork.Core.Combat;

public enum BattleEventKind
{
    ActorMoved = 0,
    CastStarted = 1,
    HitResolved = 2,
    DamageApplied = 3,
    ActorDied = 4,
    BattleEnded = 5,
}

public sealed record BattleEvent(
    int Tick,
    BattleEventKind Kind,
    ulong ActorId = 0,
    ulong TargetActorId = 0,
    int Value = 0,
    bool Success = false,
    string Detail = "");
