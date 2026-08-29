using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.Progression;
using GameForWork.Core.P18;

namespace GameForWork.Core.P23;

public enum P23BaseClass
{
    Fighter = 0,
    Rogue = 1,
    Psion = 2,
    Occultist = 3,
    Monk = 4,
    Hermit = 5,
}

public sealed record P23ClassDefinition(
    P23BaseClass Id,
    string StableId,
    string DisplayName,
    CharacterAttributes StartingAttributes,
    PassiveStartKind PassiveStart,
    string Summary,
    string StarterWeaponBaseId,
    string StarterChestBaseId,
    string StarterHelmetBaseId,
    string StarterSkillId,
    string AiPreset,
    IReadOnlyList<P18Ascendancy> Ascendancies);

public static class P23ClassCatalog
{
    private static readonly IReadOnlyDictionary<P23BaseClass, P23ClassDefinition> Definitions =
        new[]
        {
            Define(P23BaseClass.Fighter, "fighter", "斗士", new(20, 10, 10, 10), PassiveStartKind.Physique,
                "重型近战、流血、护甲与盾牌", "core.base.rusted_greatsword", "core.base.crude_chainmail",
                "core.base.iron_helmet", "core.skill.heavy_strike", "近战强攻",
                P18Ascendancy.BloodFighter, P18Ascendancy.IronGuardian, P18Ascendancy.Warbreaker),
            Define(P23BaseClass.Rogue, "rogue", "侠客", new(10, 20, 10, 10), PassiveStartKind.Dexterity,
                "远程投射、暴击、毒素与陷阱", "p19.base.harpy_rapier", "core.base.hide_coat",
                "core.base.hunter_hood", "core.skill.spirit_blade", "游击远程",
                P18Ascendancy.Marksman, P18Ascendancy.Shadowblade, P18Ascendancy.Venomist),
            Define(P23BaseClass.Psion, "psion", "灵能使", new(10, 10, 20, 10), PassiveStartKind.Spirit,
                "召唤、光环、祝福与诅咒", "p19.base.ceremonial_mace", "core.base.runed_robe",
                "core.base.ash_circlet", "core.skill.void_decay_field", "灵能支援",
                P18Ascendancy.SoulShepherd, P18Ascendancy.SpiritCantor, P18Ascendancy.Hexbinder),
            Define(P23BaseClass.Occultist, "occultist", "秘术师", new(10, 10, 10, 20), PassiveStartKind.Energy,
                "元素法术、虚空侵蚀与能量护盾", "p19.base.broad_sword", "core.base.starweave_robe",
                "core.base.oracle_crown", "core.skill.chain_lightning", "远程施法",
                P18Ascendancy.Elementalist, P18Ascendancy.VoidScholar, P18Ascendancy.AegisMage),
            Define(P23BaseClass.Monk, "monk", "僧侣", new(10, 15, 15, 10), PassiveStartKind.DexteritySpirit,
                "徒手连击、灵兽伙伴与幻身", "core.base.rusted_warhammer", "p19.base.carnal_armour",
                "core.base.raven_mask", "core.skill.seismic_charge", "机动连击",
                P18Ascendancy.MartialMonk, P18Ascendancy.BeastKeeper, P18Ascendancy.PhantomMaster),
            Define(P23BaseClass.Hermit, "hermit", "隐士", new(15, 10, 10, 15), PassiveStartKind.PhysiqueEnergy,
                "刻印武技、魔铠与构装体", "p19.base.flanged_mace", "core.base.triune_carapace",
                "core.base.warlord_helm", "core.skill.ember_nova", "刻印战法",
                P18Ascendancy.Runecarver, P18Ascendancy.Spellarmor, P18Ascendancy.IdolForger),
        }.ToDictionary(definition => definition.Id);

    public static IReadOnlyCollection<P23ClassDefinition> All => Definitions.Values.OrderBy(value => value.Id).ToArray();

