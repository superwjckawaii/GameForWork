using System.Security.Cryptography;
using System.Text;
using GameForWork.Core.P1.Items;
using GameForWork.Core.Simulation;

namespace GameForWork.Core.P1.Combat;

public enum P1BattleOutcome
{
    HeroVictory,
    EnemyVictory,
    Draw,
    Timeout,
}

public enum P1CombatEventKind
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

public sealed record P1CombatEvent(
    int Tick,
    P1CombatEventKind Kind,
    int Value = 0,
    string Detail = "");

public sealed record P1EncounterRequest(
    CharacterSheet Hero,
    WeaponProfile HeroWeapon,
    SkillConfiguration HeavyStrike,
    ScaledEnemy Enemy,
    int HeroFlatAccuracy = 80,
    int HeroIncreasedDamageBasisPoints = 0,
    int HeroIncreasedCriticalChanceBasisPoints = 0,
    int HeroIncreasedBleedChanceBasisPoints = 0,
    bool UseWarCry = true,
    bool EchoNotableAllocated = false,
    bool DeepWoundAllocated = false,
    bool FasterBleedingAllocated = false,
    int MaximumTicks = 0,
    LifeFlaskDefinition? LifeFlask = null,
    int IncreasedLifeFlaskEffectBasisPoints = 0,
    int LifeFlaskUseThresholdBasisPoints = 5_000,
    int AddedPhysicalDamage = 0,
    SkillUseProfile? HeavyStrikeProfile = null,
    LegendaryRule? WeaponLegendaryRule = null,
    int? InitialHeroLife = null,
    int? InitialHeroMana = null,
    int? InitialHeroShield = null);

public sealed record P1EncounterResult(
    ulong Seed,
    P1BattleOutcome Outcome,
    int Ticks,
    int HeroLife,
    int HeroMana,
    int HeroShield,
    int EnemyLife,
    IReadOnlyList<P1CombatEvent> Events,
    string FinalHash);

