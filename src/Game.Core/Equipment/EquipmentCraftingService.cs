using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P9;
using GameForWork.Core.Simulation;

namespace GameForWork.Core.Equipment;

public sealed class EquipmentCraftingWallet
{
    private readonly Dictionary<string, int> _balances = new(StringComparer.Ordinal);

    public int this[string resource] => _balances.GetValueOrDefault(resource);
    public void Credit(string resource, int amount) => _balances[resource] = checked(this[resource] + Math.Max(0, amount));
    public bool CanSpend(string resource, int amount) => amount >= 0 && this[resource] >= amount;
    internal void Spend(string resource, int amount) => _balances[resource] = checked(this[resource] - amount);
}

public sealed record EquipmentCraftingRequest(
    string OperationId,
    string SelectedDefinitionId = "",
    string SelectedAffixFamilyId = "",
    int WorkshopLevel = 4,
    ulong? Seed = null);

public sealed record EquipmentCraftingPreview(
    bool Available,
    string FailureReason,
    string Summary,
    string Resource,
    int Cost,
    IReadOnlyList<string> PossibleOutcomes);

public sealed record EquipmentCraftingResult(
    bool Succeeded,
    string FailureReason,
    string Summary,
    ItemInstance? Item,
    string Resource,
    int Cost,
    bool Destroyed = false);

/// <summary>One atomic executor for all 92 catalogued equipment operations.</summary>
public static class EquipmentCraftingService
{
    public static EquipmentCraftingPreview Preview(ItemInstance item, EquipmentCraftingRequest request)
    {
        ArgumentNullException.ThrowIfNull(item);
        EquipmentCraftingOperationEntry operation = Get(request.OperationId);
        (string resource, int cost) = ParseCost(operation.CostText, item);
        string failure = ValidateCommon(item, operation);
        if (failure.Length == 0) failure = ValidateRequest(item, operation, request);
        return failure.Length > 0
            ? new(false, failure, FailureText(failure), resource, cost, [])
            : new(true, string.Empty, operation.RuleText, resource, cost,
                [operation.RuleText, "具体随机分支、词缀和掷值仅在确认执行后生成"]);
    }

    public static EquipmentCraftingResult Execute(EquipmentCraftingWallet wallet, ItemInstance item, EquipmentCraftingRequest request)
    {
        ArgumentNullException.ThrowIfNull(wallet);
        EquipmentCraftingPreview preview = Preview(item, request);
        if (!preview.Available) return Fail(preview.FailureReason, preview.Summary, preview.Resource, preview.Cost);
        if (!wallet.CanSpend(preview.Resource, preview.Cost)) return Fail("insufficient_resource", $"{preview.Resource}不足。", preview.Resource, preview.Cost);

        EquipmentCraftingOperationEntry operation = Get(request.OperationId);
        ulong seed = request.Seed ?? StableSeed(item, operation.Id);
        EquipmentCraftingResult applied = Apply(item, operation, request, seed, preview.Resource, preview.Cost);
        if (!applied.Succeeded) return applied;
        wallet.Spend(preview.Resource, preview.Cost);
        return applied;
    }

    private static EquipmentCraftingResult Apply(ItemInstance item, EquipmentCraftingOperationEntry operation,
        EquipmentCraftingRequest request, ulong seed, string resource, int cost)
    {
        if (operation.Kind == "Enchantment") return ApplyEnchantment(item, request, resource, cost);
        if (operation.Kind == "LegendaryExchange") return ApplyLegendary(request, seed, resource, cost);
        if (operation.Kind == "Oath") return ApplyOath(item, operation, request, seed, resource, cost);
        if (operation.Kind == "LifeEnergy") return ApplyLifeEnergy(item, operation, seed, resource, cost);
        return ApplyMetal(item, operation, seed, resource, cost);
    }

