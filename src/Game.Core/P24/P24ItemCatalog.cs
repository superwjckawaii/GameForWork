using GameForWork.Core.P1.Items;

namespace GameForWork.Core.P24;

public sealed record P24AffixFamily(
    string StableId,
    string DisplayName,
    AffixPosition Position,
    ItemModifierKind Modifier,
    IReadOnlyList<string> Tags,
    string RuleText);

public static class P24ItemCatalog
{
    public static IReadOnlyList<ItemBaseDefinition> Bases { get; } = BuildBases();
    public static IReadOnlyList<P24AffixFamily> Families { get; } = BuildFamilies();
    public static IReadOnlyList<AffixDefinition> Affixes { get; } = BuildAffixes();

    static P24ItemCatalog()
    {
        if (Bases.Count != 50 || Bases.Select(value => value.StableId).Distinct().Count() != 50)
            throw new InvalidDataException("P24 item catalog must contain fifty unique bases.");
        if (Families.Count != 45 || Families.Select(value => value.StableId).Distinct().Count() != 45 || Affixes.Count != 135)
            throw new InvalidDataException("P24 item catalog must contain forty-five three-tier affix families.");
    }

    private static IReadOnlyList<ItemBaseDefinition> BuildBases()
    {
        var result = new List<ItemBaseDefinition>();
        AddWeapons(result, "bow", ItemCategory.TwoHandWeapon, EquipmentSlot.MainHand,
            ["粗弦短弓", "巡林反曲弓", "风脊长弓", "鹰角战弓", "穿云复合弓", "星轨猎弓"], 6, 14, 1_350, 550, ["weapon", "bow", "projectile", "two_hand_weapon"]);
        AddWeapons(result, "dagger", ItemCategory.OneHandWeapon, EquipmentSlot.MainHand,
            ["缺口匕首", "影钢短刃", "蛇牙刃", "背誓刺", "暮光双锋", "无声终刃"], 5, 11, 1_650, 700, ["weapon", "dagger", "melee", "one_hand_weapon"]);
        AddWeapons(result, "wand", ItemCategory.OneHandWeapon, EquipmentSlot.MainHand,
            ["灰木法杖", "棱晶短杖", "风暴导杖", "霜语魔杖", "蚀星权杖", "秘界法杖"], 3, 8, 1_400, 650, ["weapon", "wand", "caster", "one_hand_weapon"]);
        AddOffhands(result, "quiver", ["旧革箭袋", "斥候箭囊", "倒刺箭袋", "风羽箭囊", "猎首箭匣"],
            ["offhand", "quiver", "bow", "projectile"], 120, 0, 0);
        AddOffhands(result, "focus", ["裂纹棱镜", "元素罗盘", "虚空透镜", "秘盾法器", "星象焦点"],
            ["offhand", "focus", "caster", "energy_shield"], 0, 0, 90);
        AddOffhands(result, "summoning_focus", ["骨哨", "魂灯", "王骸权印", "颂歌灵媒", "末日咒册"],
            ["offhand", "summoning_focus", "minion", "curse"], 0, 0, 70);
        AddArmor(result, "unarmed_wrap", ItemCategory.Gloves, EquipmentSlot.Gloves,
            ["麻布拳带", "铁砂缠手", "风纹拳套", "灵兽护腕", "十方圣缠"], ["gloves", "unarmed", "wrap"], 0, 110, 0);
        AddJewels(result, "beast_talisman", ["幼兽骨符", "守巢牙坠", "荒魂角饰", "万兽心印"],
            ["amulet", "beast_talisman", "companion"]);
        AddWeapons(result, "runeblade", ItemCategory.OneHandWeapon, EquipmentSlot.MainHand,
            ["钝刻符刃", "焰文短剑", "六印术刃", "应答长刃"], 8, 16, 1_400, 600,
            ["weapon", "runeblade", "melee", "caster", "one_hand_weapon"]);
        AddOffhands(result, "construct_idol", ["铆钉偶像", "炮膛核心", "符阵机枢", "重铸圣像"],
            ["offhand", "construct_idol", "construct"], 80, 0, 50);
        return result;
    }

