using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Campaign.Progression;
using GameForWork.Core.Combat;
using GameForWork.Core.Equipment;
using GameForWork.Core.SkillCatalog;
using GameForWork.Core.Skills;
using GameForWork.Core.Simulation;
using GameForWork.Core.Ascendancies;

namespace GameForWork.Core.Spatial;

public sealed partial class SpatialCombatRunner
{
    private static void ApplyAilments(NodeCombatRequest request, ResolvedSkill skill, SkillConfiguration configuration,
        EnemyUnit enemy, IReadOnlyList<DamageBranch> source, DamageBreakdown hit, bool critical,
        Pcg32 random, int tick, Point origin, ICollection<SpatialEvent> events)
    {
        if (enemy.Life <= 0 || hit.Total <= 0) return;
        var equipment = request.Build.CombatEquipment ?? EquipmentCombatLoadout.Empty;
        var passive = request.Build.PassiveProfile ?? PassiveModifiers.Empty;
        int Value(ItemModifierKind kind) => equipment.Value(kind);
        bool Allowed(Ailment kind, int chance, bool guaranteed = false) =>
            !(configuration.Supports.HasFlag(SkillSupport.ElementalFocus) && kind is Ailment.Ignite or Ailment.Chill or Ailment.Freeze or Ailment.Shock or Ailment.Paralysis) &&
            (guaranteed || random.NextBasisPoints() < Math.Clamp(chance, 0, 10_000)) &&
            (enemy.Profile.AilmentAvoidanceBasisPoints <= 0 || random.NextBasisPoints() >= enemy.Profile.AilmentAvoidanceBasisPoints);
        int Chance(Ailment kind) => skill.Ailment == kind ? skill.AilmentChanceBasisPoints : 0;
        int threshold = CombatRules.AilmentThreshold(enemy.MaximumLife, enemy.Rarity switch
        { EnemyRarity.Magic => CombatRarity.Magic, EnemyRarity.Rare => CombatRarity.Rare,
            EnemyRarity.Boss => CombatRarity.MapBoss, _ => CombatRarity.Normal });

        decimal Basis(Ailment kind)
        {
            var branches = source.Where(branch => kind switch
            { Ailment.Bleed => branch.CurrentType == DamageType.Physical,
                Ailment.Poison => branch.CurrentType is DamageType.Physical or DamageType.Void,
                _ => branch.CurrentType == DamageType.Fire });
            decimal total = 0;
            foreach (var branch in branches)
            {
                decimal damage = branch.BaseDamage;
                if (SkillDefinitions.Get(skill.SkillId).Tags.HasFlag(SkillTag.Attack)) damage *= skill.BaseDamageBasisPoints / 10_000m;
                int common = Value(ItemModifierKind.IncreasedDamageOverTimeBasisPoints) + passive.IncreasedDamageOverTimeBasisPoints +
                    Value(kind switch { Ailment.Bleed => ItemModifierKind.IncreasedBleedDamageBasisPoints,
                        Ailment.Poison => ItemModifierKind.IncreasedPoisonDamageBasisPoints, _ => ItemModifierKind.IncreasedIgniteDamageBasisPoints });
                bool first = true, elemental = false;
                foreach (DamageType type in branch.History.Distinct())
                {
                    int increase = Value(type switch { DamageType.Physical => ItemModifierKind.IncreasedPhysicalDamageBasisPoints,
                        DamageType.Fire => ItemModifierKind.IncreasedFireDamageBasisPoints, DamageType.Cold => ItemModifierKind.IncreasedColdDamageBasisPoints,
                        DamageType.Lightning => ItemModifierKind.IncreasedLightningDamageBasisPoints, _ => ItemModifierKind.IncreasedVoidDamageBasisPoints });
                    increase += type == DamageType.Physical ? passive.IncreasedPhysicalDamageBasisPoints :
                        type == DamageType.Void ? passive.IncreasedVoidDamageBasisPoints : 0;
                    if (kind == Ailment.Bleed && type == DamageType.Physical) increase += passive.SpecializedValue(PassiveEffectKind.IncreasedPhysicalDamageOverTimeBasisPoints);
                    if (first) { increase += common; first = false; }
                    if (!elemental && type is DamageType.Fire or DamageType.Cold or DamageType.Lightning)
                    { increase += Value(ItemModifierKind.IncreasedElementalDamageBasisPoints) + passive.IncreasedElementalDamageBasisPoints; elemental = true; }
                    damage *= Math.Max(0, 10_000 + increase) / 10_000m;
                }
                damage *= (10_000m + passive.MoreDamageBasisPoints) / 10_000;
                damage *= (10_000m + request.Build.MoreDamageOverTimeBasisPoints) / 10_000;
                if (kind == Ailment.Bleed) damage *= (10_000m + request.Build.MoreBleedDamageBasisPoints) / 10_000;
                damage *= (10_000m + Value(ItemModifierKind.DamageOverTimeMultiplierBasisPoints) + (critical ? 5_000 : 0)) / 10_000;
                total += damage;
            }
            return total;
        }
        void Apply(Ailment kind, DamageType type, decimal ratio, int duration, int faster)
        {
            decimal dps = Basis(kind) * ratio;
            if (dps <= 0) return;
            duration = duration * Math.Max(0, 10_000 - enemy.Profile.ReducedAilmentDurationBasisPoints) / 10_000;
            enemy.Ailments.Apply(kind, type, dps, duration, faster, skill.SkillId);
            events.Add(Event(tick, SpatialEventKind.Ailment, "hero", enemy.EntityId, 0, origin, enemy.Position,
                $"skill:{skill.SkillId}|ailment:{kind.ToString().ToLowerInvariant()}|dps:{dps:0.###}"));
        }
        if (!configuration.Supports.HasFlag(SkillSupport.Bloodlust) && hit.Physical > 0 && Allowed(Ailment.Bleed,
            Chance(Ailment.Bleed) + Value(ItemModifierKind.BleedChanceBasisPoints) +
            (skill.Ailment == Ailment.Bleed ? 0 : skill.BleedChanceBasisPoints) +
            MasteryRuntime.AdditionalBleedChance(passive, SkillDefinitions.Get(skill.SkillId).Tags, request.Build.Weapon)))
        {
            enemy.Ailments.BleedMaximum = request.AscendancyRuntime?.TwoBleeds == true ? 2 : 1;
            enemy.Ailments.BleedMultiplier = request.AscendancyRuntime?.TwoBleeds == true ? 8_000 : 10_000;
            Apply(Ailment.Bleed, DamageType.Physical, .7m,
                CombatRules.ApplyIncreased(5_000, Value(ItemModifierKind.IncreasedBleedDurationBasisPoints)), Value(ItemModifierKind.FasterBleedBasisPoints));
            request.AscendancyRuntime?.AppliedBleed();
        }
        if (hit.Physical + hit.Void > 0 && Allowed(Ailment.Poison, Chance(Ailment.Poison) + Value(ItemModifierKind.PoisonChanceBasisPoints)))
            Apply(Ailment.Poison, DamageType.Void, .3m, 2_000, Value(ItemModifierKind.FasterPoisonBasisPoints));
        if (hit.Fire > 0 && Allowed(Ailment.Ignite, Chance(Ailment.Ignite) + Value(ItemModifierKind.IgniteChanceBasisPoints), critical))
            Apply(Ailment.Ignite, DamageType.Fire, .9m, 4_000, Value(ItemModifierKind.FasterIgniteBasisPoints));
        if (hit.Cold > 0 && Allowed(Ailment.Chill, 10_000))
        {
            var chill = CombatRules.Chill(hit.Cold, threshold);
            if (chill.EffectBasisPoints > 0)
            {
                enemy.ChillEffect = Math.Max(enemy.ChillEffect, CombatRules.ApplyIncreased(chill.EffectBasisPoints, Value(ItemModifierKind.ChillEffectBasisPoints)));
                enemy.ImpairedUntilTick = tick + chill.DurationMilliseconds / TickMilliseconds;
            }
            if (Allowed(Ailment.Freeze, Chance(Ailment.Freeze), critical))
            {
                var freeze = CombatRules.Freeze(hit.Cold, threshold, Value(ItemModifierKind.FreezeEffectBasisPoints), enemy.Boss ? 1_000 : enemy.Elite ? 2_000 : 3_000);
                enemy.FrozenUntil = Math.Max(enemy.FrozenUntil, tick + freeze.DurationMilliseconds / TickMilliseconds);
            }
        }
        if (hit.Lightning > 0 && Allowed(Ailment.Shock, Chance(Ailment.Shock) + Value(ItemModifierKind.ShockChanceBasisPoints), critical))
        {
            var shock = CombatRules.Shock(hit.Lightning, threshold);
            enemy.ShockEffect = Math.Max(enemy.ShockEffect, Math.Min(10_000, CombatRules.ApplyIncreased(shock.EffectBasisPoints, Value(ItemModifierKind.ShockEffectBasisPoints))));
            enemy.ShockUntil = tick + 40;
        }
        if (hit.Lightning > 0 && Allowed(Ailment.Paralysis, Chance(Ailment.Paralysis)))
        {
            enemy.Paralysis += CombatRules.Paralysis(hit.Lightning, threshold).AccumulationBasisPoints;
            enemy.ParalysisLastTick = tick;
            if (enemy.Paralysis >= 10_000) { enemy.Paralysis = 0; enemy.FrozenUntil = Math.Max(enemy.FrozenUntil, tick + (enemy.Boss ? 7 : enemy.Elite ? 12 : 20)); }
        }
        if (skill.Ailment is Ailment.Erosion or Ailment.Wither && Allowed(skill.Ailment, skill.AilmentChanceBasisPoints))
            enemy.Ailments.AddStack(skill.Ailment, 1, skill.Ailment == Ailment.Erosion ? 5 : 10,
                skill.Ailment == Ailment.Erosion ? 120 : 80, tick);
        if (Allowed(Ailment.ArmorBreak, (skill.Ailment == Ailment.ArmorBreak ? skill.AilmentChanceBasisPoints : 0) +
            (hit.Physical > 0 ? request.Auras?.PhysicalArmorBreakChance ?? 0 : 0)))
        { enemy.ArmorBreakStacks = Math.Min(5, enemy.ArmorBreakStacks + 1); enemy.ArmorBreakUntil = tick + 100; }
        if (skill.Ailment == Ailment.Stun && Allowed(Ailment.Stun, CombatRules.StunChance(hit.Total, threshold)))
            enemy.StunnedUntilTick = tick + (enemy.Boss ? 6 : 12);
    }

