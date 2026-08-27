using System.Security.Cryptography;
using System.Text;
using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P1.World;
using GameForWork.Core.Simulation;

namespace GameForWork.Core.P4;

public enum P4UnitRole
{
    Melee,
    Ranged,
    Caster,
    Charger,
    Summoner,
    Boss,
}

public enum P4SpatialEventKind
{
    HeroMoved,
    EnemyMoved,
    WarCry,
    HeavyStrike,
    EarthCleave,
    SpiritBladeLaunched,
    SpiritBladeHit,
    ChainHit,
    EnemyAttack,
    Bleed,
    Flask,
    EnemyDefeated,
    NodeCleared,
    HeroDefeated,
}

public readonly record struct P4Point(int XRaw, int YRaw)
{
    public static long DistanceSquared(P4Point left, P4Point right)
    {
        long x = left.XRaw - right.XRaw;
        long y = left.YRaw - right.YRaw;
        return x * x + y * y;
    }

    public static P4Point MoveToward(P4Point from, P4Point to, int maximumDistanceRaw)
    {
        long squared = DistanceSquared(from, to);
        if (squared == 0 || squared <= (long)maximumDistanceRaw * maximumDistanceRaw)
        {
            return to;
        }

        long distance = IntegerSqrt(squared);
        int x = checked(from.XRaw + (int)((to.XRaw - from.XRaw) * (long)maximumDistanceRaw / distance));
        int y = checked(from.YRaw + (int)((to.YRaw - from.YRaw) * (long)maximumDistanceRaw / distance));
        return new P4Point(Math.Clamp(x, 350, 11_650), Math.Clamp(y, 350, 23_650));
    }

    private static long IntegerSqrt(long value)
    {
        long result = (long)Math.Sqrt(value);
        while ((result + 1) * (result + 1) <= value)
        {
            result++;
        }

        while (result * result > value)
        {
            result--;
        }

        return Math.Max(1, result);
    }
}

public sealed record P4EnemyFrame(
    string EntityId,
    string EnemyStableId,
    string DisplayName,
    P4UnitRole Role,
    bool Elite,
    bool Boss,
    int Life,
    int MaximumLife,
    P4Point Position,
    string TargetId);

public sealed record P4SpatialFrame(
    long AtMilliseconds,
    int NodeIndex,
    P4Point HeroPosition,
    int HeroLife,
    int HeroMaximumLife,
    int HeroMana,
    int HeroMaximumMana,
    int HeroShield,
    int HeroMaximumShield,
    string HeroTargetId,
    IReadOnlyList<P4EnemyFrame> Enemies);

public sealed record P4SpatialEvent(
    long AtMilliseconds,
    P4SpatialEventKind Kind,
    string SourceId,
    string TargetId,
    int Value,
    P4Point SourcePosition,
    P4Point TargetPosition,
    string Detail);

public sealed record P4NodeCombatRequest(
    P1TeamBuild Build,
    int NodeIndex,
    int AreaLevel,
    int EnemyCount,
    bool HasElite,
    bool HasBoss,
    bool AbyssRoute,
    int Formation,
    int? InitialHeroLife = null,
    int? InitialHeroMana = null,
    int? InitialHeroShield = null,
    int MaximumTicks = 2_400);

public sealed record P4NodeCombatResult(
    P1BattleOutcome Outcome,
    int Ticks,
    int HeroLife,
    int HeroMana,
    int HeroShield,
    IReadOnlyList<P4SpatialFrame> Frames,
    IReadOnlyList<P4SpatialEvent> Events,
    string FinalHash);

public sealed class P4SpatialCombatRunner
{
    public const int TickMilliseconds = 50;
    private const int HeroEntityRawSpeed = 4_000;
    private const int HeavyStrikeRange = 1_500;
    private const int CleaveRange = 2_800;
    private const int BladeRange = 8_000;
    private const int ChainRange = 4_000;

