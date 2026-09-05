using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Equipment;
using GameForWork.Core.Skills;

namespace GameForWork.Core.Spatial;

public sealed partial class SpatialCombatRunner
{
    private static bool ApplyCurse(NodeCombatRequest request, ResolvedSkill skill, SkillConfiguration configuration,
        EnemyUnit target, IReadOnlyCollection<EnemyUnit> enemies, Point origin, int tick, ICollection<SpatialEvent> events)
    {
        string id = skill.SkillId;
        if (id is not ("archetypes.skill.death_mark" or "archetypes.skill.elemental_hex" or "archetypes.skill.enfeeble_hex")) return false;
        var equipment = request.Build.CombatEquipment ?? EquipmentCombatLoadout.Empty;
        int increase = equipment.Value(ItemModifierKind.IncreasedCurseEffectBasisPoints) +
            (request.Build.PassiveProfile?.SpecializedValue(GameForWork.Core.Campaign.Progression.PassiveEffectKind.IncreasedCurseEffectBasisPoints) ?? 0) + (id == "archetypes.skill.elemental_hex" ? 0 : configuration.Quality * 50);
        int effect = CombatRules.ApplyIncreased(ActiveSkillCatalog.Interpolate(1_500, id == "archetypes.skill.enfeeble_hex" ? 2_500 : 3_000, configuration.Level, false), increase);
        int secondary = id == "archetypes.skill.enfeeble_hex" ? CombatRules.ApplyIncreased(ActiveSkillCatalog.Interpolate(1_000, 1_800, configuration.Level, false), increase) : 0;
        int duration = CombatRules.ApplyIncreased(200, equipment.Value(ItemModifierKind.IncreasedCurseDurationBasisPoints) +
            (id == "archetypes.skill.elemental_hex" ? configuration.Quality * 100 : 0));
        int range = (int)(4_000 * Math.Sqrt(Math.Max(.25, 1 + equipment.Value(ItemModifierKind.IncreasedCurseRangeBasisPoints) / 10_000d)));
        var targets = id == "archetypes.skill.death_mark" ? new[] { target } : enemies.Where(enemy => enemy.Life > 0 && InRange(target.Position, enemy.Position, range)).ToArray();
        if (id == "archetypes.skill.death_mark") foreach (var enemy in enemies) enemy.Curses.Remove(id);
        foreach (var enemy in targets)
        {
            enemy.Curses.Apply(id, effect, secondary, tick + duration, 1 + equipment.Value(ItemModifierKind.AdditionalCurseMaximum), tick);
            events.Add(Event(tick, SpatialEventKind.SkillEffect, "hero", enemy.EntityId, effect, origin, enemy.Position, $"curse:{id}|until:{tick + duration}"));
        }
        return true;
    }
}
