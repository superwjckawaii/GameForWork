using System.Text;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P1.Progression;
using Godot;

namespace GameForWork.GodotClient;

internal static class P1UiText
{
    public static string ItemTooltip(ItemInstance item)
    {
        var text = new StringBuilder();
        text.AppendLine($"{RarityName(item.Rarity)} · {item.Base.DisplayName}");
        text.AppendLine($"物品等级 {item.ItemLevel} · {CategoryName(item.Base.Category)}");
        if (item.Base.Category == ItemCategory.TwoHandWeapon)
        {
            text.AppendLine($"物理伤害 {item.Base.MinimumPhysicalDamage}–{item.Base.MaximumPhysicalDamage}");
            text.AppendLine($"攻击频率 {item.Base.AttacksPerSecondMilli / 1000.0:0.00}/秒 · 暴击 {item.Base.CriticalChanceBasisPoints / 100.0:0.0}%");
        }

        if (item.Base.Armor + item.Base.Evasion + item.Base.Shield > 0)
        {
            text.AppendLine($"护甲 {item.Base.Armor} · 闪避 {item.Base.Evasion} · 护盾 {item.Base.Shield}");
        }

        if (item.Base.CoreSkillCapacity + item.Base.SupportLinkCapacity > 0)
        {
            text.AppendLine($"核心槽 {item.Base.CoreSkillCapacity} · 连接容量 {item.Base.SupportLinkCapacity + item.ExtraSupportLinkCapacity}");
        }

        if (item.Base.ImplicitModifier != ItemModifierKind.None)
        {
            text.AppendLine($"固有：{Modifier(item.Base.ImplicitModifier, item.ImplicitValue)}");
        }

        foreach (AffixRoll affix in item.Affixes)
        {
            string crafted = affix.Crafted ? "（工匠）" : string.Empty;
            text.AppendLine($"{PositionName(affix.Definition.Position)} T{affix.Definition.Tier} {affix.Definition.DisplayName}{crafted}：" +
                Modifier(affix.Definition.ModifierKind, affix.Value));
        }

        if (item.LegendaryRule is not null)
        {
            text.AppendLine("传奇规则：重击总攻击速度降低 30%");
            text.AppendLine("重击在目标身后产生一次 70% 伤害的余震");
        }

        text.Append(item.IsIdentified ? "已鉴定" : "未鉴定");
        if (item.IsLocked)
        {
            text.Append(" · 🔒 已锁定");
        }

        if (item.IsCraftingBase)
        {
            text.Append(" · ◆ 制作底材");
        }
        return text.ToString();
    }

    public static string PassiveTooltip(
        PassiveNodeDefinition node,
        bool allocated,
        bool available)
    {
        string state = allocated ? "已分配" : available ? "可分配" : "尚未连通或点数不足";
        string prerequisite = node.PrerequisiteId is null
            ? "职业起点可直接连接"
            : $"前置：{P1PassiveTree.Get(node.PrerequisiteId).DisplayName}";
        return $"{node.DisplayName} · {NodeKind(node.Kind)}\n{BranchName(node.Branch)} · {state}\n{prerequisite}\n" +
            string.Join('\n', node.Effects.Select(PassiveEffect));
    }

    public static string PassiveEffect(PassiveEffect effect) => effect.Kind switch
    {
        PassiveEffectKind.FasterBleeding => "流血造成伤害的速度加快",
        PassiveEffectKind.DeepWound => "深创：改变流血结算规则",
        PassiveEffectKind.Tenacious => "顽强：低生命时强化生存",
        PassiveEffectKind.Echo => "余音：战吼效果产生回响",
        PassiveEffectKind.ChargedHeavyStrike => "蓄势重击：改变重击行为",
        PassiveEffectKind.HeavyWeaponMastery => "震岳专精：每第五次近战攻击产生伤害回响",
        PassiveEffectKind.BleedMastery => "孤创专精：强化单一深创的 Boss 压制能力",
        PassiveEffectKind.DefenseMastery => "钢躯专精：护甲同时强化低生命防线",
        PassiveEffectKind.WarCryMastery => "震令专精：战吼更快覆盖战场",
        PassiveEffectKind.FlatAccuracy => $"命中值 +{effect.Value}",
        PassiveEffectKind.FlatMaximumLife => $"最大生命 +{effect.Value}",
        PassiveEffectKind.FlatMaximumMana => $"最大法力 +{effect.Value}",
        _ => $"{PassiveEffectName(effect.Kind)} +{effect.Value / 100.0:0.#}%",
    };

    public static Color RarityColor(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Basic => new Color("d7d2c8"),
        ItemRarity.Magic => new Color("6ca7e8"),
        ItemRarity.Rare => new Color("e7cb58"),
        ItemRarity.Legendary => new Color("d98a47"),
        _ => Colors.White,
    };

    public static string ItemGlyph(ItemCategory category) => category switch
    {
        ItemCategory.TwoHandWeapon => "刃",
        ItemCategory.BodyArmor => "甲",
        ItemCategory.Helmet => "盔",
        ItemCategory.Gloves => "手",
        ItemCategory.Boots => "靴",
        ItemCategory.Belt => "带",
        ItemCategory.Amulet => "符",
        ItemCategory.Ring => "环",
        ItemCategory.LifeFlask => "药",
        _ => "物",
    };

