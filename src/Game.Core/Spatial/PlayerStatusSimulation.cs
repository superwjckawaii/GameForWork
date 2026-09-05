using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.SkillCatalog;
using GameForWork.Core.Simulation;

namespace GameForWork.Core.Spatial;

public sealed partial class SpatialCombatRunner
{
    private static void ApplyEnemyStatus(NodeCombatRequest request, ResourceState hero, EnemyUnit enemy,
        EnemySkillProfile skill, int baseDamage, int damage, bool critical, Pcg32 random, int tick,
        Point position, ICollection<SpatialEvent> events)
    {
        if (damage <= 0 || !hero.IsAlive) return;
        var status = hero.HarmfulStatus;
        int durationScale = Math.Max(0, 10_000 - (request.Build.CombatEquipment?.Value(ItemModifierKind.ReducedDebuffDurationBasisPoints) ?? 0));
        int avoid = request.Build.CombatEquipment?.Value(ItemModifierKind.AilmentAvoidanceBasisPoints) ?? 0;
        int Duration(int milliseconds) => ScaleCombatValue(milliseconds, durationScale);
        bool Allowed(Ailment kind, bool guaranteed = false) => !status.Immune(kind) &&
            (guaranteed || skill.Ailment == kind && random.NextBasisPoints() < skill.AilmentChanceBasisPoints) &&
            (avoid <= 0 || random.NextBasisPoints() >= avoid);
        void Dot(Ailment kind, DamageType type, decimal ratio, int duration)
        {
            if (status.ApplyDot(kind, type, baseDamage * ratio * (critical ? 1.5m : 1m), Duration(duration), enemy.EntityId))
                events.Add(Event(tick, SpatialEventKind.Ailment, enemy.EntityId, "hero", 0, position, position, $"ailment:{kind}|applied"));
        }
        if (skill.DamageType == EnemyDamageType.Physical && Allowed(Ailment.Bleed)) Dot(Ailment.Bleed, DamageType.Physical, .7m, 5_000);
        if (skill.DamageType is EnemyDamageType.Physical or EnemyDamageType.Void && Allowed(Ailment.Poison)) Dot(Ailment.Poison, DamageType.Void, .3m, 2_000);
        if (skill.DamageType == EnemyDamageType.Fire && Allowed(Ailment.Ignite, critical)) Dot(Ailment.Ignite, DamageType.Fire, .9m, 4_000);
        if (skill.DamageType == EnemyDamageType.Cold && Allowed(Ailment.Chill, true))
        {
            var chill = CombatRules.Chill(damage, hero.MaximumLife);
            status.Apply(Ailment.Chill, chill.EffectBasisPoints, Duration(chill.DurationMilliseconds) / TickMilliseconds);
            if (Allowed(Ailment.Freeze, critical))
            {
                var freeze = CombatRules.Freeze(damage, hero.MaximumLife);
                status.Apply(Ailment.Freeze, 1, Duration(freeze.DurationMilliseconds) / TickMilliseconds);
            }
        }
        if (skill.DamageType == EnemyDamageType.Lightning && Allowed(Ailment.Shock, critical))
        {
            var shock = CombatRules.Shock(damage, hero.MaximumLife);
            status.Apply(Ailment.Shock, shock.EffectBasisPoints, Duration(shock.DurationMilliseconds) / TickMilliseconds);
        }
        if (Allowed(Ailment.Stun)) status.Apply(Ailment.Stun, 1, Duration(600) / TickMilliseconds);
        if (!string.IsNullOrEmpty(skill.CurseId) && skill.CurseEffectBasisPoints > 0)
            status.ApplyCurse(skill.CurseId, skill.CurseEffectBasisPoints, Duration(10_000) / TickMilliseconds);
    }
    private static void AdvancePlayerStatus(NodeCombatRequest request, ResourceState hero, Point position, int tick, ICollection<SpatialEvent> events)
    {
        int generation = hero.HarmfulStatus.Generation;
        foreach (var pulse in hero.HarmfulStatus.DamageOverTime.Advance(TickMilliseconds, (type, dps) =>
        {
            int raw = Math.Max(1, (int)Math.Min(int.MaxValue, dps));
            EnemyDamageType damageType = type switch { DamageType.Physical => EnemyDamageType.Physical, DamageType.Fire => EnemyDamageType.Fire,
                DamageType.Cold => EnemyDamageType.Cold, DamageType.Lightning => EnemyDamageType.Lightning, _ => EnemyDamageType.Void };
            int defended = request.EquipmentRuntime!.MitigateDamageOverTime(request.Build.Sheet, raw, damageType, tick, 1);
            return dps * defended / raw * (10_000 + hero.HarmfulStatus.Effect(Ailment.Shock)) / 10_000m;
        }))
        {
            int damage = request.EquipmentRuntime!.ApplyEnemyDamage(hero, pulse.Damage, false, tick, request.VirtueVice);
            events.Add(Event(tick, SpatialEventKind.Ailment, "enemy:ailment", "hero", damage, position, position, $"dot:{pulse.Kind}"));
            if (!hero.IsAlive || hero.HarmfulStatus.Generation != generation) break;
        }
    }
}
