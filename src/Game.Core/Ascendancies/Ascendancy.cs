using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Characters;
using GameForWork.Core.Builds;

namespace GameForWork.Core.Ascendancies;

public enum Ascendancy
{
    None,
    BloodFighter,
    IronGuardian,
    Warbreaker,
    Marksman,
    Shadowblade,
    Venomist,
    SoulShepherd,
    SpiritCantor,
    Hexbinder,
    Elementalist,
    VoidScholar,
    AegisMage,
    MartialMonk,
    BeastKeeper,
    PhantomMaster,
    Runecarver,
    Spellarmor,
    IdolForger,
}

public enum NodeKind { Reinforcement, Core }

public sealed record AscendancyNode(
    string StableId,
    Ascendancy Ascendancy,
    int Direction,
    NodeKind Kind,
    string DisplayName,
    string Effect,
    string? PrerequisiteId,
    int X,
    int Y);

public sealed record CombatProfile(Ascendancy Ascendancy, IReadOnlyList<string> AllocatedNodes, CombatConfiguration? Configuration = null)
{
    public bool Has(string stableId) => AllocatedNodes.Contains(stableId, StringComparer.Ordinal);
    public static CombatProfile Empty { get; } = new(Ascendancy.None, []);
}

public static class WarriorNodeIds
{
    public const string BloodLifeSmall = "core.ascendancy.blood.life.small";
    public const string BloodLifeCore = "core.ascendancy.blood.life.core";
    public const string BloodTwinSmall = "core.ascendancy.blood.twin.small";
    public const string BloodTwinCore = "core.ascendancy.blood.twin.core";
    public const string BloodRuptureSmall = "core.ascendancy.blood.rupture.small";
    public const string BloodRuptureCore = "core.ascendancy.blood.rupture.core";
    public const string BloodRageSmall = "core.ascendancy.blood.rage.small";
    public const string BloodRageCore = "core.ascendancy.blood.rage.core";
    public const string BloodLowLifeSmall = "core.ascendancy.blood.low_life.small";
    public const string BloodLowLifeCore = "core.ascendancy.blood.low_life.core";
    public const string BloodTideSmall = "core.ascendancy.blood.tide.small";
    public const string BloodTideCore = "core.ascendancy.blood.tide.core";

    public const string BastionAttackBlockSmall = "core.ascendancy.bastion.attack_block.small";
    public const string BastionAttackBlockCore = "core.ascendancy.bastion.attack_block.core";
    public const string BastionSpellBlockSmall = "core.ascendancy.bastion.spell_block.small";
    public const string BastionSpellBlockCore = "core.ascendancy.bastion.spell_block.core";
    public const string BastionArmorSmall = "core.ascendancy.bastion.armor.small";
    public const string BastionArmorCore = "core.ascendancy.bastion.armor.core";
    public const string BastionCounterSmall = "core.ascendancy.bastion.counter.small";
    public const string BastionCounterCore = "core.ascendancy.bastion.counter.core";
    public const string BastionGuardSmall = "core.ascendancy.bastion.guard.small";
    public const string BastionGuardCore = "core.ascendancy.bastion.guard.core";
    public const string BastionLayersSmall = "core.ascendancy.bastion.layers.small";
    public const string BastionLayersCore = "core.ascendancy.bastion.layers.core";

    public const string BreakerTwoHandSmall = "core.ascendancy.breaker.two_hand.small";
    public const string BreakerTwoHandCore = "core.ascendancy.breaker.two_hand.core";
    public const string BreakerAftershockSmall = "core.ascendancy.breaker.aftershock.small";
    public const string BreakerAftershockCore = "core.ascendancy.breaker.aftershock.core";
    public const string BreakerWarCrySmall = "core.ascendancy.breaker.warcry.small";
    public const string BreakerWarCryCore = "core.ascendancy.breaker.warcry.core";
    public const string BreakerArmorBreakSmall = "core.ascendancy.breaker.armor_break.small";
    public const string BreakerArmorBreakCore = "core.ascendancy.breaker.armor_break.core";
    public const string BreakerStunSmall = "core.ascendancy.breaker.stun.small";
    public const string BreakerStunCore = "core.ascendancy.breaker.stun.core";
    public const string BreakerMarchSmall = "core.ascendancy.breaker.march.small";
    public const string BreakerMarchCore = "core.ascendancy.breaker.march.core";
}

public static class AscendancyCatalog
{
    private static readonly IReadOnlyDictionary<string, AscendancyNode> NodeMap = AscendancyDefinitions.Nodes
        .ToDictionary(node => node.StableId, StringComparer.Ordinal);

