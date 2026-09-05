using System.Security.Cryptography;
using System.Text;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Spatial;
using GameForWork.Core.Simulation;

namespace GameForWork.Core.Skills;

public enum SocketCraftOperation
{
    RerollLinks,
    UpgradeLinks,
    ChaosReroll,
    DivineReroll,
    FractureAffix,
}

public sealed record CraftPreview(
    bool Succeeded,
    string FailureReason,
    string Summary,
    ItemInstance? Result,
    MetalCurrencyKind Currency,
    int Cost,
    int CurrentLinks,
    int ResultLinks);

public static class SocketCraftingRules
{
    public static CraftPreview Preview(
        ItemInstance item,
        SocketCraftOperation operation,
        string fractureFamilyId = "",
        ulong? seed = null)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.IsLocked) return Fail("item_locked", "锁定物品不能制作。", operation);
        ulong resolvedSeed = seed ?? StableSeed(item, operation, fractureFamilyId);
        return operation switch
        {
            SocketCraftOperation.RerollLinks => RerollLinks(item, resolvedSeed),
            SocketCraftOperation.UpgradeLinks => UpgradeLinks(item),
            SocketCraftOperation.ChaosReroll => Chaos(item, resolvedSeed),
            SocketCraftOperation.DivineReroll => Divine(item, resolvedSeed),
            SocketCraftOperation.FractureAffix => Fracture(item, fractureFamilyId),
            _ => Fail("unknown_operation", "未知制作操作。", operation),
        };
    }

    public static CraftPreview Craft(
        TownEconomyState economy,
        ItemInstance item,
        SocketCraftOperation operation,
        string fractureFamilyId = "",
        ulong? seed = null)
    {
        CraftPreview preview = Preview(item, operation, fractureFamilyId, seed);
        if (!preview.Succeeded) return preview;
        if (!economy.TrySpendMetal(preview.Currency, preview.Cost))
        {
            return preview with { Succeeded = false, FailureReason = "insufficient_materials", Summary = $"{MetalCurrencies.Get(preview.Currency).DisplayName}不足。" };
        }
        return preview;
    }

    private static CraftPreview RerollLinks(ItemInstance item, ulong seed)
    {
        if (!SocketRules.ProvidesSockets(item.Base.Category)) return Fail("sockets_required", "该装备不提供连接孔。", SocketCraftOperation.RerollLinks);
        int maximum = SocketRules.Maximum(item.Base.Category, item.ItemLevel);
        if (item.LinkedSocketCount >= 6)
        {
            return Fail("six_link_locked", "六连装备不能重铸连接。", SocketCraftOperation.RerollLinks);
        }
        int result = SocketRules.Roll(item.Base.Category, item.ItemLevel, seed);
        return Ok(item, item with { LinkedSocketCount = result }, SocketCraftOperation.RerollLinks,
            MetalCurrencyKind.ChainSteel, 1, $"重铸连接：{item.LinkedSocketCount} 连 → {result} 连");
    }

    private static CraftPreview UpgradeLinks(ItemInstance item)
    {
        if (!SocketRules.ProvidesSockets(item.Base.Category)) return Fail("sockets_required", "该装备不提供连接孔。", SocketCraftOperation.UpgradeLinks);
        int maximum = SocketRules.Maximum(item.Base.Category, item.ItemLevel);
        if (item.LinkedSocketCount >= maximum) return Fail("maximum_links", "物品等级或装备类型限制了继续升连。", SocketCraftOperation.UpgradeLinks);
        int cost = item.LinkedSocketCount switch { 2 => 1, 3 => 2, 4 => 4, 5 => 8, _ => 1 };
        ItemInstance result = item with { LinkedSocketCount = item.LinkedSocketCount + 1 };
        return Ok(item, result, SocketCraftOperation.UpgradeLinks, MetalCurrencyKind.ChainSteel, cost,
            $"保证升连：{item.LinkedSocketCount} 连 → {result.LinkedSocketCount} 连");
    }

    private static CraftPreview Chaos(ItemInstance item, ulong seed)
    {
        if (item.Rarity != ItemRarity.Rare) return Fail("rare_required", "混沌金只能重铸稀有装备。", SocketCraftOperation.ChaosReroll);
        ItemInstance rolled = ItemGenerator.Generate(item.Base.StableId, item.ItemLevel, ItemRarity.Rare, seed, item.InstanceId);
        AffixRoll[] protectedAffixes = item.Affixes.Where(affix => affix.Crafted || item.IsFractured(affix)).ToArray();
        AffixRoll[] random = rolled.Affixes.Where(affix => protectedAffixes.All(kept => kept.Definition.StableFamilyId != affix.Definition.StableFamilyId))
            .Take(Math.Max(0, 6 - protectedAffixes.Length)).ToArray();
        ItemInstance result = item with { Affixes = protectedAffixes.Concat(random).ToArray() };
        return Ok(item, result, SocketCraftOperation.ChaosReroll, MetalCurrencyKind.ChaosGold, 1, "已重铸随机词缀；连接、锁定、破溃和工匠词缀保持不变");
    }

    private static CraftPreview Divine(ItemInstance item, ulong seed)
    {
        if (item.Affixes.Count == 0) return Fail("affix_required", "该物品没有可重掷词缀。", SocketCraftOperation.DivineReroll);
        var random = new Pcg32(seed);
        AffixRoll[] affixes = item.Affixes.Select(affix => affix.Crafted || item.IsFractured(affix)
            ? affix
            : Reroll(affix.Definition, random)).ToArray();
        return Ok(item, item with { Affixes = affixes }, SocketCraftOperation.DivineReroll,
            MetalCurrencyKind.DivineSilver, 1, "已重掷所有非固定自然词缀数值");
    }

    private static CraftPreview Fracture(ItemInstance item, string familyId)
    {
        if (!string.IsNullOrEmpty(item.FracturedAffixFamilyId)) return Fail("already_fractured", "该物品已有破溃词缀。", SocketCraftOperation.FractureAffix);
        AffixRoll? selected = item.Affixes.FirstOrDefault(affix => !affix.Crafted &&
            (string.IsNullOrEmpty(familyId) || affix.Definition.StableFamilyId == familyId));
        if (selected is null) return Fail("natural_affix_required", "请选择一条自然词缀进行固化。", SocketCraftOperation.FractureAffix);
        return Ok(item, item with { FracturedAffixFamilyId = selected.Definition.StableFamilyId },
            SocketCraftOperation.FractureAffix, MetalCurrencyKind.FractureSteel, 1, $"已固化：{selected.Definition.DisplayName}");
    }

    private static CraftPreview Ok(ItemInstance before, ItemInstance after, SocketCraftOperation operation,
        MetalCurrencyKind currency, int cost, string summary) =>
        new(true, string.Empty, summary, after, currency, cost, before.LinkedSocketCount, after.LinkedSocketCount);

    private static CraftPreview Fail(string reason, string summary, SocketCraftOperation operation) =>
        new(false, reason, summary, null, operation is SocketCraftOperation.RerollLinks or SocketCraftOperation.UpgradeLinks
            ? MetalCurrencyKind.ChainSteel : operation switch
            {
                SocketCraftOperation.ChaosReroll => MetalCurrencyKind.ChaosGold,
                SocketCraftOperation.DivineReroll => MetalCurrencyKind.DivineSilver,
                _ => MetalCurrencyKind.FractureSteel,
            }, 0, 0, 0);

    private static int RollInclusive(Pcg32 random, int minimum, int maximum) => minimum == maximum
        ? minimum : minimum + (int)(random.NextUInt() % (uint)(maximum - minimum + 1));

    private static AffixRoll Reroll(AffixDefinition definition, Pcg32 random)
    {
        RolledAffixComponent[] components = definition.EffectComponents.Select(component =>
            new RolledAffixComponent(component.Kind, RollInclusive(random, component.MinimumValue, component.MaximumValue),
                component.Scope, component.DisplayText)).ToArray();
        return new AffixRoll(definition, components[0].Value, Components: components);
    }

    private static ulong StableSeed(ItemInstance item, SocketCraftOperation operation, string family)
    {
        string source = $"{item.InstanceId}|{item.LinkedSocketCount}|{operation}|{family}|" +
                        string.Join(';', item.Affixes.Select(affix => $"{affix.Definition.StableFamilyId}:{string.Join(',', affix.Effects.Select(effect => effect.Value))}"));
        return BitConverter.ToUInt64(SHA256.HashData(Encoding.UTF8.GetBytes(source)), 0);
    }
}