    private static IReadOnlyList<P24AffixFamily> BuildFamilies()
    {
        var result = new List<P24AffixFamily>();
        AddTheme(result, "projectile", "投射", ["bow", "quiver", "projectile"],
            ("added_projectile", "额外投射", AffixPosition.Prefix, ItemModifierKind.AddedPhysicalDamage, "投射物技能获得额外基础伤害。"),
            ("projectile_speed", "疾行", AffixPosition.Suffix, ItemModifierKind.IncreasedAttackSpeedBasisPoints, "投射物速度提高。"),
            ("pierce", "穿云", AffixPosition.Prefix, ItemModifierKind.FlatAccuracy, "投射物额外穿透目标。"),
            ("far_damage", "远猎", AffixPosition.Prefix, ItemModifierKind.IncreasedPhysicalDamageBasisPoints, "远距离命中伤害提高。"),
            ("mark_effect", "猎印", AffixPosition.Suffix, ItemModifierKind.IncreasedCriticalChanceBasisPoints, "标记效果提高。"));
        AddTheme(result, "shadow", "暗影", ["dagger"],
            ("backstab", "背袭", AffixPosition.Prefix, ItemModifierKind.IncreasedCriticalChanceBasisPoints, "背后命中伤害提高。"),
            ("poison", "淬毒", AffixPosition.Prefix, ItemModifierKind.IncreasedBleedChanceBasisPoints, "毒素积累提高。"),
            ("trap_limit", "设伏", AffixPosition.Suffix, ItemModifierKind.ExtraSupportLinkCapacity, "陷阱上限提高。"),
            ("evasion", "无踪", AffixPosition.Suffix, ItemModifierKind.IncreasedEvasionBasisPoints, "移动后闪避提高。"));
        AddTheme(result, "minion", "召唤", ["summoning_focus", "minion"],
            ("maximum", "统御", AffixPosition.Prefix, ItemModifierKind.ExtraSupportLinkCapacity, "召唤物上限提高。"),
            ("damage", "军势", AffixPosition.Prefix, ItemModifierKind.IncreasedPhysicalDamageBasisPoints, "召唤物伤害提高。"),
            ("life", "骨铸", AffixPosition.Prefix, ItemModifierKind.FlatMaximumLife, "召唤单位最大生命提高。"),
            ("speed", "魂驰", AffixPosition.Suffix, ItemModifierKind.IncreasedAttackSpeedBasisPoints, "召唤单位行动速度提高。"),
            ("aura", "共鸣", AffixPosition.Suffix, ItemModifierKind.IncreasedManaRegenerationBasisPoints, "光环效果提高。"));
        AddTheme(result, "curse", "诅咒", ["summoning_focus", "curse", "caster"],
            ("effect", "恶咒", AffixPosition.Prefix, ItemModifierKind.IncreasedPhysicalDamageBasisPoints, "诅咒效果提高。"),
            ("duration", "绵延", AffixPosition.Suffix, ItemModifierKind.IncreasedManaRegenerationBasisPoints, "诅咒持续时间提高。"),
            ("propagation", "传疫", AffixPosition.Suffix, ItemModifierKind.FlatAccuracy, "诅咒传播半径提高。"),
            ("reservation", "低吟", AffixPosition.Prefix, ItemModifierKind.FlatMaximumMana, "光环保留降低。"));
        AddTheme(result, "spell", "秘术", ["wand", "focus", "caster"],
            ("elemental", "棱彩", AffixPosition.Prefix, ItemModifierKind.IncreasedPhysicalDamageBasisPoints, "元素法术伤害提高。"),
            ("void", "虚蚀", AffixPosition.Prefix, ItemModifierKind.IncreasedPhysicalDamageBasisPoints, "虚空法术伤害提高。"),
            ("cast_speed", "疾咏", AffixPosition.Suffix, ItemModifierKind.IncreasedAttackSpeedBasisPoints, "施法速度提高。"),
            ("shield", "秘盾", AffixPosition.Prefix, ItemModifierKind.IncreasedShieldBasisPoints, "最大能量护盾提高。"),
            ("recharge", "回流", AffixPosition.Suffix, ItemModifierKind.IncreasedManaRegenerationBasisPoints, "能量护盾充能速度提高。"));
        AddTheme(result, "occult", "禁术", ["wand", "focus", "caster", "energy_shield"],
            ("wither", "深凋", AffixPosition.Prefix, ItemModifierKind.IncreasedBleedChanceBasisPoints, "凋零积累提高。"),
            ("shield_leech", "汲盾", AffixPosition.Suffix, ItemModifierKind.IncreasedManaRegenerationBasisPoints, "法术命中恢复能量护盾。"),
            ("shield_cost", "盾铸", AffixPosition.Prefix, ItemModifierKind.FlatMaximumMana, "允许部分技能消耗能量护盾。"),
            ("barrier", "镜障", AffixPosition.Suffix, ItemModifierKind.IncreasedShieldBasisPoints, "吸收屏障效果提高。"));
        AddTheme(result, "unarmed", "徒手", ["unarmed", "wrap"],
            ("damage", "刚拳", AffixPosition.Prefix, ItemModifierKind.AddedPhysicalDamage, "徒手攻击附加物理伤害。"),
            ("combo", "连势", AffixPosition.Suffix, ItemModifierKind.IncreasedAttackSpeedBasisPoints, "连击保留时间提高。"),
            ("stance", "阴阳", AffixPosition.Prefix, ItemModifierKind.IncreasedPhysicalDamageBasisPoints, "姿态效果提高。"),
            ("phantom", "百影", AffixPosition.Suffix, ItemModifierKind.ExtraSupportLinkCapacity, "幻身上限提高。"));
        AddTheme(result, "companion", "灵兽", ["beast_talisman", "companion"],
            ("damage", "兽怒", AffixPosition.Prefix, ItemModifierKind.IncreasedPhysicalDamageBasisPoints, "灵兽伤害提高。"),
            ("guard", "兽护", AffixPosition.Prefix, ItemModifierKind.FlatMaximumLife, "灵兽最大生命提高。"),
            ("revive", "归巢", AffixPosition.Suffix, ItemModifierKind.IncreasedManaRegenerationBasisPoints, "灵兽复生延迟降低。"),
            ("bond", "双魂", AffixPosition.Suffix, ItemModifierKind.IncreasedCriticalChanceBasisPoints, "主角与灵兽夹击效果提高。"));
        AddTheme(result, "monk", "行者", ["unarmed", "wrap", "beast_talisman"],
            ("mobility", "逐风", AffixPosition.Suffix, ItemModifierKind.IncreasedMovementSpeedBasisPoints, "位移技能冷却恢复提高。"));
        AddTheme(result, "rune", "符文", ["runeblade"],
            ("imprint", "刻印", AffixPosition.Prefix, ItemModifierKind.AddedPhysicalDamage, "刻印获得量提高。"),
            ("trigger", "应答", AffixPosition.Suffix, ItemModifierKind.IncreasedAttackSpeedBasisPoints, "攻击触发冷却恢复提高。"),
            ("spellblade", "法武", AffixPosition.Prefix, ItemModifierKind.IncreasedPhysicalDamageBasisPoints, "法术伤害转为攻击伤害提高。"),
            ("spellarmor", "魔铠", AffixPosition.Prefix, ItemModifierKind.IncreasedArmorBasisPoints, "魔铠效果提高。"));
        AddTheme(result, "construct", "构装", ["construct_idol", "construct"],
            ("maximum", "增殖", AffixPosition.Prefix, ItemModifierKind.ExtraSupportLinkCapacity, "构装体上限提高。"),
            ("damage", "炮铸", AffixPosition.Prefix, ItemModifierKind.IncreasedPhysicalDamageBasisPoints, "构装伤害提高。"),
            ("life", "钢芯", AffixPosition.Prefix, ItemModifierKind.FlatMaximumLife, "构装最大生命提高。"),
            ("priority", "猎首协议", AffixPosition.Suffix, ItemModifierKind.FlatAccuracy, "构装对稀有怪和Boss伤害提高。"),
            ("rebuild", "重铸", AffixPosition.Suffix, ItemModifierKind.IncreasedManaRegenerationBasisPoints, "构装重铸延迟降低。"));
        return result;
    }