    public P4NodeCombatResult Run(P4NodeCombatRequest request, ulong seed)
    {
        Validate(request);
        var random = new Pcg32(seed);
        var hero = new ResourceState(
            request.Build.Sheet,
            request.InitialHeroLife,
            request.InitialHeroMana,
            request.InitialHeroShield);
        var enemies = CreateEnemies(request, random);
        var events = new List<P4SpatialEvent>();
        var frames = new List<P4SpatialFrame>();
        var projectiles = new List<PendingBlade>();
        LifeFlaskState? flask = request.Build.LifeFlask is null ? null : new LifeFlaskState(request.Build.LifeFlask);
        SkillUseProfile heavyStrike = request.Build.HeavyStrikeProfile ?? SkillRules.BuildHeavyStrike(
            request.Build.HeavyStrike,
            request.Build.Weapon,
            hero.MaximumLife);
        heavyStrike = P1LegendaryRules.ApplyToHeavyStrike(heavyStrike, request.Build.WeaponLegendaryRule);
        var warCry = new WarCryState { EchoNotableAllocated = request.Build.EchoNotableAllocated };
        P4Point heroPosition = new(6_000, 22_000);
        string heroTargetId = string.Empty;
        int heroNextActionTick = 0;
        int cleaveReadyTick = 0;
        int bladeReadyTick = 0;
        int tick;
        CaptureFrame(frames, 0, request.NodeIndex, heroPosition, hero, heroTargetId, enemies);

        for (tick = 0; tick < request.MaximumTicks && hero.IsAlive && enemies.Any(enemy => enemy.Life > 0); tick++)
        {
            hero.AdvanceRegenerationTick(tick);
            warCry.AdvanceTick();
            ResolveFlask(request, flask, hero, heroPosition, tick, events);
            ResolveBleeds(enemies, tick, events);
            ResolveProjectiles(request, projectiles, enemies, random, tick, events);

            P4EnemyUnit? target = SelectTarget(enemies, heroPosition);
            heroTargetId = target?.EntityId ?? string.Empty;
            if (target is not null && tick >= heroNextActionTick)
            {
                if (request.Build.UseWarCry && warCry.TryActivate(hero, tick))
                {
                    events.Add(Event(tick, P4SpatialEventKind.WarCry, "hero", target.EntityId, 0,
                        heroPosition, target.Position, "area:6000"));
                    heroNextActionTick = tick + P1Skills.WarCry.CastTimeTicks;
                }
                else
                {
                    long distance = P4Point.DistanceSquared(heroPosition, target.Position);
                    P4EnemyUnit[] cleaveTargets = enemies.Where(enemy => enemy.Life > 0 &&
                        InRange(heroPosition, enemy.Position, CleaveRange)).ToArray();
                    if (tick >= cleaveReadyTick && cleaveTargets.Length >= 2 && hero.TryPayMana(P1Skills.EarthCleave.BaseManaCost))
                    {
                        foreach (P4EnemyUnit enemy in cleaveTargets)
                        {
                            ApplyHeroHit(request, enemy, random, tick, 8_000, P4SpatialEventKind.EarthCleave,
                                heroPosition, events, request.Build.IncreasedBleedChanceBasisPoints / 2);
                        }

                        cleaveReadyTick = tick + P1Skills.EarthCleave.CooldownTicks;
                        heroNextActionTick = tick + P1Skills.EarthCleave.CastTimeTicks;
                    }
                    else if (tick >= bladeReadyTick && distance <= (long)BladeRange * BladeRange &&
                             hero.TryPayMana(P1Skills.SpiritBlade.BaseManaCost))
                    {
                        int travelTicks = TravelTicks(heroPosition, target.Position, 10_000);
                        projectiles.Add(new PendingBlade(tick + travelTicks, target.EntityId, 0, 9_000,
                            [target.EntityId], heroPosition));
                        events.Add(Event(tick, P4SpatialEventKind.SpiritBladeLaunched, "hero", target.EntityId, 0,
                            heroPosition, target.Position, "projectile"));
                        bladeReadyTick = tick + P1Skills.SpiritBlade.CooldownTicks;
                        heroNextActionTick = tick + P1Skills.SpiritBlade.CastTimeTicks;
                    }
                    else if (distance <= (long)heavyStrike.RangeRaw * heavyStrike.RangeRaw &&
                             SkillRules.TryPaySkillCost(hero, heavyStrike))
                    {
                        int warCryMultiplier = warCry.ConsumeHeavyStrikeMultiplier(tick);
                        ApplyHeroHit(request, target, random, tick, warCryMultiplier,
                            P4SpatialEventKind.HeavyStrike, heroPosition, events,
                            checked(request.Build.IncreasedBleedChanceBasisPoints + heavyStrike.BleedChanceBasisPoints));
                        heroNextActionTick = tick + heavyStrike.AttackIntervalTicks;
                    }
                    else
                    {
                        int speed = Math.Max(1, checked(HeroEntityRawSpeed * request.Build.MovementSpeedBasisPoints / 10_000 / 20));
                        P4Point next = P4Point.MoveToward(heroPosition, target.Position, speed);
                        if (next != heroPosition)
                        {
                            heroPosition = next;
                            if ((tick & 3) == 0)
                            {
                                events.Add(Event(tick, P4SpatialEventKind.HeroMoved, "hero", target.EntityId, 0,
                                    heroPosition, target.Position, "move"));
                            }
                        }
                    }
                }
            }

            ResolveEnemies(request, enemies, hero, heroPosition, random, tick, events);
            if ((tick & 3) == 0 || events.Count > 0 && events[^1].AtMilliseconds == tick * TickMilliseconds)
            {
                CaptureFrame(frames, tick * TickMilliseconds, request.NodeIndex, heroPosition, hero, heroTargetId, enemies);
            }
        }

        bool victory = enemies.All(enemy => enemy.Life <= 0);
        P1BattleOutcome outcome = victory
            ? P1BattleOutcome.HeroVictory
            : hero.IsAlive ? P1BattleOutcome.Timeout : P1BattleOutcome.EnemyVictory;
        events.Add(Event(tick, victory ? P4SpatialEventKind.NodeCleared : P4SpatialEventKind.HeroDefeated,
            victory ? "hero" : "enemies", string.Empty, 0, heroPosition, heroPosition, outcome.ToString()));
        CaptureFrame(frames, tick * TickMilliseconds, request.NodeIndex, heroPosition, hero, heroTargetId, enemies);
        string hash = Hash(seed, outcome, tick, hero, enemies, events);
        return new P4NodeCombatResult(outcome, tick, hero.Life, hero.Mana, hero.Shield, frames, events, hash);
    }

