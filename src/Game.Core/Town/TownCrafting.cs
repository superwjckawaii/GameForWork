using System.Security.Cryptography;
using System.Text;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Spatial;
using GameForWork.Core.Skills;
using GameForWork.Core.Simulation;
using GameForWork.Core.Equipment;

namespace GameForWork.Core.Town;

public enum ItemCraftOperation
{
    AwakenMagic,
    AugmentMagic,
    RerollMagic,
    FatefulUpgrade,
    AlchemicalRare,
    RegalUpgrade,
    ChaosReroll,
    ExaltedAdd,
    DissolveAffix,
    Scour,
    DivineReroll,
    BlessedReroll,
    Fracture,
    PolishQuality,
    Corrupt,
}

public sealed record CraftResult(
    bool Succeeded,
    string FailureReason,
    string Summary,
    ItemInstance? Result,
    MetalCurrencyKind Currency,
    int Cost,
    bool Destroyed = false);

public static class ItemCraftingRules
{
    public static CraftResult Preview(ItemInstance item, ItemCraftOperation operation, ulong? seed = null)
    {
        ArgumentNullException.ThrowIfNull(item);
        MetalCurrencyKind currency = CurrencyFor(operation);
        if (item.IsLocked) return Fail("item_locked", "锁定装备不能加工。", currency);
        if (item.IsCorrupted) return Fail("item_corrupted", "腐化装备不能继续加工。", currency);
        ulong resolvedSeed = seed ?? StableSeed(item, operation);
        return operation switch
        {
            ItemCraftOperation.AwakenMagic => ChangeRarity(item, ItemRarity.Basic, ItemRarity.Magic, resolvedSeed, currency, "已将普通装备启灵为魔法装备"),
            ItemCraftOperation.AugmentMagic => AugmentMagic(item, resolvedSeed),
            ItemCraftOperation.RerollMagic => Reroll(item, ItemRarity.Magic, resolvedSeed, currency, "已重铸魔法词缀"),
            ItemCraftOperation.FatefulUpgrade => Fateful(item, resolvedSeed),
            ItemCraftOperation.AlchemicalRare => ChangeRarity(item, ItemRarity.Basic, ItemRarity.Rare, resolvedSeed, currency, "已将普通装备炼成稀有装备"),
            ItemCraftOperation.RegalUpgrade => Regal(item, resolvedSeed),
            ItemCraftOperation.ChaosReroll => Reroll(item, ItemRarity.Rare, resolvedSeed, currency, "已重铸稀有自然词缀"),
            ItemCraftOperation.ExaltedAdd => AddAffix(item, ItemRarity.Rare, resolvedSeed, currency, "已增加一条随机自然词缀"),
            ItemCraftOperation.DissolveAffix => Dissolve(item, resolvedSeed),
            ItemCraftOperation.Scour => Scour(item),
            ItemCraftOperation.DivineReroll => Divine(item, resolvedSeed),
            ItemCraftOperation.BlessedReroll => Blessed(item, resolvedSeed),
            ItemCraftOperation.Fracture => Fracture(item, resolvedSeed),
            ItemCraftOperation.PolishQuality => Polish(item, resolvedSeed),
            ItemCraftOperation.Corrupt => Corrupt(item, resolvedSeed),
            _ => Fail("unknown_operation", "未知金属加工。", currency),
        };
    }

    public static CraftResult Craft(TownEconomyState economy, ItemInstance item, ItemCraftOperation operation, ulong? seed = null)
    {
        ArgumentNullException.ThrowIfNull(economy);
        CraftResult preview = Preview(item, operation, seed);
        if (!preview.Succeeded) return preview;
        if (!economy.TrySpendMetal(preview.Currency, preview.Cost))
            return preview with { Succeeded = false, FailureReason = "insufficient_metal", Summary = $"{MetalCurrencies.Get(preview.Currency).DisplayName}不足。" };
        return preview;
    }