    public static IReadOnlyCollection<AscendancyNode> Nodes => NodeMap.Values.ToArray();
    public static bool IsImplemented(Ascendancy ascendancy) => ascendancy != Ascendancy.None;
    public static IReadOnlyList<AscendancyNode> For(Ascendancy ascendancy) => NodeMap.Values
        .Where(node => node.Ascendancy == ascendancy).OrderBy(node => node.Direction).ThenBy(node => node.Kind).ToArray();
    public static AscendancyNode Get(string id) => NodeMap.TryGetValue(id, out AscendancyNode? node)
        ? node : throw new KeyNotFoundException($"Unknown Ascendancies ascendancy node: {id}");

    public static string DisplayName(Ascendancy ascendancy) => AscendancyDefinitions.All.FirstOrDefault(path => path.Ascendancy == ascendancy)?.DisplayName ?? "尚未升华";
}

public static class WarriorAscendancyRules
{
    public static int AttackManaCost(int baseCost, SkillTag tags) =>
        tags.HasFlag(SkillTag.Attack) && !tags.HasFlag(SkillTag.Spell)
            ? Math.Max(1, checked((baseCost * 8 + 9) / 10))
            : baseCost;

    public static Skills.ResolvedSkill ApplySkillCost(Skills.ResolvedSkill skill, SkillTag tags,
        int maximumLife, CombatProfile profile)
    {
        Skills.ResolvedSkill result;
        if (!profile.Has(WarriorNodeIds.BloodLifeCore) || !tags.HasFlag(SkillTag.Attack) || tags.HasFlag(SkillTag.Spell))
            result = profile.Has(WarriorNodeIds.BloodLifeSmall) && skill.LifeCost > 0
                ? skill with { LifeCost = Math.Max(1, skill.LifeCost * 9 / 10) }
                : skill;
        else
        {
            int originalMana = skill.ManaCost;
            int cost = Math.Max(Math.Max(1, maximumLife * 200 / 10_000), originalMana * 2);
            if (profile.Has(WarriorNodeIds.BloodLifeSmall)) cost = Math.Max(1, cost * 9 / 10);
            result = skill with { ManaCost = 0, LifeCost = Math.Max(skill.LifeCost, cost) };
        }
        return Characters.ClassAscendancyRules.ApplyResolvedSkill(result, tags, profile);
    }

    public static SkillUseProfile ApplyHeavyStrikeCost(SkillUseProfile skill, int maximumLife,
        CombatProfile profile)
    {
        if (!profile.Has(WarriorNodeIds.BloodLifeCore)) return profile.Has(WarriorNodeIds.BloodLifeSmall) && skill.LifeCost > 0
            ? skill with { LifeCost = Math.Max(1, skill.LifeCost * 9 / 10) }
            : skill;
        int cost = Math.Max(Math.Max(1, maximumLife * 200 / 10_000), skill.ManaCost * 2);
        if (profile.Has(WarriorNodeIds.BloodLifeSmall)) cost = Math.Max(1, cost * 9 / 10);
        return skill with { ManaCost = 0, LifeCost = Math.Max(skill.LifeCost, cost) };
    }

    public static CharacterSheet ApplySheet(CharacterSheet sheet, CombatProfile profile, int shieldArmor = 0)
    {
        int life = 0;
        CharacterAttributes attributes = sheet.Attributes;
        if (profile.Has(WarriorNodeIds.BloodLifeSmall)) life += 1_000;
        if (profile.Has(WarriorNodeIds.BastionArmorSmall))
            attributes = attributes with
            {
                Physique = checked((attributes.Physique + 120) * 10_800 / 10_000),
            };
        int flatArmor = profile.Has(WarriorNodeIds.BastionArmorCore)
            ? checked(attributes.Physique / 100 * 300)
            : 0;
        if (profile.Has(WarriorNodeIds.BastionAttackBlockSmall))
            flatArmor = checked(flatArmor + Math.Max(0, shieldArmor) * 2_500 / 10_000);
        if (profile.Has(WarriorNodeIds.BastionGuardCore)) life += 2_000;
        return sheet with
        {
            Attributes = attributes,
            Equipment = sheet.Equipment with { Armor = checked(sheet.Equipment.Armor + flatArmor) },
            IncreasedMaximumLifeBasisPoints = checked(sheet.IncreasedMaximumLifeBasisPoints + life),
        };
    }