    private static List<P4EnemyUnit> CreateEnemies(P4NodeCombatRequest request, Pcg32 random)
    {
        var result = new List<P4EnemyUnit>(request.EnemyCount);
        for (int index = 0; index < request.EnemyCount; index++)
        {
            bool boss = request.HasBoss && index == 0;
            bool elite = !boss && request.HasElite && index == 0;
            EnemyProfile profile = boss
                ? P1Enemies.AbyssWarden
                : P1Enemies.NormalEnemies[(int)(random.NextUInt() % (uint)P1Enemies.NormalEnemies.Count)];
            IReadOnlyList<EliteAffix> affixes = elite ? EnemyRules.RollEliteAffixes(random) : [];
            ScaledEnemy scaled = EnemyRules.Scale(profile, request.AreaLevel, affixes, request.AbyssRoute);
            int lifeScale = boss ? 10_000 : elite ? 7_000 : 4_500;
            int life = Math.Max(2, checked(scaled.Life * lifeScale / 10_000));
            P4UnitRole role = boss ? P4UnitRole.Boss : (P4UnitRole)(index % 5);
            P4Point position = SpawnPosition(request.Formation, index, request.EnemyCount, random);
            result.Add(new P4EnemyUnit(
                $"enemy-{request.NodeIndex}-{index}", profile, scaled, role, elite, boss, life, position, index * 3));
        }

        return result;
    }

