using GameForWork.Core.P1.Items;

namespace GameForWork.Core.P1.World;

public sealed class TownEconomyState
{
    public const long SupplyProductionIntervalMilliseconds = 150_000;
    private long _supplyProductionRemainderMilliseconds;

    public TownEconomyState(
        int expeditionSupplies = 10,
        int gold = 0,
        int ironScraps = 0,
        int memoryAshes = 5,
        int wardenMarks = 0,
        int skillStones = 0)
    {
        ExpeditionSupplies = expeditionSupplies;
        Gold = gold;
        IronScraps = ironScraps;
        MemoryAshes = memoryAshes;
        WardenMarks = wardenMarks;
        SkillStones = skillStones;
    }

    public int ExpeditionSupplies { get; private set; }
    public int Gold { get; private set; }
    public int IronScraps { get; private set; }
    public int MemoryAshes { get; private set; }
    public int WardenMarks { get; private set; }
    public int SkillStones { get; private set; }
    public long SupplyProductionRemainderMilliseconds => _supplyProductionRemainderMilliseconds;

    public bool TryConsumeMapSupply()
    {
        if (ExpeditionSupplies <= 0)
        {
            return false;
        }

        ExpeditionSupplies--;
        return true;
    }

    public int AdvanceProduction(long elapsedMilliseconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(elapsedMilliseconds);
        _supplyProductionRemainderMilliseconds = checked(
            _supplyProductionRemainderMilliseconds + elapsedMilliseconds);
        int produced = checked((int)(_supplyProductionRemainderMilliseconds / SupplyProductionIntervalMilliseconds));
        _supplyProductionRemainderMilliseconds %= SupplyProductionIntervalMilliseconds;
        ExpeditionSupplies = checked(ExpeditionSupplies + produced);
        return produced;
    }

    public void AddRewards(MapStackableRewards rewards)
    {
        ArgumentNullException.ThrowIfNull(rewards);
        Gold = checked(Gold + rewards.Gold);
        IronScraps = checked(IronScraps + rewards.IronScraps);
        MemoryAshes = checked(MemoryAshes + rewards.MemoryAshes);
        WardenMarks = checked(WardenMarks + rewards.WardenMarks);
        SkillStones = checked(SkillStones + rewards.SkillStones);
    }

    public void AddDispositionProceeds(int gold, int ironScraps)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(gold);
        ArgumentOutOfRangeException.ThrowIfNegative(ironScraps);
        Gold = checked(Gold + gold);
        IronScraps = checked(IronScraps + ironScraps);
    }

    public bool TryPayWorkshopCost()
    {
        if (Gold < P1Workshop.GoldCost || IronScraps < P1Workshop.IronScrapCost)
        {
            return false;
        }

        Gold -= P1Workshop.GoldCost;
        IronScraps -= P1Workshop.IronScrapCost;
        return true;
    }

    public bool TryExchangeLegendary(out ItemInstance? legendary)
    {
        if (WardenMarks < 10)
        {
            legendary = null;
            return false;
        }

        WardenMarks -= 10;
        legendary = P1Legendary.Create(10) with { InstanceId = $"pity-echoing-oathbreaker-{WardenMarks}" };
        return true;
    }
}

public sealed record WorkshopResult(bool Succeeded, string FailureReason, ItemInstance? Item);

public static class P1Workshop
{
    public const int GoldCost = 50;
    public const int IronScrapCost = 10;
    public const int CraftedPhysicalIncreaseBasisPoints = 2_000;
    private const string CraftedFamily = "core.affix.workshop.increased_physical";

    private static readonly AffixDefinition CraftedAffix = new(
        CraftedFamily,
        "工坊物理伤害增加",
        ItemCategory.TwoHandWeapon,
        AffixPosition.Prefix,
        Tier: 0,
        MinimumItemLevel: 1,
        MinimumValue: CraftedPhysicalIncreaseBasisPoints,
        MaximumValue: CraftedPhysicalIncreaseBasisPoints,
        Weight: 0,
        ItemModifierKind.IncreasedPhysicalDamageBasisPoints);

    public static WorkshopResult CraftPhysicalIncrease(TownEconomyState economy, ItemInstance item)
    {
        ArgumentNullException.ThrowIfNull(economy);
        ArgumentNullException.ThrowIfNull(item);
        if (item.Base.Category != ItemCategory.TwoHandWeapon)
        {
            return new WorkshopResult(false, "weapon_required", null);
        }

        AffixRoll[] retained = item.Affixes
            .Where(affix => !affix.Crafted)
            .ToArray();
        int naturalPrefixes = retained.Count(affix => affix.Definition.Position == AffixPosition.Prefix);
        int maximumPrefixes = item.Rarity == ItemRarity.Magic ? 1 : 3;
        if (naturalPrefixes >= maximumPrefixes)
        {
            return new WorkshopResult(false, "no_prefix_slot", null);
        }

        if (!economy.TryPayWorkshopCost())
        {
            return new WorkshopResult(false, "insufficient_materials", null);
        }

        var affixes = retained.Append(new AffixRoll(CraftedAffix, CraftedPhysicalIncreaseBasisPoints, Crafted: true)).ToArray();
        return new WorkshopResult(true, string.Empty, item with { Affixes = affixes });
    }
}
