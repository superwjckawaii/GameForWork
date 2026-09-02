using System.Text;
using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P1.Progression;
using GameForWork.Core.P14;
using GameForWork.Core.P20;
using GameForWork.Core.P29;
using Godot;

namespace GameForWork.GodotClient;

internal static class P1UiText
{
    public static string ItemTooltip(ItemInstance item, bool includeAffixDetails = false)
    {
        var text = new StringBuilder();
        text.AppendLine($"{RarityName(item.Rarity)}·{item.DisplayName}{(item.Quality > 0 ? $"+{item.Quality}" : string.Empty)}");
        text.AppendLine($"底材：{item.Base.DisplayName}");
        text.AppendLine($"物品等级 {item.ItemLevel} · {item.Base.DetailedTypeName}");
        text.AppendLine($"底材阶级：{P29DropCatalog.BaseTierName(P29DropCatalog.BaseTier(item.Base))}");
        string[] requirements =
        [
            item.Base.RequiredLevel > 0 ? $"等级 {item.Base.RequiredLevel}" : string.Empty,
            item.Base.RequiredPhysique > 0 ? $"体魄 {item.Base.RequiredPhysique}" : string.Empty,
            item.Base.RequiredDexterity > 0 ? $"灵巧 {item.Base.RequiredDexterity}" : string.Empty,
            item.Base.RequiredSpirit > 0 ? $"精神 {item.Base.RequiredSpirit}" : string.Empty,
            item.Base.RequiredEnergy > 0 ? $"能量 {item.Base.RequiredEnergy}" : string.Empty,
        ];
        string requirementText = string.Join(" · ", requirements.Where(value => value.Length > 0));
        if (requirementText.Length > 0) text.AppendLine($"需求：{requirementText}");
        if (item.Base.Category is ItemCategory.TwoHandWeapon or ItemCategory.OneHandWeapon)
        {
            LocalWeaponStats local = EquipmentLoadout.CalculateLocalWeapon(item);
            WeaponProfile weapon = local.Physical;
            text.AppendLine($"物理伤害 {weapon.MinimumPhysicalDamage}–{weapon.MaximumPhysicalDamage}");
            AppendDamageRange(text, "火焰伤害", local.Fire);
            AppendDamageRange(text, "冰霜伤害", local.Cold);
            AppendDamageRange(text, "闪电伤害", local.Lightning);
            AppendDamageRange(text, "虚空伤害", local.Void);
            text.AppendLine($"攻击频率 {weapon.AttacksPerSecondMilli / 1000.0:0.00}/秒 · 暴击 {weapon.CriticalChanceBasisPoints / 100.0:0.0}%");
            if (includeAffixDetails)
            {
                text.AppendLine(
                    $"底材属性：物理点伤 {item.Base.MinimumPhysicalDamage}–{item.Base.MaximumPhysicalDamage} " +
                    $"[roll {item.Base.MinimumPhysicalDamage}–{item.Base.MaximumPhysicalDamage}]");
                text.AppendLine(
                    $"底材属性：攻击频率 {item.Base.AttacksPerSecondMilli / 1000.0:0.00}/秒 " +
                    $"[roll {item.Base.AttacksPerSecondMilli / 1000.0:0.00}–{item.Base.AttacksPerSecondMilli / 1000.0:0.00}]/秒 · " +
                    $"暴击 {item.Base.CriticalChanceBasisPoints / 100.0:0.0}% " +
                    $"[roll {item.Base.CriticalChanceBasisPoints / 100.0:0.0}–{item.Base.CriticalChanceBasisPoints / 100.0:0.0}%]");
                string[] dpsParts =
                [
                    $"物理 {local.PhysicalDamagePerSecond:0.0}",
                    local.ElementalDamagePerSecond > 0 ? $"元素 {local.ElementalDamagePerSecond:0.0}" : string.Empty,
                    local.VoidDamagePerSecond > 0 ? $"虚空 {local.VoidDamagePerSecond:0.0}" : string.Empty,
                ];
                text.AppendLine($"[DPS:{local.TotalDamagePerSecond:0.###}]武器秒伤 {local.TotalDamagePerSecond:0.0}（{string.Join(" · ", dpsParts.Where(part => part.Length > 0))}）");
            }
        }

        var localDefense = EquipmentLoadout.CalculateLocalDefense(item);
        if (localDefense.Armor + localDefense.Evasion + localDefense.Shield + localDefense.SpiritBarrier > 0)
        {
            string[] defenses =
            [
                localDefense.Armor > 0 ? $"护甲 {localDefense.Armor}" : string.Empty,
                localDefense.Evasion > 0 ? $"闪避 {localDefense.Evasion}" : string.Empty,
                localDefense.Shield > 0 ? $"护盾 {localDefense.Shield}" : string.Empty,
                localDefense.SpiritBarrier > 0 ? $"灵障 {localDefense.SpiritBarrier}" : string.Empty,
            ];
            text.AppendLine(string.Join(" · ", defenses.Where(value => value.Length > 0)));
        }
        if (localDefense.BlockChanceBasisPoints > 0)
        {
            text.AppendLine($"装备格挡 {localDefense.BlockChanceBasisPoints / 100.0:0.#}%");
        }
        if (includeAffixDetails)
        {
            AppendBaseDefenseRoll(text, "护甲", item.Base.Armor, item.Base.ArmorMinimum, item.Base.ArmorMaximum);
            AppendBaseDefenseRoll(text, "闪避", item.Base.Evasion, item.Base.EvasionMinimum, item.Base.EvasionMaximum);
            AppendBaseDefenseRoll(text, "护盾", item.Base.Shield, item.Base.ShieldMinimum, item.Base.ShieldMaximum);
            AppendBaseDefenseRoll(text, "灵障", item.Base.SpiritBarrier, item.Base.SpiritBarrier, item.Base.SpiritBarrier);
            if (item.Base.BlockChanceBasisPoints > 0)
            {
                double block = item.Base.BlockChanceBasisPoints / 100.0;
                text.AppendLine($"底材属性：格挡 {block:0.#}% [roll {block:0.#}–{block:0.#}%]");
            }
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
            string rollRange = includeAffixDetails
                ? $" [roll {RangeValue(item.Base.ImplicitModifier, item.Base.ImplicitMinimumValue)}～{RangeValue(item.Base.ImplicitModifier, item.Base.ImplicitMaximumValue)}]"
                : string.Empty;
            text.AppendLine($"（基底词缀）{label} {RangeValue(item.Base.ImplicitModifier, item.EffectiveImplicitValue)}{rollRange}");
        }
        foreach (ItemBaseImplicit implicitModifier in item.Base.ExtraImplicits)
            text.AppendLine($"（基底词缀）{implicitModifier.DisplayText}" + (includeAffixDetails ? " [固定]" : string.Empty));

        if (item.Enchantment is not null)
            text.AppendLine($"（附魔）{item.Enchantment.DisplayName}：{string.Join("；", item.Enchantment.EffectComponents.Select(effect => Modifier(effect.Kind, effect.MinimumValue)))}");

        if (item.LegendaryRule is not null)
        {
            P14UniqueDefinition? unique = P14UniqueItems.All.FirstOrDefault(definition =>
                definition.StableId == item.LegendaryRule.StableId);
            if (unique is not null)
            {
                foreach (P14LegendaryAffixDefinition affix in unique.LegendaryAffixes)
                    text.AppendLine($"[LEGENDARY]{affix.Text}");
            }
            else
            {
                text.AppendLine($"[LEGENDARY]{item.LegendaryRule.DisplayText}");
            }
        }

        foreach (AffixRoll affix in item.Affixes.OrderBy(affix => affix.Definition.Position).ThenBy(affix => affix.Definition.Tier))
        {
            if (string.Equals(affix.Definition.Source, "传奇固定", StringComparison.Ordinal))
            {
                text.AppendLine($"[LEGENDARY]{AffixEffects(affix)}");
                continue;
            }
            int tier = P1Affixes.TierFor(item.Base, affix.Definition);
            string markers = (affix.Crafted ? "（工匠）" : string.Empty) + (item.IsFractured(affix) ? "（破溃）" : string.Empty);
            string details = includeAffixDetails && tier > 0
                ? $" {AffixRanges(affix)}（T{tier}）"
                : string.Empty;
            string line = $"{PositionName(affix.Definition.Position)}{markers} - {AffixEffects(affix)}{details}";
            text.AppendLine(tier <= 0 ? line : $"[TIER:{tier}]{line}");
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

    private static void AppendDamageRange(StringBuilder text, string label, LocalDamageRange range)
    {
        if (range.HasDamage) text.AppendLine($"{label} {range.Minimum}–{range.Maximum}");
    }

    private static void AppendBaseDefenseRoll(StringBuilder text, string name, int actual, int minimum, int maximum)
    {
        if (actual <= 0) return;
        int low = minimum > 0 ? minimum : actual;
        int high = maximum > 0 ? maximum : low;
        text.AppendLine($"底材属性：{name} {actual} [roll {low}–{high}]");
    }

    public static string PassiveTooltip(
        PassiveNodeDefinition node,
        bool allocated,
        bool available)
    {
        if (node.StableId.StartsWith("p30.", StringComparison.Ordinal))
        {
            string header = $"{node.DisplayName}·{NodeKind(node.Kind)}";
            string branch = string.IsNullOrWhiteSpace(node.ClusterTheme)
                ? BranchName(node.Branch)
                : $"{node.ClusterTheme}分支";
            string effects = node.Kind == PassiveNodeKind.Mastery
                ? "从以下专精中选择一项：\n" + string.Join('\n',
                    P1PassiveTree.MasteryOptionDescriptions(node).Select((text, index) => $"{index + 1}. {text}"))
                : !string.IsNullOrWhiteSpace(node.SpecialRule)
                    ? node.SpecialRule
                    : string.Join('\n', node.Effects.Select(PassiveEffect));
            string p30Jewel = node.Kind == PassiveNodeKind.JewelSocket
                ? "\n可拖入一枚珠宝；半径 210，读取范围内已分配节点。"
                : string.Empty;
            return $"{header}\n{branch}\n{effects}{p30Jewel}";
        }
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

    public static Color AffixTierColor(int tier)
    {
        if (tier <= 0) return new Color("dedede");
        float strength = (13 - Math.Clamp(tier, 1, 13)) / 12f;
        strength = MathF.Pow(strength, .72f);
        return new Color(
            0.87f + 0.08f * strength,
            0.87f - 0.11f * strength,
            0.87f - 0.56f * strength);
    }

    public static Color LegendaryAffixColor => new("d7a8ff");

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
        string sign = value >= 0 ? "+" : string.Empty;
        return kind.ToString().Contains("BasisPoints", StringComparison.Ordinal)
            ? $"{name} {sign}{value / 100.0:0.#}%"
            : $"{name} {sign}{value}";
    }

    private static string AffixEffects(AffixRoll affix)
    {
        IReadOnlyList<RolledAffixComponent> effects = affix.Effects;
        string Damage(ItemModifierKind minimum, ItemModifierKind maximum, string name)
        {
            RolledAffixComponent? low = effects.FirstOrDefault(effect => effect.Kind == minimum);
            RolledAffixComponent? high = effects.FirstOrDefault(effect => effect.Kind == maximum);
            return low is null || high is null ? string.Empty : $"{name} {low.Value}–{high.Value}";
        }
        foreach ((ItemModifierKind Min, ItemModifierKind Max, string Name) pair in new[]
        {
            (ItemModifierKind.AddedMinimumPhysicalDamage, ItemModifierKind.AddedMaximumPhysicalDamage, "附加物理伤害"),
            (ItemModifierKind.AddedMinimumFireDamage, ItemModifierKind.AddedMaximumFireDamage, "附加火焰伤害"),
            (ItemModifierKind.AddedMinimumColdDamage, ItemModifierKind.AddedMaximumColdDamage, "附加冰霜伤害"),
            (ItemModifierKind.AddedMinimumLightningDamage, ItemModifierKind.AddedMaximumLightningDamage, "附加闪电伤害"),
            (ItemModifierKind.AddedMinimumVoidDamage, ItemModifierKind.AddedMaximumVoidDamage, "附加虚空伤害"),
        })
        {
            string damage = Damage(pair.Min, pair.Max, pair.Name);
            if (damage.Length > 0 && effects.Count == 2) return damage;
        }
        return string.Join("；", effects.Select(effect => Modifier(effect.Kind, effect.Value)));
    }

    private static string AffixRanges(AffixRoll affix)
    {
        IReadOnlyList<AffixModifierComponent> definitions = affix.Definition.EffectComponents;
        IReadOnlyList<RolledAffixComponent> rolled = affix.Effects;
        var ranges = new List<string>(rolled.Count);
        for (int index = 0; index < rolled.Count; index++)
        {
            RolledAffixComponent effect = rolled[index];
            AffixModifierComponent? definition = definitions.FirstOrDefault(candidate => candidate.Kind == effect.Kind)
                ?? definitions.ElementAtOrDefault(index);
            if (definition is null) continue;
            int minimum = rolled.Count == 1 ? affix.EffectiveMinimumValue : definition.MinimumValue;
            int maximum = rolled.Count == 1 ? affix.EffectiveMaximumValue : definition.MaximumValue;
            ranges.Add($"[{RangeValue(effect.Kind, minimum)}, {RangeValue(effect.Kind, maximum)}]");
        }
        return ranges.Count == 0 ? string.Empty : string.Join(' ', ranges);
    }

    internal static string AffixComponentRange(ItemModifierKind kind, int minimum, int maximum)
    {
        string low = RangeValue(kind, minimum);
        string high = RangeValue(kind, maximum);
        return $"{ModifierName(kind)} {low}{(low == high ? string.Empty : $"–{high}")}";
    }

    private static string RangeValue(ItemModifierKind kind, int value)
    {
        string sign = value >= 0 ? "+" : string.Empty;
        return kind.ToString().Contains("BasisPoints", StringComparison.Ordinal)
            ? $"{sign}{value / 100.0:0.#}%"
            : $"{sign}{value}";
    }

    internal static string ModifierName(ItemModifierKind kind) => kind switch
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
        ItemModifierKind.IncreasedCooldownRecoveryBasisPoints => "冷却恢复速度增加",
        ItemModifierKind.IncreasedFlaskChargeGainBasisPoints => "药剂充能获取增加",
        ItemModifierKind.IncreasedFlaskDurationBasisPoints => "药剂持续时间增加",
        ItemModifierKind.IncreasedMaximumLifeBasisPoints => "最大生命增加",
        ItemModifierKind.IncreasedMaximumManaBasisPoints => "最大法力增加",
        ItemModifierKind.IncreasedMaximumShieldBasisPoints => "最大护盾增加",
        ItemModifierKind.MaximumAllResistanceBasisPoints => "四元素最大抗性",
        ItemModifierKind.MoreRareBossDamageBasisPoints => "对稀有与Boss伤害总增",
        ItemModifierKind.ActiveSkillGemLevels => "主动技能石等级",
        ItemModifierKind.SupportSkillGemLevels => "辅助技能石等级",
        ItemModifierKind.AddedMinimumPhysicalDamage => "附加物理伤害下限",
        ItemModifierKind.AddedMaximumPhysicalDamage => "附加物理伤害上限",
        ItemModifierKind.AddedMinimumFireDamage => "附加火焰伤害下限",
        ItemModifierKind.AddedMaximumFireDamage => "附加火焰伤害上限",
        ItemModifierKind.AddedMinimumColdDamage => "附加冰霜伤害下限",
        ItemModifierKind.AddedMaximumColdDamage => "附加冰霜伤害上限",
        ItemModifierKind.AddedMinimumLightningDamage => "附加闪电伤害下限",
        ItemModifierKind.AddedMaximumLightningDamage => "附加闪电伤害上限",
        ItemModifierKind.AddedMinimumVoidDamage => "附加虚空伤害下限",
        ItemModifierKind.AddedMaximumVoidDamage => "附加虚空伤害上限",
        ItemModifierKind.FlatArmor => "护甲",
        ItemModifierKind.FlatEvasion => "闪避",
        ItemModifierKind.FlatShield => "最大护盾",
        ItemModifierKind.FlatSpiritBarrier => "灵障",
        ItemModifierKind.IncreasedSpiritBarrierBasisPoints => "灵障提高",
        ItemModifierKind.IncreasedLocalBlockBasisPoints => "盾牌基础格挡提高",
        ItemModifierKind.IncreasedAttackDamageBasisPoints => "攻击伤害提高",
        ItemModifierKind.IncreasedSpellDamageBasisPoints => "法术伤害提高",
        ItemModifierKind.IncreasedElementalDamageBasisPoints => "元素伤害提高",
        ItemModifierKind.IncreasedFireDamageBasisPoints => "火焰伤害提高",
        ItemModifierKind.IncreasedColdDamageBasisPoints => "冰霜伤害提高",
        ItemModifierKind.IncreasedLightningDamageBasisPoints => "闪电伤害提高",
        ItemModifierKind.IncreasedVoidDamageBasisPoints => "虚空伤害提高",
        ItemModifierKind.IncreasedMeleeDamageBasisPoints => "近战伤害提高",
        ItemModifierKind.IncreasedProjectileDamageBasisPoints => "投射物伤害提高",
        ItemModifierKind.IncreasedAreaDamageBasisPoints => "范围伤害提高",
        ItemModifierKind.IncreasedDamageOverTimeBasisPoints => "持续伤害提高",
        ItemModifierKind.DamageOverTimeMultiplierBasisPoints => "持续伤害倍率",
        ItemModifierKind.IncreasedBleedDamageBasisPoints => "流血伤害提高",
        ItemModifierKind.IncreasedPoisonDamageBasisPoints => "中毒伤害提高",
        ItemModifierKind.IncreasedIgniteDamageBasisPoints => "点燃伤害提高",
        ItemModifierKind.FasterBleedBasisPoints => "流血伤害加快",
        ItemModifierKind.FasterPoisonBasisPoints => "中毒伤害加快",
        ItemModifierKind.FasterIgniteBasisPoints => "点燃伤害加快",
        ItemModifierKind.IncreasedCriticalMultiplierBasisPoints => "暴击伤害倍率",
        ItemModifierKind.IncreasedCastSpeedBasisPoints => "施法速度提高",
        ItemModifierKind.ProjectileSpeedBasisPoints => "投射物速度提高",
        ItemModifierKind.SkillAreaBasisPoints => "技能范围效果提高",
        ItemModifierKind.SkillRangeBasisPoints => "技能距离提高",
        ItemModifierKind.AdditionalProjectile => "额外投射物",
        ItemModifierKind.AdditionalChain => "额外连锁",
        ItemModifierKind.AdditionalStrikeTarget => "额外打击目标",
        ItemModifierKind.AdditionalPierce => "额外穿透目标",
        ItemModifierKind.MaximumLifeRegenerationBasisPoints => "每秒恢复最大生命",
        ItemModifierKind.MaximumShieldRegenerationBasisPoints => "每秒恢复最大护盾",
        ItemModifierKind.IncreasedResourceRecoveryRateBasisPoints => "资源恢复率提高",
        ItemModifierKind.PhysicalResistanceBasisPoints => "物理抗性",
        ItemModifierKind.SpellSuppressionEffectBasisPoints => "法术压制效果",
        ItemModifierKind.LifeLeechBasisPoints => "击中伤害生命偷取",
        ItemModifierKind.ManaLeechBasisPoints => "击中伤害法力偷取",
        ItemModifierKind.ShieldLeechBasisPoints => "击中伤害护盾偷取",
        ItemModifierKind.LifeOnHit => "击中回复生命",
        ItemModifierKind.ManaOnHit => "击中回复法力",
        ItemModifierKind.ShieldOnHit => "击中回复护盾",
        ItemModifierKind.ReservationEfficiencyBasisPoints => "保留效率",
        ItemModifierKind.IncreasedAuraEffectBasisPoints => "光环效果提高",
        ItemModifierKind.IncreasedCurseEffectBasisPoints => "诅咒效果提高",
        ItemModifierKind.AllActiveSkillGemLevels => "所有主动技能石等级",
        ItemModifierKind.AllSupportSkillGemLevels => "所有辅助技能石等级",
        ItemModifierKind.AdditionalUnitMaximum => "单位上限",
        ItemModifierKind.AdditionalMinionMaximum => "召唤物上限",
        ItemModifierKind.AdditionalConstructMaximum => "构装上限",
        ItemModifierKind.AdditionalTrapMaximum => "陷阱上限",
        ItemModifierKind.AdditionalPhantomMaximum => "幻身上限",
        ItemModifierKind.FirePenetrationBasisPoints => "火焰穿透",
        ItemModifierKind.ColdPenetrationBasisPoints => "冰霜穿透",
        ItemModifierKind.LightningPenetrationBasisPoints => "闪电穿透",
        ItemModifierKind.VoidPenetrationBasisPoints => "虚空穿透",
        ItemModifierKind.BleedChanceBasisPoints => "流血概率",
        ItemModifierKind.PoisonChanceBasisPoints => "中毒概率",
        ItemModifierKind.IgniteChanceBasisPoints => "点燃概率",
        ItemModifierKind.ShockChanceBasisPoints => "感电概率",
        ItemModifierKind.ReducedShieldRechargeDelayBasisPoints => "护盾充能延迟缩短",
        ItemModifierKind.AilmentAvoidanceBasisPoints => "避免元素异常",
        ItemModifierKind.ReducedCurseEffectBasisPoints => "受到诅咒效果降低",
        ItemModifierKind.ReducedDebuffDurationBasisPoints => "非异常负面持续时间降低",
        ItemModifierKind.IncreasedLeechRecoveryRateBasisPoints => "偷取恢复速度提高",
        ItemModifierKind.IncreasedMaximumLeechRateBasisPoints => "偷取每秒总恢复上限提高",
        ItemModifierKind.PhysicalToFireConversionBasisPoints => "物理转火焰",
        ItemModifierKind.PhysicalToColdConversionBasisPoints => "物理转冰霜",
        ItemModifierKind.PhysicalToLightningConversionBasisPoints => "物理转闪电",
        ItemModifierKind.PhysicalToVoidConversionBasisPoints => "物理转虚空",
        ItemModifierKind.ColdToFireConversionBasisPoints => "冰霜转火焰",
        ItemModifierKind.LightningToFireConversionBasisPoints => "闪电转火焰",
        ItemModifierKind.FireToVoidConversionBasisPoints => "火焰转虚空",
        ItemModifierKind.ColdToVoidConversionBasisPoints => "冰霜转虚空",
        ItemModifierKind.LightningToVoidConversionBasisPoints => "闪电转虚空",
        ItemModifierKind.PhysicalAsExtraFireBasisPoints => "物理额外获得火焰",
        ItemModifierKind.PhysicalAsExtraColdBasisPoints => "物理额外获得冰霜",
        ItemModifierKind.PhysicalAsExtraLightningBasisPoints => "物理额外获得闪电",
        ItemModifierKind.ElementalAsExtraVoidBasisPoints => "元素额外获得虚空",
        ItemModifierKind.IncreasedPhysiqueBasisPoints => "体魄提高",
        ItemModifierKind.IncreasedDexterityBasisPoints => "灵巧提高",
        ItemModifierKind.IncreasedSpiritBasisPoints => "精神提高",
        ItemModifierKind.IncreasedEnergyBasisPoints => "能量提高",
        ItemModifierKind.IncreasedAllAttributesBasisPoints => "所有属性提高",
        ItemModifierKind.AdditionalCoreSkillCapacity => "核心技能容量",
        ItemModifierKind.HoldHumilityAtMaximum => "谦逊常驻最大层数",
        ItemModifierKind.HoldArroganceAtMaximum => "傲慢常驻最大层数",
        ItemModifierKind.HoldRageAtMaximum => "暴怒常驻最大层数",
        ItemModifierKind.HoldTemperanceAtMaximum => "节制常驻最大层数",
        ItemModifierKind.HoldMercyAtMaximum => "慈悲常驻最大层数",
        ItemModifierKind.HoldSlothAtMaximum => "懒惰常驻最大层数",
        ItemModifierKind.IncreasedFlaskRecoveryAmountBasisPoints => "药剂恢复量提高",
        ItemModifierKind.IncreasedFlaskRecoveryRateBasisPoints => "药剂恢复速度提高",
        ItemModifierKind.IncreasedFlaskChargesPerUseBasisPoints => "药剂每次充能消耗提高",
        ItemModifierKind.InstantFlaskRecoveryPortionBasisPoints => "药剂立即恢复比例",
        ItemModifierKind.FlaskRecoveryAtEnd => "药剂效果结束时结算恢复",
        ItemModifierKind.FlaskLifeRemovedFromManaBasisPoints => "从法力移除生命恢复量",
        ItemModifierKind.FlaskManaRemovedFromLifeBasisPoints => "从生命移除法力恢复量",
        ItemModifierKind.FlaskBuffArmorBasisPoints => "药剂期间护甲提高",
        ItemModifierKind.FlaskBuffEvasionBasisPoints => "药剂期间闪避提高",
        ItemModifierKind.FlaskBuffCriticalChanceBasisPoints => "药剂期间暴击率提高",
        ItemModifierKind.FlaskBuffMovementSpeedBasisPoints => "药剂期间移动速度提高",
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
        PassiveNodeKind.Mastery => "专精天赋",
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
