using GameForWork.Core.P1.Items;
using GameForWork.Core.Simulation;

namespace GameForWork.Core.P29;

public sealed record P29ResourceCraftResult(bool Succeeded, string Summary, ItemInstance? Result, int Cost);

public static class P29ResourceCrafting
{
    public const int RedFavorCost = 100;
    public const int BlueFavorCost = 100;

    public static P29ResourceCraftResult ShiftAffixTier(ItemInstance item, string affixFamilyId, ulong seed)
    {
        if (!item.CanModify || item.Rarity != ItemRarity.Rare) return Fail("赤誓升降要求可修改的稀有装备。", RedFavorCost);
        AffixRoll? current = item.Affixes.FirstOrDefault(affix => !affix.Crafted && affix.Definition.StableFamilyId == affixFamilyId);
        if (current is null) return Fail("请选择一条非制作词缀。", RedFavorCost);
        AffixDefinition[] family = P1Affixes.For(item.Base, item.ItemLevel)
            .Where(definition => definition.StableFamilyId == current.Definition.StableFamilyId && definition.Position == current.Definition.Position)
            .OrderBy(definition => P1Affixes.TierFor(item.Base, definition)).ToArray();
        if (family.Length < 2) return Fail("该词缀在当前底材上没有可升降档位。", RedFavorCost);
        int currentTier = P1Affixes.TierFor(item.Base, current.Definition);
        var random = new Pcg32(seed);
        int direction = random.NextBasisPoints() < 5_000 ? -1 : 1;
        int desired = currentTier + direction;
        AffixDefinition? target = family.FirstOrDefault(definition => P1Affixes.TierFor(item.Base, definition) == desired);
        if (target is null)
        {
            direction = -direction;
            desired = currentTier + direction;
            target = family.FirstOrDefault(definition => P1Affixes.TierFor(item.Base, definition) == desired);
        }
        if (target is null) return Fail("该词缀已处于唯一边界档位。", RedFavorCost);
        RolledAffixComponent[] components = target.EffectComponents.Select(component =>
        {
            int span = Math.Max(1, component.MaximumValue - component.MinimumValue + 1);
            return new RolledAffixComponent(component.Kind,
                component.MinimumValue + (int)(random.NextUInt() % (uint)span), component.Scope, component.DisplayText);
        }).ToArray();
        var replacement = new AffixRoll(target, components[0].Value, Components: components);
        ItemInstance result = item with { Affixes = item.Affixes.Select(affix => ReferenceEquals(affix, current) ? replacement : affix).ToArray() };
        string verb = desired < currentTier ? "提升" : "降低";
        return new(true, $"赤誓将 {current.Definition.DisplayName} 从 T{currentTier} {verb}至 T{desired}。", result, RedFavorCost);
    }

    public static P29ResourceCraftResult RerollQuality(ItemInstance item, ulong seed)
    {
        if (!item.CanModify) return Fail("苍誓品质加工要求未锁定且未腐化的装备。", BlueFavorCost);
        int quality = (int)(new Pcg32(seed).NextUInt() % 41);
        return new(true, $"苍誓将装备品质从 {item.Quality}% 重置为 {quality}%。", item with { Quality = quality }, BlueFavorCost);
    }

    private static P29ResourceCraftResult Fail(string summary, int cost) => new(false, summary, null, cost);
}
