using GameForWork.Core.P1.Items;

namespace GameForWork.Core.P2;

public enum P2CharacterKind
{
    Hero,
    Mercenary,
}

public enum ItemContainerKind
{
    SortingBag,
    Storage,
    Recovery,
    Buyback,
    Equipped,
}

public enum SkillStoneKind
{
    Active,
    Support,
}

public sealed record SkillStoneDefinition(
    string StableId,
    string DisplayName,
    SkillStoneKind Kind,
    int LinkCost = 0);

public static class P2SkillStones
{
    private static readonly IReadOnlyDictionary<string, SkillStoneDefinition> Catalog = new[]
    {
        new SkillStoneDefinition("core.skill_stone.heavy_strike", "重击", SkillStoneKind.Active),
        new SkillStoneDefinition("core.skill_stone.war_cry", "战吼", SkillStoneKind.Active),
        new SkillStoneDefinition("core.skill_stone.earth_cleave", "裂地横扫", SkillStoneKind.Active),
        new SkillStoneDefinition("core.skill_stone.spirit_blade", "幽魂飞刃", SkillStoneKind.Active),
        new SkillStoneDefinition("core.skill_stone.increased_area", "扩大范围", SkillStoneKind.Support, 1),
        new SkillStoneDefinition("core.skill_stone.attack_speed", "攻击速度", SkillStoneKind.Support, 1),
        new SkillStoneDefinition("core.skill_stone.bleed", "流血", SkillStoneKind.Support, 1),
        new SkillStoneDefinition("core.skill_stone.life_cost", "生命消耗", SkillStoneKind.Support, 1),
        new SkillStoneDefinition("core.skill_stone.chain", "追加连锁", SkillStoneKind.Support, 1),
    }.ToDictionary(item => item.StableId, StringComparer.Ordinal);

    public static IReadOnlyCollection<SkillStoneDefinition> All => Catalog.Values.ToArray();

    public static SkillStoneDefinition Get(string stableId) => Catalog.TryGetValue(stableId, out SkillStoneDefinition? value)
        ? value
        : throw new KeyNotFoundException($"Unknown skill stone: {stableId}");
}

public sealed record SkillStoneInstance(
    string InstanceId,
    string DefinitionId,
    int Level = 1,
    int Experience = 0)
{
    public SkillStoneDefinition Definition => P2SkillStones.Get(DefinitionId);
}

public sealed record SkillLinkConfiguration(
    string ActiveStoneInstanceId,
    IReadOnlyList<string> SupportStoneInstanceIds,
    int Priority);

public sealed record BuybackEntry(ItemInstance Item, int SalePrice, long Sequence);

public sealed record P2ManagementSnapshot(
    IReadOnlyList<ItemInstance> SortingBag,
    IReadOnlyList<ItemInstance> Recovery,
    IReadOnlyList<BuybackEntry> Buyback,
    IReadOnlyList<SkillStoneInstance> SkillStones,
    IReadOnlyList<SkillLinkConfiguration> SkillLinks,
    IReadOnlyList<string> OperationHistory,
    long OperationSequence,
    bool FreeFullRespecAvailable);

public sealed class P2ManagementState
{
    public const int SortingBagCapacity = 20;
    public const int BuybackCapacity = 20;
    public const int HistoryCapacity = 200;

    private readonly List<ItemInstance> _sortingBag = [];
    private readonly List<ItemInstance> _recovery = [];
    private readonly List<BuybackEntry> _buyback = [];
    private readonly List<SkillStoneInstance> _skillStones = [];
    private readonly List<SkillLinkConfiguration> _skillLinks = [];
    private readonly List<string> _operationHistory = [];
    private long _operationSequence;

    public IReadOnlyList<ItemInstance> SortingBag => _sortingBag;
    public IReadOnlyList<ItemInstance> Recovery => _recovery;
    public IReadOnlyList<BuybackEntry> Buyback => _buyback;
    public IReadOnlyList<SkillStoneInstance> SkillStones => _skillStones;
    public IReadOnlyList<SkillLinkConfiguration> SkillLinks => _skillLinks;
    public IReadOnlyList<string> OperationHistory => _operationHistory;
    public bool FreeFullRespecAvailable { get; private set; }

    public static P2ManagementState CreateNew()
    {
        var state = new P2ManagementState();
        foreach (SkillStoneDefinition definition in P2SkillStones.All.OrderBy(item => item.StableId, StringComparer.Ordinal))
        {
            state._skillStones.Add(new SkillStoneInstance(
                $"starter-{definition.StableId[(definition.StableId.LastIndexOf('.') + 1)..]}",
                definition.StableId));
        }

        string heavyStrike = state._skillStones.Single(item => item.DefinitionId == "core.skill_stone.heavy_strike").InstanceId;
        string bleed = state._skillStones.Single(item => item.DefinitionId == "core.skill_stone.bleed").InstanceId;
        string earthCleave = state._skillStones.Single(item => item.DefinitionId == "core.skill_stone.earth_cleave").InstanceId;
        string spiritBlade = state._skillStones.Single(item => item.DefinitionId == "core.skill_stone.spirit_blade").InstanceId;
        string chain = state._skillStones.Single(item => item.DefinitionId == "core.skill_stone.chain").InstanceId;
        state._skillLinks.Add(new SkillLinkConfiguration(heavyStrike, [bleed], 1));
        state._skillLinks.Add(new SkillLinkConfiguration(earthCleave, [], 2));
        state._skillLinks.Add(new SkillLinkConfiguration(spiritBlade, [chain], 3));
        return state;
    }

