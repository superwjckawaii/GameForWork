using System.Security.Cryptography;
using System.Text;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Campaign.Progression;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Simulation;
using GameForWork.Core.Skills;
using GameForWork.Core.Content;
using GameForWork.Core.SkillCatalog;
using GameForWork.Core.Ascendancies;
using GameForWork.Core.Characters;
using GameForWork.Core.Monsters;
using GameForWork.Core.Builds;
using GameForWork.Core.Equipment;

namespace GameForWork.Core.Spatial;

public enum UnitRole
{
    Melee,
    Ranged,
    Caster,
    Charger,
    Summoner,
    Boss,
}

public enum SpatialEventKind
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

public readonly record struct Point(int XRaw, int YRaw)
{
    public static long DistanceSquared(Point left, Point right)
    {
        long x = left.XRaw - right.XRaw;
        long y = left.YRaw - right.YRaw;
        return x * x + y * y;
    }

    public static Point MoveToward(Point from, Point to, int maximumDistanceRaw)
    {
        long squared = DistanceSquared(from, to);
        if (squared == 0 || squared <= (long)maximumDistanceRaw * maximumDistanceRaw)
        {
            return to;
        }

        long distance = IntegerSqrt(squared);
        int x = checked(from.XRaw + (int)((to.XRaw - from.XRaw) * (long)maximumDistanceRaw / distance));
        int y = checked(from.YRaw + (int)((to.YRaw - from.YRaw) * (long)maximumDistanceRaw / distance));
        return new Point(Math.Clamp(x, 350, 11_650), Math.Clamp(y, 350, 23_650));
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

public sealed record EnemyFrame(
    string EntityId,
    string EnemyStableId,
    string DisplayName,
    UnitRole Role,
    EnemyRarity Rarity,
    bool Elite,
    bool Boss,
    int Life,
    int MaximumLife,
    Point Position,
    string TargetId,
    IReadOnlyList<EliteAffix>? EliteAffixes = null, bool Summoned = false,
    int BleedStacks = 0, Ailment DamageOverTimeAilment = Ailment.None,
    int ArmorBreakStacks = 0, int ShockStacks = 0, bool Impaired = false);

public sealed record AllyFrame(string EntityId, Point Position, bool Frontline,
    string SkillId = "", int Life = 1, int MaximumLife = 1);

public sealed record SpatialFrame(
    long AtMilliseconds,
    int NodeIndex,
    Point HeroPosition,
    int HeroLife,
    int HeroMaximumLife,
    int HeroMana,
    int HeroMaximumMana,
    int HeroShield,
    int HeroMaximumShield,
    string HeroTargetId,
    IReadOnlyList<EnemyFrame> Enemies,
    IReadOnlyList<AllyFrame>? Allies = null,
    IReadOnlyDictionary<VirtueViceKind, int>? HeroVirtueViceLayers = null);

public sealed record SpatialEvent(
    long AtMilliseconds,
    SpatialEventKind Kind,
    string SourceId,
    string TargetId,
    int Value,
    Point SourcePosition,
    Point TargetPosition,
    string Detail);

public sealed record NodeCombatRequest(
    TeamBuild Build,
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
    CombatRuntime? AscendancyRuntime = null,
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
    VirtueViceState? VirtueVice = null,
    EquipmentCombatRuntime? EquipmentRuntime = null,
    Combat.CombatActionQueue? Actions = null, Combat.AuraCombatProfile? Auras = null,
    Combat.FlaskRack? FlaskState = null);

public sealed record NodeCombatResult(
    BattleOutcome Outcome,
    int Ticks,
    int HeroLife,
    int HeroMana,
    int HeroShield,
    IReadOnlyList<SpatialFrame> Frames,
    IReadOnlyList<SpatialEvent> Events,
    string FinalHash);

public sealed partial class SpatialCombatRunner
{
    public const int TickMilliseconds = 50;
    private const int StalemateProgressWindowTicks = 1_200;
    private const int HeroEntityRawSpeed = 4_000;
    private const int HeavyStrikeRange = 1_500;
    private const int CleaveRange = 2_800;
    private const int BladeRange = 8_000;
    private const int ChainRange = 4_000;

    public NodeCombatResult Run(NodeCombatRequest request, ulong seed)
    {
        Validate(request);
        var auras = Combat.AuraCombatProfile.Resolve(request.Build);
        request = request with { Build = auras.Build, Auras = auras };
        var random = new Pcg32(seed);
        var equipment = request.EquipmentRuntime ?? new EquipmentCombatRuntime(request.Build.CombatEquipment ?? EquipmentCombatLoadout.Empty, seed);
        TeamBuild originalBuild = request.Build;
        request = request with { EquipmentRuntime = equipment, Actions = new Combat.CombatActionQueue() };
        var hero = new ResourceState(
            request.Build.Sheet,
            request.InitialHeroLife,
            request.InitialHeroMana,
            request.InitialHeroShield);
        hero.ReserveMana(auras.ReservedMana);
        equipment.ExternalSkillCostMultiplier = auras.SkillCostMultiplier;
        var enemies = CreateEnemies(request, random);
        var events = new List<SpatialEvent>();
        var frames = new List<SpatialFrame>();
        var projectiles = new List<PendingProjectile>();
        var aftershocks = new List<PendingAftershock>();
        var hazards = new List<EnemyHazard>();
        int rootedUntilTick = 0;
        int dodgeUntilTick = 0, dodgeReadyTick = 0;
        CombatProfile ascendancy = request.Build.Ascendancy ?? CombatProfile.Empty;
        var ascendancyRuntime = new CombatRuntime(ascendancy);
        VirtueViceKind[] held = AscendancyDefinitions.PermanentVirtueVice(ascendancy).ToArray();
        VirtueViceLoadout loadout = request.Build.VirtueViceLoadout ?? VirtueViceLoadout.Empty;
        held = held.Concat(loadout.HeldAtMaximum).Distinct().ToArray();
        var maxima = new Dictionary<VirtueViceKind, int>(loadout.AdditionalMaximum);
        foreach (VirtueViceKind kind in AscendancyDefinitions.PermanentVirtueVice(ascendancy))
            maxima[kind] = maxima.GetValueOrDefault(kind) + 1;
        var virtueVice = request.VirtueVice ?? new VirtueViceState(
            maxima, held);
        request = request with { AscendancyRuntime = ascendancyRuntime, VirtueVice = virtueVice };
        Point heroPosition = new(6_000, 22_000);
        var flasks = request.FlaskState ?? new Combat.FlaskRack(request.Build);
        void UseFlask(FlaskKind kind, int at, int threshold = 0)
        {
            if (flasks.TryUse(kind, hero, random, threshold,
                ScaleCombatValue(request.PlayerRecoveryBasisPoints, request.FlaskRecoveryBasisPoints)) is not { } activation) return;
            equipment.FlaskUsed(virtueVice);
            events.Add(Event(at, SpatialEventKind.Flask, "hero", "hero", 0, heroPosition, heroPosition,
                $"{kind.ToString().ToLowerInvariant()}|bottle:{activation.Id}|charges:{activation.ChargesSpent}"));
        }
        SkillUseProfile heavyStrike = request.Build.HeavyStrikeProfile ?? SkillRules.BuildHeavyStrike(
            (request.Build.ActiveSkills ?? []).FirstOrDefault(skill => skill.SkillId == SkillIds.HeavyStrike) ?? request.Build.HeavyStrike,
            request.Build.Weapon,
            hero.MaximumLife,
            request.Build.IncreasedAttackSpeedBasisPoints);
        if (request.Build.HeavyStrikeProfile is null)
            heavyStrike = LegendaryRules.ApplyToHeavyStrike(heavyStrike, request.Build.WeaponLegendaryRule);
        heavyStrike = WarriorAscendancyRules.ApplyHeavyStrikeCost(heavyStrike, hero.MaximumLife, ascendancy);
        var warCry = new WarCryState { EchoNotableAllocated = request.Build.EchoNotableAllocated };
        Dictionary<string, SkillConfiguration> skills = (request.Build.ActiveSkills ?? [request.Build.HeavyStrike])
            .GroupBy(skill => skill.SkillId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        HashSet<string> legacySkills =
        [
            SkillIds.HeavyStrike, SkillIds.WarCry, SkillIds.EarthCleave, SkillIds.SpiritBlade,
            SkillIds.SeismicCharge, SkillIds.BloodTideSpin, SkillIds.IronOathBanner,
            SkillIds.AshJavelin, SkillIds.EmberNova, SkillIds.StormBrand,
        ];
        Dictionary<string, ResolvedSkill> skillCatalogSkills = skills
            .Where(pair => !legacySkills.Contains(pair.Key) && pair.Key != "archetypes.skill.elemental_imprint")
            .ToDictionary(pair => pair.Key, pair => equipment.Resolve(ApplyAscendancyCost(
                CombatSkillRules.Resolve(pair.Value, hero.MaximumLife, request.Build.PassiveProfile), pair.Value, hero.MaximumLife, ascendancy)), StringComparer.Ordinal);
        Dictionary<string, int> skillCatalogReadyTicks = skillCatalogSkills.Keys.ToDictionary(key => key, _ => 0, StringComparer.Ordinal);
        Dictionary<string, int> skillCatalogUseCounts = skillCatalogSkills.Keys.ToDictionary(key => key, _ => 0, StringComparer.Ordinal);
        ResolvedSkill? shieldCounter = skillCatalogSkills.GetValueOrDefault(SkillIds.VengefulCounter);
        SkillConfiguration? shieldCounterConfiguration = skills.GetValueOrDefault(SkillIds.VengefulCounter);
        int shieldCounterReadyTick = 0;
        ResolvedSkill? Asc(string id)
        {
            if (Resolve(skills, id, hero.MaximumLife, request.Build.PassiveProfile) is not { } resolved) return null;
            resolved = equipment.Resolve(ApplyAscendancyCost(resolved, skills[id], hero.MaximumLife, ascendancy));
            if (id != SkillIds.WarCry) return resolved;
            return resolved with
            {
                RangeRaw = checked(resolved.RangeRaw * (10_000 + request.Build.IncreasedWarCryRangeBasisPoints) / 10_000),
                CooldownTicks = Math.Max(1, checked(resolved.CooldownTicks * 10_000 /
                    Math.Max(1, 10_000 + request.Build.IncreasedWarCryCooldownRecoveryBasisPoints))),
            };
        }
        ResolvedSkill? cleave = Asc(SkillIds.EarthCleave);
        ResolvedSkill? blade = Asc(SkillIds.SpiritBlade);
        ResolvedSkill? charge = Asc(SkillIds.SeismicCharge);
        ResolvedSkill? spin = Asc(SkillIds.BloodTideSpin);

        ResolvedSkill? warCrySkill = Asc(SkillIds.WarCry);
        ResolvedSkill? heavyResolved = Asc(SkillIds.HeavyStrike);
        ResolvedSkill? ashJavelin = Asc(SkillIds.AshJavelin);
        ResolvedSkill? emberNova = Asc(SkillIds.EmberNova);
        ResolvedSkill? stormBrand = Asc(SkillIds.StormBrand);
        if (warCrySkill is not null)
        {
            warCry.ManaCost = warCrySkill.ManaCost;
            warCry.CooldownDurationTicks = warCrySkill.CooldownTicks;
            warCry.EffectMultiplierBasisPoints = skills[SkillIds.WarCry].Supports.HasFlag(SkillSupport.UrgentWarCry) ? 8_500 : 10_000;
            if (ascendancy.Has(WarriorNodeIds.BreakerWarCrySmall))
                warCry.CooldownDurationTicks = Math.Max(1, warCry.CooldownDurationTicks * 10_000 / 13_000);
        }

        var army = new BattleArmy(request, skills.Values, heroPosition);
        equipment.NearbyEnemyCount = () => enemies.Count(enemy => enemy.Life > 0 && InRange(heroPosition, enemy.Position, 6_000));
        string heroTargetId = string.Empty;
        int heroNextActionTick = 0;
        int heavyStrikeFrequencyCarry = 0;
        var skillCatalogAttackFrequencyCarry = new Dictionary<string, int>(StringComparer.Ordinal);
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
        int fortificationLayers = 0;
        int fortificationUntilTick = 0;
        int tick = 0;
        string lastFortificationAction = "";
        equipment.PhysicalMeleeHit = () =>
        {
            if (!auras.PhysicalFortification || lastFortificationAction == equipment.ActionId) return;
            lastFortificationAction = equipment.ActionId;
            fortificationLayers = Math.Min(MasteryRuntime.FortificationMaximum(request.Build.PassiveProfile ?? PassiveModifiers.Empty), fortificationLayers + 1);
            fortificationUntilTick = tick + 80;
        };
        int lastSelfAttackOrSpellTick = int.MinValue / 2;
        foreach (string id in auras.ActiveIds)
            events.Add(Event(0, SpatialEventKind.BannerActivated, "hero", "hero", 0,
                heroPosition, heroPosition, $"skill:{id}|reserved-mana:{auras.ReservedMana}"));

        equipment.RedirectDamage = damage => army.RedirectDamage(damage, tick, events);
        equipment.CompanionAlive = () => army.CompanionAlive;
        hero.LifeDepleted = () =>
        {
            if (!equipment.TryRekindle(hero)) return;
            flasks.Fill();
            rootedUntilTick = 0;
            warCry.ResetCooldown();
            foreach (string id in skillCatalogReadyTicks.Keys.ToArray())
                if (SkillDefinitions.Get(id).Tags.HasFlag(SkillTag.WarCry)) skillCatalogReadyTicks[id] = tick;
            events.Add(Event(tick, SpatialEventKind.Ascendancy, "hero", "hero", equipment.Rekindles,
                heroPosition, heroPosition, "equipment:灰烬之心|重燃"));
        };
        int initialHeroLife = hero.Life;
        int minimumHeroLife = hero.Life;
        int initialEnemyLife = enemies.Sum(enemy => enemy.MaximumLife);
        int minimumEnemyLife = initialEnemyLife;
        int lastProgressTick = 0;
        BattleOutcome? projectedOutcome = null;
        CaptureFrame(frames, 0, request.NodeIndex, heroPosition, hero, heroTargetId, enemies,
            request.Build.PartySize, request.Build.FrontlineCount, request.VirtueVice, 0, army, request.Actions);

        for (tick = 0; (request.MaximumTicks == 0 || tick < request.MaximumTicks) &&
             hero.IsAlive && enemies.Any(enemy => enemy.Life > 0); tick++)
        {
            virtueVice.Advance(TickMilliseconds);
            Point beforeMovement = heroPosition;
            foreach (EnemyUnit unit in enemies)
                unit.LinkedBy = enemies.FirstOrDefault(source => source != unit && source.Life > 0 &&
                    source.ShieldUntilTick > tick && InRange(source.Position, unit.Position, 4_000));
            if (tick >= rootedUntilTick)
            {
                EnemyUnit? warning = enemies.FirstOrDefault(e => e.Life > 0 && e.TelegraphTarget is not null &&
                    InRange(heroPosition, e.TelegraphTarget.Value, 2_000));
                Point? danger = warning?.TelegraphTarget ?? hazards.FirstOrDefault(h =>
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
                    heroPosition = Point.MoveToward(heroPosition,
                        heroPosition with { XRaw = Math.Clamp(heroPosition.XRaw + direction * 3_000, 350, 11_650) },
                        Math.Max(1, request.Build.MovementSpeedBasisPoints * 300 / 10_000));
                }
            }
            request = request with { Build = originalBuild with
            {
                IncreasedActionSpeedBasisPoints = originalBuild.IncreasedActionSpeedBasisPoints + equipment.SpeedBonus(tick),
                MovementSpeedBasisPoints = originalBuild.MovementSpeedBasisPoints + equipment.MovementBonus(tick) + flasks.Buff(ItemModifierKind.FlaskBuffMovementSpeedBasisPoints) +
                    (equipment.Has("朝圣者之债") ? Math.Min(4_500, flasks.UnusedUses * 300) : 0),
                IncreasedCriticalChanceBasisPoints = originalBuild.IncreasedCriticalChanceBasisPoints + flasks.Buff(ItemModifierKind.FlaskBuffCriticalChanceBasisPoints),
                Sheet = originalBuild.Sheet with {
                    IncreasedArmorBasisPoints = originalBuild.Sheet.IncreasedArmorBasisPoints + flasks.Buff(ItemModifierKind.FlaskBuffArmorBasisPoints),
                    IncreasedEvasionBasisPoints = originalBuild.Sheet.IncreasedEvasionBasisPoints + flasks.Buff(ItemModifierKind.FlaskBuffEvasionBasisPoints),
                },
            } };
            hero.AdvanceRegenerationTick(tick);
            if (tick >= fortificationUntilTick) fortificationLayers = 0;
            if (tick > 0 && tick % 20 == 0)
            {
                ascendancyRuntime.AdvanceSecond();
                int passiveRecovery = ascendancyRuntime.PassiveRecoveryBasisPoints;
                if (passiveRecovery > 0) hero.HealLife(Math.Max(1, hero.MaximumLife * passiveRecovery / 10_000));
                if (tick < guardUntilTick && ascendancy.Has(WarriorNodeIds.BastionGuardCore))
                    hero.HealLife(Math.Max(1, hero.MaximumLife * 500 / 10_000));
            }
            warCry.AdvanceTick();
            foreach (var recovery in flasks.Advance(hero, TickMilliseconds))
                events.Add(Event(tick, SpatialEventKind.Flask, "hero", "hero", recovery.Amount,
                    heroPosition, heroPosition, $"{recovery.Kind.ToString().ToLowerInvariant()}|recovery"));
            if (hero.Life * 10_000L < hero.MaximumLife * (long)request.Build.LifeFlaskUseThresholdBasisPoints)
                UseFlask(FlaskKind.Life, tick, request.Build.LifeFlaskUseThresholdBasisPoints);
            if (hero.Mana * 10_000L < hero.AvailableMaximumMana * 3_500L)
                UseFlask(FlaskKind.Mana, tick, 3_500);
            if (hero.LastDamageTick >= tick - 1)
            {
                UseFlask(FlaskKind.Armor, tick);
                UseFlask(FlaskKind.Resistance, tick);
            }
            AdvanceAilments(request, enemies, hero, tick, events);
            ResolveProjectiles(projectiles, enemies, hero, heroPosition, random, tick, events);
            request.Actions!.CompleteReady(tick * TickMilliseconds, projectiles.Select(projectile => projectile.Action.Context.Id).ToHashSet(),
                equipment.Has("百式回身"), equipment.Has("攻法回文"));
            ResolveCopies(request, enemies, hero, random, tick, events);
            ResolveAftershocks(aftershocks, enemies, tick, events);

            EnemyUnit? target = SelectTarget(enemies, heroPosition);
            heroTargetId = target?.EntityId ?? string.Empty;
            if (target is not null && tick >= heroNextActionTick && heroPosition == beforeMovement)
            {
                var skillTargets = skills.ToDictionary(
                    pair => pair.Key,
                    pair => SelectTarget(enemies, heroPosition,
                        pair.Value.AiRule?.TargetPolicy ?? SkillTargetPolicy.AllEnemies),
                    StringComparer.Ordinal);
                EnemyUnit? SkillTarget(string skillId) => skillTargets.GetValueOrDefault(skillId) ?? target;
                long SkillDistance(string skillId) => Point.DistanceSquared(heroPosition, SkillTarget(skillId)!.Position);
                int ConeCount(string skillId, int range) => SkillTarget(skillId) is not EnemyUnit selected ? 0 :
                    enemies.Count(enemy => enemy.Life > 0 &&
                        InCleaveCone(heroPosition, selected.Position, enemy.Position, range));
                int NearbyCount(string skillId, int range) => SkillTarget(skillId) is null ? 0 :
                    enemies.Count(enemy => enemy.Life > 0 && InRange(heroPosition, enemy.Position, range));
                string? chosen = new[]
                    {
                        Candidate(SkillIds.WarCry, request.Build.UseWarCry && warCrySkill is not null && warCry.IsReady &&
                            hero.Mana >= warCry.ManaCost),
                        Candidate(SkillIds.SeismicCharge, charge is not null && tick >= chargeReadyTick &&
                            SkillTarget(SkillIds.SeismicCharge) is not null &&
                            SkillDistance(SkillIds.SeismicCharge) > (long)HeavyStrikeRange * HeavyStrikeRange &&
                            SkillDistance(SkillIds.SeismicCharge) <= (long)charge.RangeRaw * charge.RangeRaw && CanPay(hero, charge)),
                        Candidate(SkillIds.BloodTideSpin, spin is not null && tick >= spinReadyTick &&
                            SkillTarget(SkillIds.BloodTideSpin) is not null && NearbyCount(SkillIds.BloodTideSpin, spin.RangeRaw) >= 2 && CanPay(hero, spin)),
                        Candidate(SkillIds.EarthCleave, cleave is not null && tick >= cleaveReadyTick &&
                            SkillTarget(SkillIds.EarthCleave) is not null && ConeCount(SkillIds.EarthCleave, cleave.RangeRaw) >= 2 && CanPay(hero, cleave)),
                        Candidate(SkillIds.SpiritBlade, blade is not null && tick >= bladeReadyTick &&
                            SkillTarget(SkillIds.SpiritBlade) is not null && SkillDistance(SkillIds.SpiritBlade) <= (long)blade.RangeRaw * blade.RangeRaw && CanPay(hero, blade)),
                        Candidate(SkillIds.AshJavelin, ashJavelin is not null && tick >= ashJavelinReadyTick &&
                            SkillTarget(SkillIds.AshJavelin) is not null && SkillDistance(SkillIds.AshJavelin) <= (long)ashJavelin.RangeRaw * ashJavelin.RangeRaw && CanPay(hero, ashJavelin)),
                        Candidate(SkillIds.EmberNova, emberNova is not null && tick >= emberNovaReadyTick &&
                            SkillTarget(SkillIds.EmberNova) is not null && NearbyCount(SkillIds.EmberNova, emberNova.RangeRaw) >= 2 && CanPay(hero, emberNova)),
                        Candidate(SkillIds.StormBrand, stormBrand is not null && tick >= stormBrandReadyTick &&
                            SkillTarget(SkillIds.StormBrand) is not null && SkillDistance(SkillIds.StormBrand) <= (long)stormBrand.RangeRaw * stormBrand.RangeRaw && CanPay(hero, stormBrand)),
                        Candidate(SkillIds.HeavyStrike, skills.ContainsKey(SkillIds.HeavyStrike) &&
                            SkillTarget(SkillIds.HeavyStrike) is not null && SkillDistance(SkillIds.HeavyStrike) <= (long)heavyStrike.RangeRaw * heavyStrike.RangeRaw &&
                            (heavyStrike.LifeCost > 0 ? hero.Life > heavyStrike.LifeCost : hero.Mana >= heavyStrike.ManaCost)),
                    }
                    .Where(candidate => candidate is not null && AiMatches(skills[candidate], request, hero,
                        SkillTarget(candidate)!, enemies, SkillDistance(candidate)))
                    .OrderBy(candidate => skills[candidate!].Priority)
                    .FirstOrDefault();
                string? skillCatalogChosen = skillCatalogSkills.Values
                    .Where(skill => army.CanUse(skill.SkillId) && skill.Role is not SkillRole.Reservation and not SkillRole.Counter &&
                                    (!skill.RequiresShield || request.Build.HasShield) && tick >= skillCatalogReadyTicks[skill.SkillId] &&
                                    SkillTarget(skill.SkillId) is not null &&
                                    (skill.Shape == SkillShape.Self ||
                                     SkillDistance(skill.SkillId) <= (long)skill.RangeRaw * skill.RangeRaw) && CanPay(hero, skill) &&
                                    AiMatches(skills[skill.SkillId], request, hero, SkillTarget(skill.SkillId)!, enemies, SkillDistance(skill.SkillId)))
                    .OrderBy(skill => skills[skill.SkillId].Priority)
                    .Select(skill => skill.SkillId)
                    .FirstOrDefault();
                if (skillCatalogChosen is not null && (chosen is null || skills[skillCatalogChosen].Priority < skills[chosen].Priority))
                    chosen = skillCatalogChosen;

                if (chosen is not null)
                {
                    target = SkillTarget(chosen)!;
                    heroTargetId = target.EntityId;
                }
                long distance = Point.DistanceSquared(heroPosition, target.Position);
                int cleaveRange = cleave is null ? 0 : cleave.RangeRaw *
                    (ascendancyRuntime.MarchReady && ascendancy.Has(WarriorNodeIds.BreakerMarchCore) ? 15_000 : 10_000) / 10_000;
                EnemyUnit[] cleaveTargets = cleave is null ? [] : enemies.Where(enemy => enemy.Life > 0 &&
                    InCleaveCone(heroPosition, target.Position, enemy.Position, cleaveRange)).ToArray();
                EnemyUnit[] spinTargets = spin is null ? [] : enemies.Where(enemy => enemy.Life > 0 &&
                    InRange(heroPosition, enemy.Position, spin.RangeRaw)).ToArray();

                if (chosen is null)
                {
                    ResolvedSkill? blocked = new[] { charge, spin, cleave, blade, ashJavelin, emberNova, stormBrand, heavyResolved }
                        .Where(skill => skill is not null && distance <= (long)skill.RangeRaw * skill.RangeRaw && !CanPay(hero, skill))
                        .OrderBy(skill => skills[skill!.SkillId].Priority)
                        .FirstOrDefault();
                    if (blocked is not null && AiMatches(skills[blocked.SkillId], request, hero, target, enemies, distance))
                    {
                        string resource = blocked.LifeCost > 0 ? "life" : "mana";
                        events.Add(Event(tick, SpatialEventKind.SkillFailed, "hero", target.EntityId, 0,
                            heroPosition, target.Position, $"{blocked.SkillId}|{resource}"));
                    }
                }

                if (chosen == SkillIds.WarCry && warCry.TryActivate(hero, tick))
                {
                    equipment.BeginAction(SkillIds.WarCry, 0, warCry.ManaCost, false, virtueVice);
                    equipment.Warcry(tick, enemies.Where(enemy => enemy.Life > 0 && InRange(heroPosition, enemy.Position, warCrySkill!.RangeRaw)).Select(enemy => enemy.EntityId), virtueVice);
                    ascendancyRuntime.WarCry();
                    events.Add(Event(tick, SpatialEventKind.WarCry, "hero", target.EntityId, 0,
                        heroPosition, target.Position, "area:6000"));
                    heroNextActionTick = tick + ActionDelay(request.Build, SkillDefinitions.WarCry.CastTimeTicks, SkillTag.WarCry);
                    if (ascendancy.Has(WarriorNodeIds.BreakerWarCryCore)) heroNextActionTick = tick;
                }
                else
                {
                    if (chosen is not null && skillCatalogSkills.TryGetValue(chosen, out ResolvedSkill? skillCatalogSkill) &&
                        TryPayEquipmentCost(request, hero, skillCatalogSkill))
                    {
                        SkillTag skillCatalogTags = SkillDefinitions.Get(skillCatalogSkill.SkillId).Tags;
                        int useCount = 1;
                        if (skillCatalogTags.HasFlag(SkillTag.Attack))
                        {
                            int frequency = CombatSkillRules.ActionFrequencyMilliPerSecond(request.Build,
                                skillCatalogSkill.CastTimeTicks, skillCatalogSkill.CooldownTicks, skillCatalogTags);
                            int carry = skillCatalogAttackFrequencyCarry.GetValueOrDefault(skillCatalogSkill.SkillId);
                            useCount = CombatRules.AttacksForScheduledSimulationTick(frequency, ref carry);
                            skillCatalogAttackFrequencyCarry[skillCatalogSkill.SkillId] = carry;
                        }
                        for (int use = 0; use < useCount && target.Life > 0; use++)
                        {
                            if (use > 0 && !TryPayEquipmentCost(request, hero, skillCatalogSkill)) break;
                            if (!army.Execute(skills[chosen], heroPosition, enemies, random, tick, events, hero, target))
                            ExecuteConfiguredSkill(request, skillCatalogSkill, skills[chosen], target, enemies, hero, random, tick,
                                ref heroPosition, bannerMultiplier, events, ref guardUntilTick, ref guardReductionBasisPoints,
                                skillCatalogUseCounts, ref fortificationLayers, ref fortificationUntilTick,
                                ref lastSelfAttackOrSpellTick, projectiles);
                        }
                        if (skillCatalogSkill.Role == SkillRole.Guard && ascendancy.Has(WarriorNodeIds.BastionGuardSmall))
                            guardUntilTick += Math.Max(1, (guardUntilTick - tick) / 4);
                        skillCatalogReadyTicks[chosen] = tick + Math.Max(1, skillCatalogSkill.CooldownTicks);
                        heroNextActionTick = tick + ActionDelay(request.Build, skillCatalogSkill.CastTimeTicks,
                            SkillDefinitions.Get(skillCatalogSkill.SkillId).Tags);
                    }
                    else if (chosen == SkillIds.SeismicCharge && TryPayEquipmentCost(request, hero, charge!))
                    {
                        ResolvedSkill chargeSkill = charge!;
                        Point beforeCharge = heroPosition;
                        heroPosition = Point.MoveToward(heroPosition, target.Position, Math.Max(1, chargeSkill.RangeRaw - 900));
                        ascendancyRuntime.Moved((int)Math.Sqrt(Point.DistanceSquared(beforeCharge, heroPosition)));
                        if (beforeCharge != heroPosition) equipment.UsedMovementSkill(tick);
                        foreach (EnemyUnit enemy in enemies.Where(enemy => enemy.Life > 0 && InRange(heroPosition, enemy.Position, 1_800)))
                        {
                            int multiplier = bannerMultiplier;
                            ApplyHeroHit(request, enemy, random, tick, multiplier, SpatialEventKind.SeismicCharge,
                                heroPosition, events, chargeSkill.BleedChanceBasisPoints, hero, chargeSkill.LifeLeechBasisPoints);
                        }
                        events.Add(Event(tick, SpatialEventKind.SeismicCharge, "hero", target.EntityId, 0,
                            heroPosition, target.Position, "movement"));
                        chargeReadyTick = tick + chargeSkill.CooldownTicks;
                        heroNextActionTick = tick + ActionDelay(request.Build, chargeSkill.CastTimeTicks,
                            SkillDefinitions.Get(chargeSkill.SkillId).Tags);
                    }
                    else if (chosen == SkillIds.BloodTideSpin && TryPayEquipmentCost(request, hero, spin!))
                    {
                        ResolvedSkill spinSkill = spin!;
                        foreach (EnemyUnit enemy in spinTargets)
                        {
                            ApplyHeroHit(request, enemy, random, tick, bannerMultiplier * 8_000 / 10_000,
                                SpatialEventKind.BloodTideSpin, heroPosition, events,
                                checked(3_500 + spinSkill.BleedChanceBasisPoints), hero, spinSkill.LifeLeechBasisPoints);
                        }
                        spinReadyTick = tick + spinSkill.CooldownTicks;
                        heroNextActionTick = tick + ActionDelay(request.Build, spinSkill.CastTimeTicks,
                            SkillDefinitions.Get(spinSkill.SkillId).Tags);
                    }
                    else if (chosen == SkillIds.EarthCleave && TryPayEquipmentCost(request, hero, cleave!))
                    {
                        ResolvedSkill cleaveSkill = cleave!;
                        foreach (EnemyUnit enemy in cleaveTargets)
                        {
                            int lifeBefore = enemy.Life;
                            ApplyHeroHit(request, enemy, random, tick, bannerMultiplier * 8_000 / 10_000,
                                SpatialEventKind.EarthCleave, heroPosition, events,
                                checked(request.Build.IncreasedBleedChanceBasisPoints / 2 + cleaveSkill.BleedChanceBasisPoints),
                                hero, cleaveSkill.LifeLeechBasisPoints);
                            if (ascendancy.Has(WarriorNodeIds.BreakerAftershockCore) && lifeBefore > enemy.Life)
                                aftershocks.Add(new PendingAftershock(tick + 10, enemy.EntityId, lifeBefore - enemy.Life, heroPosition));
                        }

                        cleaveReadyTick = tick + cleaveSkill.CooldownTicks;
                        heroNextActionTick = tick + ActionDelay(request.Build, cleaveSkill.CastTimeTicks,
                            SkillDefinitions.Get(cleaveSkill.SkillId).Tags);
                    }
                    else if (chosen == SkillIds.SpiritBlade && TryPayEquipmentCost(request, hero, blade!))
                    {
                        ResolvedSkill bladeSkill = blade!;
                        LaunchProjectiles(request, bladeSkill, skills[chosen], target, enemies, heroPosition,
                            bannerMultiplier * 9_000 / 10_000, tick, projectiles);
                        events.Add(Event(tick, SpatialEventKind.SpiritBladeLaunched, "hero", target.EntityId, 0,
                            heroPosition, target.Position, $"projectile:{bladeSkill.ProjectileCount}"));
                        bladeReadyTick = tick + bladeSkill.CooldownTicks;
                        heroNextActionTick = tick + ActionDelay(request.Build, bladeSkill.CastTimeTicks,
                            SkillDefinitions.Get(bladeSkill.SkillId).Tags);
                    }
                    else if (chosen == SkillIds.AshJavelin && TryPayEquipmentCost(request, hero, ashJavelin!))
                    {
                        ResolvedSkill skill = ashJavelin!;
                        ApplyHeroHit(request, target, random, tick, bannerMultiplier,
                            SpatialEventKind.AshJavelin, heroPosition, events, skill.BleedChanceBasisPoints, hero,
                            skill.LifeLeechBasisPoints);
                        ashJavelinReadyTick = tick + skill.CooldownTicks;
                        heroNextActionTick = tick + ActionDelay(request.Build, skill.CastTimeTicks,
                            SkillDefinitions.Get(skill.SkillId).Tags);
                    }
                    else if (chosen == SkillIds.EmberNova && TryPayEquipmentCost(request, hero, emberNova!))
                    {
                        ResolvedSkill skill = emberNova!;
                        foreach (EnemyUnit enemy in enemies.Where(enemy => enemy.Life > 0 && InRange(heroPosition, enemy.Position, skill.RangeRaw)))
                            ApplyHeroHit(request, enemy, random, tick, bannerMultiplier,
                                SpatialEventKind.EmberNova, heroPosition, events, 0, hero, skill.LifeLeechBasisPoints);
                        emberNovaReadyTick = tick + skill.CooldownTicks;
                        heroNextActionTick = tick + ActionDelay(request.Build, skill.CastTimeTicks,
                            SkillDefinitions.Get(skill.SkillId).Tags);
                    }
                    else if (chosen == SkillIds.StormBrand && TryPayEquipmentCost(request, hero, stormBrand!))
                    {
                        ResolvedSkill skill = stormBrand!;
                        EnemyUnit[] marked = enemies.Where(enemy => enemy.Life > 0)
                            .OrderBy(enemy => Point.DistanceSquared(target.Position, enemy.Position))
                            .Take(Math.Max(2, skill.MaximumChains + 1)).ToArray();
                        foreach (EnemyUnit enemy in marked)
                            ApplyHeroHit(request, enemy, random, tick, bannerMultiplier * 8_500 / 10_000,
                                SpatialEventKind.StormBrand, heroPosition, events, 0, hero, skill.LifeLeechBasisPoints);
                        stormBrandReadyTick = tick + skill.CooldownTicks;
                        heroNextActionTick = tick + ActionDelay(request.Build, skill.CastTimeTicks,
                            SkillDefinitions.Get(skill.SkillId).Tags);
                    }
                    else if (chosen == SkillIds.HeavyStrike &&
                             TryPayEquipmentCost(request, hero, heavyStrike))
                    {
                        int speedBasisPoints = Math.Max(1_000, 10_000 + ascendancyRuntime.AttackSpeedBasisPoints +
                            request.Build.IncreasedActionSpeedBasisPoints +
                            virtueVice.Bonuses().IncreasedActionSpeedBasisPoints);
                        int masterySpeed = MasteryRuntime.ActionSpeedMultiplier(
                            request.Build.PassiveProfile ?? PassiveModifiers.Empty,
                            SkillDefinitions.HeavyStrike.Tags, request.Build.Weapon);
                        int frequency = CombatRules.AttackFrequencyMilliPerSecond(
                            heavyStrike.UncappedAttackFrequencyMilliPerSecond,
                            speedBasisPoints - 10_000,
                            [masterySpeed]);
                        int attackCount = CombatRules.AttacksForScheduledSimulationTick(frequency,
                            ref heavyStrikeFrequencyCarry);
                        for (int attack = 0; attack < attackCount && target.Life > 0; attack++)
                        {
                            if (attack > 0 && !TryPayEquipmentCost(request, hero, heavyStrike)) break;
                            int warCryMultiplier = checked(warCry.ConsumeHeavyStrikeMultiplier(tick) * bannerMultiplier / 10_000);
                            int lifeBeforeHit = target.Life;
                            ApplyHeroHit(request, target, random, tick, warCryMultiplier,
                                SpatialEventKind.HeavyStrike, heroPosition, events,
                                checked(request.Build.IncreasedBleedChanceBasisPoints + heavyStrike.BleedChanceBasisPoints), hero,
                                ascendancy.Has(WarriorNodeIds.BloodTideSmall) ? 100 : 0);
                            if (equipment.Has("回响破誓者") && target.Life < lifeBeforeHit)
                                aftershocks.Add(new PendingAftershock(tick + 7, target.EntityId,
                                    ScaleCombatValue(lifeBeforeHit - target.Life, 7_000), heroPosition, "equipment:回响破誓者"));
                        }
                        int attackInterval = Math.Max(1, checked((20_000 + frequency - 1) / frequency));
                        heroNextActionTick = tick + attackInterval;
                    }
                    else
                    {
                        if (distance > 36_000_000)
                            UseFlask(FlaskKind.Movement, tick);
                        int flaskSpeed = 10_000 + flasks.UtilityEffect(FlaskKind.Movement);
                        int speed = Math.Max(1, checked((int)((long)HeroEntityRawSpeed * request.Build.MovementSpeedBasisPoints / 10_000 * flaskSpeed / 10_000 / 20)));
                        Point next = Point.MoveToward(heroPosition, target.Position, speed);
                        if (next != heroPosition)
                        {
                            ascendancyRuntime.Moved((int)Math.Sqrt(Point.DistanceSquared(heroPosition, next)));
                            heroPosition = next;
                            if ((tick & 3) == 0)
                            {
                                events.Add(Event(tick, SpatialEventKind.HeroMoved, "hero", target.EntityId, 0,
                                    heroPosition, target.Position, "move"));
                            }
                        }
                    }
                }
            }

            army.Advance(enemies, heroPosition, random, tick, events);
            if (RechargeFlasksForKills(enemies, flasks, tick, heroPosition, events, ascendancyRuntime, hero, equipment))
                chargeReadyTick = 0;
            if (tick < rootedUntilTick) heroPosition = beforeMovement;
            equipment.Advance(tick, hero, heroPosition != beforeMovement);
            ResolveEnemies(request, enemies, hero, heroPosition, random, tick, events, flasks,
                guardUntilTick, guardReductionBasisPoints, shieldCounter, shieldCounterConfiguration,
                ref shieldCounterReadyTick, ascendancyRuntime, hazards, ref rootedUntilTick, fortificationLayers, army);
            foreach (EnemyHazard hazard in hazards.Where(h => h.Expires > tick && tick >= h.Start && (tick - h.Start) % 10 == 0))
            {
                army.ReceiveHazard(hazard, tick, events);
                int damage = InRange(heroPosition, hazard.Position, hazard.Radius) ? hazard.Damage : 0;
                if (damage > 0)
                {
                    damage = equipment.MitigateDamageOverTime(hero.Sheet, damage, hazard.DamageType, tick, 2);
                }
                if (damage > 0)
                {
                    damage = equipment.ApplyEnemyDamage(hero, damage, false, tick, virtueVice);
                }
                events.Add(Event(tick, SpatialEventKind.EnemyAttack, hazard.Source, "hero", damage,
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
                projectedOutcome = BattleOutcome.Draw;
                break;
            }

            if (request.MaximumTicks > 0 || (tick & 3) == 0)
                CaptureFrame(frames, tick * TickMilliseconds, request.NodeIndex, heroPosition, hero, heroTargetId, enemies,
                    request.Build.PartySize, request.Build.FrontlineCount, request.VirtueVice, tick, army, request.Actions);
        }

        bool victory = enemies.All(enemy => enemy.Life <= 0);
        BattleOutcome outcome = projectedOutcome ?? (victory
            ? BattleOutcome.HeroVictory
            : hero.IsAlive ? BattleOutcome.Timeout : BattleOutcome.EnemyVictory);
        events.Add(Event(tick, victory ? SpatialEventKind.NodeCleared : SpatialEventKind.HeroDefeated,
            victory ? "hero" : "enemies", string.Empty, 0, heroPosition, heroPosition, outcome.ToString()));
        CaptureFrame(frames, tick * TickMilliseconds, request.NodeIndex, heroPosition, hero, heroTargetId, enemies,
            request.Build.PartySize, request.Build.FrontlineCount, request.VirtueVice, tick, army, request.Actions);
        string hash = Hash(seed, outcome, tick, hero, enemies, events);
        equipment.EndEncounter();
        flasks.EndEncounter();
        return new NodeCombatResult(outcome, tick, hero.Life, hero.Mana, hero.Shield, frames, events, hash);
    }

    private static List<EnemyUnit> CreateEnemies(NodeCombatRequest request, Pcg32 random)
    {
        var result = new List<EnemyUnit>(request.EnemyCount);
        IReadOnlyList<EnemyProfile> pool = request.EnemyPool ?? Enemies.ForEncounter(request.AreaLevel, request.EncounterFamily);
        // Monsters intentionally permits extreme packs: one repeated monster, or an entire pack
        // drawn from a single combat role. It does not synthesize a front/back/support template.
        IReadOnlyList<EnemyProfile> packPool = request.EnemyPool ?? MonsterCatalog.SelectPackPool(pool, random);
        int magicCount = request.AreaLevel >= 8 && request.EnemyCount >= 6 ? Math.Clamp(request.EnemyCount / 4, 2, 6) : 0;
        int firstNonBoss = request.HasBoss ? request.BossCount : 0;
        for (int index = 0; index < request.EnemyCount; index++)
        {
            bool boss = request.HasBoss && index < request.BossCount;
            EnemyRarity rarity = boss
                ? EnemyRarity.Boss
                : (request.HasElite && index == firstNonBoss || index >= firstNonBoss && index < firstNonBoss + request.AdditionalRareEnemies)
                    ? EnemyRarity.Rare
                    : index >= (request.HasElite ? 1 : 0) && index < magicCount + (request.HasElite ? 1 : 0)
                        ? EnemyRarity.Magic
                        : EnemyRarity.Normal;
            bool elite = rarity is EnemyRarity.Magic or EnemyRarity.Rare;
            EnemyProfile profile = boss
                ? string.IsNullOrEmpty(request.BossStableId) || request.BossStableId == Enemies.AbyssWarden.StableId ? Enemies.AbyssWarden :
                    Enemies.NormalEnemies.FirstOrDefault(e => e.StableId == request.BossStableId) ?? Bosses.CombatProfile(request.BossStableId)
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
            UnitRole role = boss ? UnitRole.Boss : profile.Role switch
            {
                EnemyRole.Ranged => UnitRole.Ranged,
                EnemyRole.Caster => UnitRole.Caster,
                EnemyRole.Charger => UnitRole.Charger,
                EnemyRole.Summoner => UnitRole.Summoner,
                EnemyRole.Support => UnitRole.Summoner,
                _ => UnitRole.Melee,
            };
            Point position = SpawnPosition(request.Formation, index, request.EnemyCount, random);
            result.Add(new EnemyUnit(
                $"enemy-{request.NodeIndex}-{index}", profile, scaled, role, rarity, elite, boss, life, position, index * 3));
        }

        return result;
    }

    private static Point SpawnPosition(int formation, int index, int count, Pcg32 random)
    {
        int jitterX = (int)(random.NextUInt() % 401) - 200;
        int jitterY = (int)(random.NextUInt() % 401) - 200;
        return (formation % 3) switch
        {
            0 => new Point(1_500 + index % 6 * 1_800 + jitterX, 2_500 + index / 6 * 1_600 + jitterY),
            1 => new Point(index % 2 == 0 ? 1_000 + jitterX : 11_000 + jitterX,
                3_000 + index % 8 * 1_900 + jitterY),
            _ => RingPosition(index, count, jitterX, jitterY),
        };
    }

    private static Point RingPosition(int index, int count, int jitterX, int jitterY)
    {
        double angle = index * Math.PI * 2 / Math.Max(1, count);
        return new Point(
            Math.Clamp(6_000 + (int)(Math.Cos(angle) * 5_000) + jitterX, 500, 11_500),
            Math.Clamp(11_000 + (int)(Math.Sin(angle) * 8_000) + jitterY, 500, 19_500));
    }

    private static EnemyUnit? SelectTarget(IEnumerable<EnemyUnit> enemies, Point heroPosition) => enemies
        .Where(enemy => enemy.Life > 0)
        .OrderByDescending(enemy => enemy.Boss)
        .ThenByDescending(enemy => enemy.Elite)
        .ThenByDescending(enemy => enemy.Scaled.Base.ThreatPoints)
        .ThenBy(enemy => Point.DistanceSquared(heroPosition, enemy.Position))
        .ThenBy(enemy => enemy.Life)
        .FirstOrDefault();

    private static EnemyUnit? SelectTarget(
        IEnumerable<EnemyUnit> enemies,
        Point heroPosition,
        SkillTargetPolicy policy) => enemies
        .Where(enemy => enemy.Life > 0 && policy switch
        {
            SkillTargetPolicy.BossOnly => enemy.Boss,
            SkillTargetPolicy.EliteAndBoss => enemy.Elite || enemy.Boss,
            _ => true,
        })
        .OrderBy(enemy => Point.DistanceSquared(heroPosition, enemy.Position))
        .ThenByDescending(enemy => enemy.Boss)
        .ThenByDescending(enemy => enemy.Elite)
        .ThenBy(enemy => enemy.EntityId, StringComparer.Ordinal)
        .FirstOrDefault();

    private static void ExecuteConfiguredSkill(
        NodeCombatRequest request,
        ResolvedSkill skill,
        SkillConfiguration configuration,
        EnemyUnit target,
        IReadOnlyCollection<EnemyUnit> enemies,
        ResourceState hero,
        Pcg32 random,
        int tick,
        ref Point heroPosition,
        int bannerMultiplier,
        ICollection<SpatialEvent> events,
        ref int guardUntilTick,
        ref int guardReductionBasisPoints,
        IDictionary<string, int> useCounts,
        ref int fortificationLayers,
        ref int fortificationUntilTick,
        ref int lastSelfAttackOrSpellTick, IList<PendingProjectile> projectiles)
    {
        if (ApplyCurse(request, skill, configuration, target, enemies, heroPosition, tick, events)) return;
        if (skill.Role == SkillRole.Movement)
        {
            Point beforeMove = heroPosition;
            heroPosition = Point.MoveToward(heroPosition, target.Position, Math.Max(1, skill.RangeRaw - 900));
            request.AscendancyRuntime?.Moved((int)Math.Sqrt(Point.DistanceSquared(beforeMove, heroPosition)));
            if (beforeMove != heroPosition) request.EquipmentRuntime?.UsedMovementSkill(tick);
            events.Add(Event(tick, SpatialEventKind.HeroMoved, "hero", target.EntityId, 0,
                heroPosition, target.Position, $"skill:{skill.SkillId}"));
        }

        if (skill.Role == SkillRole.Guard || skill.SkillId == SkillIds.DefiantCry)
        {
            guardUntilTick = tick + (skill.SkillId == SkillIds.PrismaticGuard ? 80 : 60);
            guardReductionBasisPoints = skill.SkillId == SkillIds.IronGuard ? 4_000 :
                skill.SkillId == SkillIds.PrismaticGuard ? 2_500 : 2_000;
            if (skill.SkillId == SkillIds.DefiantCry)
                hero.HealLife(Math.Max(1, (hero.MaximumLife - hero.Life) / 10));
            events.Add(Event(tick, SpatialEventKind.Guard, "hero", "hero", guardReductionBasisPoints,
                heroPosition, heroPosition, $"skill:{skill.SkillId}|until:{guardUntilTick}"));
        }

        if (skill.SkillId == SkillIds.BreakerCry)
        {
            Point cryOrigin = heroPosition;
            foreach (EnemyUnit enemy in enemies.Where(item => item.Life > 0 && InRange(cryOrigin, item.Position, skill.RangeRaw)))
                enemy.ArmorBreakStacks = Math.Min(request.AscendancyRuntime?.ArmorBreakMaximum ?? 5, enemy.ArmorBreakStacks + 2);
            events.Add(Event(tick, SpatialEventKind.SkillEffect, "hero", target.EntityId, 0,
                heroPosition, target.Position, $"skill:{skill.SkillId}|armor-break:2"));
            return;
        }
        if (skill.DamageType == SkillDamageType.None) return;

        Point origin = heroPosition;
        if (request.EquipmentRuntime is { ExtraActionChains: > 0 } actionEquipment)
            skill = skill with { MaximumChains = skill.MaximumChains + actionEquipment.ExtraActionChains };
        EnemyUnit[] affected = skill.Shape switch
        {
            SkillShape.Circle or SkillShape.MovementCircle or SkillShape.GroundArea => enemies
                .Where(enemy => enemy.Life > 0 && InRange(origin, enemy.Position, skill.RangeRaw)).ToArray(),
            SkillShape.Cone => enemies.Where(enemy => enemy.Life > 0 &&
                InCleaveCone(origin, target.Position, enemy.Position, skill.RangeRaw)).ToArray(),
            SkillShape.Chain => enemies.Where(enemy => enemy.Life > 0)
                .OrderBy(enemy => Point.DistanceSquared(target.Position, enemy.Position))
                .Take(Math.Max(1, skill.MaximumChains + 1)).ToArray(),
            SkillShape.Projectile => enemies.Where(enemy => enemy.Life > 0)
                .OrderBy(enemy => Point.DistanceSquared(origin, enemy.Position))
                .Take(Math.Max(1, skill.ProjectileCount + skill.PierceCount + skill.ForkCount)).ToArray(),
            _ => [target],
        };
        if (affected.Length == 0) affected = [target];

        int useCount = useCounts[skill.SkillId] = useCounts[skill.SkillId] + 1;
        PassiveModifiers passive = request.Build.PassiveProfile ?? PassiveModifiers.Empty;
        SkillTag skillTags = SkillDefinitions.Get(skill.SkillId).Tags;
        if (skillTags.HasFlag(SkillTag.WarCry))
            request.EquipmentRuntime?.Warcry(tick, affected.Select(enemy => enemy.EntityId), request.VirtueVice);
        bool empoweredArmorBreak = tick - lastSelfAttackOrSpellTick >= 20;
        if (skillTags.HasFlag(SkillTag.Attack) || skillTags.HasFlag(SkillTag.Spell))
            lastSelfAttackOrSpellTick = tick;
        if (configuration.Supports.HasFlag(SkillSupport.Trauma))
        {
            int traumaStacks = Math.Min(10, useCount);
            hero.ApplyDamage(Math.Max(1, hero.MaximumLife * traumaStacks / 1_000), tick);
            bannerMultiplier = checked(bannerMultiplier * (10_000 + traumaStacks * 500) / 10_000);
        }
        if (configuration.Supports.HasFlag(SkillSupport.TripleImpact) && useCount % 3 == 0)
            bannerMultiplier = checked(bannerMultiplier * 18_000 / 10_000);

        if (skill.Shape is SkillShape.Projectile or SkillShape.Chain)
        {
            LaunchProjectiles(request, skill, configuration, target, enemies, heroPosition, bannerMultiplier, tick, projectiles);
            return;
        }

        if (skill.SkillId is "archetypes.skill.phantom_step" or "archetypes.skill.hundred_shadows")
        {
            bool enhanced = request.Build.Ascendancy?.Has("core.ascendancy.phantom_master.copy.core") == true;
            int ratio = enhanced ? 5_000 : 3_000;
            if (skill.SkillId == "archetypes.skill.phantom_step")
                request.Actions!.SpawnPhantom(heroPosition, tick, 80 + configuration.Quality * 80 / 100,
                    (request.Build.Ascendancy?.Has("core.ascendancy.phantom_master.spawn.core") == true ? 4 : 2) +
                    (request.Build.CombatEquipment?.Value(ItemModifierKind.AdditionalPhantomMaximum) ?? 0), ratio);
            else request.Actions!.CommandPhantoms(tick, ratio);
            events.Add(Event(tick, SpatialEventKind.SkillEffect, "hero", "", 0, heroPosition, heroPosition, $"skill:{skill.SkillId}|phantoms:{request.Actions!.PhantomFrames(tick).Count}"));
            return;
        }
        foreach (EnemyUnit enemy in affected)
        {
            int burstDamage = 0;
            if (skill.SkillId == SkillIds.BloodBurst && enemy.BleedRemaining > 0)
            {
                burstDamage = (int)Math.Min(int.MaxValue, enemy.Ailments.Consume(Ailment.Bleed, 6_500));
            }
            ResolveHeroHit(request, skill, configuration, enemy, hero, random, tick, heroPosition,
                bannerMultiplier, events,
                MasteryRuntime.Has(passive, "破甲_物理穿透", 1) && skillTags.HasFlag(SkillTag.Physical)
                    ? empoweredArmorBreak ? 5 : 2
                    : 0);
            if (burstDamage > 0 && enemy.Life > 0)
            {
                int armor = enemy.Scaled.Armor * Math.Max(0, 10_000 - enemy.ArmorBreakStacks * 800) / 10_000;
                DamageBreakdown burst = DamagePacketRules.Resolve(burstDamage, SkillDamageType.Physical,
                    SkillSupport.None, armor, 0, 0, 0, 0);
                enemy.Life = Math.Max(0, enemy.Life - burst.Total);
                events.Add(Event(tick, SpatialEventKind.SkillEffect, "hero", enemy.EntityId, burst.Total,
                    heroPosition, enemy.Position, $"skill:{skill.SkillId}|blood-burst|damage:{burst.Compact}|supports:{(ulong)configuration.Supports}"));
                if (enemy.Life == 0)
                    events.Add(Event(tick, SpatialEventKind.EnemyDefeated, "hero", enemy.EntityId, 0,
                        heroPosition, enemy.Position, enemy.Profile.StableId));
            }
        }
        if (configuration.Supports.HasFlag(SkillSupport.Fortification) &&
            skillTags.HasFlag(SkillTag.Attack) && skillTags.HasFlag(SkillTag.Melee))
        {
            fortificationLayers = Math.Min(MasteryRuntime.FortificationMaximum(passive),
                fortificationLayers + affected.Length);
            fortificationUntilTick = tick + 80;
        }
    }

    private static ResolvedHeroHit? ResolveHeroHit(
        NodeCombatRequest request,
        ResolvedSkill skill,
        SkillConfiguration configuration,
        EnemyUnit enemy,
        ResourceState hero,
        Pcg32 random,
        int tick,
        Point source,
        int multiplier,
        ICollection<SpatialEvent> events,
        int masteryArmorBreakStacks = 0,
        int additionalIncreasedBasisPoints = 0,
        SpatialEventKind eventKind = SpatialEventKind.SkillEffect,
        int chainIndex = 0)
    {
        CombatRuntime runtime = request.AscendancyRuntime ?? new CombatRuntime(CombatProfile.Empty);
        SkillTag tags = SkillDefinitions.Get(skill.SkillId).Tags;
        EquipmentCombatRuntime? equipment = request.EquipmentRuntime;
        request.Actions?.Begin(equipment!.ActionId, skill, request.Build, tick, equipment.CaptureAction().Triggered);
        bool hunted = enemy.Rarity is EnemyRarity.Rare or EnemyRarity.Boss && request.Auras?.HunterAlwaysHits == true;
        if (tags.HasFlag(SkillTag.Attack) && !request.Build.AlwaysHit && !hunted && random.NextBasisPoints() >=
            DamageRules.HitChance(request.Build.Sheet.Accuracy(request.Build.FlatAccuracy).Value, enemy.Scaled.Evasion, false).Value)
        {
            events.Add(Event(tick, eventKind, "hero", enemy.EntityId, 0, source, enemy.Position, $"skill:{skill.SkillId}|miss"));
            return null;
        }
        int distanceRaw = (int)Math.Sqrt(Point.DistanceSquared(source, enemy.Position));
        multiplier = ScaleCombatValue(multiplier, equipment?.HitMultiplier(request.Build, hero, tags, enemy.EntityId,
            enemy.Life, enemy.MaximumLife, enemy.Rarity is EnemyRarity.Rare or EnemyRarity.Boss, enemy.Boss, enemy.BleedRemaining > 0,
            distanceRaw, equipment.NearbyEnemyCount?.Invoke() ?? 0, tick, chainIndex) ?? 10_000);
        if (runtime.Has(WarriorNodeIds.BloodLifeCore) && tags.HasFlag(SkillTag.Attack)) runtime.PaidLife(tick);
        int ascendancyMultiplier = runtime.ConsumeAttackMultiplier(tags,
            hero.Life * 2L <= hero.MaximumLife, !request.Build.HasShield,
            new EnemyState(enemy.ArmorBreakStacks, tick < enemy.StunnedUntilTick));
        int weaponSpan = request.Build.Weapon.MaximumPhysicalDamage - request.Build.Weapon.MinimumPhysicalDamage + 1;
        int weaponRoll = request.Build.Weapon.MinimumPhysicalDamage + (int)(random.NextUInt() % (uint)Math.Max(1, weaponSpan));
        int raw = CombatSkillRules.BaseDamage(skill, tags, request.Build.Weapon,
            request.Build.AddedPhysicalDamage, weaponRoll);
        if (skill.Role == SkillRole.DamageOverTime && skill.Shape == SkillShape.GroundArea &&
            tags.HasFlag(SkillTag.Attack))
            raw = Math.Max(1, request.AreaLevel * 5 + weaponRoll / 2);
        PassiveModifiers profile = request.Build.PassiveProfile ?? PassiveModifiers.Empty;
        AddedWeaponDamage addedWeapon = tags.HasFlag(SkillTag.Attack) && skill.Role != SkillRole.DamageOverTime &&
                                           request.Build.LocalWeaponStats is { } localWeapon
            ? new(Roll(localWeapon.Fire), Roll(localWeapon.Cold), Roll(localWeapon.Lightning), Roll(localWeapon.Void))
            : default;
        int criticalChance = request.Build.CannotCrit ? 0 : CombatRules.CriticalChance(
            request.Build.Weapon.CriticalChanceBasisPoints + (equipment?.BaseCriticalBonus(tags, distanceRaw) ?? 0), request.Build.IncreasedCriticalChanceBasisPoints);
        bool critical = !request.Build.CannotCrit && skill.Role != SkillRole.DamageOverTime && !MasteryRuntime.CannotCrit(profile) &&
                        (equipment?.ForceCritical(tags) == true || random.NextUInt() % 10_000 < Math.Clamp(criticalChance, 0, 10_000));
        int criticalMultiplier = critical ? ScaleCombatValue(request.Build.CriticalMultiplierBasisPoints +
            (hunted ? request.Auras?.HunterCriticalMultiplier ?? 0 : 0), equipment?.ForceCritical(tags) == true ? 15_000 : 10_000) : 10_000;
        if (critical && request.VirtueVice is { } criticalVirtues)
            criticalMultiplier = ScaleCombatValue(criticalMultiplier, 10_000 + criticalVirtues.Bonuses().MoreCriticalDamageBasisPoints);
        int armor = CombatRules.ArmorAfterBreak(enemy.Scaled.Armor, enemy.ArmorBreakStacks, runtime.ArmorBreakMaximum, request.Auras?.EnemyArmorReduction ?? 0);
        if (configuration.Supports.HasFlag(SkillSupport.ArmorPierce)) armor = armor * 7_000 / 10_000;
        var ailmentSource = new List<DamageBranch>();
        var offensiveBranches = new List<DamageBranch>();
        DamageBreakdown damage = DamagePacketRules.ResolveMixed(raw, skill.DamageType, addedWeapon, configuration.Supports,
            armor, EnemyResistance(enemy, request, SkillDamageType.Fire, tags.HasFlag(SkillTag.Spell) && skill.Role != SkillRole.DamageOverTime),
            EnemyResistance(enemy, request, SkillDamageType.Cold, tags.HasFlag(SkillTag.Spell) && skill.Role != SkillRole.DamageOverTime),
            EnemyResistance(enemy, request, SkillDamageType.Lightning, tags.HasFlag(SkillTag.Spell) && skill.Role != SkillRole.DamageOverTime),
            EnemyResistance(enemy, request, SkillDamageType.Void),
            enemy.Scaled.PhysicalResistanceBasisPoints + request.EnemyPhysicalReductionBasisPoints,
            equipment?.Loadout.Modifiers,
            CombatSkillRules.OffensiveIncreases(skill, configuration, request.Build, tags, additionalIncreasedBasisPoints),
            branch =>
            {
                if (request.Auras?.ExclusiveElement is { } allowed && branch.CurrentType is DamageType.Fire or DamageType.Cold or DamageType.Lightning && branch.CurrentType != allowed) return 0;
                int scaled = CombatSkillRules.ScaleOffensiveDamage(branch.BaseDamage, skill, configuration,
                    request.Build, tags, enemy.Life, enemy.MaximumLife, multiplier,
                    targetRareOrBoss: enemy.Rarity is EnemyRarity.Rare or EnemyRarity.Boss,
                    applyIncreased: false, damageHistory: branch.History);
                scaled = ScaleCombatValue(scaled, ascendancyMultiplier);
                scaled = ScaleCombatValue(scaled, criticalMultiplier);
                if (!configuration.Supports.HasFlag(SkillSupport.Brutality) || branch.CurrentType == DamageType.Physical)
                    offensiveBranches.Add(branch with { BaseDamage = scaled });
                scaled = ScaleCombatValue(scaled, 10_000 + enemy.ShockEffect);
                scaled = ScaleCombatValue(scaled, 10_000 + enemy.Curses.Effect("archetypes.skill.death_mark", tick));
                if (branch.CurrentType == DamageType.Void)
                    scaled = ScaleCombatValue(scaled, CombatRules.WitherMultiplier(enemy.Ailments.Stack(Ailment.Wither, tick)));
                return scaled;
            }, branches => ailmentSource.AddRange(branches));
        request.Actions?.Record(equipment!.ActionId, new(enemy.EntityId, source, skill, configuration, request.Build,
            new(0, 0, 0, 0, 0, offensiveBranches.ToArray(), []), ailmentSource.ToArray(), critical, criticalMultiplier), tick, equipment.CaptureAction().Triggered);
        ApplyHeroDamage(request, skill, configuration, enemy, hero, random, tick, source,
            damage, critical, events, masteryArmorBreakStacks, eventKind, ailmentSource);
        return new(damage, critical);
        int Roll(LocalDamageRange range)
        {
            if (!range.HasDamage) return 0;
            int span = range.Maximum - range.Minimum + 1;
            return range.Minimum + (int)(random.NextUInt() % (uint)Math.Max(1, span));
        }

    }

    private sealed record ResolvedHeroHit(DamageBreakdown Damage, bool Critical);

    private static void ApplyHeroDamage(NodeCombatRequest request, ResolvedSkill skill,
        SkillConfiguration configuration, EnemyUnit enemy, ResourceState hero, Pcg32 random,
        int tick, Point source, DamageBreakdown damage, bool critical, ICollection<SpatialEvent> events,
        int masteryArmorBreakStacks = 0, SpatialEventKind eventKind = SpatialEventKind.SkillEffect,
        IReadOnlyList<DamageBranch>? ailmentSource = null)
    {
        CombatRuntime runtime = request.AscendancyRuntime ?? new CombatRuntime(CombatProfile.Empty);
        SkillTag tags = SkillDefinitions.Get(skill.SkillId).Tags;
        EquipmentCombatRuntime? equipment = request.EquipmentRuntime;
        PassiveModifiers profile = request.Build.PassiveProfile ?? PassiveModifiers.Empty;
        int value = damage.Total;
        int beforeShieldLink = enemy.Life;
        enemy.Life = Math.Max(0, enemy.Life - value);
        value = beforeShieldLink - enemy.Life;
        if (value > 0 && damage.Physical > 0 && tags.HasFlag(SkillTag.Attack) && tags.HasFlag(SkillTag.Melee)) equipment?.PhysicalMeleeHit?.Invoke();
        if (value > 0 && equipment?.CaptureAction().Triggered != true && skill.Role != SkillRole.DamageOverTime &&
            enemy.Rarity is EnemyRarity.Rare or EnemyRarity.Boss && request.VirtueVice is { } oathState)
        {
            IReadOnlyList<VirtueViceKind> oaths = request.Build.VirtueViceLoadout?.Oaths ?? [];
            string action = equipment?.ActionId ?? $"{tick}:{skill.SkillId}";
            if (oaths.Contains(VirtueViceKind.Rage))
                oathState.TryOathChance(VirtueViceKind.Rage, action, 1_200, random.NextUInt());
            if (critical && oaths.Contains(VirtueViceKind.Arrogance))
                oathState.TryOathChance(VirtueViceKind.Arrogance, action, 1_200, random.NextUInt());
            if (oaths.Contains(VirtueViceKind.Sloth)) oathState.RecordSlothOathHit(action);
        }
        if (skill.Role != SkillRole.DamageOverTime)
        {
            int freeze = equipment?.OnHit(hero, tags, enemy.EntityId, enemy.Boss, critical, value, request.VirtueVice) ?? 0;
            if (value > 0 && tags.HasFlag(SkillTag.Attack) && tags.HasFlag(SkillTag.Projectile))
                equipment?.ProjectileHit(enemy.Rarity is EnemyRarity.Rare or EnemyRarity.Boss);
            enemy.NextActionTick = Math.Max(enemy.NextActionTick, tick + freeze);
        }
        int leech = skill.LifeLeechBasisPoints + MasteryRuntime.AdditionalLifeLeech(profile) +
            (runtime.Has(WarriorNodeIds.BloodTideSmall) && tags.HasFlag(SkillTag.Attack) &&
             skill.DamageType == SkillDamageType.Physical ? 100 : 0);
        if (value > 0 && leech > 0)
            ApplyLifeLeech(hero, Math.Max(1, value * leech / 10_000), request.Build.InstantLifeLeechBasisPoints);

        ApplyAilments(request, skill, configuration, enemy, ailmentSource ?? [], damage, critical, random, tick, source, events);
        if (value > 0 && masteryArmorBreakStacks > 0)
        {
            enemy.ArmorBreakStacks = Math.Min(runtime.ArmorBreakMaximum,
                enemy.ArmorBreakStacks + masteryArmorBreakStacks);
            enemy.ArmorBreakUntil = tick + 100;
        }
        events.Add(Event(tick, eventKind, "hero", enemy.EntityId, value,
            source, enemy.Position,
            $"skill:{skill.SkillId}|damage:{damage.Compact}|supports:{(ulong)configuration.Supports}{(critical ? "|critical" : string.Empty)}"));
        if (enemy.Life == 0)
            events.Add(Event(tick, SpatialEventKind.EnemyDefeated, "hero", enemy.EntityId, 0,
                source, enemy.Position, enemy.Profile.StableId));
    }

    private static void ResolveEnemies(
        NodeCombatRequest request,
        List<EnemyUnit> enemies,
        ResourceState hero,
        Point heroPosition,
        Pcg32 random,
        int tick,
        ICollection<SpatialEvent> events,
        Combat.FlaskRack flasks,
        int guardUntilTick,
        int guardReductionBasisPoints,
        ResolvedSkill? shieldCounter,
        SkillConfiguration? shieldCounterConfiguration,
        ref int shieldCounterReadyTick,
        CombatRuntime ascendancy, List<EnemyHazard> hazards, ref int rootedUntilTick,
        int fortificationLayers, BattleArmy army)
    {
        foreach (EnemyUnit enemy in enemies.Where(enemy => enemy.Life > 0).ToArray())
        {
            if (enemy.Life <= 0) continue; // An earlier counterattack may have killed this snapshot member.
            if (tick < enemy.FrozenUntil || tick < enemy.StunnedUntilTick) continue;
            EnemySkillProfile activeSkill = enemy.Profile.EffectiveSkills[enemy.ActionSequence % enemy.Profile.EffectiveSkills.Count];
            if (army.ReceiveEnemyAction(enemy, activeSkill, heroPosition, request, random, tick, events)) continue;
            BossDefinition? bossDefinition = enemy.Boss ? Bosses.TryGet(enemy.Profile.StableId) : null;
            if (bossDefinition is not null)
            {
                int phase = tick >= bossDefinition.EnrageSeconds * 20 ? 2 :
                    enemy.Life * 10_000L <= enemy.MaximumLife * bossDefinition.PhaseThresholdBasisPoints ? 1 : 0;
                if (phase != enemy.BossPhase)
                {
                    enemy.BossPhase = phase;
                    events.Add(Event(tick, SpatialEventKind.BossPhaseChanged, enemy.EntityId, "hero", phase,
                        enemy.Position, heroPosition, phase == 2 ? "enraged" : "phase_two"));
                }
            }
            int range = Math.Max(enemy.Profile.AttackRangeRaw, activeSkill.RangeRaw);
            if (activeSkill.Area)
                range = checked((int)((long)range * request.EnemyAreaBasisPoints / 10_000));
            long distance = Point.DistanceSquared(enemy.Position, heroPosition);
            if (distance > (long)range * range ||
                enemy.Role is UnitRole.Ranged or UnitRole.Caster or UnitRole.Summoner && distance < 9_000_000)
            {
                Point destination = distance > (long)range * range
                    ? heroPosition with { XRaw = Math.Clamp(heroPosition.XRaw + LaneOffset(enemy.Ordinal), 350, 11_650) }
                    : new Point(
                        Math.Clamp(enemy.Position.XRaw + Math.Sign(enemy.Position.XRaw - heroPosition.XRaw) * 700, 350, 11_650),
                        Math.Clamp(enemy.Position.YRaw + Math.Sign(enemy.Position.YRaw - heroPosition.YRaw) * 700, 350, 23_650));
                int move = Math.Max(1, checked((int)((long)enemy.Profile.MovementSpeedRawPerSecond * request.EnemySpeedBasisPoints / 10_000 / 20)));
                move = ScaleCombatValue(move, Math.Max(1_000, 10_000 - enemy.ChillEffect));
                if (enemy.Role == UnitRole.Charger)
                {
                    move = move * 3 / 2;
                }

                Point next = Point.MoveToward(enemy.Position, destination, move);
                enemy.Position = next;
                if ((tick & 3) == 0)
                {
                    events.Add(Event(tick, SpatialEventKind.EnemyMoved, enemy.EntityId, "hero", 0,
                        enemy.Position, heroPosition, enemy.Role.ToString()));
                }

                distance = Point.DistanceSquared(enemy.Position, heroPosition);
            }

            if (tick < enemy.NextActionTick || enemy.TelegraphTarget is null && distance > (long)range * range || !hero.IsAlive)
            {
                continue;
            }

            int attacksPerSecond = checked((int)((long)enemy.Scaled.AttacksPerSecondMilli * request.EnemySpeedBasisPoints / 10_000));
            attacksPerSecond = Math.Max(1, ScaleCombatValue(attacksPerSecond, Math.Max(1_000, 10_000 - enemy.ChillEffect -
                enemy.Curses.Secondary("archetypes.skill.enfeeble_hex", tick))));
            int normalInterval = Math.Max(8, checked((20_000 + attacksPerSecond - 1) / attacksPerSecond));
            bool areaAttack = activeSkill.Area || activeSkill.Kind is EnemySkillKind.Burrow or EnemySkillKind.Artillery;
            if (activeSkill.Avoidable && areaAttack && enemy.TelegraphTarget is null &&
                activeSkill.Kind is not (EnemySkillKind.HealingBloom or EnemySkillKind.RepairPulse or EnemySkillKind.ShieldLink))
            {
                enemy.TelegraphTarget = heroPosition;
                enemy.NextActionTick = tick + 12;
                events.Add(Event(tick, SpatialEventKind.BossTelegraph, enemy.EntityId, "hero", 2_000,
                    enemy.Position, heroPosition, $"{activeSkill.DisplayName}|{activeSkill.Telegraph}|{activeSkill.DamageType}|True|until:{(tick + 12) * TickMilliseconds}"));
                continue;
            }
            Point impactPoint = enemy.TelegraphTarget ?? heroPosition;
            enemy.TelegraphTarget = null;
            if (activeSkill.Kind == EnemySkillKind.Burrow)
            {
                enemy.Position = impactPoint;
                events.Add(Event(tick, SpatialEventKind.EnemyMoved, enemy.EntityId, "hero", 0,
                    enemy.Position, impactPoint, "钻地包抄"));
            }
            if (activeSkill.Kind is EnemySkillKind.ShieldLink or EnemySkillKind.SummonSwarm ||
                enemy.Boss && enemy.BossPhase > 0 && enemy.ActionSequence % 4 == 0 &&
                enemy.Profile.StableId.Contains("warfront", StringComparison.Ordinal))
            {
                if (activeSkill.Kind == EnemySkillKind.ShieldLink)
                {
                    enemy.ShieldUntilTick = tick + 80;
                    events.Add(Event(tick, SpatialEventKind.Guard, enemy.EntityId, "allies", 3_000,
                        enemy.Position, enemy.Position, "护盾链接：4米内其他友军30%减伤，不叠加，来源死亡或离开即断开"));
                }
                else if (enemies.Count(e => e.Summoned && e.Life > 0) < 8 && enemies.Count(e => e.Summoned) < 24)
                {
                    EnemyProfile child = Enemies.CorruptedWorker;
                    ScaledEnemy scaled = EnemyRules.Scale(child, request.AreaLevel, [], request.AbyssRoute, EnemyRarity.Normal);
                    int ordinal = enemies.Count;
                    enemies.Add(new EnemyUnit($"enemy-{request.NodeIndex}-{ordinal}", child, scaled, UnitRole.Melee,
                        EnemyRarity.Normal, false, false, Math.Max(2, scaled.Life / 2), enemy.Position, tick + 10)
                    { Summoned = true });
                    events.Add(Event(tick, SpatialEventKind.SkillEffect, enemy.EntityId, $"enemy-{request.NodeIndex}-{ordinal}", 1,
                        enemy.Position, enemy.Position, "召唤增援；无经验、物品和药剂充能"));
                }
                enemy.NextActionTick = tick + normalInterval * 2; enemy.ActionSequence++; continue;
            }
            if (activeSkill.Kind is EnemySkillKind.HealingBloom or EnemySkillKind.RepairPulse)
            {
                int restored = 0;
                foreach (EnemyUnit ally in enemies.Where(unit => unit.Life > 0 &&
                             InRange(enemy.Position, unit.Position, Math.Max(3_000, activeSkill.RangeRaw))))
                {
                    int before = ally.Life;
                    ally.Life = Math.Min(ally.MaximumLife, ally.Life + Math.Max(1, ally.MaximumLife * 4 / 100));
                    restored += ally.Life - before;
                }
                events.Add(Event(tick, SpatialEventKind.EnemyAttack, enemy.EntityId, "allies", restored,
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
                TargetArmor: activeSkill.DamageType == EnemyDamageType.Physical
                    ? checked(request.Build.Sheet.Armor().Value * ascendancy.ArmorMultiplier(tick) / 10_000 *
                        (10_000 + (request.EquipmentRuntime?.ArmorIncrease(request.EquipmentRuntime.NearbyEnemyCount?.Invoke() ?? 0) ?? 0)) / 10_000)
                    : 0,
                TargetEvasion: activeSkill.IsSpell ? 0 : request.Build.Sheet.Evasion().Value,
                Accuracy: enemy.Profile.Accuracy,
                IsSpell: activeSkill.IsSpell), random);
            int divisor = Math.Max(6, 8 + request.EnemyCount / 3);
            if (activeSkill.Area)
            {
                int unitRaw = ScaleCombatValue((weapon.MinimumPhysicalDamage + weapon.MaximumPhysicalDamage) / 2 / divisor,
                    activeSkill.DamageMultiplierBasisPoints);
                unitRaw = ScaleCombatValue(unitRaw, request.EnemyAreaDamageBasisPoints);
                army.ReceiveArea(enemy, activeSkill, activeSkill.Kind is EnemySkillKind.Artillery or EnemySkillKind.GroundHazard or EnemySkillKind.DelayedNova ? heroPosition : enemy.Position,
                    Math.Max(1_800, activeSkill.RangeRaw), unitRaw, random, tick, events);
            }
            int damage = hit.Hit ? hit.FinalPhysicalDamage / divisor : 0;
            damage = ScaleCombatValue(damage, Math.Max(0, 10_000 - enemy.Curses.Effect("archetypes.skill.enfeeble_hex", tick)));
            damage = ScaleCombatValue(damage, request.Auras?.IncomingHitMultiplier ?? 10_000);
            damage = checked((int)((long)damage * request.IncomingHitBasisPoints / 10_000));
            int skillMultiplier = activeSkill.DamageMultiplierBasisPoints;
            if (enemies.Any(unit => unit.Life > 0 && unit.Profile.EffectiveSkills.Any(skill => skill.Kind == EnemySkillKind.WarAura)))
                skillMultiplier = checked(skillMultiplier * 11_000 / 10_000);
            damage = checked(damage * skillMultiplier / 10_000);
            if (activeSkill.Area)
                damage = checked(damage * request.EnemyAreaDamageBasisPoints / 10_000);
            if (request.ExtraEnemyProjectiles > 0 && enemy.Role is UnitRole.Ranged or UnitRole.Caster)
                damage = checked(damage * (1 + request.ExtraEnemyProjectiles) * request.EnemyProjectileDamageBasisPoints / 10_000);
            if (enemy.BossPhase == 1) damage = checked(damage * 11_500 / 10_000);
            if (enemy.BossPhase == 2) damage = checked(damage * 17_500 / 10_000);
            if (damage > 0 && activeSkill.DamageType == EnemyDamageType.Physical)
                damage = ScaleCombatValue(damage, Math.Max(0, 10_000 - flasks.UtilityEffect(FlaskKind.Armor)));
            if (damage > 0 && activeSkill.IsSpell)
                damage = ScaleCombatValue(damage, Math.Max(0, 10_000 - flasks.UtilityEffect(FlaskKind.Resistance)));
            if (damage > 0 && tick < guardUntilTick)
                damage = Math.Max(1, damage * (10_000 - guardReductionBasisPoints) / 10_000);
            if (damage > 0 && tick < guardUntilTick && ascendancy.Has(WarriorNodeIds.BastionGuardCore))
                damage = Math.Max(1, damage * 7_500 / 10_000);
            if (damage > 0 && fortificationLayers > 0)
                damage = Math.Max(1, damage * CombatRules.FortificationMultiplier(
                    fortificationLayers, MasteryRuntime.FortificationMaximum(
                        request.Build.PassiveProfile ?? PassiveModifiers.Empty)) / 10_000);
            if (damage > 0 && hero.Life * 2L <= hero.MaximumLife && ascendancy.Has(WarriorNodeIds.BloodLowLifeCore))
                damage = Math.Max(1, damage * 7_500 / 10_000);
            bool spell = activeSkill.IsSpell;
            bool suppressed = false;
            if (!hit.Hit && !spell) request.EquipmentRuntime?.Evaded();
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
                int effectiveResistance = activeSkill.DamageType == EnemyDamageType.Physical
                    ? hero.Sheet.CappedPhysicalResistance(request.EquipmentRuntime?.Value(ItemModifierKind.PhysicalResistanceBasisPoints) ?? 0) :
                    CombatRules.EffectiveResistance(resistance,
                        request.Build.Sheet.ResistanceMaximum(activeSkill.DamageType),
                        request.EnemyPenetrationBasisPoints);
                damage = Math.Max(1, CombatRules.MitigateByResistance(damage, effectiveResistance));
                int suppression = request.Build.Sheet.EffectiveSpellSuppressionBasisPoints + (request.EquipmentRuntime?.SuppressionBonus(tick) ?? 0);
                if (spell && suppression > 0 && random.NextUInt() % 10_000 < suppression)
                {
                    suppressed = true;
                    damage = Math.Max(1, CombatRules.SuppressedDamage(damage,
                        request.Build.Sheet.SpellSuppressionEffectBasisPoints));
                    events.Add(Event(tick, SpatialEventKind.Guard, "hero", enemy.EntityId, 0,
                        heroPosition, enemy.Position, "spell_suppression"));
                }
            }
            int attackBlock = WarriorAscendancyRules.AttackBlockChanceBasisPoints(
                request.Build.BlockChanceBasisPoints, ascendancy.Profile, request.Build.HasShield);
            int attackBlockMaximum = WarriorAscendancyRules.AttackBlockMaximumBasisPoints(
                request.Build.Sheet.MaximumBlockChanceBasisPoints, ascendancy.Profile, request.Build.HasShield);
            int finalAttackBlock = Math.Clamp(attackBlock, 0, attackBlockMaximum);
            int blockChance = spell
                ? WarriorAscendancyRules.SpellBlockChanceBasisPoints(request.Build.Sheet.SpellBlockChanceBasisPoints,
                    finalAttackBlock, ascendancy.Profile, request.Build.HasShield)
                : attackBlock;
            int blockCap = spell
                ? request.Build.Sheet.MaximumSpellBlockChanceBasisPoints
                : attackBlockMaximum;
            bool blocked = damage > 0 && blockChance > 0 &&
                random.NextUInt() % 10_000 < Math.Min(blockCap, blockChance);
            if (blocked)
            {
                request.EquipmentRuntime?.Blocked(tick, spell);
                damage = spell && ascendancy.Has(WarriorNodeIds.BastionSpellBlockCore)
                    ? Math.Max(0, damage * ascendancy.IncomingHitMultiplier(true, true, tick) / 10_000) : 0;
                events.Add(Event(tick, SpatialEventKind.Block, "hero", enemy.EntityId, 0,
                    heroPosition, enemy.Position, spell ? "spell" : "attack"));
                int counterMultiplier = spell ? 10_000 : ascendancy.OnAttackBlock();
                if (shieldCounter is not null && shieldCounterConfiguration is not null &&
                    tick >= shieldCounterReadyTick)
                {
                    foreach (EnemyUnit counterTarget in enemies.Where(item => item.Life > 0 &&
                                 InRange(heroPosition, item.Position, ascendancy.Has(WarriorNodeIds.BastionCounterSmall)
                                      ? shieldCounter.RangeRaw * 12_000 / 10_000 : shieldCounter.RangeRaw)).ToArray())
                        ResolveHeroHit(request, shieldCounter, shieldCounterConfiguration, counterTarget, hero,
                            random, tick, heroPosition, counterMultiplier, events,
                            additionalIncreasedBasisPoints: ascendancy.Has(WarriorNodeIds.BastionCounterSmall) ? 4_000 : 0);
                    if (counterMultiplier >= 28_000)
                        hero.HealLife(Math.Max(1, hero.MaximumLife * 800 / 10_000));
                    int counterCooldown = ascendancy.Has(WarriorNodeIds.BastionCounterSmall)
                        ? shieldCounter.CooldownTicks * 10_000 / 12_500 : shieldCounter.CooldownTicks;
                    shieldCounterReadyTick = tick + Math.Max(1, counterCooldown);
                }
            }
            else if (damage > 0)
            {
                damage = Math.Max(1, damage * ascendancy.IncomingHitMultiplier(spell, false, tick) / 10_000);
                if (!spell) ascendancy.OnUnblockedAttack(tick);
            }
            if (activeSkill.Kind is EnemySkillKind.GroundHazard or EnemySkillKind.Artillery)
                hazards.Add(new(enemy.EntityId, impactPoint, 1_800, Math.Max(1, hit.PreMitigationPhysicalDamage / divisor / 2), tick + 10, tick + 90,
                    activeSkill.DamageType));
            if (request.ExtraBossPhase && enemy.Boss && enemy.BossPhase > 0)
                hazards.Add(new(enemy.EntityId, impactPoint, 2_000, Math.Max(1, hit.PreMitigationPhysicalDamage / divisor / 2), tick + 20, tick + 31,
                    activeSkill.DamageType));
            bool areaAvoided = areaAttack && !InRange(heroPosition, impactPoint, 2_000);
            if (areaAvoided) damage = 0;
            if (activeSkill.Kind == EnemySkillKind.RootSnare && damage > 0)
            {
                rootedUntilTick = Math.Max(rootedUntilTick, tick + 20);
                events.Add(Event(tick, SpatialEventKind.Ailment, enemy.EntityId, "hero", 20,
                    enemy.Position, heroPosition, "缠根：1秒禁止移动"));
            }
            if (damage > 0)
            {
                VirtueViceBonuses virtueBonuses = request.VirtueVice?.Bonuses() ?? new VirtueViceState().Bonuses();
                damage = activeSkill.DamageType is EnemyDamageType.Physical or EnemyDamageType.Void
                    ? damage * virtueBonuses.PhysicalVoidDamageTakenMultiplierBasisPoints / 10_000
                    : damage * virtueBonuses.ElementalDamageTakenMultiplierBasisPoints / 10_000;
                if (!spell)
                    damage = damage * MasteryRuntime.IncomingAttackMultiplier(
                        request.Build.PassiveProfile ?? PassiveModifiers.Empty, request.Build.Weapon) / 10_000;
                damage = ScaleCombatValue(damage, request.EquipmentRuntime?.IncomingMultiplier(hero.Sheet, activeSkill.DamageType, true, tick) ?? 10_000);
                if (request.EquipmentRuntime is { } equipment)
                    damage = equipment.ApplyEnemyDamage(hero, damage, true, tick, request.VirtueVice, blocked);
                else hero.ApplyDamage(damage, tick);
            }
            if (suppressed && !areaAvoided) request.EquipmentRuntime?.Suppressed(tick, hero);

            string attackDetail = $"{activeSkill.DisplayName}|{activeSkill.Telegraph}|{activeSkill.DamageType}|{activeSkill.Avoidable}|{(spell ? "spell" : "attack")}|result:{(areaAvoided ? "dodge" : blocked ? "block" : "hit")}";
            events.Add(Event(tick, SpatialEventKind.EnemyAttack, enemy.EntityId, "hero", damage,
                enemy.Position, impactPoint, attackDetail));
            int interval = Math.Max(8, checked((20_000 + attacksPerSecond - 1) / attacksPerSecond));
            interval = Math.Max(8, checked(interval * activeSkill.CooldownMultiplierBasisPoints / 10_000));
            enemy.NextActionTick = tick + interval;
            enemy.ActionSequence++;
        }
    }

    private static void ApplyHeroHit(
        NodeCombatRequest request,
        EnemyUnit enemy,
        Pcg32 random,
        int tick,
        int skillMultiplier,
        SpatialEventKind kind,
        Point source,
        ICollection<SpatialEvent> events,
        int bleedChance,
        ResourceState? hero = null,
        int lifeLeechBasisPoints = 0)
    {
        string skillId = SkillIdFor(kind);
        SkillConfiguration configuration = (request.Build.ActiveSkills ?? [request.Build.HeavyStrike])
            .FirstOrDefault(candidate => candidate.SkillId == skillId) ?? new SkillConfiguration(skillId, SkillSupport.None);
        hero ??= new ResourceState(request.Build.Sheet);
        ResolvedSkill skill = CombatSkillRules.Resolve(configuration, hero.MaximumLife, request.Build.PassiveProfile);
        skill = skill with
        {
            LifeLeechBasisPoints = skill.LifeLeechBasisPoints + lifeLeechBasisPoints,
            Ailment = bleedChance > 0 ? Ailment.Bleed : skill.Ailment,
            AilmentChanceBasisPoints = Math.Max(skill.AilmentChanceBasisPoints, bleedChance),
        };
        ResolveHeroHit(request, skill, configuration, enemy, hero, random, tick, source,
            skillMultiplier, events, eventKind: kind);
    }
    private static int EnemyResistance(EnemyUnit enemy, NodeCombatRequest request, SkillDamageType type, bool spellHit = false, bool penetrate = true)
    {
        int resistance = type switch
        {
            SkillDamageType.Fire => enemy.Scaled.FireResistanceBasisPoints,
            SkillDamageType.Cold => enemy.Scaled.ColdResistanceBasisPoints,
            SkillDamageType.Lightning => enemy.Scaled.LightningResistanceBasisPoints,
            _ => enemy.Scaled.VoidResistanceBasisPoints,
        };
        if (spellHit && type is SkillDamageType.Fire or SkillDamageType.Cold or SkillDamageType.Lightning &&
            request.EquipmentRuntime?.Has("逆抗星盘") == true)
            resistance = Math.Min(enemy.Scaled.FireResistanceBasisPoints,
                Math.Min(enemy.Scaled.ColdResistanceBasisPoints, enemy.Scaled.LightningResistanceBasisPoints));
        resistance += type == SkillDamageType.Void ? request.EnemyVoidResistanceBasisPoints : request.EnemyElementalResistanceBasisPoints;
        if (type is SkillDamageType.Fire or SkillDamageType.Cold or SkillDamageType.Lightning)
            resistance -= enemy.Curses.Effect("archetypes.skill.elemental_hex", enemy.CurrentTick);
        if (type == SkillDamageType.Void) resistance -= CombatRules.CorrosionResistanceReduction(enemy.Ailments.Stack(Ailment.Erosion, enemy.CurrentTick));
        return Math.Clamp(resistance, CombatRules.MinimumResistance, 7_500) -
            (penetrate ? request.Build.CombatEquipment?.Penetration(type) ?? 0 : 0);
    }

    private static int ActionDelay(TeamBuild build, int baseTicks, SkillTag tags = SkillTag.Attack)
        => CombatSkillRules.ActionDelay(build, baseTicks, tags);

    private static bool TryPayEquipmentCost(NodeCombatRequest request, ResourceState hero, ResolvedSkill skill)
    {
        if (!CombatSkillRules.TryPay(hero, skill)) return false;
        request.EquipmentRuntime?.BeginAction(skill.SkillId, skill.LifeCost, skill.ManaCost,
            SkillDefinitions.Get(skill.SkillId).Tags.HasFlag(SkillTag.Trigger), request.VirtueVice);
        return true;
    }

    private static bool TryPayEquipmentCost(NodeCombatRequest request, ResourceState hero, SkillUseProfile skill)
    {
        int multiplier = ScaleCombatValue(request.EquipmentRuntime?.Has("怒节同契") == true ? 12_000 : 10_000, request.Auras?.SkillCostMultiplier ?? 10_000);
        skill = skill with { LifeCost = ScaleCombatValue(skill.LifeCost, multiplier), ManaCost = ScaleCombatValue(skill.ManaCost, multiplier) };
        if (!SkillRules.TryPaySkillCost(hero, skill)) return false;
        request.EquipmentRuntime?.BeginAction(skill.SkillId, skill.LifeCost, skill.ManaCost, false, request.VirtueVice);
        return true;
    }

    private static void ApplyLifeLeech(ResourceState hero, int amount, int instantBasisPoints)
    {
        int instant = ScaleCombatValue(amount, Math.Clamp(instantBasisPoints, 0, 10_000));
        if (instant > 0) hero.HealLife(instant);
        int remaining = amount - instant;
        if (remaining > 0) hero.AddLifeLeech(remaining);
    }

    private static ResolvedSkill ApplyAscendancyCost(ResolvedSkill skill, SkillConfiguration configuration,
        int maximumLife, CombatProfile profile)
    {
        ResolvedSkill result = WarriorAscendancyRules.ApplySkillCost(
            skill, SkillDefinitions.Get(configuration.SkillId).Tags, maximumLife, profile);
        if (result.Role == SkillRole.Guard && profile.Has(WarriorNodeIds.BastionGuardSmall))
            result = result with { CooldownTicks = Math.Max(1, result.CooldownTicks * 10_000 / 13_000) };
        return result;
    }

    private static bool RechargeFlasksForKills(
        IEnumerable<EnemyUnit> enemies,
        Combat.FlaskRack flasks,
        int tick,
        Point heroPosition,
        ICollection<SpatialEvent> events,
        CombatRuntime runtime,
        ResourceState hero,
        EquipmentCombatRuntime equipment)
    {
        bool resetMovement = false;
        foreach (EnemyUnit enemy in enemies.Where(item => item.Life <= 0 && !item.KillCharged && !item.Summoned))
        {
            enemy.KillCharged = true;
            equipment.Killed(enemy.Rarity, tick);
            int charges = enemy.Rarity switch
            {
                EnemyRarity.Boss => 6,
                EnemyRarity.Rare => 4,
                EnemyRarity.Magic => 2,
                _ => 1,
            };
            if (equipment.Has("饥馑指环")) charges *= 2;
            if (equipment.Has("余烬锁链")) charges *= 2;
            flasks.GainCharges(charges);
            events.Add(Event(tick, SpatialEventKind.FlaskCharge, "hero", "hero", charges,
                heroPosition, heroPosition, $"life+{charges}|mana+{charges}"));
            if (enemy.BleedRemaining > 0)
            {
                runtime.KilledBleedingEnemy();
                if (runtime.Has(WarriorNodeIds.BloodTideCore))
                {
                    hero.HealLife(Math.Max(1, hero.MaximumLife * 400 / 10_000));
                    runtime.TriggerRecoveryProtection(tick);
                    EnemyUnit? spread = enemies.Where(item => item.Life > 0)
                        .OrderBy(item => Point.DistanceSquared(enemy.Position, item.Position)).FirstOrDefault();
                    if (spread is not null) enemy.Ailments.SpreadTo(spread.Ailments, Ailment.Bleed);
                }
            }
            resetMovement |= runtime.TryResetMovementCooldownOnKill(tick);
        }
        return resetMovement;
    }

    private static string SkillIdFor(SpatialEventKind kind) => kind switch
    {
        SpatialEventKind.HeavyStrike => SkillIds.HeavyStrike,
        SpatialEventKind.EarthCleave => SkillIds.EarthCleave,
        SpatialEventKind.SpiritBladeHit or SpatialEventKind.ChainHit => SkillIds.SpiritBlade,
        SpatialEventKind.SeismicCharge => SkillIds.SeismicCharge,
        SpatialEventKind.BloodTideSpin => SkillIds.BloodTideSpin,
        SpatialEventKind.AshJavelin => SkillIds.AshJavelin,
        SpatialEventKind.EmberNova => SkillIds.EmberNova,
        SpatialEventKind.StormBrand => SkillIds.StormBrand,
        _ => string.Empty,
    };

    private static void ResolveAftershocks(IList<PendingAftershock> pending, IEnumerable<EnemyUnit> enemies,
        int tick, ICollection<SpatialEvent> events)
    {
        foreach (PendingAftershock aftershock in pending.Where(item => item.ImpactTick <= tick).ToArray())
        {
            pending.Remove(aftershock);
            EnemyUnit? target = enemies.FirstOrDefault(item => item.EntityId == aftershock.TargetId && item.Life > 0);
            if (target is null) continue;
            int damage = Math.Min(target.Life, aftershock.ActualHitDamage);
            target.Life -= damage;
            events.Add(Event(tick, SpatialEventKind.Ascendancy, "hero", target.EntityId, damage,
                aftershock.Origin, target.Position, aftershock.Detail));
            if (target.Life == 0)
                events.Add(Event(tick, SpatialEventKind.EnemyDefeated, "hero", target.EntityId, 0,
                    aftershock.Origin, target.Position, target.Profile.StableId));
        }
    }

    private static void CaptureFrame(
        ICollection<SpatialFrame> frames,
        long at,
        int node,
        Point heroPosition,
        ResourceState hero,
        string target,
        IEnumerable<EnemyUnit> enemies,
        int partySize,
        int frontlineCount,
        VirtueViceState? virtueVice,
        int tick, BattleArmy? army = null, Combat.CombatActionQueue? actions = null)
    {
        if (frames.LastOrDefault()?.AtMilliseconds == at)
        {
            return;
        }

        frames.Add(new SpatialFrame(
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
            enemies.Select(enemy => new EnemyFrame(
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
                enemy.Scaled.EliteAffixes, enemy.Summoned,
                enemy.BleedPulses > 0 ? 1 : 0, enemy.DamageOverTimePulses > 0 ? enemy.DamageOverTimeAilment : Ailment.None,
                enemy.ArmorBreakStacks, enemy.ShockStacks, tick < enemy.ImpairedUntilTick)).ToArray(),
            BuildAllies(heroPosition, partySize, frontlineCount).Concat(army?.Frames() ?? []).Concat(actions?.PhantomFrames(tick) ?? []).ToArray(),
            CaptureVirtueVice(virtueVice)));
        // Keep simulation exact, but bound playback snapshots in extremely long battles.
        if (frames is List<SpatialFrame> list && list.Count > 4_096)
        {
            SpatialFrame[] retained = list.Where((_, index) => index == 0 || index % 2 == 1 || index == list.Count - 1).ToArray();
            list.Clear(); list.AddRange(retained);
        }
    }

    private static int ScaleCombatValue(int value, int basisPoints) =>
        (int)Math.Clamp((long)value * Math.Max(0, basisPoints) / 10_000, 0, int.MaxValue);

    private static int SaturatingAdd(int left, int right) =>
        (int)Math.Clamp((long)left + right, 0, int.MaxValue);

    private static IReadOnlyDictionary<VirtueViceKind, int>? CaptureVirtueVice(VirtueViceState? state)
    {
        if (state is null) return null;
        Dictionary<VirtueViceKind, int> layers = Enum.GetValues<VirtueViceKind>()
            .Where(kind => state.Layers(kind) > 0)
            .ToDictionary(kind => kind, state.Layers);
        return layers.Count == 0 ? null : layers;
    }

    private static IReadOnlyList<AllyFrame> BuildAllies(Point leader, int partySize, int frontlineCount)
    {
        var result = new List<AllyFrame>();
        int frontRemaining = Math.Max(0, frontlineCount - 1);
        for (int index = 1; index < Math.Clamp(partySize, 1, 6); index++)
        {
            bool front = index <= frontRemaining;
            int ordinal = front ? index - 1 : index - frontRemaining - 1;
            int x = leader.XRaw + (ordinal % 2 == 0 ? -1 : 1) * (900 + ordinal / 2 * 650);
            int y = leader.YRaw + (front ? -1_100 : 1_200 + ordinal / 2 * 550);
            result.Add(new AllyFrame($"ally-{index}", new Point(Math.Clamp(x, 350, 11_650), Math.Clamp(y, 350, 23_650)), front));
        }
        return result;
    }

    private static SpatialEvent Event(
        int tick,
        SpatialEventKind kind,
        string source,
        string target,
        int value,
        Point sourcePosition,
        Point targetPosition,
        string detail) => new(
        checked((long)tick * TickMilliseconds), kind, source, target, value, sourcePosition, targetPosition, detail);

    private static bool InRange(Point left, Point right, int range) =>
        Point.DistanceSquared(left, right) <= (long)range * range;

    private static bool InCleaveCone(Point origin, Point facingTarget, Point candidate, int range)
    {
        long candidateDistance = Point.DistanceSquared(origin, candidate);
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

    private static int TravelTicks(Point from, Point to, int speedPerSecond)
    {
        long distance = (long)Math.Sqrt(Point.DistanceSquared(from, to));
        return Math.Max(1, checked((int)((distance * 20 + speedPerSecond - 1) / speedPerSecond)));
    }

    private static ResolvedSkill? Resolve(
        IReadOnlyDictionary<string, SkillConfiguration> skills,
        string skillId,
        int maximumLife,
        PassiveModifiers? passive) => skills.TryGetValue(skillId, out SkillConfiguration? configuration)
        ? CombatSkillRules.Resolve(configuration, maximumLife, passive)
        : null;

    private static string? Candidate(string skillId, bool available) => available ? skillId : null;

    private static bool CanPay(ResourceState hero, ResolvedSkill skill) => skill.LifeCost > 0
        ? hero.Life > skill.LifeCost
        : hero.Mana >= skill.ManaCost;

    private static bool AiMatches(
        SkillConfiguration configuration,
        NodeCombatRequest request,
        ResourceState hero,
        EnemyUnit target,
        IReadOnlyCollection<EnemyUnit> enemies,
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

    private static int EnemyRange(UnitRole role) => role switch
    {
        UnitRole.Ranged => 6_000,
        UnitRole.Caster => 7_000,
        UnitRole.Summoner => 5_500,
        UnitRole.Boss => 2_000,
        _ => 1_200,
    };

    private static int LaneOffset(int ordinal) => (ordinal % 5 - 2) * 350;

    private static string Hash(
        ulong seed,
        BattleOutcome outcome,
        int ticks,
        ResourceState hero,
        IEnumerable<EnemyUnit> enemies,
        IEnumerable<SpatialEvent> events)
    {
        string source = $"Spatial|{seed}|{outcome}|{ticks}|{hero.Life}|{hero.Mana}|{hero.Shield}|" +
                        string.Join(';', enemies.Select(enemy => $"{enemy.EntityId}:{enemy.Life}:{enemy.Position.XRaw}:{enemy.Position.YRaw}")) +
                        "|" + string.Join(';', events.Select(item => $"{item.AtMilliseconds}:{item.Kind}:{item.TargetId}:{item.Value}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant();
    }

    private static void Validate(NodeCombatRequest request)
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
            // Rank-4 Area Disaster (90%) combined with King Disaster (80% more area)
            // is a legal 34,200-basis-point map roll.
            request.EnemyAreaBasisPoints is < 1_000 or > 35_000 || request.EnemyAreaDamageBasisPoints is < 1_000 or > 30_000 ||
            request.BossCount is < 1 or > 2 || request.AdditionalRareEnemies is < 0 or > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(request));
        }
    }

    private sealed class EnemyUnit(
        string entityId,
        EnemyProfile profile,
        ScaledEnemy scaled,
        UnitRole role,
        EnemyRarity rarity,
        bool elite,
        bool boss,
        int life,
        Point position,
        int nextActionTick)
    {
        public string EntityId { get; } = entityId;
        public EnemyProfile Profile { get; } = profile;
        public ScaledEnemy Scaled { get; } = scaled;
        public UnitRole Role { get; } = role;
        public EnemyRarity Rarity { get; } = rarity;
        public bool Elite { get; } = elite;
        public bool Boss { get; } = boss;
        public int MaximumLife { get; } = life;
        public int Ordinal { get; } = int.Parse(entityId[(entityId.LastIndexOf('-') + 1)..]);
        private int _life = life;
        public EnemyUnit? LinkedBy { get; set; }
        public int Life
        {
            get => _life;
            set => _life = value < _life && LinkedBy is { Life: > 0 } source &&
                InRange(Position, source.Position, 4_000)
                ? Math.Max(0, _life - Math.Max(1, (_life - value) * 7 / 10)) : value;
        }
        public bool Summoned { get; init; }
        public bool CorpseConsumed { get; set; }
        public string ArmyTauntId { get; set; } = "";
        public int ArmyTauntUntil { get; set; }
        public int ShieldUntilTick { get; set; }
        public Point? TelegraphTarget { get; set; }
        public Point Position { get; set; } = position;
        public int NextActionTick { get; set; } = nextActionTick;
        public Combat.AilmentState Ailments { get; } = new();
        public Combat.CurseState Curses { get; } = new();
        public int BleedRemaining => (int)Math.Min(int.MaxValue, Ailments.Remaining(Ailment.Bleed));
        public int BleedPulses => Ailments.Count(Ailment.Bleed);

        public int DamageOverTimePulses => Ailments.Instances.Count;
        public Ailment DamageOverTimeAilment => Ailments.Instances.FirstOrDefault(instance => instance.Kind != Ailment.Bleed)?.Kind ?? Ailment.None;
        public int CurrentTick, ShockEffect, ShockUntil, ChillEffect, FrozenUntil, Paralysis, ParalysisLastTick, ArmorBreakUntil;
        public int ArmorBreakStacks { get; set; }
        public int ShockStacks => ShockEffect > 0 ? 1 : 0;
        public int ImpairedUntilTick { get; set; }
        public int BossPhase { get; set; }
        public int LastTelegraphTick { get; set; } = int.MinValue;
        public int RuptureStacks { get; set; }
        public int StunnedUntilTick { get; set; }
        public int StunImmuneUntilTick { get; set; }
        public bool KillCharged { get; set; }
        public int ActionSequence { get; set; }
    }

    private sealed record PendingAftershock(int ImpactTick, string TargetId, int ActualHitDamage, Point Origin, string Detail = "linebreaker:aftershock");
    private sealed record EnemyHazard(string Source, Point Position, int Radius, int Damage, int Start, int Expires,
        EnemyDamageType DamageType);
}
