using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Progression;
using GameForWork.Core.Ascendancies;

namespace GameForWork.Core.Characters;

public enum BaseClass
{
    Fighter = 0,
    Rogue = 1,
    Psion = 2,
    Occultist = 3,
    Monk = 4,
    Hermit = 5,
}

public sealed record ClassDefinition(
    BaseClass Id,
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
    IReadOnlyList<Ascendancy> Ascendancies);

public static class ClassCatalog
{
    private static readonly IReadOnlyDictionary<BaseClass, ClassDefinition> Definitions =
        new[]
        {
            Define(BaseClass.Fighter, "fighter", "斗士", new(20, 10, 10, 10), PassiveStartKind.Physique,
                "重型近战、流血、护甲与盾牌", "core.base.rusted_greatsword", "core.base.crude_chainmail",
                "core.base.iron_helmet", "core.skill.heavy_strike", "近战强攻",
                Ascendancy.BloodFighter, Ascendancy.IronGuardian, Ascendancy.Warbreaker),
            Define(BaseClass.Rogue, "rogue", "侠客", new(10, 20, 10, 10), PassiveStartKind.Dexterity,
                "远程投射、暴击、毒素与陷阱", "equipmentImport.base.harpy_rapier", "core.base.hide_coat",
                "core.base.hunter_hood", "core.skill.spirit_blade", "游击远程",
                Ascendancy.Marksman, Ascendancy.Shadowblade, Ascendancy.Venomist),
            Define(BaseClass.Psion, "psion", "灵能使", new(10, 10, 20, 10), PassiveStartKind.Spirit,
                "召唤、光环、祝福与诅咒", "equipmentImport.base.ceremonial_mace", "core.base.runed_robe",
                "core.base.ash_circlet", "core.skill.void_decay_field", "灵能支援",
                Ascendancy.SoulShepherd, Ascendancy.SpiritCantor, Ascendancy.Hexbinder),
            Define(BaseClass.Occultist, "occultist", "秘术师", new(10, 10, 10, 20), PassiveStartKind.Energy,
                "元素法术、虚空侵蚀与能量护盾", "equipmentImport.base.broad_sword", "core.base.starweave_robe",
                "core.base.oracle_crown", "core.skill.chain_lightning", "远程施法",
                Ascendancy.Elementalist, Ascendancy.VoidScholar, Ascendancy.AegisMage),
            Define(BaseClass.Monk, "monk", "僧侣", new(10, 15, 15, 10), PassiveStartKind.DexteritySpirit,
                "徒手连击、灵兽伙伴与幻身", "core.base.rusted_warhammer", "equipmentImport.base.carnal_armour",
                "core.base.raven_mask", "core.skill.seismic_charge", "机动连击",
                Ascendancy.MartialMonk, Ascendancy.BeastKeeper, Ascendancy.PhantomMaster),
            Define(BaseClass.Hermit, "hermit", "隐士", new(15, 10, 10, 15), PassiveStartKind.PhysiqueEnergy,
                "刻印武技、魔铠与构装体", "equipmentImport.base.flanged_mace", "core.base.triune_carapace",
                "core.base.warlord_helm", "core.skill.ember_nova", "刻印战法",
                Ascendancy.Runecarver, Ascendancy.Spellarmor, Ascendancy.IdolForger),
        }.ToDictionary(definition => definition.Id);

    public static IReadOnlyCollection<ClassDefinition> All => Definitions.Values.OrderBy(value => value.Id).ToArray();

    public static ClassDefinition Get(BaseClass id) => Definitions.TryGetValue(id, out ClassDefinition? value)
        ? value
        : throw new KeyNotFoundException($"Unknown base class: {id}");

    public static bool Allows(BaseClass id, Ascendancy ascendancy) => Get(id).Ascendancies.Contains(ascendancy);

    private static ClassDefinition Define(BaseClass id, string stableId, string name,
        CharacterAttributes attributes, PassiveStartKind start, string summary, string weapon, string chest,
        string helmet, string skill, string ai, params Ascendancy[] ascendancies) =>
        new(id, $"core.class.{stableId}", name, attributes, start, summary, weapon, chest, helmet, skill, ai, ascendancies);
}

public enum CombatEntityKind { Hero, Mercenary, Minion, Companion, Construct, Trap }

public sealed record CombatOwner(string StableId, CombatEntityKind Kind, string OwnerStableId = "");

public static class CombatLimits
{
    public const int MaximumMinions = 6;
    public const int MaximumCompanions = 1;
    public const int MaximumConstructs = 3;
    public const int MaximumTraps = 3;

    public static int Maximum(CombatEntityKind kind) => kind switch
    {
        CombatEntityKind.Minion => MaximumMinions,
        CombatEntityKind.Companion => MaximumCompanions,
        CombatEntityKind.Construct => MaximumConstructs,
        CombatEntityKind.Trap => MaximumTraps,
        _ => 1,
    };
}

public sealed class EnergyShieldState
{
    public const int RechargeDelayTicks = 40;
    public const int RechargeBasisPointsPerSecond = 2_000;

    public EnergyShieldState(int maximum, CombatProfile? ascendancy = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximum);
        EnergyShieldProfile profile = ClassAscendancyRules.EnergyShield(ascendancy ?? CombatProfile.Empty);
        Maximum = ModifierMath.ApplyMore(maximum, profile.MoreMaximumBasisPoints);
        RechargeDelay = profile.RechargeDelayTicks;
        RechargeRateBasisPointsPerSecond = ModifierMath.ApplyIncreased(
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

public sealed record RangeAiProfile(int PreferredDistanceRaw, int RetreatDistanceRaw, bool MoveWhileAttacking)
{
    public RangeAiProfile Validate()
    {
        if (PreferredDistanceRaw is < 1_000 or > 30_000 || RetreatDistanceRaw is < 0 or > 30_000 ||
            RetreatDistanceRaw >= PreferredDistanceRaw)
            throw new ArgumentOutOfRangeException(nameof(PreferredDistanceRaw));
        return this;
    }

    public static RangeAiProfile For(BaseClass baseClass) => baseClass switch
    {
        BaseClass.Rogue => new(8_000, 3_000, true),
        BaseClass.Psion or BaseClass.Occultist => new(7_000, 2_500, false),
        _ => new(2_000, 500, false),
    };
}