    private static P4Point SpawnPosition(int formation, int index, int count, Pcg32 random)
    {
        int jitterX = (int)(random.NextUInt() % 401) - 200;
        int jitterY = (int)(random.NextUInt() % 401) - 200;
        return (formation % 3) switch
        {
            0 => new P4Point(1_500 + index % 6 * 1_800 + jitterX, 2_500 + index / 6 * 1_600 + jitterY),
            1 => new P4Point(index % 2 == 0 ? 1_000 + jitterX : 11_000 + jitterX,
                3_000 + index % 8 * 1_900 + jitterY),
            _ => RingPosition(index, count, jitterX, jitterY),
        };
    }

    private static P4Point RingPosition(int index, int count, int jitterX, int jitterY)
    {
        double angle = index * Math.PI * 2 / Math.Max(1, count);
        return new P4Point(
            Math.Clamp(6_000 + (int)(Math.Cos(angle) * 5_000) + jitterX, 500, 11_500),
            Math.Clamp(11_000 + (int)(Math.Sin(angle) * 8_000) + jitterY, 500, 19_500));
    }

    private static P4EnemyUnit? SelectTarget(IEnumerable<P4EnemyUnit> enemies, P4Point heroPosition) => enemies
        .Where(enemy => enemy.Life > 0)
        .OrderByDescending(enemy => enemy.Boss)
        .ThenByDescending(enemy => enemy.Elite)
        .ThenByDescending(enemy => enemy.Scaled.Base.ThreatPoints)
        .ThenBy(enemy => P4Point.DistanceSquared(heroPosition, enemy.Position))
        .ThenBy(enemy => enemy.Life)
        .FirstOrDefault();

    private static void ResolveEnemies(
        P4NodeCombatRequest request,
        IEnumerable<P4EnemyUnit> enemies,
        ResourceState hero,
        P4Point heroPosition,
        Pcg32 random,
        int tick,
        ICollection<P4SpatialEvent> events)
    {
        foreach (P4EnemyUnit enemy in enemies.Where(enemy => enemy.Life > 0))
        {
            int range = EnemyRange(enemy.Role);
            long distance = P4Point.DistanceSquared(enemy.Position, heroPosition);
            if (distance > (long)range * range ||
                enemy.Role is P4UnitRole.Ranged or P4UnitRole.Caster or P4UnitRole.Summoner && distance < 9_000_000)
            {
                P4Point destination = distance > (long)range * range
                    ? heroPosition with { XRaw = Math.Clamp(heroPosition.XRaw + LaneOffset(enemy.Ordinal), 350, 11_650) }
                    : new P4Point(
                        Math.Clamp(enemy.Position.XRaw + Math.Sign(enemy.Position.XRaw - heroPosition.XRaw) * 700, 350, 11_650),
                        Math.Clamp(enemy.Position.YRaw + Math.Sign(enemy.Position.YRaw - heroPosition.YRaw) * 700, 350, 23_650));
                int move = Math.Max(1, enemy.Profile.MovementSpeedRawPerSecond / 20);
                if (enemy.Role == P4UnitRole.Charger)
                {
                    move = move * 3 / 2;
                }

                P4Point next = P4Point.MoveToward(enemy.Position, destination, move);
                enemy.Position = next;
                if ((tick & 3) == 0)
                {
                    events.Add(Event(tick, P4SpatialEventKind.EnemyMoved, enemy.EntityId, "hero", 0,
                        enemy.Position, heroPosition, enemy.Role.ToString()));
                }

                distance = P4Point.DistanceSquared(enemy.Position, heroPosition);
            }

            if (tick < enemy.NextActionTick || distance > (long)range * range || !hero.IsAlive)
            {
                continue;
            }

            var weapon = new WeaponProfile(
                enemy.Profile.StableId + ".spatial",
                enemy.Scaled.MinimumPhysicalDamage,
                enemy.Scaled.MaximumPhysicalDamage,
                enemy.Scaled.AttacksPerSecondMilli,
                500);
            DamageResult hit = DamageRules.Resolve(new DamageRequest(
                weapon,
                TargetArmor: request.Build.Sheet.Armor().Value,
                TargetEvasion: request.Build.Sheet.Evasion().Value,
                Accuracy: enemy.Profile.Accuracy,
                IsSpell: enemy.Role == P4UnitRole.Caster), random);
            int divisor = Math.Max(2, request.EnemyCount * 2);
            int damage = hit.Hit ? hit.FinalPhysicalDamage / divisor : 0;
            if (enemy.Boss && hit.Hit)
            {
                damage = Math.Max(1, damage);
            }
            if (damage > 0)
            {
                hero.ApplyDamage(damage, tick);
            }

            events.Add(Event(tick, P4SpatialEventKind.EnemyAttack, enemy.EntityId, "hero", damage,
                enemy.Position, heroPosition, enemy.Role.ToString()));
            int interval = Math.Max(8, checked((20_000 + enemy.Scaled.AttacksPerSecondMilli - 1) /
                enemy.Scaled.AttacksPerSecondMilli));
            enemy.NextActionTick = tick + interval;
        }
    }

