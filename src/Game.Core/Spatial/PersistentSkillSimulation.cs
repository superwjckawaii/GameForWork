using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Campaign.Progression;
using GameForWork.Core.SkillCatalog;
using GameForWork.Core.Skills;
using GameForWork.Core.Simulation;

namespace GameForWork.Core.Spatial;

public sealed partial class SpatialCombatRunner
{
    private sealed class PersistentArea(NodeCombatRequest request, ResolvedSkill skill, SkillConfiguration configuration,
        Point start, Point end, string target, int radius, int created, int duration, int interval, int multiplier)
    {
        public NodeCombatRequest Request { get; } = request;
        public ResolvedSkill Skill { get; } = skill;
        public SkillConfiguration Configuration { get; } = configuration;
        public Point Start { get; } = start;
        public Point End { get; } = end;
        public string Target { get; } = target;
        public int Radius { get; } = radius;
        public int Created { get; } = created;
        public int Expires { get; set; } = created + duration;
        public int Interval { get; } = interval;
        public int NextPulse { get; set; } = created + interval;
        public int Multiplier { get; } = multiplier;
        public bool Armed { get; set; }
        public DamageBreakdown? DamagePerSecond { get; set; }
        public DamageBreakdown? RareDamagePerSecond { get; set; }
    }

    private static bool CreatePersistentArea(NodeCombatRequest request, ResolvedSkill skill, SkillConfiguration configuration,
        EnemyUnit target, IReadOnlyCollection<EnemyUnit> enemies, ResourceState hero, Pcg32 random, int tick,
        Point origin, Point destination, int multiplier, IList<PersistentArea> areas, ICollection<SpatialEvent> events)
    {
        string id = skill.SkillId;
        if (id is not (SkillIds.FlameStep or SkillIds.VoidDecayField or SkillIds.StormBrand or
            "archetypes.skill.void_rift" or "archetypes.skill.corrosive_trap" or "archetypes.skill.thunderstorm")) return false;
        bool brand = id == SkillIds.StormBrand, thunder = id == "archetypes.skill.thunderstorm";
        bool trap = id == "archetypes.skill.corrosive_trap", flame = id == SkillIds.FlameStep;
        if (brand) target = enemies.Where(enemy => enemy.Life > 0 && InRange(origin, enemy.Position, skill.RangeRaw) &&
                !areas.Any(area => area.Skill.SkillId == id && area.Target == enemy.EntityId && area.Expires >= tick))
            .OrderBy(enemy => Point.DistanceSquared(origin, enemy.Position)).FirstOrDefault() ?? target;
        int duration = flame ? 50 : brand ? 120 : thunder ? 100 : trap ? 160 : id == SkillIds.VoidDecayField ?
            CombatRules.ApplyIncreased(120, configuration.Quality * 100) : 100;
        int radius = flame ? 750 : brand ? 5_000 : thunder ? 4_000 : id == SkillIds.VoidDecayField ? 3_500 : 3_000;
        if (id == "archetypes.skill.void_rift") radius = CombatRules.ApplyIncreased(radius, configuration.Quality * 100);
        int interval = brand ? 15 - Math.Clamp(configuration.Quality, 0, 20) / 10 :
            thunder ? 10 - Math.Clamp(configuration.Quality, 0, 20) / 20 : 0;
        int maximum = brand ? 3 : id == "archetypes.skill.void_rift" ? 2 : flame || trap ? int.MaxValue : 1;
        foreach (var old in areas.Where(area => area.Expires < tick || brand && area.Skill.SkillId == id && area.Target == target.EntityId).ToArray()) areas.Remove(old);
        while (areas.Count(area => area.Skill.SkillId == id) >= maximum)
            areas.Remove(areas.First(area => area.Skill.SkillId == id));
        var area = new PersistentArea(request, skill, configuration, flame ? origin : target.Position,
            flame ? destination : target.Position, target.EntityId, radius, tick, duration, interval, multiplier) { Armed = trap };
        if (!brand && !thunder)
        {
            int weapon = request.Build.Weapon.MinimumPhysicalDamage + (int)(random.NextUInt() %
                (uint)Math.Max(1, request.Build.Weapon.MaximumPhysicalDamage - request.Build.Weapon.MinimumPhysicalDamage + 1));
            int raw = trap ? weapon : (int)Math.Round((flame ? 45 : id == SkillIds.VoidDecayField ? 42 : 32) *
                Math.Pow(1.065, Math.Clamp(configuration.Level, 1, 40) - 1), MidpointRounding.AwayFromZero);
            if (trap) raw = ScaleCombatValue(raw, (int)Math.Round(3_500 * Math.Pow(1.05, Math.Clamp(configuration.Level, 1, 40) - 1)));
            area.DamagePerSecond = SnapshotGroundDamage(request, skill, configuration, raw, trap, false, multiplier);
            area.RareDamagePerSecond = SnapshotGroundDamage(request, skill, configuration, raw, trap, true, multiplier);
        }
        if (id == "archetypes.skill.void_rift")
            foreach (var enemy in enemies.Where(enemy => enemy.Life > 0 && InRange(target.Position, enemy.Position, radius)))
                ResolveHeroHit(request, skill with { Role = SkillRole.Clear }, configuration, enemy, hero, random, tick, origin, multiplier, events);
        areas.Add(area);
        events.Add(Event(tick, SpatialEventKind.SkillEffect, "hero", target.EntityId, 0, area.Start, area.End,
            $"skill:{id}|area-created|radius:{radius}|expires:{area.Expires}"));
        return true;
    }

