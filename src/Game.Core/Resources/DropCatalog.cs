using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Maps;
using GameForWork.Core.Management;
using GameForWork.Core.Encounters;
using GameForWork.Core.Equipment;

namespace GameForWork.Core.Resources;

public enum BaseTier { Normal = 1, Advanced = 2, High = 3, Pinnacle = 4 }

public sealed record SourceProfile(string StableId, string DisplayName, EnemyFamily Family,
    EnemyRarity Rarity, IReadOnlyList<string> PreferredBaseTags, IReadOnlyList<string> PreferredSkillTags);

public static class DropCatalog
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

    public static SourceProfile Source(EnemyFamily family, EnemyRarity rarity, string bossId = "")
    {
        (string Name, string[] Bases, string[] Skills) entry = Families.TryGetValue(family, out var known)
            ? known : ("异界生物", Array.Empty<string>(), Array.Empty<string>());
        string rarityName = rarity switch { EnemyRarity.Magic => "魔法", EnemyRarity.Rare => "稀有", EnemyRarity.Boss => "首领", _ => "普通" };
        string id = rarity == EnemyRarity.Boss && !string.IsNullOrWhiteSpace(bossId)
            ? $"resources.source.boss.{bossId}" : $"resources.source.{family}.{rarity}".ToLowerInvariant();
        return new(id, rarity == EnemyRarity.Boss && !string.IsNullOrWhiteSpace(bossId) ? $"Boss：{bossId}" : $"{entry.Name}·{rarityName}",
            family, rarity, entry.Bases, entry.Skills);
    }

    public static BaseTier ResolveBaseTier(ItemBaseDefinition item)
    {
        if (item.ItemTags.Contains("warfront", StringComparer.Ordinal))
            return item.RequiredLevel >= 100 ? BaseTier.Pinnacle : item.RequiredLevel >= 85 ? BaseTier.High : BaseTier.Advanced;
        // The imported PoE-style catalog tops out around level 60-80 rather than level 100.
        // Its explicit top-tier identity is authoritative; the fallback bands cover custom
        // jewellery and armour bases which do not carry that imported tag.
        if (item.ItemTags.Contains("top_tier_base_item_type", StringComparer.Ordinal) || item.RequiredLevel >= 70)
            return BaseTier.Pinnacle;
        return item.RequiredLevel switch { >= 55 => BaseTier.High, >= 30 => BaseTier.Advanced, _ => BaseTier.Normal };
    }

    public static string BaseTierName(BaseTier tier) => tier switch
    {
        BaseTier.Normal => "普通底材", BaseTier.Advanced => "进阶底材",
        BaseTier.High => "高阶底材", _ => "巅峰底材",
    };

    public static string SourceDisplay(string stableId) => stableId.StartsWith("resources.source.boss.", StringComparison.Ordinal)
        ? $"Boss：{stableId["resources.source.boss.".Length..]}"
        : stableId.Replace("resources.source.", string.Empty, StringComparison.Ordinal).Replace('.', '·');

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

public static class WarfrontBases
{
    public static IReadOnlyList<ItemBaseDefinition> All { get; } = EquipmentCatalog.Snapshot.Bases
        .Where(value => value.LegacyIds.Any(id => id.StartsWith("resources.base.warfront.", StringComparison.Ordinal)))
        .Select(value => EquipmentCatalog.GetBase(value.Id)).OrderBy(value => value.StableId, StringComparer.Ordinal).ToArray();

    public static IReadOnlyList<ItemBaseDefinition> ForTier(int tier) => All.Where(item => item.RequiredLevel == tier switch { 1 => 70, 2 => 85, _ => 100 }).ToArray();
}

public static class WarfrontRewards
{
    public static ItemInstance Create(int tier, ulong seed, string previousBaseId, string instanceId)
    {
        if (tier is < 1 or > 3) throw new ArgumentOutOfRangeException(nameof(tier));
        ItemBaseDefinition[] pool = WarfrontBases.ForTier(tier).OrderBy(item => item.StableId, StringComparer.Ordinal).ToArray();
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
            { DropSource = $"resources.source.warfront_supply.t{tier}", IsCraftingBase = true };
            int best = item.Affixes.Select(affix => Affixes.TierFor(item.Base, affix.Definition)).DefaultIfEmpty(int.MaxValue).Min();
            if (item.Affixes.Count >= requiredCount && best <= requiredBestTier) return item;
        }
        throw new InvalidOperationException("Unable to generate a legal warfront supply item.");
    }
}

public static class SkillDropCatalog
{
    public static IReadOnlySet<string> For(Mechanic mechanic)
    {
        SkillTag tags = mechanic switch
        {
            Mechanic.Abyss => SkillTag.Attack | SkillTag.Projectile | SkillTag.Void | SkillTag.Chaining,
            Mechanic.Garden => SkillTag.Duration | SkillTag.Guard | SkillTag.Aura | SkillTag.Cold,
            Mechanic.Red => SkillTag.Attack | SkillTag.Bleed | SkillTag.Fire | SkillTag.Duration,
            Mechanic.Blue => SkillTag.Spell | SkillTag.Cold | SkillTag.Lightning | SkillTag.Chaining,
            _ => SkillTag.Attack | SkillTag.Guard | SkillTag.Physical | SkillTag.WarCry,
        };
        return SkillStoneCatalog.DropPool.Where(stone => ((stone.Tags | stone.SupportedTags) & tags) != 0)
            .OrderBy(stone => stone.StableId, StringComparer.Ordinal).Take(10)
            .Select(stone => stone.StableId).ToHashSet(StringComparer.Ordinal);
    }
}