    private static void ApplyHeroHit(
        P4NodeCombatRequest request,
        P4EnemyUnit enemy,
        Pcg32 random,
        int tick,
        int skillMultiplier,
        P4SpatialEventKind kind,
        P4Point source,
        ICollection<P4SpatialEvent> events,
        int bleedChance)
    {
        DamageResult damage = DamageRules.Resolve(new DamageRequest(
            request.Build.Weapon,
            request.Build.AddedPhysicalDamage,
            request.Build.AddedPhysicalDamage,
            request.Build.IncreasedDamageBasisPoints,
            [skillMultiplier],
            checked(request.Build.Weapon.CriticalChanceBasisPoints + request.Build.IncreasedCriticalChanceBasisPoints),
            TargetArmor: enemy.Scaled.Armor,
            TargetEvasion: enemy.Scaled.Evasion,
            Accuracy: request.Build.Sheet.Accuracy(request.Build.FlatAccuracy).Value,
            IsSpell: kind is P4SpatialEventKind.SpiritBladeHit or P4SpatialEventKind.ChainHit,
            BleedChanceBasisPoints: bleedChance), random);
        int value = damage.Hit ? damage.FinalPhysicalDamage : 0;
        enemy.Life = Math.Max(0, enemy.Life - value);
        if (damage.AppliedBleed && enemy.Life > 0)
        {
            enemy.BleedRemaining = checked(enemy.BleedRemaining + damage.BleedTotalDamage);
            enemy.BleedPulses = Math.Max(enemy.BleedPulses, 5);
        }

        events.Add(Event(tick, kind, "hero", enemy.EntityId, value, source, enemy.Position,
            damage.Critical ? "critical" : damage.Hit ? "hit" : "miss"));
        if (enemy.Life == 0)
        {
            events.Add(Event(tick, P4SpatialEventKind.EnemyDefeated, "hero", enemy.EntityId, 0,
                source, enemy.Position, enemy.Profile.StableId));
        }
    }

