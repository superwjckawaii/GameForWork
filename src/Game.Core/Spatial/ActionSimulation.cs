using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Combat;
using GameForWork.Core.SkillCatalog;
using GameForWork.Core.Simulation;

namespace GameForWork.Core.Spatial;

public sealed partial class SpatialCombatRunner
{
    private static void ResolveCopies(NodeCombatRequest request, IReadOnlyList<EnemyUnit> enemies,
        ResourceState hero, Pcg32 random, int tick, ICollection<SpatialEvent> events)
    {
        foreach (DeferredCombatCopy copy in request.Actions!.TakeDue(tick * TickMilliseconds))
        {
            var context = request.EquipmentRuntime!.CreateTriggeredAction("");
            foreach (CombatHitSnapshot hit in copy.Action.Hits)
            {
                EnemyUnit? enemy = enemies.FirstOrDefault(enemy => enemy.EntityId == hit.TargetId && enemy.Life > 0);
                if (enemy is null) continue;
                int multiplier = copy.Multiplier;
                bool critical = hit.Critical;
                if (copy.RollCritical)
                {
                    if (critical) multiplier = (int)((long)multiplier * 10_000 / Math.Max(1, hit.AppliedCriticalMultiplier));
                    critical = !hit.Build.CannotCrit && random.NextBasisPoints() < CombatRules.CriticalChance(
                        hit.Build.Weapon.CriticalChanceBasisPoints, hit.Build.IncreasedCriticalChanceBasisPoints);
                    if (critical) multiplier = ScaleCombatValue(multiplier, hit.Build.CriticalMultiplierBasisPoints);
                }
                DamagePacket offensive = hit.OffensivePacket with
                { Branches = hit.OffensivePacket.Branches.Select(branch => branch with { BaseDamage = ScaleCombatValue(
                    ScaleCombatValue(ScaleCombatValue(ScaleCombatValue(branch.BaseDamage, multiplier), 10_000 + enemy.ShockEffect),
                        10_000 + enemy.Curses.Effect("archetypes.skill.death_mark", tick)),
                    branch.CurrentType == DamageType.Void ? CombatRules.WitherMultiplier(enemy.Ailments.Stack(Ailment.Wither, tick)) : 10_000) }).ToArray() };
                var defended = CombatRules.Mitigate(offensive, CombatRules.ArmorAfterBreak(enemy.Scaled.Armor, enemy.ArmorBreakStacks),
                    new(enemy.Scaled.PhysicalResistanceBasisPoints + request.EnemyPhysicalReductionBasisPoints,
                        EnemyResistance(enemy, request, SkillDamageType.Fire), EnemyResistance(enemy, request, SkillDamageType.Cold),
                        EnemyResistance(enemy, request, SkillDamageType.Lightning), EnemyResistance(enemy, request, SkillDamageType.Void)));
                var damage = new DamageBreakdown(defended.Physical, defended.Fire, defended.Cold, defended.Lightning, defended.Void, defended.Total, defended.Trace);
                request.EquipmentRuntime.InAction(context, () => ApplyHeroDamage(request with { Build = hit.Build }, hit.Skill,
                    hit.Configuration, enemy, hero, random, tick, hit.Origin, damage, critical, events,
                    ailmentSource: hit.AilmentSource.Select(branch => branch with { BaseDamage = ScaleCombatValue(branch.BaseDamage, copy.Multiplier) }).ToArray()));
                events.Add(Event(tick, SpatialEventKind.SkillEffect, copy.Source, enemy.EntityId, damage.Total, hit.Origin, enemy.Position,
                    $"copy:{copy.Id}|source-action:{copy.Action.Id}|due-ms:{copy.DueMilliseconds}|scale:{copy.Multiplier}"));
            }
        }
    }
}
