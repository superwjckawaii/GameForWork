namespace GameForWork.Core.Combat;

public enum BattleCommandKind
{
    Wait = 0,
    MoveTo = 1,
    UseSkill = 2,
}

public sealed record BattleCommand(
    int Tick,
    ulong ActorId,
    BattleCommandKind Kind,
    int XRaw = 0,
    int YRaw = 0,
    ulong TargetActorId = 0,
    string SkillId = "core.foundation.basic_attack");