    public static int IncreasedAttackDamageBasisPoints(CombatProfile profile, int finalPhysique) =>
        profile.Has(WarriorNodeIds.BastionArmorCore) ? checked(Math.Max(0, finalPhysique) / 100 * 8_000) : 0;

    public static int AttackBlockChanceBasisPoints(int baseChance, CombatProfile profile, bool hasShield)
    {
        if (!hasShield) return baseChance;
        int result = baseChance;
        if (profile.Has(WarriorNodeIds.BastionAttackBlockSmall)) result = checked(result + 800);
        if (profile.Has(WarriorNodeIds.BastionAttackBlockCore)) result = checked(result + 1_200);
        return result;
    }

    public static int AttackBlockMaximumBasisPoints(int baseMaximum, CombatProfile profile, bool hasShield) =>
        hasShield && profile.Has(WarriorNodeIds.BastionAttackBlockCore)
            ? Math.Min(CombatRules.AbsoluteBlockMaximum, checked(baseMaximum + 500))
            : baseMaximum;

    public static int SpellBlockChanceBasisPoints(int baseChance, int finalAttackBlockChance,
        CombatProfile profile, bool hasShield, int minimumInheritanceBasisPoints = 0)
    {
        int result = baseChance;
        if (hasShield && profile.Has(WarriorNodeIds.BastionSpellBlockSmall)) result = checked(result + 800);
        int inheritance = Math.Max(minimumInheritanceBasisPoints, profile.Has(WarriorNodeIds.BastionSpellBlockCore) ? 6000 : 0);
        result = checked(result + (int)((long)finalAttackBlockChance * inheritance / 10000));
        return result;
    }
}

/// <summary>Per-node combat state. It is intentionally transient and never saved mid encounter.</summary>
public sealed class CombatRuntime(CombatProfile profile)
{
    public CombatProfile Profile { get; } = profile;
    public int BloodRage { get; private set; }
    public int BastionLayers { get; private set; }
    public int RevengeLayers { get; private set; }
    public int MarchDistanceRaw { get; private set; }
    public bool MarchReady { get; private set; }
    public int ExertedAttacks { get; private set; }
    public int LastMovementResetTick { get; private set; } = int.MinValue;
    public int RecoveryProtectionUntilTick { get; private set; }
    public int ArmorWindowUntilTick { get; private set; }
    public int LastLifePaymentTick { get; private set; } = int.MinValue;

    public bool Has(string id) => Profile.Has(id);

    public void PaidLife(int tick)
    {
        if (!Has(WarriorNodeIds.BloodRageCore) || tick == LastLifePaymentTick) return;
        LastLifePaymentTick = tick;
        BloodRage = Math.Min(20, BloodRage + 2);
    }
    public void AppliedBleed() { if (Has(WarriorNodeIds.BloodRageCore)) BloodRage = Math.Min(20, BloodRage + 1); }
    public void KilledBleedingEnemy() { if (Has(WarriorNodeIds.BloodRageCore)) BloodRage = Math.Min(20, BloodRage + 3); }
    public void AdvanceSecond() { if (Has(WarriorNodeIds.BloodRageCore)) BloodRage = Math.Max(0, BloodRage - 1); }

    public void Moved(int distanceRaw)
    {
        if (!Has(WarriorNodeIds.BreakerMarchCore) || MarchReady) return;
        MarchDistanceRaw = checked(MarchDistanceRaw + Math.Max(0, distanceRaw));
        if (MarchDistanceRaw < 6_000) return;
        MarchDistanceRaw -= 6_000;
        MarchReady = true;
    }

    public int ConsumeAttackMultiplier(SkillTag tags, bool lowLife, bool twoHanded, EnemyState enemy)
    {
        int result = 10_000;
        bool attack = tags.HasFlag(SkillTag.Attack) && !tags.HasFlag(SkillTag.Spell);
        if (attack && Has(WarriorNodeIds.BloodLifeCore)) result = Multiply(result, 15_000);
        if (attack && lowLife && Has(WarriorNodeIds.BloodLowLifeCore)) result = Multiply(result, 16_000);
        if (attack && twoHanded && Has(WarriorNodeIds.BreakerTwoHandCore)) result = Multiply(result, 16_000);
        else if (attack && twoHanded && Has(WarriorNodeIds.BreakerTwoHandSmall)) result = Multiply(result, 11_500);
        if (attack && tags.HasFlag(SkillTag.Slam) && Has(WarriorNodeIds.BreakerAftershockSmall)) result = Multiply(result, 11_500);
        if (attack && BloodRage > 0 && Has(WarriorNodeIds.BloodRageCore)) result = Multiply(result, 10_000 + BloodRage * 200);
        if (attack && enemy.ArmorBreakStacks >= 8 && Has(WarriorNodeIds.BreakerArmorBreakCore)) result = Multiply(result, 15_000);
        if (attack && enemy.Stunned && Has(WarriorNodeIds.BreakerStunCore)) result = Multiply(result, 20_000);
        if (tags.HasFlag(SkillTag.Slam) && MarchReady)
        {
            result = Multiply(result, 16_000);
            MarchReady = false;
        }
        if (attack && tags.HasFlag(SkillTag.Melee) && ExertedAttacks > 0)
        {
            result = Multiply(result, 15_000);
            ExertedAttacks--;
        }
        return result;
    }

