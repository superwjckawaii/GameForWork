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
using GameForWork.Core.Archetypes;

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
    ProjectileMoved = 100,
    HeroMoved = 0,
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
    IReadOnlyDictionary<VirtueViceKind, int>? HeroVirtueViceLayers = null,
    int HeroOvercharge = 0, int HeroMaximumOvercharge = 0);

public sealed record SpatialEvent(
    long AtMilliseconds,
    SpatialEventKind Kind,
    string SourceId,
    string TargetId,
    int Value,
    Point SourcePosition,
    Point TargetPosition,
    string Detail,
    SpatialPresentation? Presentation = null);

public sealed record SpatialPresentation(string ActionId, long StartsAtMilliseconds, long EndsAtMilliseconds,
    string Shape, int RadiusRaw, Point Direction, IReadOnlyList<Point> Trajectory)
{
    public bool Equals(SpatialPresentation? other) => other is not null && ActionId == other.ActionId &&
        StartsAtMilliseconds == other.StartsAtMilliseconds && EndsAtMilliseconds == other.EndsAtMilliseconds &&
        Shape == other.Shape && RadiusRaw == other.RadiusRaw && Direction == other.Direction &&
        Trajectory.SequenceEqual(other.Trajectory);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(ActionId); hash.Add(StartsAtMilliseconds); hash.Add(EndsAtMilliseconds);
        hash.Add(Shape); hash.Add(RadiusRaw); hash.Add(Direction);
        foreach (Point point in Trajectory) hash.Add(point);
        return hash.ToHashCode();
    }
}