    private static IReadOnlyList<AffixDefinition> BuildAffixes()
    {
        var result = new List<AffixDefinition>(135);
        foreach (P24AffixFamily family in Families)
        for (int tier = 3; tier >= 1; tier--)
        {
            int power = 4 - tier;
            IReadOnlyList<ItemCategory> categories = Categories(family.Tags);
            result.Add(new AffixDefinition(family.StableId, family.DisplayName, categories[0], family.Position,
                tier, tier == 3 ? 1 : tier == 2 ? 25 : 60, power * 300, power * 500,
                1_200 / power, family.Modifier, family.StableId, categories,
                family.Tags.ToDictionary(tag => tag, _ => 1_000 / power, StringComparer.Ordinal),
                "P24", family.RuleText, family.Tags, Source: "Natural"));
        }
        return result;
    }

    private static void AddWeapons(ICollection<ItemBaseDefinition> target, string key, ItemCategory category,
        EquipmentSlot slot, IReadOnlyList<string> names, int minimum, int maximum, int speed, int crit,
        IReadOnlyList<string> tags)
    {
        for (int index = 0; index < names.Count; index++)
            target.Add(new ItemBaseDefinition($"p24.base.{key}.{index + 1}", names[index], category, slot,
                minimum + index * 3, maximum + index * 6, speed + index * 15, crit + index * 15,
                CoreSkillCapacity: index >= names.Count - 2 ? 1 : 0, SupportLinkCapacity: Math.Min(5, 2 + index / 2),
                RequiredLevel: 1 + index * 12, RequiredDexterity: key is "bow" or "dagger" ? 10 + index * 12 : 0,
                RequiredEnergy: key is "wand" or "runeblade" ? 10 + index * 12 : 0, SourceId: "P24", Tags: tags,
                SocketLimit: Math.Min(6, 2 + index / 2)));
    }