    public static P2ManagementState Restore(P2ManagementSnapshot? snapshot, bool legacyMigration)
    {
        P2ManagementState state = snapshot is null ? CreateNew() : new P2ManagementState();
        if (snapshot is not null)
        {
            state._sortingBag.AddRange(snapshot.SortingBag.Take(SortingBagCapacity));
            state._recovery.AddRange(snapshot.Recovery);
            state._recovery.AddRange(snapshot.SortingBag.Skip(SortingBagCapacity));
            state._buyback.AddRange(snapshot.Buyback.TakeLast(BuybackCapacity));
            state._skillStones.AddRange(snapshot.SkillStones);
            state._skillLinks.AddRange(snapshot.SkillLinks);
            state._operationHistory.AddRange(snapshot.OperationHistory.TakeLast(HistoryCapacity));
            state._operationSequence = Math.Max(snapshot.OperationSequence, 0);
            state.FreeFullRespecAvailable = snapshot.FreeFullRespecAvailable;
        }

        if (legacyMigration)
        {
            state.FreeFullRespecAvailable = true;
            state.AddHistory("旧存档已迁移：获得一次免费完整洗点。");
        }

        foreach (SkillStoneDefinition definition in P2SkillStones.All.Where(definition =>
                     state._skillStones.All(stone => stone.DefinitionId != definition.StableId)))
        {
            state._skillStones.Add(new SkillStoneInstance(
                $"starter-{definition.StableId[(definition.StableId.LastIndexOf('.') + 1)..]}",
                definition.StableId));
        }

        state.EnsureP4SkillLink("core.skill_stone.earth_cleave", 2);
        state.EnsureP4SkillLink("core.skill_stone.spirit_blade", 3, "core.skill_stone.chain");

        return state;
    }

    private void EnsureP4SkillLink(string activeDefinitionId, int priority, string? supportDefinitionId = null)
    {
        SkillStoneInstance active = _skillStones.Single(stone => stone.DefinitionId == activeDefinitionId);
        if (_skillLinks.Any(link => link.ActiveStoneInstanceId == active.InstanceId))
        {
            return;
        }

        string[] supports = supportDefinitionId is null
            ? []
            : [_skillStones.Single(stone => stone.DefinitionId == supportDefinitionId).InstanceId];
        _skillLinks.Add(new SkillLinkConfiguration(active.InstanceId, supports, priority));
    }

    public P2ManagementSnapshot Capture() => new(
        _sortingBag.ToArray(),
        _recovery.ToArray(),
        _buyback.ToArray(),
        _skillStones.ToArray(),
        _skillLinks.ToArray(),
        _operationHistory.ToArray(),
        _operationSequence,
        FreeFullRespecAvailable);

    public bool TryAddToSortingBag(ItemInstance item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (_sortingBag.Count >= SortingBagCapacity || Contains(item.InstanceId))
        {
            return false;
        }

        _sortingBag.Add(item);
        AddHistory($"{item.Base.DisplayName} 已放入整理背包。");
        return true;
    }

    public void AddToRecovery(ItemInstance item, string reason)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (Contains(item.InstanceId))
        {
            throw new InvalidOperationException($"Item {item.InstanceId} already exists in a P2 management container.");
        }