public sealed class P1EncounterRunner
{
    public P1EncounterResult Run(P1EncounterRequest request, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(request);
        var random = new Pcg32(seed);
        var heroResources = new ResourceState(
            request.Hero,
            request.InitialHeroLife,
            request.InitialHeroMana,
            request.InitialHeroShield);
        LifeFlaskState? lifeFlask = request.LifeFlask is null ? null : new LifeFlaskState(request.LifeFlask);
        SkillUseProfile heavyStrike = request.HeavyStrikeProfile ?? SkillRules.BuildHeavyStrike(
                request.HeavyStrike,
                request.HeroWeapon,
                heroResources.MaximumLife);
        var warCry = new WarCryState { EchoNotableAllocated = request.EchoNotableAllocated };
        var enemyBleeds = new BleedCollection(request.DeepWoundAllocated, request.FasterBleedingAllocated);
        var heroBleeds = new BleedCollection();
        var events = new List<P1CombatEvent>();
        int enemyLife = request.Enemy.Life;
        int heroNextActionTick = 0;
        int enemyNextActionTick = 0;
        int corpseExplosionTick = -1;
        BossPhase? previousBossPhase = null;
        int tick;

        for (tick = 0; request.MaximumTicks == 0 || tick < request.MaximumTicks; tick++)
        {
            heroResources.AdvanceRegenerationTick(tick);
            warCry.AdvanceTick();

            if (lifeFlask is not null && heroResources.IsAlive &&
                (long)heroResources.Life * 10_000 <=
                (long)heroResources.MaximumLife * request.LifeFlaskUseThresholdBasisPoints)
            {
                int recovered = lifeFlask.TryUse(
                    heroResources.MaximumLife - heroResources.Life,
                    request.IncreasedLifeFlaskEffectBasisPoints);
                if (recovered > 0)
                {
                    heroResources.HealLife(recovered);
                    events.Add(new P1CombatEvent(tick, P1CombatEventKind.LifeFlaskUsed, recovered));
                }
            }

            int enemyBleedDamage = enemyBleeds.AdvanceTick(tick);
            if (enemyBleedDamage > 0 && enemyLife > 0)
            {
                enemyLife = Math.Max(0, enemyLife - enemyBleedDamage);
                events.Add(new P1CombatEvent(tick, P1CombatEventKind.BleedDamage, enemyBleedDamage, "enemy"));
            }

            int heroBleedDamage = heroBleeds.AdvanceTick(tick);
            if (heroBleedDamage > 0 && heroResources.IsAlive)
            {
                heroResources.ApplyDamage(heroBleedDamage, tick);
                events.Add(new P1CombatEvent(tick, P1CombatEventKind.BleedDamage, heroBleedDamage, "hero"));
            }

            BossPhaseState? bossState = null;
            if (request.Enemy.Base.StableId == P1Enemies.AbyssWarden.StableId && enemyLife > 0)
            {
                bossState = AbyssWardenRules.DeterminePhase(enemyLife, request.Enemy.Life, tick);
                if (bossState.Phase != previousBossPhase)
                {
                    previousBossPhase = bossState.Phase;
                    events.Add(new P1CombatEvent(tick, P1CombatEventKind.BossPhaseChanged, Detail: bossState.Phase.ToString()));
                    if (bossState.SummonsWorkers)
                    {
                        events.Add(new P1CombatEvent(tick, P1CombatEventKind.BossSummonedWorkers, 3));
                    }

                    if (bossState.CreatesHazardZone)
                    {
                        events.Add(new P1CombatEvent(tick, P1CombatEventKind.BossHazardCreated));
                    }
                }
            }

            if (heroResources.IsAlive && enemyLife > 0 && tick >= heroNextActionTick)
            {
                bool shouldWarCry = request.UseWarCry && warCry.CooldownRemainingTicks == 0 && warCry.EmpoweredHeavyStrikes == 0;
                if (shouldWarCry && warCry.TryActivate(heroResources, tick))
                {
                    heroNextActionTick = checked(tick + SkillRules.BuildWarCry().CastTimeTicks);
                    events.Add(new P1CombatEvent(tick, P1CombatEventKind.WarCryUsed, warCry.EmpoweredHeavyStrikes));
                }
                else
                {
                    var strikeRequest = new HeavyStrikeRequest(
                        heroResources,
                        heavyStrike,
                        request.HeroWeapon,
                        request.Hero.Accuracy(request.HeroFlatAccuracy).Value,
                        request.Enemy.Evasion,
                        request.Enemy.Armor,
                        IncreasedDamageBasisPoints: request.HeroIncreasedDamageBasisPoints,
                        AddedMinimumPhysicalDamage: request.AddedPhysicalDamage,
                        AddedMaximumPhysicalDamage: request.AddedPhysicalDamage,
                        IncreasedCriticalChanceBasisPoints: request.HeroIncreasedCriticalChanceBasisPoints,
                        IncreasedBleedChanceBasisPoints: request.HeroIncreasedBleedChanceBasisPoints,
                        WarCry: warCry);
                    HeavyStrikeResult strike = HeavyStrikeRules.Resolve(strikeRequest, random, tick);
                    if (strike.CastSucceeded)
                    {
                        heroNextActionTick = checked(tick + heavyStrike.AttackIntervalTicks);
                        DamageResult damage = strike.Damage!;
                        if (damage.Hit)
                        {
                            enemyLife = Math.Max(0, enemyLife - damage.FinalPhysicalDamage);
                            events.Add(new P1CombatEvent(tick, P1CombatEventKind.HeavyStrikeHit, damage.FinalPhysicalDamage));
                            int aftershockDamage = P1LegendaryRules.CalculateAftershockDamage(
                                damage.FinalPhysicalDamage,
                                request.WeaponLegendaryRule);
                            if (aftershockDamage > 0 && enemyLife > 0)
                            {
                                enemyLife = Math.Max(0, enemyLife - aftershockDamage);
                                events.Add(new P1CombatEvent(
                                    tick,
                                    P1CombatEventKind.LegendaryAftershock,
                                    aftershockDamage,
                                    "target_behind"));
                            }

                            if (damage.AppliedBleed && enemyLife > 0)
                            {
                                enemyBleeds.Apply(1, damage.BleedTotalDamage, tick, damage.BleedDurationTicks);
                                events.Add(new P1CombatEvent(tick, P1CombatEventKind.BleedApplied, damage.BleedTotalDamage, "enemy"));
                            }
                        }
                        else
                        {
                            events.Add(new P1CombatEvent(tick, P1CombatEventKind.HeavyStrikeMissed));
                        }
                    }
                    else
                    {
                        heroNextActionTick = tick + 1;
                    }
                }
            }

            if (heroResources.IsAlive && enemyLife > 0 && tick >= enemyNextActionTick)
            {
                ResolveEnemyAttack(request, bossState, random, heroResources, heroBleeds, tick, events);
                int attackRate = request.Enemy.AttacksPerSecondMilli;
                if (bossState is not null)
                {
                    attackRate = checked((int)((long)attackRate * bossState.AttackSpeedMoreBasisPoints / 10_000));
                }

                enemyNextActionTick = checked(tick + Math.Max(1, DivideRoundUp(20_000, attackRate)));
            }

            if (enemyLife == 0 && corpseExplosionTick < 0 &&
                request.Enemy.EliteAffixes.Contains(EliteAffix.CorpseExplosion))
            {
                corpseExplosionTick = tick + 20;
            }

            if (corpseExplosionTick == tick && heroResources.IsAlive)
            {
                int baseExplosionDamage = checked(20 * (10_000 + (1_000 * (request.Enemy.AreaLevel - 1))) / 10_000);
                CalculatedValue reduction = DamageRules.ArmorReduction(request.Hero.Armor().Value, baseExplosionDamage);
                int explosionDamage = Math.Max(1, checked(baseExplosionDamage * (10_000 - reduction.Value) / 10_000));
                heroResources.ApplyDamage(explosionDamage, tick);
                events.Add(new P1CombatEvent(tick, P1CombatEventKind.CorpseExplosion, explosionDamage));
            }

            bool pendingCorpseExplosion = corpseExplosionTick > tick;
            if ((!heroResources.IsAlive || enemyLife == 0) && !pendingCorpseExplosion)
            {
                break;
            }
        }

        int elapsedTicks = request.MaximumTicks == 0 ? tick + 1 : Math.Min(tick + 1, request.MaximumTicks);
        bool diagnosticLimitReached = request.MaximumTicks > 0 && tick >= request.MaximumTicks;
        P1BattleOutcome outcome = (heroResources.IsAlive, enemyLife > 0, diagnosticLimitReached) switch
        {
            (_, _, true) => P1BattleOutcome.Timeout,
            (true, false, false) => P1BattleOutcome.HeroVictory,
            (false, true, false) => P1BattleOutcome.EnemyVictory,
            (false, false, false) => P1BattleOutcome.Draw,
            _ => P1BattleOutcome.Timeout,
        };
        events.Add(new P1CombatEvent(elapsedTicks - 1, P1CombatEventKind.BattleEnded, Detail: outcome.ToString()));
        string hash = Hash(seed, outcome, elapsedTicks, heroResources, enemyLife, events);
        return new P1EncounterResult(
            seed,
            outcome,
            elapsedTicks,
            heroResources.Life,
            heroResources.Mana,
            heroResources.Shield,
            enemyLife,
            events,
            hash);
    }