public sealed record CombatObjective(Point InteractionPosition, Point ExitPosition, int InteractionTicks = 40,
    int HazardIntervalTicks = 120, int HazardDamage = 100);

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
    IReadOnlyList<EnemyProfile>? EnemyPool = null, EnemyProfile? EliteProfile = null,
    VirtueViceState? VirtueVice = null,
    EquipmentCombatRuntime? EquipmentRuntime = null,
    Combat.CombatActionQueue? Actions = null, Combat.AuraCombatProfile? Auras = null,
    Combat.FlaskRack? FlaskState = null, Combat.GuardState? Guard = null, Combat.CombatBuffState? Buffs = null,
    Combat.ReactionState? Reactions = null, Combat.ChannelCostState? ChannelCosts = null, Combat.CombatConditionState? Conditions = null, Combat.SpellCastState? SpellCasts = null, int? SpellDamageMultiplierSnapshot = null,
    EquipmentOffenseSnapshot? OffenseSnapshot = null, Combat.RuneFieldState? RuneFields = null,
    int? ActionMultiplierSnapshot = null, int? SpellEnergyIncreaseSnapshot = null, Combat.TeamProtectionState? TeamProtection = null,
    int? ResourceDamageMultiplierSnapshot = null, int? ArmorSnapshot = null, Combat.UnarmedCombatState? Unarmed = null, Combat.ElementalCombatState? Elemental = null, int? ElementalMultiplierSnapshot = null, int? ResistanceSnapshotTick = null, int? ElementalHitUntilSnapshot = null, int? VoidHitUntilSnapshot = null, bool ElementalSourceSelf = false,
    DamageOverTimeRecoveryState? DamageOverTimeRecovery = null, CombatObjective? Objective = null);

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
        request = request with { RuneFields = new(request.Build.Ascendancy) };
        request = request with { EquipmentRuntime = equipment, Actions = new Combat.CombatActionQueue(request.Build.Ascendancy), Guard = new Combat.GuardState(request.Build.Ascendancy), Buffs = new Combat.CombatBuffState(request.Build.Ascendancy), Reactions = new Combat.ReactionState(request.Build.PassiveProfile), ChannelCosts = new Combat.ChannelCostState(), Conditions = new Combat.CombatConditionState(), SpellCasts = new Combat.SpellCastState(), DamageOverTimeRecovery = new DamageOverTimeRecoveryState() };
        request = request with { TeamProtection = new(request.Build.Ascendancy?.Has("core.ascendancy.spirit_cantor.protection.core") == true) };
        var hero = new ResourceState(
            request.Build.Sheet,
            request.InitialHeroLife,
            request.InitialHeroMana,
            request.InitialHeroShield, request.Build.Ascendancy, request.Build.PassiveProfile, request.Build.Weapon);
        hero.ReserveMana(auras.ReservedMana);
        equipment.AbsorbEnemyDamage = (damage, hit, tick) => request.TeamProtection.Absorb("hero", request.Guard.AbsorbBarriers(
            hit ? damage : ScaleCombatValue(damage, MasteryRuntime.IncomingResourceMultiplier(request.Build.PassiveProfile ?? PassiveModifiers.Empty, hero, false)), tick), hit, tick);
        equipment.EnemyDamageApplied = (resource, result) =>
        {
            request.TeamProtection.Update("hero", hero.Life, hero.MaximumLife, hero.MaximumShield, auras.ActiveIds.Count > 0, result.Tick);
            request.Guard.ObserveEnemyDamage(resource, result);
            if (result.Damage > 0) request.Conditions.Damaged(result.Hit, result.Tick);
        };
        request.TeamProtection.Update("hero", hero.Life, hero.MaximumLife, hero.MaximumShield, auras.ActiveIds.Count > 0, 0);
        equipment.ExternalSkillCostMultiplier = auras.SkillCostMultiplier;
        var enemies = CreateEnemies(request, random);
        var events = new List<SpatialEvent>();
        var frames = new List<SpatialFrame>();
        var projectiles = new List<PendingProjectile>();
        var aftershocks = new List<PendingAftershock>();
        var hazards = new List<EnemyHazard>();
        var persistentAreas = new List<PersistentArea>();
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
            maxima, held, AscendancyDefinitions.ResourceDuration(ascendancy));
        request = request with
        {
            AscendancyRuntime = ascendancyRuntime,
            VirtueVice = virtueVice,
            Elemental = request.Elemental ?? new Combat.ElementalCombatState(ascendancy),
            Unarmed = new Combat.UnarmedCombatState(ascendancy, request.Build.ActiveSkills?.Any(skill => skill.SkillId == "archetypes.skill.chain_fists") == true)
        };
        Point heroPosition = new(6_000, 22_000);
        auras.SourcePosition = () => heroPosition;
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
        heavyStrike = heavyStrike with { ManaCost = MasteryRuntime.ManaCost(request.Build.PassiveProfile ?? PassiveModifiers.Empty, SkillTag.Attack, heavyStrike.ManaCost) };
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
        var cooldowns = new Combat.CooldownMasteryState(request.Build.PassiveProfile);
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
            if (ascendancy.Has("core.ascendancy.spirit_cantor.war_song.small"))
            {
                warCry.EffectMultiplierBasisPoints = ScaleCombatValue(warCry.EffectMultiplierBasisPoints, 12_500);
                warCry.DurationTicks = 208;
            }
            if (ascendancy.Has(WarriorNodeIds.BreakerWarCrySmall))
                warCry.CooldownDurationTicks = Math.Max(1, warCry.CooldownDurationTicks * 10_000 / 13_000);
        }

        var army = new BattleArmy(request, skills.Values, heroPosition, hero);
        equipment.NearbyEnemyCount = () => enemies.Count(enemy => enemy.Life > 0 && InRange(heroPosition, enemy.Position, 6_000));
        string heroTargetId = string.Empty;
        int heroNextActionTick = 0;
        int heavyStrikeFrequencyCarry = 0;
        var skillCatalogAttackFrequencyCarry = new Dictionary<string, int>(StringComparer.Ordinal);
        int bannerMultiplier = 10_000;
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

        equipment.RedirectDamage = (damage, hit) => army.RedirectDamage(damage, hit, heroPosition, tick, events);
        equipment.CompanionAlive = () => army.CompanionAlive;
        hero.LifeDepleted = () =>
        {
            if (!equipment.TryRekindle(hero)) return;
            hero.HarmfulStatus.Clear();
            flasks.Fill();
            rootedUntilTick = 0;
            warCry.ResetCooldown();
            foreach (ResolvedSkill skill in skillCatalogSkills.Values.Where(skill =>
                         SkillDefinitions.Get(skill.SkillId).Tags.HasFlag(SkillTag.WarCry)))
                cooldowns.Reset(skill, tick);
            events.Add(Event(tick, SpatialEventKind.Ascendancy, "hero", "hero", equipment.Rekindles,
                heroPosition, heroPosition, "equipment:灰烬之心|重燃"));
        };
        int initialHeroLife = hero.Life;
        int minimumHeroLife = hero.Life;
        int initialEnemyLife = enemies.Sum(enemy => enemy.MaximumLife);
        int minimumEnemyLife = initialEnemyLife;
        int lastProgressTick = 0;
        BattleOutcome? projectedOutcome = null;
        int interactionTicks = 0;
        int interactionEventIndex = -1;
        bool objectiveComplete = request.Objective is null;
        CaptureFrame(frames, 0, request.NodeIndex, heroPosition, hero, heroTargetId, enemies,
            request.Build.PartySize, request.Build.FrontlineCount, request.VirtueVice, 0, army, request.Actions);

        for (tick = 0; (request.MaximumTicks == 0 || tick < request.MaximumTicks) &&
             (hero.IsAlive || army.MercenaryAlive) && (request.Objective is null ? enemies.Any(enemy => enemy.Life > 0) : !objectiveComplete); tick++)
        {
            equipment.BeginTick(tick);
            if (request.Objective is { } objective && tick % objective.HazardIntervalTicks == 0)
            {
                int starts = tick + 30, ends = starts + 40;
                string actionId = $"harbor.hazard.{request.NodeIndex}.{tick}";
                hazards.Add(new(actionId, heroPosition, 1_800, objective.HazardDamage, starts, ends, EnemyDamageType.Fire));
                events.Add(new(tick * TickMilliseconds, SpatialEventKind.BossTelegraph, actionId, "hero", 0,
                    heroPosition, heroPosition, "港区周期危险预警",
                    new(actionId, starts * TickMilliseconds, ends * TickMilliseconds, "circle", 1_800, new(0, 0), [])));
            }
            hero.HarmfulStatus.Tick = tick;
            request.Reactions!.Tick = tick;
            virtueVice.Advance(TickMilliseconds);
            Point beforeMovement = heroPosition;
            foreach (EnemyUnit unit in enemies)
                unit.LinkedBy = enemies.FirstOrDefault(source => source != unit && source.Life > 0 &&
                    source.ShieldUntilTick > tick && InRange(source.Position, unit.Position, 4_000));
            if (tick >= rootedUntilTick && hero.HarmfulStatus.Effect(Ailment.Freeze) == 0 && hero.HarmfulStatus.Effect(Ailment.Stun) == 0)
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
                        Math.Max(1, request.Build.MovementSpeedBasisPoints * 300 / 10_000 *
                            (request.Actions!.IsChanneling(tick * TickMilliseconds) &&
                             MasteryRuntime.Has(request.Build.PassiveProfile ?? PassiveModifiers.Empty, "重复_引导", 3) ? 5_000 : 10_000) / 10_000));
                }
            }
            if (heroPosition != beforeMovement)
                request.Unarmed!.Moved((int)Math.Sqrt(Point.DistanceSquared(beforeMovement, heroPosition)), tick);
            const string instantSong = "archetypes.skill.soul_warsong";
            if (request.Buffs!.Instant(instantSong) && skillCatalogSkills.TryGetValue(instantSong, out var song) &&
                cooldowns.CanUse(song, tick, hero) && !TriggerSupported(skills[instantSong]) &&
                hero.HarmfulStatus.Effect(Ailment.Freeze) == 0 && hero.HarmfulStatus.Effect(Ailment.Stun) == 0 &&
                SelectTarget(enemies, heroPosition) is { } songTarget &&
                AiMatches(skills[instantSong], request, hero, songTarget, enemies, Point.DistanceSquared(heroPosition, songTarget.Position)) &&
                TryPayEquipmentCost(request, hero, song))
            {
                request.Buffs.Activate(skills[instantSong], !request.Build.HasUsableWeapon, tick);
                cooldowns.Start(song, tick, hero);
                events.Add(Event(tick, SpatialEventKind.SkillEffect, "hero", "hero", 0, heroPosition, heroPosition, $"skill:{instantSong}|buff-applied|instant"));
            }
            if (request.Guard!.TryOverload(hero, tick))
                events.Add(Event(tick, SpatialEventKind.Ascendancy, "hero", "hero", 0, heroPosition, heroPosition, "spellarmor-overload|duration:6000"));
            TeamBuild buffedBuild = request.Guard.ApplyBonuses(request.Actions!.ApplyPhantomBonuses(request.Buffs!.Apply(request.Unarmed!.Apply(request.Elemental!.Apply(originalBuild, virtueVice.Layers(VirtueViceKind.Temperance)), tick, virtueVice.Layers(VirtueViceKind.Mercy)), tick), tick), hero, tick);
            request = request with
            {
                Build = buffedBuild with
                {
                    AttackCastSpeedMultiplierBasisPoints = ScaleCombatValue(buffedBuild.AttackCastSpeedMultiplierBasisPoints, StunMasteryRules.SpeedMultiplier(buffedBuild.PassiveProfile ?? PassiveModifiers.Empty, tick, request.Conditions!.StunRecentUntil)),
                    IncreasedActionSpeedBasisPoints = buffedBuild.IncreasedActionSpeedBasisPoints + equipment.SpeedBonus(tick) - hero.HarmfulStatus.Effect(Ailment.Chill),
                    IncreasedAttackSpeedBasisPoints = buffedBuild.IncreasedAttackSpeedBasisPoints + equipment.AttackSpeedBonus(tick),
                    IncreasedCastSpeedBasisPoints = buffedBuild.IncreasedCastSpeedBasisPoints + equipment.CastSpeedBonus(tick),
                    MovementSpeedBasisPoints = buffedBuild.MovementSpeedBasisPoints + equipment.MovementBonus(tick) + flasks.Buff(ItemModifierKind.FlaskBuffMovementSpeedBasisPoints) +
                    (equipment.Has("朝圣者之债") ? Math.Min(4_500, flasks.UnusedUses * 300) : 0) - hero.HarmfulStatus.Effect(Ailment.Chill),
                    IncreasedCriticalChanceBasisPoints = buffedBuild.IncreasedCriticalChanceBasisPoints + flasks.Buff(ItemModifierKind.FlaskBuffCriticalChanceBasisPoints) + CriticalMasteryRules.RecentChanceIncrease(buffedBuild.PassiveProfile ?? PassiveModifiers.Empty, tick, request.Conditions!.KillRecentUntil),
                    CriticalMultiplierBasisPoints = buffedBuild.CriticalMultiplierBasisPoints + CriticalMasteryRules.RecentMultiplierBonus(buffedBuild.PassiveProfile ?? PassiveModifiers.Empty, tick, request.Conditions!.KillRecentUntil),
                    Sheet = buffedBuild.Sheet with
                    {
                        IncreasedArmorBasisPoints = buffedBuild.Sheet.IncreasedArmorBasisPoints + flasks.Buff(ItemModifierKind.FlaskBuffArmorBasisPoints),
                        IncreasedEvasionBasisPoints = buffedBuild.Sheet.IncreasedEvasionBasisPoints + flasks.Buff(ItemModifierKind.FlaskBuffEvasionBasisPoints),
                    },
                }
            };
            request = request with
            {
                Build = request.Build with
                {
                    Sheet = SpiritBarrierMasteryRules.Dynamic(request.Build.Sheet,
                request.Build.PassiveProfile ?? PassiveModifiers.Empty, hero.Life * 2L <= hero.MaximumLife, tick, request.Conditions!.HitRecentUntil)
                }
            };
            CharacterSheet recoverySheet = request.RuneFields!.Apply(request.Build.Sheet, heroPosition);
            recoverySheet = BlockMasteryRules.Recovery(recoverySheet, request.Build.PassiveProfile ?? PassiveModifiers.Empty, tick, request.Conditions!.BlockRecentUntil);
            recoverySheet = SpiritBarrierMasteryRules.Recovery(recoverySheet, request.Build.PassiveProfile ?? PassiveModifiers.Empty, tick, request.Conditions!.DamageOverTimeRecentUntil);
            if (MasteryRuntime.Has(request.Build.PassiveProfile ?? PassiveModifiers.Empty, "流血", 6) &&
                enemies.Any(enemy => enemy.Life > 0 && enemy.Rarity is EnemyRarity.Rare or EnemyRarity.Boss && enemy.Ailments.Count(Ailment.Bleed) > 0))
                recoverySheet = recoverySheet with { MaximumLifeRegenerationBasisPoints = recoverySheet.MaximumLifeRegenerationBasisPoints + 300 };
            recoverySheet = SuppressionMasteryRules.Recovery(recoverySheet, request.Build.PassiveProfile ?? PassiveModifiers.Empty, tick, request.Conditions!.SuppressionRecentUntil);
            recoverySheet = ResistanceMasteryRules.Recovery(recoverySheet, request.Build.PassiveProfile ?? PassiveModifiers.Empty, tick, request.Conditions!.VoidHitRecentUntil);
            recoverySheet = RegenerationMasteryRules.Recent(recoverySheet, request.Build.PassiveProfile ?? PassiveModifiers.Empty, tick, request.Conditions!.HitRecentUntil, hero.Life * 2L <= hero.MaximumLife, hero.Shield * 2L <= hero.MaximumShield);
            hero.UpdateSheet(recoverySheet);
            hero.AdvanceRegenerationTick(tick);
            request.TeamProtection!.Update("hero", hero.Life, hero.MaximumLife, hero.MaximumShield, auras.ActiveIds.Count > 0, tick);
            AdvancePlayerStatus(request, hero, heroPosition, tick, events);
            if (!hero.IsAlive)
            {
                army.Advance(enemies, heroPosition, random, tick, events, heroTargetId, request.Build);
                heroPosition = ResolveEnemies(request, enemies, hero, heroPosition, random, tick, events, flasks,
                    guardUntilTick, guardReductionBasisPoints, shieldCounter, shieldCounterConfiguration,
                    ref shieldCounterReadyTick, ascendancyRuntime, hazards, ref rootedUntilTick, fortificationLayers, army,
                    _ => { });
                foreach (EnemyHazard hazard in hazards.Where(hazard => hazard.Expires > tick && tick >= hazard.Start && (tick - hazard.Start) % 10 == 0))
                    army.ReceiveHazard(hazard, tick, events);
                if (request.MaximumTicks > 0 || (tick & 3) == 0)
                    CaptureFrame(frames, tick * TickMilliseconds, request.NodeIndex, heroPosition, hero, heroTargetId, enemies,
                        request.Build.PartySize, request.Build.FrontlineCount, request.VirtueVice, tick, army, request.Actions);
                continue;
            }
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
            AdvancePersistentAreas(persistentAreas, enemies, hero, random, tick, events);
            AdvanceAilments(request, enemies, hero, tick, events);
            ResolveProjectiles(projectiles, enemies, hero, heroPosition, random, tick, events);
            request.Actions!.CompleteReady(tick * TickMilliseconds, projectiles.Select(projectile => projectile.Action.Context.Id).ToHashSet(),
                equipment.Has("百式回身"), equipment.Has("攻法回文"), heroPosition, hero);
            request.Actions.ExpirePhantoms(tick, hero, request.Build.Ascendancy?.Has("core.ascendancy.phantom_master.sustain.small") == true);
            ResolveCopies(request, enemies, hero, heroPosition, random, tick, events);
            ResolveAftershocks(aftershocks, enemies, tick, events);

            EnemyUnit? target = SelectTarget(enemies, heroPosition);
            heroTargetId = target?.EntityId ?? string.Empty;
            bool movingWhileChanneling = heroPosition != beforeMovement &&
                MasteryRuntime.Has(request.Build.PassiveProfile ?? PassiveModifiers.Empty, "重复_引导", 3) &&
                request.Actions.IsChanneling(tick * TickMilliseconds);
            if (target is not null && tick >= heroNextActionTick && (heroPosition == beforeMovement || movingWhileChanneling) &&
                hero.HarmfulStatus.Effect(Ailment.Freeze) == 0 && hero.HarmfulStatus.Effect(Ailment.Stun) == 0)
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
                        Candidate(SkillIds.SeismicCharge, charge is not null && cooldowns.CanUse(charge, tick, hero) &&
                            SkillTarget(SkillIds.SeismicCharge) is not null &&
                            SkillDistance(SkillIds.SeismicCharge) > (long)HeavyStrikeRange * HeavyStrikeRange &&
                            SkillDistance(SkillIds.SeismicCharge) <= (long)charge.RangeRaw * charge.RangeRaw && CanPay(request, hero, charge)),
                        Candidate(SkillIds.BloodTideSpin, spin is not null && cooldowns.CanUse(spin, tick, hero) &&
                            SkillTarget(SkillIds.BloodTideSpin) is not null && NearbyCount(SkillIds.BloodTideSpin, spin.AreaRadiusRaw) >= 2 && CanPay(request, hero, spin)),
                        Candidate(SkillIds.EarthCleave, cleave is not null && cooldowns.CanUse(cleave, tick, hero) &&
                            SkillTarget(SkillIds.EarthCleave) is not null && ConeCount(SkillIds.EarthCleave, cleave.AreaRadiusRaw) >= 2 && CanPay(request, hero, cleave)),
                        Candidate(SkillIds.SpiritBlade, blade is not null && cooldowns.CanUse(blade, tick, hero) &&
                            SkillTarget(SkillIds.SpiritBlade) is not null && SkillDistance(SkillIds.SpiritBlade) <= (long)blade.RangeRaw * blade.RangeRaw && CanPay(request, hero, blade)),
                        Candidate(SkillIds.AshJavelin, ashJavelin is not null && cooldowns.CanUse(ashJavelin, tick, hero) &&
                            SkillTarget(SkillIds.AshJavelin) is not null && SkillDistance(SkillIds.AshJavelin) <= (long)ashJavelin.RangeRaw * ashJavelin.RangeRaw && CanPay(request, hero, ashJavelin)),
                        Candidate(SkillIds.EmberNova, emberNova is not null && cooldowns.CanUse(emberNova, tick, hero) &&
                            SkillTarget(SkillIds.EmberNova) is not null && NearbyCount(SkillIds.EmberNova, emberNova.AreaRadiusRaw) >= 2 && CanPay(request, hero, emberNova)),
                        Candidate(SkillIds.StormBrand, stormBrand is not null && cooldowns.CanUse(stormBrand, tick, hero) &&
                            SkillTarget(SkillIds.StormBrand) is not null &&
                            enemies.Any(enemy => enemy.Life > 0 && InRange(heroPosition, enemy.Position, stormBrand.RangeRaw) &&
                                !persistentAreas.Any(area => area.Skill.SkillId == SkillIds.StormBrand && area.Target == enemy.EntityId)) && CanPay(request, hero, stormBrand)),
                        Candidate(SkillIds.HeavyStrike, skills.ContainsKey(SkillIds.HeavyStrike) &&
                            SkillTarget(SkillIds.HeavyStrike) is not null && SkillDistance(SkillIds.HeavyStrike) <= (long)heavyStrike.RangeRaw * heavyStrike.RangeRaw &&
                            (heavyStrike.LifeCost > 0 ? hero.Life > heavyStrike.LifeCost : hero.Mana >= heavyStrike.ManaCost)),
                    }
                    .Where(candidate => candidate is not null && !TriggerSupported(skills[candidate]) && AiMatches(skills[candidate], request, hero,
                        SkillTarget(candidate)!, enemies, SkillDistance(candidate)))
                    .OrderBy(candidate => skills[candidate!].Priority)
                    .FirstOrDefault();
                string? skillCatalogChosen = skillCatalogSkills.Values.Select(skill => request.Unarmed!.Resolve(skill, tick, virtueVice.Layers(VirtueViceKind.Mercy)))
                    .Where(skill => (!UnarmedRules.IsSkill(skill.SkillId) || !request.Build.HasUsableWeapon) && !request.Buffs!.Instant(skill.SkillId) && army.CanUse(skill.SkillId) && request.Buffs.CanUse(skills[skill.SkillId], tick) &&
                                    (skill.Role != SkillRole.Reservation || Combat.CombatBuffState.IsSkill(skill.SkillId)) && skill.Role != SkillRole.Counter &&
                                    !TriggerSupported(skills[skill.SkillId]) &&
                                    (skill.SkillId != Combat.ReactionState.Overload || request.Guard!.ArmorEnergy > 0) &&
                                    (skill.SkillId != "archetypes.skill.doom_brand" || persistentAreas.Count(area => area.Skill.SkillId == skill.SkillId) < 3 &&
                                        enemies.Any(enemy => enemy.Life > 0 && InRange(heroPosition, enemy.Position, skill.RangeRaw) &&
                                            !persistentAreas.Any(area => area.Skill.SkillId == skill.SkillId && area.Target == enemy.EntityId))) &&
                                    (skill.SkillId != "archetypes.skill.yin_yang_stance" || !request.Build.HasUsableWeapon) &&
                                    (!skill.RequiresShield || request.Build.HasShield) && cooldowns.CanUse(skill, tick, hero) &&
                                    SkillTarget(skill.SkillId) is not null &&
                                    (skill.Shape == SkillShape.Self ||
                                     SkillDistance(skill.SkillId) <= (long)AreaRules.EngagementRange(skill) * AreaRules.EngagementRange(skill)) && CanPay(request, hero, skill) &&
                                    AiMatches(skills[skill.SkillId], request, hero, SkillTarget(skill.SkillId)!, enemies, SkillDistance(skill.SkillId)))
                    .OrderBy(skill => skills[skill.SkillId].Priority)
                    .Select(skill => skill.SkillId)
                    .FirstOrDefault();
                if (skillCatalogChosen is not null && (chosen is null || skills[skillCatalogChosen].Priority < skills[chosen].Priority))
                    chosen = skillCatalogChosen;

                if (chosen is not null)
                {
                    target = SkillTarget(chosen)!;
                    request.Conditions!.SelectAttackTarget(target.EntityId);
                    heroTargetId = target.EntityId;
                }
                long distance = Point.DistanceSquared(heroPosition, target.Position);
                int cleaveRange = cleave is null ? 0 : cleave.AreaRadiusRaw *
                    (ascendancyRuntime.MarchReady && ascendancy.Has(WarriorNodeIds.BreakerMarchCore) ? 15_000 : 10_000) / 10_000;
                EnemyUnit[] cleaveTargets = cleave is null ? [] : enemies.Where(enemy => enemy.Life > 0 &&
                    InCleaveCone(heroPosition, target.Position, enemy.Position, cleaveRange)).ToArray();
                EnemyUnit[] spinTargets = spin is null ? [] : enemies.Where(enemy => enemy.Life > 0 &&
                    InRange(heroPosition, enemy.Position, spin.AreaRadiusRaw)).ToArray();

                if (chosen is null)
                {
                    ResolvedSkill? blocked = new[] { charge, spin, cleave, blade, ashJavelin, emberNova, stormBrand, heavyResolved }
                        .Where(skill => skill is not null && distance <= (long)skill.RangeRaw * skill.RangeRaw && !CanPay(request, hero, skill))
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
                    ResolvedSkill? selectedCatalogSkill = chosen is not null && skillCatalogSkills.TryGetValue(chosen, out var template)
                        ? request.Unarmed!.Resolve(template, tick, virtueVice.Layers(VirtueViceKind.Mercy)) : null;
                    if (chosen is not null && selectedCatalogSkill is { } skillCatalogSkill && TryPayEquipmentCost(request, hero, skillCatalogSkill))
                    {
                        SkillTag skillCatalogTags = SkillDefinitions.Get(skillCatalogSkill.SkillId).Tags;
                        int useCount = 1;
                        if (skillCatalogTags.HasFlag(SkillTag.Attack))
                        {
                            int frequency = CombatSkillRules.ActionFrequencyMilliPerSecond(request.Build,
                                skillCatalogSkill, skillCatalogTags);
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
                                    ref lastSelfAttackOrSpellTick, projectiles, persistentAreas);
                        }
                        if (skillCatalogSkill.Role == SkillRole.Guard && ascendancy.Has(WarriorNodeIds.BastionGuardSmall))
                        {
                            request.Guard!.Extend(Math.Max(1, (guardUntilTick - tick) / 4));
                            guardUntilTick = request.Guard.Expires;
                        }
                        cooldowns.Start(skillCatalogSkill with
                        {
                            CooldownTicks = Math.Max(1,
                            skillCatalogSkill.CooldownTicks * 10_000 / (10_000 + request.Buffs.CooldownRecovery(chosen)))
                        }, tick, hero);
                        heroNextActionTick = tick + (skillCatalogTags.HasFlag(SkillTag.Channelling) ? 5 :
                            CombatSkillRules.ActionDelay(request.Build, skillCatalogSkill, skillCatalogTags));
                        if (UnarmedRules.Repeats(chosen, request.Build)) heroNextActionTick += heroNextActionTick - tick;
                        if (request.Buffs.Instant(chosen)) heroNextActionTick = tick;
                    }
                    else if (chosen == SkillIds.SeismicCharge && TryPayEquipmentCost(request, hero, charge!))
                    {
                        ResolvedSkill chargeSkill = charge!;
                        Point beforeCharge = heroPosition;
                        heroPosition = Point.MoveToward(heroPosition, target.Position, Math.Max(1, chargeSkill.RangeRaw - 900));
                        ascendancyRuntime.Moved((int)Math.Sqrt(Point.DistanceSquared(beforeCharge, heroPosition)));
                        request.Unarmed!.Moved((int)Math.Sqrt(Point.DistanceSquared(beforeCharge, heroPosition)), tick, chargeSkill.SkillId);
                        if (beforeCharge != heroPosition) equipment.UsedMovementSkill(tick);
                        foreach (EnemyUnit enemy in enemies.Where(enemy => enemy.Life > 0 && InRange(heroPosition, enemy.Position, chargeSkill.AreaRadiusRaw)))
                        {
                            int multiplier = bannerMultiplier;
                            ApplyHeroHit(request, enemy, random, tick, multiplier, SpatialEventKind.SeismicCharge,
                                heroPosition, events, chargeSkill.BleedChanceBasisPoints, hero, chargeSkill.LifeLeechBasisPoints);
                        }
                        events.Add(Event(tick, SpatialEventKind.SeismicCharge, "hero", target.EntityId, 0,
                            heroPosition, target.Position, "movement"));
                        cooldowns.Start(chargeSkill, tick, hero);
                        heroNextActionTick = tick + CombatSkillRules.ActionDelay(request.Build, chargeSkill,
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
                        cooldowns.Start(spinSkill, tick, hero);
                        heroNextActionTick = tick + CombatSkillRules.ActionDelay(request.Build, spinSkill,
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

                        cooldowns.Start(cleaveSkill, tick, hero);
                        heroNextActionTick = tick + CombatSkillRules.ActionDelay(request.Build, cleaveSkill,
                            SkillDefinitions.Get(cleaveSkill.SkillId).Tags);
                    }
                    else if (chosen == SkillIds.SpiritBlade && TryPayEquipmentCost(request, hero, blade!))
                    {
                        ResolvedSkill bladeSkill = blade!;
                        LaunchProjectiles(request, bladeSkill, skills[chosen], target, enemies, heroPosition,
                            bannerMultiplier * 9_000 / 10_000, tick, projectiles);
                        events.Add(Event(tick, SpatialEventKind.SpiritBladeLaunched, "hero", target.EntityId, 0,
                            heroPosition, target.Position, $"projectile:{bladeSkill.ProjectileCount}"));
                        cooldowns.Start(bladeSkill, tick, hero);
                        heroNextActionTick = tick + CombatSkillRules.ActionDelay(request.Build, bladeSkill,
                            SkillDefinitions.Get(bladeSkill.SkillId).Tags);
                    }
                    else if (chosen == SkillIds.AshJavelin && TryPayEquipmentCost(request, hero, ashJavelin!))
                    {
                        ResolvedSkill skill = ashJavelin!;
                        ApplyHeroHit(request, target, random, tick, bannerMultiplier,
                            SpatialEventKind.AshJavelin, heroPosition, events, skill.BleedChanceBasisPoints, hero,
                            skill.LifeLeechBasisPoints);
                        cooldowns.Start(skill, tick, hero);
                        heroNextActionTick = tick + CombatSkillRules.ActionDelay(request.Build, skill,
                            SkillDefinitions.Get(skill.SkillId).Tags);
                    }
                    else if (chosen == SkillIds.EmberNova && TryPayEquipmentCost(request, hero, emberNova!))
                    {
                        ResolvedSkill skill = emberNova!;
                        foreach (EnemyUnit enemy in enemies.Where(enemy => enemy.Life > 0 && InRange(heroPosition, enemy.Position, skill.AreaRadiusRaw)))
                            ApplyHeroHit(request, enemy, random, tick, bannerMultiplier,
                                SpatialEventKind.EmberNova, heroPosition, events, 0, hero, skill.LifeLeechBasisPoints);
                        cooldowns.Start(skill, tick, hero);
                        heroNextActionTick = tick + CombatSkillRules.ActionDelay(request.Build, skill,
                            SkillDefinitions.Get(skill.SkillId).Tags);
                    }
                    else if (chosen == SkillIds.StormBrand && TryPayEquipmentCost(request, hero, stormBrand!))
                    {
                        ResolvedSkill skill = stormBrand!;
                        CreatePersistentArea(request, skill, skills[chosen], target, enemies, hero, random, tick,
                            heroPosition, heroPosition, bannerMultiplier, persistentAreas, events);
                        cooldowns.Start(skill, tick, hero);
                        heroNextActionTick = tick + CombatSkillRules.ActionDelay(request.Build, skill,
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
                            [masterySpeed, request.Build.AttackCastSpeedMultiplierBasisPoints]);
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
                            request.Unarmed!.Moved((int)Math.Sqrt(Point.DistanceSquared(heroPosition, next)), tick);
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

            ResolveReactions(request, enemies, hero, heroPosition, random, tick, events, projectiles, persistentAreas);
            army.Advance(enemies, heroPosition, random, tick, events, heroTargetId, request.Build);
            if (RechargeFlasksForKills(enemies, flasks, tick, heroPosition, events, ascendancyRuntime, hero, equipment, random, request) && charge is not null)
                cooldowns.Reset(charge, tick);
            if (request.Objective is { } destination &&
                (interactionTicks >= destination.InteractionTicks || enemies.All(enemy => enemy.Life <= 0)) && hero.IsAlive &&
                tick >= rootedUntilTick && hero.HarmfulStatus.Effect(Ailment.Stun) == 0 && hero.HarmfulStatus.Effect(Ailment.Freeze) == 0)
            {
                bool collected = interactionTicks >= destination.InteractionTicks;
                Point targetPosition = collected ? destination.ExitPosition : destination.InteractionPosition;
                if (heroPosition == beforeMovement)
                {
                    Point from = heroPosition;
                    heroPosition = Point.MoveToward(from, targetPosition,
                        Math.Max(1, (int)Math.Min(int.MaxValue, 200L * request.Build.MovementSpeedBasisPoints / 10_000)));
                    if (from != heroPosition)
                        events.Add(new(tick * TickMilliseconds, SpatialEventKind.HeroMoved, "hero", "harbor.objective", 0,
                            from, heroPosition, collected ? "自动撤离" : "前往宝库",
                            new($"harbor.move.{request.NodeIndex}.{tick}", tick * TickMilliseconds,
                                (tick + 1L) * TickMilliseconds, "segment", 0,
                                new(heroPosition.XRaw - from.XRaw, heroPosition.YRaw - from.YRaw), [from, heroPosition])));
                }
                if (heroPosition == targetPosition)
                {
                    if (collected) objectiveComplete = true;
                    else
                    {
                        if (interactionTicks == 0)
                        {
                            interactionEventIndex = events.Count;
                            events.Add(new(tick * TickMilliseconds, SpatialEventKind.SkillEffect, "hero", "harbor.vault", 0,
                                heroPosition, heroPosition, "自动开启宝库（尚未获得奖励）",
                                new($"harbor.interact.{request.NodeIndex}.{tick}", tick * TickMilliseconds,
                                    (tick + destination.InteractionTicks) * (long)TickMilliseconds,
                                    "interaction", 0, new(0, 0), [])));
                        }
                        interactionTicks++;
                        if (interactionTicks == destination.InteractionTicks)
                        {
                            foreach (EnemyUnit pursuer in CreateEnemies(request with { EnemyCount = 2, HasBoss = false, HasElite = false }, random))
                            {
                                Point arrival = new(Math.Max(350, heroPosition.XRaw - 2_000), heroPosition.YRaw);
                                enemies.Add(new EnemyUnit($"enemy-{request.NodeIndex}-{enemies.Count}", pursuer.Profile, pursuer.Scaled,
                                    pursuer.Role, pursuer.Rarity, pursuer.Elite, false, pursuer.Life, arrival, tick + 10));
                            }
                            events.Add(Event(tick, SpatialEventKind.SkillEffect, "harbor.pursuit", "hero", 2,
                                heroPosition, heroPosition, "宝库开启，追击者入场；抵达出口即可撤离"));
                        }
                    }
                }
                else if (!collected)
                {
                    if (interactionEventIndex >= 0 && events[interactionEventIndex].Presentation is { } interaction)
                        events[interactionEventIndex] = events[interactionEventIndex] with
                        { Presentation = interaction with { EndsAtMilliseconds = tick * TickMilliseconds } };
                    interactionEventIndex = -1;
                    interactionTicks = 0;
                }
            }
            if (tick < rootedUntilTick) heroPosition = beforeMovement;
            equipment.Advance(tick, hero, heroPosition != beforeMovement, virtueVice);
            guardUntilTick = Math.Max(guardUntilTick, request.Guard.Expires);
            heroPosition = ResolveEnemies(request, enemies, hero, heroPosition, random, tick, events, flasks,
                guardUntilTick, guardReductionBasisPoints, shieldCounter, shieldCounterConfiguration,
                ref shieldCounterReadyTick, ascendancyRuntime, hazards, ref rootedUntilTick, fortificationLayers, army,
                position => ResolveReactions(request, enemies, hero, position, random, tick, events, projectiles, persistentAreas));
            foreach (EnemyHazard hazard in hazards.Where(h => h.Expires > tick && tick >= h.Start && (tick - h.Start) % 10 == 0))
            {
                army.ReceiveHazard(hazard, tick, events);
                int damage = InRange(heroPosition, hazard.Position, hazard.Radius) ? hazard.Damage : 0;
                if (damage > 0)
                {
                    damage = equipment.MitigateDamageOverTime(hero.Sheet, damage, hazard.DamageType, tick, 2);
                    damage = ScaleCombatValue(damage, ResistanceMasteryRules.IncomingMultiplier(hero.Sheet, request.Build.PassiveProfile ?? PassiveModifiers.Empty, hazard.DamageType, false));
                }
                if (damage > 0)
                {
                    damage = equipment.ApplyEnemyDamage(hero, damage, false, tick, virtueVice);
                }
                events.Add(Event(tick, SpatialEventKind.EnemyAttack, hazard.Source, "hero", damage,
                    hazard.Position, hazard.Position, $"持续危险地面|radius:{hazard.Radius}|until:{hazard.Expires * TickMilliseconds}") with
                { Presentation = new($"{hazard.Source}.ground.{hazard.Start}.{hazard.Position}", hazard.Start * TickMilliseconds, hazard.Expires * TickMilliseconds,
                    "circle", hazard.Radius, new(0, 0), []) });
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

        bool victory = request.Objective is null ? enemies.All(enemy => enemy.Life <= 0) : objectiveComplete && hero.IsAlive;
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
                : request.EliteProfile is not null && elite && index == firstNonBoss
                    ? request.EliteProfile
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
        ref int lastSelfAttackOrSpellTick, IList<PendingProjectile> projectiles, IList<PersistentArea> persistentAreas)
    {
        if (skill.SkillId == Combat.ReactionState.Overload)
        {
            if (request.Reactions!.Arm(configuration, request.Guard!))
                events.Add(Event(tick, SpatialEventKind.SkillEffect, "hero", "hero", 0, heroPosition, heroPosition, $"skill:{skill.SkillId}|attack-armed"));
            return;
        }
        if (ApplyCurse(request, skill, configuration, target, enemies, heroPosition, tick, events)) return;
        if (Combat.CombatBuffState.IsSkill(skill.SkillId))
        {
            var commandTarget = enemies.Where(enemy => enemy.Life > 0).OrderByDescending(enemy => enemy.Rarity is EnemyRarity.Rare or EnemyRarity.Boss)
                .ThenByDescending(enemy => enemy.Life).FirstOrDefault();
            if (request.Buffs!.Activate(configuration, !request.Build.HasUsableWeapon, tick, commandTarget?.EntityId ?? ""))
                events.Add(Event(tick, SpatialEventKind.SkillEffect, "hero", commandTarget?.EntityId ?? "hero", 0,
                    heroPosition, heroPosition, $"skill:{skill.SkillId}|buff-applied"));
            return;
        }
        Point actionOrigin = heroPosition;
        if (SkillDefinitions.Get(skill.SkillId).Tags.HasFlag(SkillTag.Channelling))
        {
            if (skill.SkillId == "archetypes.skill.withering_ray") skill = skill with { AilmentChanceBasisPoints = 7_000 + configuration.Quality * 100 };
            ResolveHeroHit(request, skill, configuration, target, hero, random, tick, heroPosition, bannerMultiplier, events);
            return;
        }
        if (skill.Role == SkillRole.Movement)
        {
            Point beforeMove = heroPosition;
            heroPosition = Point.MoveToward(heroPosition, target.Position, Math.Max(1, skill.RangeRaw - (skill.SkillId == SkillIds.FlameStep ? 0 : 900)));
            request.AscendancyRuntime?.Moved((int)Math.Sqrt(Point.DistanceSquared(beforeMove, heroPosition)));
            request.Unarmed!.Moved((int)Math.Sqrt(Point.DistanceSquared(beforeMove, heroPosition)), tick, skill.SkillId);
            if (beforeMove != heroPosition) request.EquipmentRuntime?.UsedMovementSkill(tick);
            events.Add(Event(tick, SpatialEventKind.HeroMoved, "hero", target.EntityId, 0,
                heroPosition, target.Position, $"skill:{skill.SkillId}"));
        }

        if (skill.Role == SkillRole.Guard || skill.SkillId == SkillIds.DefiantCry)
        {
            if (skill.SkillId == SkillIds.DefiantCry)
            {
                guardUntilTick = tick + 60;
                guardReductionBasisPoints = 2_000;
                request.Guard!.StartGuard(hero, tick, 60);
                hero.HealLife(Math.Max(1, (hero.MaximumLife - hero.Life) / 10));
            }
            else
            {
                if (!request.Guard!.Activate(skill.SkillId, hero, configuration.Level, configuration.Quality, tick)) return;
                request.Guard.ApplySupports(configuration, hero);
                guardUntilTick = request.Guard.Expires;
                guardReductionBasisPoints = 0;
            }
            events.Add(Event(tick, SpatialEventKind.Guard, "hero", "hero", request.Guard?.Remaining ?? 0,
                heroPosition, heroPosition, $"skill:{skill.SkillId}|until:{guardUntilTick}"));
            if (skill.SkillId == "archetypes.skill.aegis_pulse")
            {
                Point center = heroPosition;
                foreach (var enemy in enemies.Where(enemy => enemy.Life > 0 && InRange(center, enemy.Position, skill.AreaRadiusRaw)))
                    ResolveHeroHit(request, skill with { Role = SkillRole.Clear }, configuration, enemy, hero, random,
                        tick, center, bannerMultiplier, events, additionalBaseDamage: request.Guard!.LastPaidShield);
            }
            return;
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
        if (CreatePersistentArea(request, skill, configuration, target, enemies, hero, random, tick,
            actionOrigin, heroPosition, bannerMultiplier, persistentAreas, events)) return;

        Point origin = heroPosition;
        if (request.EquipmentRuntime is { ExtraActionChains: > 0 } actionEquipment)
            skill = skill with { MaximumChains = skill.MaximumChains + actionEquipment.ExtraActionChains };
        EnemyUnit[] affected = skill.Shape switch
        {
            SkillShape.Circle or SkillShape.MovementCircle or SkillShape.GroundArea => enemies
                .Where(enemy => enemy.Life > 0 && InRange(origin, enemy.Position, skill.AreaRadiusRaw)).ToArray(),
            SkillShape.Cone => enemies.Where(enemy => enemy.Life > 0 &&
                InCleaveCone(origin, target.Position, enemy.Position, skill.AreaRadiusRaw)).ToArray(),
            SkillShape.Chain => enemies.Where(enemy => enemy.Life > 0)
                .OrderBy(enemy => Point.DistanceSquared(target.Position, enemy.Position))
                .Take(Math.Max(1, skill.MaximumChains + 1)).ToArray(),
            SkillShape.Projectile => enemies.Where(enemy => enemy.Life > 0)
                .OrderBy(enemy => Point.DistanceSquared(origin, enemy.Position))
                .Take(Math.Max(1, skill.ProjectileCount + skill.PierceCount + skill.ForkCount)).ToArray(),
            _ => [target],
        };


        int useCount = useCounts[skill.SkillId] = useCounts[skill.SkillId] + 1;
        PassiveModifiers passive = request.Build.PassiveProfile ?? PassiveModifiers.Empty;
        SkillTag skillTags = SkillDefinitions.Get(skill.SkillId).Tags;
        if (skill.Shape == SkillShape.Single && skillTags.HasFlag(SkillTag.Strike) && !skill.SingleTargetOnly)
        {
            int additional = request.Build.CombatEquipment?.Value(ItemModifierKind.AdditionalStrikeTarget) ?? 0;
            affected = affected.Concat(enemies.Where(enemy => enemy != target && enemy.Life > 0 && InRange(origin, enemy.Position, skill.RangeRaw))
                .OrderBy(enemy => Point.DistanceSquared(origin, enemy.Position)).ThenBy(enemy => enemy.EntityId, StringComparer.Ordinal)
                .Take(Math.Max(0, additional))).ToArray();
        }
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
            bool copies = LinkedSupportRules.Support(configuration, SupportMechanic.PhantomCopy);
            int sacrifice = LinkedSupportRules.SupportValue(configuration, SupportMechanic.PhantomSacrifice, 6_000, 10_000);
            if (copies) ratio = ScaleCombatValue(ratio, 10_000 - LinkedSupportRules.QualityOverride(configuration,
                SupportMechanic.PhantomCopy, LinkedSupportRules.SupportValue(configuration, SupportMechanic.PhantomCopy, 2_500, 1_000), 500));
            if (skill.SkillId == "archetypes.skill.phantom_step")
                for (int spawn = 0; spawn < (copies ? 2 : 1); spawn++)
                    request.Actions!.SpawnPhantom(heroPosition, tick, ScaleCombatValue(80 + configuration.Quality * 80 / 100,
                        10_000 - LinkedSupportRules.SupportValue(configuration, SupportMechanic.PhantomSacrifice, 3_000, 2_000)),
                        (request.Build.Ascendancy?.Has("core.ascendancy.phantom_master.spawn.core") == true ? 4 : 2) +
                        (request.Build.CombatEquipment?.Value(ItemModifierKind.AdditionalPhantomMaximum) ?? 0), ratio, sacrifice,
                        (int)(3_000 * Math.Sqrt(1 + LinkedSupportRules.SupportQuality(configuration, SupportMechanic.PhantomSacrifice) / 100d)),
                        hero, request.Build.Ascendancy?.Has("core.ascendancy.phantom_master.sustain.small") == true);
            else request.Actions!.CommandPhantoms(tick);
            events.Add(Event(tick, SpatialEventKind.SkillEffect, "hero", "", 0, heroPosition, heroPosition, $"skill:{skill.SkillId}|phantoms:{request.Actions!.PhantomFrames(tick).Count}"));
            return;
        }
        foreach (EnemyUnit enemy in affected)
        {
            int burstDamage = 0;
            if (skill.SkillId == SkillIds.BloodBurst)
            {
                burstDamage = (int)Math.Min(int.MaxValue, enemy.Ailments.ConsumeForAction(Ailment.Bleed,
                    request.Actions?.CanonicalAction(request.EquipmentRuntime!.ActionId) ?? request.EquipmentRuntime?.ActionId ?? $"{tick}:{skill.SkillId}", 6_500 + Math.Clamp(configuration.Quality, 0, 20) * 50,
                    (type, dps) => DefendEnemyDot(request, enemy, type, dps, tick), VoidDebuffed(enemy, tick)));
            }
            if (burstDamage > 0 && enemy.Life > 0)
            {
                int dealt = Math.Min(enemy.Life, burstDamage);
                enemy.Life -= dealt;
                events.Add(Event(tick, SpatialEventKind.SkillEffect, "hero", enemy.EntityId, dealt,
                    heroPosition, enemy.Position, $"skill:{skill.SkillId}|blood-burst|dot:bleed|supports:{(ulong)configuration.Supports}"));
                if (enemy.Life == 0)
                    events.Add(Event(tick, SpatialEventKind.EnemyDefeated, "hero", enemy.EntityId, 0,
                        heroPosition, enemy.Position, enemy.Profile.StableId));
            }
            if (enemy.Life <= 0) continue;
            ResolveHeroHit(request, skill, configuration, enemy, hero, random, tick, heroPosition,
                enemy != target && skill.Shape == SkillShape.Single && skillTags.HasFlag(SkillTag.Strike) && UnarmedRules.IsSkill(skill.SkillId) && MasteryRuntime.Has(passive, "徒手", 6)
                    ? ScaleCombatValue(bannerMultiplier, 13_000) : bannerMultiplier, events,
                MasteryRuntime.Has(passive, "破甲_物理穿透", 1) && skillTags.HasFlag(SkillTag.Physical)
                    ? empoweredArmorBreak ? 5 : 2
                    : 0);
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
        int chainIndex = 0, int additionalBaseDamage = 0)
    {
        CombatRuntime runtime = request.AscendancyRuntime ?? new CombatRuntime(CombatProfile.Empty);
        SkillTag tags = SkillDefinitions.Get(skill.SkillId).Tags | skill.AdditionalTags;
        EquipmentCombatRuntime? equipment = request.EquipmentRuntime;
        if (tags.HasFlag(SkillTag.Area) && skill.Role != SkillRole.DamageOverTime &&
            request.Actions?.TryAreaHit(equipment!.ActionId, enemy.EntityId, tick,
                MasteryRuntime.Has(request.Build.PassiveProfile ?? PassiveModifiers.Empty, "范围_距离", 4)) == false) return null;
        if (skill.Role != SkillRole.DamageOverTime) multiplier = ScaleCombatValue(multiplier,
            request.ResourceDamageMultiplierSnapshot ?? MasteryRuntime.OffensiveResourceMultiplier(request.Build.PassiveProfile ?? PassiveModifiers.Empty, hero));
        var unarmedBonus = request.Unarmed?.Begin(equipment!.ActionId, configuration, request.Build, enemy.EntityId, tick, equipment.CaptureAction().Triggered) ?? new Combat.UnarmedActionBonus(10_000, 0, false, 0);
        multiplier = ScaleCombatValue(multiplier, unarmedBonus.Multiplier);
        additionalIncreasedBasisPoints += unarmedBonus.IncreasedDamage + (tags.HasFlag(SkillTag.Counter) ? request.Unarmed?.CounterIncrease(tick) ?? 0 : 0);
        request.Actions?.Begin(equipment!.ActionId, skill, request.Build, tick, equipment.CaptureAction().Triggered);
        multiplier = ScaleCombatValue(multiplier, request.ElementalMultiplierSnapshot ?? request.Elemental?.Begin(
            request.Actions?.CanonicalAction(equipment!.ActionId) ?? equipment!.ActionId, tags, tick, equipment.CaptureAction().Triggered) ?? 10_000);
        if (tags.HasFlag(SkillTag.Spell)) additionalIncreasedBasisPoints += request.SpellEnergyIncreaseSnapshot ??
            (equipment!.CaptureAction().Triggered ? request.Guard?.SpellDamageIncrease ?? 0 : request.Reactions?.SpellIncrease(equipment.ActionId) ?? 0);
        if (tags.HasFlag(SkillTag.Attack)) multiplier = ScaleCombatValue(multiplier, AttackMasteryRules.ActivationMultiplier(
            request.Build.PassiveProfile ?? PassiveModifiers.Empty, equipment?.CaptureAction().Triggered != true && !tags.HasFlag(SkillTag.Counter) && skill.SkillId != "archetypes.skill.corrosive_trap"));
        if (skill.Role != SkillRole.DamageOverTime)
        {
            var suppressionProfile = request.Build.PassiveProfile ?? PassiveModifiers.Empty;
            multiplier = ScaleCombatValue(multiplier, SuppressionMasteryRules.HitMultiplier(suppressionProfile, tick, request.Conditions?.SuppressionRecentUntil ?? 0));
            additionalIncreasedBasisPoints += SuppressionMasteryRules.OverflowHitIncrease(suppressionProfile, request.Build.Sheet.SpellSuppressionBasisPoints + (equipment?.SuppressionBonus(tick) ?? 0)) -
                SuppressionMasteryRules.OverflowHitIncrease(suppressionProfile, request.Build.Sheet.SpellSuppressionBasisPoints);
        }
        bool hunted = enemy.Rarity is EnemyRarity.Rare or EnemyRarity.Boss && request.Auras?.HunterAlwaysHits == true;
        bool missed = tags.HasFlag(SkillTag.Attack) && !skill.AlwaysHit && !request.Build.AlwaysHit && !MasteryRuntime.AlwaysHits(request.Build.PassiveProfile ?? PassiveModifiers.Empty, tags) && !hunted && random.NextBasisPoints() >=
            DamageRules.HitChance(request.Build.Sheet.Accuracy(request.Build.FlatAccuracy + UnarmedRules.Accuracy(skill.SkillId, request.Build)).Value, enemy.Scaled.Evasion, false).Value;
        if (missed)
        {
            events.Add(Event(tick, eventKind, "hero", enemy.EntityId, 0, source, enemy.Position, $"skill:{skill.SkillId}|miss"));
            if (request.Build.Ascendancy?.Ascendancy != Ascendancy.PhantomMaster && !UnarmedRules.Repeats(skill.SkillId, request.Build) && !LinkedSupportRules.MovementEcho(configuration)) return null;
        }
        if (!missed && skill.Role != SkillRole.DamageOverTime) multiplier = ScaleCombatValue(multiplier,
            request.Conditions?.ConsumeBlockCharge(tick, (tags & (SkillTag.Attack | SkillTag.Spell)) != 0 && equipment?.CaptureAction() is not { Triggered: true } and not { Copy: true } && !tags.HasFlag(SkillTag.Counter) && skill.SkillId != "archetypes.skill.corrosive_trap") ?? 10000);
        int distanceRaw = (int)Math.Sqrt(Point.DistanceSquared(source, enemy.Position));
        int areaPositionMultiplier = tags.HasFlag(SkillTag.Area) ?
            AreaRules.PositionMultiplier(request.Build.PassiveProfile ?? PassiveModifiers.Empty, distanceRaw, skill.AreaRadiusRaw) : 10_000;
        multiplier = ScaleCombatValue(multiplier, areaPositionMultiplier);
        multiplier = ScaleCombatValue(multiplier, request.ActionMultiplierSnapshot ??
            (equipment?.CaptureAction().Triggered != true ? request.Reactions?.ActionMultiplier(equipment!.ActionId) ?? 10_000 : 10_000));
        multiplier = ScaleCombatValue(multiplier, equipment?.HitMultiplier(request.Build, hero, tags, enemy.EntityId,
            enemy.Life, enemy.MaximumLife, enemy.Rarity is EnemyRarity.Rare or EnemyRarity.Boss, enemy.Boss, enemy.BleedRemaining > 0,
            distanceRaw, equipment.NearbyEnemyCount?.Invoke() ?? 0, tick, chainIndex, request.OffenseSnapshot) ?? 10_000);
        if (runtime.Has(WarriorNodeIds.BloodLifeCore) && tags.HasFlag(SkillTag.Attack)) runtime.PaidLife(tick);
        int ascendancyMultiplier = runtime.ConsumeAttackMultiplier(tags,
            hero.Life * 2L <= hero.MaximumLife, !request.Build.HasShield,
            new EnemyState(enemy.ArmorBreakStacks, tick < enemy.StunnedUntilTick));
        PassiveModifiers profile = request.Build.PassiveProfile ?? PassiveModifiers.Empty;
        bool triggeredAction = equipment?.CaptureAction().Triggered == true;
        multiplier = ScaleCombatValue(multiplier, ActionMasteryRules.DamageMultiplier(profile, tags, triggeredAction));
        multiplier = ScaleCombatValue(multiplier, request.Actions?.ChannelDepthMultiplier(
            equipment?.ActionId ?? "", tick, profile) ?? 10_000);
        if (tags.HasFlag(SkillTag.Spell)) multiplier = ScaleCombatValue(multiplier, CombatRules.ApplyMore(
            SpellActionMultiplier(request, skill),
            [skill.Role != SkillRole.DamageOverTime ? SpellMasteryRules.HitMultiplier(profile) : 10000]));
        bool luckyPhysical = MasteryRuntime.Has(profile, "物理", 5);
        WeaponProfile damageSource = UnarmedRules.Source(skill.SkillId, request.Build);
        int weaponRoll = MasteryDamageRules.RollPhysical(damageSource.MinimumPhysicalDamage,
            damageSource.MaximumPhysicalDamage, random, luckyPhysical && tags.HasFlag(SkillTag.Attack));
        int raw = tags.HasFlag(SkillTag.Spell) ? SpellHitRules.Roll(skill, configuration.Level, random) :
            CombatSkillRules.BaseDamage(skill, tags, damageSource, AttackMasteryRules.AddedDamage(request.Build.PassiveProfile ?? PassiveModifiers.Empty, request.Build.AddedPhysicalDamage), weaponRoll);
        if (luckyPhysical && tags.HasFlag(SkillTag.Spell) && skill.DamageType == SkillDamageType.Physical)
            raw = Math.Max(raw, SpellHitRules.Roll(skill, configuration.Level, random));
        raw = (int)Math.Min(int.MaxValue, (long)raw + Math.Max(0, additionalBaseDamage));
        if (skill.SkillId == "archetypes.skill.plague_detonation")
            multiplier = ScaleCombatValue(multiplier, 10_000 + enemy.Ailments.ConsumeStacks(Ailment.Poison, 10 + configuration.Quality / 10, tick) * 1_200);
        if (skill.SkillId == "archetypes.skill.forbidden_collapse")
            multiplier = ScaleCombatValue(multiplier, 10_000 + enemy.Ailments.ConsumeStacks(Ailment.Wither, 10, tick) * 2_000);
        AddedWeaponDamage addedWeapon = tags.HasFlag(SkillTag.Attack) && !UnarmedRules.IsSkill(skill.SkillId) && skill.Role != SkillRole.DamageOverTime &&
                                           request.Build.LocalWeaponStats is { } localWeapon
            ? new(Roll(localWeapon.Fire), Roll(localWeapon.Cold), Roll(localWeapon.Lightning), Roll(localWeapon.Void))
            : default;
        int criticalChance = CriticalHitRules.Chance(request.Build, skill, configuration, distanceRaw, equipment?.BaseCriticalBonus(tags, distanceRaw) ?? 0);
        bool critical = !request.Build.CannotCrit && skill.Role != SkillRole.DamageOverTime && !MasteryRuntime.CannotCrit(profile, tags, request.Build.Weapon) &&
                        (unarmedBonus.ForceCritical || equipment?.ForceCritical(tags) == true || CriticalMasteryRules.Roll(profile, criticalChance, random));
        int criticalMultiplier = critical ? ScaleCombatValue(CriticalHitRules.Multiplier(request.Build, configuration) +
            (hunted ? request.Auras?.HunterCriticalMultiplier ?? 0 : 0), equipment?.ForceCritical(tags) == true ? 15_000 : 10_000) : 10_000;
        if (critical && request.VirtueVice is { } criticalVirtues)
            criticalMultiplier = ScaleCombatValue(criticalMultiplier, 10_000 + criticalVirtues.Bonuses().MoreCriticalDamageBasisPoints);
        int armor = CombatRules.ArmorAfterBreak(enemy.Scaled.Armor, enemy.ArmorBreakStacks, runtime.ArmorBreakMaximum,
            request.Auras?.ArmorReductionAt(enemy.Position, distanceRaw) ?? 0);
        if (configuration.Supports.HasFlag(SkillSupport.ArmorPierce)) armor = armor * 7_000 / 10_000;
        var ailmentSource = new List<DamageBranch>();
        var offensiveBranches = new List<DamageBranch>();
        DamageBreakdown damage = DamagePacketRules.ResolveMixed(raw, skill.DamageType, addedWeapon, configuration.Supports,
            armor, EnemyResistance(enemy, request, SkillDamageType.Fire, tags.HasFlag(SkillTag.Spell) && skill.Role != SkillRole.DamageOverTime, penetrate: skill.Role != SkillRole.DamageOverTime),
            EnemyResistance(enemy, request, SkillDamageType.Cold, tags.HasFlag(SkillTag.Spell) && skill.Role != SkillRole.DamageOverTime, penetrate: skill.Role != SkillRole.DamageOverTime),
            EnemyResistance(enemy, request, SkillDamageType.Lightning, tags.HasFlag(SkillTag.Spell) && skill.Role != SkillRole.DamageOverTime, penetrate: skill.Role != SkillRole.DamageOverTime),
            EnemyResistance(enemy, request, SkillDamageType.Void, penetrate: skill.Role != SkillRole.DamageOverTime),
            enemy.Scaled.PhysicalResistanceBasisPoints + request.EnemyPhysicalReductionBasisPoints,
            equipment?.Loadout.Modifiers,
            CombatSkillRules.OffensiveIncreases(request.Build, tags, skill.Role == SkillRole.DamageOverTime,
                additionalIncreasedBasisPoints + (request.RuneFields?.DamageIncrease(source) ?? 0),
                armor: MasteryRuntime.Has(profile, "护甲", 6) ? request.ArmorSnapshot ?? HeroCurrentArmor(request, hero, tick) : null),
            branch =>
            {
                if (request.Auras?.ExclusiveElement is { } allowed && branch.CurrentType is DamageType.Fire or DamageType.Cold or DamageType.Lightning && branch.CurrentType != allowed) return 0;
                int ScaleBranch(int amount) => ScaleCombatValue(ScaleCombatValue(CombatSkillRules.ScaleOffensiveDamage(amount, skill, configuration,
                    request.Build, tags, enemy.Life, enemy.MaximumLife, multiplier,
                    targetRareOrBoss: enemy.Rarity is EnemyRarity.Rare or EnemyRarity.Boss,
                    applyIncreased: false, damageHistory: branch.History,
                    nearbyEnemyCount: equipment?.NearbyEnemyCount?.Invoke() ?? 1, distanceRaw: distanceRaw), ascendancyMultiplier), criticalMultiplier);
                int resistanceMultiplier = ResistanceMasteryRules.OutgoingMultiplier(profile, branch.CurrentType, skill.Role != SkillRole.DamageOverTime, request.ResistanceSnapshotTick ?? tick, request.ElementalHitUntilSnapshot ?? request.Conditions?.ElementalHitRecentUntil ?? 0, request.VoidHitUntilSnapshot ?? request.Conditions?.VoidHitRecentUntil ?? 0);
                int scaled = ScaleCombatValue(ScaleBranch(branch.BaseDamage), resistanceMultiplier);
                int? debuffed = branch.DebuffedBaseDamage is { } conditional ? ScaleCombatValue(ScaleBranch(conditional), resistanceMultiplier) : null;
                if (!configuration.Supports.HasFlag(SkillSupport.Brutality) || branch.CurrentType == DamageType.Physical)
                    offensiveBranches.Add(branch with { BaseDamage = scaled, DebuffedBaseDamage = debuffed });
                if (VoidDebuffed(enemy, tick) && debuffed.HasValue) scaled = debuffed.Value;
                if (tags.HasFlag(SkillTag.Attack)) scaled = ScaleCombatValue(scaled, AttackMasteryRules.TargetMultiplier(profile, enemy.Rarity is EnemyRarity.Rare or EnemyRarity.Boss));
                if (tags.HasFlag(SkillTag.Attack) && MasteryRuntime.Has(profile, "攻击", 6)) scaled = ScaleCombatValue(scaled, request.Conditions?.AttackMultiplier(enemy.EntityId, tick) ?? 10_000);
                scaled = ScaleCombatValue(scaled, ElementalRules.TargetMultiplier(request.Build.Ascendancy, branch.CurrentType, ElementalStatus(enemy, tick), skill.Role != SkillRole.DamageOverTime, critical));
                if (skill.Role != SkillRole.DamageOverTime && branch.CurrentType == DamageType.Cold)
                    scaled = ScaleCombatValue(scaled, ElementalControlMasteryRules.ColdHitMultiplier(profile, tick, enemy.ColdPursuitUntil));
                if (skill.Role != SkillRole.DamageOverTime) scaled = ScaleCombatValue(scaled, ElementalControlMasteryRules.HitMultiplier(profile, tick, enemy.ParalysisPursuitUntil));
                if (skill.Role != SkillRole.DamageOverTime) scaled = ScaleCombatValue(scaled, CriticalMasteryRules.TargetMultiplier(profile, critical,
                    enemy.ChillEffect > 0 && tick < enemy.ImpairedUntilTick || tick < enemy.FrozenUntil || enemy.ShockEffect > 0 && tick < enemy.ShockUntil || tick < enemy.ParalyzedUntil || tick < enemy.StunnedUntilTick));
                if (skill.Role != SkillRole.DamageOverTime) scaled = ScaleCombatValue(scaled, StunMasteryRules.HitMultiplier(profile, tick, enemy.StunPursuitUntil));
                if (skill.Role != SkillRole.DamageOverTime) scaled = ScaleCombatValue(scaled,
                    AilmentMasteryRules.BleedingTargetHitMultiplier(request.Build.PassiveProfile ?? PassiveModifiers.Empty, enemy.Ailments));
                scaled = ScaleCombatValue(scaled, 10_000 + enemy.ShockEffect);
                scaled = ScaleCombatValue(scaled, 10_000 + enemy.Curses.Effect("archetypes.skill.death_mark", tick));
                if (branch.CurrentType == DamageType.Void)
                {
                    scaled = ScaleCombatValue(scaled, 10_000 + enemy.Curses.Effect("archetypes.skill.doom_brand", tick));
                    scaled = ScaleCombatValue(scaled, CombatRules.WitherMultiplier(enemy.Ailments.Stack(Ailment.Wither, tick), 15));
                    if (skill.Role != SkillRole.DamageOverTime) scaled = ScaleCombatValue(scaled, VoidDebuffMasteryRules.HitMultiplier(profile, enemy.Ailments, tick));
                }
                return scaled;
            }, branches => ailmentSource.AddRange(branches), configuration,
            tags.HasFlag(SkillTag.Spell) ? SpellHitRules.Effectiveness(skill.SkillId) : 10_000, random: random,
            mastery: new(profile, skill.Role != SkillRole.DamageOverTime, enemy.Life, enemy.MaximumLife), ascendancy: request.Build.Ascendancy);
        request.Actions?.Record(equipment!.ActionId, new(enemy.EntityId, source, skill, configuration, request.Build,
            new(0, 0, 0, 0, 0, offensiveBranches.ToArray(), []), ailmentSource.ToArray(), critical, criticalMultiplier, AreaPositionMultiplier: areaPositionMultiplier, SpellDamageMultiplier: SpellActionMultiplier(request, skill), ResistanceSnapshotTick: request.ResistanceSnapshotTick ?? tick,
                ElementalHitUntil: request.ElementalHitUntilSnapshot ?? request.Conditions?.ElementalHitRecentUntil ?? 0, VoidHitUntil: request.VoidHitUntilSnapshot ?? request.Conditions?.VoidHitRecentUntil ?? 0), tick, equipment.CaptureAction().Triggered);
        if (missed) return null;
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
        SkillTag tags = SkillDefinitions.Get(skill.SkillId).Tags | skill.AdditionalTags;
        EquipmentCombatRuntime? equipment = request.EquipmentRuntime;
        PassiveModifiers profile = request.Build.PassiveProfile ?? PassiveModifiers.Empty;
        int value = damage.Total;
        request.Elemental?.Observe(damage, tick, equipment?.CaptureAction().Copy == true || equipment?.CaptureAction().Triggered == true && !request.ElementalSourceSelf);
        int beforeShieldLink = enemy.Life;
        enemy.Life = Math.Max(0, enemy.Life - value);
        value = beforeShieldLink - enemy.Life;
        bool triggered = equipment?.CaptureAction().Triggered == true;
        if (value > 0 && !request.Build.HasUsableWeapon && request.Unarmed?.Hit(equipment!.ActionId, configuration, tick, triggered) == true)
            events.Add(Event(tick, SpatialEventKind.SkillEffect, "hero", enemy.EntityId, request.Unarmed.Combo(tick), source, enemy.Position, "unarmed-combo"));
        if (!triggered && value > 0 && damage.Physical > 0 && tags.HasFlag(SkillTag.Attack) && tags.HasFlag(SkillTag.Melee)) equipment?.PhysicalMeleeHit?.Invoke();
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
            int freeze = equipment?.OnHit(hero, tags, enemy.EntityId, enemy.Boss, critical, value, request.VirtueVice, tick) ?? 0;
            if (value > 0 && tags.HasFlag(SkillTag.Attack) && tags.HasFlag(SkillTag.Projectile))
                equipment?.ProjectileHit(enemy.Rarity is EnemyRarity.Rare or EnemyRarity.Boss);
            enemy.NextActionTick = Math.Max(enemy.NextActionTick, tick + freeze);
        }
        if (value > 0 && skill.Role != SkillRole.DamageOverTime)
            hero.RestoreMana(request.SpellCasts?.HitRecovery(profile, SpellMasteryRules.Self(skill.SkillId, tags, equipment?.CaptureAction().Triggered == true) && equipment?.CaptureAction().Copy != true,
                enemy.Rarity is EnemyRarity.Rare or EnemyRarity.Boss, tick, hero.MaximumMana) ?? 0);
        int leech = skill.LifeLeechBasisPoints + MasteryRuntime.AdditionalLifeLeech(profile) +
            (runtime.Has(WarriorNodeIds.BloodTideSmall) && tags.HasFlag(SkillTag.Attack) &&
             skill.DamageType == SkillDamageType.Physical ? 100 : 0);
        if (equipment?.CaptureAction().Copy != true && value > 0 && leech > 0)
            ApplyLifeLeech(hero, Math.Max(1, value * leech / 10_000), request.Build.InstantLifeLeechBasisPoints);
        if (!triggered && value > 0 && skill.SkillId == "archetypes.skill.shield_drain")
            hero.AddShieldLeech(ScaleCombatValue(value, 300 + Math.Clamp(configuration.Quality, 0, 20) * 5));

        bool settlementKilled = false;
        if (value > 0 && enemy.Life > 0)
        {
            string action = request.Actions?.CanonicalAction(equipment!.ActionId) ?? equipment?.ActionId ?? $"{tick}:{skill.SkillId}";
            bool selfCast = !triggered && equipment?.CaptureAction().Copy != true;
            foreach (var kind in new[] { Ailment.Bleed, Ailment.Ignite })
                if (MasteryRuntime.Has(profile, kind == Ailment.Bleed ? "流血" : "点燃", 5) &&
                    enemy.Ailments.CountSettlementHit(kind, action, kind == Ailment.Bleed ? 5 : 4, selfCast))
                {
                    int settled = (int)Math.Min(enemy.Life, enemy.Ailments.ConsumeForAction(kind, action, 6_000,
                        (type, dps) => DefendEnemyDot(request, enemy, type, dps, tick), VoidDebuffed(enemy, tick)));
                    enemy.Life -= settled;
                    settlementKilled |= enemy.Life == 0;
                    events.Add(Event(tick, SpatialEventKind.Ailment, "hero", enemy.EntityId, settled, source, enemy.Position,
                        $"settlement:{kind.ToString().ToLowerInvariant()}"));
                }
        }
        if (value > 0 && tags.HasFlag(SkillTag.Attack) && MasteryRuntime.Has(profile, "攻击", 6))
            request.Conditions?.AttackHit(enemy.EntityId, request.Actions?.CanonicalAction(equipment?.ActionId ?? "") ?? $"{tick}:{skill.SkillId}", tick,
                !triggered && equipment?.CaptureAction().Copy != true && !tags.HasFlag(SkillTag.Counter) && skill.SkillId != "archetypes.skill.corrosive_trap");
        ApplyAilments(request, skill, configuration, enemy, ailmentSource ?? [], damage, critical, random, tick, source, events);
        if (value > 0 && masteryArmorBreakStacks > 0)
        {
            enemy.ArmorBreakStacks = Math.Min(runtime.ArmorBreakMaximum,
                enemy.ArmorBreakStacks + masteryArmorBreakStacks);
            enemy.ArmorBreakUntil = tick + 100;
        }
        events.Add(Event(tick, eventKind, "hero", enemy.EntityId, value,
            source, enemy.Position,
            $"skill:{skill.SkillId}|damage:{damage.Compact}|range:{AreaRules.EngagementRange(skill)}|supports:{(ulong)configuration.Supports}{(skill.AdditionalTags.HasFlag(SkillTag.Counter) ? "|counter" : string.Empty)}{(critical ? "|critical" : string.Empty)}"));
        if (enemy.Life == 0)
            events.Add(Event(tick, SpatialEventKind.EnemyDefeated, "hero", enemy.EntityId, 0,
                source, enemy.Position, enemy.Profile.StableId));
        if (beforeShieldLink > 0 && enemy.Life == 0 && !settlementKilled && skill.Role != SkillRole.DamageOverTime &&
            damage.Physical > 0 && damage.Total == damage.Physical && MasteryRuntime.Has(profile, "物理", 4))
            request.Reactions?.EnqueueBurst(new Combat.PendingAreaBurst(enemy.Position, enemy.MaximumLife / 10,
                SkillDamageType.Physical, 3_000, "physical-corpse-burst"));
        if (!triggered && value > 0 && tags.HasFlag(SkillTag.Attack) && ReactionConfiguration(request, Combat.ReactionState.Answer) is { } answer)
            request.Reactions?.Schedule(answer, enemy.EntityId, 44);
        if (!triggered && value > 0 && tags.HasFlag(SkillTag.Attack)) ScheduleSupportedReactions(request, hero, enemy.EntityId, attack: true);
        if (!triggered && value > 0 && skill.Role != SkillRole.DamageOverTime && enemy.Rarity is EnemyRarity.Rare or EnemyRarity.Boss)
            request.Actions?.TryUnity(tick);
    }

    private static Point ResolveEnemies(
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
        int fortificationLayers, BattleArmy army, Action<Point> afterEnemyAction)
    {
        foreach (EnemyUnit enemy in enemies.Where(enemy => enemy.Life > 0).ToArray())
        {
            if (enemy.Life <= 0) continue; // An earlier counterattack may have killed this snapshot member.
            hero.UpdateSheet(request.RuneFields?.Apply(request.Build.Sheet, heroPosition) ?? request.Build.Sheet);
            if (tick < enemy.FrozenUntil || tick < enemy.ParalyzedUntil || tick < enemy.StunnedUntilTick) continue;
            EnemySkillProfile activeSkill = enemy.Profile.EffectiveSkills[enemy.ActionSequence % enemy.Profile.EffectiveSkills.Count];
            if (army.ReceiveEnemyAction(enemy, activeSkill, heroPosition, hero.IsAlive, request, random, tick, events)) continue;
            if (request.Actions?.UntargetableUntil > tick && enemy.TelegraphTarget is null) continue;
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

            if (tick < enemy.NextActionTick || enemy.TelegraphTarget is null && distance > (long)range * range ||
                !hero.IsAlive && !army.MercenaryAlive)
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
                    enemy.Position, heroPosition, $"{activeSkill.DisplayName}|{activeSkill.Telegraph}|{activeSkill.DamageType}|True|until:{(tick + 12) * TickMilliseconds}") with
                { Presentation = new($"{enemy.EntityId}.warning.{tick}", tick * TickMilliseconds,
                    (tick + 12) * TickMilliseconds, "circle", 2_000, new(0, 0), []) });
                continue;
            }
            Point impactPoint = enemy.TelegraphTarget ?? heroPosition;
            bool areaAvoided = areaAttack && !InRange(heroPosition, impactPoint, 2_000);
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
                    ally.Life = Math.Min(ally.MaximumLife, ally.Life + ScaleCombatValue(Math.Max(1, ally.MaximumLife * 4 / 100), AilmentMasteryRules.LifeRecoveryMultiplier(request.Build.PassiveProfile ?? PassiveModifiers.Empty, ally.Ailments)));
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
                TargetArmor: 0,
                TargetEvasion: activeSkill.IsSpell ? 0 : hero.MasteryEvasion(tick),
                Accuracy: enemy.Profile.Accuracy,
                CriticalMultiplierBasisPoints: hero.IncomingMasteryCriticalMultiplier(15_000),
                LuckyTargetEvasion: hero.LuckyEvasion,
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
            bool substituted = hit.Hit && !areaAvoided && request.Build.Ascendancy?.Has("core.ascendancy.phantom_master.sustain.core") == true &&
                request.Actions?.TrySubstitute(heroPosition, tick) == true;
            if (substituted)
            {
                damage = 0;
                events.Add(Event(tick, SpatialEventKind.Guard, "hero", enemy.EntityId, 0, heroPosition, enemy.Position, "phantom-substitute"));
            }
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
            if (damage > 0)
            {
                int armor = HeroBaseArmor(request, hero, tick);
                armor = hero.MasteryArmor(armor, damage, activeSkill.DamageType, tick);
                damage = ScaleCombatValue(damage, 10_000 - CombatRules.ArmorReduction(armor, damage));
            }
            damage = ScaleCombatValue(damage, 10_000 + hero.HarmfulStatus.Effect(Ailment.Shock) + hero.HarmfulStatus.Curses.Effect("vulnerability", tick));
            damage = ScaleCombatValue(damage, Math.Max(0, 10_000 - enemy.Curses.Effect("archetypes.skill.enfeeble_hex", tick)));
            damage = ScaleCombatValue(damage, request.Auras?.IncomingHitMultiplier ?? 10_000);
            damage = ScaleCombatValue(damage, request.RuneFields?.HitMultiplier(heroPosition) ?? 10_000);
            damage = checked((int)((long)damage * request.IncomingHitBasisPoints / 10_000));
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
            if (!hit.Hit && !spell && !areaAvoided)
            {
                request.EquipmentRuntime?.Evaded();
                hero.ObserveEvade(tick);
                ScheduleUnarmedCounter(request, hero, enemy.EntityId, tick, true);
            }
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

            }
            int attackBlock = WarriorAscendancyRules.AttackBlockChanceBasisPoints(
                request.Build.BlockChanceBasisPoints, ascendancy.Profile, request.Build.HasShield);
            int attackBlockMaximum = WarriorAscendancyRules.AttackBlockMaximumBasisPoints(
                request.Build.Sheet.MaximumBlockChanceBasisPoints, ascendancy.Profile, request.Build.HasShield);
            int finalAttackBlock = Math.Clamp(attackBlock, 0, attackBlockMaximum);
            int blockChance = spell
                ? WarriorAscendancyRules.SpellBlockChanceBasisPoints(request.Build.Sheet.SpellBlockChanceBasisPoints,
                    BlockMasteryRules.Has(request.Build.PassiveProfile ?? PassiveModifiers.Empty, 2) ? attackBlock : finalAttackBlock, ascendancy.Profile, request.Build.HasShield, BlockMasteryRules.Has(request.Build.PassiveProfile ?? PassiveModifiers.Empty, 2) ? 5000 : 0) + request.Guard!.SpellBlockBonus(tick)
                : attackBlock;
            int blockCap = spell
                ? request.Build.Sheet.MaximumSpellBlockChanceBasisPoints
                : attackBlockMaximum;
            blockChance = BlockMasteryRules.Chance(request.Build.PassiveProfile ?? PassiveModifiers.Empty, blockChance);
            bool blocked = !areaAvoided && damage > 0 && blockCap > 0 && (request.Conditions!.GuaranteedBlock(request.Build.PassiveProfile ?? PassiveModifiers.Empty, tick) ||
                blockChance > 0 && random.NextUInt() % 10_000 < Math.Min(9000, Math.Min(blockCap, blockChance)));
            if (blocked)
            {
                request.EquipmentRuntime?.Blocked(tick, spell);
                request.Conditions!.Blocked(request.Build.PassiveProfile ?? PassiveModifiers.Empty, tick);
                ScheduleUnarmedCounter(request, hero, enemy.EntityId, tick, !spell);
                damage = ScaleCombatValue(damage, BlockMasteryRules.RemainingMultiplier(request.Build.PassiveProfile ?? PassiveModifiers.Empty,
                    spell && ascendancy.Has(WarriorNodeIds.BastionSpellBlockCore) ? ascendancy.IncomingHitMultiplier(true, true, tick) : 0));
                events.Add(Event(tick, SpatialEventKind.Block, "hero", enemy.EntityId, 0,
                    heroPosition, enemy.Position, spell ? "spell" : "attack"));
                int counterMultiplier = spell ? 10_000 : ascendancy.OnAttackBlock();
                if (shieldCounter is not null && shieldCounterConfiguration is not null &&
                    tick >= shieldCounterReadyTick)
                {
                    request.Reactions?.Enqueue(new(shieldCounter.SkillId, enemy.EntityId, counterMultiplier,
                        shieldCounter with { AreaIncreasedBasisPoints = shieldCounter.AreaIncreasedBasisPoints + (ascendancy.Has(WarriorNodeIds.BastionCounterSmall) ? 2_000 : 0) },
                        ascendancy.Has(WarriorNodeIds.BastionCounterSmall) ? 4_000 : 0, counterMultiplier >= 28_000));
                    int counterCooldown = ascendancy.Has(WarriorNodeIds.BastionCounterSmall)
                        ? shieldCounter.CooldownTicks * 10_000 / 12_500 : shieldCounter.CooldownTicks;
                    shieldCounterReadyTick = tick + Math.Max(1, counterCooldown);
                }
            }
            else if (damage > 0)
            {
                damage = Math.Max(1, damage * ascendancy.IncomingHitMultiplier(spell, false, tick) / 10_000);
                damage = ScaleCombatValue(damage, BlockMasteryRules.UnblockedMultiplier(request.Build.PassiveProfile ?? PassiveModifiers.Empty, tick, request.Conditions!.BlockRecentUntil));
            }
            if (spell && damage > 0)
            {
                var suppressionProfile = request.Build.PassiveProfile ?? PassiveModifiers.Empty;
                int chance = request.Build.Sheet.SpellSuppressionBasisPoints + (request.EquipmentRuntime?.SuppressionBonus(tick) ?? 0);
                if (chance > 0 && SuppressionMasteryRules.Roll(suppressionProfile, chance, random))
                {
                    suppressed = true;
                    damage = CombatRules.SuppressedDamage(damage, request.Build.Sheet.SpellSuppressionEffectBasisPoints);
                    request.Conditions?.Suppressed(tick);
                    events.Add(Event(tick, SpatialEventKind.Guard, "hero", enemy.EntityId, 0, heroPosition, enemy.Position, "spell_suppression"));
                }
                else damage = ScaleCombatValue(damage, SuppressionMasteryRules.UnsuppressedMultiplier(suppressionProfile));
            }
            if (activeSkill.Kind is EnemySkillKind.GroundHazard or EnemySkillKind.Artillery)
                hazards.Add(new(enemy.EntityId, impactPoint, 1_800, Math.Max(1, hit.PreMitigationPhysicalDamage / divisor / 2), tick + 10, tick + 90,
                    activeSkill.DamageType));
            if (request.ExtraBossPhase && enemy.Boss && enemy.BossPhase > 0)
                hazards.Add(new(enemy.EntityId, impactPoint, 2_000, Math.Max(1, hit.PreMitigationPhysicalDamage / divisor / 2), tick + 20, tick + 31,
                    activeSkill.DamageType));
            int statusGeneration = hero.HarmfulStatus.Generation;
            int resourcesBeforeHit = hero.Life + hero.Shield;
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
                damage = ScaleCombatValue(damage, ResistanceMasteryRules.IncomingMultiplier(hero.Sheet, request.Build.PassiveProfile ?? PassiveModifiers.Empty, activeSkill.DamageType, true));
                damage = ScaleCombatValue(damage, request.Buffs?.IncomingHitMultiplier(!request.Build.HasUsableWeapon, tick) ?? 10_000);
                damage = ScaleCombatValue(damage, MasteryRuntime.IncomingResourceMultiplier(request.Build.PassiveProfile ?? PassiveModifiers.Empty, hero, true));
                damage = ScaleCombatValue(damage, DamageOverTimeMasteryRules.IncomingHitMultiplier(
                    request.Build.PassiveProfile ?? PassiveModifiers.Empty, enemy.Ailments));
                damage = ScaleCombatValue(damage, ActionMasteryRules.IncomingHitMultiplier(
                    request.Build.PassiveProfile ?? PassiveModifiers.Empty, request.Actions?.IsChanneling(tick * TickMilliseconds) == true));
                damage = ScaleCombatValue(damage, hero.RecentEvadeHitMultiplier(tick));
                if (enemy.Ailments.Count(Ailment.Poison) >= 10 && MasteryRuntime.Has(request.Build.PassiveProfile ?? PassiveModifiers.Empty, "中毒", 5))
                    damage = ScaleCombatValue(damage, 8_500);
                damage = ScaleCombatValue(damage, VoidDebuffMasteryRules.IncomingHitMultiplier(request.Build.PassiveProfile ?? PassiveModifiers.Empty, enemy.Ailments, tick));
                damage = request.Guard?.Absorb(damage, activeSkill.DamageType, tick) ?? damage;
                if (request.EquipmentRuntime is { } equipment)
                    damage = equipment.ApplyEnemyDamage(hero, damage, true, tick, request.VirtueVice, blocked);
                else hero.ApplyDamage(damage, tick);
                if (damage > 0) request.Conditions?.EnemyHit(activeSkill.DamageType, tick);
                if (damage > 0) hero.ObserveEnemyHit(!spell, tick);
                if (damage > 0 && !spell && !blocked) ascendancy.OnUnblockedAttack(tick);
            }
            if (suppressed && !areaAvoided && !substituted) request.EquipmentRuntime?.Suppressed(tick, hero);
            if (!areaAvoided && (!blocked || damage > 0) && !substituted && hero.HarmfulStatus.Generation == statusGeneration)
                ApplyEnemyStatus(request, hero, enemy, activeSkill, ScaleCombatValue(hit.WeaponRoll / divisor, skillMultiplier), damage,
                    hit.Critical, random, tick, heroPosition, events);

            string attackDetail = $"{activeSkill.DisplayName}|{activeSkill.Telegraph}|{activeSkill.DamageType}|{activeSkill.Avoidable}|{(spell ? "spell" : "attack")}|result:{(areaAvoided ? "dodge" : blocked ? "block" : "hit")}";
            events.Add(Event(tick, SpatialEventKind.EnemyAttack, enemy.EntityId, "hero", damage,
                enemy.Position, impactPoint, attackDetail));
            if (hero.IsAlive && !substituted && !areaAvoided)
                ScheduleSupportedReactions(request, hero, enemy.EntityId, block: blocked, hitDamage: damage);
            if (hero.IsAlive && hit.Hit && !substituted && !areaAvoided && hero.HarmfulStatus.Generation == statusGeneration)
            {
                request.Guard?.EnemyHit(tick);
                if (spell) request.Guard?.EnemySpellHit(enemy.EntityId, blocked, tick);
            }
            if (hero.IsAlive && !substituted && damage > 0 && request.EquipmentRuntime?.LastEnemyShieldLoss > 0)
            {
                if (ReactionConfiguration(request, Combat.ReactionState.Mirror) is { } mirror)
                    request.Reactions?.Schedule(mirror, enemy.EntityId, 30);
                if (request.EquipmentRuntime.LastEnemyHitBrokeShield && MeleeEquipped(request.Build) && ReactionConfiguration(request, Combat.ReactionState.ShieldBreak) is { } broken)
                    request.Reactions?.Schedule(broken, enemy.EntityId, 100);
            }
            int interval = Math.Max(8, checked((20_000 + attacksPerSecond - 1) / attacksPerSecond));
            interval = Math.Max(8, checked(interval * activeSkill.CooldownMultiplierBasisPoints / 10_000));
            enemy.NextActionTick = tick + interval;
            enemy.ActionSequence++;
            if (hero.IsAlive && request.Actions?.TrySwap(heroPosition, tick,
                    Math.Max(0, resourcesBeforeHit - hero.Life - hero.Shield), hero.MaximumLife + hero.MaximumShield) is { } swapDestination)
            {
                events.Add(Event(tick, SpatialEventKind.HeroMoved, "hero", "", 0, heroPosition, swapDestination, "phantom-swap"));
                heroPosition = swapDestination;
            }
            afterEnemyAction(heroPosition);
        }
        hero.UpdateSheet(request.RuneFields?.Apply(request.Build.Sheet, heroPosition) ?? request.Build.Sheet);
        return heroPosition;
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
        if (type == SkillDamageType.Void) resistance -= CombatRules.CorrosionResistanceReduction(enemy.Ailments.Stack(Ailment.Erosion, enemy.CurrentTick), 10);
        return Math.Clamp(resistance, CombatRules.MinimumResistance, 7_500) -
            (penetrate ? (request.Build.CombatEquipment?.Penetration(type) ?? 0) +
                (type == SkillDamageType.Void && MasteryRuntime.Has(request.Build.PassiveProfile ?? PassiveModifiers.Empty, "虚空", 5) ? 2_000 : 0) : 0);
    }

    private static int HeroBaseArmor(NodeCombatRequest request, ResourceState hero, int tick)
    {
        int increased = hero.Sheet.IncreasedArmorBasisPoints + hero.Sheet.ElementalOverflowDefenseIncrease +
            (request.EquipmentRuntime?.ArmorIncrease(request.EquipmentRuntime.NearbyEnemyCount?.Invoke() ?? 0) ?? 0);
        return CombatRules.ApplyMore(CombatRules.ApplyIncreased(hero.Sheet.Equipment.Armor, increased),
            [hero.Sheet.ArmorMultiplierBasisPoints, request.AscendancyRuntime?.ArmorMultiplier(tick) ?? 10_000]);
    }
    private static int HeroCurrentArmor(NodeCombatRequest request, ResourceState hero, int tick) =>
        hero.MasteryArmor(HeroBaseArmor(request, hero, tick), 0, EnemyDamageType.Physical, tick);

    private static int ActionDelay(TeamBuild build, int baseTicks, SkillTag tags = SkillTag.Attack)
        => CombatSkillRules.ActionDelay(build, baseTicks, tags);

    private static bool TryPayEquipmentCost(NodeCombatRequest request, ResourceState hero, ResolvedSkill skill)
    {
        SkillTag tags = SkillDefinitions.Get(skill.SkillId).Tags | skill.AdditionalTags;
        bool triggered = tags.HasFlag(SkillTag.Trigger);
        int costMultiplier = ActionMasteryRules.SkillCostMultiplier(
            request.Build.PassiveProfile ?? PassiveModifiers.Empty, triggered);
        skill = skill with
        {
            LifeCost = ScaleCombatValue(skill.LifeCost, costMultiplier),
            ManaCost = ScaleCombatValue(skill.ManaCost, costMultiplier)
        };
        var p = request.Build.PassiveProfile ?? PassiveModifiers.Empty;
        int tick = request.Reactions?.Tick ?? 0;
        bool highMana = hero.Mana * 10L >= hero.MaximumMana * 8L;
        skill = request.SpellCasts?.Quote(skill, p, tick) ?? skill;
        if (!CanPay(request, hero, skill)) return false;
        if (tags.HasFlag(SkillTag.Channelling))
        {
            if (request.ChannelCosts?.TryPay(hero, skill, out int paid) != true) return false;
            request.EquipmentRuntime?.BeginAction(skill.SkillId, hero.LastSkillLifePaid, hero.LastSkillManaPaid, false, request.VirtueVice);
            request.Reactions?.Begin(request.EquipmentRuntime?.ActionId ?? "", skill.SkillId, request.Guard,
                skill.LifeCost == 0 && hero.LastSpellFullyFunded ? 15_000 : 10_000, hero.LastSkillPaymentMultiplier);
            request.SpellCasts?.Started(request.EquipmentRuntime?.ActionId ?? "", skill.SkillId, tags, p, tick, highMana);
            return true;
        }
        if (!CombatSkillRules.TryPay(hero, skill)) return false;
        request.EquipmentRuntime?.BeginAction(skill.SkillId, hero.LastSkillLifePaid, hero.LastSkillManaPaid,
            triggered, request.VirtueVice);
        request.Reactions?.Begin(request.EquipmentRuntime?.ActionId ?? "", skill.SkillId, request.Guard,
            skill.LifeCost == 0 && hero.LastSpellFullyFunded ? 15_000 : 10_000, hero.LastSkillPaymentMultiplier);
        request.SpellCasts?.Started(request.EquipmentRuntime?.ActionId ?? "", skill.SkillId, tags, p, tick, highMana);
        return true;
    }

    private static bool TryPayEquipmentCost(NodeCombatRequest request, ResourceState hero, SkillUseProfile skill)
    {
        int multiplier = ScaleCombatValue(request.EquipmentRuntime?.Has("怒节同契") == true ? 12_000 : 10_000, request.Auras?.SkillCostMultiplier ?? 10_000);
        skill = skill with { LifeCost = ScaleCombatValue(skill.LifeCost, multiplier), ManaCost = ScaleCombatValue(skill.ManaCost, multiplier) };
        bool spell = SkillDefinitions.Get(skill.SkillId).Tags.HasFlag(SkillTag.Spell);
        if (!SkillRules.TryPaySkillCost(hero, skill)) return false;
        request.EquipmentRuntime?.BeginAction(skill.SkillId, hero.LastSkillLifePaid, hero.LastSkillManaPaid, false, request.VirtueVice);
        request.Reactions?.Begin(request.EquipmentRuntime?.ActionId ?? "", skill.SkillId, request.Guard,
            spell && skill.LifeCost == 0 && hero.LastSpellFullyFunded ? 15_000 : 10_000, hero.LastSkillPaymentMultiplier);
        request.SpellCasts?.Started(request.EquipmentRuntime?.ActionId ?? "", skill.SkillId, SkillDefinitions.Get(skill.SkillId).Tags, request.Build.PassiveProfile ?? PassiveModifiers.Empty, request.Reactions?.Tick ?? 0, false);
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
        if (result.AdditionalTags.HasFlag(SkillTag.Counter) && UnarmedRules.Has(profile, "counter"))
            result = result with { CooldownTicks = Math.Max(1, (int)Math.Ceiling(result.CooldownTicks * 10_000d / 12_000)) };
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
        EquipmentCombatRuntime equipment, Pcg32 random, NodeCombatRequest request)
    {
        bool resetMovement = false;
        EnemyUnit[] enemyUnits = enemies.ToArray();
        foreach (EnemyUnit enemy in enemyUnits.Where(item => item.Life <= 0 && !item.KillCharged))
        {
            enemy.KillCharged = true;
            request.Conditions?.Killed(tick);
            var passives = request.Build.PassiveProfile ?? PassiveModifiers.Empty;
            if (!enemy.MasteryDeathSuppressed && tick < enemy.OriginalStunUntil && MasteryRuntime.Has(passives, "眩晕", 4))
                foreach (var target in enemyUnits.Where(candidate => candidate.Life > 0 && InRange(enemy.Position, candidate.Position, 4_000))
                    .OrderBy(candidate => Point.DistanceSquared(enemy.Position, candidate.Position)).ThenBy(candidate => candidate.EntityId, StringComparer.Ordinal).Take(5))
                    if (target.Profile.AilmentAvoidanceBasisPoints <= 0 || random.NextBasisPoints() >= target.Profile.AilmentAvoidanceBasisPoints)
                        ApplyStun(request, target, tick, enemy.Position, events, true);
            if (!enemy.MasteryDeathSuppressed && ElementalControlMasteryRules.CanSpreadChill(
                    passives, enemy.ChillEffect, tick, enemy.ImpairedUntilTick, enemy.PropagatedChill))
                foreach (EnemyUnit target in enemyUnits.Where(candidate => candidate.Life > 0 &&
                             InRange(enemy.Position, candidate.Position, 5_000) &&
                             (candidate.Profile.AilmentAvoidanceBasisPoints <= 0 ||
                              random.NextBasisPoints() >= candidate.Profile.AilmentAvoidanceBasisPoints))
                         .OrderBy(candidate => Point.DistanceSquared(enemy.Position, candidate.Position))
                         .ThenBy(candidate => candidate.EntityId, StringComparer.Ordinal).Take(8))
                {
                    target.ChillEffect = Math.Max(target.ChillEffect, enemy.ChillEffect);
                    target.ImpairedUntilTick = Math.Max(target.ImpairedUntilTick, enemy.ImpairedUntilTick);
                    target.PropagatedChill = true;
                    events.Add(Event(tick, SpatialEventKind.Ailment, "hero", target.EntityId, enemy.ChillEffect,
                        enemy.Position, target.Position, "mastery:chill-spread"));
                }
            int frozenExplosion = enemy.MasteryDeathSuppressed ? 0 : ElementalControlMasteryRules.FrozenExplosionDamage(
                passives, enemy.MaximumLife, tick < enemy.FrozenUntil);
            if (frozenExplosion > 0)
                foreach (EnemyUnit target in enemyUnits.Where(candidate => candidate.Life > 0 &&
                             InRange(enemy.Position, candidate.Position, 3_000)).ToArray())
                {
                    int resistance = EnemyResistance(target, request, SkillDamageType.Cold, penetrate: false);
                    int damage = Math.Min(target.Life, CombatRules.MitigateByResistance(
                        frozenExplosion, resistance));
                    target.Life -= damage;
                    if (target.Life == 0)
                    {
                        target.MasteryDeathSuppressed = true;
                        events.Add(Event(tick, SpatialEventKind.EnemyDefeated, "hero", target.EntityId, 0,
                            enemy.Position, target.Position, target.Profile.StableId));
                    }
                    events.Add(Event(tick, SpatialEventKind.SkillEffect, "hero", target.EntityId, damage,
                        enemy.Position, target.Position, "mastery:frozen-explosion"));
                }
            foreach (var kind in new[] { Ailment.Poison, Ailment.Bleed, Ailment.Ignite })
                if (MasteryRuntime.Has(passives, kind == Ailment.Poison ? "中毒" : kind == Ailment.Bleed ? "流血" : "点燃", 4) && enemy.Ailments.Count(kind) > 0)
                    foreach (var target in enemies.Where(candidate => candidate.Life > 0 && InRange(enemy.Position, candidate.Position, 4_000))
                        .OrderBy(candidate => Point.DistanceSquared(enemy.Position, candidate.Position)).ThenBy(candidate => candidate.EntityId, StringComparer.Ordinal).Take(5))
                    {
                        AilmentMasteryRules.Configure(target.Ailments, passives, runtime.TwoBleeds, ElementalRules.Has(request.Build.Ascendancy, "fire", "core"));
                        enemy.Ailments.SpreadTo(target.Ailments, kind,
                            () => target.Profile.AilmentAvoidanceBasisPoints <= 0 || random.NextBasisPoints() >= target.Profile.AilmentAvoidanceBasisPoints,
                            kind == Ailment.Poison ? 5 : 1, VoidDebuffed(enemy, tick));
                    }
            if (MasteryRuntime.Has(passives, "侵蚀_凋零", 4))
                foreach (var target in enemyUnits.Where(candidate => candidate.Life > 0 && InRange(enemy.Position, candidate.Position, 4_000))
                    .OrderBy(candidate => Point.DistanceSquared(enemy.Position, candidate.Position)).ThenBy(candidate => candidate.EntityId, StringComparer.Ordinal).Take(5))
                    enemy.Ailments.SpreadDebuffsTo(target.Ailments, passives, tick,
                        () => target.Profile.AilmentAvoidanceBasisPoints <= 0 || random.NextBasisPoints() >= target.Profile.AilmentAvoidanceBasisPoints);
            if (enemy.Summoned) continue;
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
                    if (spread is not null)
                    {
                        AilmentMasteryRules.Configure(spread.Ailments, passives, runtime.TwoBleeds, ElementalRules.Has(request.Build.Ascendancy, "fire", "core"));
                        enemy.Ailments.SpreadTo(spread.Ailments, Ailment.Bleed,
                            () => spread.Profile.AilmentAvoidanceBasisPoints <= 0 || random.NextBasisPoints() >= spread.Profile.AilmentAvoidanceBasisPoints);
                    }
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
            CaptureVirtueVice(virtueVice), hero.Overcharge, hero.MaximumOvercharge));
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

    private static bool CanPay(NodeCombatRequest request, ResourceState hero, ResolvedSkill skill)
    {
        skill = request.SpellCasts?.Quote(skill, request.Build.PassiveProfile ?? PassiveModifiers.Empty, request.Reactions?.Tick ?? 0) ?? skill;
        return
        SkillDefinitions.Get(skill.SkillId).Tags.HasFlag(SkillTag.Channelling)
            ? request.ChannelCosts?.CanPay(hero, skill) == true
            : hero.CanPaySkillCost(skill.LifeCost, skill.ManaCost, SkillDefinitions.Get(skill.SkillId).Tags,
                extraShield: Combat.GuardState.ShieldCost(skill.SkillId, hero.MaximumShield), waiveMana: skill.WaiveManaCost);
    }

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
        if (request.Objective is { } objective && (objective.InteractionTicks <= 0 || objective.HazardIntervalTicks <= 0 ||
            objective.HazardDamage < 0 || request.MaximumTicks <= 0))
            throw new ArgumentOutOfRangeException(nameof(request));
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
        public int CurrentTick, ShockEffect, ShockUntil, ChillEffect, FrozenUntil, ParalyzedUntil, Paralysis, ParalysisLastTick, ArmorBreakUntil;
        public int ColdPursuitUntil, ParalysisPursuitUntil;
        public int LastColdGroundChillTick = int.MinValue / 2;
        public bool PropagatedChill, MasteryDeathSuppressed;
        public int ArmorBreakStacks { get; set; }
        public int ShockStacks => ShockEffect > 0 ? 1 : 0;
        public int ImpairedUntilTick { get; set; }
        public int BossPhase { get; set; }
        public int LastTelegraphTick { get; set; } = int.MinValue;
        public int RuptureStacks { get; set; }
        public int StunnedUntilTick { get; set; }
        public int OriginalStunUntil, StunPursuitUntil, StunArmorBreakReadyTick;
        public int StunImmuneUntilTick { get; set; }
        public bool KillCharged { get; set; }
        public int ActionSequence { get; set; }
    }

    private sealed record PendingAftershock(int ImpactTick, string TargetId, int ActualHitDamage, Point Origin, string Detail = "linebreaker:aftershock");
    private sealed record EnemyHazard(string Source, Point Position, int Radius, int Damage, int Start, int Expires,
        EnemyDamageType DamageType);
}