    private static void AdvanceAilments(NodeCombatRequest request, IEnumerable<EnemyUnit> enemies, ResourceState hero, int tick, ICollection<SpatialEvent> events)
    {
        bool recovered = false;
        foreach (var enemy in enemies.Where(enemy => enemy.Life > 0))
        {
            if (tick >= enemy.ShockUntil) enemy.ShockEffect = 0;
            if (tick >= enemy.ImpairedUntilTick) enemy.ChillEffect = 0;
            if (tick >= enemy.ArmorBreakUntil) enemy.ArmorBreakStacks = 0;
            if (tick - enemy.ParalysisLastTick >= 40) enemy.Paralysis = Math.Max(0, enemy.Paralysis - 125);
            enemy.CurrentTick = tick;
            foreach (var pulse in enemy.Ailments.Advance(TickMilliseconds, (type, dps) =>
            {
                decimal damage = dps;
                if (type == DamageType.Physical) damage *= (10_000 - CombatRules.PhysicalDotArmorReduction(
                    CombatRules.ArmorAfterBreak(enemy.Scaled.Armor, enemy.ArmorBreakStacks), (int)Math.Min(int.MaxValue, dps))) / 10_000m;
                int resistance = type == DamageType.Physical ? enemy.Scaled.PhysicalResistanceBasisPoints + request.EnemyPhysicalReductionBasisPoints :
                    EnemyResistance(enemy, request, type == DamageType.Fire ? SkillDamageType.Fire : type == DamageType.Void ? SkillDamageType.Void : type == DamageType.Cold ? SkillDamageType.Cold : SkillDamageType.Lightning, penetrate: false);
                damage *= (10_000 - Math.Clamp(resistance, CombatRules.MinimumResistance, type == DamageType.Physical ? 5_000 : 7_500)) / 10_000m;
                damage *= (10_000 + enemy.ShockEffect) / 10_000m;
                if (type == DamageType.Void)
                    damage *= CombatRules.WitherMultiplier(enemy.Ailments.Stack(Ailment.Wither, tick)) / 10_000m *
                        (10_000 + enemy.Curses.Effect("archetypes.skill.doom_brand", tick)) / 10_000m;
                damage *= (10_000 - CombatRules.SpiritBarrierReduction(enemy.Profile.SpiritBarrier, (int)Math.Min(int.MaxValue, damage))) / 10_000m;
                return damage;
            }))
            {
                int damage = Math.Min(enemy.Life, pulse.Damage);
                enemy.Life -= damage;
                if (damage > 0 && tick % 20 == 0 && !recovered && enemy.Rarity is EnemyRarity.Rare or EnemyRarity.Boss &&
                    request.AscendancyRuntime?.Has(WarriorNodeIds.BloodTideCore) == true)
                {
                    int amount = hero.HealLife(Math.Max(1, hero.MaximumLife * 400 / 10_000));
                    request.AscendancyRuntime.TriggerRecoveryProtection(tick);
                    recovered = true;
                    events.Add(Event(tick, SpatialEventKind.Ascendancy, "hero", "hero", amount, enemy.Position, enemy.Position, "赤潮归身|持续伤害恢复"));
                }
                events.Add(Event(tick, pulse.Kind == Ailment.Bleed ? SpatialEventKind.Bleed : SpatialEventKind.Ailment,
                    "hero", enemy.EntityId, damage, enemy.Position, enemy.Position, $"dot:{pulse.Kind.ToString().ToLowerInvariant()}"));
                if (enemy.Life == 0) { events.Add(Event(tick, SpatialEventKind.EnemyDefeated, "hero", enemy.EntityId, 0, enemy.Position, enemy.Position, enemy.Profile.StableId)); break; }
            }
        }
    }
}