        _recovery.Add(item);
        AddHistory($"{item.Base.DisplayName} 已进入恢复箱：{reason}");
    }

    public ItemInstance? TakeSortingBagAt(int index) => TakeAt(_sortingBag, index);

    public ItemInstance? TakeRecoveryAt(int index) => TakeAt(_recovery, index);

    public bool TryInsertSortingBag(ItemInstance item, int index)
    {
        if (_sortingBag.Count >= SortingBagCapacity || Contains(item.InstanceId))
        {
            return false;
        }

        _sortingBag.Insert(Math.Clamp(index, 0, _sortingBag.Count), item);
        AddHistory($"{item.Base.DisplayName} 已放入整理背包。");
        return true;
    }

    public bool TryMoveSortingBag(int sourceIndex, int targetIndex)
    {
        ItemInstance? item = TakeAt(_sortingBag, sourceIndex);
        if (item is null)
        {
            return false;
        }

        _sortingBag.Insert(Math.Clamp(targetIndex, 0, _sortingBag.Count), item);
        return true;
    }

    public void ReturnToSortingBag(ItemInstance item, int preferredIndex = -1)
    {
        if (_sortingBag.Count >= SortingBagCapacity)
        {
            AddToRecovery(item, "整理背包已满");
            return;
        }

        InsertAt(_sortingBag, item, preferredIndex);
    }

    public void AddBuyback(ItemInstance item, int salePrice)
    {
        if (item.IsLocked)
        {
            throw new InvalidOperationException("Locked items cannot be sold.");
        }

        _buyback.Add(new BuybackEntry(item, salePrice, ++_operationSequence));
        if (_buyback.Count > BuybackCapacity)
        {
            _buyback.RemoveRange(0, _buyback.Count - BuybackCapacity);
        }

        AddHistory($"已出售 {item.Base.DisplayName}，可在回购中恢复。");
    }

    public BuybackEntry? TakeBuybackAt(int index)
    {
        if (index < 0 || index >= _buyback.Count)
        {
            return null;
        }

        BuybackEntry entry = _buyback[index];
        _buyback.RemoveAt(index);
        return entry;
    }

    public bool ConsumeFreeFullRespec()
    {
        if (!FreeFullRespecAvailable)
        {
            return false;
        }

        FreeFullRespecAvailable = false;
        AddHistory("已使用迁移赠送的免费完整洗点。");
        return true;
    }

    public bool TryLinkSupport(string activeStoneInstanceId, string supportStoneInstanceId)
    {
        SkillStoneInstance? active = _skillStones.FirstOrDefault(item => item.InstanceId == activeStoneInstanceId);
        SkillStoneInstance? support = _skillStones.FirstOrDefault(item => item.InstanceId == supportStoneInstanceId);
        if (active?.Definition.Kind != SkillStoneKind.Active || support?.Definition.Kind != SkillStoneKind.Support)
        {
            return false;
        }

        SkillLinkConfiguration? previous = _skillLinks.FirstOrDefault(link => link.ActiveStoneInstanceId == activeStoneInstanceId);
        if (previous?.SupportStoneInstanceIds.Contains(supportStoneInstanceId, StringComparer.Ordinal) == true ||
            previous?.SupportStoneInstanceIds.Count >= 5)
        {
            return false;
        }

        _skillLinks.RemoveAll(link => link.ActiveStoneInstanceId == activeStoneInstanceId);
        foreach (SkillLinkConfiguration link in _skillLinks.ToArray())
        {
            if (link.SupportStoneInstanceIds.Contains(supportStoneInstanceId, StringComparer.Ordinal))
            {
                _skillLinks.Remove(link);
                _skillLinks.Add(link with
                {
                    SupportStoneInstanceIds = link.SupportStoneInstanceIds
                        .Where(id => id != supportStoneInstanceId)
                        .ToArray(),
                });
            }
        }

        List<string> supports = previous?.SupportStoneInstanceIds.ToList() ?? [];
        supports.Add(supportStoneInstanceId);
        _skillLinks.Add(new SkillLinkConfiguration(activeStoneInstanceId, supports, previous?.Priority ?? _skillLinks.Count + 1));
        AddHistory($"{support.Definition.DisplayName} 已连接到 {active.Definition.DisplayName}。");
        return true;
    }

    public bool UnlinkSupport(string activeStoneInstanceId, string supportStoneInstanceId)
    {
        SkillLinkConfiguration? link = _skillLinks.FirstOrDefault(item => item.ActiveStoneInstanceId == activeStoneInstanceId);
        if (link is null || !link.SupportStoneInstanceIds.Contains(supportStoneInstanceId, StringComparer.Ordinal))
        {
            return false;
        }

        _skillLinks.Remove(link);
        _skillLinks.Add(link with
        {
            SupportStoneInstanceIds = link.SupportStoneInstanceIds.Where(id => id != supportStoneInstanceId).ToArray(),
        });
        AddHistory("辅助技能石已解除连接。");
        return true;
    }

    public void AddSkillExperience(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        for (int index = 0; index < _skillStones.Count; index++)
        {
            SkillStoneInstance stone = _skillStones[index];
            int total = checked(stone.Experience + amount);
            int level = Math.Min(20, 1 + total / 1_000);
            _skillStones[index] = stone with { Level = level, Experience = total };
        }
    }

    public void AddHistory(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        _operationHistory.Add(message.Trim());
        if (_operationHistory.Count > HistoryCapacity)
        {
            _operationHistory.RemoveRange(0, _operationHistory.Count - HistoryCapacity);
        }
    }

    public static int SalePrice(ItemInstance item) => item.Rarity switch
    {
        ItemRarity.Basic => 1,
        ItemRarity.Magic => 3,
        ItemRarity.Rare => 8,
        ItemRarity.Legendary => 20,
        _ => 0,
    };

    private bool Contains(string instanceId) =>
        _sortingBag.Any(item => item.InstanceId == instanceId) ||
        _recovery.Any(item => item.InstanceId == instanceId) ||
        _buyback.Any(entry => entry.Item.InstanceId == instanceId);

    private static ItemInstance? TakeAt(List<ItemInstance> items, int index)
    {
        if (index < 0 || index >= items.Count)
        {
            return null;
        }

        ItemInstance item = items[index];
        items.RemoveAt(index);
        return item;
    }

    private static void InsertAt(List<ItemInstance> items, ItemInstance item, int preferredIndex)
    {
        if (preferredIndex < 0 || preferredIndex > items.Count)
        {
            items.Add(item);
        }
        else
        {
            items.Insert(preferredIndex, item);
        }
    }
}
