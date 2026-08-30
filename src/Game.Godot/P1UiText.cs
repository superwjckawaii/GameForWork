using System.Text;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P1.Progression;
using GameForWork.Core.P14;
using GameForWork.Core.P20;
using Godot;

namespace GameForWork.GodotClient;

internal static class P1UiText
{
    public static string ItemTooltip(ItemInstance item)
    {
        var text = new StringBuilder();
        text.AppendLine($"{RarityName(item.Rarity)}·{item.DisplayName}{(item.Quality > 0 ? $"+{item.Quality}" : string.Empty)}");
        text.AppendLine($"底材：{item.Base.DisplayName}");
        text.AppendLine($"物品等级 {item.ItemLevel} · {item.Base.Category}");
        text.AppendLine($"需求：等级 {item.Base.RequiredLevel} · 体魄 {item.Base.RequiredPhysique} · " +
            $"灵巧 {item.Base.RequiredDexterity} · 精神 {item.Base.RequiredSpirit} · 能量 {item.Base.RequiredEnergy}");
        if (item.Base.Category is ItemCategory.TwoHandWeapon or ItemCategory.OneHandWeapon)
        {
            int finalMinimum = QualityScale(item.Base.MinimumPhysicalDamage, item.Quality);
            int finalMaximum = QualityScale(item.Base.MaximumPhysicalDamage, item.Quality);
            text.AppendLine($"物理伤害 {finalMinimum}–{finalMaximum}");
            text.AppendLine($"攻击频率 {item.Base.AttacksPerSecondMilli / 1000.0:0.00}/秒 · 暴击 {item.Base.CriticalChanceBasisPoints / 100.0:0.0}%");
        }

        if (item.Base.Armor + item.Base.Evasion + item.Base.Shield > 0)
        {
            text.AppendLine($"护甲 {QualityScale(item.Base.Armor, item.Quality)} · " +
                $"闪避 {QualityScale(item.Base.Evasion, item.Quality)} · 护盾 {QualityScale(item.Base.Shield, item.Quality)}");
        }
        if (item.Base.BlockChanceBasisPoints > 0)
        {
            text.AppendLine($"基础格挡 {item.Base.BlockChanceBasisPoints / 100.0:0.#}%");
        }

        if (item.LinkedSocketCount > 0)
        {
            text.AppendLine($"连接孔组：{item.LinkedSocketCount}连");
        }

        if (item.Base.ImplicitModifier != ItemModifierKind.None)
        {
            string label = string.IsNullOrWhiteSpace(item.Base.ImplicitText)
                ? "底材固有"
                : item.Base.ImplicitText;
            text.AppendLine($"（基底词缀）{label}：{Modifier(item.Base.ImplicitModifier, item.EffectiveImplicitValue)}");
        }

        if (item.Enchantment is not null)
            text.AppendLine($"（附魔）{item.Enchantment.DisplayName}：{Modifier(item.Enchantment.ModifierKind, item.Enchantment.Value)}");

        if (item.LegendaryRule is not null)
        {
            P14UniqueDefinition? unique = P14UniqueItems.All.FirstOrDefault(definition =>
                definition.StableId == item.LegendaryRule.StableId);
            text.AppendLine($"（传奇效果）{unique?.RuleText ?? item.LegendaryRule.DisplayText}");
        }

        foreach (AffixRoll affix in item.Affixes.OrderBy(affix => affix.Definition.Position).ThenBy(affix => affix.Definition.Tier))
        {
            int tier = P1Affixes.TierFor(item.Base, affix.Definition);
            string markers = (affix.Crafted ? "（工匠）" : string.Empty) + (item.IsFractured(affix) ? "（破溃）" : string.Empty);
            string source = affix.Crafted ? "工匠" : affix.Definition.Source == "Natural" ? "自然" : affix.Definition.Source;
            text.AppendLine($"[TIER:{tier}]{PositionName(affix.Definition.Position)} T{tier} " +
                $"{affix.Definition.DisplayName}{markers}：{Modifier(affix.Definition.ModifierKind, affix.EffectiveValue)} " +
                $"[{affix.EffectiveMinimumValue}–{affix.EffectiveMaximumValue}] · {source}");
        }

        if (P1FlaskRules.KindForBase(item.Base.StableId) is { } flaskKind)
        {
            P14FlaskDefinition flask = P14Flasks.All.Single(definition => definition.Kind == flaskKind);
            text.AppendLine($"效果：{flask.EffectDescription}");
            text.AppendLine($"充能：最大{flask.MaximumCharges} · 每次使用消耗{flask.ChargesPerUse}");
            text.AppendLine("击杀充能：普通+1 · 魔法+2 · 稀有+4 · Boss+6");
            text.AppendLine($"自动使用：{flask.AutoCondition}");
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
        if (item.IsCorrupted)
        {
            text.Append($" · ☠ 已腐化（{item.CorruptionOutcome}）");
        }
        text.AppendLine();
        text.Append($"出售 {P20ItemValue.SalePrice(item)} 金币");
        return text.ToString();
    }

    public static string PassiveTooltip(
        PassiveNodeDefinition node,
        bool allocated,
        bool available)
    {
        string state = allocated ? "已分配" : available ? "可分配" : "尚未连通或点数不足";
        string prerequisite = node.Kind == PassiveNodeKind.Start
            ? "免费职业锚点，不消耗天赋点"
            : node.PrerequisiteId is null
            ? "职业起点可直接连接"
            : $"前置：{P1PassiveTree.Get(node.PrerequisiteId).DisplayName}";
        string special = string.IsNullOrWhiteSpace(node.SpecialRule) ? string.Empty : $"\n规则：{node.SpecialRule}";
        string mastery = node.Kind == PassiveNodeKind.Mastery
            ? "\n专精候选：\n" + string.Join('\n', P1PassiveTree.MasteryOptions(node).Select(PassiveEffect))
            : string.Empty;
        string jewel = node.Kind == PassiveNodeKind.JewelSocket ? "\n珠宝半径：150（按半径内已分配节点增幅）" : string.Empty;
        return $"{node.DisplayName} · {NodeKind(node.Kind)}\n{BranchName(node.Branch)} · {state}\n{prerequisite}\n" +
            string.Join('\n', node.Effects.Select(PassiveEffect)) + special + mastery + jewel;
    }

    public static string PassiveEffect(PassiveEffect effect) => effect.Kind switch
    {
        PassiveEffectKind.FasterBleeding => "流血造成伤害的速度加快",
        PassiveEffectKind.DeepWound => "深创：改变流血结算规则",
        PassiveEffectKind.Tenacious => "顽强：低生命时强化生存",
        PassiveEffectKind.Echo => "余音：战吼效果产生回响",
        PassiveEffectKind.ChargedHeavyStrike => "蓄势重击：改变重击行为",
        PassiveEffectKind.HeavyWeaponMastery => "震岳专精：显著强化双手武器伤害",
        PassiveEffectKind.BleedMastery => "孤创专精：显著强化流血持续伤害",
        PassiveEffectKind.DefenseMastery => "钢躯专精：同时强化最大生命与护甲",
        PassiveEffectKind.WarCryMastery => "震令专精：缩短战吼冷却并扩大范围",
        PassiveEffectKind.FlatAccuracy => $"命中值 +{effect.Value}",
        PassiveEffectKind.FlatMaximumLife => $"最大生命 +{effect.Value}",
        PassiveEffectKind.FlatMaximumMana => $"最大法力 +{effect.Value}",
        PassiveEffectKind.FlatPhysique => $"体魄 +{effect.Value}",
        PassiveEffectKind.FlatDexterity => $"灵巧 +{effect.Value}",
        PassiveEffectKind.FlatSpirit => $"精神 +{effect.Value}",
        PassiveEffectKind.FlatEnergy => $"能量 +{effect.Value}",
        PassiveEffectKind.FlatLifeRegeneration => $"每秒生命恢复 +{effect.Value}",
        PassiveEffectKind.LifeOnHit => $"击中回复生命 +{effect.Value}",
        PassiveEffectKind.ManaOnHit => $"击中回复法力 +{effect.Value}",
        PassiveEffectKind.RuleResoluteTechnique => "必中誓约：攻击必定命中，但无法暴击",
        PassiveEffectKind.RuleIronReflexes => "钢铁反射：闪避转化为护甲",
        PassiveEffectKind.RuleFlaskless => "无药之誓：不能使用药剂",
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
        ItemModifierKind.Dexterity => "灵巧",
        ItemModifierKind.Energy => "能量",
        ItemModifierKind.FireResistanceBasisPoints => "火焰抗性",
        ItemModifierKind.ColdResistanceBasisPoints => "冰霜抗性",
        ItemModifierKind.LightningResistanceBasisPoints => "闪电抗性",
        ItemModifierKind.VoidResistanceBasisPoints => "虚空抗性",
        ItemModifierKind.IncreasedMovementSpeedBasisPoints => "移动速度增加",
        ItemModifierKind.BlockChanceBasisPoints => "格挡概率",
        ItemModifierKind.SpellSuppressionBasisPoints => "法术压制概率",
        ItemModifierKind.FlatLifeRegeneration => "每秒生命恢复",
        _ => kind.ToString(),
    };

    private static int QualityScale(int value, int quality) => value * (100 + Math.Clamp(quality, 0, 20)) / 100;

    private static string PassiveEffectName(PassiveEffectKind kind) => kind switch
    {
        PassiveEffectKind.IncreasedAttackDamageBasisPoints => "攻击伤害增加",
        PassiveEffectKind.IncreasedAttackSpeedBasisPoints => "攻击速度增加",
        PassiveEffectKind.IncreasedTwoHandDamageBasisPoints => "双手武器伤害增加",
        PassiveEffectKind.IncreasedOneHandDamageBasisPoints => "单手武器伤害增加",
        PassiveEffectKind.IncreasedSwordDamageBasisPoints => "剑类伤害增加",
        PassiveEffectKind.IncreasedAxeDamageBasisPoints => "斧类伤害增加",
        PassiveEffectKind.IncreasedMaceDamageBasisPoints => "锤类伤害增加",
        PassiveEffectKind.IncreasedDaggerDamageBasisPoints => "匕首伤害增加",
        PassiveEffectKind.IncreasedBowDamageBasisPoints => "弓类伤害增加",
        PassiveEffectKind.IncreasedWandDamageBasisPoints => "法杖伤害增加",
        PassiveEffectKind.IncreasedUnarmedDamageBasisPoints => "徒手伤害增加",
        PassiveEffectKind.IncreasedShieldAttackDamageBasisPoints => "盾牌攻击伤害增加",
        PassiveEffectKind.IncreasedDualWieldDamageBasisPoints => "双持伤害增加",
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
        PassiveEffectKind.IncreasedCriticalChanceBasisPoints => "暴击率增加",
        PassiveEffectKind.IncreasedCriticalMultiplierBasisPoints => "暴击伤害倍率增加",
        PassiveEffectKind.IncreasedEvasionBasisPoints => "闪避增加",
        PassiveEffectKind.IncreasedShieldBasisPoints => "护盾增加",
        PassiveEffectKind.BlockChanceBasisPoints => "格挡概率",
        PassiveEffectKind.SpellSuppressionBasisPoints => "法术压制概率",
        PassiveEffectKind.FireResistanceBasisPoints => "火焰抗性",
        PassiveEffectKind.ColdResistanceBasisPoints => "冰霜抗性",
        PassiveEffectKind.LightningResistanceBasisPoints => "闪电抗性",
        PassiveEffectKind.VoidResistanceBasisPoints => "虚空抗性",
        PassiveEffectKind.IncreasedAttackSkillDamageBasisPoints => "攻击技能伤害增加",
        PassiveEffectKind.IncreasedSpellDamageBasisPoints => "法术伤害增加",
        PassiveEffectKind.IncreasedMeleeDamageBasisPoints => "近战伤害增加",
        PassiveEffectKind.IncreasedProjectileDamageBasisPoints => "投射物伤害增加",
        PassiveEffectKind.IncreasedAreaDamageBasisPoints => "范围伤害增加",
        PassiveEffectKind.IncreasedPhysicalDamageBasisPoints => "物理伤害增加",
        PassiveEffectKind.IncreasedElementalDamageBasisPoints => "元素伤害增加",
        PassiveEffectKind.IncreasedVoidDamageBasisPoints => "虚空伤害增加",
        PassiveEffectKind.IncreasedDamageOverTimeBasisPoints => "持续伤害增加",
        PassiveEffectKind.IncreasedLifeLeechRateBasisPoints => "生命偷取速率增加",
        PassiveEffectKind.IncreasedManaLeechRateBasisPoints => "法力偷取速率增加",
        PassiveEffectKind.IncreasedShieldLeechRateBasisPoints => "护盾吸收速率增加",
        PassiveEffectKind.IncreasedMinionDamageBasisPoints => "召唤物伤害增加",
        PassiveEffectKind.IncreasedCompanionDamageBasisPoints => "伙伴伤害增加",
        PassiveEffectKind.IncreasedConstructDamageBasisPoints => "构装体伤害增加",
        PassiveEffectKind.IncreasedTrapDamageBasisPoints => "陷阱伤害增加",
        PassiveEffectKind.IncreasedAuraEffectBasisPoints => "光环效果增加",
        PassiveEffectKind.IncreasedCurseEffectBasisPoints => "诅咒效果增加",
        PassiveEffectKind.IncreasedEnergyShieldRechargeBasisPoints => "能量护盾充能速度增加",
        PassiveEffectKind.ReducedSkillCostBasisPoints => "技能消耗降低",
        PassiveEffectKind.IncreasedSkillRangeBasisPoints => "技能范围增加",
        PassiveEffectKind.IncreasedCooldownRecoveryBasisPoints => "冷却恢复速度增加",
        PassiveEffectKind.MoreDamageBasisPoints => "伤害总增",
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
        ItemCategory.LifeFlask => "药剂",
        _ => category.ToString(),
    };

    private static string PositionName(AffixPosition position) => position == AffixPosition.Prefix ? "前缀" : "后缀";
    private static string NodeKind(PassiveNodeKind kind) => kind switch
    {
        PassiveNodeKind.Start => "职业起点",
        PassiveNodeKind.Small => "小型天赋",
        PassiveNodeKind.Notable => "显著天赋",
        PassiveNodeKind.Mastery => "集群专精",
        PassiveNodeKind.Rule => "规则天赋",
        PassiveNodeKind.JewelSocket => "记忆棱孔",
        _ => kind.ToString(),
    };

    private static string BranchName(PassiveBranch branch) => branch switch
    {
        PassiveBranch.HeavyWeapon => "重兵分支",
        PassiveBranch.Bleed => "流血分支",
        PassiveBranch.Defense => "防御分支",
        PassiveBranch.WarCry => "战吼分支",
        PassiveBranch.Mobility => "机动分支",
        PassiveBranch.Critical => "暴击分支",
        PassiveBranch.Accuracy => "命中分支",
        PassiveBranch.Mana => "法力分支",
        PassiveBranch.Shield => "护盾分支",
        PassiveBranch.Flask => "药剂分支",
        PassiveBranch.Elemental => "元素分支",
        PassiveBranch.Void => "虚空分支",
        _ => branch.ToString(),
    };
}
