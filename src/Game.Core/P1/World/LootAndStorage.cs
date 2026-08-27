using GameForWork.Core.P1.Items;

namespace GameForWork.Core.P1.World;

public enum LootDisposition
{
    Keep,
    Sell,
    Dismantle,
}

public sealed record LootFilterRule(
    string StableId,
    LootDisposition Disposition,
    ItemRarity? Rarity = null,
    string? BaseStableId = null,
    string? AffixFamilyId = null,
    int? MinimumAffixValue = null)
{
    public bool Matches(ItemInstance item)
    {
        if (Rarity is not null && item.Rarity != Rarity ||
            BaseStableId is not null && item.Base.StableId != BaseStableId)
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
        LootFilterRule? match = _rules.FirstOrDefault(rule => rule.Matches(item));
        return match?.Disposition ?? LootDisposition.Keep;
    }

    public void ReplaceRules(IEnumerable<LootFilterRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _rules.Clear();
        _rules.AddRange(rules);
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

    public int Capacity { get; }
    public StorageUpgradeState Upgrade { get; }
    public int Count => _items.Count;
    public bool IsFull => Count >= Capacity;
    public IReadOnlyList<ItemInstance> Items => _items;
    public IReadOnlySet<string> DiscoveredBases => _discoveredBases;
    public IReadOnlySet<string> DiscoveredLegendaryRules => _discoveredLegendaryRules;

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
            LootDisposition disposition = firstDiscovery ? LootDisposition.Keep : filter.Evaluate(item);
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
}
