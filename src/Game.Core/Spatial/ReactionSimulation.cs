using GameForWork.Core.Builds;
using GameForWork.Core.Archetypes;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Combat;
using GameForWork.Core.Equipment;
using GameForWork.Core.SkillCatalog;
using GameForWork.Core.Skills;
using GameForWork.Core.Simulation;

namespace GameForWork.Core.Spatial;

public sealed partial class SpatialCombatRunner
{
    private static SkillConfiguration? ReactionConfiguration(NodeCombatRequest request, string id) =>
        request.Build.ActiveSkills?.FirstOrDefault(skill => skill.SkillId == id);
    private static bool TriggerSupported(SkillConfiguration config) =>
        (config.Supports & (SkillSupport.BlockTrigger | SkillSupport.CastWhenDamaged)) != 0 ||
        LinkedSupportRules.Support(config, SupportMechanic.AttackTrigger);
    private static void ScheduleSupportedReactions(NodeCombatRequest request, ResourceState hero, string target,
        bool attack = false, bool block = false, int hitDamage = 0)
    {
        foreach (var config in request.Build.ActiveSkills ?? [])
        {
            if (attack && LinkedSupportRules.Support(config, SupportMechanic.AttackTrigger))
            {
                int recovery = LinkedSupportRules.SupportQuality(config, SupportMechanic.AttackTrigger) * 100;
                int cooldown = (int)Math.Ceiling(LinkedSupportRules.SupportValue(config, SupportMechanic.AttackTrigger, 16, 8) * 10_000d / (10_000 + recovery));
                request.Reactions!.Schedule(config, target, cooldown, payCost: true);
            }
            else if (block && config.Supports.HasFlag(SkillSupport.BlockTrigger))
            {
                var link = CombatSkillRules.SupportLink(config, SkillSupport.BlockTrigger);
                int cooldown = ActiveSkillCatalog.Interpolate(30, 20, link.Level, false);
                cooldown += (17 - cooldown) * Math.Clamp(link.Quality, 0, 20) / 20;
                request.Reactions!.Schedule(config, target, cooldown, payCost: true);
            }
            else if (hitDamage > 0 && config.Supports.HasFlag(SkillSupport.CastWhenDamaged))
            {
                var link = CombatSkillRules.SupportLink(config, SkillSupport.CastWhenDamaged);
                int threshold = ActiveSkillCatalog.Interpolate(2_000, 1_200, link.Level, false) - Math.Clamp(link.Quality, 0, 20) * 10;
                if (request.Reactions!.AccumulateDamage(config.SkillId, hitDamage,
                    (int)(((long)hero.MaximumLife + hero.MaximumShield) * threshold / 10_000)))
                    request.Reactions.Schedule(config, target, 5, payCost: true);
            }
        }
    }
    private static bool MeleeEquipped(TeamBuild build)
    {
        if (!build.HasUsableWeapon) return true;
        try
        {
            var item = EquipmentCatalog.GetBase(build.Weapon.StableId);
            return item.Category is ItemCategory.OneHandWeapon or ItemCategory.TwoHandWeapon &&
                item.WeaponFamily is not (WeaponFamily.Bow or WeaponFamily.Wand);
        }
        catch (KeyNotFoundException) { return false; }
    }
    private static SkillDamageType MainElement(TeamBuild build) =>
        build.ActiveSkills?.FirstOrDefault(skill => skill.SkillId == "archetypes.skill.elemental_imprint")?.Mode switch
        {
            "Cold" or "寒霜" => SkillDamageType.Cold,
            "Lightning" or "闪电" => SkillDamageType.Lightning,
            _ => SkillDamageType.Fire,
        };
    private static void ResolveReactions(NodeCombatRequest request, IReadOnlyList<EnemyUnit> enemies,
        ResourceState hero, Point origin, Pcg32 random, int tick, ICollection<SpatialEvent> events,
        IList<PendingProjectile> projectiles, IList<PersistentArea> areas)
    {
        ResolveShieldBurst(request, enemies, hero, origin, tick, events);
        foreach (var reaction in request.Reactions!.Drain())
        {
            if (!hero.IsAlive || ReactionConfiguration(request, reaction.SkillId) is not { } config) continue;
            var skill = reaction.Resolved ?? CombatSkillRules.Resolve(config, hero.MaximumLife, request.Build.PassiveProfile);
            if (reaction.PayCost)
            {
                skill = request.EquipmentRuntime!.Resolve(ApplyAscendancyCost(skill, config, hero.MaximumLife, request.AscendancyRuntime!.Profile));
                if (hero.Shield < GuardState.ShieldCost(skill.SkillId, hero.MaximumShield) || !CombatSkillRules.TryPay(hero, skill))
                {
                    events.Add(Event(tick, SpatialEventKind.SkillFailed, "hero", reaction.TargetId, 0, origin, origin, $"reaction:{skill.SkillId}|resource"));
                    continue;
                }
            }
            bool overload = reaction.SkillId == ReactionState.Overload;
            bool shieldBreak = reaction.SkillId == ReactionState.ShieldBreak;
            if (overload || reaction.SkillId == ReactionState.Answer) skill = skill with { DamageType = MainElement(request.Build) };
            if (reaction.SkillId == ReactionState.Mirror) skill = skill with { DamageType = SkillDamageType.Lightning, RangeRaw = 10_000 };
            if (overload || shieldBreak) skill = skill with { BaseDamageBasisPoints = (int)Math.Round((overload ? 8_000 : 18_000) * Math.Pow(1.05, config.Level - 1)) };
            var target = enemies.Where(enemy => enemy.Life > 0 && (skill.Shape == SkillShape.Self || InRange(origin, enemy.Position, skill.RangeRaw)))
                .OrderBy(enemy => enemy.EntityId != reaction.TargetId).ThenBy(enemy => Point.DistanceSquared(origin, enemy.Position))
                .ThenBy(enemy => enemy.EntityId, StringComparer.Ordinal).FirstOrDefault();
            if (shieldBreak) request.Guard!.GainEnergy(3);
            if (reaction.RecoverLife) hero.HealLife(Math.Max(1, hero.MaximumLife * 800 / 10_000));
            if (target is null) continue;
            var equipment = request.EquipmentRuntime!;
            equipment.InAction(equipment.CreateTriggeredAction(target.EntityId), () =>
            {
                if (skill.Role == SkillRole.Guard)
                {
                    if (!request.Guard!.Activate(skill.SkillId, hero, config.Level, config.Quality, tick)) return;
                    request.Guard.ApplySupports(config, hero);
                    events.Add(Event(tick, SpatialEventKind.Guard, "hero", "hero", request.Guard.Remaining, origin, origin, $"skill:{skill.SkillId}|triggered-guard"));
                    if (skill.SkillId != "archetypes.skill.aegis_pulse") return;
                }
                if (CombatBuffState.IsSkill(skill.SkillId)) { request.Buffs!.Activate(config, !request.Build.HasUsableWeapon, tick, target.EntityId); return; }
                if (ApplyCurse(request, skill, config, target, enemies, origin, tick, events) ||
                    CreatePersistentArea(request, skill, config, target, enemies, hero, random, tick, origin, origin, reaction.Multiplier, areas, events)) return;
                skill = skill with { Role = SkillRole.Clear };
                if (skill.Shape == SkillShape.Projectile)
                    LaunchProjectiles(request, skill, config, target, enemies, origin, reaction.Multiplier, tick, projectiles);
                else
                    foreach (var enemy in (overload || shieldBreak || skill.Shape is SkillShape.Circle or SkillShape.Cone ?
                        enemies.Where(enemy => enemy.Life > 0 && InRange(origin, enemy.Position, skill.RangeRaw)) : [target]).ToArray())
                        ResolveHeroHit(request, skill, config, enemy, hero, random, tick, origin, reaction.Multiplier, events,
                            additionalIncreasedBasisPoints: reaction.IncreasedDamage,
                            additionalBaseDamage: skill.SkillId == "archetypes.skill.aegis_pulse" ? request.Guard!.LastPaidShield : 0);
            });
            events.Add(Event(tick, SpatialEventKind.SkillEffect, "hero", target.EntityId, 0, origin, target.Position, $"reaction:{skill.SkillId}"));
        }
    }