    public void WarCry() { if (Has(WarriorNodeIds.BreakerWarCryCore)) ExertedAttacks = 4; }

    public int OnAttackBlock()
    {
        int multiplier = Has(WarriorNodeIds.BastionCounterCore) && RevengeLayers >= 3 ? 28_000 : 10_000;
        if (Has(WarriorNodeIds.BastionLayersCore) && BastionLayers >= 5)
            multiplier = Multiply(multiplier, 15_000);
        RevengeLayers = Math.Min(3, RevengeLayers + 1);
        if (BastionLayers > 0) BastionLayers = 0;
        return multiplier;
    }

    public void OnUnblockedAttack(int tick = 0)
    {
        if (Has(WarriorNodeIds.BastionLayersSmall)) ArmorWindowUntilTick = tick + 40;
        if (Has(WarriorNodeIds.BastionLayersCore)) BastionLayers = Math.Min(5, BastionLayers + 1);
    }

    public int IncomingHitMultiplier(bool spell, bool blocked, int tick)
    {
        if (blocked && spell && Has(WarriorNodeIds.BastionSpellBlockCore)) return 0;
        int result = 10_000;
        if (!blocked && !spell && Has(WarriorNodeIds.BastionAttackBlockCore)) result = Multiply(result, 8_000);
        if (!blocked && spell && Has(WarriorNodeIds.BastionSpellBlockCore)) result = Multiply(result, 7_500);
        if (Has(WarriorNodeIds.BastionLayersCore)) result = Multiply(result, 10_000 - BastionLayers * 500);
        if (tick < RecoveryProtectionUntilTick) result = Multiply(result, 8_000);
        return result;
    }

    public int PassiveRecoveryBasisPoints => Has(WarriorNodeIds.BastionLayersCore) && BastionLayers >= 5 ? 400 :
        Has(WarriorNodeIds.BastionLayersCore) && BastionLayers >= 3 ? 200 : 0;
    public int ArmorMultiplier(int tick) => tick < ArmorWindowUntilTick ? 12_500 : 10_000;
    public void TriggerRecoveryProtection(int tick) { if (Has(WarriorNodeIds.BloodTideCore)) RecoveryProtectionUntilTick = tick + 40; }
    public int ArmorBreakMaximum => Has(WarriorNodeIds.BreakerArmorBreakCore) ? 8 : 5;
    public int ArmorBreakPerStackBasisPoints => Has(WarriorNodeIds.BreakerArmorBreakCore) ? 1_200 : 800;
    public int AdditionalBleedChance => Has(WarriorNodeIds.BloodTwinSmall) ? 2_500 : 0;
    public int BleedDamageMultiplier => Has(WarriorNodeIds.BloodTwinSmall) ? 11_500 : 10_000;
    public int BleedPulseCount => Has(WarriorNodeIds.BloodRuptureSmall) ? 4 : 5;
    public bool TwoBleeds => Has(WarriorNodeIds.BloodTwinCore);
    public int AttackSpeedBasisPoints => (Has(WarriorNodeIds.BloodRageSmall) && BloodRage > 0 ? 800 : 0) +
        (Has(WarriorNodeIds.BloodRageCore) ? BloodRage * 50 : 0) +
        (Has(WarriorNodeIds.BreakerTwoHandSmall) ? 800 : 0) -
        (Has(WarriorNodeIds.BreakerTwoHandCore) ? 1_000 : 0);

    public bool TryResetMovementCooldownOnKill(int tick)
    {
        if (!Has(WarriorNodeIds.BreakerMarchCore) || tick - LastMovementResetTick < 40) return false;
        LastMovementResetTick = tick;
        return true;
    }

    private static int Multiply(int left, int right) => checked((int)((long)left * right / 10_000));
}

public readonly record struct EnemyState(int ArmorBreakStacks, bool Stunned);

