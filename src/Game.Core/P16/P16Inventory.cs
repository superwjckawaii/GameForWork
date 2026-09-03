using GameForWork.Core.P1;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P2;
using GameForWork.Core.Equipment;

namespace GameForWork.Core.P16;

public enum P16BatchScope { SortingBag, Storage, Both }
public enum P16BatchAction { Sell, Dismantle }

public sealed record P16BatchTarget(ItemContainerKind Container, int Index, ItemInstance Item);
public sealed record P16BatchPreview(
    P16BatchAction Action,
    P16BatchScope Scope,
    ItemRarity MaximumRarity,
    IReadOnlyList<P16BatchTarget> Targets,
    IReadOnlyDictionary<ItemRarity, int> Counts,
    int Proceeds,
    int Excluded,
    IReadOnlyDictionary<string, int> ExcludedReasons,
    int BuybackEvictions)
{
    public int Total => Targets.Count;
}

public sealed record P16BatchExecution(int Completed, int Failed);

public static class P16BatchItems
{
    public static P16BatchPreview Preview(P1GameSession session, P16BatchAction action,
        P16BatchScope scope, ItemRarity maximumRarity, bool includeCraftingBases = false)
    {
        ArgumentNullException.ThrowIfNull(session);
        IEnumerable<P16BatchTarget> candidates = scope switch
        {
            P16BatchScope.SortingBag => EnumerateSorting(session),
            P16BatchScope.Storage => EnumerateStorage(session),
            _ => EnumerateStorage(session).Concat(EnumerateSorting(session)),
        };
        P16BatchTarget[] all = candidates.ToArray();
        P16BatchTarget[] withinRarity = all.Where(target => target.Item.Rarity <= maximumRarity).ToArray();
        P16BatchTarget[] selected = withinRarity.Where(target => IsSafe(target.Item, includeCraftingBases))
            .OrderBy(target => target.Container).ThenBy(target => target.Index).ToArray();
        IReadOnlyDictionary<string, int> excludedReasons = withinRarity
            .Where(target => !IsSafe(target.Item, includeCraftingBases))
            .GroupBy(target => ProtectionReason(target.Item), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        int proceeds = action == P16BatchAction.Sell
            ? selected.Sum(target => P2ManagementState.SalePrice(target.Item))
            : selected.Sum(target => DismantleYield(target.Item));
        int evictions = action == P16BatchAction.Sell
            ? Math.Max(0, session.Management.Buyback.Count + selected.Length - P2ManagementState.BuybackCapacity)
            : 0;
        return new(action, scope, maximumRarity, selected,
            selected.GroupBy(target => target.Item.Rarity).ToDictionary(group => group.Key, group => group.Count()),
            proceeds, withinRarity.Length - selected.Length, excludedReasons, evictions);
    }

    public static P16BatchExecution Execute(P1GameSession session, P16BatchPreview preview)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(preview);
        var commands = new P2ItemCommandService(session);
        int completed = 0;
        foreach (IGrouping<ItemContainerKind, P16BatchTarget> group in preview.Targets.GroupBy(target => target.Container))
        {
            foreach (P16BatchTarget target in group.OrderByDescending(target => target.Index))
            {
                P2ItemCommandResult result = preview.Action == P16BatchAction.Sell
                    ? commands.Sell(target.Container, target.Index)
                    : commands.Dismantle(target.Container, target.Index, confirmed: true);
                if (result.Succeeded) completed++;
            }
        }
        return new(completed, preview.Targets.Count - completed);
    }

    public static int DismantleYield(ItemInstance item) => item.Rarity switch
    {
        ItemRarity.Basic => 1,
        ItemRarity.Magic => 2,
        ItemRarity.Rare => 5,
        ItemRarity.Legendary => 12,
        _ => 0,
    };

    public static bool IsSafe(ItemInstance item, bool includeCraftingBases = false)
    {
        if (item.IsLocked || item.IsKeyItem || IsMythic(item)) return false;
        if (item.IsCraftingBase && !includeCraftingBases) return false;
        return true;
    }

    private static bool IsMythic(ItemInstance item) =>
        item.LegendaryRule?.StableId.StartsWith("core.mythic.", StringComparison.Ordinal) == true ||
        EquipmentCatalog.LegendaryItems.Any(value => value.Id == item.LegendaryCatalogId && value.Rarity == "Mythic");

    private static string ProtectionReason(ItemInstance item) => item switch
    {
        { IsLocked: true } => "已锁定",
        { IsCraftingBase: true } => "制作底材",
        { IsKeyItem: true } => "关键物品",
        _ when IsMythic(item) => "神话装备",
        _ => "其他保护",
    };

    private static IEnumerable<P16BatchTarget> EnumerateStorage(P1GameSession session) =>
        session.World.Storage.Items.Select((item, index) => new P16BatchTarget(ItemContainerKind.Storage, index, item));

    private static IEnumerable<P16BatchTarget> EnumerateSorting(P1GameSession session) =>
        session.Management.SortingBag.Select((item, index) => new P16BatchTarget(ItemContainerKind.SortingBag, index, item));
}
