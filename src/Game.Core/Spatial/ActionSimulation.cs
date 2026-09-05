using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Combat;
using GameForWork.Core.SkillCatalog;
using GameForWork.Core.Simulation;
using GameForWork.Core.Skills;

namespace GameForWork.Core.Spatial;

public sealed partial class SpatialCombatRunner
{
    private static void ResolveCopies(NodeCombatRequest request, IReadOnlyList<EnemyUnit> enemies,
        ResourceState hero, Point heroPosition, Pcg32 random, int tick, ICollection<SpatialEvent> events)
    {
        foreach (DeferredCombatCopy copy in request.Actions!.TakeDue(tick * TickMilliseconds))
        {
            var context = request.EquipmentRuntime!.CreateTriggeredAction("", copy: true);
            var assigned = new Dictionary<string, EnemyUnit>();
            var replayed = new List<CombatHitSnapshot>();
            var hits = copy.Sacrifice ? copy.Action.Hits.TakeLast(1) : copy.Action.Hits;
            foreach (CombatHitSnapshot recorded in hits)
            {
                Point origin = copy.Source.StartsWith("phantom:", StringComparison.Ordinal) ? recorded.Origin : heroPosition;
                var hit = recorded with { Origin = origin };
                if (!assigned.TryGetValue(hit.TargetId, out var selected))
                {
                    selected = enemies.Where(enemy => enemy.Life > 0 && !assigned.ContainsValue(enemy) &&
                            InRange(origin, enemy.Position, hit.Skill.RangeRaw))
                        .OrderBy(enemy => enemy.EntityId != hit.TargetId).ThenBy(enemy => Point.DistanceSquared(origin, enemy.Position))
                        .ThenBy(enemy => enemy.EntityId, StringComparer.Ordinal).FirstOrDefault();
                    if (selected is not null) assigned[hit.TargetId] = selected;
                }
                var targets = copy.Sacrifice ? enemies.Where(enemy => enemy.Life > 0 && InRange(origin, enemy.Position, copy.Radius)).ToArray() :
                    selected is null || selected.Life <= 0 ? [] : new[] { selected };
                foreach (var enemy in targets)
                {
                    if (!copy.Sacrifice && hit.Skill.Shape == SkillShape.Cone && !InCleaveCone(origin, selected!.Position, enemy.Position, hit.Skill.RangeRaw)) continue;
                    int multiplier = copy.Multiplier;
                    bool critical = hit.Critical;
                    if (copy.RollCritical)
                    {
                        if (critical) multiplier = (int)((long)multiplier * 10_000 / Math.Max(1, hit.AppliedCriticalMultiplier));
                        critical = !hit.Build.CannotCrit && random.NextBasisPoints() < CombatRules.CriticalChance(
                            copy.Action.Tags.HasFlag(SkillTag.Spell) ? SpellHitRules.BaseCriticalChance(hit.Skill.SkillId,
                                (int)Math.Sqrt(Point.DistanceSquared(origin, enemy.Position)), hit.Configuration.Quality) :
                                hit.Build.Weapon.CriticalChanceBasisPoints, hit.Build.IncreasedCriticalChanceBasisPoints);
                        if (critical) multiplier = ScaleCombatValue(multiplier, hit.Build.CriticalMultiplierBasisPoints);
                    }
                    DamagePacket offensive = hit.OffensivePacket with
                    {
                        Branches = hit.OffensivePacket.Branches.Select(branch => branch with
                        {
                            BaseDamage = ScaleCombatValue(
                        ScaleCombatValue(ScaleCombatValue(ScaleCombatValue(branch.BaseDamage, multiplier), 10_000 + enemy.ShockEffect),
                            10_000 + enemy.Curses.Effect("archetypes.skill.death_mark", tick)),
                        branch.CurrentType == DamageType.Void ? ScaleCombatValue(CombatRules.WitherMultiplier(enemy.Ailments.Stack(Ailment.Wither, tick)),
                            10_000 + enemy.Curses.Effect("archetypes.skill.doom_brand", tick)) : 10_000)
                        }).ToArray()
                    };
                    int armor = CombatRules.ArmorAfterBreak(enemy.Scaled.Armor, enemy.ArmorBreakStacks);
                    if (!copy.Sacrifice && hit.Configuration.Supports.HasFlag(SkillSupport.ArmorPierce)) armor = armor * 7_000 / 10_000;
                    bool spell = !copy.Sacrifice && copy.Action.Tags.HasFlag(SkillTag.Spell);
                    var defended = CombatRules.Mitigate(offensive, armor,
                        new(enemy.Scaled.PhysicalResistanceBasisPoints + request.EnemyPhysicalReductionBasisPoints,
                            EnemyResistance(enemy, request, SkillDamageType.Fire, spell), EnemyResistance(enemy, request, SkillDamageType.Cold, spell),
                            EnemyResistance(enemy, request, SkillDamageType.Lightning, spell), EnemyResistance(enemy, request, SkillDamageType.Void)));
                    var damage = new DamageBreakdown(defended.Physical, defended.Fire, defended.Cold, defended.Lightning, defended.Void, defended.Total, defended.Trace);
                    if (copy.Sacrifice)
                    {
                        enemy.Life = Math.Max(0, enemy.Life - damage.Total);
                        if (enemy.Life == 0) events.Add(Event(tick, SpatialEventKind.EnemyDefeated, copy.Source, enemy.EntityId, 0, origin, enemy.Position, enemy.Profile.StableId));
                    }
                    else
                    {
                        request.EquipmentRuntime.InAction(context, () => ApplyHeroDamage(request with { Build = hit.Build }, hit.Skill,
                            hit.Configuration, enemy, hero, random, tick, hit.Origin, damage, critical, events,
                            ailmentSource: hit.AilmentSource.Select(branch => branch with { BaseDamage = ScaleCombatValue(branch.BaseDamage, copy.Multiplier) }).ToArray()));
                        replayed.Add(hit with { TargetId = enemy.EntityId });
                    }
                    events.Add(Event(tick, SpatialEventKind.SkillEffect, copy.Source, enemy.EntityId, damage.Total, hit.Origin, enemy.Position,
                        $"copy:{copy.Id}|source-action:{copy.Action.Id}|due-ms:{copy.DueMilliseconds}|scale:{copy.Multiplier}{(copy.Sacrifice ? "|sacrifice" : "")}"));
                }
            }
            request.Actions.Replayed(copy, replayed);
        }
    }
}
