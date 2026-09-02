using System.Security.Cryptography;
using System.Text;
using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P1.Progression;
using GameForWork.Core.P1.World;
using GameForWork.Core.Simulation;
using GameForWork.Core.P6;
using GameForWork.Core.P14;
using GameForWork.Core.P17;
using GameForWork.Core.P18;
using GameForWork.Core.P23;
using GameForWork.Core.P27;
using GameForWork.Core.P30;

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
    SeismicCharge,
    BloodTideSpin,
    BannerActivated,
    SkillFailed,
    AshJavelin,
    EmberNova,
    StormBrand,
    BossTelegraph,
    BossPhaseChanged,
    SkillEffect,
    Ailment,
    Block,
    Guard,
    Ascendancy,
    FlaskCharge,
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
    EnemyRarity Rarity,
    bool Elite,
    bool Boss,
    int Life,
    int MaximumLife,
    P4Point Position,
    string TargetId,
    IReadOnlyList<EliteAffix>? EliteAffixes = null, bool Summoned = false);

public sealed record P4AllyFrame(string EntityId, P4Point Position, bool Frontline);

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
    IReadOnlyList<P4EnemyFrame> Enemies,
    IReadOnlyList<P4AllyFrame>? Allies = null);

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
    int MaximumTicks = 0,
    int EnemyLifeBasisPoints = 10_000,
    int EnemyDamageBasisPoints = 10_000,
    int EnemySpeedBasisPoints = 10_000,
    int PlayerRecoveryBasisPoints = 10_000,
    string BossStableId = "",
    P18CombatRuntime? AscendancyRuntime = null,
    int BossLifeBasisPoints = 10_000,
    int BossDamageBasisPoints = 10_000,
    int EnemyPhysicalReductionBasisPoints = 0,
    int EnemyElementalResistanceBasisPoints = 0,
    int EnemyVoidResistanceBasisPoints = 0,
    int EnemyPenetrationBasisPoints = 0,
    int ExtraEnemyProjectiles = 0,
    int EnemyProjectileDamageBasisPoints = 10_000,
    int EnemyAreaBasisPoints = 10_000,
    int EnemyAreaDamageBasisPoints = 10_000,
    int BossCount = 1,
    int AdditionalRareEnemies = 0,
    EnemyFamily? EncounterFamily = null,
    int FlaskRecoveryBasisPoints = 10_000,
    int IncomingHitBasisPoints = 10_000,
    bool ExtraBossPhase = false,
    IReadOnlyList<string>? GardenTags = null,
    IReadOnlyList<EnemyProfile>? EnemyPool = null,
    P30VirtueViceState? VirtueVice = null);

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
    private const int StalemateProgressWindowTicks = 1_200;
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
        var aftershocks = new List<PendingAftershock>();
        var hazards = new List<EnemyHazard>();
        int rootedUntilTick = 0;
        int dodgeUntilTick = 0, dodgeReadyTick = 0;
        P18CombatProfile ascendancy = request.Build.Ascendancy ?? P18CombatProfile.Empty;
        var ascendancyRuntime = new P18CombatRuntime(ascendancy);
        P30VirtueViceKind[] held = P30Ascendancies.PermanentVirtueVice(ascendancy).ToArray();
        P30VirtueViceLoadout loadout = request.Build.VirtueViceLoadout ?? P30VirtueViceLoadout.Empty;
        held = held.Concat(loadout.HeldAtMaximum).Distinct().ToArray();
        var maxima = new Dictionary<P30VirtueViceKind, int>(loadout.AdditionalMaximum);
        foreach (P30VirtueViceKind kind in P30Ascendancies.PermanentVirtueVice(ascendancy))
            maxima[kind] = maxima.GetValueOrDefault(kind) + 1;
        var virtueVice = request.VirtueVice ?? new P30VirtueViceState(
            maxima, held);
        request = request with { AscendancyRuntime = ascendancyRuntime, VirtueVice = virtueVice };
        HashSet<P1FlaskKind> flaskKinds = (request.Build.Flasks ??
            (request.Build.LifeFlask is null ? [] : [P1FlaskKind.Life])).ToHashSet();
        LifeFlaskState? flask = request.Build.LifeFlask is null || !flaskKinds.Contains(P1FlaskKind.Life)
            ? null : new LifeFlaskState(request.Build.LifeFlask);
        Dictionary<P1FlaskKind, P1UtilityFlaskState> utilityFlasks = flaskKinds
            .Where(kind => kind != P1FlaskKind.Life)
            .ToDictionary(kind => kind, kind =>
            {
                P14FlaskDefinition definition = P14Flasks.All.Single(item => item.Kind == kind);
                return new P1UtilityFlaskState(definition.MaximumCharges, definition.ChargesPerUse, definition.DurationTicks);
            });
        SkillUseProfile heavyStrike = request.Build.HeavyStrikeProfile ?? SkillRules.BuildHeavyStrike(
            (request.Build.ActiveSkills ?? []).FirstOrDefault(skill => skill.SkillId == P1SkillIds.HeavyStrike) ?? request.Build.HeavyStrike,
            request.Build.Weapon,
            hero.MaximumLife);
        heavyStrike = P1LegendaryRules.ApplyToHeavyStrike(heavyStrike, request.Build.WeaponLegendaryRule);
        heavyStrike = P18AscendancyRules.ApplyHeavyStrikeCost(heavyStrike, hero.MaximumLife, ascendancy);
        var warCry = new WarCryState { EchoNotableAllocated = request.Build.EchoNotableAllocated };
        Dictionary<string, SkillConfiguration> skills = (request.Build.ActiveSkills ?? [request.Build.HeavyStrike])
            .GroupBy(skill => skill.SkillId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        HashSet<string> legacySkills =
        [
            P1SkillIds.HeavyStrike, P1SkillIds.WarCry, P1SkillIds.EarthCleave, P1SkillIds.SpiritBlade,
            P1SkillIds.SeismicCharge, P1SkillIds.BloodTideSpin, P1SkillIds.IronOathBanner,
            P1SkillIds.AshJavelin, P1SkillIds.EmberNova, P1SkillIds.StormBrand,
        ];
        Dictionary<string, P6ResolvedSkill> p17Skills = skills
            .Where(pair => !legacySkills.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => ApplyAscendancyCost(
                P6CombatSkillRules.Resolve(pair.Value, hero.MaximumLife, request.Build.PassiveProfile), pair.Value, hero.MaximumLife, ascendancy), StringComparer.Ordinal);
        Dictionary<string, int> p17ReadyTicks = p17Skills.Keys.ToDictionary(key => key, _ => 0, StringComparer.Ordinal);
        Dictionary<string, int> p17UseCounts = p17Skills.Keys.ToDictionary(key => key, _ => 0, StringComparer.Ordinal);
        P6ResolvedSkill? shieldCounter = p17Skills.GetValueOrDefault(P1SkillIds.VengefulCounter);
        SkillConfiguration? shieldCounterConfiguration = skills.GetValueOrDefault(P1SkillIds.VengefulCounter);
        int shieldCounterReadyTick = 0;
        P6ResolvedSkill? Asc(string id)
        {
            if (Resolve(skills, id, hero.MaximumLife, request.Build.PassiveProfile) is not { } resolved) return null;
            resolved = ApplyAscendancyCost(resolved, skills[id], hero.MaximumLife, ascendancy);
            if (id != P1SkillIds.WarCry) return resolved;
            return resolved with
            {
                RangeRaw = checked(resolved.RangeRaw * (10_000 + request.Build.IncreasedWarCryRangeBasisPoints) / 10_000),
                CooldownTicks = Math.Max(1, checked(resolved.CooldownTicks * 10_000 /
                    Math.Max(1, 10_000 + request.Build.IncreasedWarCryCooldownRecoveryBasisPoints))),
            };
        }
        P6ResolvedSkill? cleave = Asc(P1SkillIds.EarthCleave);
        P6ResolvedSkill? blade = Asc(P1SkillIds.SpiritBlade);
        P6ResolvedSkill? charge = Asc(P1SkillIds.SeismicCharge);
        P6ResolvedSkill? spin = Asc(P1SkillIds.BloodTideSpin);
        P6ResolvedSkill? banner = Asc(P1SkillIds.IronOathBanner);
        P6ResolvedSkill? warCrySkill = Asc(P1SkillIds.WarCry);
        P6ResolvedSkill? heavyResolved = Asc(P1SkillIds.HeavyStrike);
        P6ResolvedSkill? ashJavelin = Asc(P1SkillIds.AshJavelin);
        P6ResolvedSkill? emberNova = Asc(P1SkillIds.EmberNova);
        P6ResolvedSkill? stormBrand = Asc(P1SkillIds.StormBrand);
        if (warCrySkill is not null)
        {
            warCry.ManaCost = warCrySkill.ManaCost;
            warCry.CooldownDurationTicks = warCrySkill.CooldownTicks;
            warCry.EffectMultiplierBasisPoints = skills[P1SkillIds.WarCry].Supports.HasFlag(SkillSupport.UrgentWarCry) ? 8_500 : 10_000;
            if (ascendancy.Has(P18NodeIds.BreakerWarCrySmall))
                warCry.CooldownDurationTicks = Math.Max(1, warCry.CooldownDurationTicks * 10_000 / 13_000);
        }
        P4Point heroPosition = new(6_000, 22_000);
        string heroTargetId = string.Empty;
        int heroNextActionTick = 0;
        int cleaveReadyTick = 0;
        int bladeReadyTick = 0;
        int chargeReadyTick = 0;
        int spinReadyTick = 0;
        int bannerMultiplier = 10_000;
        int ashJavelinReadyTick = 0;
        int emberNovaReadyTick = 0;
        int stormBrandReadyTick = 0;
        int guardUntilTick = 0;
        int guardReductionBasisPoints = 0;
        int bannerReservation = ascendancy.Has(P18NodeIds.BastionGuardCore) ? 750 : 1_500;
        if (banner is not null && hero.TryPayMana(Math.Max(1, hero.MaximumMana * bannerReservation / 10_000)))
        {
            bannerMultiplier = ascendancy.Has(P18NodeIds.BastionGuardCore) ? 11_950 : 11_500;
            events.Add(Event(0, P4SpatialEventKind.BannerActivated, "hero", "hero", 0,
                heroPosition, heroPosition, $"reservation:{bannerReservation / 100.0:0.#}"));
        }
        foreach (P6ResolvedSkill reservation in p17Skills.Values.Where(item => item.Role == P17SkillRole.Reservation))
        {
            int reservationBasisPoints = reservation.SkillId == P1SkillIds.ElementalResonance ? 2_000 : 1_000;
            if (!hero.TryPayMana(Math.Max(1, hero.MaximumMana * reservationBasisPoints / 10_000))) continue;
            bannerMultiplier = checked(bannerMultiplier * 11_000 / 10_000);
            events.Add(Event(0, P4SpatialEventKind.BannerActivated, "hero", "hero", 0,
                heroPosition, heroPosition, $"skill:{reservation.SkillId}|reservation:{reservationBasisPoints}"));
        }
        int tick;
        int initialHeroLife = hero.Life;
        int minimumHeroLife = hero.Life;
        int initialEnemyLife = enemies.Sum(enemy => enemy.MaximumLife);
        int minimumEnemyLife = initialEnemyLife;
        int lastProgressTick = 0;
        P1BattleOutcome? projectedOutcome = null;
        CaptureFrame(frames, 0, request.NodeIndex, heroPosition, hero, heroTargetId, enemies,
            request.Build.PartySize, request.Build.FrontlineCount);

        for (tick = 0; (request.MaximumTicks == 0 || tick < request.MaximumTicks) &&
             hero.IsAlive && enemies.Any(enemy => enemy.Life > 0); tick++)
        {
            virtueVice.Advance(TickMilliseconds);
            P4Point beforeMovement = heroPosition;
            foreach (P4EnemyUnit unit in enemies)
                unit.LinkedBy = enemies.FirstOrDefault(source => source != unit && source.Life > 0 &&
                    source.ShieldUntilTick > tick && InRange(source.Position, unit.Position, 4_000));
            if (tick >= rootedUntilTick)
            {
                P4EnemyUnit? warning = enemies.FirstOrDefault(e => e.Life > 0 && e.TelegraphTarget is not null &&
                    InRange(heroPosition, e.TelegraphTarget.Value, 2_000));
                P4Point? danger = warning?.TelegraphTarget ?? hazards.FirstOrDefault(h =>
                    h.Expires > tick && InRange(heroPosition, h.Position, h.Radius))?.Position;
                if (danger is not null && tick >= dodgeReadyTick)
                {
                    dodgeUntilTick = tick + 10;
                    dodgeReadyTick = tick + 40;
                }
                if (danger is not null && tick < dodgeUntilTick)
                {
                    int direction = heroPosition.XRaw >= danger.Value.XRaw ? 1 : -1;
                    if (heroPosition.XRaw >= 11_300) direction = -1;
                    if (heroPosition.XRaw <= 700) direction = 1;
                    heroPosition = P4Point.MoveToward(heroPosition,
                        heroPosition with { XRaw = Math.Clamp(heroPosition.XRaw + direction * 3_000, 350, 11_650) },
                        Math.Max(1, request.Build.MovementSpeedBasisPoints * 300 / 10_000));
                }
            }
            hero.AdvanceRegenerationTick(tick);
            if (tick > 0 && tick % 20 == 0)
            {
                ascendancyRuntime.AdvanceSecond();
                int passiveRecovery = ascendancyRuntime.PassiveRecoveryBasisPoints;
                if (passiveRecovery > 0) hero.HealLife(Math.Max(1, hero.MaximumLife * passiveRecovery / 10_000));
                if (tick < guardUntilTick && ascendancy.Has(P18NodeIds.BastionGuardCore))
                    hero.HealLife(Math.Max(1, hero.MaximumLife * 500 / 10_000));
            }
            warCry.AdvanceTick();
            foreach (P1UtilityFlaskState state in utilityFlasks.Values) state.AdvanceTick();
            ResolveFlask(request, flask, hero, heroPosition, tick, events);
            if (utilityFlasks.GetValueOrDefault(P1FlaskKind.Mana) is { } manaFlask &&
                hero.Mana * 10_000L < hero.MaximumMana * 3_500L && manaFlask.TryUse())
            {
                int restored = hero.RestoreMana(Math.Max(0, (int)((long)hero.MaximumMana * 3_000 / 10_000 * request.FlaskRecoveryBasisPoints / 10_000)));
                events.Add(Event(tick, P4SpatialEventKind.Flask, "hero", "hero", restored,
                    heroPosition, heroPosition, "mana"));
            }
            if (hero.LastDamageTick >= tick - 1)
            {
                ActivateUtility(utilityFlasks, P1FlaskKind.Armor, tick, heroPosition, events);
                ActivateUtility(utilityFlasks, P1FlaskKind.Resistance, tick, heroPosition, events);
            }
            ResolveBleeds(enemies, hero, ascendancyRuntime, tick, events);
            ResolveP17DamageOverTime(enemies, tick, events);
            ResolveProjectiles(request, projectiles, enemies, hero, random, tick, events);
            ResolveAftershocks(aftershocks, enemies, tick, events);

            P4EnemyUnit? target = SelectTarget(enemies, heroPosition);
            heroTargetId = target?.EntityId ?? string.Empty;
            if (target is not null && tick >= heroNextActionTick && heroPosition == beforeMovement)
            {
                var skillTargets = skills.ToDictionary(
                    pair => pair.Key,
                    pair => SelectTarget(enemies, heroPosition,
                        pair.Value.AiRule?.TargetPolicy ?? SkillTargetPolicy.AllEnemies),
                    StringComparer.Ordinal);
                P4EnemyUnit? SkillTarget(string skillId) => skillTargets.GetValueOrDefault(skillId) ?? target;
                long SkillDistance(string skillId) => P4Point.DistanceSquared(heroPosition, SkillTarget(skillId)!.Position);
                int ConeCount(string skillId, int range) => SkillTarget(skillId) is not P4EnemyUnit selected ? 0 :
                    enemies.Count(enemy => enemy.Life > 0 &&
                        InCleaveCone(heroPosition, selected.Position, enemy.Position, range));
                int NearbyCount(string skillId, int range) => SkillTarget(skillId) is null ? 0 :
                    enemies.Count(enemy => enemy.Life > 0 && InRange(heroPosition, enemy.Position, range));
                string? chosen = new[]
                    {
                        Candidate(P1SkillIds.WarCry, request.Build.UseWarCry && warCrySkill is not null && warCry.IsReady &&
                            hero.Mana >= warCry.ManaCost),
                        Candidate(P1SkillIds.SeismicCharge, charge is not null && tick >= chargeReadyTick &&
                            SkillTarget(P1SkillIds.SeismicCharge) is not null &&
                            SkillDistance(P1SkillIds.SeismicCharge) > (long)HeavyStrikeRange * HeavyStrikeRange &&
                            SkillDistance(P1SkillIds.SeismicCharge) <= (long)charge.RangeRaw * charge.RangeRaw && CanPay(hero, charge)),
                        Candidate(P1SkillIds.BloodTideSpin, spin is not null && tick >= spinReadyTick &&
                            SkillTarget(P1SkillIds.BloodTideSpin) is not null && NearbyCount(P1SkillIds.BloodTideSpin, spin.RangeRaw) >= 2 && CanPay(hero, spin)),
                        Candidate(P1SkillIds.EarthCleave, cleave is not null && tick >= cleaveReadyTick &&
                            SkillTarget(P1SkillIds.EarthCleave) is not null && ConeCount(P1SkillIds.EarthCleave, cleave.RangeRaw) >= 2 && CanPay(hero, cleave)),
                        Candidate(P1SkillIds.SpiritBlade, blade is not null && tick >= bladeReadyTick &&
                            SkillTarget(P1SkillIds.SpiritBlade) is not null && SkillDistance(P1SkillIds.SpiritBlade) <= (long)blade.RangeRaw * blade.RangeRaw && CanPay(hero, blade)),
                        Candidate(P1SkillIds.AshJavelin, ashJavelin is not null && tick >= ashJavelinReadyTick &&
                            SkillTarget(P1SkillIds.AshJavelin) is not null && SkillDistance(P1SkillIds.AshJavelin) <= (long)ashJavelin.RangeRaw * ashJavelin.RangeRaw && CanPay(hero, ashJavelin)),
                        Candidate(P1SkillIds.EmberNova, emberNova is not null && tick >= emberNovaReadyTick &&
                            SkillTarget(P1SkillIds.EmberNova) is not null && NearbyCount(P1SkillIds.EmberNova, emberNova.RangeRaw) >= 2 && CanPay(hero, emberNova)),
                        Candidate(P1SkillIds.StormBrand, stormBrand is not null && tick >= stormBrandReadyTick &&
                            SkillTarget(P1SkillIds.StormBrand) is not null && SkillDistance(P1SkillIds.StormBrand) <= (long)stormBrand.RangeRaw * stormBrand.RangeRaw && CanPay(hero, stormBrand)),
                        Candidate(P1SkillIds.HeavyStrike, skills.ContainsKey(P1SkillIds.HeavyStrike) &&
                            SkillTarget(P1SkillIds.HeavyStrike) is not null && SkillDistance(P1SkillIds.HeavyStrike) <= (long)heavyStrike.RangeRaw * heavyStrike.RangeRaw &&
                            (heavyStrike.LifeCost > 0 ? hero.Life > heavyStrike.LifeCost : hero.Mana >= heavyStrike.ManaCost)),
                    }
                    .Where(candidate => candidate is not null && AiMatches(skills[candidate], request, hero,
                        SkillTarget(candidate)!, enemies, SkillDistance(candidate)))
                    .OrderBy(candidate => skills[candidate!].Priority)
                    .FirstOrDefault();
                string? p17Chosen = p17Skills.Values
                    .Where(skill => skill.Role is not P17SkillRole.Reservation and not P17SkillRole.Counter &&
                                    (!skill.RequiresShield || request.Build.HasShield) && tick >= p17ReadyTicks[skill.SkillId] &&
                                    SkillTarget(skill.SkillId) is not null &&
                                    (skill.Shape == P17SkillShape.Self ||
                                     SkillDistance(skill.SkillId) <= (long)skill.RangeRaw * skill.RangeRaw) && CanPay(hero, skill) &&
                                    AiMatches(skills[skill.SkillId], request, hero, SkillTarget(skill.SkillId)!, enemies, SkillDistance(skill.SkillId)))
                    .OrderBy(skill => skills[skill.SkillId].Priority)
                    .Select(skill => skill.SkillId)
                    .FirstOrDefault();
                if (p17Chosen is not null && (chosen is null || skills[p17Chosen].Priority < skills[chosen].Priority))
                    chosen = p17Chosen;

                if (chosen is not null)
                {
                    target = SkillTarget(chosen)!;
                    heroTargetId = target.EntityId;
                }
                long distance = P4Point.DistanceSquared(heroPosition, target.Position);
                int cleaveRange = cleave is null ? 0 : cleave.RangeRaw *
                    (ascendancyRuntime.MarchReady && ascendancy.Has(P18NodeIds.BreakerMarchCore) ? 15_000 : 10_000) / 10_000;
                P4EnemyUnit[] cleaveTargets = cleave is null ? [] : enemies.Where(enemy => enemy.Life > 0 &&
                    InCleaveCone(heroPosition, target.Position, enemy.Position, cleaveRange)).ToArray();
                P4EnemyUnit[] spinTargets = spin is null ? [] : enemies.Where(enemy => enemy.Life > 0 &&
                    InRange(heroPosition, enemy.Position, spin.RangeRaw)).ToArray();

                if (chosen is null)
                {
                    P6ResolvedSkill? blocked = new[] { charge, spin, cleave, blade, ashJavelin, emberNova, stormBrand, heavyResolved }
                        .Where(skill => skill is not null && distance <= (long)skill.RangeRaw * skill.RangeRaw && !CanPay(hero, skill))
                        .OrderBy(skill => skills[skill!.SkillId].Priority)
                        .FirstOrDefault();
                    if (blocked is not null && AiMatches(skills[blocked.SkillId], request, hero, target, enemies, distance))
                    {
                        string resource = blocked.LifeCost > 0 ? "life" : "mana";
                        events.Add(Event(tick, P4SpatialEventKind.SkillFailed, "hero", target.EntityId, 0,
                            heroPosition, target.Position, $"{blocked.SkillId}|{resource}"));
                    }
                }

                if (chosen == P1SkillIds.WarCry && warCry.TryActivate(hero, tick))
                {
                    ascendancyRuntime.WarCry();
                    events.Add(Event(tick, P4SpatialEventKind.WarCry, "hero", target.EntityId, 0,
                        heroPosition, target.Position, "area:6000"));
                    heroNextActionTick = tick + ActionDelay(request.Build, P1Skills.WarCry.CastTimeTicks);
                    if (ascendancy.Has(P18NodeIds.BreakerWarCryCore)) heroNextActionTick = tick;
                }
                else
                {
                    if (chosen is not null && p17Skills.TryGetValue(chosen, out P6ResolvedSkill? p17Skill) &&
                        P6CombatSkillRules.TryPay(hero, p17Skill))
                    {
                        ExecuteP17Skill(request, p17Skill, skills[chosen], target, enemies, hero, random, tick,
                            ref heroPosition, bannerMultiplier, events, ref guardUntilTick, ref guardReductionBasisPoints,
                            p17UseCounts);
                        if (p17Skill.Role == P17SkillRole.Guard && ascendancy.Has(P18NodeIds.BastionGuardCore))
                        {
                            guardReductionBasisPoints = Math.Min(8_000, guardReductionBasisPoints + 2_500);
                            hero.HealLife(Math.Max(1, hero.MaximumLife * 500 / 10_000));
                        }
                        if (p17Skill.Role == P17SkillRole.Guard && ascendancy.Has(P18NodeIds.BastionGuardSmall))
                            guardUntilTick += Math.Max(1, (guardUntilTick - tick) / 5);
                        p17ReadyTicks[chosen] = tick + Math.Max(1, p17Skill.CooldownTicks);
                        heroNextActionTick = tick + ActionDelay(request.Build, p17Skill.CastTimeTicks);
                    }
                    else if (chosen == P1SkillIds.SeismicCharge && P6CombatSkillRules.TryPay(hero, charge!))
                    {
                        P6ResolvedSkill chargeSkill = charge!;
                        P4Point beforeCharge = heroPosition;
                        heroPosition = P4Point.MoveToward(heroPosition, target.Position, Math.Max(1, chargeSkill.RangeRaw - 900));
                        ascendancyRuntime.Moved((int)Math.Sqrt(P4Point.DistanceSquared(beforeCharge, heroPosition)));
                        foreach (P4EnemyUnit enemy in enemies.Where(enemy => enemy.Life > 0 && InRange(heroPosition, enemy.Position, 1_800)))
                        {
                            int multiplier = SkillMultiplier(chargeSkill, enemy, bannerMultiplier);
                            ApplyHeroHit(request, enemy, random, tick, multiplier, P4SpatialEventKind.SeismicCharge,
                                heroPosition, events, chargeSkill.BleedChanceBasisPoints, hero, chargeSkill.LifeLeechBasisPoints);
                        }
                        events.Add(Event(tick, P4SpatialEventKind.SeismicCharge, "hero", target.EntityId, 0,
                            heroPosition, target.Position, "movement"));
                        chargeReadyTick = tick + chargeSkill.CooldownTicks;
                        heroNextActionTick = tick + ActionDelay(request.Build, chargeSkill.CastTimeTicks);
                    }
                    else if (chosen == P1SkillIds.BloodTideSpin && P6CombatSkillRules.TryPay(hero, spin!))
                    {
                        P6ResolvedSkill spinSkill = spin!;
                        foreach (P4EnemyUnit enemy in spinTargets)
                        {
                            ApplyHeroHit(request, enemy, random, tick, SkillMultiplier(spinSkill, enemy, bannerMultiplier) * 8_000 / 10_000,
                                P4SpatialEventKind.BloodTideSpin, heroPosition, events,
                                checked(3_500 + spinSkill.BleedChanceBasisPoints), hero, spinSkill.LifeLeechBasisPoints);
                        }
                        spinReadyTick = tick + spinSkill.CooldownTicks;
                        heroNextActionTick = tick + ActionDelay(request.Build, spinSkill.CastTimeTicks);
                    }
                    else if (chosen == P1SkillIds.EarthCleave && P6CombatSkillRules.TryPay(hero, cleave!))
                    {
                        P6ResolvedSkill cleaveSkill = cleave!;
                        foreach (P4EnemyUnit enemy in cleaveTargets)
                        {
                            int lifeBefore = enemy.Life;
                            ApplyHeroHit(request, enemy, random, tick, SkillMultiplier(cleaveSkill, enemy, bannerMultiplier) * 8_000 / 10_000,
                                P4SpatialEventKind.EarthCleave, heroPosition, events,
                                checked(request.Build.IncreasedBleedChanceBasisPoints / 2 + cleaveSkill.BleedChanceBasisPoints),
                                hero, cleaveSkill.LifeLeechBasisPoints);
                            if (ascendancy.Has(P18NodeIds.BreakerAftershockCore) && lifeBefore > enemy.Life)
                                aftershocks.Add(new PendingAftershock(tick + 10, enemy.EntityId, lifeBefore - enemy.Life, heroPosition));
                        }

                        cleaveReadyTick = tick + cleaveSkill.CooldownTicks;
                        heroNextActionTick = tick + ActionDelay(request.Build, cleaveSkill.CastTimeTicks);
                    }
                    else if (chosen == P1SkillIds.SpiritBlade && P6CombatSkillRules.TryPay(hero, blade!))
                    {
                        P6ResolvedSkill bladeSkill = blade!;
                        P4EnemyUnit[] projectileTargets = P231AscendancyRules.Projectile(ascendancy).CanRepeatHitSameTarget
                            ? Enumerable.Repeat(target, bladeSkill.ProjectileCount).ToArray()
                            : enemies.Where(enemy => enemy.Life > 0)
                                .OrderBy(enemy => P4Point.DistanceSquared(heroPosition, enemy.Position))
                                .Take(bladeSkill.ProjectileCount).ToArray();
                        foreach (P4EnemyUnit projectileTarget in projectileTargets)
                        {
                            int travelTicks = TravelTicks(heroPosition, projectileTarget.Position, bladeSkill.ProjectileSpeedRawPerSecond);
                            projectiles.Add(new PendingBlade(tick + travelTicks, projectileTarget.EntityId, 0,
                                SkillMultiplier(bladeSkill, projectileTarget, bannerMultiplier) * 9_000 / 10_000,
                                [projectileTarget.EntityId], heroPosition, bladeSkill.MaximumChains, bladeSkill.LifeLeechBasisPoints));
                        }
                        events.Add(Event(tick, P4SpatialEventKind.SpiritBladeLaunched, "hero", target.EntityId, 0,
                            heroPosition, target.Position, $"projectile:{bladeSkill.ProjectileCount}"));
                        bladeReadyTick = tick + bladeSkill.CooldownTicks;
                        heroNextActionTick = tick + ActionDelay(request.Build, bladeSkill.CastTimeTicks);
                    }
                    else if (chosen == P1SkillIds.AshJavelin && P6CombatSkillRules.TryPay(hero, ashJavelin!))
                    {
                        P6ResolvedSkill skill = ashJavelin!;
                        ApplyHeroHit(request, target, random, tick, SkillMultiplier(skill, target, bannerMultiplier),
                            P4SpatialEventKind.AshJavelin, heroPosition, events, skill.BleedChanceBasisPoints, hero,
                            skill.LifeLeechBasisPoints);
                        ashJavelinReadyTick = tick + skill.CooldownTicks;
                        heroNextActionTick = tick + ActionDelay(request.Build, skill.CastTimeTicks);
                    }
                    else if (chosen == P1SkillIds.EmberNova && P6CombatSkillRules.TryPay(hero, emberNova!))
                    {
                        P6ResolvedSkill skill = emberNova!;
                        foreach (P4EnemyUnit enemy in enemies.Where(enemy => enemy.Life > 0 && InRange(heroPosition, enemy.Position, skill.RangeRaw)))
                            ApplyHeroHit(request, enemy, random, tick, SkillMultiplier(skill, enemy, bannerMultiplier),
                                P4SpatialEventKind.EmberNova, heroPosition, events, 0, hero, skill.LifeLeechBasisPoints);
                        emberNovaReadyTick = tick + skill.CooldownTicks;
                        heroNextActionTick = tick + ActionDelay(request.Build, skill.CastTimeTicks);
                    }
                    else if (chosen == P1SkillIds.StormBrand && P6CombatSkillRules.TryPay(hero, stormBrand!))
                    {
                        P6ResolvedSkill skill = stormBrand!;
                        P4EnemyUnit[] marked = enemies.Where(enemy => enemy.Life > 0)
                            .OrderBy(enemy => P4Point.DistanceSquared(target.Position, enemy.Position))
                            .Take(Math.Max(2, skill.MaximumChains + 1)).ToArray();
                        foreach (P4EnemyUnit enemy in marked)
                            ApplyHeroHit(request, enemy, random, tick, SkillMultiplier(skill, enemy, bannerMultiplier) * 8_500 / 10_000,
                                P4SpatialEventKind.StormBrand, heroPosition, events, 0, hero, skill.LifeLeechBasisPoints);
                        stormBrandReadyTick = tick + skill.CooldownTicks;
                        heroNextActionTick = tick + ActionDelay(request.Build, skill.CastTimeTicks);
                    }
                    else if (chosen == P1SkillIds.HeavyStrike &&
                             SkillRules.TryPaySkillCost(hero, heavyStrike))
                    {
                        int warCryMultiplier = checked(warCry.ConsumeHeavyStrikeMultiplier(tick) * bannerMultiplier / 10_000);
                        ApplyHeroHit(request, target, random, tick, warCryMultiplier,
                            P4SpatialEventKind.HeavyStrike, heroPosition, events,
                            checked(request.Build.IncreasedBleedChanceBasisPoints + heavyStrike.BleedChanceBasisPoints), hero,
                            ascendancy.Has(P18NodeIds.BloodTideSmall) ? 100 : 0);
                        int speedBasisPoints = Math.Max(1_000, 10_000 + ascendancyRuntime.AttackSpeedBasisPoints +
                            request.Build.IncreasedActionSpeedBasisPoints +
                            virtueVice.Bonuses().IncreasedActionSpeedBasisPoints);
                        int attackInterval = checked((heavyStrike.AttackIntervalTicks * 10_000 + speedBasisPoints - 1) / speedBasisPoints);
                        heroNextActionTick = tick + attackInterval;
                    }
                    else
                    {
                        if (distance > 36_000_000)
                            ActivateUtility(utilityFlasks, P1FlaskKind.Movement, tick, heroPosition, events);
                        int flaskSpeed = utilityFlasks.GetValueOrDefault(P1FlaskKind.Movement)?.Active == true ? 13_000 : 10_000;
                        int speed = Math.Max(1, checked((int)((long)HeroEntityRawSpeed * request.Build.MovementSpeedBasisPoints / 10_000 * flaskSpeed / 10_000 / 20)));
                        P4Point next = P4Point.MoveToward(heroPosition, target.Position, speed);
                        if (next != heroPosition)
                        {
                            ascendancyRuntime.Moved((int)Math.Sqrt(P4Point.DistanceSquared(heroPosition, next)));
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

            if (RechargeFlasksForKills(enemies, flask, utilityFlasks, tick, heroPosition, events, ascendancyRuntime, hero))
                chargeReadyTick = 0;
            if (tick < rootedUntilTick) heroPosition = beforeMovement;
            ResolveEnemies(request, enemies, hero, heroPosition, random, tick, events, utilityFlasks,
                guardUntilTick, guardReductionBasisPoints, shieldCounter, shieldCounterConfiguration,
                ref shieldCounterReadyTick, ascendancyRuntime, hazards, ref rootedUntilTick);
            foreach (EnemyHazard hazard in hazards.Where(h => h.Expires > tick && tick >= h.Start && (tick - h.Start) % 10 == 0))
            {
                int damage = InRange(heroPosition, hazard.Position, hazard.Radius) ? hazard.Damage : 0;
                if (damage > 0)
                {
                    int barrier = request.Build.Sheet.SpiritBarrier().Value;
                    int reduction = P30CombatRules.SpiritBarrierReduction(barrier, checked(damage * 2));
                    damage = Math.Max(1, checked(damage * (10_000 - reduction) / 10_000));
                }
                if (damage > 0) hero.ApplyDamage(damage, tick);
                events.Add(Event(tick, P4SpatialEventKind.EnemyAttack, hazard.Source, "hero", damage,
                    hazard.Position, hazard.Position, $"持续危险地面|radius:{hazard.Radius}|until:{hazard.Expires * TickMilliseconds}"));
            }
            hazards.RemoveAll(h => h.Expires <= tick);
            int totalEnemyLife = enemies.Sum(enemy => Math.Max(0, enemy.Life));
            if (hero.Life < minimumHeroLife || totalEnemyLife < minimumEnemyLife)
            {
                minimumHeroLife = Math.Min(minimumHeroLife, hero.Life);
                minimumEnemyLife = Math.Min(minimumEnemyLife, totalEnemyLife);
                lastProgressTick = tick;
            }

            if (request.MaximumTicks == 0 && hero.IsAlive && totalEnemyLife > 0 &&
                tick - lastProgressTick >= StalemateProgressWindowTicks)
            {
                projectedOutcome = P1BattleOutcome.Draw;
                break;
            }

            if (request.MaximumTicks > 0 || (tick & 3) == 0)
                CaptureFrame(frames, tick * TickMilliseconds, request.NodeIndex, heroPosition, hero, heroTargetId, enemies,
                    request.Build.PartySize, request.Build.FrontlineCount);
        }

        bool victory = enemies.All(enemy => enemy.Life <= 0);
        P1BattleOutcome outcome = projectedOutcome ?? (victory
            ? P1BattleOutcome.HeroVictory
            : hero.IsAlive ? P1BattleOutcome.Timeout : P1BattleOutcome.EnemyVictory);
        events.Add(Event(tick, victory ? P4SpatialEventKind.NodeCleared : P4SpatialEventKind.HeroDefeated,
            victory ? "hero" : "enemies", string.Empty, 0, heroPosition, heroPosition, outcome.ToString()));
        CaptureFrame(frames, tick * TickMilliseconds, request.NodeIndex, heroPosition, hero, heroTargetId, enemies,
            request.Build.PartySize, request.Build.FrontlineCount);
        string hash = Hash(seed, outcome, tick, hero, enemies, events);
        return new P4NodeCombatResult(outcome, tick, hero.Life, hero.Mana, hero.Shield, frames, events, hash);
    }

    private static List<P4EnemyUnit> CreateEnemies(P4NodeCombatRequest request, Pcg32 random)
    {
        var result = new List<P4EnemyUnit>(request.EnemyCount);
        IReadOnlyList<EnemyProfile> pool = request.EnemyPool ?? P1Enemies.ForEncounter(request.AreaLevel, request.EncounterFamily);
        // P27 intentionally permits extreme packs: one repeated monster, or an entire pack
        // drawn from a single combat role. It does not synthesize a front/back/support template.
        IReadOnlyList<EnemyProfile> packPool = request.EnemyPool ?? P27MonsterCatalog.SelectPackPool(pool, random);
        int magicCount = request.AreaLevel >= 8 && request.EnemyCount >= 6 ? Math.Clamp(request.EnemyCount / 4, 2, 6) : 0;
        for (int index = 0; index < request.EnemyCount; index++)
        {
            bool boss = request.HasBoss && index < request.BossCount;
            EnemyRarity rarity = boss
                ? EnemyRarity.Boss
                : (request.HasElite && index == request.BossCount || index >= request.BossCount && index < request.BossCount + request.AdditionalRareEnemies)
                    ? EnemyRarity.Rare
                    : index >= (request.HasElite ? 1 : 0) && index < magicCount + (request.HasElite ? 1 : 0)
                        ? EnemyRarity.Magic
                        : EnemyRarity.Normal;
            bool elite = rarity is EnemyRarity.Magic or EnemyRarity.Rare;
            EnemyProfile profile = boss
                ? string.IsNullOrEmpty(request.BossStableId) || request.BossStableId == P1Enemies.AbyssWarden.StableId ? P1Enemies.AbyssWarden :
                    P1Enemies.NormalEnemies.FirstOrDefault(e => e.StableId == request.BossStableId) ?? P14Bosses.CombatProfile(request.BossStableId)
                : packPool[(int)(random.NextUInt() % (uint)packPool.Count)];
            if (!boss && request.GardenTags is { Count: > 0 })
            {
                EnemySkillKind kind = request.GardenTags[index % request.GardenTags.Count] switch
                {
                    "life" => EnemySkillKind.RootSnare,
                    "defense" => EnemySkillKind.ShieldLink,
                    "attack" => EnemySkillKind.Burrow,
                    _ => EnemySkillKind.GroundHazard,
                };
                profile = profile with
                {
                    Skills = profile.EffectiveSkills.Append(new EnemySkillProfile(kind,
                    "苗圃特性", kind == EnemySkillKind.GroundHazard ? EnemyDamageType.Fire : EnemyDamageType.Physical,
                    10_000, RangeRaw: 5_000, Area: kind != EnemySkillKind.ShieldLink,
                    IsSpell: kind is EnemySkillKind.RootSnare or EnemySkillKind.GroundHazard)).ToArray()
                };
            }
            IReadOnlyList<EliteAffix> affixes = EnemyRules.RollAffixes(random, rarity);
            ScaledEnemy scaled = EnemyRules.Scale(profile, request.AreaLevel, affixes, request.AbyssRoute, rarity);
            int lifeScale = boss ? 10_000 : rarity == EnemyRarity.Rare ? 7_000 : rarity == EnemyRarity.Magic ? 5_000 : 4_500;
            int encounterLife = boss
                ? checked((int)((long)request.EnemyLifeBasisPoints * request.BossLifeBasisPoints / 10_000))
                : request.EnemyLifeBasisPoints;
            int life = Math.Max(2, checked((int)((long)scaled.Life * lifeScale / 10_000 * encounterLife / 10_000)));
            P4UnitRole role = boss ? P4UnitRole.Boss : profile.Role switch
            {
                EnemyRole.Ranged => P4UnitRole.Ranged,
                EnemyRole.Caster => P4UnitRole.Caster,
                EnemyRole.Charger => P4UnitRole.Charger,
                EnemyRole.Summoner => P4UnitRole.Summoner,
                EnemyRole.Support => P4UnitRole.Summoner,
                _ => P4UnitRole.Melee,
            };
            P4Point position = SpawnPosition(request.Formation, index, request.EnemyCount, random);
            result.Add(new P4EnemyUnit(
                $"enemy-{request.NodeIndex}-{index}", profile, scaled, role, rarity, elite, boss, life, position, index * 3));
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

    private static P4EnemyUnit? SelectTarget(
        IEnumerable<P4EnemyUnit> enemies,
        P4Point heroPosition,
        SkillTargetPolicy policy) => enemies
        .Where(enemy => enemy.Life > 0 && policy switch
        {
            SkillTargetPolicy.BossOnly => enemy.Boss,
            SkillTargetPolicy.EliteAndBoss => enemy.Elite || enemy.Boss,
            _ => true,
        })
        .OrderBy(enemy => P4Point.DistanceSquared(heroPosition, enemy.Position))
        .ThenByDescending(enemy => enemy.Boss)
        .ThenByDescending(enemy => enemy.Elite)
        .ThenBy(enemy => enemy.EntityId, StringComparer.Ordinal)
        .FirstOrDefault();

    private static void ExecuteP17Skill(
        P4NodeCombatRequest request,
        P6ResolvedSkill skill,
        SkillConfiguration configuration,
        P4EnemyUnit target,
        IReadOnlyCollection<P4EnemyUnit> enemies,
        ResourceState hero,
        Pcg32 random,
        int tick,
        ref P4Point heroPosition,
        int bannerMultiplier,
        ICollection<P4SpatialEvent> events,
        ref int guardUntilTick,
        ref int guardReductionBasisPoints,
        IDictionary<string, int> useCounts)
    {
        if (skill.Role == P17SkillRole.Movement)
        {
            P4Point beforeMove = heroPosition;
            heroPosition = P4Point.MoveToward(heroPosition, target.Position, Math.Max(1, skill.RangeRaw - 900));
            request.AscendancyRuntime?.Moved((int)Math.Sqrt(P4Point.DistanceSquared(beforeMove, heroPosition)));
            events.Add(Event(tick, P4SpatialEventKind.HeroMoved, "hero", target.EntityId, 0,
                heroPosition, target.Position, $"skill:{skill.SkillId}"));
        }

        if (skill.Role == P17SkillRole.Guard || skill.SkillId == P1SkillIds.DefiantCry)
        {
            guardUntilTick = tick + (skill.SkillId == P1SkillIds.PrismaticGuard ? 80 : 60);
            guardReductionBasisPoints = skill.SkillId == P1SkillIds.IronGuard ? 4_000 :
                skill.SkillId == P1SkillIds.PrismaticGuard ? 2_500 : 2_000;
            if (skill.SkillId == P1SkillIds.DefiantCry)
                hero.HealLife(Math.Max(1, (hero.MaximumLife - hero.Life) / 10));
            events.Add(Event(tick, P4SpatialEventKind.Guard, "hero", "hero", guardReductionBasisPoints,
                heroPosition, heroPosition, $"skill:{skill.SkillId}|until:{guardUntilTick}"));
        }

        if (skill.SkillId == P1SkillIds.BreakerCry)
        {
            P4Point cryOrigin = heroPosition;
            foreach (P4EnemyUnit enemy in enemies.Where(item => item.Life > 0 && InRange(cryOrigin, item.Position, skill.RangeRaw)))
                enemy.ArmorBreakStacks = Math.Min(request.AscendancyRuntime?.ArmorBreakMaximum ?? 5, enemy.ArmorBreakStacks + 2);
            events.Add(Event(tick, P4SpatialEventKind.SkillEffect, "hero", target.EntityId, 0,
                heroPosition, target.Position, $"skill:{skill.SkillId}|armor-break:2"));
            return;
        }
        if (skill.DamageType == P17DamageType.None) return;

        P4Point origin = heroPosition;
        P4EnemyUnit[] affected = skill.Shape switch
        {
            P17SkillShape.Circle or P17SkillShape.MovementCircle or P17SkillShape.GroundArea => enemies
                .Where(enemy => enemy.Life > 0 && InRange(origin, enemy.Position, skill.RangeRaw)).ToArray(),
            P17SkillShape.Cone => enemies.Where(enemy => enemy.Life > 0 &&
                InCleaveCone(origin, target.Position, enemy.Position, skill.RangeRaw)).ToArray(),
            P17SkillShape.Chain => enemies.Where(enemy => enemy.Life > 0)
                .OrderBy(enemy => P4Point.DistanceSquared(target.Position, enemy.Position))
                .Take(Math.Max(1, skill.MaximumChains + 1)).ToArray(),
            P17SkillShape.Projectile => enemies.Where(enemy => enemy.Life > 0)
                .OrderBy(enemy => P4Point.DistanceSquared(origin, enemy.Position))
                .Take(Math.Max(1, skill.ProjectileCount + skill.PierceCount + skill.ForkCount)).ToArray(),
            _ => [target],
        };
        if (affected.Length == 0) affected = [target];

        int useCount = useCounts[skill.SkillId] = useCounts[skill.SkillId] + 1;
        if (configuration.Supports.HasFlag(SkillSupport.Trauma))
        {
            int traumaStacks = Math.Min(10, useCount);
            hero.ApplyDamage(Math.Max(1, hero.MaximumLife * traumaStacks / 1_000), tick);
            bannerMultiplier = checked(bannerMultiplier * (10_000 + traumaStacks * 500) / 10_000);
        }
        if (configuration.Supports.HasFlag(SkillSupport.TripleImpact) && useCount % 3 == 0)
            bannerMultiplier = checked(bannerMultiplier * 18_000 / 10_000);

        foreach (P4EnemyUnit enemy in affected)
        {
            int burstDamage = 0;
            if (skill.SkillId == P1SkillIds.BloodBurst && enemy.BleedRemaining > 0)
            {
                burstDamage = enemy.BleedRemaining * 6_500 / 10_000;
                enemy.BleedRemaining = 0;
                enemy.BleedPulses = 0;
            }
            ApplyP17HeroHit(request, skill, configuration, enemy, hero, random, tick, heroPosition,
                bannerMultiplier, events);
            if (burstDamage > 0 && enemy.Life > 0)
            {
                int armor = enemy.Scaled.Armor * Math.Max(0, 10_000 - enemy.ArmorBreakStacks * 800) / 10_000;
                P17DamageBreakdown burst = P17DamageRules.Resolve(burstDamage, P17DamageType.Physical,
                    SkillSupport.None, armor, 0, 0, 0, 0);
                enemy.Life = Math.Max(0, enemy.Life - burst.Total);
                events.Add(Event(tick, P4SpatialEventKind.SkillEffect, "hero", enemy.EntityId, burst.Total,
                    heroPosition, enemy.Position, $"skill:{skill.SkillId}|blood-burst|damage:{burst.Compact}|supports:{(ulong)configuration.Supports}"));
                if (enemy.Life == 0)
                    events.Add(Event(tick, P4SpatialEventKind.EnemyDefeated, "hero", enemy.EntityId, 0,
                        heroPosition, enemy.Position, enemy.Profile.StableId));
            }
            if (skill.Returns && enemy.Life > 0)
                ApplyP17HeroHit(request, skill, configuration, enemy, hero, random, tick, heroPosition,
                    bannerMultiplier * 8_000 / 10_000, events);
        }
    }

    private static void ApplyP17HeroHit(
        P4NodeCombatRequest request,
        P6ResolvedSkill skill,
        SkillConfiguration configuration,
        P4EnemyUnit enemy,
        ResourceState hero,
        Pcg32 random,
        int tick,
        P4Point source,
        int multiplier,
        ICollection<P4SpatialEvent> events)
    {
        P18CombatRuntime runtime = request.AscendancyRuntime ?? new P18CombatRuntime(P18CombatProfile.Empty);
        SkillTag tags = P1Skills.Get(skill.SkillId).Tags;
        if (runtime.Has(P18NodeIds.BloodLifeCore) && tags.HasFlag(SkillTag.Attack)) runtime.PaidLife(tick);
        int ascendancyMultiplier = runtime.ConsumeAttackMultiplier(tags,
            hero.Life * 2L <= hero.MaximumLife, !request.Build.HasShield,
            new P18EnemyState(enemy.ArmorBreakStacks, tick < enemy.StunnedUntilTick));
        int weaponSpan = request.Build.Weapon.MaximumPhysicalDamage - request.Build.Weapon.MinimumPhysicalDamage + 1;
        int weaponRoll = request.Build.Weapon.MinimumPhysicalDamage + (int)(random.NextUInt() % (uint)Math.Max(1, weaponSpan));
        int raw = skill.Role == P17SkillRole.DamageOverTime && skill.Shape == P17SkillShape.GroundArea
            ? Math.Max(1, request.AreaLevel * 5 + weaponRoll / 2)
            : Math.Max(1, weaponRoll + request.Build.AddedPhysicalDamage);
        P205PassiveModifiers profile = request.Build.PassiveProfile ?? P205PassiveModifiers.Empty;
        P17AddedWeaponDamage addedWeapon = tags.HasFlag(SkillTag.Attack) && skill.Role != P17SkillRole.DamageOverTime &&
                                           request.Build.LocalWeaponStats is { } localWeapon
            ? new(Roll(localWeapon.Fire), Roll(localWeapon.Cold), Roll(localWeapon.Lightning), Roll(localWeapon.Void))
            : default;
        raw = ScaleOffensive(raw);
        addedWeapon = new(
            ScaleOffensive(addedWeapon.Fire), ScaleOffensive(addedWeapon.Cold),
            ScaleOffensive(addedWeapon.Lightning), ScaleOffensive(addedWeapon.Void));
        int criticalChance = request.Build.CannotCrit ? 0 : P30CombatRules.CriticalChance(
            request.Build.Weapon.CriticalChanceBasisPoints, request.Build.IncreasedCriticalChanceBasisPoints);
        if (skill.Role != P17SkillRole.DamageOverTime && random.NextUInt() % 10_000 < Math.Clamp(criticalChance, 0, 10_000))
        {
            raw = checked(raw * request.Build.CriticalMultiplierBasisPoints / 10_000);
            addedWeapon = new(
                checked(addedWeapon.Fire * request.Build.CriticalMultiplierBasisPoints / 10_000),
                checked(addedWeapon.Cold * request.Build.CriticalMultiplierBasisPoints / 10_000),
                checked(addedWeapon.Lightning * request.Build.CriticalMultiplierBasisPoints / 10_000),
                checked(addedWeapon.Void * request.Build.CriticalMultiplierBasisPoints / 10_000));
        }
        int armor = enemy.Scaled.Armor * Math.Max(0, 10_000 - enemy.ArmorBreakStacks * runtime.ArmorBreakPerStackBasisPoints) / 10_000;
        if (configuration.Supports.HasFlag(SkillSupport.ArmorPierce)) armor = armor * 7_000 / 10_000;
        P17DamageBreakdown damage = P17DamageRules.ResolveMixed(raw, skill.DamageType, addedWeapon, configuration.Supports,
            armor, enemy.Scaled.FireResistanceBasisPoints + request.EnemyElementalResistanceBasisPoints,
            enemy.Scaled.ColdResistanceBasisPoints + request.EnemyElementalResistanceBasisPoints,
            enemy.Scaled.LightningResistanceBasisPoints + request.EnemyElementalResistanceBasisPoints,
            enemy.Scaled.VoidResistanceBasisPoints + request.EnemyVoidResistanceBasisPoints,
            enemy.Scaled.PhysicalResistanceBasisPoints + request.EnemyPhysicalReductionBasisPoints);
        int value = damage.Total;
        int beforeShieldLink = enemy.Life;
        enemy.Life = Math.Max(0, enemy.Life - value);
        value = beforeShieldLink - enemy.Life;
        int leech = skill.LifeLeechBasisPoints +
            (runtime.Has(P18NodeIds.BloodTideSmall) && tags.HasFlag(SkillTag.Attack) &&
             skill.DamageType == P17DamageType.Physical ? 100 : 0);
        if (value > 0 && leech > 0)
            ApplyLifeLeech(hero, Math.Max(1, value * leech / 10_000), request.Build.InstantLifeLeechBasisPoints);

        int ScaleOffensive(int value)
        {
            if (value <= 0) return 0;
            value = checked(value * (10_000 + request.Build.IncreasedDamageBasisPoints + profile.DamageFor(tags) +
                                     configuration.Quality * 100) / 10_000);
            value = checked(value * (10_000 + profile.MoreDamageBasisPoints) / 10_000);
            value = checked(value * (10_000 + JewelMoreDamage(request.Build, tags,
                skill.Role == P17SkillRole.DamageOverTime)) / 10_000);
            value = checked(value * skill.BaseDamageBasisPoints / 10_000);
            value = checked(value * P6CombatSkillRules.DamageMultiplier(skill, enemy.Life, enemy.MaximumLife) / 10_000);
            value = checked(value * multiplier / 10_000);
            value = checked(value * ascendancyMultiplier / 10_000);
            return checked(value * (10_000 + enemy.ShockStacks * 500) / 10_000);
        }

        int Roll(LocalDamageRange range)
        {
            if (!range.HasDamage) return 0;
            int span = range.Maximum - range.Minimum + 1;
            return range.Minimum + (int)(random.NextUInt() % (uint)Math.Max(1, span));
        }

        bool ailmentAllowed = !configuration.Supports.HasFlag(SkillSupport.ElementalFocus) &&
                              random.NextUInt() % 10_000 < Math.Clamp(skill.AilmentChanceBasisPoints, 0, 10_000);
        if (ailmentAllowed && enemy.Life > 0)
        {
            switch (skill.Ailment)
            {
                case P17Ailment.Bleed when !configuration.Supports.HasFlag(SkillSupport.Bloodlust):
                    ApplyBleed(enemy, Math.Max(1, value * 7 / 10), runtime);
                    runtime.AppliedBleed();
                    break;
                case P17Ailment.Ignite or P17Ailment.Erosion or P17Ailment.Wither:
                    enemy.P17DotRemaining += Math.Max(1, value / 2);
                    enemy.P17DotPulses = Math.Max(enemy.P17DotPulses, 4);
                    enemy.P17DotAilment = skill.Ailment;
                    break;
                case P17Ailment.ArmorBreak:
                    int extra = runtime.Has(P18NodeIds.BreakerArmorBreakSmall) && random.NextUInt() % 4 == 0 ? 1 : 0;
                    enemy.ArmorBreakStacks = Math.Min(runtime.ArmorBreakMaximum, enemy.ArmorBreakStacks + 1 + extra);
                    break;
                case P17Ailment.Chill or P17Ailment.Freeze:
                    enemy.ImpairedUntilTick = Math.Max(enemy.ImpairedUntilTick, tick + 30);
                    break;
                case P17Ailment.Stun or P17Ailment.Paralysis:
                    if (enemy.Boss && tick < enemy.StunImmuneUntilTick) break;
                    enemy.NextActionTick = Math.Max(enemy.NextActionTick, tick + 20);
                    enemy.StunnedUntilTick = Math.Max(enemy.StunnedUntilTick, tick +
                        (runtime.Has(P18NodeIds.BreakerStunSmall) ? 25 : 20));
                    if (runtime.Has(P18NodeIds.BreakerStunCore))
                    {
                        int shockwave = Math.Max(1, value * 15_000 / 10_000);
                        enemy.Life = Math.Max(0, enemy.Life - shockwave);
                        events.Add(Event(tick, P4SpatialEventKind.Ascendancy, "hero", enemy.EntityId, shockwave,
                            source, enemy.Position, "linebreaker:stun-shockwave"));
                        if (enemy.Boss) enemy.StunImmuneUntilTick = enemy.StunnedUntilTick + 100;
                    }
                    break;
                case P17Ailment.Shock:
                    enemy.ShockStacks = Math.Min(3, enemy.ShockStacks + 1);
                    break;
            }
            events.Add(Event(tick, P4SpatialEventKind.Ailment, "hero", enemy.EntityId, 0,
                source, enemy.Position, $"skill:{skill.SkillId}|ailment:{skill.Ailment.ToString().ToLowerInvariant()}"));
        }
        events.Add(Event(tick, P4SpatialEventKind.SkillEffect, "hero", enemy.EntityId, value,
            source, enemy.Position, $"skill:{skill.SkillId}|damage:{damage.Compact}|supports:{(ulong)configuration.Supports}"));
        if (enemy.Life == 0)
            events.Add(Event(tick, P4SpatialEventKind.EnemyDefeated, "hero", enemy.EntityId, 0,
                source, enemy.Position, enemy.Profile.StableId));
    }

    private static void ResolveEnemies(
        P4NodeCombatRequest request,
        List<P4EnemyUnit> enemies,
        ResourceState hero,
        P4Point heroPosition,
        Pcg32 random,
        int tick,
        ICollection<P4SpatialEvent> events,
        IReadOnlyDictionary<P1FlaskKind, P1UtilityFlaskState> utilityFlasks,
        int guardUntilTick,
        int guardReductionBasisPoints,
        P6ResolvedSkill? shieldCounter,
        SkillConfiguration? shieldCounterConfiguration,
        ref int shieldCounterReadyTick,
        P18CombatRuntime ascendancy, List<EnemyHazard> hazards, ref int rootedUntilTick)
    {
        foreach (P4EnemyUnit enemy in enemies.Where(enemy => enemy.Life > 0).ToArray())
        {
            if (enemy.Life <= 0) continue; // An earlier counterattack may have killed this snapshot member.
            EnemySkillProfile activeSkill = enemy.Profile.EffectiveSkills[enemy.ActionSequence % enemy.Profile.EffectiveSkills.Count];
            P14BossDefinition? bossDefinition = enemy.Boss ? P14Bosses.TryGet(enemy.Profile.StableId) : null;
            if (bossDefinition is not null)
            {
                int phase = tick >= bossDefinition.EnrageSeconds * 20 ? 2 :
                    enemy.Life * 10_000L <= enemy.MaximumLife * bossDefinition.PhaseThresholdBasisPoints ? 1 : 0;
                if (phase != enemy.BossPhase)
                {
                    enemy.BossPhase = phase;
                    events.Add(Event(tick, P4SpatialEventKind.BossPhaseChanged, enemy.EntityId, "hero", phase,
                        enemy.Position, heroPosition, phase == 2 ? "enraged" : "phase_two"));
                }
            }
            int range = Math.Max(enemy.Profile.AttackRangeRaw, activeSkill.RangeRaw);
            if (activeSkill.Area)
                range = checked((int)((long)range * request.EnemyAreaBasisPoints / 10_000));
            long distance = P4Point.DistanceSquared(enemy.Position, heroPosition);
            if (distance > (long)range * range ||
                enemy.Role is P4UnitRole.Ranged or P4UnitRole.Caster or P4UnitRole.Summoner && distance < 9_000_000)
            {
                P4Point destination = distance > (long)range * range
                    ? heroPosition with { XRaw = Math.Clamp(heroPosition.XRaw + LaneOffset(enemy.Ordinal), 350, 11_650) }
                    : new P4Point(
                        Math.Clamp(enemy.Position.XRaw + Math.Sign(enemy.Position.XRaw - heroPosition.XRaw) * 700, 350, 11_650),
                        Math.Clamp(enemy.Position.YRaw + Math.Sign(enemy.Position.YRaw - heroPosition.YRaw) * 700, 350, 23_650));
                int move = Math.Max(1, checked((int)((long)enemy.Profile.MovementSpeedRawPerSecond * request.EnemySpeedBasisPoints / 10_000 / 20)));
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

            if (tick < enemy.NextActionTick || enemy.TelegraphTarget is null && distance > (long)range * range || !hero.IsAlive)
            {
                continue;
            }

            int attacksPerSecond = checked((int)((long)enemy.Scaled.AttacksPerSecondMilli * request.EnemySpeedBasisPoints / 10_000));
            int normalInterval = Math.Max(8, checked((20_000 + attacksPerSecond - 1) / attacksPerSecond));
            bool areaAttack = activeSkill.Area || activeSkill.Kind is EnemySkillKind.Burrow or EnemySkillKind.Artillery;
            if (activeSkill.Avoidable && areaAttack && enemy.TelegraphTarget is null &&
                activeSkill.Kind is not (EnemySkillKind.HealingBloom or EnemySkillKind.RepairPulse or EnemySkillKind.ShieldLink))
            {
                enemy.TelegraphTarget = heroPosition;
                enemy.NextActionTick = tick + 12;
                events.Add(Event(tick, P4SpatialEventKind.BossTelegraph, enemy.EntityId, "hero", 2_000,
                    enemy.Position, heroPosition, $"{activeSkill.DisplayName}|{activeSkill.Telegraph}|{activeSkill.DamageType}|True|until:{(tick + 12) * TickMilliseconds}"));
                continue;
            }
            P4Point impactPoint = enemy.TelegraphTarget ?? heroPosition;
            enemy.TelegraphTarget = null;
            if (activeSkill.Kind == EnemySkillKind.Burrow)
            {
                enemy.Position = impactPoint;
                events.Add(Event(tick, P4SpatialEventKind.EnemyMoved, enemy.EntityId, "hero", 0,
                    enemy.Position, impactPoint, "钻地包抄"));
            }
            if (activeSkill.Kind is EnemySkillKind.ShieldLink or EnemySkillKind.SummonSwarm ||
                enemy.Boss && enemy.BossPhase > 0 && enemy.ActionSequence % 4 == 0 &&
                enemy.Profile.StableId.Contains("warfront", StringComparison.Ordinal))
            {
                if (activeSkill.Kind == EnemySkillKind.ShieldLink)
                {
                    enemy.ShieldUntilTick = tick + 80;
                    events.Add(Event(tick, P4SpatialEventKind.Guard, enemy.EntityId, "allies", 3_000,
                        enemy.Position, enemy.Position, "护盾链接：4米内其他友军30%减伤，不叠加，来源死亡或离开即断开"));
                }
                else if (enemies.Count(e => e.Summoned && e.Life > 0) < 8 && enemies.Count(e => e.Summoned) < 24)
                {
                    EnemyProfile child = P1Enemies.CorruptedWorker;
                    ScaledEnemy scaled = EnemyRules.Scale(child, request.AreaLevel, [], request.AbyssRoute, EnemyRarity.Normal);
                    int ordinal = enemies.Count;
                    enemies.Add(new P4EnemyUnit($"enemy-{request.NodeIndex}-{ordinal}", child, scaled, P4UnitRole.Melee,
                        EnemyRarity.Normal, false, false, Math.Max(2, scaled.Life / 2), enemy.Position, tick + 10)
                    { Summoned = true });
                    events.Add(Event(tick, P4SpatialEventKind.SkillEffect, enemy.EntityId, $"enemy-{request.NodeIndex}-{ordinal}", 1,
                        enemy.Position, enemy.Position, "召唤增援；无经验、物品和药剂充能"));
                }
                enemy.NextActionTick = tick + normalInterval * 2; enemy.ActionSequence++; continue;
            }
            if (activeSkill.Kind is EnemySkillKind.HealingBloom or EnemySkillKind.RepairPulse)
            {
                int restored = 0;
                foreach (P4EnemyUnit ally in enemies.Where(unit => unit.Life > 0 &&
                             InRange(enemy.Position, unit.Position, Math.Max(3_000, activeSkill.RangeRaw))))
                {
                    int before = ally.Life;
                    ally.Life = Math.Min(ally.MaximumLife, ally.Life + Math.Max(1, ally.MaximumLife * 4 / 100));
                    restored += ally.Life - before;
                }
                events.Add(Event(tick, P4SpatialEventKind.EnemyAttack, enemy.EntityId, "allies", restored,
                    enemy.Position, enemy.Position, $"{activeSkill.DisplayName}|support-heal"));
                int supportInterval = Math.Max(8, checked((20_000 + attacksPerSecond - 1) / attacksPerSecond));
                enemy.NextActionTick = tick + Math.Max(8,
                    checked(supportInterval * activeSkill.CooldownMultiplierBasisPoints / 10_000));
                enemy.ActionSequence++;
                continue;
            }
            int encounterDamage = enemy.Boss
                ? checked((int)((long)request.EnemyDamageBasisPoints * request.BossDamageBasisPoints / 10_000))
                : request.EnemyDamageBasisPoints;
            var weapon = new WeaponProfile(
                enemy.Profile.StableId + ".spatial",
                checked((int)((long)enemy.Scaled.MinimumPhysicalDamage * encounterDamage / 10_000)),
                checked((int)((long)enemy.Scaled.MaximumPhysicalDamage * encounterDamage / 10_000)),
                attacksPerSecond,
                500);
            DamageResult hit = DamageRules.Resolve(new DamageRequest(
                weapon,
                TargetArmor: activeSkill.DamageType == EnemyDamageType.Physical ? request.Build.Sheet.Armor().Value : 0,
                TargetEvasion: activeSkill.IsSpell ? 0 : request.Build.Sheet.Evasion().Value,
                Accuracy: enemy.Profile.Accuracy,
                IsSpell: activeSkill.IsSpell), random);
            int divisor = Math.Max(6, 8 + request.EnemyCount / 3);
            int damage = hit.Hit ? hit.FinalPhysicalDamage / divisor : 0;
            damage = checked((int)((long)damage * request.IncomingHitBasisPoints / 10_000));
            int skillMultiplier = activeSkill.DamageMultiplierBasisPoints;
            if (enemies.Any(unit => unit.Life > 0 && unit.Profile.EffectiveSkills.Any(skill => skill.Kind == EnemySkillKind.WarAura)))
                skillMultiplier = checked(skillMultiplier * 11_000 / 10_000);
            damage = checked(damage * skillMultiplier / 10_000);
            if (activeSkill.Area)
                damage = checked(damage * request.EnemyAreaDamageBasisPoints / 10_000);
            if (request.ExtraEnemyProjectiles > 0 && enemy.Role is P4UnitRole.Ranged or P4UnitRole.Caster)
                damage = checked(damage * (1 + request.ExtraEnemyProjectiles) * request.EnemyProjectileDamageBasisPoints / 10_000);
            if (enemy.BossPhase == 1) damage = checked(damage * 11_500 / 10_000);
            if (enemy.BossPhase == 2) damage = checked(damage * 17_500 / 10_000);
            if (damage > 0 && activeSkill.DamageType == EnemyDamageType.Physical && utilityFlasks.GetValueOrDefault(P1FlaskKind.Armor)?.Active == true)
                damage = Math.Max(1, damage * 7_000 / 10_000);
            if (damage > 0 && activeSkill.IsSpell &&
                utilityFlasks.GetValueOrDefault(P1FlaskKind.Resistance)?.Active == true)
                damage = Math.Max(1, damage * 7_500 / 10_000);
            if (damage > 0 && tick < guardUntilTick)
                damage = Math.Max(1, damage * (10_000 - guardReductionBasisPoints) / 10_000);
            if (damage > 0 && hero.Life * 2L <= hero.MaximumLife && ascendancy.Has(P18NodeIds.BloodLowLifeCore))
                damage = Math.Max(1, damage * 7_500 / 10_000);
            bool spell = activeSkill.IsSpell;
            if (damage > 0)
            {
                int resistance = activeSkill.DamageType switch
                {
                    EnemyDamageType.Cold => request.Build.Sheet.ColdResistanceBasisPoints,
                    EnemyDamageType.Fire => request.Build.Sheet.FireResistanceBasisPoints,
                    EnemyDamageType.Void => request.Build.Sheet.VoidResistanceBasisPoints,
                    EnemyDamageType.Lightning => request.Build.Sheet.LightningResistanceBasisPoints,
                    _ => 0,
                };
                int effectiveResistance = activeSkill.DamageType == EnemyDamageType.Physical ? 0 :
                    P30CombatRules.EffectiveResistance(resistance,
                        activeSkill.DamageType == EnemyDamageType.Void
                            ? request.Build.Sheet.MaximumVoidResistanceBasisPoints
                            : request.Build.Sheet.MaximumElementalResistanceBasisPoints,
                        request.EnemyPenetrationBasisPoints);
                damage = Math.Max(1, P30CombatRules.MitigateByResistance(damage, effectiveResistance));
                int suppression = request.Build.Sheet.EffectiveSpellSuppressionBasisPoints;
                if (spell && suppression > 0 && random.NextUInt() % 10_000 < suppression)
                {
                    damage = Math.Max(1, P30CombatRules.SuppressedDamage(damage,
                        request.Build.Sheet.SpellSuppressionEffectBasisPoints));
                    events.Add(Event(tick, P4SpatialEventKind.Guard, "hero", enemy.EntityId, 0,
                        heroPosition, enemy.Position, "spell_suppression"));
                }
            }
            int blockChance = request.Build.BlockChanceBasisPoints;
            if (request.Build.HasShield && ascendancy.Has(P18NodeIds.BastionAttackBlockSmall)) blockChance += 800;
            if (request.Build.HasShield && ascendancy.Has(P18NodeIds.BastionAttackBlockCore)) blockChance += 1_200;
            if (spell && ascendancy.Has(P18NodeIds.BastionSpellBlockSmall)) blockChance += 800;
            if (spell && ascendancy.Has(P18NodeIds.BastionSpellBlockCore)) blockChance += request.Build.BlockChanceBasisPoints * 6 / 10;
            int blockCap = ascendancy.Has(P18NodeIds.BastionAttackBlockCore) ? 8_000 :
                request.Build.Sheet.MaximumBlockChanceBasisPoints;
            bool blocked = damage > 0 && request.Build.HasShield && blockChance > 0 &&
                random.NextUInt() % 10_000 < Math.Min(blockCap, blockChance);
            if (blocked)
            {
                damage = spell && ascendancy.Has(P18NodeIds.BastionSpellBlockCore)
                    ? Math.Max(0, damage * ascendancy.IncomingHitMultiplier(true, true, tick) / 10_000) : 0;
                events.Add(Event(tick, P4SpatialEventKind.Block, "hero", enemy.EntityId, 0,
                    heroPosition, enemy.Position, spell ? "spell" : "attack"));
                int counterMultiplier = spell ? 10_000 : ascendancy.OnAttackBlock();
                if (shieldCounter is not null && shieldCounterConfiguration is not null &&
                    tick >= shieldCounterReadyTick)
                {
                    foreach (P4EnemyUnit counterTarget in enemies.Where(item => item.Life > 0 &&
                                 InRange(heroPosition, item.Position, ascendancy.Has(P18NodeIds.BastionCounterSmall)
                                     ? shieldCounter.RangeRaw * 11_500 / 10_000 : shieldCounter.RangeRaw)).ToArray())
                        ApplyP17HeroHit(request, shieldCounter, shieldCounterConfiguration, counterTarget, hero,
                            random, tick, heroPosition, counterMultiplier, events);
                    if (counterMultiplier >= 28_000)
                        hero.HealLife(Math.Max(1, hero.MaximumLife * 600 / 10_000));
                    int counterCooldown = ascendancy.Has(P18NodeIds.BastionCounterSmall)
                        ? shieldCounter.CooldownTicks * 10_000 / 12_000 : shieldCounter.CooldownTicks;
                    shieldCounterReadyTick = tick + Math.Max(1, counterCooldown);
                }
            }
            else if (damage > 0)
            {
                damage = Math.Max(1, damage * ascendancy.IncomingHitMultiplier(spell, false, tick) / 10_000);
                if (!spell) ascendancy.OnUnblockedAttack(tick);
            }
            if (activeSkill.Kind is EnemySkillKind.GroundHazard or EnemySkillKind.Artillery)
                hazards.Add(new(enemy.EntityId, impactPoint, 1_800, Math.Max(1, damage / 2), tick + 10, tick + 90,
                    activeSkill.DamageType));
            if (request.ExtraBossPhase && enemy.Boss && enemy.BossPhase > 0)
                hazards.Add(new(enemy.EntityId, impactPoint, 2_000, Math.Max(1, damage / 2), tick + 20, tick + 31,
                    activeSkill.DamageType));
            if (areaAttack && !InRange(heroPosition, impactPoint, 2_000)) damage = 0;
            if (activeSkill.Kind == EnemySkillKind.RootSnare && damage > 0)
            {
                rootedUntilTick = Math.Max(rootedUntilTick, tick + 20);
                events.Add(Event(tick, P4SpatialEventKind.Ailment, enemy.EntityId, "hero", 20,
                    enemy.Position, heroPosition, "缠根：1秒禁止移动"));
            }
            if (damage > 0)
            {
                P30VirtueViceBonuses virtueBonuses = request.VirtueVice?.Bonuses() ?? new P30VirtueViceState().Bonuses();
                damage = activeSkill.DamageType is EnemyDamageType.Physical or EnemyDamageType.Void
                    ? damage * virtueBonuses.PhysicalVoidDamageTakenMultiplierBasisPoints / 10_000
                    : damage * virtueBonuses.ElementalDamageTakenMultiplierBasisPoints / 10_000;
                hero.ApplyDamage(damage, tick);
            }

            string attackDetail = $"{activeSkill.DisplayName}|{activeSkill.Telegraph}|{activeSkill.DamageType}|{activeSkill.Avoidable}|{(spell ? "spell" : "attack")}";
            events.Add(Event(tick, P4SpatialEventKind.EnemyAttack, enemy.EntityId, "hero", damage,
                enemy.Position, impactPoint, attackDetail));
            int interval = Math.Max(8, checked((20_000 + attacksPerSecond - 1) / attacksPerSecond));
            interval = Math.Max(8, checked(interval * activeSkill.CooldownMultiplierBasisPoints / 10_000));
            enemy.NextActionTick = tick + interval;
            enemy.ActionSequence++;
        }
    }

    private static void ActivateUtility(
        IReadOnlyDictionary<P1FlaskKind, P1UtilityFlaskState> flasks,
        P1FlaskKind kind,
        int tick,
        P4Point heroPosition,
        ICollection<P4SpatialEvent> events)
    {
        if (flasks.GetValueOrDefault(kind) is not { } flask || !flask.TryUse()) return;
        events.Add(Event(tick, P4SpatialEventKind.Flask, "hero", "hero", 0, heroPosition, heroPosition,
            kind.ToString().ToLowerInvariant()));
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
        int bleedChance,
        ResourceState? hero = null,
        int lifeLeechBasisPoints = 0)
    {
        P18CombatRuntime runtime = request.AscendancyRuntime ?? new P18CombatRuntime(P18CombatProfile.Empty);
        string skillId = SkillIdFor(kind);
        SkillTag tags = string.IsNullOrEmpty(skillId) ? SkillTag.Attack : P1Skills.Get(skillId).Tags;
        int ascendancyMultiplier = runtime.ConsumeAttackMultiplier(tags,
            hero is not null && hero.Life * 2L <= hero.MaximumLife, !request.Build.HasShield,
            new P18EnemyState(enemy.ArmorBreakStacks, tick < enemy.StunnedUntilTick));
        if (runtime.Has(P18NodeIds.BloodLifeCore) && tags.HasFlag(SkillTag.Attack)) runtime.PaidLife(tick);
        int increased = checked(request.Build.IncreasedDamageBasisPoints +
            (request.Build.PassiveProfile ?? P205PassiveModifiers.Empty).DamageFor(tags) +
            ((request.Build.ActiveSkills ?? []).FirstOrDefault(s => s.SkillId == skillId)?.Quality ?? 0) * 100);
        int[] more = [skillMultiplier, ascendancyMultiplier, 10_000 + (request.Build.PassiveProfile?.MoreDamageBasisPoints ?? 0),
            10_000 + JewelMoreDamage(request.Build, tags, false)];
        DamageResult damage = DamageRules.Resolve(new DamageRequest(
            request.Build.Weapon,
            request.Build.AddedPhysicalDamage,
            request.Build.AddedPhysicalDamage,
            increased,
            more,
            request.Build.CannotCrit ? 0 : P30CombatRules.CriticalChance(
                request.Build.Weapon.CriticalChanceBasisPoints, request.Build.IncreasedCriticalChanceBasisPoints),
            request.Build.CriticalMultiplierBasisPoints,
            TargetArmor: enemy.Scaled.Armor,
            TargetEvasion: request.Build.AlwaysHit ? 0 : enemy.Scaled.Evasion,
            Accuracy: request.Build.Sheet.Accuracy(request.Build.FlatAccuracy).Value,
            IsSpell: kind is P4SpatialEventKind.SpiritBladeHit or P4SpatialEventKind.ChainHit,
            BleedChanceBasisPoints: checked(bleedChance + runtime.AdditionalBleedChance)), random);
        int physicalResistance = P30CombatRules.EffectiveResistance(
            enemy.Scaled.PhysicalResistanceBasisPoints + request.EnemyPhysicalReductionBasisPoints, 3_500);
        int physicalDamage = damage.Hit ? P30CombatRules.MitigateByResistance(damage.FinalPhysicalDamage, physicalResistance) : 0;
        SkillSupport supports = SupportsFor(request.Build, kind);
        P17DamageBreakdown localDamage = damage.Hit && tags.HasFlag(SkillTag.Attack) &&
                                             request.Build.LocalWeaponStats is { } localWeapon
            ? P17DamageRules.ResolveMixed(0, P17DamageType.Physical,
                new P17AddedWeaponDamage(
                    ScaleLocal(Roll(localWeapon.Fire)), ScaleLocal(Roll(localWeapon.Cold)),
                    ScaleLocal(Roll(localWeapon.Lightning)), ScaleLocal(Roll(localWeapon.Void))),
                supports, 0, enemy.Scaled.FireResistanceBasisPoints + request.EnemyElementalResistanceBasisPoints,
                enemy.Scaled.ColdResistanceBasisPoints + request.EnemyElementalResistanceBasisPoints,
                enemy.Scaled.LightningResistanceBasisPoints + request.EnemyElementalResistanceBasisPoints,
                enemy.Scaled.VoidResistanceBasisPoints + request.EnemyVoidResistanceBasisPoints,
                physicalResistance)
            : new P17DamageBreakdown(0, 0, 0, 0, 0, 0, []);
        int value = checked(physicalDamage + localDamage.Total);
        if (damage.Critical && request.VirtueVice is { } virtueVice)
            value = checked(value * (10_000 + virtueVice.Bonuses().MoreCriticalDamageBasisPoints) / 10_000);
        if (value > 0 && enemy.Rarity is EnemyRarity.Rare or EnemyRarity.Boss && request.VirtueVice is { } oathState)
        {
            IReadOnlyList<P30VirtueViceKind> oaths = request.Build.VirtueViceLoadout?.Oaths ?? [];
            string action = $"{tick}:{skillId}:{enemy.EntityId}";
            if (oaths.Contains(P30VirtueViceKind.Rage)) oathState.TryOathChance(P30VirtueViceKind.Rage, action, 1_200, random.NextUInt());
            if (damage.Critical && oaths.Contains(P30VirtueViceKind.Arrogance))
                oathState.TryOathChance(P30VirtueViceKind.Arrogance, action, 1_200, random.NextUInt());
            if (oaths.Contains(P30VirtueViceKind.Sloth)) oathState.RecordSlothOathHit($"{tick}:{skillId}");
        }
        enemy.Life = Math.Max(0, enemy.Life - value);
        if (hero is not null && value > 0 && lifeLeechBasisPoints > 0)
        {
            ApplyLifeLeech(hero, Math.Max(1, checked(value * lifeLeechBasisPoints / 10_000)),
                request.Build.InstantLifeLeechBasisPoints);
        }
        if (damage.AppliedBleed && enemy.Life > 0)
        {
            ApplyBleed(enemy, checked(damage.BleedTotalDamage * runtime.BleedDamageMultiplier / 10_000), runtime);
            runtime.AppliedBleed();
        }

        string hitDetail = damage.Critical ? "critical" : damage.Hit ? "hit" : "miss";
        events.Add(Event(tick, kind, "hero", enemy.EntityId, value, source, enemy.Position,
            $"{hitDetail}|damage:physical:{physicalDamage},fire:{localDamage.Fire},cold:{localDamage.Cold}," +
            $"lightning:{localDamage.Lightning},void:{localDamage.Void}|supports:{(int)supports}"));
        if (enemy.Life == 0)
        {
            events.Add(Event(tick, P4SpatialEventKind.EnemyDefeated, "hero", enemy.EntityId, 0,
                source, enemy.Position, enemy.Profile.StableId));
        }

        int ScaleLocal(int value)
        {
            if (value <= 0) return 0;
            value = checked(value * (10_000 + increased) / 10_000);
            foreach (int multiplier in more) value = checked(value * multiplier / 10_000);
            if (damage.Critical) value = checked(value * request.Build.CriticalMultiplierBasisPoints / 10_000);
            return value;
        }

        int Roll(LocalDamageRange range)
        {
            if (!range.HasDamage) return 0;
            int span = range.Maximum - range.Minimum + 1;
            return range.Minimum + (int)(random.NextUInt() % (uint)Math.Max(1, span));
        }
    }

    private static SkillSupport SupportsFor(P1TeamBuild build, P4SpatialEventKind kind)
    {
        string skillId = kind switch
        {
            P4SpatialEventKind.HeavyStrike => P1SkillIds.HeavyStrike,
            P4SpatialEventKind.EarthCleave => P1SkillIds.EarthCleave,
            P4SpatialEventKind.SpiritBladeHit or P4SpatialEventKind.ChainHit => P1SkillIds.SpiritBlade,
            P4SpatialEventKind.SeismicCharge => P1SkillIds.SeismicCharge,
            P4SpatialEventKind.BloodTideSpin => P1SkillIds.BloodTideSpin,
            P4SpatialEventKind.AshJavelin => P1SkillIds.AshJavelin,
            P4SpatialEventKind.EmberNova => P1SkillIds.EmberNova,
            P4SpatialEventKind.StormBrand => P1SkillIds.StormBrand,
            _ => string.Empty,
        };
        return (build.ActiveSkills ?? [build.HeavyStrike]).FirstOrDefault(skill => skill.SkillId == skillId)?.Supports ?? SkillSupport.None;
    }

    private static int JewelMoreDamage(P1TeamBuild build, SkillTag tags, bool damageOverTime)
    {
        int result = damageOverTime ? build.MoreDamageOverTimeBasisPoints : 0;
        if (tags.HasFlag(SkillTag.Attack)) result += build.MoreAttackDamageBasisPoints;
        if (tags.HasFlag(SkillTag.Spell)) result += build.MoreSpellDamageBasisPoints;
        return result;
    }

    private static int ActionDelay(P1TeamBuild build, int baseTicks) => Math.Max(1,
        checked((int)((long)Math.Max(1, baseTicks) * 10_000 /
            Math.Max(1_000, 10_000 + build.IncreasedActionSpeedBasisPoints))));

    private static void ApplyLifeLeech(ResourceState hero, int amount, int instantBasisPoints)
    {
        int instant = checked(amount * Math.Clamp(instantBasisPoints, 0, 10_000) / 10_000);
        if (instant > 0) hero.HealLife(instant);
        int remaining = amount - instant;
        if (remaining > 0) hero.AddLifeLeech(remaining);
    }

    private static P6ResolvedSkill ApplyAscendancyCost(P6ResolvedSkill skill, SkillConfiguration configuration,
        int maximumLife, P18CombatProfile profile)
    {
        P6ResolvedSkill result = P18AscendancyRules.ApplySkillCost(
            skill, P1Skills.Get(configuration.SkillId).Tags, maximumLife, profile);
        if (result.Role == P17SkillRole.Guard && profile.Has(P18NodeIds.BastionGuardSmall))
            result = result with { CooldownTicks = Math.Max(1, result.CooldownTicks * 10_000 / 12_500) };
        return result;
    }

    private static bool RechargeFlasksForKills(
        IEnumerable<P4EnemyUnit> enemies,
        LifeFlaskState? lifeFlask,
        IReadOnlyDictionary<P1FlaskKind, P1UtilityFlaskState> utilityFlasks,
        int tick,
        P4Point heroPosition,
        ICollection<P4SpatialEvent> events,
        P18CombatRuntime runtime,
        ResourceState hero)
    {
        bool resetMovement = false;
        foreach (P4EnemyUnit enemy in enemies.Where(item => item.Life <= 0 && !item.KillCharged && !item.Summoned))
        {
            enemy.KillCharged = true;
            int charges = enemy.Rarity switch
            {
                EnemyRarity.Boss => 6,
                EnemyRarity.Rare => 4,
                EnemyRarity.Magic => 2,
                _ => 1,
            };
            lifeFlask?.GainCharges(charges);
            utilityFlasks.GetValueOrDefault(P1FlaskKind.Mana)?.GainCharges(charges);
            events.Add(Event(tick, P4SpatialEventKind.FlaskCharge, "hero", "hero", charges,
                heroPosition, heroPosition, $"life+{charges}|mana+{charges}"));
            if (enemy.BleedRemaining > 0)
            {
                runtime.KilledBleedingEnemy();
                if (runtime.Has(P18NodeIds.BloodTideCore))
                {
                    hero.HealLife(Math.Max(1, hero.MaximumLife * 400 / 10_000));
                    runtime.TriggerRecoveryProtection(tick);
                    P4EnemyUnit? spread = enemies.Where(item => item.Life > 0)
                        .OrderBy(item => P4Point.DistanceSquared(enemy.Position, item.Position)).FirstOrDefault();
                    if (spread is not null) ApplyBleed(spread, checked(enemy.BleedRemaining * 12 / 10), runtime);
                }
            }
            resetMovement |= runtime.TryResetMovementCooldownOnKill(tick);
        }
        return resetMovement;
    }

    private static string SkillIdFor(P4SpatialEventKind kind) => kind switch
    {
        P4SpatialEventKind.HeavyStrike => P1SkillIds.HeavyStrike,
        P4SpatialEventKind.EarthCleave => P1SkillIds.EarthCleave,
        P4SpatialEventKind.SpiritBladeHit or P4SpatialEventKind.ChainHit => P1SkillIds.SpiritBlade,
        P4SpatialEventKind.SeismicCharge => P1SkillIds.SeismicCharge,
        P4SpatialEventKind.BloodTideSpin => P1SkillIds.BloodTideSpin,
        P4SpatialEventKind.AshJavelin => P1SkillIds.AshJavelin,
        P4SpatialEventKind.EmberNova => P1SkillIds.EmberNova,
        P4SpatialEventKind.StormBrand => P1SkillIds.StormBrand,
        _ => string.Empty,
    };

    private static void ApplyBleed(P4EnemyUnit enemy, int totalDamage, P18CombatRuntime runtime)
    {
        int pulses = Math.Max(1, runtime.BleedPulseCount - Math.Min(3, enemy.RuptureStacks));
        if (runtime.Has(P18NodeIds.BloodRuptureCore) && enemy.BleedRemaining > 0)
            enemy.RuptureStacks = Math.Min(3, enemy.RuptureStacks + 1);
        if (runtime.TwoBleeds)
        {
            int first = Math.Max(enemy.BleedRemaining, totalDamage);
            int second = Math.Min(Math.Max(enemy.BleedRemaining, 0), totalDamage);
            enemy.BleedRemaining = checked((first + second) * 8 / 10);
        }
        else enemy.BleedRemaining = Math.Max(enemy.BleedRemaining, totalDamage);
        enemy.BleedPulses = Math.Max(enemy.BleedPulses, pulses);
    }

    private static void ResolveProjectiles(
        P4NodeCombatRequest request,
        IList<PendingBlade> projectiles,
        IList<P4EnemyUnit> enemies,
        ResourceState hero,
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
                projectile.Origin, events, 0, hero, projectile.LifeLeechBasisPoints);
            if (projectile.ChainIndex >= projectile.MaximumChains)
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
                    target.Position,
                    projectile.MaximumChains,
                    projectile.LifeLeechBasisPoints));
            }
        }
    }

    private static void ResolveAftershocks(IList<PendingAftershock> pending, IEnumerable<P4EnemyUnit> enemies,
        int tick, ICollection<P4SpatialEvent> events)
    {
        foreach (PendingAftershock aftershock in pending.Where(item => item.ImpactTick <= tick).ToArray())
        {
            pending.Remove(aftershock);
            P4EnemyUnit? target = enemies.FirstOrDefault(item => item.EntityId == aftershock.TargetId && item.Life > 0);
            if (target is null) continue;
            int damage = Math.Min(target.Life, aftershock.ActualHitDamage);
            target.Life -= damage;
            events.Add(Event(tick, P4SpatialEventKind.Ascendancy, "hero", target.EntityId, damage,
                aftershock.Origin, target.Position, "linebreaker:aftershock"));
            if (target.Life == 0)
                events.Add(Event(tick, P4SpatialEventKind.EnemyDefeated, "hero", target.EntityId, 0,
                    aftershock.Origin, target.Position, target.Profile.StableId));
        }
    }

    private static void ResolveBleeds(IEnumerable<P4EnemyUnit> enemies, ResourceState hero,
        P18CombatRuntime runtime, int tick, ICollection<P4SpatialEvent> events)
    {
        if (tick == 0 || tick % 20 != 0)
        {
            return;
        }

        bool recoveredFromBoss = false;
        foreach (P4EnemyUnit enemy in enemies.Where(enemy => enemy.Life > 0 && enemy.BleedPulses > 0))
        {
            int damage = Math.Max(1, enemy.BleedRemaining / enemy.BleedPulses);
            enemy.BleedRemaining -= damage;
            enemy.BleedPulses--;
            enemy.Life = Math.Max(0, enemy.Life - damage);
            events.Add(Event(tick, P4SpatialEventKind.Bleed, "hero", enemy.EntityId, damage,
                enemy.Position, enemy.Position, "enemy"));
            if (!recoveredFromBoss && runtime.Has(P18NodeIds.BloodTideCore) &&
                enemy.Rarity is EnemyRarity.Rare or EnemyRarity.Boss)
            {
                hero.HealLife(Math.Max(1, hero.MaximumLife * 400 / 10_000));
                runtime.TriggerRecoveryProtection(tick);
                recoveredFromBoss = true;
            }
            if (enemy.Life == 0)
            {
                events.Add(Event(tick, P4SpatialEventKind.EnemyDefeated, "hero", enemy.EntityId, 0,
                    enemy.Position, enemy.Position, enemy.Profile.StableId));
            }
        }
    }

    private static void ResolveP17DamageOverTime(IEnumerable<P4EnemyUnit> enemies, int tick, ICollection<P4SpatialEvent> events)
    {
        if (tick == 0 || tick % 20 != 0) return;
        foreach (P4EnemyUnit enemy in enemies.Where(enemy => enemy.Life > 0 && enemy.P17DotPulses > 0))
        {
            int damage = Math.Max(1, enemy.P17DotRemaining / enemy.P17DotPulses);
            enemy.P17DotRemaining -= damage;
            enemy.P17DotPulses--;
            enemy.Life = Math.Max(0, enemy.Life - damage);
            events.Add(Event(tick, P4SpatialEventKind.Ailment, "hero", enemy.EntityId, damage,
                enemy.Position, enemy.Position, $"dot:{enemy.P17DotAilment.ToString().ToLowerInvariant()}"));
            if (enemy.Life == 0)
                events.Add(Event(tick, P4SpatialEventKind.EnemyDefeated, "hero", enemy.EntityId, 0,
                    enemy.Position, enemy.Position, enemy.Profile.StableId));
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

        int flaskEffect = request.Build.IncreasedLifeFlaskEffectBasisPoints;
        if (request.Build.Ascendancy?.Has(P18NodeIds.BloodLowLifeSmall) == true && hero.Life * 2L <= hero.MaximumLife)
            flaskEffect = checked(flaskEffect + 2_500);
        int recovered = flask.TryUse(hero.MaximumLife - hero.Life, flaskEffect);
        recovered = checked((int)((long)recovered * request.PlayerRecoveryBasisPoints / 10_000));
        recovered = checked((int)((long)recovered * request.FlaskRecoveryBasisPoints / 10_000));
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
        IEnumerable<P4EnemyUnit> enemies,
        int partySize,
        int frontlineCount)
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
                enemy.Rarity,
                enemy.Elite,
                enemy.Boss,
                enemy.Life,
                enemy.MaximumLife,
                enemy.Position,
                "hero",
                enemy.Scaled.EliteAffixes, enemy.Summoned)).ToArray(),
            BuildAllies(heroPosition, partySize, frontlineCount)));
        // Keep simulation exact, but bound playback snapshots in extremely long battles.
        if (frames is List<P4SpatialFrame> list && list.Count > 4_096)
        {
            P4SpatialFrame[] retained = list.Where((_, index) => index == 0 || index % 2 == 1 || index == list.Count - 1).ToArray();
            list.Clear(); list.AddRange(retained);
        }
    }

    private static IReadOnlyList<P4AllyFrame> BuildAllies(P4Point leader, int partySize, int frontlineCount)
    {
        var result = new List<P4AllyFrame>();
        int frontRemaining = Math.Max(0, frontlineCount - 1);
        for (int index = 1; index < Math.Clamp(partySize, 1, 6); index++)
        {
            bool front = index <= frontRemaining;
            int ordinal = front ? index - 1 : index - frontRemaining - 1;
            int x = leader.XRaw + (ordinal % 2 == 0 ? -1 : 1) * (900 + ordinal / 2 * 650);
            int y = leader.YRaw + (front ? -1_100 : 1_200 + ordinal / 2 * 550);
            result.Add(new P4AllyFrame($"ally-{index}", new P4Point(Math.Clamp(x, 350, 11_650), Math.Clamp(y, 350, 23_650)), front));
        }
        return result;
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

    private static bool InCleaveCone(P4Point origin, P4Point facingTarget, P4Point candidate, int range)
    {
        long candidateDistance = P4Point.DistanceSquared(origin, candidate);
        if (candidateDistance > (long)range * range)
        {
            return false;
        }

        long facingX = facingTarget.XRaw - origin.XRaw;
        long facingY = facingTarget.YRaw - origin.YRaw;
        long candidateX = candidate.XRaw - origin.XRaw;
        long candidateY = candidate.YRaw - origin.YRaw;
        long dot = facingX * candidateX + facingY * candidateY;
        long facingDistance = facingX * facingX + facingY * facingY;
        return dot > 0 && 4 * dot * dot >= facingDistance * candidateDistance;
    }

    private static int TravelTicks(P4Point from, P4Point to, int speedPerSecond)
    {
        long distance = (long)Math.Sqrt(P4Point.DistanceSquared(from, to));
        return Math.Max(1, checked((int)((distance * 20 + speedPerSecond - 1) / speedPerSecond)));
    }

    private static P6ResolvedSkill? Resolve(
        IReadOnlyDictionary<string, SkillConfiguration> skills,
        string skillId,
        int maximumLife,
        P205PassiveModifiers? passive) => skills.TryGetValue(skillId, out SkillConfiguration? configuration)
        ? P6CombatSkillRules.Resolve(configuration, maximumLife, passive)
        : null;

    private static string? Candidate(string skillId, bool available) => available ? skillId : null;

    private static bool CanPay(ResourceState hero, P6ResolvedSkill skill) => skill.LifeCost > 0
        ? hero.Life > skill.LifeCost
        : hero.Mana >= skill.ManaCost;

    private static bool AiMatches(
        SkillConfiguration configuration,
        P4NodeCombatRequest request,
        ResourceState hero,
        P4EnemyUnit target,
        IReadOnlyCollection<P4EnemyUnit> enemies,
        long distanceSquared)
    {
        SkillAiRule rule = configuration.AiRule ?? new SkillAiRule();
        int distance = (int)Math.Sqrt(distanceSquared);
        int alive = enemies.Count(enemy => enemy.Life > 0);
        string rarity = target.Boss ? "Boss" : target.Elite ? "精英" : "普通";
        bool targetMatches = rule.TargetPolicy switch
        {
            SkillTargetPolicy.BossOnly => target.Boss,
            SkillTargetPolicy.EliteAndBoss => target.Elite || target.Boss,
            _ => true,
        };
        if (!targetMatches) return false;
        bool[] checks =
        [
            (long)hero.Life * 10_000 >= (long)hero.MaximumLife * rule.MinimumLifeBasisPoints,
            (long)hero.Mana * 10_000 >= (long)Math.Max(1, hero.MaximumMana) * rule.MinimumManaBasisPoints,
            alive >= rule.MinimumEnemyCount,
            rule.EnemyRarity == "任意" || rule.EnemyRarity == rarity,
            distance >= rule.MinimumDistanceRaw,
            distance <= rule.MaximumDistanceRaw,
            50 + request.AreaLevel * 5 >= rule.DangerThreshold,
            !rule.BossOnly || target.Boss,
        ];
        return rule.MatchAll ? checks.All(value => value) : checks.Any(value => value);
    }

    private static int SkillMultiplier(P6ResolvedSkill skill, P4EnemyUnit enemy, int bannerMultiplier) => checked(
        P6CombatSkillRules.DamageMultiplier(skill, enemy.Life, enemy.MaximumLife) * bannerMultiplier / 10_000);

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
        if (request.NodeIndex <= 0 || request.AreaLevel is < 1 or > 120 || request.EnemyCount is < 1 or > 128 ||
            request.MaximumTicks < 0 || request.EnemyLifeBasisPoints is < 1_000 or > 500_000 ||
            request.EnemyDamageBasisPoints is < 1_000 or > 500_000 || request.EnemySpeedBasisPoints is < 1_000 or > 100_000 ||
            request.PlayerRecoveryBasisPoints is < 0 or > 10_000 ||
            request.BossLifeBasisPoints is < 1_000 or > 100_000 || request.BossDamageBasisPoints is < 1_000 or > 100_000 ||
            request.EnemyPhysicalReductionBasisPoints is < 0 or > 9_000 || request.EnemyElementalResistanceBasisPoints is < 0 or > 9_000 ||
            request.EnemyVoidResistanceBasisPoints is < 0 or > 9_000 || request.EnemyPenetrationBasisPoints is < 0 or > 9_000 ||
            request.ExtraEnemyProjectiles is < 0 or > 8 || request.EnemyProjectileDamageBasisPoints is < 1_000 or > 20_000 ||
            request.EnemyAreaBasisPoints is < 1_000 or > 30_000 || request.EnemyAreaDamageBasisPoints is < 1_000 or > 30_000 ||
            request.BossCount is < 1 or > 2 || request.AdditionalRareEnemies is < 0 or > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(request));
        }
    }

    private sealed class P4EnemyUnit(
        string entityId,
        EnemyProfile profile,
        ScaledEnemy scaled,
        P4UnitRole role,
        EnemyRarity rarity,
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
        public EnemyRarity Rarity { get; } = rarity;
        public bool Elite { get; } = elite;
        public bool Boss { get; } = boss;
        public int MaximumLife { get; } = life;
        public int Ordinal { get; } = int.Parse(entityId[(entityId.LastIndexOf('-') + 1)..]);
        private int _life = life;
        public P4EnemyUnit? LinkedBy { get; set; }
        public int Life
        {
            get => _life;
            set => _life = value < _life && LinkedBy is { Life: > 0 } source &&
                InRange(Position, source.Position, 4_000)
                ? Math.Max(0, _life - Math.Max(1, (_life - value) * 7 / 10)) : value;
        }
        public bool Summoned { get; init; }
        public int ShieldUntilTick { get; set; }
        public P4Point? TelegraphTarget { get; set; }
        public P4Point Position { get; set; } = position;
        public int NextActionTick { get; set; } = nextActionTick;
        public int BleedRemaining { get; set; }
        public int BleedPulses { get; set; }
        public int P17DotRemaining { get; set; }
        public int P17DotPulses { get; set; }
        public P17Ailment P17DotAilment { get; set; }
        public int ArmorBreakStacks { get; set; }
        public int ShockStacks { get; set; }
        public int ImpairedUntilTick { get; set; }
        public int BossPhase { get; set; }
        public int LastTelegraphTick { get; set; } = int.MinValue;
        public int RuptureStacks { get; set; }
        public int StunnedUntilTick { get; set; }
        public int StunImmuneUntilTick { get; set; }
        public bool KillCharged { get; set; }
        public int ActionSequence { get; set; }
    }

    private sealed record PendingBlade(
        int ImpactTick,
        string TargetId,
        int ChainIndex,
        int Multiplier,
        IReadOnlyList<string> HitIds,
        P4Point Origin,
        int MaximumChains,
        int LifeLeechBasisPoints);

    private sealed record PendingAftershock(int ImpactTick, string TargetId, int ActualHitDamage, P4Point Origin);
    private sealed record EnemyHazard(string Source, P4Point Position, int Radius, int Damage, int Start, int Expires,
        EnemyDamageType DamageType);
}
