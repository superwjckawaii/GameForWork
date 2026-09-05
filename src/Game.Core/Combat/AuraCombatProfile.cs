using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Campaign.Progression;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Equipment;
using GameForWork.Core.SkillCatalog;

namespace GameForWork.Core.Combat;

public sealed record AuraCombatProfile(TeamBuild Build, int ReservedMana, IReadOnlyList<string> ActiveIds,
    int IncomingHitMultiplier, int UnitDamageMultiplier, int UnitLifeIncrease, int UnitSpeedIncrease,
    int HunterCriticalMultiplier, bool HunterAlwaysHits, int EnemyArmorReduction, int SkillCostMultiplier,
    int PhysicalArmorBreakChance, bool PhysicalFortification, DamageType? ExclusiveElement = null)
{
    public static AuraCombatProfile Resolve(TeamBuild original)
    {
        var equipment = original.CombatEquipment ?? EquipmentCombatLoadout.Empty;
        var modifiers = new Dictionary<ItemModifierKind, int>(equipment.Modifiers);
        CharacterSheet sheet = original.Sheet;
        TeamBuild build = original;
        var active = new List<string>();
        int reserved = 0, maximumMana = sheet.MaximumMana().Value, incoming = 10_000, unitDamage = 10_000,
            unitLife = 0, unitSpeed = 0, hunterCritical = 0, armorReduction = 0, costs = 10_000, armorBreak = 0;
        bool hunter = false, fortification = false;
        DamageType? exclusiveElement = null;
        void Add(ItemModifierKind kind, int value) => modifiers[kind] = modifiers.GetValueOrDefault(kind) + value;
        SkillConfiguration? imprint = original.ActiveSkills?.FirstOrDefault(skill => skill.SkillId == "archetypes.skill.elemental_imprint");
        DamageType majorElement = imprint?.Mode is "Cold" or "寒霜" ? DamageType.Cold : imprint?.Mode is "Lightning" or "闪电" ? DamageType.Lightning : DamageType.Fire;
        if (imprint is not null)
            Add(majorElement == DamageType.Cold ? ItemModifierKind.IncreasedColdDamageBasisPoints : majorElement == DamageType.Lightning ? ItemModifierKind.IncreasedLightningDamageBasisPoints : ItemModifierKind.IncreasedFireDamageBasisPoints,
                ActiveSkillCatalog.Interpolate(2_000, 4_000, imprint.Level, false));
        void LessDamage(int less)
        {
            var passive = build.PassiveProfile ?? PassiveModifiers.Empty;
            build = build with { PassiveProfile = passive with { MoreDamageBasisPoints = CombatRules.CombineMoreBasisPoints(passive.MoreDamageBasisPoints, -less) } };
        }
        foreach (var skill in (original.ActiveSkills ?? []).OrderBy(skill => skill.Priority)
            .GroupBy(skill => skill.SkillId).Select(group => group.OrderByDescending(skill => skill.Level).First()))
        {
            string id = skill.SkillId;
            int reservation = id switch
            {
                SkillIds.IronOathBanner => equipment.Has("末旗护符") ? 0 : 1_500,
                SkillIds.BreachBanner => 1_000, SkillIds.ElementalResonance or "archetypes.skill.courage_hymn" => 2_000,
                "builds.skill.swift_war_rhythm" => 3_500, "builds.skill.hunter_banner" => 4_000,
                "builds.skill.hundred_soul_army" => 4_500, "builds.skill.undying_sanctuary" or "builds.skill.primal_reflection" => 5_000,
                _ => -1,
            };
            if (reservation < 0) continue;
            bool banner = id is SkillIds.IronOathBanner or SkillIds.BreachBanner or "builds.skill.hunter_banner";
            if (banner && equipment.Has("末旗护符")) reservation = 0;
            int efficiency = equipment.Value(ItemModifierKind.ReservationEfficiencyBasisPoints);
            if (original.Ascendancy?.Has("core.ascendancy.spirit_cantor.reservation.core") == true) efficiency += 6_000;
            int amount = (int)(((long)maximumMana * reservation + Math.Max(1, 10_000 + efficiency) - 1) / Math.Max(1, 10_000 + efficiency));
            if (reserved + amount > maximumMana) continue;
            reserved += amount; active.Add(id);
            int effect = equipment.Value(ItemModifierKind.IncreasedAuraEffectBasisPoints);
            if (banner && equipment.Has("末旗护符")) effect += 8_000;
            if (original.Ascendancy?.Has("core.ascendancy.spirit_cantor.aura.core") == true) effect += 5_000;
            if (skill.Supports.HasFlag(SkillSupport.BannerPotency)) effect += 2_500;
            int Value(int one, int twentyOne) => CombatRules.ApplyIncreased(ActiveSkillCatalog.Interpolate(one, twentyOne, skill.Level, false), effect);
            switch (id)
            {
                case SkillIds.IronOathBanner:
                    Add(ItemModifierKind.IncreasedPhysicalDamageBasisPoints, Value(4_000, 7_000));
                    sheet = sheet with { IncreasedArmorBasisPoints = sheet.IncreasedArmorBasisPoints + Value(6_000, 10_000) };
                    fortification = true; break;
                case SkillIds.BreachBanner: armorReduction = Value(2_000, 3_500); armorBreak = Value(3_000, 5_000); break;
                case SkillIds.ElementalResonance:
                    Add(ItemModifierKind.IncreasedElementalDamageBasisPoints, Value(4_000, 7_000));
                    foreach (var kind in new[] { ItemModifierKind.FirePenetrationBasisPoints, ItemModifierKind.ColdPenetrationBasisPoints, ItemModifierKind.LightningPenetrationBasisPoints }) Add(kind, Value(800, 1_500));
                    Resist(Value(1_500, 2_500), false); break;
                case "archetypes.skill.courage_hymn":
                    Resist(Value(2_000, 3_000), true);
                    Add(ItemModifierKind.ReducedDebuffDurationBasisPoints, Value(3_500, 6_000)); break;
                case "builds.skill.swift_war_rhythm":
                    build = build with { IncreasedActionSpeedBasisPoints = build.IncreasedActionSpeedBasisPoints + Value(3_500, 6_000),
                        MovementSpeedBasisPoints = build.MovementSpeedBasisPoints + Value(2_000, 3_500) };
                    Add(ItemModifierKind.IncreasedCooldownRecoveryBasisPoints, Value(2_000, 3_500)); costs = 12_500 - Math.Min(20, skill.Quality) * 25; break;
                case "builds.skill.hunter_banner":
                    hunter = true; hunterCritical = Value(2_000, 4_000);
                    build = build with { MoreRareBossDamageBasisPoints = CombatRules.CombineMoreBasisPoints(build.MoreRareBossDamageBasisPoints, Value(3_000, 5_000)) }; break;
                case "builds.skill.hundred_soul_army":
                    unitDamage = 10_000 + Value(3_500, 6_000); unitLife = Value(5_000, 9_000) + skill.Quality * 100; unitSpeed = Value(2_500, 4_000); LessDamage(2_000); break;
                case "builds.skill.undying_sanctuary":
                    incoming = 10_000 - Value(1_500, 2_500); LessDamage(2_000 - Math.Min(20, skill.Quality) * 25);
                    sheet = sheet with { MaximumElementalResistanceBasisPoints = sheet.MaximumElementalResistanceBasisPoints + Value(200, 400),
                        MaximumVoidResistanceBasisPoints = sheet.MaximumVoidResistanceBasisPoints + Value(200, 400),
                        IncreasedRecoveryRateBasisPoints = sheet.IncreasedRecoveryRateBasisPoints + Value(3_000, 5_000) }; break;
                case "builds.skill.primal_reflection":
                    exclusiveElement = majorElement;
                    string mode = original.ActiveSkills?.FirstOrDefault(skill => skill.SkillId == "archetypes.skill.elemental_imprint")?.Mode ?? "Fire";
                    var extra = mode is "Cold" or "寒霜" ? ItemModifierKind.PhysicalAsExtraColdBasisPoints : mode is "Lightning" or "闪电" ? ItemModifierKind.PhysicalAsExtraLightningBasisPoints : ItemModifierKind.PhysicalAsExtraFireBasisPoints;
                    Add(extra, Value(4_000, 7_000));
                    Add(extra == ItemModifierKind.PhysicalAsExtraColdBasisPoints ? ItemModifierKind.ColdPenetrationBasisPoints : extra == ItemModifierKind.PhysicalAsExtraLightningBasisPoints ? ItemModifierKind.LightningPenetrationBasisPoints : ItemModifierKind.FirePenetrationBasisPoints, Value(1_000, 2_000)); break;
            }
        }
        return new(build with { Sheet = sheet, CombatEquipment = equipment with { Modifiers = modifiers } }, reserved, active,
            incoming, unitDamage, unitLife, unitSpeed, hunterCritical, hunter, armorReduction, costs, armorBreak, fortification, exclusiveElement);
        void Resist(int value, bool abyss) => sheet = sheet with
        { FireResistanceBasisPoints = sheet.FireResistanceBasisPoints + value, ColdResistanceBasisPoints = sheet.ColdResistanceBasisPoints + value,
            LightningResistanceBasisPoints = sheet.LightningResistanceBasisPoints + value, VoidResistanceBasisPoints = sheet.VoidResistanceBasisPoints + (abyss ? value : 0) };
    }
}
