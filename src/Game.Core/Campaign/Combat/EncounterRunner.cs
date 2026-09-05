using System.Security.Cryptography;
using System.Text;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Builds;
using GameForWork.Core.Simulation;

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

public sealed record EncounterRequest(
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

public sealed record EncounterResult(
    ulong Seed,
    BattleOutcome Outcome,
    int Ticks,
    int HeroLife,
    int HeroMana,
    int HeroShield,
    int EnemyLife,
    IReadOnlyList<CombatEvent> Events,
    string FinalHash);

public sealed class EncounterRunner
{
    private const int StalemateProgressWindowTicks = 1_200;
    private const int DetailedSimulationTicks = 20_000;

    public EncounterResult Run(EncounterRequest request, ulong seed)
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
        var events = new List<CombatEvent>();
        int enemyLife = request.Enemy.Life;
        int initialEnemyLife = enemyLife;
        int initialHeroLife = heroResources.Life;
        int heroNextActionTick = 0;
        int heroAttackFrequencyCarry = 0;
        int enemyNextActionTick = 0;
        int corpseExplosionTick = -1;
        BossPhase? previousBossPhase = null;
        int minimumHeroLife = heroResources.Life;
        int minimumEnemyLife = enemyLife;
        int lastProgressTick = 0;
        bool stalemate = false;
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
                    events.Add(new CombatEvent(tick, CombatEventKind.LifeFlaskUsed, recovered));
                }
            }

            int enemyBleedDamage = enemyBleeds.AdvanceTick(tick);
            if (enemyBleedDamage > 0 && enemyLife > 0)
            {
                enemyLife = Math.Max(0, enemyLife - enemyBleedDamage);
                events.Add(new CombatEvent(tick, CombatEventKind.BleedDamage, enemyBleedDamage, "enemy"));
            }

            int heroBleedDamage = heroBleeds.AdvanceTick(tick);
            if (heroBleedDamage > 0 && heroResources.IsAlive)
            {
                heroResources.ApplyDamage(heroBleedDamage, tick);
                events.Add(new CombatEvent(tick, CombatEventKind.BleedDamage, heroBleedDamage, "hero"));
            }

            BossPhaseState? bossState = null;
            if (request.Enemy.Base.StableId == Enemies.AbyssWarden.StableId && enemyLife > 0)
            {
                bossState = AbyssWardenRules.DeterminePhase(enemyLife, request.Enemy.Life, tick);
                if (bossState.Phase != previousBossPhase)
                {
                    previousBossPhase = bossState.Phase;
                    events.Add(new CombatEvent(tick, CombatEventKind.BossPhaseChanged, Detail: bossState.Phase.ToString()));
                    if (bossState.SummonsWorkers)
                    {
                        events.Add(new CombatEvent(tick, CombatEventKind.BossSummonedWorkers, 3));
                    }

                    if (bossState.CreatesHazardZone)
                    {
                        events.Add(new CombatEvent(tick, CombatEventKind.BossHazardCreated));
                    }
                }
            }

            if (heroResources.IsAlive && enemyLife > 0 && tick >= heroNextActionTick)
            {
                bool shouldWarCry = request.UseWarCry && warCry.CooldownRemainingTicks == 0 && warCry.EmpoweredHeavyStrikes == 0;
                if (shouldWarCry && warCry.TryActivate(heroResources, tick))
                {
                    heroNextActionTick = checked(tick + SkillRules.BuildWarCry().CastTimeTicks);
                    events.Add(new CombatEvent(tick, CombatEventKind.WarCryUsed, warCry.EmpoweredHeavyStrikes));
                }
                else
                {
                    int attacks = CombatRules.AttacksForScheduledSimulationTick(
                        heavyStrike.AttackFrequencyMilliPerSecond, ref heroAttackFrequencyCarry);
                    bool castSucceeded = false;
                    for (int attack = 0; attack < attacks && enemyLife > 0; attack++)
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
                        if (!strike.CastSucceeded) break;
                        castSucceeded = true;
                        DamageResult damage = strike.Damage!;
                        if (damage.Hit)
                        {
                            enemyLife = Math.Max(0, enemyLife - damage.FinalPhysicalDamage);
                            events.Add(new CombatEvent(tick, CombatEventKind.HeavyStrikeHit, damage.FinalPhysicalDamage));
                            int aftershockDamage = LegendaryRules.CalculateAftershockDamage(
                                damage.FinalPhysicalDamage,
                                request.WeaponLegendaryRule);
                            if (aftershockDamage > 0 && enemyLife > 0)
                            {
                                enemyLife = Math.Max(0, enemyLife - aftershockDamage);
                                events.Add(new CombatEvent(
                                    tick,
                                    CombatEventKind.LegendaryAftershock,
                                    aftershockDamage,
                                    "target_behind"));
                            }

                            if (damage.AppliedBleed && enemyLife > 0)
                            {
                                enemyBleeds.Apply(1, damage.BleedTotalDamage, tick, damage.BleedDurationTicks);
                                events.Add(new CombatEvent(tick, CombatEventKind.BleedApplied, damage.BleedTotalDamage, "enemy"));
                            }
                        }
                        else
                        {
                            events.Add(new CombatEvent(tick, CombatEventKind.HeavyStrikeMissed));
                        }
                    }
                    heroNextActionTick = tick + (castSucceeded ? heavyStrike.AttackIntervalTicks : 1);
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
                events.Add(new CombatEvent(tick, CombatEventKind.CorpseExplosion, explosionDamage));
            }

            if (heroResources.Life < minimumHeroLife || enemyLife < minimumEnemyLife)
            {
                minimumHeroLife = Math.Min(minimumHeroLife, heroResources.Life);
                minimumEnemyLife = Math.Min(minimumEnemyLife, enemyLife);
                lastProgressTick = tick;
            }

            if (heroResources.IsAlive && enemyLife > 0 &&
                tick - lastProgressTick >= StalemateProgressWindowTicks)
            {
                stalemate = true;
                break;
            }

            if (heroResources.IsAlive && enemyLife > 0 && tick >= DetailedSimulationTicks)
            {
                long heroDamageProgress = (long)(initialHeroLife - minimumHeroLife) * 1_000_000 /
                    Math.Max(1, initialHeroLife);
                long enemyDamageProgress = (long)(initialEnemyLife - minimumEnemyLife) * 1_000_000 /
                    Math.Max(1, initialEnemyLife);
                if (enemyDamageProgress > heroDamageProgress)
                {
                    enemyLife = 0;
                }
                else if (heroDamageProgress > enemyDamageProgress)
                {
                    heroResources.ApplyDamage(checked(heroResources.Life + heroResources.Shield), tick);
                }
                else
                {
                    stalemate = true;
                }
                break;
            }

            bool pendingCorpseExplosion = corpseExplosionTick > tick;
            if ((!heroResources.IsAlive || enemyLife == 0) && !pendingCorpseExplosion)
            {
                break;
            }
        }

        int elapsedTicks = request.MaximumTicks == 0 ? tick + 1 : Math.Min(tick + 1, request.MaximumTicks);
        bool diagnosticLimitReached = request.MaximumTicks > 0 && tick >= request.MaximumTicks;
        BattleOutcome outcome = stalemate
            ? BattleOutcome.Draw
            : (heroResources.IsAlive, enemyLife > 0, diagnosticLimitReached) switch
        {
            (_, _, true) => BattleOutcome.Timeout,
            (true, false, false) => BattleOutcome.HeroVictory,
            (false, true, false) => BattleOutcome.EnemyVictory,
            (false, false, false) => BattleOutcome.Draw,
            _ => BattleOutcome.Timeout,
        };
        events.Add(new CombatEvent(elapsedTicks - 1, CombatEventKind.BattleEnded, Detail: outcome.ToString()));
        string hash = Hash(seed, outcome, elapsedTicks, heroResources, enemyLife, events);
        return new EncounterResult(
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
        EncounterRequest request,
        BossPhaseState? bossState,
        Pcg32 random,
        ResourceState heroResources,
        BleedCollection heroBleeds,
        int tick,
        ICollection<CombatEvent> events)
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
            events.Add(new CombatEvent(tick, CombatEventKind.EnemyMissed));
            return;
        }

        heroResources.ApplyDamage(damage.FinalPhysicalDamage, tick);
        events.Add(new CombatEvent(tick, CombatEventKind.EnemyHit, damage.FinalPhysicalDamage));
        if (damage.AppliedBleed && heroResources.IsAlive)
        {
            heroBleeds.Apply(2, damage.BleedTotalDamage, tick, damage.BleedDurationTicks);
            events.Add(new CombatEvent(tick, CombatEventKind.BleedApplied, damage.BleedTotalDamage, "hero"));
        }
    }

    private static string Hash(
        ulong seed,
        BattleOutcome outcome,
        int ticks,
        ResourceState hero,
        int enemyLife,
        IEnumerable<CombatEvent> events)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write("GFWCampaignB1"u8);
        writer.Write(seed);
        writer.Write((byte)outcome);
        writer.Write(ticks);
        writer.Write(hero.Life);
        writer.Write(hero.Mana);
        writer.Write(hero.Shield);
        writer.Write(enemyLife);
        foreach (CombatEvent item in events)
        {
            writer.Write(item.Tick);
            writer.Write((byte)item.Kind);
            writer.Write(item.Value);
            writer.Write(item.Detail);
        }

        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    private static void Validate(EncounterRequest request)
    {
        if (request.MaximumTicks < 0 || request.HeavyStrike.SkillId != SkillIds.HeavyStrike ||
            request.LifeFlaskUseThresholdBasisPoints is < 0 or > 10_000)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Encounter request is invalid.");
        }
    }

    private static int DivideRoundUp(int numerator, int denominator) =>
        checked((numerator + denominator - 1) / denominator);
}
