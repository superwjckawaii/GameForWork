using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P12;
using GameForWork.Core.P2;
using GameForWork.Core.P28;
using GameForWork.Core.Equipment;

namespace GameForWork.Core.P29;

public enum P29BaseTier { Normal = 1, Advanced = 2, High = 3, Pinnacle = 4 }

public sealed record P29SourceProfile(string StableId, string DisplayName, EnemyFamily Family,
    EnemyRarity Rarity, IReadOnlyList<string> PreferredBaseTags, IReadOnlyList<string> PreferredSkillTags);

public static class P29DropCatalog
{
    private static readonly IReadOnlyDictionary<EnemyFamily, (string Name, string[] Bases, string[] Skills)> Families =
        new Dictionary<EnemyFamily, (string, string[], string[])>
        {
            [EnemyFamily.AshenLegion] = ("烬骸军团", ["armor", "axe"], ["attack", "fire"]),
            [EnemyFamily.FrostwildPack] = ("霜原猎群", ["evasion", "bow"], ["projectile", "cold"]),
            [EnemyFamily.DrownedDead] = ("沉墓亡者", ["shield", "mace"], ["minion", "physical"]),
            [EnemyFamily.BloodforgeConstruct] = ("血炉造物", ["armor", "shield"], ["slam", "lightning"]),
            [EnemyFamily.VoidCult] = ("虚空教团", ["wand", "energy_shield"], ["spell", "void"]),
            [EnemyFamily.RiftBeast] = ("裂渊异兽", ["weapon", "evasion"], ["attack", "critical"]),
            [EnemyFamily.LifeGarden] = ("命能孽种", ["life", "belt"], ["life", "spell"]),
            [EnemyFamily.RedOath] = ("赤誓刑团", ["weapon", "ring"], ["attack", "bleed"]),
            [EnemyFamily.BlueOath] = ("苍誓星侍", ["amulet", "energy_shield"], ["spell", "critical"]),
            [EnemyFamily.Warfront] = ("亡旗军阵", ["armor", "jewellery"], ["attack", "defense"]),
            [EnemyFamily.Boss] = ("首领", ["high_base"], ["boss"]),
        };

    public static P29SourceProfile Source(EnemyFamily family, EnemyRarity rarity, string bossId = "")
    {
        (string Name, string[] Bases, string[] Skills) entry = Families.TryGetValue(family, out var known)
            ? known : ("异界生物", Array.Empty<string>(), Array.Empty<string>());
        string rarityName = rarity switch { EnemyRarity.Magic => "魔法", EnemyRarity.Rare => "稀有", EnemyRarity.Boss => "首领", _ => "普通" };
        string id = rarity == EnemyRarity.Boss && !string.IsNullOrWhiteSpace(bossId)
            ? $"p29.source.boss.{bossId}" : $"p29.source.{family}.{rarity}".ToLowerInvariant();
        return new(id, rarity == EnemyRarity.Boss && !string.IsNullOrWhiteSpace(bossId) ? $"Boss：{bossId}" : $"{entry.Name}·{rarityName}",
            family, rarity, entry.Bases, entry.Skills);
    }

    public static P29BaseTier BaseTier(ItemBaseDefinition item)
    {
        if (item.ItemTags.Contains("warfront", StringComparer.Ordinal))
            return item.RequiredLevel >= 100 ? P29BaseTier.Pinnacle : item.RequiredLevel >= 85 ? P29BaseTier.High : P29BaseTier.Advanced;
        // The imported PoE-style catalog tops out around level 60-80 rather than level 100.
        // Its explicit top-tier identity is authoritative; the fallback bands cover custom
        // jewellery and armour bases which do not carry that imported tag.
        if (item.ItemTags.Contains("top_tier_base_item_type", StringComparer.Ordinal) || item.RequiredLevel >= 70)
            return P29BaseTier.Pinnacle;
        return item.RequiredLevel switch { >= 55 => P29BaseTier.High, >= 30 => P29BaseTier.Advanced, _ => P29BaseTier.Normal };
    }

    public static string BaseTierName(P29BaseTier tier) => tier switch
    {
        P29BaseTier.Normal => "普通底材", P29BaseTier.Advanced => "进阶底材",
        P29BaseTier.High => "高阶底材", _ => "巅峰底材",
    };