    private static void ResolveProjectiles(
        P4NodeCombatRequest request,
        IList<PendingBlade> projectiles,
        IList<P4EnemyUnit> enemies,
        Pcg32 random,
        int tick,
        ICollection<P4SpatialEvent> events)
    {
        foreach (PendingBlade projectile in projectiles.Where(projectile => projectile.ImpactTick <= tick).ToArray())
        {
            projectiles.Remove(projectile);
            P4EnemyUnit? target = enemies.FirstOrDefault(enemy => enemy.EntityId == projectile.TargetId && enemy.Life > 0) ??
                                  enemies.Where(enemy => enemy.Life > 0 && !projectile.HitIds.Contains(enemy.EntityId))
                                      .OrderBy(enemy => P4Point.DistanceSquared(projectile.Origin, enemy.Position)).FirstOrDefault();
            if (target is null)
            {
                continue;
            }

            ApplyHeroHit(request, target, random, tick, projectile.Multiplier,
                projectile.ChainIndex == 0 ? P4SpatialEventKind.SpiritBladeHit : P4SpatialEventKind.ChainHit,
                projectile.Origin, events, 0);
            int maximumChains = request.Build.ActiveSkills?.FirstOrDefault(skill => skill.SkillId == P1SkillIds.SpiritBlade)?
                .Supports.HasFlag(SkillSupport.Chain) == true ? 3 : 2;
            if (projectile.ChainIndex >= maximumChains)
            {
                continue;
            }

            P4EnemyUnit? next = enemies.Where(enemy => enemy.Life > 0 && !projectile.HitIds.Contains(enemy.EntityId) &&
                    InRange(target.Position, enemy.Position, ChainRange))
                .OrderBy(enemy => P4Point.DistanceSquared(target.Position, enemy.Position)).FirstOrDefault();
            if (next is not null)
            {
                string[] hitIds = projectile.HitIds.Append(next.EntityId).ToArray();
                projectiles.Add(new PendingBlade(
                    tick + TravelTicks(target.Position, next.Position, 12_000),
                    next.EntityId,
                    projectile.ChainIndex + 1,
                    Math.Max(4_000, projectile.Multiplier - 2_000),
                    hitIds,
                    target.Position));
            }
        }
    }

    private static void ResolveBleeds(IEnumerable<P4EnemyUnit> enemies, int tick, ICollection<P4SpatialEvent> events)
    {
        if (tick == 0 || tick % 20 != 0)
        {
            return;
        }

        foreach (P4EnemyUnit enemy in enemies.Where(enemy => enemy.Life > 0 && enemy.BleedPulses > 0))
        {
            int damage = Math.Max(1, enemy.BleedRemaining / enemy.BleedPulses);
            enemy.BleedRemaining -= damage;
            enemy.BleedPulses--;
            enemy.Life = Math.Max(0, enemy.Life - damage);
            events.Add(Event(tick, P4SpatialEventKind.Bleed, "hero", enemy.EntityId, damage,
                enemy.Position, enemy.Position, "enemy"));
            if (enemy.Life == 0)
            {
                events.Add(Event(tick, P4SpatialEventKind.EnemyDefeated, "hero", enemy.EntityId, 0,
                    enemy.Position, enemy.Position, enemy.Profile.StableId));
            }
        }
    }

    private static void ResolveFlask(
        P4NodeCombatRequest request,
        LifeFlaskState? flask,
        ResourceState hero,
        P4Point heroPosition,
        int tick,
        ICollection<P4SpatialEvent> events)
    {
        if (flask is null || !hero.IsAlive ||
            (long)hero.Life * 10_000 > (long)hero.MaximumLife * request.Build.LifeFlaskUseThresholdBasisPoints)
        {
            return;
        }

        int recovered = flask.TryUse(hero.MaximumLife - hero.Life, request.Build.IncreasedLifeFlaskEffectBasisPoints);
        if (recovered > 0)
        {
            hero.HealLife(recovered);
            events.Add(Event(tick, P4SpatialEventKind.Flask, "hero", "hero", recovered,
                heroPosition, heroPosition, "life"));
        }
    }

