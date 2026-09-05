using GameForWork.Core.Campaign;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Management;
using GameForWork.Core.Equipment;

namespace GameForWork.Core.Inventory;

public enum BatchScope { SortingBag, Storage, Both }
public enum BatchAction { Sell, Dismantle }

public sealed record BatchTarget(ItemContainerKind Container, int Index, ItemInstance Item);
public sealed record BatchPreview(
    BatchAction Action,
    BatchScope Scope,
    ItemRarity MaximumRarity,
    IReadOnlyList<BatchTarget> Targets,
    IReadOnlyDictionary<ItemRarity, int> Counts,
    int Proceeds,
    int Excluded,
    IReadOnlyDictionary<string, int> ExcludedReasons,
    int BuybackEvictions,
    bool IncludesMythic = false,
    int MythicCount = 0)
{
    public int Total => Targets.Count;
}

public sealed record BatchExecution(int Completed, int Failed);

public static class BatchItems
{
    public static BatchPreview Preview(GameSession session, BatchAction action,
        BatchScope scope, ItemRarity maximumRarity, bool includeCraftingBases = false,
        bool includeMythic = false)
    {
        ArgumentNullException.ThrowIfNull(session);
        IEnumerable<BatchTarget> candidates = scope switch
        {
            BatchScope.SortingBag => EnumerateSorting(session),
            BatchScope.Storage => EnumerateStorage(session),
            _ => EnumerateStorage(session).Concat(EnumerateSorting(session)),
        };
        BatchTarget[] all = candidates.ToArray();
        BatchTarget[] withinRarity = all.Where(target => target.Item.Rarity <= maximumRarity).ToArray();
        BatchTarget[] selected = withinRarity.Where(target => IsSafe(target.Item, includeCraftingBases, includeMythic))
            .OrderBy(target => target.Container).ThenBy(target => target.Index).ToArray();
        IReadOnlyDictionary<string, int> excludedReasons = withinRarity
            .Where(target => !IsSafe(target.Item, includeCraftingBases, includeMythic))
            .GroupBy(target => ProtectionReason(target.Item), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        int proceeds = action == BatchAction.Sell
            ? selected.Sum(target => ManagementState.SalePrice(target.Item))
            : selected.Sum(target => DismantleYield(target.Item));
        int evictions = action == BatchAction.Sell
            ? Math.Max(0, session.Management.Buyback.Count + selected.Length - ManagementState.BuybackCapacity)
            : 0;
        return new(action, scope, maximumRarity, selected,
            selected.GroupBy(target => target.Item.Rarity).ToDictionary(group => group.Key, group => group.Count()),
            proceeds, withinRarity.Length - selected.Length, excludedReasons, evictions, includeMythic,
            selected.Count(target => IsMythic(target.Item)));
    }

    public static BatchExecution Execute(GameSession session, BatchPreview preview)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(preview);
        var commands = new ItemCommandService(session);
        int completed = 0;
        foreach (IGrouping<ItemContainerKind, BatchTarget> group in preview.Targets.GroupBy(target => target.Container))
        {
            foreach (BatchTarget target in group.OrderByDescending(target => target.Index))
            {
                ItemCommandResult result = preview.Action == BatchAction.Sell
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

    public static bool IsSafe(ItemInstance item, bool includeCraftingBases = false, bool includeMythic = false)
    {
        if (item.IsLocked || item.IsKeyItem || IsMythic(item) && !includeMythic) return false;
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

    private static IEnumerable<BatchTarget> EnumerateStorage(GameSession session) =>
        session.World.Storage.Items.Select((item, index) => new BatchTarget(ItemContainerKind.Storage, index, item));

    private static IEnumerable<BatchTarget> EnumerateSorting(GameSession session) =>
        session.Management.SortingBag.Select((item, index) => new BatchTarget(ItemContainerKind.SortingBag, index, item));
}