    public static string SourceDisplay(string stableId) => stableId.StartsWith("p29.source.boss.", StringComparison.Ordinal)
        ? $"Boss：{stableId["p29.source.boss.".Length..]}"
        : stableId.Replace("p29.source.", string.Empty, StringComparison.Ordinal).Replace('.', '·');

    public static bool IsGameplayBiased(ItemInstance item)
    {
        if (string.IsNullOrWhiteSpace(item.DropSource)) return false;
        string source = item.DropSource;
        return source.Contains("Warfront", StringComparison.OrdinalIgnoreCase) && item.Base.Category is ItemCategory.Ring or ItemCategory.Amulet or ItemCategory.Belt ||
            source.Contains("LifeGarden", StringComparison.OrdinalIgnoreCase) && item.Base.Category is ItemCategory.Belt or ItemCategory.BodyArmor ||
            source.Contains("RedOath", StringComparison.OrdinalIgnoreCase) && item.Base.Category is ItemCategory.Ring or ItemCategory.OneHandWeapon or ItemCategory.TwoHandWeapon ||
            source.Contains("BlueOath", StringComparison.OrdinalIgnoreCase) && item.Base.Category is ItemCategory.Amulet or ItemCategory.OneHandWeapon ||
            source.Contains("RiftBeast", StringComparison.OrdinalIgnoreCase) && item.Base.Category is ItemCategory.OneHandWeapon or ItemCategory.TwoHandWeapon;
    }
}

public static class P29WarfrontBases
{
    private static ItemBaseDefinition B(string id, string name, ItemCategory category, int level,
        ItemModifierKind primary, int value, string text, params ItemBaseImplicit[] extra) => new(
        id, name, category, category switch
        {
            ItemCategory.Ring => EquipmentSlot.RingLeft,
            ItemCategory.Amulet => EquipmentSlot.Amulet,
            _ => EquipmentSlot.Belt,
        }, ImplicitModifier: primary, ImplicitMinimumValue: value, ImplicitMaximumValue: value,
        RequiredLevel: level, SourceId: "P29", Tags: ["warfront", "jewellery", category.ToString().ToLowerInvariant(), $"warfront_t{(level == 70 ? 1 : level == 85 ? 2 : 3)}"],
        SocketLimit: 0, ImplicitText: text, AdditionalImplicits: extra);

