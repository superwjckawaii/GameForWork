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

public static class WarriorAscendancyCatalog
{
    private static readonly IReadOnlyDictionary<string, AscendancyNode> NodeMap = AscendancyDefinitions.Apply(Build()
        .Concat(ClassAscendancyCatalog.Nodes).ToArray())
        .ToDictionary(node => node.StableId, StringComparer.Ordinal);

    public static IReadOnlyCollection<AscendancyNode> Nodes => NodeMap.Values.ToArray();
    public static bool IsImplemented(Ascendancy ascendancy) => ascendancy != Ascendancy.None;
    public static IReadOnlyList<AscendancyNode> For(Ascendancy ascendancy) => NodeMap.Values
        .Where(node => node.Ascendancy == ascendancy).OrderBy(node => node.Direction).ThenBy(node => node.Kind).ToArray();
    public static AscendancyNode Get(string id) => NodeMap.TryGetValue(id, out AscendancyNode? node)
        ? node : throw new KeyNotFoundException($"Unknown Ascendancies ascendancy node: {id}");

    public static string DisplayName(Ascendancy ascendancy) => ascendancy switch
    {
        Ascendancy.BloodFighter => "血战士",
        Ascendancy.IronGuardian => "铁壁卫",
        Ascendancy.Warbreaker => "破军者",
        Ascendancy.Marksman => "神射手",
        Ascendancy.Shadowblade => "影刃客",
        Ascendancy.Venomist => "毒术师",
        Ascendancy.SoulShepherd => "牧魂师",
        Ascendancy.SpiritCantor => "颂灵师",
        Ascendancy.Hexbinder => "咒契师",
        Ascendancy.Elementalist => "元素使",
        Ascendancy.VoidScholar => "虚空学者",
        Ascendancy.AegisMage => "秘盾师",
        Ascendancy.MartialMonk => "行武僧",
        Ascendancy.BeastKeeper => "灵兽使",
        Ascendancy.PhantomMaster => "幻身宗师",
        Ascendancy.Runecarver => "刻印师",
        Ascendancy.Spellarmor => "魔铠师",
        Ascendancy.IdolForger => "铸像师",
        _ => "尚未升华",
    };

