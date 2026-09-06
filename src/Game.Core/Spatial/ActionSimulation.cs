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
        foreach (DeferredCombatCopy copy in request.Actions!.TakeDue(tick * TickMilliseconds,
                     position => request.Buffs!.ForUnit(tick, heroPosition, position).ActionSpeed))
        {
            var context = request.EquipmentRuntime!.CreateTriggeredAction("", copy: true);
            var assigned = new Dictionary<string, EnemyUnit>();
            var replayed = new List<CombatHitSnapshot>();
            var areaHits = new Dictionary<(int Offset, Point Origin, string Target), int>();
            var areaAims = new Dictionary<(int Offset, Point Origin), Point>();
            var hits = copy.Sacrifice ? copy.Action.Hits.TakeLast(1) : copy.Action.Hits;
            foreach (CombatHitSnapshot recorded in hits)
            {
                Point origin = copy.Source.StartsWith("phantom:", StringComparison.Ordinal) || copy.Source is "mastery:aftershock" or "support:movement-echo" ? recorded.Origin : heroPosition;
                var hit = recorded with { Origin = origin };
                if (!assigned.TryGetValue(hit.TargetId, out var selected))
                {
                    selected = enemies.Where(enemy => enemy.Life > 0 && !assigned.ContainsValue(enemy) &&
                            InRange(origin, enemy.Position, AreaRules.EngagementRange(hit.Skill)))
                        .OrderBy(enemy => enemy.EntityId != hit.TargetId).ThenBy(enemy => Point.DistanceSquared(origin, enemy.Position))
                        .ThenBy(enemy => enemy.EntityId, StringComparer.Ordinal).FirstOrDefault();
                    if (selected is not null) assigned[hit.TargetId] = selected;
                }
                bool area = !copy.Sacrifice && hit.Skill.Shape is SkillShape.Circle or SkillShape.Cone or SkillShape.MovementCircle or SkillShape.GroundArea;
                var areaKey = (hit.OffsetMilliseconds, origin);
                if (area && !areaAims.ContainsKey(areaKey) && selected is not null) areaAims[areaKey] = selected.Position;
                var targets = copy.Sacrifice ? enemies.Where(enemy => enemy.Life > 0 && InRange(origin, enemy.Position, copy.Radius)).ToArray() :
                    area ? enemies.Where(enemy => enemy.Life > 0 && InRange(origin, enemy.Position, hit.Skill.AreaRadiusRaw) &&
                        (hit.Skill.Shape != SkillShape.Cone || areaAims.TryGetValue(areaKey, out var aim) && InCleaveCone(origin, aim, enemy.Position, hit.Skill.AreaRadiusRaw))).ToArray() :
                    selected is null || selected.Life <= 0 ? [] : new[] { selected };
                foreach (var enemy in targets)
                {
                    if (area)
                    {
                        var key = (hit.OffsetMilliseconds, origin, enemy.EntityId);
                        int count = areaHits.GetValueOrDefault(key);
                        int maximum = MasteryRuntime.Has(hit.Build.PassiveProfile ?? Campaign.Progression.PassiveModifiers.Empty, "范围_距离", 4)
                            ? Math.Min(2, copy.Action.Hits.Where(item => item.OffsetMilliseconds == hit.OffsetMilliseconds && item.Origin == recorded.Origin)
                                .GroupBy(item => item.TargetId).Max(group => group.Count())) : 1;
                        if (count >= maximum) continue;
                        areaHits[key] = count + 1;
                    }
                    if (copy.Source is "mastery:unarmed-repeat" or "support:movement-echo" && !hit.Build.AlwaysHit && !hit.Skill.AlwaysHit && random.NextBasisPoints() >=
                        DamageRules.HitChance(hit.Build.Sheet.Accuracy(hit.Build.FlatAccuracy + UnarmedRules.Accuracy(hit.Skill.SkillId, hit.Build)).Value, enemy.Scaled.Evasion, false).Value)
                    {
                        events.Add(Event(tick, SpatialEventKind.SkillEffect, "hero", enemy.EntityId, 0, origin, enemy.Position, $"{copy.Source}|miss"));
                        continue;
                    }
                    int multiplier = copy.Multiplier;
                    if (area)
                        multiplier = (int)((long)multiplier * AreaRules.PositionMultiplier(hit.Build.PassiveProfile ?? Campaign.Progression.PassiveModifiers.Empty,
                            (int)Math.Sqrt(Point.DistanceSquared(origin, enemy.Position)), hit.Skill.AreaRadiusRaw) / hit.AreaPositionMultiplier);
                    if (copy.Source.StartsWith("phantom:", StringComparison.Ordinal) && !copy.Sacrifice)
                        multiplier = (int)((long)multiplier * (10_000 + request.Buffs!.WarSongMore(tick)) /
                            (10_000 + hit.Build.WarSongMoreDamageBasisPoints));
                    if (copy.Source.StartsWith("phantom:", StringComparison.Ordinal) && !copy.Sacrifice)
                        multiplier = ScaleCombatValue(multiplier, request.Actions.PhantomTargetMultiplier(
                            enemy == SelectTarget(enemies, heroPosition) && enemy.Rarity is EnemyRarity.Rare or EnemyRarity.Boss));
                    bool critical = hit.Critical;
                    if (copy.RollCritical)
                    {
                        if (critical) multiplier = (int)((long)multiplier * 10_000 / Math.Max(1, hit.AppliedCriticalMultiplier));
                        critical = !hit.Build.CannotCrit && random.NextBasisPoints() < CombatRules.CriticalChance(
                            copy.Action.Tags.HasFlag(SkillTag.Spell) ? SpellHitRules.BaseCriticalChance(hit.Skill.SkillId,
                                (int)Math.Sqrt(Point.DistanceSquared(origin, enemy.Position)), hit.Configuration.Quality) :
                                UnarmedRules.Source(hit.Skill.SkillId, hit.Build.Weapon).CriticalChanceBasisPoints + UnarmedRules.CriticalBonus(hit.Configuration), hit.Build.IncreasedCriticalChanceBasisPoints);
                        if (critical) multiplier = ScaleCombatValue(multiplier, hit.Build.CriticalMultiplierBasisPoints);
                    }
                    int TargetDamage(DamageBranch branch)
                    {
                        int amount = VoidDebuffed(enemy, tick) ? branch.DebuffedBaseDamage ?? branch.BaseDamage : branch.BaseDamage;
                        amount = ScaleCombatValue(amount, multiplier);
                        amount = ScaleCombatValue(amount, ElementalRules.TargetMultiplier(hit.Build.Ascendancy, branch.CurrentType, ElementalStatus(enemy, tick), true, critical));
                        amount = ScaleCombatValue(amount, 10_000 + enemy.ShockEffect);
                        amount = ScaleCombatValue(amount, 10_000 + enemy.Curses.Effect("archetypes.skill.death_mark", tick));
                        amount = ScaleCombatValue(amount, AilmentMasteryRules.BleedingTargetHitMultiplier(hit.Build.PassiveProfile ?? Campaign.Progression.PassiveModifiers.Empty, enemy.Ailments));
                        if (branch.CurrentType == DamageType.Void)
                        {
                            amount = ScaleCombatValue(amount, ScaleCombatValue(CombatRules.WitherMultiplier(enemy.Ailments.Stack(Ailment.Wither, tick)),
                                10_000 + enemy.Curses.Effect("archetypes.skill.doom_brand", tick)));
                        }
                        return amount;
                    }
                    DamagePacket offensive = hit.OffensivePacket with
                    {
                        Branches = hit.OffensivePacket.Branches.Select(branch => branch with { BaseDamage = TargetDamage(branch) }).ToArray()
                    };
                    int armor = CombatRules.ArmorAfterBreak(enemy.Scaled.Armor, enemy.ArmorBreakStacks,
                        additionalReductionBasisPoints: request.Auras?.ArmorReductionAt((int)Math.Sqrt(Point.DistanceSquared(heroPosition, enemy.Position))) ?? 0);
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
