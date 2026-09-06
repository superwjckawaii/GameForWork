namespace GameForWork.Core.Campaign.Combat;

public enum BattleOutcome
{
    HeroVictory,
    EnemyVictory,
    Draw,
    Timeout,
}

public enum CombatEventKind
{
    WarCryUsed,
    HeavyStrikeHit,
    HeavyStrikeMissed,
    EnemyHit,
    EnemyMissed,
    BleedApplied,
    BleedDamage,
    BossPhaseChanged,
    BossSummonedWorkers,
    BossHazardCreated,
    CorpseExplosion,
    LifeFlaskUsed,
    LegendaryAftershock,
    BattleEnded,
}

public sealed record CombatEvent(
    int Tick,
    CombatEventKind Kind,
    int Value = 0,
    string Detail = "");