    private static void AddOffhands(ICollection<ItemBaseDefinition> target, string key, IReadOnlyList<string> names,
        IReadOnlyList<string> tags, int evasion, int armor, int shield)
    {
        for (int index = 0; index < names.Count; index++)
            target.Add(new ItemBaseDefinition($"p24.base.{key}.{index + 1}", names[index], ItemCategory.Shield,
                EquipmentSlot.OffHand, Armor: armor + index * 25, Evasion: evasion + index * 25,
                Shield: shield + index * 20, SupportLinkCapacity: Math.Min(3, 1 + index / 2),
                RequiredLevel: 1 + index * 15, SourceId: "P24", Tags: tags, SocketLimit: Math.Min(4, 1 + index / 2),
                ImplicitText: key == "quiver" ? "仅能与弓同时装备" : "施法副手，不提供盾牌格挡"));
    }

    private static void AddArmor(ICollection<ItemBaseDefinition> target, string key, ItemCategory category,
        EquipmentSlot slot, IReadOnlyList<string> names, IReadOnlyList<string> tags, int armor, int evasion, int shield)
    {
        for (int index = 0; index < names.Count; index++)
            target.Add(new ItemBaseDefinition($"p24.base.{key}.{index + 1}", names[index], category, slot,
                Armor: armor + index * 20, Evasion: evasion + index * 30, Shield: shield + index * 15,
                SupportLinkCapacity: Math.Min(3, 1 + index / 2), RequiredLevel: 1 + index * 15,
                SourceId: "P24", Tags: tags, SocketLimit: Math.Min(4, 1 + index / 2),
                ImplicitText: "徒手攻击视为未装备武器"));
    }

    private static void AddJewels(ICollection<ItemBaseDefinition> target, string key, IReadOnlyList<string> names,
        IReadOnlyList<string> tags)
    {
        for (int index = 0; index < names.Count; index++)
            target.Add(new ItemBaseDefinition($"p24.base.{key}.{index + 1}", names[index], ItemCategory.Amulet,
                EquipmentSlot.Amulet, RequiredLevel: 1 + index * 20, SourceId: "P24", Tags: tags,
                ImplicitText: "强化唯一灵兽伙伴，不提供独立装备栏"));
    }

    private static void AddTheme(ICollection<P24AffixFamily> target, string theme, string display,
        IReadOnlyList<string> tags, params (string Id, string Name, AffixPosition Position, ItemModifierKind Modifier, string Text)[] entries)
    {
        foreach (var entry in entries)
            target.Add(new P24AffixFamily($"p24.affix.{theme}.{entry.Id}", $"{display}·{entry.Name}", entry.Position,
                entry.Modifier, tags, entry.Text));
    }

    private static IReadOnlyList<ItemCategory> Categories(IReadOnlyList<string> tags)
    {
        var result = new HashSet<ItemCategory>();
        if (tags.Any(tag => tag is "bow" or "projectile")) result.Add(ItemCategory.TwoHandWeapon);
        if (tags.Any(tag => tag is "dagger" or "wand" or "runeblade")) result.Add(ItemCategory.OneHandWeapon);
        if (tags.Any(tag => tag is "quiver" or "focus" or "summoning_focus" or "construct_idol" or "caster")) result.Add(ItemCategory.Shield);
        if (tags.Any(tag => tag is "unarmed" or "wrap")) result.Add(ItemCategory.Gloves);
        if (tags.Any(tag => tag is "beast_talisman" or "companion")) result.Add(ItemCategory.Amulet);
        return result.Count == 0 ? [ItemCategory.Amulet] : result.OrderBy(value => value).ToArray();
    }
}

public sealed record P24ItemMechanicProfile(
    int AdditionalMinionMaximum,
    int AdditionalConstructMaximum,
    int AdditionalTrapMaximum,
    int AdditionalPhantomMaximum,
    int IncreasedAuraEffectBasisPoints,
    int IncreasedEnergyShieldRechargeBasisPoints,
    int IncreasedBossPriorityDamageBasisPoints);

public static class P24ItemRules
{
    public static P24ItemMechanicProfile Resolve(ItemInstance item)
    {
        int Value(string suffix) => item.Affixes.Where(affix => affix.Definition.StableFamilyId.EndsWith(suffix, StringComparison.Ordinal))
            .Sum(affix => affix.EffectiveValue);
        return new P24ItemMechanicProfile(
            Math.Min(10, Value("minion.maximum") / 300),
            Math.Min(5, Value("construct.maximum") / 300),
            Math.Min(5, Value("shadow.trap_limit") / 300),
            Math.Min(5, Value("unarmed.phantom") / 300),
            Value("minion.aura"), Value("spell.recharge"), Value("construct.priority"));
    }
}
