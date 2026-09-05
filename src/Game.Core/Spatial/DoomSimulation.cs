using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Skills;
using GameForWork.Core.SkillCatalog;
using GameForWork.Core.Simulation;

namespace GameForWork.Core.Spatial;

public sealed partial class SpatialCombatRunner
{
    private static bool ApplyDoomCurse(NodeCombatRequest request, SkillConfiguration config, EnemyUnit target, int expires, int tick)
    {
        int effect = CombatRules.ApplyIncreased(ActiveSkillCatalog.Interpolate(1_000, 2_000, config.Level, false),
            request.Build.CombatEquipment?.Value(ItemModifierKind.IncreasedCurseEffectBasisPoints) ?? 0);
        return target.Curses.Apply(config.SkillId, effect, 0, expires + 1,
            1 + (request.Build.CombatEquipment?.Value(ItemModifierKind.AdditionalCurseMaximum) ?? 0), tick, config.Priority);
    }
    private static void AdvanceDoom(PersistentArea area, IList<PersistentArea> areas, IReadOnlyCollection<EnemyUnit> enemies,
        ResourceState hero, Pcg32 random, int tick, ICollection<SpatialEvent> events)
    {
        var target = enemies.FirstOrDefault(enemy => enemy.EntityId == area.Target);
        if (target is null) { areas.Remove(area); return; }
        if (target.Life > 0 && tick < area.Expires)
        {
            if (target.Curses.Effect(area.Skill.SkillId, tick) == 0) areas.Remove(area);
            return;
        }
        int interval = 20 - Math.Clamp(area.Configuration.Quality, 0, 20) / 5;
        int stacks = Math.Min(5, area.InheritedStacks + (tick - area.Created) / interval);
        areas.Remove(area);
        if (target.Life == 0 && !area.Propagated && tick < area.Expires)
        {
            int available = Math.Max(0, 3 - areas.Count(item => item.Skill.SkillId == area.Skill.SkillId));
            foreach (var next in enemies.Where(enemy => enemy.Life > 0 && InRange(target.Position, enemy.Position, 4_000) &&
                    !areas.Any(item => item.Skill.SkillId == area.Skill.SkillId && item.Target == enemy.EntityId))
                .OrderBy(enemy => Point.DistanceSquared(target.Position, enemy.Position)).ThenBy(enemy => enemy.EntityId, StringComparer.Ordinal).Take(Math.Min(2, available)).ToArray())
            {
                if (!ApplyDoomCurse(area.Request, area.Configuration, next, area.Expires, tick)) continue;
                areas.Add(new(area.Request, area.Skill, area.Configuration, next.Position, next.Position, next.EntityId,
                    area.Radius, tick, area.Expires - tick, 0, area.Multiplier) { InheritedStacks = stacks, Propagated = true });
                events.Add(Event(tick, SpatialEventKind.SkillEffect, target.EntityId, next.EntityId, stacks, target.Position, next.Position, "doom-propagated"));
            }
        }
        var equipment = area.Request.EquipmentRuntime!;
        equipment.InAction(equipment.CreateTriggeredAction(target.EntityId), () =>
        {
            foreach (var enemy in enemies.Where(enemy => enemy.Life > 0 && InRange(target.Position, enemy.Position, area.Radius)).ToArray())
                ResolveHeroHit(area.Request, area.Skill with { Role = SkillRole.Clear, Ailment = Ailment.Wither, AilmentChanceBasisPoints = 10_000 },
                    area.Configuration, enemy, hero, random, tick, target.Position, ScaleCombatValue(area.Multiplier, 10_000 + stacks * 2_000), events);
        });
        target.Curses.Remove(area.Skill.SkillId);
        events.Add(Event(tick, SpatialEventKind.SkillEffect, "hero", target.EntityId, stacks, target.Position, target.Position, "doom-detonated"));
    }
}