    private static void ResolveShieldBurst(NodeCombatRequest request, IReadOnlyList<EnemyUnit> enemies,
        ResourceState hero, Point origin, int tick, ICollection<SpatialEvent> events)
    {
        var (raw, empowered) = request.Guard!.TakeShieldBurst();
        if (raw == 0 || !hero.IsAlive) return;
        var build = request.Build;
        var passive = build.PassiveProfile ?? GameForWork.Core.Campaign.Progression.PassiveModifiers.Empty;
        var equipment = build.CombatEquipment ?? EquipmentCombatLoadout.Empty;
        var modifiers = CombatSkillRules.OffensiveIncreases(build, SkillTag.Area | SkillTag.Lightning);
        foreach (var enemy in enemies.Where(enemy => enemy.Life > 0 && InRange(origin, enemy.Position, 4_000)))
        {
            var damage = DamagePacketRules.ResolveMixed(raw, SkillDamageType.Lightning, default, SkillSupport.None,
                enemy.Scaled.Armor, EnemyResistance(enemy, request, SkillDamageType.Fire),
                EnemyResistance(enemy, request, SkillDamageType.Cold), EnemyResistance(enemy, request, SkillDamageType.Lightning),
                EnemyResistance(enemy, request, SkillDamageType.Void), equipment: equipment.Modifiers, modifiers: modifiers,
                scaleBranch: branch =>
                {
                    int value = ScaleCombatValue(branch.BaseDamage, 10_000 + passive.MoreDamageBasisPoints);
                    value = ScaleCombatValue(value, 10_000 + build.MoreElementalDamageBasisPoints);
                    if (branch.CurrentType == DamageType.Void) value = ScaleCombatValue(value, 10_000 + build.MoreVoidDamageBasisPoints);
                    if (enemy.Rarity is EnemyRarity.Rare or EnemyRarity.Boss) value = ScaleCombatValue(value, 10_000 + build.MoreRareBossDamageBasisPoints);
                    value = ScaleCombatValue(value, 10_000 + enemy.ShockEffect);
                    return ScaleCombatValue(value, 10_000 + enemy.Curses.Effect("archetypes.skill.death_mark", tick));
                }, allowAddedHitDamage: false);
            int actual = Math.Min(enemy.Life, damage.Total);
            enemy.Life -= actual;
            if (empowered && actual > 0)
            {
                if (enemy.Boss)
                {
                    enemy.ArmorBreakStacks = Math.Min(request.AscendancyRuntime!.ArmorBreakMaximum, enemy.ArmorBreakStacks + 5);
                    enemy.ArmorBreakUntil = tick + 100;
                }
                else enemy.StunnedUntilTick = Math.Max(enemy.StunnedUntilTick, tick + 12);
            }
            events.Add(Event(tick, SpatialEventKind.SkillEffect, "hero", enemy.EntityId, actual, origin, enemy.Position,
                $"spellarmor-break|damage:{damage.Compact}"));
            if (enemy.Life == 0) events.Add(Event(tick, SpatialEventKind.EnemyDefeated, "hero", enemy.EntityId, 0, origin, enemy.Position, enemy.Profile.StableId));
        }
    }
}