    private static string Modifier(ItemModifierKind kind, int value)
    {
        string name = ModifierName(kind);
        return kind.ToString().Contains("BasisPoints", StringComparison.Ordinal)
            ? $"{name} +{value / 100.0:0.#}%"
            : $"{name} +{value}";
    }

    private static string ModifierName(ItemModifierKind kind) => kind switch
    {
        ItemModifierKind.AddedPhysicalDamage => "附加物理伤害",
        ItemModifierKind.IncreasedPhysicalDamageBasisPoints => "物理伤害增加",
        ItemModifierKind.FlatAccuracy => "命中值",
        ItemModifierKind.IncreasedAttackSpeedBasisPoints => "攻击速度增加",
        ItemModifierKind.IncreasedCriticalChanceBasisPoints => "暴击率增加",
        ItemModifierKind.IncreasedBleedChanceBasisPoints => "流血概率增加",
        ItemModifierKind.Physique => "体魄",
        ItemModifierKind.Spirit => "精神",
        ItemModifierKind.FlatMaximumLife => "最大生命",
        ItemModifierKind.FlatMaximumMana => "最大法力",
        ItemModifierKind.IncreasedArmorBasisPoints => "护甲增加",
        ItemModifierKind.IncreasedEvasionBasisPoints => "闪避增加",
        ItemModifierKind.IncreasedShieldBasisPoints => "护盾增加",
        ItemModifierKind.IncreasedLifeFlaskEffectBasisPoints => "生命药剂效果增加",
        ItemModifierKind.ExtraSupportLinkCapacity => "额外连接容量",
        ItemModifierKind.IncreasedManaRegenerationBasisPoints => "法力恢复增加",
        _ => kind.ToString(),
    };

    private static string PassiveEffectName(PassiveEffectKind kind) => kind switch
    {
        PassiveEffectKind.IncreasedAttackDamageBasisPoints => "攻击伤害增加",
        PassiveEffectKind.IncreasedAttackSpeedBasisPoints => "攻击速度增加",
        PassiveEffectKind.IncreasedTwoHandDamageBasisPoints => "双手武器伤害增加",
        PassiveEffectKind.IncreasedBleedDamageBasisPoints => "流血伤害增加",
        PassiveEffectKind.IncreasedBleedChanceBasisPoints => "流血概率增加",
        PassiveEffectKind.IncreasedBleedDurationBasisPoints => "流血持续时间增加",
        PassiveEffectKind.IncreasedPhysicalDamageOverTimeBasisPoints => "物理持续伤害增加",
        PassiveEffectKind.IncreasedMaximumLifeBasisPoints => "最大生命增加",
        PassiveEffectKind.IncreasedArmorBasisPoints => "护甲增加",
        PassiveEffectKind.IncreasedLifeFlaskEffectBasisPoints => "生命药剂效果增加",
        PassiveEffectKind.IncreasedWarCryCooldownRecoveryBasisPoints => "战吼冷却恢复增加",
        PassiveEffectKind.IncreasedManaRegenerationBasisPoints => "法力恢复增加",
        PassiveEffectKind.IncreasedWarCryRangeBasisPoints => "战吼范围增加",
        PassiveEffectKind.IncreasedMovementSpeedBasisPoints => "移动速度增加",
        _ => kind.ToString(),
    };

    private static string RarityName(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Basic => "基础",
        ItemRarity.Magic => "魔法",
        ItemRarity.Rare => "稀有",
        ItemRarity.Legendary => "传奇",
        _ => rarity.ToString(),
    };

    private static string CategoryName(ItemCategory category) => category switch
    {
        ItemCategory.TwoHandWeapon => "双手武器",
        ItemCategory.BodyArmor => "胸甲",
        ItemCategory.Helmet => "头盔",
        ItemCategory.Gloves => "手套",
        ItemCategory.Boots => "鞋子",
        ItemCategory.Belt => "腰带",
        ItemCategory.Amulet => "项链",
        ItemCategory.Ring => "戒指",
        ItemCategory.LifeFlask => "生命药剂",
        _ => category.ToString(),
    };

    private static string PositionName(AffixPosition position) => position == AffixPosition.Prefix ? "前缀" : "后缀";
    private static string NodeKind(PassiveNodeKind kind) => kind switch
    {
        PassiveNodeKind.Small => "小型天赋",
        PassiveNodeKind.Notable => "显著天赋",
        PassiveNodeKind.Mastery => "集群专精",
        PassiveNodeKind.Rule => "规则天赋",
        _ => kind.ToString(),
    };

    private static string BranchName(PassiveBranch branch) => branch switch
    {
        PassiveBranch.HeavyWeapon => "重兵分支",
        PassiveBranch.Bleed => "流血分支",
        PassiveBranch.Defense => "防御分支",
        PassiveBranch.WarCry => "战吼分支",
        _ => branch.ToString(),
    };
}