public sealed record BenchmarkBuild(
    string StableId,
    string DisplayName,
    Ascendancy Ascendancy,
    bool EndgameGear,
    IReadOnlyList<string> Nodes,
    IReadOnlyList<string> Skills,
    string Purpose);

public static class WarriorBenchmarkBuilds
{
    public static IReadOnlyList<BenchmarkBuild> All { get; } =
    [
        Build("blood.entry", "血战士·开荒流血", Ascendancy.BloodFighter, false,
            [WarriorNodeIds.BloodLifeSmall, WarriorNodeIds.BloodLifeCore, WarriorNodeIds.BloodTwinSmall, WarriorNodeIds.BloodTwinCore,
             WarriorNodeIds.BloodRuptureSmall, WarriorNodeIds.BloodRuptureCore, WarriorNodeIds.BloodTideSmall, WarriorNodeIds.BloodTideCore],
            [SkillIds.HeavyStrike, SkillIds.BloodTideSpin], "低装备门槛、流血清图与自回复"),
        Build("blood.endgame", "血战士·终局死战", Ascendancy.BloodFighter, true,
            [WarriorNodeIds.BloodLifeSmall, WarriorNodeIds.BloodLifeCore, WarriorNodeIds.BloodRageSmall, WarriorNodeIds.BloodRageCore,
             WarriorNodeIds.BloodLowLifeSmall, WarriorNodeIds.BloodLowLifeCore, WarriorNodeIds.BloodTideSmall, WarriorNodeIds.BloodTideCore],
            [SkillIds.HeavyStrike, SkillIds.BloodBurst], "低生命攻坚与恢复保护"),
        Build("bastion.entry", "铁壁卫·开荒盾列", Ascendancy.IronGuardian, false,
            [WarriorNodeIds.BastionArmorSmall, WarriorNodeIds.BastionArmorCore, WarriorNodeIds.BastionAttackBlockSmall, WarriorNodeIds.BastionAttackBlockCore,
             WarriorNodeIds.BastionCounterSmall, WarriorNodeIds.BastionCounterCore, WarriorNodeIds.BastionLayersSmall, WarriorNodeIds.BastionLayersCore],
            [SkillIds.HeavyStrike, SkillIds.VengefulCounter], "廉价盾牌、格挡与反击"),
        Build("bastion.endgame", "铁壁卫·终局守誓", Ascendancy.IronGuardian, true,
            [WarriorNodeIds.BastionAttackBlockSmall, WarriorNodeIds.BastionAttackBlockCore, WarriorNodeIds.BastionSpellBlockSmall, WarriorNodeIds.BastionSpellBlockCore,
             WarriorNodeIds.BastionGuardSmall, WarriorNodeIds.BastionGuardCore, WarriorNodeIds.BastionLayersSmall, WarriorNodeIds.BastionLayersCore],
            [SkillIds.VengefulCounter, SkillIds.IronOathBanner], "双格挡、护卫和天垒存活"),
        Build("breaker.entry", "破军者·开荒重兵", Ascendancy.Warbreaker, false,
            [WarriorNodeIds.BreakerTwoHandSmall, WarriorNodeIds.BreakerTwoHandCore, WarriorNodeIds.BreakerAftershockSmall, WarriorNodeIds.BreakerAftershockCore,
             WarriorNodeIds.BreakerWarCrySmall, WarriorNodeIds.BreakerWarCryCore, WarriorNodeIds.BreakerArmorBreakSmall, WarriorNodeIds.BreakerArmorBreakCore],
            [SkillIds.EarthCleave, SkillIds.WarCry], "双手猛击、余震与破甲"),
        Build("breaker.endgame", "破军者·终局山崩", Ascendancy.Warbreaker, true,
            [WarriorNodeIds.BreakerAftershockSmall, WarriorNodeIds.BreakerAftershockCore, WarriorNodeIds.BreakerMarchSmall, WarriorNodeIds.BreakerMarchCore,
             WarriorNodeIds.BreakerStunSmall, WarriorNodeIds.BreakerStunCore, WarriorNodeIds.BreakerArmorBreakSmall, WarriorNodeIds.BreakerArmorBreakCore],
            [SkillIds.SeismicCharge, SkillIds.EarthCleave], "移动蓄势、完整眩晕与高层破甲"),
    ];

    private static BenchmarkBuild Build(string id, string name, Ascendancy path, bool endgame,
        IReadOnlyList<string> nodes, IReadOnlyList<string> skills, string purpose) =>
        new($"core.benchmark.{id}", name, path, endgame, nodes, skills, purpose);
}