    public static IReadOnlyList<ItemBaseDefinition> All { get; } =
    [
        B("p29.base.warfront.vanguard_iron_ring", "军锋铁戒", ItemCategory.Ring, 70, ItemModifierKind.AddedMinimumPhysicalDamage, 10, "攻击附加 10–18 物理伤害",
            new ItemBaseImplicit(ItemModifierKind.AddedMaximumPhysicalDamage, 18, "攻击附加物理伤害上限 +18")),
        B("p29.base.warfront.sentry_alloy_ring", "哨戒合金戒", ItemCategory.Ring, 70, ItemModifierKind.FireResistanceBasisPoints, 1_400, "四元素抗性 +12–16%",
            new ItemBaseImplicit(ItemModifierKind.ColdResistanceBasisPoints, 1_400, "寒霜抗性 +14%"), new ItemBaseImplicit(ItemModifierKind.LightningResistanceBasisPoints, 1_400, "闪电抗性 +14%"), new ItemBaseImplicit(ItemModifierKind.VoidResistanceBasisPoints, 1_400, "虚空抗性 +14%")),
        B("p29.base.warfront.vanguard_medal", "前锋章", ItemCategory.Amulet, 70, ItemModifierKind.Physique, 35, "体魄与灵巧 +30–40", new ItemBaseImplicit(ItemModifierKind.Dexterity, 35, "灵巧 +35")),
        B("p29.base.warfront.arcane_medal", "秘仪章", ItemCategory.Amulet, 70, ItemModifierKind.Spirit, 35, "精神与能量 +30–40", new ItemBaseImplicit(ItemModifierKind.Energy, 35, "能量 +35")),
        B("p29.base.warfront.march_girdle", "行军束带", ItemCategory.Belt, 70, ItemModifierKind.FlatMaximumLife, 105, "最大生命 +90–120"),
        B("p29.base.warfront.field_flask_belt", "战地药带", ItemCategory.Belt, 70, ItemModifierKind.IncreasedFlaskChargeGainBasisPoints, 4_250, "药剂充能获取 +35–50%"),

        B("p29.base.warfront.execution_ring", "处刑印戒", ItemCategory.Ring, 85, ItemModifierKind.IncreasedCriticalChanceBasisPoints, 12_000, "全局暴击率 +100–140%"),
        B("p29.base.warfront.iron_curtain_ring", "铁幕指环", ItemCategory.Ring, 85, ItemModifierKind.BlockChanceBasisPoints, 700, "格挡 +6–8%；法术压制 +10–14%", new ItemBaseImplicit(ItemModifierKind.SpellSuppressionBasisPoints, 1_200, "法术压制 +12%")),
        B("p29.base.warfront.quartermaster_insignia", "军械总管徽记", ItemCategory.Amulet, 85, ItemModifierKind.ExtraSupportLinkCapacity, 1, "核心技能辅助连接容量 +1"),
        B("p29.base.warfront.swift_command_insignia", "迅令徽记", ItemCategory.Amulet, 85, ItemModifierKind.IncreasedAttackSpeedBasisPoints, 1_400, "攻击与施法速度 +14%；冷却恢复 +22.5%",
            new ItemBaseImplicit(ItemModifierKind.IncreasedCastSpeedBasisPoints, 1_400, "施法速度 +14%"), new ItemBaseImplicit(ItemModifierKind.IncreasedCooldownRecoveryBasisPoints, 2_250, "技能冷却恢复 +22.5%")),
        B("p29.base.warfront.bastion_waistguard", "壁垒腰铠", ItemCategory.Belt, 85, ItemModifierKind.IncreasedArmorBasisPoints, 4_750, "护甲、闪避与护盾 +40–55%", new ItemBaseImplicit(ItemModifierKind.IncreasedEvasionBasisPoints, 4_750, "闪避 +47.5%"), new ItemBaseImplicit(ItemModifierKind.IncreasedShieldBasisPoints, 4_750, "护盾 +47.5%")),
        B("p29.base.warfront.inexhaustible_warbelt", "不竭军带", ItemCategory.Belt, 85, ItemModifierKind.IncreasedLifeFlaskEffectBasisPoints, 2_750, "药剂效果 +25–30%；充能获取 +50–70%", new ItemBaseImplicit(ItemModifierKind.IncreasedFlaskChargeGainBasisPoints, 6_000, "药剂充能获取 +60%")),

        B("p29.base.warfront.decisive_ring", "决胜印戒", ItemCategory.Ring, 100, ItemModifierKind.MoreRareBossDamageBasisPoints, 2_250, "对稀有与首领敌人造成 20–25% 更多伤害"),
        B("p29.base.warfront.three_oaths_ring", "三誓指环", ItemCategory.Ring, 100, ItemModifierKind.MaximumAllResistanceBasisPoints, 300, "四元素最大抗性 +3%；四元素抗性 +15–20%",
            new ItemBaseImplicit(ItemModifierKind.FireResistanceBasisPoints, 1_750, "火焰抗性 +17.5%"), new ItemBaseImplicit(ItemModifierKind.ColdResistanceBasisPoints, 1_750, "寒霜抗性 +17.5%"), new ItemBaseImplicit(ItemModifierKind.LightningResistanceBasisPoints, 1_750, "闪电抗性 +17.5%"), new ItemBaseImplicit(ItemModifierKind.VoidResistanceBasisPoints, 1_750, "虚空抗性 +17.5%")),
        B("p29.base.warfront.marshal_decree", "元帅敕令", ItemCategory.Amulet, 100, ItemModifierKind.ExtraSupportLinkCapacity, 1, "核心技能容量 +1；辅助连接容量 +1", new ItemBaseImplicit(ItemModifierKind.AdditionalCoreSkillCapacity, 1, "核心技能容量 +1")),
        B("p29.base.warfront.last_banner_emblem", "末旗圣徽", ItemCategory.Amulet, 100, ItemModifierKind.ActiveSkillGemLevels, 2, "已镶嵌主动与辅助技能石等级 +2", new ItemBaseImplicit(ItemModifierKind.SupportSkillGemLevels, 2, "辅助技能石等级 +2")),
        B("p29.base.warfront.war_machine_girdle", "战争机器腰封", ItemCategory.Belt, 100, ItemModifierKind.IncreasedMaximumLifeBasisPoints, 1_350, "最大生命、法力与护盾 +12–15%", new ItemBaseImplicit(ItemModifierKind.IncreasedMaximumManaBasisPoints, 1_350, "最大法力 +13.5%"), new ItemBaseImplicit(ItemModifierKind.IncreasedMaximumShieldBasisPoints, 1_350, "最大护盾 +13.5%")),
        B("p29.base.warfront.perpetual_arsenal", "永续军库", ItemCategory.Belt, 100, ItemModifierKind.IncreasedFlaskChargeGainBasisPoints, 10_000, "药剂充能获取 +100%；效果 +35%；持续时间 20% 更少", new ItemBaseImplicit(ItemModifierKind.IncreasedLifeFlaskEffectBasisPoints, 3_500, "药剂效果 +35%"), new ItemBaseImplicit(ItemModifierKind.IncreasedFlaskDurationBasisPoints, -2_000, "药剂持续时间 20% 更少")),
    ];