    private static EquipmentCraftingResult ApplyMetal(ItemInstance item, EquipmentCraftingOperationEntry operation,
        ulong seed, string resource, int cost)
    {
        if (operation.DisplayName is "淬刃打造" or "守壁打造" or "活血打造")
            return AddCraftedAffix(item, operation, resource, cost);

        P9CraftOperation? legacy = operation.DisplayName switch
        {
            "启灵" => P9CraftOperation.AwakenMagic, "添铸" => P9CraftOperation.AugmentMagic,
            "易变重铸" => P9CraftOperation.RerollMagic, "命铸" => P9CraftOperation.FatefulUpgrade,
            "炼真" => P9CraftOperation.AlchemicalRare, "王铸" => P9CraftOperation.RegalUpgrade,
            "混沌重铸" => P9CraftOperation.ChaosReroll, "崇高增附" => P9CraftOperation.ExaltedAdd,
            "消解" => P9CraftOperation.DissolveAffix, "洗炼" => P9CraftOperation.Scour,
            "神铸重掷" => P9CraftOperation.DivineReroll, "祝铸重掷" => P9CraftOperation.BlessedReroll,
            "破裂" => P9CraftOperation.Fracture, "精磨品质" => P9CraftOperation.PolishQuality,
            "赤蚀腐化" => P9CraftOperation.Corrupt,
            _ => null,
        };
        if (legacy is not null)
        {
            if (legacy == P9CraftOperation.FatefulUpgrade) return Fateful(item, seed, resource, cost);
            if (legacy == P9CraftOperation.Corrupt) return Corrupt(item, seed, resource, cost);
            P9CraftResult result = P9CraftingRules.Preview(item, legacy.Value, seed);
            if (!result.Succeeded) return Fail(result.FailureReason, result.Summary, resource, cost);
            ItemInstance? protectedResult = result.Result is null ? null : ApplyProtections(item, result.Result, ChangesExplicitAffixes(legacy.Value));
            return new(true, string.Empty, result.Summary, protectedResult, resource, cost, result.Destroyed);
        }

        if (operation.DisplayName == "连接重铸") return RerollLinks(item, seed, resource, cost);
        if (operation.DisplayName == "稳固增连")
        {
            if (item.Base.SocketLimit <= 0) return Fail("no_sockets", "此底材没有连接孔。", resource, cost);
            if (item.LinkedSocketCount >= item.Base.SocketLimit) return Fail("links_full", "连接数已经达到底材上限。", resource, cost);
            return Ok(item with { LinkedSocketCount = item.LinkedSocketCount + 1, CraftSequence = item.CraftSequence + 1 }, operation.DisplayName, resource, cost);
        }
        return Fail("operation_not_implemented", $"未实现做装操作：{operation.DisplayName}", resource, cost);
    }

    private static EquipmentCraftingResult ApplyLifeEnergy(ItemInstance item, EquipmentCraftingOperationEntry operation,
        ulong seed, string resource, int cost)
    {
        if (item.Rarity != ItemRarity.Rare) return Fail("rare_required", "命能加工要求稀有装备。", resource, cost);
        if (operation.DisplayName.StartsWith("保留前缀", StringComparison.Ordinal)) return RerollHalf(item, AffixPosition.Prefix, seed, resource, cost);
        if (operation.DisplayName.StartsWith("保留后缀", StringComparison.Ordinal)) return RerollHalf(item, AffixPosition.Suffix, seed, resource, cost);
        string tag = operation.DisplayName.Replace("偏向重铸", string.Empty, StringComparison.Ordinal)
            .Replace("偏向打造", string.Empty, StringComparison.Ordinal).Trim();
        return operation.DisplayName.Contains("偏向重铸", StringComparison.Ordinal)
            ? BiasedReroll(item, tag, seed, resource, cost)
            : BiasedReplace(item, tag, seed, resource, cost);
    }

    private static EquipmentCraftingResult ApplyOath(ItemInstance item, EquipmentCraftingOperationEntry operation,
        EquipmentCraftingRequest request, ulong seed, string resource, int cost)
    {
        if (operation.DisplayName == "赤誓保护")
            return item.PrefixCount == 0 ? Fail("prefix_required", "装备没有可保护的前缀。", resource, cost)
                : Ok(item with { ProtectPrefixesNextCraft = true, CraftSequence = item.CraftSequence + 1 }, operation.DisplayName, resource, cost);
        if (operation.DisplayName == "苍誓保护")
            return item.SuffixCount == 0 ? Fail("suffix_required", "装备没有可保护的后缀。", resource, cost)
                : Ok(item with { ProtectSuffixesNextCraft = true, CraftSequence = item.CraftSequence + 1 }, operation.DisplayName, resource, cost);
        if (operation.DisplayName.Contains("品质", StringComparison.Ordinal))
        {
            int quality = 20 + (int)(new Pcg32(seed).NextUInt() % 21);
            return Ok(item with { Quality = quality, CraftSequence = item.CraftSequence + 1 }, $"品质重置为 {quality}%", resource, cost);
        }
        return ShiftTier(item, request.SelectedAffixFamilyId, seed, resource, cost);
    }

