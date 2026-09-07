namespace GameForWork.Core.Combat;

public sealed class StunCombatState
{
    public int RecentUntil { get; private set; }
    public void Applied(int tick) => RecentUntil = tick + 80;
}
