using GameForWork.Core.P1.Items;

namespace GameForWork.Core.P1.World;

public enum LootDisposition
{
    Keep,
    Sell,
    Dismantle,
}

public enum P16ItemSortMode
{
    LinkedSockets,
    Rarity,
}

public static class P16ItemSorting
{
    public static int Compare(ItemInstance left, ItemInstance right, P16ItemSortMode mode)
    {
        int primary = mode == P16ItemSortMode.LinkedSockets
            ? right.LinkedSocketCount.CompareTo(left.LinkedSocketCount)
            : right.Rarity.CompareTo(left.Rarity);
        if (primary != 0) return primary;
        int secondary = mode == P16ItemSortMode.LinkedSockets
            ? right.Rarity.CompareTo(left.Rarity)
            : right.LinkedSocketCount.CompareTo(left.LinkedSocketCount);
        if (secondary != 0) return secondary;
        int level = right.ItemLevel.CompareTo(left.ItemLevel);
        return level != 0 ? level : string.Compare(left.Base.DisplayName, right.Base.DisplayName, StringComparison.Ordinal);
    }
}

public sealed record LootFilterRule(
    string StableId,
    LootDisposition Disposition,
    ItemRarity? Rarity = null,
    string? BaseStableId = null,
    string? AffixFamilyId = null,
    int? MinimumAffixValue = null,
    bool Enabled = true,
    EquipmentSlot? Slot = null,
    int MinimumLinkedSockets = 0,
    bool RequireFiveOrSixLink = false,
    bool RequireCurrentSchemeNeed = false,
    ItemRarity? MinimumRarity = null,
    ItemRarity? MaximumRarity = null,
    ItemCategory? Category = null,
    int? MinimumItemLevel = null,
    int? MaximumItemLevel = null,
    int? MaximumLinkedSockets = null)
{
    public bool Matches(ItemInstance item)
    {
        if (!Enabled || Rarity is not null && item.Rarity != Rarity ||
            MinimumRarity is not null && item.Rarity < MinimumRarity ||
            MaximumRarity is not null && item.Rarity > MaximumRarity ||
            Category is not null && item.Base.Category != Category ||
            BaseStableId is not null && item.Base.StableId != BaseStableId ||
            Slot is not null && item.Base.PrimarySlot != Slot ||
            MinimumItemLevel is not null && item.ItemLevel < MinimumItemLevel ||
            MaximumItemLevel is not null && item.ItemLevel > MaximumItemLevel ||
            item.LinkedSocketCount < MinimumLinkedSockets ||
            MaximumLinkedSockets is not null && item.LinkedSocketCount > MaximumLinkedSockets ||
            RequireFiveOrSixLink && item.LinkedSocketCount < 5 ||
            RequireCurrentSchemeNeed && item.LinkedSocketCount < Math.Max(2, MinimumLinkedSockets))
        {
            return false;
        }

        if (AffixFamilyId is null)
        {
            return true;
        }

        return item.Affixes.Any(affix =>
            affix.Definition.StableFamilyId == AffixFamilyId &&
            (MinimumAffixValue is null || affix.Value >= MinimumAffixValue));
    }
}

public sealed class LootFilter
{
    private readonly List<LootFilterRule> _rules;

    public LootFilter(IEnumerable<LootFilterRule>? rules = null)
    {
        _rules = rules?.ToList() ?? CreateDefaultRules().ToList();
    }

    public IReadOnlyList<LootFilterRule> Rules => _rules;

    public LootDisposition Evaluate(ItemInstance item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.IsLocked || item.IsKeyItem || item.LinkedSocketCount >= 5)
            return LootDisposition.Keep;
        LootFilterRule? match = _rules.FirstOrDefault(rule => rule.Matches(item));
        return match?.Disposition ?? LootDisposition.Keep;
    }

    public void ReplaceRules(IEnumerable<LootFilterRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _rules.Clear();
        _rules.AddRange(rules.Where(rule => rule.StableId is not "core.filter.six_link" and not "core.filter.five_link"));
    }

    private static IReadOnlyList<LootFilterRule> CreateDefaultRules() =>
    [
        new("core.filter.legendary", LootDisposition.Keep, ItemRarity.Legendary),
        new("core.filter.rare", LootDisposition.Keep, ItemRarity.Rare),
        new("core.filter.magic", LootDisposition.Sell, ItemRarity.Magic),
        new("core.filter.basic", LootDisposition.Dismantle, ItemRarity.Basic),
    ];
}

public sealed class EquipmentStorage
{
    public const int InitialCapacity = 100;
    private readonly List<ItemInstance> _items = [];
    private readonly HashSet<string> _discoveredBases = new(StringComparer.Ordinal);
    private readonly HashSet<string> _discoveredLegendaryRules = new(StringComparer.Ordinal);

    public EquipmentStorage(int capacity = InitialCapacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        Capacity = capacity;
        Upgrade = new StorageUpgradeState(0, capacity);
    }

    public int Capacity { get; private set; }
    public StorageUpgradeState Upgrade { get; }
    public int Count => _items.Count;
    public bool IsFull => Count >= Capacity;
    public IReadOnlyList<ItemInstance> Items => _items;
    public IReadOnlySet<string> DiscoveredBases => _discoveredBases;
    public IReadOnlySet<string> DiscoveredLegendaryRules => _discoveredLegendaryRules;