    private static EquipmentCraftingResult ApplyEnchantment(ItemInstance item, EquipmentCraftingRequest request,
        string resource, int cost)
    {
        if (string.IsNullOrWhiteSpace(request.SelectedDefinitionId)) return Fail("enchantment_required", "请选择附魔。", resource, cost);
        ItemEnchantment enchantment;
        try { enchantment = EquipmentEnchantmentCatalog.Get(request.SelectedDefinitionId); }
        catch (InvalidOperationException) { return Fail("unknown_enchantment", "未知附魔。", resource, cost); }
        if (request.WorkshopLevel < enchantment.WorkshopLevel) return Fail("workshop_level", $"需要工坊 Lv.{enchantment.WorkshopLevel}。", resource, cost);
        if (!EquipmentEnchantmentCatalog.Supports(enchantment, item.Base)) return Fail("incompatible_base", "该附魔不适用于此装备。", resource, cost);
        return Ok(item with { Enchantment = enchantment, CraftSequence = item.CraftSequence + 1 }, $"附魔：{enchantment.DisplayName}", resource, cost);
    }

    private static EquipmentCraftingResult ApplyLegendary(EquipmentCraftingRequest request, ulong seed, string resource, int cost)
    {
        EquipmentLegendaryEntry? entry = EquipmentCatalog.LegendaryItems.FirstOrDefault(value => value.Id == request.SelectedDefinitionId && value.Rarity == "Legendary");
        if (entry is null) return Fail("exchange_target_invalid", "请选择可兑换的普通传奇。", resource, cost);
        ItemInstance result = EquipmentLegendaryFactory.Create(entry.Id, 100, $"legendary-exchange-{seed:x16}", seed);
        return Ok(result with { CraftSequence = 1 }, $"已兑换：{entry.DisplayName}", resource, cost);
    }

    private static EquipmentCraftingResult AddCraftedAffix(ItemInstance item, EquipmentCraftingOperationEntry operation, string resource, int cost)
    {
        if (item.Rarity == ItemRarity.Legendary) return Fail("legendary_forbidden", "传奇装备不能添加打造词缀。", resource, cost);
        AffixPosition position = AffixPosition.Prefix;
        if (item.PrefixCount >= 3) return Fail("prefix_full", "前缀已满。", resource, cost);
        (ItemModifierKind kind, int value, ItemModifierScope scope) = operation.DisplayName switch
        {
            "淬刃打造" => (ItemModifierKind.IncreasedPhysicalDamageBasisPoints, 3_500, ItemModifierScope.LocalWeapon),
            "守壁打造" => (ItemModifierKind.IncreasedArmorBasisPoints, 3_000, ItemModifierScope.LocalDefense),
            _ => (ItemModifierKind.FlatMaximumLife, 40, ItemModifierScope.Global),
        };
        AffixRoll[] retained = item.Affixes.Where(affix => !affix.Crafted).ToArray();
        var definition = new AffixDefinition($"equipment.crafted.{operation.Id.Split('.').Last()}", operation.DisplayName,
            item.Base.Category, position, 0, 1, value, value, 0, kind, Source: "Crafted",
            Components: [new AffixModifierComponent(kind, value, value, scope, operation.RuleText)]);
        RolledAffixComponent[] effects = definition.EffectComponents.Select(component =>
            new RolledAffixComponent(component.Kind, component.MinimumValue, component.Scope, component.DisplayText)).ToArray();
        ItemInstance changed = item with { Affixes = retained.Append(new AffixRoll(definition, value, true, effects)).ToArray() };
        return Ok(ApplyProtections(item, changed, true), operation.DisplayName, resource, cost);
    }

    private static EquipmentCraftingResult RerollLinks(ItemInstance item, ulong seed, string resource, int cost)
    {
        int maximum = item.Base.SocketLimit;
        if (maximum <= 0) return Fail("no_sockets", "此底材没有连接孔。", resource, cost);
        var random = new Pcg32(seed);
        int fullChance = maximum switch { 3 => 3_000, 4 => 1_500, 5 => 500, 6 => 100, _ => 10_000 };
        int roll = random.NextBasisPoints();
        int links = roll < fullChance ? maximum : 1 + WeightedLowLink(random, maximum - 1);
        return Ok(item with { LinkedSocketCount = links, CraftSequence = item.CraftSequence + 1 }, $"连接重铸为 {links}", resource, cost);
    }