    private static void ResolveEnemyAttack(
        P1EncounterRequest request,
        BossPhaseState? bossState,
        Pcg32 random,
        ResourceState heroResources,
        BleedCollection heroBleeds,
        int tick,
        ICollection<P1CombatEvent> events)
    {
        var enemyWeapon = new WeaponProfile(
            request.Enemy.Base.StableId + ".attack",
            request.Enemy.MinimumPhysicalDamage,
            request.Enemy.MaximumPhysicalDamage,
            request.Enemy.AttacksPerSecondMilli,
            500);
        var multipliers = new List<int>();
        if (bossState is not null)
        {
            multipliers.Add(bossState.DamageMoreBasisPoints);
        }

        bool lacerating = request.Enemy.EliteAffixes.Contains(EliteAffix.Lacerating);
        int ailmentDurationReduction = request.Hero.AilmentDurationReductionBasisPoints().Value;
        int bleedDuration = Math.Max(1, checked(80 * (10_000 - ailmentDurationReduction) / 10_000));
        var damageRequest = new DamageRequest(
            enemyWeapon,
            MoreDamageMultipliersBasisPoints: multipliers,
            CriticalChanceBasisPoints: 500,
            TargetArmor: request.Hero.Armor().Value,
            TargetEvasion: request.Hero.Evasion().Value,
            Accuracy: request.Enemy.Base.Accuracy,
            BleedChanceBasisPoints: lacerating ? 3_000 : 0,
            BleedTotalDamageBasisPoints: 3_000,
            BleedDurationTicks: bleedDuration);
        DamageResult damage = DamageRules.Resolve(damageRequest, random);
        if (!damage.Hit)
        {
            events.Add(new P1CombatEvent(tick, P1CombatEventKind.EnemyMissed));
            return;
        }

        heroResources.ApplyDamage(damage.FinalPhysicalDamage, tick);
        events.Add(new P1CombatEvent(tick, P1CombatEventKind.EnemyHit, damage.FinalPhysicalDamage));
        if (damage.AppliedBleed && heroResources.IsAlive)
        {
            heroBleeds.Apply(2, damage.BleedTotalDamage, tick, damage.BleedDurationTicks);
            events.Add(new P1CombatEvent(tick, P1CombatEventKind.BleedApplied, damage.BleedTotalDamage, "hero"));
        }
    }

    private static string Hash(
        ulong seed,
        P1BattleOutcome outcome,
        int ticks,
        ResourceState hero,
        int enemyLife,
        IEnumerable<P1CombatEvent> events)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write("GFWP1B1"u8);
        writer.Write(seed);
        writer.Write((byte)outcome);
        writer.Write(ticks);
        writer.Write(hero.Life);
        writer.Write(hero.Mana);
        writer.Write(hero.Shield);
        writer.Write(enemyLife);
        foreach (P1CombatEvent item in events)
        {
            writer.Write(item.Tick);
            writer.Write((byte)item.Kind);
            writer.Write(item.Value);
            writer.Write(item.Detail);
        }

        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    private static void Validate(P1EncounterRequest request)
    {
        if (request.MaximumTicks < 0 || request.HeavyStrike.SkillId != P1SkillIds.HeavyStrike ||
            request.LifeFlaskUseThresholdBasisPoints is < 0 or > 10_000)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Encounter request is invalid.");
        }
    }

    private static int DivideRoundUp(int numerator, int denominator) =>
        checked((numerator + denominator - 1) / denominator);
}
