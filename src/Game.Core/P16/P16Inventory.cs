using GameForWork.Core.P1;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P2;

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
    int BuybackEvictions)
{
    public int Total => Targets.Count;
}

public static class P16BatchItems
{
    public static P16BatchPreview Preview(P1GameSession session, P16BatchAction action,
        P16BatchScope scope, ItemRarity maximumRarity)
    {
        ArgumentNullException.ThrowIfNull(session);
        IEnumerable<P16BatchTarget> candidates = scope switch
        {
            P16BatchScope.SortingBag => EnumerateSorting(session),
            P16BatchScope.Storage => EnumerateStorage(session),
            _ => EnumerateStorage(session).Concat(EnumerateSorting(session)),
        };
        P16BatchTarget[] all = candidates.ToArray();
        P16BatchTarget[] eligible = all.Where(target => target.Item.Rarity <= maximumRarity &&
            !target.Item.IsLocked && !target.Item.IsCraftingBase && !target.Item.IsKeyItem && !IsMythic(target.Item)).ToArray();
        var targets = new List<P16BatchTarget>();
        targets.AddRange(eligible.Where(target => target.Item.LegendaryRule is null));
        foreach (IGrouping<string, P16BatchTarget> group in eligible.Where(target => target.Item.LegendaryRule is not null)
                     .GroupBy(target => target.Item.LegendaryRule!.StableId, StringComparer.Ordinal))
        {
            int removable = Math.Max(0, LegendaryCopies(session, group.Key) - 1);
            targets.AddRange(group.Take(removable));
        }
        P16BatchTarget[] selected = targets.OrderBy(target => target.Container).ThenBy(target => target.Index).ToArray();
        int proceeds = action == P16BatchAction.Sell
            ? selected.Sum(target => P2ManagementState.SalePrice(target.Item))
            : selected.Sum(target => DismantleYield(target.Item));
        int evictions = action == P16BatchAction.Sell
            ? Math.Max(0, session.Management.Buyback.Count + selected.Length - P2ManagementState.BuybackCapacity)
            : 0;
        return new(action, scope, maximumRarity, selected,
            selected.GroupBy(target => target.Item.Rarity).ToDictionary(group => group.Key, group => group.Count()),
            proceeds, all.Count(target => target.Item.Rarity <= maximumRarity) - selected.Length, evictions);
    }

    public static int Execute(P1GameSession session, P16BatchPreview preview)
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
        return completed;
    }

    public static int DismantleYield(ItemInstance item) => item.Rarity switch
    {
        ItemRarity.Basic => 1,
        ItemRarity.Magic => 2,
        ItemRarity.Rare => 5,
        ItemRarity.Legendary => 12,
        _ => 0,
    };

    public static bool IsSafe(P1GameSession session, ItemInstance item)
    {
        if (item.IsLocked || item.IsCraftingBase || item.IsKeyItem || IsMythic(item)) return false;
        if (item.LegendaryRule is null) return true;
        return LegendaryCopies(session, item.LegendaryRule.StableId) > 1;
    }

    private static bool IsMythic(ItemInstance item) =>
        item.LegendaryRule?.StableId.StartsWith("core.mythic.", StringComparison.Ordinal) == true;

    private static int LegendaryCopies(P1GameSession session, string ruleId)
    {
        IEnumerable<ItemInstance> equipped = session.HeroEquipment.Items.Values
            .Concat(session.MercenaryEquipment.Items.Values)
            .Concat(session.Town.Roster.SelectMany(member => member.Equipment.Items.Values));
        return session.World.Storage.Items.Concat(session.Management.SortingBag).Concat(equipped)
            .Count(item => item.LegendaryRule?.StableId == ruleId);
    }

    private static IEnumerable<P16BatchTarget> EnumerateStorage(P1GameSession session) =>
        session.World.Storage.Items.Select((item, index) => new P16BatchTarget(ItemContainerKind.Storage, index, item));

    private static IEnumerable<P16BatchTarget> EnumerateSorting(P1GameSession session) =>
        session.Management.SortingBag.Select((item, index) => new P16BatchTarget(ItemContainerKind.SortingBag, index, item));
}