    private static int WeightedLowLink(Pcg32 random, int outcomes)
    {
        int total = outcomes * (outcomes + 1) / 2;
        int roll = (int)(random.NextUInt() % (uint)total);
        for (int link = 0; link < outcomes; link++)
        {
            int weight = outcomes - link;
            if (roll < weight) return link;
            roll -= weight;
        }
        return outcomes - 1;
    }

    private static EquipmentCraftingResult Fateful(ItemInstance item, ulong seed, string resource, int cost)
    {
        if (item.Rarity != ItemRarity.Basic) return Fail("basic_required", "命铸只能用于普通装备。", resource, cost);
        var random = new Pcg32(seed);
        int roll = random.NextBasisPoints();
        EquipmentLegendaryEntry[] matching = EquipmentLegendaryFactory.ForBase(item.Base).ToArray();
        if (roll < 10 && matching.Length > 0)
        {
            EquipmentLegendaryEntry entry = matching[(int)(random.NextUInt() % (uint)matching.Length)];
            return Ok(EquipmentLegendaryFactory.Create(entry.Id, item.ItemLevel, item.InstanceId, seed), "命铸为传奇", resource, cost);
        }
        ItemRarity rarity = roll < 2_000 ? ItemRarity.Rare : ItemRarity.Magic;
        ItemInstance generated = ItemGenerator.Generate(item.Base.StableId, item.ItemLevel, rarity, seed, item.InstanceId);
        return Ok(CopyPersistent(item, generated), $"命铸为{rarity}", resource, cost);
    }

    private static EquipmentCraftingResult Corrupt(ItemInstance item, ulong seed, string resource, int cost)
    {
        var random = new Pcg32(seed);
        int roll = random.NextBasisPoints();
        ItemInstance cleared = item with { ProtectPrefixesNextCraft = false, ProtectSuffixesNextCraft = false };
        if (roll < 3_500)
        {
            EquipmentCorruptionImplicitEntry[] candidates = EquipmentCatalog.CorruptionImplicits.Where(value => EquipmentCorruptionCatalog.Supports(value, item.Base)).ToArray();
            if (candidates.Length == 0) return Fail("no_corruption_candidate", "此底材没有合法腐化词缀。", resource, cost);
            EquipmentCorruptionImplicitEntry selected = candidates[(int)(random.NextUInt() % (uint)candidates.Length)];
            return Ok(cleared with
            {
                IsCorrupted = true,
                CorruptionOutcome = "implicit",
                CorruptionImplicitId = selected.Id,
                RolledCorruptionComponents = EquipmentCorruptionCatalog.Roll(selected, random),
                CraftSequence = item.CraftSequence + 1,
            }, $"腐化：{selected.DisplayName}", resource, cost);
        }
        if (roll < 6_000) return Ok(cleared with { IsCorrupted = true, CorruptionOutcome = "sealed", CraftSequence = item.CraftSequence + 1 }, "腐化锁定", resource, cost);
        if (roll < 8_000)
        {
            int qualityGain = 5 + (int)(random.NextUInt() % 6);
            return Ok(cleared with { IsCorrupted = true, Quality = Math.Min(30, item.Quality + qualityGain), CorruptionOutcome = "quality", CraftSequence = item.CraftSequence + 1 }, "腐化品质", resource, cost);
        }
        if (roll < 9_000)
        {
            AffixRoll[] removable = item.Affixes.Where(value => !item.IsFractured(value)).ToArray();
            AffixRoll[] kept = removable.Length == 0 ? item.Affixes.ToArray() : item.Affixes.Where(value => !ReferenceEquals(value, removable[(int)(random.NextUInt() % (uint)removable.Length)])).ToArray();
            return Ok(cleared with { IsCorrupted = true, Quality = 0, Affixes = kept, CorruptionOutcome = "scarred", CraftSequence = item.CraftSequence + 1 }, "腐化损伤", resource, cost);
        }
        return new(true, string.Empty, "腐化失控：装备被摧毁", null, resource, cost, true);
    }