    private static IReadOnlyList<AscendancyNode> Build()
    {
        var result = new List<AscendancyNode>(36);
        AddBranch(result, Ascendancy.BloodFighter, 0, WarriorNodeIds.BloodLifeSmall, "血肉薪火",
            "最大生命提高5%；技能生命消耗降低10%", WarriorNodeIds.BloodLifeCore, "血铸契约",
            "攻击技能改用生命；生命消耗至少为最大生命2%，支付生命的攻击命中与流血造成50%更多伤害");
        AddBranch(result, Ascendancy.BloodFighter, 1, WarriorNodeIds.BloodTwinSmall, "深红刀口",
            "流血概率提高25%；流血伤害提高15%", WarriorNodeIds.BloodTwinCore, "双痕法则",
            "最强两条流血同时生效，每条造成原伤害80%");
        AddBranch(result, Ascendancy.BloodFighter, 2, WarriorNodeIds.BloodRuptureSmall, "急血奔流",
            "流血造成伤害速度提高15%，持续时间缩短10%", WarriorNodeIds.BloodRuptureCore, "裂创连击",
            "命中流血敌人施加裂创，最多3层；每层使流血造成伤害速度提高15%");
        AddBranch(result, Ascendancy.BloodFighter, 3, WarriorNodeIds.BloodRageSmall, "沸血战意",
            "最近3秒支付过生命时攻击速度提高8%", WarriorNodeIds.BloodRageCore, "血怒不息",
            "支付生命、施加流血和击杀流血敌人积累血怒；满层提供40%更多物理与流血伤害和10%攻击速度");
        AddBranch(result, Ascendancy.BloodFighter, 4, WarriorNodeIds.BloodLowLifeSmall, "濒战本能",
            "低生命时生命偷取和药剂生命恢复提高25%，攻击速度提高8%", WarriorNodeIds.BloodLowLifeCore, "死战不退",
            "低生命时物理命中与流血造成60%更多伤害，受到击中伤害降低25%，免疫眩晕");
        AddBranch(result, Ascendancy.BloodFighter, 5, WarriorNodeIds.BloodTideSmall, "饮血收割",
            "物理攻击伤害的1%转化为生命偷取", WarriorNodeIds.BloodTideCore, "赤潮归身",
            "流血击杀传播120%最强剩余流血并回复4%最大生命；持续伤害稀有怪或Boss时每秒回复4%最大生命；触发恢复后2秒内受到击中伤害降低20%");

        AddBranch(result, Ascendancy.IronGuardian, 0, WarriorNodeIds.BastionArmorSmall, "层叠甲幕",
            "护甲提高25%；最大生命提高5%", WarriorNodeIds.BastionArmorCore, "百炼甲幕",
            "30%护甲参与元素击中减伤；元素击中伤害降低20%");
        AddBranch(result, Ascendancy.IronGuardian, 1, WarriorNodeIds.BastionAttackBlockSmall, "盾列操练",
            "装备盾牌时攻击格挡率额外提高8%；盾牌护甲提高25%", WarriorNodeIds.BastionAttackBlockCore, "绝对盾面",
            "攻击格挡率额外提高12%、上限提高至80%；未格挡攻击击中伤害降低20%");
        AddBranch(result, Ascendancy.IronGuardian, 2, WarriorNodeIds.BastionCounterSmall, "回震刃缘",
            "反击伤害提高30%、冷却恢复提高20%、范围提高15%", WarriorNodeIds.BastionCounterCore, "复仇壁垒",
            "格挡积累复仇；满3层反击造成180%更多伤害并回复6%最大生命");
        AddBranch(result, Ascendancy.IronGuardian, 3, WarriorNodeIds.BastionLayersSmall, "受击成垒",
            "受到未格挡攻击后，2秒内护甲提高20%", WarriorNodeIds.BastionLayersCore, "不破阵地",
            "未格挡攻击积累壁垒；满层降低25%击中伤害、反击造成50%更多伤害并每秒回复4%最大生命；格挡攻击后清空");
        AddBranch(result, Ascendancy.IronGuardian, 4, WarriorNodeIds.BastionGuardSmall, "守护轮转",
            "护卫冷却恢复提高25%、持续时间提高20%", WarriorNodeIds.BastionGuardCore, "守誓疆域",
            "最大生命提高20%；战旗保留降低50%、效果提高30%；护卫期间额外降低25%击中伤害并每秒回复5%最大生命");
        AddBranch(result, Ascendancy.IronGuardian, 5, WarriorNodeIds.BastionSpellBlockSmall, "咒击偏转",
            "装备盾牌时获得8%法术格挡率", WarriorNodeIds.BastionSpellBlockCore, "镜铁守誓",
            "获得攻击格挡率60%的额外法术格挡；法术格挡降低70%伤害，未格挡法术击中伤害降低25%");

        AddBranch(result, Ascendancy.Warbreaker, 0, WarriorNodeIds.BreakerTwoHandSmall, "巨兵驾驭",
            "双手武器伤害提高15%；双手攻击速度提高8%", WarriorNodeIds.BreakerTwoHandCore, "重兵裁决",
            "双手攻击击中造成60%更多伤害，但攻击速度降低10%");
        AddBranch(result, Ascendancy.Warbreaker, 1, WarriorNodeIds.BreakerAftershockSmall, "震域扩张",
            "猛击范围提高20%；猛击伤害提高15%", WarriorNodeIds.BreakerAftershockCore, "震岳余势",
            "猛击命中0.5秒后产生造成原始击中实际伤害100%的余震");
        AddBranch(result, Ascendancy.Warbreaker, 2, WarriorNodeIds.BreakerMarchSmall, "踏阵而行",
            "移动技能冷却恢复提高25%；移动技能和猛击范围提高15%", WarriorNodeIds.BreakerMarchCore, "裂阵行军",
            "累计实际移动6米获得行军势；下一次猛击范围提高50%、造成60%更多伤害，击杀可重置移动技能冷却");
        AddBranch(result, Ascendancy.Warbreaker, 3, WarriorNodeIds.BreakerStunSmall, "撼魂重势",
            "眩晕积累提高40%；眩晕持续时间提高25%", WarriorNodeIds.BreakerStunCore, "山崩之王",
            "稀有怪和Boss可完整眩晕；眩晕期间承受100%更多伤害，触发时产生150%击中伤害的震波");
        AddBranch(result, Ascendancy.Warbreaker, 4, WarriorNodeIds.BreakerArmorBreakSmall, "碎甲专断",
            "物理攻击每次技能使用有25%概率额外施加1层破甲", WarriorNodeIds.BreakerArmorBreakCore, "碎城铁律",
            "破甲上限提高至8层、每层降低12%护甲；满层额外承受50%更多物理伤害");
        AddBranch(result, Ascendancy.Warbreaker, 5, WarriorNodeIds.BreakerWarCrySmall, "号令回响",
            "战吼冷却恢复提高30%、持续时间提高25%", WarriorNodeIds.BreakerWarCryCore, "号令无尽",
            "战吼不占用攻击动作时间，并使接下来4次近战攻击造成50%更多伤害");
        return result;
    }

    private static void AddBranch(List<AscendancyNode> nodes, Ascendancy ascendancy, int direction,
        string smallId, string smallName, string smallEffect, string coreId, string coreName, string coreEffect)
    {
        (int smallX, int smallY, int coreX, int coreY) = direction switch
        {
            0 => (0, -92, 0, -190),
            1 => (80, -46, 165, -95),
            2 => (80, 46, 165, 95),
            3 => (0, 92, 0, 190),
            4 => (-80, 46, -165, 95),
            _ => (-80, -46, -165, -95),
        };
        nodes.Add(new(smallId, ascendancy, direction, NodeKind.Reinforcement, smallName, smallEffect,
            null, smallX, smallY));
        nodes.Add(new(coreId, ascendancy, direction, NodeKind.Core, coreName, coreEffect,
            smallId, coreX, coreY));
    }
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
        if (!hasShield) return 0;
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
        CombatProfile profile, bool hasShield)
    {
        if (!hasShield) return 0;
        int result = baseChance;
        if (profile.Has(WarriorNodeIds.BastionSpellBlockSmall)) result = checked(result + 800);
        if (profile.Has(WarriorNodeIds.BastionSpellBlockCore))
            result = checked(result + finalAttackBlockChance * 6 / 10);
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
