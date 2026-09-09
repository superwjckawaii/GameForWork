using GameForWork.Core.Equipment;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Spatial;
using GameForWork.Core.Management;
using GameForWork.Core.Builds;

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
    IReadOnlyList<SkillStoneInstance>? SkillStones = null,
    IReadOnlyList<JewelInstance>? Jewels = null,
    bool IsNew = true)
{
    public LootChest Validate()
    {
        if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(SourceId) ||
            Difficulty is < 1 or > 120 || Gold < 0 || IronScraps < 0 || Equipment is null ||
            Equipment.Any(item => item is null) ||
            Equipment.Select(item => item.InstanceId).Distinct(StringComparer.Ordinal).Count() != Equipment.Count ||
            (Metals ?? []).Any(stack => stack.Amount < 0) ||
            (SkillStones ?? []).Any(stone => stone is null || string.IsNullOrWhiteSpace(stone.InstanceId)) ||
            (SkillStones ?? []).Select(stone => stone.InstanceId).Distinct(StringComparer.Ordinal).Count() != (SkillStones ?? []).Count ||
            (Jewels ?? []).Any(jewel => jewel is null || string.IsNullOrWhiteSpace(jewel.InstanceId)) ||
            (Jewels ?? []).Select(jewel => jewel.InstanceId).Distinct(StringComparer.Ordinal).Count() != (Jewels ?? []).Count)
            throw new InvalidDataException("Invalid ordinary loot chest.");
        return this;
    }
}

public sealed record LootChestSnapshot(IReadOnlyList<LootChest> Chests);