    private static EquipmentCraftingResult RerollHalf(ItemInstance item, AffixPosition preserved, ulong seed, string resource, int cost)
    {
        AffixRoll[] keep = item.Affixes.Where(affix => affix.Definition.Position == preserved || affix.Crafted || item.IsFractured(affix)).ToArray();
        ItemInstance generated = ItemGenerator.Generate(item.Base.StableId, item.ItemLevel, ItemRarity.Rare, seed, item.InstanceId);
        AffixRoll[] fill = generated.Affixes.Where(affix => affix.Definition.Position != preserved && keep.All(existing => existing.Definition.MutualExclusionGroup != affix.Definition.MutualExclusionGroup)).Take(Math.Max(0, 6 - keep.Length)).ToArray();
        ItemInstance changed = CopyPersistent(item, generated) with { Affixes = keep.Concat(fill).ToArray() };
        return Ok(ApplyProtections(item, changed, true), "已完成保留半边重铸", resource, cost);
    }

    private static EquipmentCraftingResult BiasedReroll(ItemInstance item, string tag, ulong seed, string resource, int cost)
    {
        ItemInstance generated = ItemGenerator.Generate(item.Base.StableId, item.ItemLevel, ItemRarity.Rare, seed, item.InstanceId);
        AffixDefinition[] targets = P1Affixes.For(item.Base, item.ItemLevel).Where(value => HasDirection(value, tag)).ToArray();
        if (targets.Length == 0) return Fail("no_direction_candidate", $"没有合法{tag}词缀。", resource, cost);
        var random = new Pcg32(seed ^ 0x9e3779b97f4a7c15UL);
        AffixDefinition target = targets[(int)(random.NextUInt() % (uint)targets.Length)];
        AffixRoll rolled = Roll(target, random);
        AffixRoll[] affixes = generated.Affixes.Where(value => value.Definition.Position != target.Position).Take(3).Append(rolled)
            .Concat(generated.Affixes.Where(value => value.Definition.Position == target.Position).Take(2)).Take(6).ToArray();
        ItemInstance changed = CopyPersistent(item, generated) with { Affixes = affixes };
        return Ok(ApplyProtections(item, changed, true), $"已完成{tag}偏向重铸", resource, cost);
    }

    private static EquipmentCraftingResult BiasedReplace(ItemInstance item, string tag, ulong seed, string resource, int cost)
    {
        AffixDefinition[] candidates = P1Affixes.For(item.Base, item.ItemLevel).Where(value => HasDirection(value, tag)).ToArray();
        if (candidates.Length == 0) return Fail("no_direction_candidate", $"没有合法{tag}词缀。", resource, cost);
        AffixRoll[] removable = item.Affixes.Where(value => !value.Crafted && !item.IsFractured(value) && !HasDirection(value.Definition, tag)).ToArray();
        if (removable.Length == 0) return Fail("no_replaceable_affix", "没有可替换的非目标自然词缀。", resource, cost);
        var random = new Pcg32(seed);
        AffixRoll removed = removable[(int)(random.NextUInt() % (uint)removable.Length)];
        AffixDefinition[] samePosition = candidates.Where(value => value.Position == removed.Definition.Position && item.Affixes.All(existing => existing.Definition.MutualExclusionGroup != value.MutualExclusionGroup)).ToArray();
        if (samePosition.Length == 0) return Fail("no_same_position_candidate", "同位置没有合法目标词缀。", resource, cost);
        AffixRoll replacement = Roll(samePosition[(int)(random.NextUInt() % (uint)samePosition.Length)], random);
        ItemInstance changed = item with { Affixes = item.Affixes.Select(value => ReferenceEquals(value, removed) ? replacement : value).ToArray() };
        return Ok(ApplyProtections(item, changed, true), $"已完成{tag}偏向打造", resource, cost);
    }