    public static P23ClassDefinition Get(P23BaseClass id) => Definitions.TryGetValue(id, out P23ClassDefinition? value)
        ? value
        : throw new KeyNotFoundException($"Unknown base class: {id}");

    public static bool Allows(P23BaseClass id, P18Ascendancy ascendancy) => Get(id).Ascendancies.Contains(ascendancy);

    private static P23ClassDefinition Define(P23BaseClass id, string stableId, string name,
        CharacterAttributes attributes, PassiveStartKind start, string summary, string weapon, string chest,
        string helmet, string skill, string ai, params P18Ascendancy[] ascendancies) =>
        new(id, $"core.class.{stableId}", name, attributes, start, summary, weapon, chest, helmet, skill, ai, ascendancies);
}

public enum P23CombatEntityKind { Hero, Mercenary, Minion, Companion, Construct, Trap }

public sealed record P23CombatOwner(string StableId, P23CombatEntityKind Kind, string OwnerStableId = "");

public static class P23CombatLimits
{
    public const int MaximumMinions = 6;
    public const int MaximumCompanions = 1;
    public const int MaximumConstructs = 3;
    public const int MaximumTraps = 3;

    public static int Maximum(P23CombatEntityKind kind) => kind switch
    {
        P23CombatEntityKind.Minion => MaximumMinions,
        P23CombatEntityKind.Companion => MaximumCompanions,
        P23CombatEntityKind.Construct => MaximumConstructs,
        P23CombatEntityKind.Trap => MaximumTraps,
        _ => 1,
    };
}

public sealed class P23EnergyShieldState
{
    public const int RechargeDelayTicks = 40;
    public const int RechargeBasisPointsPerSecond = 2_000;

    public P23EnergyShieldState(int maximum, P18CombatProfile? ascendancy = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximum);
        P231EnergyShieldProfile profile = P231AscendancyRules.EnergyShield(ascendancy ?? P18CombatProfile.Empty);
        Maximum = P231ModifierMath.ApplyMore(maximum, profile.MoreMaximumBasisPoints);
        RechargeDelay = profile.RechargeDelayTicks;
        RechargeRateBasisPointsPerSecond = P231ModifierMath.ApplyIncreased(
            RechargeBasisPointsPerSecond, profile.IncreasedRechargeRateBasisPoints);
        Current = Maximum;
    }

    public int Maximum { get; }
    public int Current { get; private set; }
    public int RechargeDelay { get; }
    public int RechargeRateBasisPointsPerSecond { get; }
    public int TicksSinceDamage { get; private set; } = RechargeDelayTicks;
    public bool IsRecharging => Current < Maximum && TicksSinceDamage >= RechargeDelay;

    public int AbsorbHit(int damage)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(damage);
        int absorbed = Math.Min(Current, damage);
        Current -= absorbed;
        TicksSinceDamage = 0;
        return damage - absorbed;
    }

    public void AdvanceTick()
    {
        TicksSinceDamage++;
        if (!IsRecharging || Maximum == 0) return;
        int perTick = Math.Max(1, checked(Maximum * RechargeRateBasisPointsPerSecond / 10_000 / 20));
        Current = Math.Min(Maximum, checked(Current + perTick));
    }
}

public sealed record P23RangeAiProfile(int PreferredDistanceRaw, int RetreatDistanceRaw, bool MoveWhileAttacking)
{
    public P23RangeAiProfile Validate()
    {
        if (PreferredDistanceRaw is < 1_000 or > 30_000 || RetreatDistanceRaw is < 0 or > 30_000 ||
            RetreatDistanceRaw >= PreferredDistanceRaw)
            throw new ArgumentOutOfRangeException(nameof(PreferredDistanceRaw));
        return this;
    }

    public static P23RangeAiProfile For(P23BaseClass baseClass) => baseClass switch
    {
        P23BaseClass.Rogue => new(8_000, 3_000, true),
        P23BaseClass.Psion or P23BaseClass.Occultist => new(7_000, 2_500, false),
        _ => new(2_000, 500, false),
    };
}
