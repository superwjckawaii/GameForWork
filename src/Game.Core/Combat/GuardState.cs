using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Archetypes;
using GameForWork.Core.Skills;

namespace GameForWork.Core.Combat;

public sealed class GuardState
{
    public int Remaining { get; private set; }
    public int Expires { get; private set; }
    public bool ElementalOnly { get; private set; }
    public int ArmorEnergy { get; private set; }
    public int LastPaidShield { get; private set; }
    public void GainEnergy(int amount) => ArmorEnergy = Math.Clamp(ArmorEnergy + amount, 0, 10);
    public int ConsumeEnergy() { int energy = ArmorEnergy; ArmorEnergy = 0; return energy; }
    public static int ShieldCost(string skillId, int maximumShield) => skillId switch
    {
        "archetypes.skill.spellarmor_activate" => Math.Max(1, maximumShield / 5),
        "archetypes.skill.aegis_pulse" => Math.Max(1, (int)((long)maximumShield * 800 / 10_000)),
        _ => 0,
    };
    public void Extend(int ticks) => Expires += Math.Max(0, ticks);
    public void ApplySupports(SkillConfiguration config, ResourceState hero)
    {
        if (!LinkedSupportRules.Support(config, SupportMechanic.SpellArmorFusion)) return;
        int ratio = LinkedSupportRules.SupportValue(config, SupportMechanic.SpellArmorFusion, 1_500, 2_500);
        int extra = (int)Math.Min(int.MaxValue, ((long)hero.Sheet.Armor().Value + hero.MaximumShield) * ratio / 10_000);
        Remaining = CombatRules.ApplyIncreased((int)Math.Min(int.MaxValue, (long)Remaining + extra),
            LinkedSupportRules.SupportQuality(config, SupportMechanic.SpellArmorFusion) * 50);
    }
    public bool Activate(string skillId, ResourceState hero, int level, int quality, int tick)
    {
        int capacity, duration;
        LastPaidShield = 0;
        bool elemental = skillId == SkillIds.PrismaticGuard;
        int cost = ShieldCost(skillId, hero.MaximumShield);
        if (cost > 0)
        {
            if (!hero.TryPayShield(cost)) return false;
            LastPaidShield = cost;
            bool armor = skillId == "archetypes.skill.spellarmor_activate";
            int ratio = armor ? ActiveSkillCatalog.Interpolate(15_000, 25_000, level, false) : 15_000;
            capacity = CombatRules.ApplyIncreased((int)Math.Min(int.MaxValue, (long)ratio * cost / 10_000), quality * (armor ? 100 : 150));
            duration = armor ? 120 : 60;
            if (armor) ArmorEnergy = 10;
        }
        else
        {
            int ratio = elemental ? ActiveSkillCatalog.Interpolate(3_000, 5_000, level, false) :
                ActiveSkillCatalog.Interpolate(2_500, 4_000, level, false);
            capacity = (int)Math.Min(int.MaxValue, ((long)hero.MaximumLife + hero.MaximumShield) * ratio / 10_000);
            if (elemental) capacity = CombatRules.ApplyIncreased(capacity, quality * 100);
            duration = elemental ? 80 : CombatRules.ApplyIncreased(60, quality * 100);
        }
        Remaining = capacity; Expires = tick + duration; ElementalOnly = elemental;
        return true;
    }
    public int Absorb(int damage, EnemyDamageType type, int tick)
    {
        if (tick >= Expires) Remaining = 0;
        if (Remaining <= 0 || damage <= 0 || ElementalOnly && type == EnemyDamageType.Physical) return damage;
        int absorbed = Math.Min(Remaining, damage);
        Remaining -= absorbed;
        return damage - absorbed;
    }
}