    private static CraftResult AugmentMagic(ItemInstance item, ulong seed)
    {
        if (item.Rarity != ItemRarity.Magic || item.Affixes.Count != 1)
            return Fail("single_magic_affix_required", "添铸锡要求只有一条词缀的魔法装备。", MetalCurrencyKind.AugmentingTin);
        return AddAffix(item, ItemRarity.Magic, seed, MetalCurrencyKind.AugmentingTin, "已为魔法装备增加一条词缀", maximum: 2);
    }

    private static CraftResult ChangeRarity(ItemInstance item, ItemRarity required, ItemRarity target, ulong seed,
        MetalCurrencyKind currency, string summary)
    {
        if (item.Rarity != required) return Fail("rarity_required", $"该加工要求{RarityName(required)}装备。", currency);
        ItemInstance generated = PreserveState(item, ItemGenerator.Generate(item.Base.StableId, item.ItemLevel, target, seed, item.InstanceId));
        return Ok(generated, currency, summary);
    }

    private static CraftResult Reroll(ItemInstance item, ItemRarity required, ulong seed, MetalCurrencyKind currency, string summary)
    {
        if (item.Rarity != required) return Fail("rarity_required", $"该加工要求{RarityName(required)}装备。", currency);
        ItemInstance generated = PreserveState(item, ItemGenerator.Generate(item.Base.StableId, item.ItemLevel, required, seed, item.InstanceId));
        AffixRoll[] protectedAffixes = item.Affixes.Where(affix => affix.Crafted || item.IsFractured(affix)).ToArray();
        int maximum = required == ItemRarity.Magic ? 2 : 6;
        AffixRoll[] random = generated.Affixes.Where(affix => protectedAffixes.All(existing =>
            existing.Definition.StableFamilyId != affix.Definition.StableFamilyId)).Take(Math.Max(0, maximum - protectedAffixes.Length)).ToArray();
        return Ok(generated with { Affixes = protectedAffixes.Concat(random).ToArray() }, currency, summary);
    }

    private static CraftResult Fateful(ItemInstance item, ulong seed)
    {
        if (item.Rarity != ItemRarity.Basic) return Fail("basic_required", "命铸金只能用于普通装备。", MetalCurrencyKind.FatefulGold);
        var random = new Pcg32(seed);
        int roll = random.NextBasisPoints();
        if (roll < 10 && item.Base.StableId == "core.base.heavy_battleaxe")
        {
            ItemInstance legendary = Legendary.Create(item.ItemLevel) with
            {
                InstanceId = item.InstanceId,
                LinkedSocketCount = item.LinkedSocketCount,
                Quality = item.Quality,
            };
            return Ok(legendary, MetalCurrencyKind.FatefulGold, "命铸成功：装备蜕变为传奇");
        }
        ItemRarity rarity = roll < 2_000 ? ItemRarity.Rare : ItemRarity.Magic;
        ItemInstance generated = PreserveState(item, ItemGenerator.Generate(item.Base.StableId, item.ItemLevel, rarity, seed, item.InstanceId));
        return Ok(generated, MetalCurrencyKind.FatefulGold, $"命铸结果：{RarityName(rarity)}装备");
    }

    private static CraftResult Regal(ItemInstance item, ulong seed)
    {
        if (item.Rarity != ItemRarity.Magic) return Fail("magic_required", "王铸金只能用于魔法装备。", MetalCurrencyKind.RegalGold);
        ItemInstance rare = item with { Rarity = ItemRarity.Rare };
        AffixRoll? extra = ItemGenerator.RollAdditionalAffix(rare, seed);
        return extra is null
            ? Fail("no_affix_available", "没有可添加的词缀。", MetalCurrencyKind.RegalGold)
            : Ok(rare with { Affixes = rare.Affixes.Append(extra).ToArray() }, MetalCurrencyKind.RegalGold, "已晋升稀有并增加一条词缀");
    }