    private static void CaptureFrame(
        ICollection<P4SpatialFrame> frames,
        long at,
        int node,
        P4Point heroPosition,
        ResourceState hero,
        string target,
        IEnumerable<P4EnemyUnit> enemies)
    {
        if (frames.LastOrDefault()?.AtMilliseconds == at)
        {
            return;
        }

        frames.Add(new P4SpatialFrame(
            at,
            node,
            heroPosition,
            hero.Life,
            hero.MaximumLife,
            hero.Mana,
            hero.MaximumMana,
            hero.Shield,
            hero.MaximumShield,
            target,
            enemies.Select(enemy => new P4EnemyFrame(
                enemy.EntityId,
                enemy.Profile.StableId,
                enemy.Profile.DisplayName,
                enemy.Role,
                enemy.Elite,
                enemy.Boss,
                enemy.Life,
                enemy.MaximumLife,
                enemy.Position,
                "hero")).ToArray()));
    }

    private static P4SpatialEvent Event(
        int tick,
        P4SpatialEventKind kind,
        string source,
        string target,
        int value,
        P4Point sourcePosition,
        P4Point targetPosition,
        string detail) => new(
        checked((long)tick * TickMilliseconds), kind, source, target, value, sourcePosition, targetPosition, detail);

    private static bool InRange(P4Point left, P4Point right, int range) =>
        P4Point.DistanceSquared(left, right) <= (long)range * range;

    private static int TravelTicks(P4Point from, P4Point to, int speedPerSecond)
    {
        long distance = (long)Math.Sqrt(P4Point.DistanceSquared(from, to));
        return Math.Max(1, checked((int)((distance * 20 + speedPerSecond - 1) / speedPerSecond)));
    }

    private static int EnemyRange(P4UnitRole role) => role switch
    {
        P4UnitRole.Ranged => 6_000,
        P4UnitRole.Caster => 7_000,
        P4UnitRole.Summoner => 5_500,
        P4UnitRole.Boss => 2_000,
        _ => 1_200,
    };

    private static int LaneOffset(int ordinal) => (ordinal % 5 - 2) * 350;

    private static string Hash(
        ulong seed,
        P1BattleOutcome outcome,
        int ticks,
        ResourceState hero,
        IEnumerable<P4EnemyUnit> enemies,
        IEnumerable<P4SpatialEvent> events)
    {
        string source = $"P4|{seed}|{outcome}|{ticks}|{hero.Life}|{hero.Mana}|{hero.Shield}|" +
                        string.Join(';', enemies.Select(enemy => $"{enemy.EntityId}:{enemy.Life}:{enemy.Position.XRaw}:{enemy.Position.YRaw}")) +
                        "|" + string.Join(';', events.Select(item => $"{item.AtMilliseconds}:{item.Kind}:{item.TargetId}:{item.Value}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant();
    }

    private static void Validate(P4NodeCombatRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Build);
        if (request.NodeIndex <= 0 || request.AreaLevel is < 1 or > 10 || request.EnemyCount is < 1 or > 24 ||
            request.MaximumTicks <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request));
        }
    }

    private sealed class P4EnemyUnit(
        string entityId,
        EnemyProfile profile,
        ScaledEnemy scaled,
        P4UnitRole role,
        bool elite,
        bool boss,
        int life,
        P4Point position,
        int nextActionTick)
    {
        public string EntityId { get; } = entityId;
        public EnemyProfile Profile { get; } = profile;
        public ScaledEnemy Scaled { get; } = scaled;
        public P4UnitRole Role { get; } = role;
        public bool Elite { get; } = elite;
        public bool Boss { get; } = boss;
        public int MaximumLife { get; } = life;
        public int Ordinal { get; } = int.Parse(entityId[(entityId.LastIndexOf('-') + 1)..]);
        public int Life { get; set; } = life;
        public P4Point Position { get; set; } = position;
        public int NextActionTick { get; set; } = nextActionTick;
        public int BleedRemaining { get; set; }
        public int BleedPulses { get; set; }
    }

    private sealed record PendingBlade(
        int ImpactTick,
        string TargetId,
        int ChainIndex,
        int Multiplier,
        IReadOnlyList<string> HitIds,
        P4Point Origin);
}
