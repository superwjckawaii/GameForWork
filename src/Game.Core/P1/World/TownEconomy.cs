using GameForWork.Core.P1.Items;
using GameForWork.Core.P4;

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
        int skillStones = 0,
        long supplyProductionRemainderMilliseconds = 0,
        IReadOnlyDictionary<MetalCurrencyKind, int>? metalCurrencies = null)
    {
        if (expeditionSupplies < 0 || gold < 0 || ironScraps < 0 || memoryAshes < 0 ||
            wardenMarks < 0 || skillStones < 0 || supplyProductionRemainderMilliseconds is < 0 or >= SupplyProductionIntervalMilliseconds)
        {
            throw new ArgumentOutOfRangeException(nameof(expeditionSupplies));
        }

        ExpeditionSupplies = expeditionSupplies;
        Gold = gold;
        IronScraps = ironScraps;
        MemoryAshes = memoryAshes;
        WardenMarks = wardenMarks;
        SkillStones = skillStones;
        foreach (MetalCurrencyKind kind in Enum.GetValues<MetalCurrencyKind>())
        {
            _metalCurrencies[kind] = Math.Max(0, metalCurrencies?.GetValueOrDefault(kind) ?? 0);
        }
        _supplyProductionRemainderMilliseconds = supplyProductionRemainderMilliseconds;
    }

    private readonly Dictionary<MetalCurrencyKind, int> _metalCurrencies = [];

    public int ExpeditionSupplies { get; private set; }
    public int Gold { get; private set; }
    public int IronScraps { get; private set; }
    public int MemoryAshes { get; private set; }
    public int WardenMarks { get; private set; }
    public int SkillStones { get; private set; }
    public long SupplyProductionRemainderMilliseconds => _supplyProductionRemainderMilliseconds;
    public IReadOnlyDictionary<MetalCurrencyKind, int> MetalCurrencies => _metalCurrencies;

    public int MetalAmount(MetalCurrencyKind kind) => _metalCurrencies.GetValueOrDefault(kind);

    public void AddMetal(MetalCurrencyKind kind, int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        _metalCurrencies[kind] = checked(MetalAmount(kind) + amount);
    }

    public bool TrySpendMetal(MetalCurrencyKind kind, int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        if (MetalAmount(kind) < amount)
        {
            return false;
        }

        _metalCurrencies[kind] -= amount;
        return true;
    }

    public bool TryConsumeMapSupply()
    {
        if (ExpeditionSupplies <= 0)
        {
            return false;
        }

        ExpeditionSupplies--;
        return true;
    }

    public void AddExpeditionSupplies(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        ExpeditionSupplies = checked(ExpeditionSupplies + amount);
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
        foreach (MetalCurrencyStack stack in rewards.Metals ?? [])
        {
            AddMetal(stack.Kind, stack.Amount);
        }
    }

    public void AddDispositionProceeds(int gold, int ironScraps)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(gold);
        ArgumentOutOfRangeException.ThrowIfNegative(ironScraps);
        Gold = checked(Gold + gold);
        IronScraps = checked(IronScraps + ironScraps);
    }

    public bool TrySpendGold(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        if (Gold < amount)
        {
            return false;
        }

        Gold -= amount;
        return true;
    }

    public bool TrySpendMemoryAshes(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        if (MemoryAshes < amount) return false;
        MemoryAshes -= amount;
        return true;
    }

    public bool TryPay(int gold, int ironScraps)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(gold);
        ArgumentOutOfRangeException.ThrowIfNegative(ironScraps);
        if (Gold < gold || IronScraps < ironScraps)
        {
            return false;
        }

        Gold -= gold;
        IronScraps -= ironScraps;
        return true;
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

    public int TakeMemoryAshes()
    {
        int amount = MemoryAshes;
        MemoryAshes = 0;
        return amount;
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