    private static EquipmentCraftingResult ShiftTier(ItemInstance item, string familyId, ulong seed, string resource, int cost)
    {
        AffixRoll? selected = item.Affixes.FirstOrDefault(value => EquipmentCatalog.ResolveAffixId(value.Definition.StableFamilyId) == EquipmentCatalog.ResolveAffixId(familyId));
        if (selected is null || selected.Crafted || item.IsFractured(selected)) return Fail("invalid_tier_target", "请选择可升降的自然词缀。", resource, cost);
        AffixDefinition[] tiers = P1Affixes.For(item.Base, item.ItemLevel).Where(value => value.StableFamilyId == selected.Definition.StableFamilyId).OrderBy(value => value.Tier).ToArray();
        int index = Array.FindIndex(tiers, value => value.Tier == selected.Definition.Tier);
        if (index < 0 || tiers.Length < 2) return Fail("adjacent_tier_unavailable", "没有相邻阶级。", resource, cost);
        bool improve = index == tiers.Length - 1 || index > 0 && new Pcg32(seed).NextBasisPoints() < 5_000;
        int next = improve ? index - 1 : index + 1;
        next = Math.Clamp(next, 0, tiers.Length - 1);
        var random = new Pcg32(seed ^ 0xd1b54a32d192ed03UL);
        AffixRoll replacement = Roll(tiers[next], random);
        ItemInstance changed = item with { Affixes = item.Affixes.Select(value => ReferenceEquals(value, selected) ? replacement : value).ToArray() };
        return Ok(ApplyProtections(item, changed, true), $"词缀移动至 T{tiers[next].Tier}", resource, cost);
    }

    private static ItemInstance ApplyProtections(ItemInstance original, ItemInstance changed, bool explicitCraft)
    {
        if (!explicitCraft) return changed;
        IEnumerable<AffixRoll> affixes = changed.Affixes;
        if (original.ProtectPrefixesNextCraft)
            affixes = affixes.Where(value => value.Definition.Position != AffixPosition.Prefix).Concat(original.Affixes.Where(value => value.Definition.Position == AffixPosition.Prefix));
        if (original.ProtectSuffixesNextCraft)
            affixes = affixes.Where(value => value.Definition.Position != AffixPosition.Suffix).Concat(original.Affixes.Where(value => value.Definition.Position == AffixPosition.Suffix));
        AffixRoll[] normalized = affixes.GroupBy(value => value.Definition.StableFamilyId, StringComparer.Ordinal).Select(group => group.First()).Take(6).ToArray();
        return changed with { Affixes = normalized, ProtectPrefixesNextCraft = false, ProtectSuffixesNextCraft = false, CraftSequence = original.CraftSequence + 1 };
    }

    private static ItemInstance CopyPersistent(ItemInstance original, ItemInstance generated) => generated with
    {
        IsLocked = original.IsLocked, IsCraftingBase = original.IsCraftingBase, LinkedSocketCount = original.LinkedSocketCount,
        Quality = original.Quality, Enchantment = original.Enchantment, RolledBaseArmor = original.RolledBaseArmor,
        RolledBaseEvasion = original.RolledBaseEvasion, RolledBaseShield = original.RolledBaseShield,
        RolledBaseSpiritBarrier = original.RolledBaseSpiritBarrier, RolledImplicitComponents = original.RolledImplicitComponents,
        ProtectPrefixesNextCraft = original.ProtectPrefixesNextCraft, ProtectSuffixesNextCraft = original.ProtectSuffixesNextCraft,
        CraftSequence = original.CraftSequence,
    };