    private static DamageBreakdown SnapshotGroundDamage(NodeCombatRequest request, ResolvedSkill skill, SkillConfiguration configuration,
        int raw, bool weaponDerived, bool rare, int multiplier)
    {
        SkillTag tags = SkillDefinitions.Get(skill.SkillId).Tags & ~SkillTag.Attack;
        skill = skill with { Role = SkillRole.DamageOverTime };
        var modifiers = (request.Build.CombatEquipment?.Modifiers ?? new Dictionary<ItemModifierKind, int>()).ToDictionary(pair => pair.Key, pair => pair.Value);
        if (weaponDerived)
        {
            foreach (var kind in new[] { ItemModifierKind.PhysicalToFireConversionBasisPoints, ItemModifierKind.PhysicalToColdConversionBasisPoints,
                ItemModifierKind.PhysicalToLightningConversionBasisPoints }) modifiers.Remove(kind);
            modifiers[ItemModifierKind.PhysicalToVoidConversionBasisPoints] = 10_000;
        }
        return DamagePacketRules.ResolveMixed(raw, weaponDerived ? SkillDamageType.Physical : skill.DamageType, default,
            configuration.Supports, 0, 0, 0, 0, 0, equipment: modifiers,
            modifiers: CombatSkillRules.OffensiveIncreases(skill, configuration with { Quality = 0 }, request.Build, tags),
            scaleBranch: branch =>
            {
                if (request.Auras?.ExclusiveElement is { } allowed && branch.CurrentType is DamageType.Fire or DamageType.Cold or DamageType.Lightning && branch.CurrentType != allowed) return 0;
                int scaled = CombatSkillRules.ScaleOffensiveDamage(branch.BaseDamage, skill, configuration, request.Build, tags,
                    1, 1, multiplier, targetRareOrBoss: rare, applyIncreased: false, damageHistory: branch.History);
                return ScaleCombatValue(scaled, 10_000 + modifiers.GetValueOrDefault(ItemModifierKind.DamageOverTimeMultiplierBasisPoints));
            }, configuration: configuration, allowAddedHitDamage: false);
    }

    private static void AdvancePersistentAreas(IList<PersistentArea> areas, IReadOnlyCollection<EnemyUnit> enemies,
        ResourceState hero, Pcg32 random, int tick, ICollection<SpatialEvent> events)
    {
        foreach (var area in areas.ToArray())
        {
            if (tick > area.Expires) { areas.Remove(area); continue; }
            bool brand = area.Skill.SkillId == SkillIds.StormBrand;
            var attached = brand ? enemies.FirstOrDefault(enemy => enemy.EntityId == area.Target && enemy.Life > 0) : null;
            if (brand && attached is null) { areas.Remove(area); continue; }
            Point center = attached?.Position ?? area.End;
            if (area.Armed)
            {
                if (!enemies.Any(enemy => enemy.Life > 0 && InRange(area.End, enemy.Position, 2_000))) continue;
                area.Armed = false; area.Expires = tick + 79;
                foreach (var enemy in enemies.Where(enemy => enemy.Life > 0 && InRange(area.End, enemy.Position, area.Radius)))
                    Pulse(enemy, area.Skill with { Role = SkillRole.Clear });
            }
            if (area.Interval > 0)
            {
                if (tick < area.NextPulse) continue;
                area.NextPulse += area.Interval;
                var candidates = enemies.Where(enemy => enemy.Life > 0 && InRange(center, enemy.Position, area.Radius));
                if (brand) candidates = candidates.OrderBy(enemy => enemy != attached)
                    .ThenBy(enemy => Point.DistanceSquared(center, enemy.Position)).Take(2 + Math.Max(0, area.Skill.MaximumChains));
                foreach (var enemy in candidates.ToArray()) Pulse(enemy, area.Skill);
            }
            else
            {
                foreach (var enemy in enemies.Where(enemy => enemy.Life > 0 && OnSegment(enemy.Position, area.Start, area.End, area.Radius)))
                {
                    DamageBreakdown dps = (enemy.Rarity is EnemyRarity.Rare or EnemyRarity.Boss ? area.RareDamagePerSecond : area.DamagePerSecond)!;
                    Apply(DamageType.Physical, dps.Physical); Apply(DamageType.Fire, dps.Fire); Apply(DamageType.Cold, dps.Cold);
                    Apply(DamageType.Lightning, dps.Lightning); Apply(DamageType.Void, dps.Void);
                    if (area.Skill.SkillId == SkillIds.VoidDecayField && (tick - area.Created) % 20 == 0)
                        enemy.Ailments.AddStack(Ailment.Erosion, 1, 5, 120, tick);
                    if (area.Skill.SkillId == "archetypes.skill.corrosive_trap")
                    { enemy.ChillEffect = Math.Max(enemy.ChillEffect, 2_000); enemy.ImpairedUntilTick = tick + 1; }
                    void Apply(DamageType type, int value) => enemy.Ailments.Apply(Ailment.Ground, type, value,
                        TickMilliseconds, 0, area.Skill.SkillId, instanceId: $"{area.Skill.SkillId}:{area.Created}");
                }
            }
            if (tick >= area.Expires) areas.Remove(area);
            void Pulse(EnemyUnit enemy, ResolvedSkill skill)
            {
                var equipment = area.Request.EquipmentRuntime!;
                equipment.InAction(equipment.CreateTriggeredAction(enemy.EntityId), () =>
                    ResolveHeroHit(area.Request, skill, area.Configuration, enemy, hero, random, tick, center,
                        area.Multiplier, events, eventKind: brand ? SpatialEventKind.StormBrand : SpatialEventKind.SkillEffect));
                events.Add(Event(tick, SpatialEventKind.SkillEffect, "hero", enemy.EntityId, 0, center, enemy.Position,
                    $"skill:{skill.SkillId}|area-pulse|created:{area.Created}"));
            }
        }
    }
}