    public bool TrySetCapacity(int capacity)
    {
        if (capacity < Count || capacity <= 0) return false;
        Capacity = capacity;
        return true;
    }

    public bool IsFirstDiscovery(ItemInstance item) =>
        !_discoveredBases.Contains(item.Base.StableId) ||
        item.LegendaryRule is not null && !_discoveredLegendaryRules.Contains(item.LegendaryRule.StableId);

    public bool TryStore(ItemInstance item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (IsFull)
        {
            return false;
        }

        _items.Add(item);
        _discoveredBases.Add(item.Base.StableId);
        if (item.LegendaryRule is not null)
        {
            _discoveredLegendaryRules.Add(item.LegendaryRule.StableId);
        }

        return true;
    }

    public ItemInstance? TakeAt(int index)
    {
        if (index < 0 || index >= _items.Count)
        {
            return null;
        }

        ItemInstance item = _items[index];
        _items.RemoveAt(index);
        return item;
    }

    public bool TryReplaceAt(int index, ItemInstance item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (index < 0 || index >= _items.Count)
        {
            return false;
        }

        _items[index] = item;
        RecordDiscovery(item);
        return true;
    }

    public int IndexOf(string instanceId) => _items.FindIndex(item => item.InstanceId == instanceId);

    public bool TryInsert(ItemInstance item, int index)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (IsFull)
        {
            return false;
        }

        _items.Insert(Math.Clamp(index, 0, _items.Count), item);
        RecordDiscovery(item);
        return true;
    }

    public bool TryMove(int sourceIndex, int targetIndex)
    {
        ItemInstance? item = TakeAt(sourceIndex);
        if (item is null)
        {
            return false;
        }

        _items.Insert(Math.Clamp(targetIndex, 0, _items.Count), item);
        return true;
    }

    public void Sort(P16ItemSortMode mode) => _items.Sort((left, right) => P16ItemSorting.Compare(left, right, mode));

    public void SortByLinkedSockets() => Sort(P16ItemSortMode.LinkedSockets);

    public void RestoreDiscoveries(IEnumerable<string> bases, IEnumerable<string> legendaryRules)
    {
        ArgumentNullException.ThrowIfNull(bases);
        ArgumentNullException.ThrowIfNull(legendaryRules);
        _discoveredBases.UnionWith(bases);
        _discoveredLegendaryRules.UnionWith(legendaryRules);
    }

    private void RecordDiscovery(ItemInstance item)
    {
        _discoveredBases.Add(item.Base.StableId);
        if (item.LegendaryRule is not null)
        {
            _discoveredLegendaryRules.Add(item.LegendaryRule.StableId);
        }
    }
}

public sealed record StorageUpgradeState(int Level, int Capacity);

public sealed record LootProcessingResult(
    int Stored,
    int Sold,
    int Dismantled,
    int GoldGained,
    int IronScrapsGained,
    bool StorageBecameFull,
    bool ExpeditionMustStop,
    IReadOnlyList<string> ForcedFirstDiscoveries);

public static class LootProcessor
{
    public const int MagicSaleGold = 3;
    public const int BasicDismantleIronScraps = 1;

    public static LootProcessingResult Process(
        IReadOnlyList<ItemInstance> items,
        EquipmentStorage storage,
        LootFilter filter,
        StorageFullBehavior fullBehavior)
    {
        int stored = 0;
        int sold = 0;
        int dismantled = 0;
        int gold = 0;
        int scraps = 0;
        bool mustStop = false;
        var discoveries = new List<string>();
        foreach (ItemInstance item in items)
        {
            bool firstDiscovery = storage.IsFirstDiscovery(item);
            LootDisposition disposition = firstDiscovery || item.IsLocked
                ? LootDisposition.Keep
                : filter.Evaluate(item);
            if (disposition == LootDisposition.Keep)
            {
                if (!storage.TryStore(item))
                {
                    mustStop |= fullBehavior == StorageFullBehavior.StopExpedition;
                    continue;
                }

                stored++;
                if (firstDiscovery)
                {
                    discoveries.Add(item.Base.StableId);
                }

                continue;
            }

            if (disposition == LootDisposition.Sell)
            {
                sold++;
                gold = checked(gold + MagicSaleGold);
            }
            else
            {
                dismantled++;
                scraps = checked(scraps + BasicDismantleIronScraps);
            }
        }

        return new LootProcessingResult(
            stored,
            sold,
            dismantled,
            gold,
            scraps,
            storage.IsFull,
            mustStop,
            discoveries);
    }
}

public sealed class ExpeditionBackpack
{
    public const int Capacity = 20;
    private readonly List<ItemInstance> _items = [];

    public int Count => _items.Count;
    public IReadOnlyList<ItemInstance> Items => _items;

    public bool TryAdd(ItemInstance item)
    {
        if (_items.Count >= Capacity)
        {
            return false;
        }

        _items.Add(item);
        return true;
    }

    public IReadOnlyList<ItemInstance> TakeAll()
    {
        ItemInstance[] items = _items.ToArray();
        _items.Clear();
        return items;
    }

    public void Replace(IEnumerable<ItemInstance> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        ItemInstance[] replacement = items.ToArray();
        if (replacement.Length > Capacity)
        {
            throw new InvalidDataException("Expedition backpack snapshot exceeds its capacity.");
        }

        _items.Clear();
        _items.AddRange(replacement);
    }
}