    private static bool HasDirection(AffixDefinition value, string direction)
    {
        string text = $"{value.DisplayName} {value.RawText} {string.Join(' ', value.ModTags ?? [])}";
        string[] needles = direction switch
        {
            "生命" => ["生命", "life"], "防御" => ["护甲", "闪避", "护盾", "灵障", "格挡", "压制", "defence"],
            "攻击" => ["攻击", "命中", "attack"], "法术" => ["法术", "施法", "spell", "caster"],
            "速度" => ["速度", "speed"], "暴击" => ["暴击", "critical"], "物理" => ["物理", "流血", "physical"],
            "火焰" => ["火焰", "点燃", "fire"], "冰霜" => ["冰霜", "冰缓", "冻结", "cold"],
            "闪电" => ["闪电", "感电", "lightning"], "虚空" => ["虚空", "凋零", "void"],
            "属性" => ["体魄", "灵巧", "精神", "能量", "attribute"], _ => [direction],
        };
        return needles.Any(needle => text.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }

    private static AffixRoll Roll(AffixDefinition definition, Pcg32 random)
    {
        RolledAffixComponent[] components = definition.EffectComponents.Select(component => new RolledAffixComponent(component.Kind,
            RollInclusive(random, component.MinimumValue, component.MaximumValue), component.Scope, component.DisplayText)).ToArray();
        return new AffixRoll(definition, components[0].Value, Components: components);
    }

    private static string ValidateCommon(ItemInstance item, EquipmentCraftingOperationEntry operation)
    {
        if (operation.Kind == "LegendaryExchange") return string.Empty;
        if (item.IsLocked) return "item_locked";
        if (item.IsCorrupted) return "item_corrupted";
        return string.Empty;
    }

    private static string ValidateRequest(ItemInstance item, EquipmentCraftingOperationEntry operation,
        EquipmentCraftingRequest request)
    {
        if (operation.Kind == "Enchantment")
        {
            ItemEnchantment? enchantment = EquipmentEnchantmentCatalog.All.FirstOrDefault(value => value.StableId == request.SelectedDefinitionId);
            if (enchantment is null) return "enchantment_required";
            if (request.WorkshopLevel < enchantment.WorkshopLevel) return "workshop_level";
            if (!EquipmentEnchantmentCatalog.Supports(enchantment, item.Base)) return "incompatible_base";
        }
        if (operation.Kind == "LegendaryExchange" && !EquipmentCatalog.LegendaryItems.Any(value =>
                value.Id == request.SelectedDefinitionId && value.Rarity == "Legendary")) return "exchange_target_invalid";
        if (operation.Kind == "LifeEnergy" && item.Rarity != ItemRarity.Rare) return "rare_required";
        if (operation.DisplayName == "淬刃打造" && item.Base.Category is not (ItemCategory.OneHandWeapon or ItemCategory.TwoHandWeapon)) return "incompatible_base";
        if (operation.DisplayName == "守壁打造" && item.Base.ArmorMaximum + item.Base.EvasionMaximum + item.Base.ShieldMaximum + item.Base.SpiritBarrierMaximum == 0) return "incompatible_base";
        if (operation.DisplayName is "连接重铸" or "稳固增连" && item.Base.SocketLimit <= 0) return "no_sockets";
        if (operation.DisplayName == "稳固增连" && item.LinkedSocketCount >= item.Base.SocketLimit) return "links_full";
        return string.Empty;
    }

    private static EquipmentCraftingOperationEntry Get(string id) => EquipmentCatalog.CraftingOperations.Single(value => value.Id == id);

    private static (string resource, int cost) ParseCost(string text, ItemInstance item)
    {
        if (text.Contains("×1/1/1/2/4/8", StringComparison.Ordinal))
            return ("链铸钢", new[] { 1, 1, 1, 2, 4, 8 }[Math.Clamp(item.LinkedSocketCount, 0, 5)]);
        MatchCollection matches = Regex.Matches(text, @"\d[\d,]*");
        int cost = matches.Count == 0 ? 0 : int.Parse(matches[^1].Value.Replace(",", string.Empty, StringComparison.Ordinal), CultureInfo.InvariantCulture);
        string resource = text.Contains('；') ? text.Split('；')[1].Trim() : text;
        resource = Regex.Replace(resource, @"\s*[×x]?\s*\d[\d,]*.*$", string.Empty).Trim();
        return (resource, cost);
    }

    private static bool ChangesExplicitAffixes(P9CraftOperation operation) => operation is not (P9CraftOperation.BlessedReroll or P9CraftOperation.PolishQuality);
    private static int RollInclusive(Pcg32 random, int minimum, int maximum) => minimum == maximum ? minimum : minimum + (int)(random.NextUInt() % (uint)(maximum - minimum + 1));
    private static ulong StableSeed(ItemInstance item, string operation)
    {
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes($"{item.InstanceId}\n{item.CraftSequence}\n{operation}"));
        return BitConverter.ToUInt64(digest, 0);
    }
    private static string FailureText(string reason) => reason switch
    {
        "item_locked" => "锁定装备不能加工。", "item_corrupted" => "腐化装备不能继续加工。",
        "enchantment_required" => "请选择附魔。", "workshop_level" => "工坊等级不足。",
        "incompatible_base" => "该操作不适用于此装备。", "exchange_target_invalid" => "请选择可兑换的普通传奇。",
        "rare_required" => "该操作要求稀有装备。", "no_sockets" => "此底材没有连接孔。",
        "links_full" => "连接数已经达到底材上限。", _ => reason,
    };
    private static EquipmentCraftingResult Ok(ItemInstance item, string summary, string resource, int cost) => new(true, string.Empty, summary, item, resource, cost);
    private static EquipmentCraftingResult Fail(string reason, string summary, string resource, int cost) => new(false, reason, summary, null, resource, cost);
}