    private static CraftResult AddAffix(ItemInstance item, ItemRarity required, ulong seed,
        MetalCurrencyKind currency, string summary, int maximum = 6)
    {
        if (item.Rarity != required) return Fail("rarity_required", $"该加工要求{RarityName(required)}装备。", currency);
        if (item.Affixes.Count >= maximum) return Fail("affixes_full", "装备词缀已经达到上限。", currency);
        AffixRoll? extra = ItemGenerator.RollAdditionalAffix(item, seed);
        return extra is null ? Fail("no_affix_available", "没有可添加的词缀。", currency)
            : Ok(item with { Affixes = item.Affixes.Append(extra).ToArray() }, currency, summary);
    }

    private static CraftResult Dissolve(ItemInstance item, ulong seed)
    {
        AffixRoll[] removable = item.Affixes.Where(affix => !item.IsFractured(affix)).ToArray();
        if (removable.Length == 0) return Fail("no_mutable_affix", "没有可消解的显式词缀。", MetalCurrencyKind.DissolutionSilver);
        var random = new Pcg32(seed);
        AffixRoll removed = removable[(int)(random.NextUInt() % (uint)removable.Length)];
        bool skipped = false;
        AffixRoll[] retained = item.Affixes.Where(affix =>
        {
            if (!skipped && ReferenceEquals(affix, removed)) { skipped = true; return false; }
            return true;
        }).ToArray();
        return Ok(item with { Affixes = retained }, MetalCurrencyKind.DissolutionSilver, $"已随机移除：{removed.Definition.DisplayName}");
    }

    private static CraftResult Scour(ItemInstance item)
    {
        AffixRoll[] fractured = item.Affixes.Where(item.IsFractured).ToArray();
        ItemRarity rarity = fractured.Length == 0 ? ItemRarity.Basic : ItemRarity.Rare;
        return Ok(item with { Affixes = fractured, Rarity = rarity }, MetalCurrencyKind.ScouringLead,
            fractured.Length == 0 ? "已洗炼为普通装备" : "已移除可变词缀；破溃词缀保留");
    }

    private static CraftResult Divine(ItemInstance item, ulong seed)
    {
        AffixRoll[] mutable = item.Affixes.Where(affix => !affix.Crafted && !item.IsFractured(affix)).ToArray();
        if (mutable.Length == 0) return Fail("no_mutable_affix", "没有可重掷的自然词缀。", MetalCurrencyKind.DivineSilver);
        var random = new Pcg32(seed);
        AffixRoll[] rerolled = item.Affixes.Select(affix => affix.Crafted || item.IsFractured(affix)
            ? affix : RollAffix(affix.Definition, random)).ToArray();
        return Ok(item with { Affixes = rerolled }, MetalCurrencyKind.DivineSilver, "已重掷所有可变自然词缀数值");
    }

    private static CraftResult Blessed(ItemInstance item, ulong seed)
    {
        if (item.Base.ImplicitModifier == ItemModifierKind.None || item.Base.ImplicitMaximumValue <= item.Base.ImplicitMinimumValue)
            return Fail("implicit_unavailable", "该底材没有可重掷的固有词缀。", MetalCurrencyKind.BlessedSilver);
        var random = new Pcg32(seed);
        int value = RollInclusive(random, item.Base.ImplicitMinimumValue, item.Base.ImplicitMaximumValue);
        return Ok(item with { ImplicitValue = value }, MetalCurrencyKind.BlessedSilver, $"固有词缀数值重掷为 {value}");
    }

    private static CraftResult Fracture(ItemInstance item, ulong seed)
    {
        AffixRoll[] natural = item.Affixes.Where(affix => !affix.Crafted).ToArray();
        if (!string.IsNullOrEmpty(item.FracturedAffixFamilyId)) return Fail("already_fractured", "该装备已经拥有破溃词缀。", MetalCurrencyKind.FractureSteel);
        if (natural.Length < 4) return Fail("four_natural_affixes_required", "破溃钢要求至少四条自然词缀。", MetalCurrencyKind.FractureSteel);
        var random = new Pcg32(seed);
        AffixRoll selected = natural[(int)(random.NextUInt() % (uint)natural.Length)];
        return Ok(item with { FracturedAffixFamilyId = selected.Definition.StableFamilyId }, MetalCurrencyKind.FractureSteel,
            $"随机固化：{selected.Definition.DisplayName}");
    }

