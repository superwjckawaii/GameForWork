using GameForWork.Core.Equipment;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Spatial;

namespace GameForWork.Core.Campaign.World;

/// <summary>Persistent ordinary map loot. Contents are frozen at map settlement and claimed atomically.</summary>
public sealed record LootChest(
    string Id,
    string SourceId,
    int? Difficulty,
    IReadOnlyList<ItemInstance> Equipment,
    int Gold,
    int IronScraps,
    IReadOnlyList<MetalCurrencyStack>? Metals = null,
    bool IsNew = true)
{
    public LootChest Validate()
    {
        if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(SourceId) ||
            Difficulty is < 1 or > 120 || Gold < 0 || IronScraps < 0 || Equipment is null ||
            Equipment.Any(item => item is null) ||
            Equipment.Select(item => item.InstanceId).Distinct(StringComparer.Ordinal).Count() != Equipment.Count ||
            (Metals ?? []).Any(stack => stack.Amount < 0))
            throw new InvalidDataException("Invalid ordinary loot chest.");
        return this;
    }
}

public sealed record LootChestSnapshot(IReadOnlyList<LootChest> Chests);