    public static IReadOnlyList<ItemBaseDefinition> ForTier(int tier) => All.Where(item => item.RequiredLevel == tier switch { 1 => 70, 2 => 85, _ => 100 }).ToArray();
}

public static class P29WarfrontRewards
{
    public static ItemInstance Create(int tier, ulong seed, string previousBaseId, string instanceId)
    {
        if (tier is < 1 or > 3) throw new ArgumentOutOfRangeException(nameof(tier));
        ItemBaseDefinition[] pool = P29WarfrontBases.ForTier(tier).OrderBy(item => item.StableId, StringComparer.Ordinal).ToArray();
        int category = (int)(seed % 3);
        ItemCategory wanted = category switch { 0 => ItemCategory.Ring, 1 => ItemCategory.Amulet, _ => ItemCategory.Belt };
        ItemBaseDefinition[] pair = pool.Where(item => item.Category == wanted).ToArray();
        ItemBaseDefinition selected = pair[(int)((seed >> 8) % 2)];
        string previous = EquipmentCatalog.ResolveBaseId(previousBaseId);
        if (EquipmentCatalog.ResolveBaseId(selected.StableId) == previous)
            selected = pair.First(item => EquipmentCatalog.ResolveBaseId(item.StableId) != previous);
        int requiredCount = tier switch { 1 => 4, 2 => 5, _ => 6 };
        int requiredBestTier = tier switch { 2 => 3, 3 => 2, _ => int.MaxValue };
        for (int attempt = 0; attempt < 256; attempt++)
        {
            ulong itemSeed = seed + (ulong)attempt * 0x9e3779b97f4a7c15UL;
            ItemInstance item = ItemGenerator.Generate(selected.StableId, selected.RequiredLevel, ItemRarity.Rare, itemSeed, instanceId) with
            { DropSource = $"p29.source.warfront_supply.t{tier}", IsCraftingBase = true };
            int best = item.Affixes.Select(affix => P1Affixes.TierFor(item.Base, affix.Definition)).DefaultIfEmpty(int.MaxValue).Min();
            if (item.Affixes.Count >= requiredCount && best <= requiredBestTier) return item;
        }
        throw new InvalidOperationException("Unable to generate a legal warfront supply item.");
    }
}

public static class P29SkillDropCatalog
{
    public static IReadOnlySet<string> For(P28Mechanic mechanic)
    {
        SkillTag tags = mechanic switch
        {
            P28Mechanic.Abyss => SkillTag.Attack | SkillTag.Projectile | SkillTag.Void | SkillTag.Chaining,
            P28Mechanic.Garden => SkillTag.Duration | SkillTag.Guard | SkillTag.Aura | SkillTag.Cold,
            P28Mechanic.Red => SkillTag.Attack | SkillTag.Bleed | SkillTag.Fire | SkillTag.Duration,
            P28Mechanic.Blue => SkillTag.Spell | SkillTag.Cold | SkillTag.Lightning | SkillTag.Chaining,
            _ => SkillTag.Attack | SkillTag.Guard | SkillTag.Physical | SkillTag.WarCry,
        };
        return P2SkillStones.DropPool.Where(stone => ((stone.Tags | stone.SupportedTags) & tags) != 0)
            .OrderBy(stone => stone.StableId, StringComparer.Ordinal).Take(10)
            .Select(stone => stone.StableId).ToHashSet(StringComparer.Ordinal);
    }
}