    private static CraftResult Polish(ItemInstance item, ulong seed)
    {
        if (item.Base.Category is ItemCategory.Ring or ItemCategory.Amulet or ItemCategory.Belt)
            return Fail("quality_category_invalid", "首饰不能使用精磨钴。", MetalCurrencyKind.PolishingCobalt);
        if (item.Quality >= 20) return Fail("quality_maximum", "装备品质已经达到 20%。", MetalCurrencyKind.PolishingCobalt);
        var random = new Pcg32(seed);
        int gain = 1 + (int)(random.NextUInt() % 5);
        int result = Math.Min(20, item.Quality + gain);
        return Ok(item with { Quality = result }, MetalCurrencyKind.PolishingCobalt, $"品质 {item.Quality}% → {result}%");
    }

    private static CraftResult Corrupt(ItemInstance item, ulong seed)
    {
        var random = new Pcg32(seed);
        int roll = random.NextBasisPoints();
        if (roll < 4_000)
        {
            int quality = Math.Min(20, item.Quality + 5);
            return Ok(item with { IsCorrupted = true, CorruptionOutcome = "empowered", Quality = quality },
                MetalCurrencyKind.CorruptionIron, $"强力腐化：品质提升至 {quality}%");
        }
        if (roll < 7_000)
            return Ok(item with { IsCorrupted = true, CorruptionOutcome = "sealed" }, MetalCurrencyKind.CorruptionIron, "腐化锁定：装备没有额外变化");
        if (roll < 9_000)
        {
            AffixRoll[] affixes = item.Affixes.Take(Math.Max(0, item.Affixes.Count - 1)).ToArray();
            return Ok(item with { IsCorrupted = true, CorruptionOutcome = "scarred", Quality = 0, Affixes = affixes },
                MetalCurrencyKind.CorruptionIron, "负面腐化：品质归零并失去一条词缀");
        }
        return new CraftResult(true, string.Empty, "腐化失控：装备被彻底摧毁", null,
            MetalCurrencyKind.CorruptionIron, 1, Destroyed: true);
    }

    private static ItemInstance PreserveState(ItemInstance original, ItemInstance generated) => generated with
    {
        IsLocked = original.IsLocked,
        IsCraftingBase = original.IsCraftingBase,
        LinkedSocketCount = original.LinkedSocketCount,
        Quality = original.Quality,
        Enchantment = original.Enchantment,
        IsCorrupted = original.IsCorrupted,
        CorruptionOutcome = original.CorruptionOutcome,
        FracturedAffixFamilyId = original.FracturedAffixFamilyId,
    };

    private static MetalCurrencyKind CurrencyFor(ItemCraftOperation operation) => operation switch
    {
        ItemCraftOperation.AwakenMagic => MetalCurrencyKind.AwakeningCopper,
        ItemCraftOperation.AugmentMagic => MetalCurrencyKind.AugmentingTin,
        ItemCraftOperation.RerollMagic => MetalCurrencyKind.MutableMercury,
        ItemCraftOperation.FatefulUpgrade => MetalCurrencyKind.FatefulGold,
        ItemCraftOperation.AlchemicalRare => MetalCurrencyKind.AlchemicalGold,
        ItemCraftOperation.RegalUpgrade => MetalCurrencyKind.RegalGold,
        ItemCraftOperation.ChaosReroll => MetalCurrencyKind.ChaosGold,
        ItemCraftOperation.ExaltedAdd => MetalCurrencyKind.ExaltedGold,
        ItemCraftOperation.DissolveAffix => MetalCurrencyKind.DissolutionSilver,
        ItemCraftOperation.Scour => MetalCurrencyKind.ScouringLead,
        ItemCraftOperation.DivineReroll => MetalCurrencyKind.DivineSilver,
        ItemCraftOperation.BlessedReroll => MetalCurrencyKind.BlessedSilver,
        ItemCraftOperation.Fracture => MetalCurrencyKind.FractureSteel,
        ItemCraftOperation.PolishQuality => MetalCurrencyKind.PolishingCobalt,
        ItemCraftOperation.Corrupt => MetalCurrencyKind.CorruptionIron,
        _ => throw new ArgumentOutOfRangeException(nameof(operation)),
    };

