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
    private static ElementalAilments ElementalStatus(EnemyUnit enemy, int tick) => new(enemy.Ailments.Count(Ailment.Ignite) > 0,
        enemy.ChillEffect > 0 && tick < enemy.ImpairedUntilTick, tick < enemy.FrozenUntil,
        enemy.ShockEffect > 0 && tick < enemy.ShockUntil, tick < enemy.ParalyzedUntil);
    private static bool VoidDebuffed(EnemyUnit enemy, int tick) =>
        enemy.Ailments.Stack(Ailment.Erosion, tick) > 0 || enemy.Ailments.Stack(Ailment.Wither, tick) > 0;
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
        bool Element(string branch, string size = "small") => ElementalRules.Has(request.Build.Ascendancy, branch, size);
        int Chance(Ailment kind) => (skill.Ailment == kind ? skill.AilmentChanceBasisPoints : 0) +
            (kind is Ailment.Ignite or Ailment.Freeze or Ailment.Shock or Ailment.Paralysis && Element("ailment") ? 2_500 : 0) +
            (kind == Ailment.Ignite && Element("fire") ? 3_000 : 0);
        int effectIncrease = Element("resonance", "core") ? (request.VirtueVice?.Layers(VirtueViceKind.Temperance) ?? 0) * 400 : 0;
        int Duration(int value) => CombatRules.ApplyIncreased(value, Element("ailment") ? 2_500 : 0);
        int threshold = CombatRules.AilmentThreshold(enemy.MaximumLife, enemy.Rarity switch
        {
            EnemyRarity.Magic => CombatRarity.Magic,
            EnemyRarity.Rare => CombatRarity.Rare,
            EnemyRarity.Boss => CombatRarity.MapBoss,
            _ => CombatRarity.Normal
        });

        decimal Basis(Ailment kind, bool voidDebuffed = false)
        {
            var branches = source.Where(branch => kind switch
            {
                Ailment.Bleed => branch.CurrentType == DamageType.Physical,
                Ailment.Poison => branch.CurrentType is DamageType.Physical or DamageType.Void,
                _ => branch.CurrentType == DamageType.Fire || MasteryRuntime.Has(passive, "点燃", 3) && branch.CurrentType is DamageType.Cold or DamageType.Lightning
            });
            DamageType output = kind == Ailment.Bleed ? DamageType.Physical : kind == Ailment.Poison ? DamageType.Void : DamageType.Fire;
            decimal total = 0;
            foreach (var branch in branches)
            {
                decimal damage = branch.BaseDamage;
                if (SkillDefinitions.Get(skill.SkillId).Tags.HasFlag(SkillTag.Attack)) damage *= skill.BaseDamageBasisPoints / 10_000m;
                int common = request.Build.IncreasedGenericDamageBasisPoints + Value(ItemModifierKind.IncreasedDamageOverTimeBasisPoints) + passive.IncreasedDamageOverTimeBasisPoints +
                    Value(kind switch
                    {
                        Ailment.Bleed => ItemModifierKind.IncreasedBleedDamageBasisPoints,
                        Ailment.Poison => ItemModifierKind.IncreasedPoisonDamageBasisPoints,
                        _ => ItemModifierKind.IncreasedIgniteDamageBasisPoints
                    });
                if (kind == Ailment.Bleed) common += passive.SpecializedValue(PassiveEffectKind.IncreasedBleedDamageBasisPoints);
                bool first = true, elemental = false;
                foreach (DamageType type in branch.History.Append(output).Distinct())
                {
                    int increase = Value(type switch
                    {
                        DamageType.Physical => ItemModifierKind.IncreasedPhysicalDamageBasisPoints,
                        DamageType.Fire => ItemModifierKind.IncreasedFireDamageBasisPoints,
                        DamageType.Cold => ItemModifierKind.IncreasedColdDamageBasisPoints,
                        DamageType.Lightning => ItemModifierKind.IncreasedLightningDamageBasisPoints,
                        _ => ItemModifierKind.IncreasedVoidDamageBasisPoints
                    });
                    increase += ElementalRules.TypeIncrease(request.Build, type);
                    increase += type == DamageType.Physical ? passive.IncreasedPhysicalDamageBasisPoints :
                        type == DamageType.Void ? passive.IncreasedVoidDamageBasisPoints : 0;
                    if (kind == Ailment.Bleed && type == DamageType.Physical) increase += passive.SpecializedValue(PassiveEffectKind.IncreasedPhysicalDamageOverTimeBasisPoints);
                    if (voidDebuffed && type == DamageType.Void && MasteryRuntime.Has(passive, "虚空", 4)) increase += 6_000;
                    if (first) { increase += common; first = false; }
                    if (!elemental && type is DamageType.Fire or DamageType.Cold or DamageType.Lightning)
                    { increase += Value(ItemModifierKind.IncreasedElementalDamageBasisPoints) + passive.IncreasedElementalDamageBasisPoints; elemental = true; }
                    damage *= Math.Max(0, 10_000 + increase) / 10_000m;
                }
                damage *= (10_000m + passive.MoreDamageBasisPoints) / 10_000;
                damage *= (10_000m + request.Build.MoreDamageOverTimeBasisPoints) / 10_000;
                if (kind == Ailment.Bleed) damage *= (10_000m + request.Build.MoreBleedDamageBasisPoints) / 10_000;
                damage *= (10_000m + Value(ItemModifierKind.DamageOverTimeMultiplierBasisPoints) + (kind == Ailment.Ignite && MasteryRuntime.Has(passive, "点燃", 6) ? 3_000 : 0) + (critical ? 5_000 + (kind == Ailment.Poison && MasteryRuntime.Has(passive, "中毒", 3) ? 10_000 : 0) : 0)) / 10_000;
                if (configuration.Supports.HasFlag(SkillSupport.Brutality) && output != DamageType.Physical) continue;
                damage *= MasteryDamageRules.AilmentMultiplier(passive, branch, output, kind == Ailment.Poison) / 10_000m;
                if (kind == Ailment.Bleed)
                    damage *= (MasteryRuntime.Has(passive, "流血", 0) ? 2m : 1m) * (MasteryRuntime.Has(passive, "流血", 2) ? .8m : 1m);
                if (kind == Ailment.Ignite && MasteryRuntime.Has(passive, "点燃", 0)) damage *= 1.3m;
                damage *= DamageOverTimeMasteryRules.OutputMultiplier(passive, true) / 10_000m;
                total += damage;
            }
            return total;
        }
        void Apply(Ailment kind, DamageType type, decimal ratio, int duration, int faster)
        {
            decimal dps = Basis(kind) * ratio;
            if (dps <= 0) return;
            int compositeMultiplier = DamageOverTimeMasteryRules.NewEffectMultiplier(passive, enemy.Ailments, type);
            dps *= compositeMultiplier / 10_000m;
            duration = checked(duration * DamageOverTimeMasteryRules.DurationMultiplier(passive) / 10_000);
            faster = checked(faster + DamageOverTimeMasteryRules.FasterAilments(passive));
            duration = duration * Math.Max(0, 10_000 - enemy.Profile.ReducedAilmentDurationBasisPoints) / 10_000;
            decimal? debuffedDps = type == DamageType.Void && MasteryRuntime.Has(passive, "虚空", 4)
                ? Basis(kind, true) * ratio * compositeMultiplier / 10_000m : null;
            bool selfCast = request.EquipmentRuntime?.CaptureAction().Copy != true && (request.ElementalSourceSelf || request.EquipmentRuntime?.CaptureAction().Triggered != true);
            if (kind == Ailment.Poison)
                dps = enemy.Ailments.ApplyPoison(passive, dps, duration, faster, skill.SkillId,
                    request.Actions?.CanonicalAction(request.EquipmentRuntime!.ActionId) ?? request.EquipmentRuntime?.ActionId ?? $"{tick}:{skill.SkillId}",
                    selfCast, debuffedDps);
            else enemy.Ailments.Apply(kind, type, dps, duration, faster, skill.SkillId,
                debuffedDamagePerSecond: debuffedDps, selfCast: selfCast);
            events.Add(Event(tick, SpatialEventKind.Ailment, "hero", enemy.EntityId, 0, origin, enemy.Position,
                $"skill:{skill.SkillId}|ailment:{kind.ToString().ToLowerInvariant()}|dps:{dps:0.###}"));
        }
        if (!configuration.Supports.HasFlag(SkillSupport.Bloodlust) && hit.Physical > 0 && Allowed(Ailment.Bleed,
            Chance(Ailment.Bleed) + Value(ItemModifierKind.BleedChanceBasisPoints) +
            (skill.Ailment == Ailment.Bleed ? 0 : skill.BleedChanceBasisPoints) +
            MasteryRuntime.AdditionalBleedChance(passive, SkillDefinitions.Get(skill.SkillId).Tags, request.Build.Weapon)))
        {
            AilmentMasteryRules.Configure(enemy.Ailments, passive, request.AscendancyRuntime?.TwoBleeds == true, Element("fire", "core"));
            Apply(Ailment.Bleed, DamageType.Physical, .7m,
                CombatRules.ApplyIncreased(5_000, Value(ItemModifierKind.IncreasedBleedDurationBasisPoints) + passive.SpecializedValue(PassiveEffectKind.IncreasedBleedDurationBasisPoints)), Value(ItemModifierKind.FasterBleedBasisPoints) + (MasteryRuntime.Has(passive, "流血", 3) ? 5_000 : 0));
            request.AscendancyRuntime?.AppliedBleed();
        }
        if (hit.Physical + hit.Void > 0 && Allowed(Ailment.Poison, Chance(Ailment.Poison) + Value(ItemModifierKind.PoisonChanceBasisPoints) + (MasteryRuntime.Has(passive, "虚空", 1) ? 2_000 : 0), critical && MasteryRuntime.Has(passive, "中毒", 3)))
            Apply(Ailment.Poison, DamageType.Void, .3m, 2_000, Value(ItemModifierKind.FasterPoisonBasisPoints));
        if (hit.Fire + (MasteryRuntime.Has(passive, "点燃", 3) ? hit.Cold + hit.Lightning : 0) > 0 && Allowed(Ailment.Ignite, Chance(Ailment.Ignite) + Value(ItemModifierKind.IgniteChanceBasisPoints), critical))
        {
            AilmentMasteryRules.Configure(enemy.Ailments, passive, request.AscendancyRuntime?.TwoBleeds == true, Element("fire", "core"));
            Apply(Ailment.Ignite, DamageType.Fire, .9m, CombatRules.ApplyIncreased(4_000, (Element("ailment") ? 2_500 : 0) + (MasteryRuntime.Has(passive, "点燃", 0) ? 6_000 : 0)), Value(ItemModifierKind.FasterIgniteBasisPoints) + (MasteryRuntime.Has(passive, "点燃", 2) ? 6_000 : 0));
        }
        if (hit.Cold > 0 && Allowed(Ailment.Chill, 10_000))
        {
            var chill = CombatRules.Chill(hit.Cold, threshold,
                maximumEffectBasisPoints: ElementalControlMasteryRules.ChillMaximum(passive),
                increasedEffectBasisPoints: Value(ItemModifierKind.ChillEffectBasisPoints) + (Element("cold") ? 2_500 : 0) + effectIncrease);
            if (chill.EffectBasisPoints > 0)
            {
                enemy.ChillEffect = Math.Max(enemy.ChillEffect, chill.EffectBasisPoints);
                enemy.ImpairedUntilTick = Math.Max(enemy.ImpairedUntilTick, tick + Duration(chill.DurationMilliseconds) / TickMilliseconds);
                enemy.PropagatedChill = false;
            }
        }
        if (hit.Cold > 0)
        {
            if (Allowed(Ailment.Freeze, Chance(Ailment.Freeze), critical))
            {
                int freezeThreshold = enemy.Boss && Element("cold", "core") ? Math.Max(1, threshold * 6 / 10) : threshold;
                var freeze = CombatRules.Freeze(ElementalControlMasteryRules.FreezeDamage(passive, hit.Cold), freezeThreshold,
                    Value(ItemModifierKind.FreezeEffectBasisPoints) + effectIncrease + (Element("ailment") ? 2_500 : 0),
                    enemy.Boss ? 1_000 : enemy.Rarity == EnemyRarity.Rare ? 2_000 : 3_000,
                    MasteryRuntime.Has(passive, "冰缓_冻结", 1) ? 1 : 300);
                if (freeze.DurationMilliseconds > 0)
                {
                    enemy.FrozenUntil = Math.Max(enemy.FrozenUntil, tick + freeze.DurationMilliseconds / TickMilliseconds);
                    enemy.ColdPursuitUntil = tick + 80;
                }
            }
        }
        int lightningDamage = ElementalControlMasteryRules.LightningDamage(passive, hit.Lightning, critical);
        if (hit.Lightning > 0 && ElementalControlMasteryRules.CanShock(passive) &&
            Allowed(Ailment.Shock, Chance(Ailment.Shock) + Value(ItemModifierKind.ShockChanceBasisPoints), critical))
        {
            int cap = ElementalControlMasteryRules.ShockMaximum(passive, Element("lightning", "core") ? 7_500 : 5_000);
            var shock = CombatRules.Shock(lightningDamage, threshold, cap,
                Value(ItemModifierKind.ShockEffectBasisPoints) + (Element("lightning") ? 2_500 : 0) + effectIncrease +
                (MasteryRuntime.Has(passive, "感电_麻痹", 4) ? 10_000 : 0));
            enemy.ShockEffect = Math.Max(enemy.ShockEffect, Math.Min(cap, ElementalControlMasteryRules.ShockEffect(passive, shock.EffectBasisPoints)));
            enemy.ShockUntil = Math.Max(enemy.ShockUntil, tick + Duration(2_000) / TickMilliseconds);
        }
        if (hit.Lightning > 0 && ElementalControlMasteryRules.CanParalyze(passive) &&
            Allowed(Ailment.Paralysis, ElementalControlMasteryRules.ParalysisChance(passive, Chance(Ailment.Paralysis))))
        {
            enemy.Paralysis += CombatRules.Paralysis(lightningDamage, threshold,
                ElementalControlMasteryRules.ParalysisAccumulationIncrease(passive)).AccumulationBasisPoints;
            enemy.ParalysisLastTick = tick;
            if (enemy.Paralysis >= 10_000)
            {
                enemy.Paralysis = 0;
                enemy.ParalyzedUntil = Math.Max(enemy.ParalyzedUntil, tick + (enemy.Boss ? 350 : enemy.Rarity == EnemyRarity.Rare ? 600 : enemy.Rarity == EnemyRarity.Magic ? 800 : 1_000) / TickMilliseconds);
                enemy.ParalysisPursuitUntil = tick + 80;
            }
        }
        bool selfAction = request.EquipmentRuntime?.CaptureAction() is not { Triggered: true } and not { Copy: true };
        bool qualifiedVoid = selfAction && hit.Void > 0 && (SkillDefinitions.Get(skill.SkillId).Tags & (SkillTag.Attack | SkillTag.Spell)) != 0;
        foreach (var kind in new[] { Ailment.Erosion, Ailment.Wither })
        {
            int chance = (skill.Ailment == kind ? skill.AilmentChanceBasisPoints : 0) + (qualifiedVoid ? passive.SpecializedValue(
                kind == Ailment.Erosion ? PassiveEffectKind.VoidHitErosionChanceBasisPoints : PassiveEffectKind.VoidHitWitherChanceBasisPoints) : 0);
            if (chance > 0 && Allowed(kind, chance))
                enemy.Ailments.ApplyVoidDebuff(passive, kind, tick,
                    request.Actions?.CanonicalAction(request.EquipmentRuntime?.ActionId ?? "") ?? $"{tick}:{skill.SkillId}",
                    selfAction, enemy.Profile.ReducedAilmentDurationBasisPoints);
        }
        if (Allowed(Ailment.ArmorBreak, (skill.Ailment == Ailment.ArmorBreak ? skill.AilmentChanceBasisPoints : 0) +
            (hit.Physical > 0 ? request.Auras?.PhysicalArmorBreakChance ?? 0 : 0)))
        { enemy.ArmorBreakStacks = Math.Min(5, enemy.ArmorBreakStacks + 1); enemy.ArmorBreakUntil = tick + 100; }
        if (Allowed(Ailment.Stun, StunMasteryRules.Chance(passive, hit.Total, threshold)))
            ApplyStun(request, enemy, tick, origin, events, false);
    }

    private static void ApplyStun(NodeCombatRequest request, EnemyUnit enemy, int tick, Point origin,
        ICollection<SpatialEvent> events, bool propagated)
    {
        var passive = request.Build.PassiveProfile ?? PassiveModifiers.Empty;
        int duration = StunMasteryRules.Duration(passive, enemy.Boss ? 500 : enemy.Rarity == EnemyRarity.Rare ? 1_000 : 1_500,
            enemy.Profile.ReducedAilmentDurationBasisPoints);
        if (duration <= 0) return;
        int until = tick + Math.Max(1, duration / TickMilliseconds);
        enemy.StunnedUntilTick = Math.Max(enemy.StunnedUntilTick, until);
        if (!propagated) enemy.OriginalStunUntil = Math.Max(enemy.OriginalStunUntil, until);
        enemy.StunPursuitUntil = tick + 80;
        request.Stun?.Applied(tick);
        if (MasteryRuntime.Has(passive, "眩晕", 6) && tick >= enemy.StunArmorBreakReadyTick)
        {
            enemy.StunArmorBreakReadyTick = tick + 20;
            enemy.ArmorBreakStacks = Math.Min(request.AscendancyRuntime?.ArmorBreakMaximum ?? 5, enemy.ArmorBreakStacks + 3);
            enemy.ArmorBreakUntil = Math.Max(enemy.ArmorBreakUntil, tick + 100);
        }
        events.Add(Event(tick, SpatialEventKind.Ailment, "hero", enemy.EntityId, duration, origin, enemy.Position,
            propagated ? "mastery:stun-spread" : "ailment:stun"));
    }

    private static decimal DefendEnemyDot(NodeCombatRequest request, EnemyUnit enemy, DamageType type, decimal dps, int tick)
    {
        decimal damage = dps;
        if (type == DamageType.Physical) damage *= (10_000 - CombatRules.PhysicalDotArmorReduction(
            CombatRules.ArmorAfterBreak(enemy.Scaled.Armor, enemy.ArmorBreakStacks), (int)Math.Min(int.MaxValue, dps))) / 10_000m;
        int resistance = type == DamageType.Physical ? enemy.Scaled.PhysicalResistanceBasisPoints + request.EnemyPhysicalReductionBasisPoints :
            EnemyResistance(enemy, request, type == DamageType.Fire ? SkillDamageType.Fire : type == DamageType.Void ? SkillDamageType.Void : type == DamageType.Cold ? SkillDamageType.Cold : SkillDamageType.Lightning, penetrate: false);
        damage *= (10_000 - Math.Clamp(resistance, CombatRules.MinimumResistance, type == DamageType.Physical ? 5_000 : 7_500)) / 10_000m;
        damage *= ElementalRules.TargetMultiplier(request.Build.Ascendancy, type, ElementalStatus(enemy, tick), false, false) / 10_000m;
        damage *= (10_000 + enemy.ShockEffect) / 10_000m;
        if (type == DamageType.Void)
            damage *= CombatRules.WitherMultiplier(enemy.Ailments.Stack(Ailment.Wither, tick), 15) / 10_000m *
                (10_000 + enemy.Curses.Effect("archetypes.skill.doom_brand", tick)) / 10_000m;
        damage *= (10_000 - CombatRules.SpiritBarrierReduction(enemy.Profile.SpiritBarrier, (int)Math.Min(int.MaxValue, damage))) / 10_000m;
        return damage;
    }

    private static void AdvanceAilments(NodeCombatRequest request, IEnumerable<EnemyUnit> enemies, ResourceState hero, int tick, ICollection<SpatialEvent> events)
    {
        bool recovered = false;
        foreach (var enemy in enemies.Where(enemy => enemy.Life > 0))
        {
            if (tick >= enemy.ShockUntil) enemy.ShockEffect = 0;
            if (tick >= enemy.ImpairedUntilTick) enemy.ChillEffect = 0;
            if (tick >= enemy.ArmorBreakUntil) enemy.ArmorBreakStacks = 0;
            if (tick - enemy.ParalysisLastTick >= ElementalControlMasteryRules.ParalysisDecayDelayTicks(
                    request.Build.PassiveProfile ?? PassiveModifiers.Empty))
                enemy.Paralysis = Math.Max(0, enemy.Paralysis - 125);
            enemy.CurrentTick = tick;
            foreach (var pulse in enemy.Ailments.Advance(TickMilliseconds,
                (type, dps) => DefendEnemyDot(request, enemy, type, dps, tick), VoidDebuffed(enemy, tick)))
            {
                int damage = Math.Min(enemy.Life, pulse.Damage);
                if (damage > 0) request.Elemental?.Observe(pulse.Type, tick, !pulse.SelfCast);
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
                if (enemy.Life == 0)
                {
                    PassiveModifiers passives = request.Build.PassiveProfile ?? PassiveModifiers.Empty;
                    if (DamageOverTimeMasteryRules.RecoversOnKill(passives) && request.DamageOverTimeRecovery!.TryRecover(tick))
                    {
                        int life = hero.HealLife(Math.Max(1, hero.MaximumLife * 200 / 10_000));
                        int mana = hero.RestoreMana(Math.Max(1, hero.MaximumMana * 200 / 10_000));
                        events.Add(Event(tick, SpatialEventKind.SkillEffect, "hero", "hero", life + mana,
                            enemy.Position, enemy.Position, "mastery:damage-over-time-recovery"));
                    }
                    events.Add(Event(tick, SpatialEventKind.EnemyDefeated, "hero", enemy.EntityId, 0, enemy.Position, enemy.Position, enemy.Profile.StableId));
                    break;
                }
            }
        }
    }
}