    private static CraftResult Ok(ItemInstance result, MetalCurrencyKind currency, string summary) =>
        new(true, string.Empty, summary, result, currency, 1);

    private static CraftResult Fail(string reason, string summary, MetalCurrencyKind currency) =>
        new(false, reason, summary, null, currency, 0);

    private static string RarityName(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Basic => "普通",
        ItemRarity.Magic => "魔法",
        ItemRarity.Rare => "稀有",
        ItemRarity.Legendary => "传奇",
        _ => rarity.ToString(),
    };

    private static int RollInclusive(Pcg32 random, int minimum, int maximum) => minimum == maximum
        ? minimum : minimum + (int)(random.NextUInt() % (uint)(maximum - minimum + 1));

    private static AffixRoll RollAffix(AffixDefinition definition, Pcg32 random)
    {
        RolledAffixComponent[] components = definition.EffectComponents.Select(component =>
            new RolledAffixComponent(component.Kind, RollInclusive(random, component.MinimumValue, component.MaximumValue),
                component.Scope, component.DisplayText)).ToArray();
        return new AffixRoll(definition, components[0].Value, Components: components);
    }

    private static ulong StableSeed(ItemInstance item, ItemCraftOperation operation)
    {
        string source = $"{item.InstanceId}|{operation}|{item.Rarity}|{item.Quality}|{item.ImplicitValue}|" +
                        string.Join(';', item.Affixes.Select(affix => $"{affix.Definition.StableFamilyId}:{string.Join(',', affix.Effects.Select(effect => effect.Value))}"));
        return BitConverter.ToUInt64(SHA256.HashData(Encoding.UTF8.GetBytes(source)), 0);
    }
}

public static class EnchantmentCatalog
{
    public static IReadOnlyList<ItemEnchantment> All => EquipmentEnchantmentCatalog.All;

    public static ItemEnchantment Get(string stableId) => EquipmentEnchantmentCatalog.Get(stableId);

    public static CraftResult Preview(ItemInstance item, string stableId, int workshopLevel)
    {
        ItemEnchantment enchantment = Get(stableId);
        if (item.IsLocked || item.IsCorrupted) return new(false, "item_locked", "锁定或腐化装备不能附魔。", item, MetalCurrencyKind.TemperingIron, 0);
        if (workshopLevel < enchantment.WorkshopLevel) return new(false, "workshop_level", $"需要工坊 Lv.{enchantment.WorkshopLevel}。", item, MetalCurrencyKind.TemperingIron, 0);
        if (!EquipmentEnchantmentCatalog.Supports(enchantment, item.Base)) return new(false, "incompatible_base", "该附魔不支持此装备类型。", item, MetalCurrencyKind.TemperingIron, 0);
        if (enchantment.DisplayName == "完美链印" && (!SocketRules.ProvidesSockets(item.Base.Category) || item.LinkedSocketCount >= SocketRules.Maximum(item.Base.Category, item.ItemLevel)))
            return new(false, "maximum_links", "该装备没有可提升的连接容量。", item, MetalCurrencyKind.TemperingIron, 0);
        return new(true, string.Empty, $"附魔：{enchantment.DisplayName}（覆盖现有附魔）", item with { Enchantment = enchantment }, MetalCurrencyKind.TemperingIron, 0);
    }

    public static CraftResult Craft(TownEconomyState economy, ItemInstance item, string stableId, int workshopLevel)
    {
        CraftResult preview = Preview(item, stableId, workshopLevel);
        if (!preview.Succeeded) return preview;
        ItemEnchantment enchantment = Get(stableId);
        return economy.TrySpendGold(enchantment.GoldCost) ? preview
            : preview with { Succeeded = false, FailureReason = "insufficient_gold", Summary = $"需要 {enchantment.GoldCost} 金币。" };
    }
}
