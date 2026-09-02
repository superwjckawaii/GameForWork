using GameForWork.Core.P1.Items;
using GameForWork.Core.P5;
using GameForWork.Core.P6;
using GameForWork.Core.P1.Combat;
using GameForWork.Core.Simulation;
using GameForWork.Core.P1.World;
using GameForWork.Core.P17;
using GameForWork.Core.P23;
using GameForWork.Core.P24;
using GameForWork.Core.P30;

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
    int LinkCost = 0,
    SkillTag Tags = SkillTag.None,
    SkillTag SupportedTags = SkillTag.None,
    SkillTag ExcludedTags = SkillTag.None,
    string Description = "",
    bool StarterGranted = true,
    P17SkillCapability Capabilities = P17SkillCapability.None,
    P17SkillCapability RequiredAllCapabilities = P17SkillCapability.None,
    P17SkillCapability RequiredAnyCapabilities = P17SkillCapability.None,
    P17SkillCapability ExcludedCapabilities = P17SkillCapability.None,
    SkillSupport CombatSupport = SkillSupport.None,
    P24SupportMechanic P24Support = P24SupportMechanic.None,
    P17SupportConflict ProvidesConflict = P17SupportConflict.None,
    P17SupportConflict ConflictsWith = P17SupportConflict.None,
    string P30SupportId = "");

public static class P2SkillStones
{
    private static readonly IReadOnlyDictionary<string, SkillStoneDefinition> Catalog =
        P30SkillCatalog.Active.Select(Active)
            .Concat(P30SkillCatalog.Supports.Select(Support))
            .ToDictionary(item => item.StableId, StringComparer.Ordinal);

    public static IReadOnlyCollection<SkillStoneDefinition> All => Catalog.Values.ToArray();

    public static SkillStoneDefinition Get(string stableId) => Catalog.TryGetValue(stableId, out SkillStoneDefinition? value)
        ? value
        : throw new KeyNotFoundException($"Unknown skill stone: {stableId}");

    public static IReadOnlyCollection<SkillStoneDefinition> DropPool => Catalog.Values.Where(item => !item.StarterGranted).ToArray();

    private static SkillStoneDefinition Active(P30ActiveSkillDefinition definition) =>
        new(definition.Combat.StoneId, definition.Combat.DisplayName, SkillStoneKind.Active,
            Tags: definition.Combat.Tags,
            Description: definition.Combat.Description, StarterGranted: definition.Combat.StarterGranted,
            Capabilities: definition.Combat.Capabilities);

    private static SkillStoneDefinition Support(P30SupportSkillDefinition definition) =>
        new(definition.StoneId, definition.DisplayName, SkillStoneKind.Support, 1,
            Description: definition.Effect, StarterGranted: definition.StarterGranted,
            RequiredAllCapabilities: definition.RequiredAll, RequiredAnyCapabilities: definition.RequiredAny,
            ExcludedCapabilities: definition.Excluded, CombatSupport: definition.LegacySupport,
            P24Support: definition.LegacyP24Support, ProvidesConflict: definition.ProvidesConflict,
            ConflictsWith: definition.ConflictsWith, P30SupportId: definition.StoneId);
}

public sealed record SkillStoneInstance(
    string InstanceId,
    string DefinitionId,
    int Level = 1,
    int Experience = 0, int Quality = 0, bool Mutated = false)
{
    public SkillStoneDefinition Definition => P2SkillStones.Get(DefinitionId);
}

public sealed record SkillLinkConfiguration(
    string ActiveStoneInstanceId,
    IReadOnlyList<string> SupportStoneInstanceIds,
    int Priority,
    string ChainId = "",
    IReadOnlyList<string?>? SocketStoneInstanceIds = null,
    SkillAiRule? AiRule = null,
    bool ReservationEnabled = true);

public enum P6SkillSchemeKind
{
    Clear,
    Boss,
    Custom,
}

public sealed record P6SkillSchemeSnapshot(P6SkillSchemeKind Kind, IReadOnlyList<SkillLinkConfiguration> Links);
public sealed record P6SchemeSwitchResult(bool Succeeded, int MissingStones, int EjectedStones, string Message);

public sealed record BuybackEntry(ItemInstance Item, int SalePrice, long Sequence);

public sealed record P2ManagementSnapshot(
    IReadOnlyList<ItemInstance> SortingBag,
    IReadOnlyList<ItemInstance> Recovery,
    IReadOnlyList<BuybackEntry> Buyback,
    IReadOnlyList<SkillStoneInstance> SkillStones,
    IReadOnlyList<SkillLinkConfiguration> SkillLinks,
    IReadOnlyList<string> OperationHistory,
    long OperationSequence,
    bool FreeFullRespecAvailable,
    IReadOnlyList<P6SkillSchemeSnapshot>? SkillSchemes = null,
    P6SkillSchemeKind ActiveSkillScheme = P6SkillSchemeKind.Clear);

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
    private readonly Dictionary<P6SkillSchemeKind, IReadOnlyList<SkillLinkConfiguration>> _skillSchemes = [];
    private long _operationSequence;

    public IReadOnlyList<ItemInstance> SortingBag => _sortingBag;
    public IReadOnlyList<ItemInstance> Recovery => _recovery;
    public IReadOnlyList<BuybackEntry> Buyback => _buyback;
    public IReadOnlyList<SkillStoneInstance> SkillStones => _skillStones;
    public IReadOnlyList<SkillLinkConfiguration> SkillLinks => _skillLinks;
    public IReadOnlySet<string> InstalledSkillStoneIds => _skillLinks
        .Where(link => !string.IsNullOrEmpty(link.ChainId))
        .SelectMany(link => SocketIds(link))
        .Where(id => !string.IsNullOrEmpty(id))
        .Cast<string>()
        .ToHashSet(StringComparer.Ordinal);
    public IReadOnlyList<SkillStoneInstance> UninstalledSkillStones => _skillStones
        .Where(stone => !InstalledSkillStoneIds.Contains(stone.InstanceId))
        .ToArray();
    public IReadOnlyList<string> OperationHistory => _operationHistory;
    public bool FreeFullRespecAvailable { get; private set; }
    public P6SkillSchemeKind ActiveSkillScheme { get; private set; } = P6SkillSchemeKind.Clear;
    public IReadOnlyDictionary<P6SkillSchemeKind, IReadOnlyList<SkillLinkConfiguration>> SkillSchemes => _skillSchemes;

    public static P2ManagementState CreateNew(P23BaseClass baseClass = P23BaseClass.Fighter)
    {
        var state = new P2ManagementState();
        foreach (SkillStoneDefinition definition in P2SkillStones.All.Where(item => item.StarterGranted)
                     .OrderBy(item => item.StableId, StringComparer.Ordinal))
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
        string warCry = state._skillStones.Single(item => item.DefinitionId == "core.skill_stone.war_cry").InstanceId;
        state._skillLinks.Add(new SkillLinkConfiguration(heavyStrike, [bleed], 1, P5SkillChainIds.WeaponPrimary));
        state._skillLinks.Add(new SkillLinkConfiguration(earthCleave, [], 2, P5SkillChainIds.WeaponSecondary));
        state._skillLinks.Add(new SkillLinkConfiguration(spiritBlade, [chain], 3, P5SkillChainIds.Chest));
        state._skillLinks.Add(new SkillLinkConfiguration(warCry, [], 4, P5SkillChainIds.HelmetTool));
        state.ConfigureStarterSkill(baseClass);
        state.SaveAllSchemesFromCurrent();
        return state;
    }

    private void ConfigureStarterSkill(P23BaseClass baseClass)
    {
        if (baseClass == P23BaseClass.Fighter) return;
        P23ClassDefinition definition = P23ClassCatalog.Get(baseClass);
        string stoneId = definition.StarterSkillId.Replace("core.skill.", "core.skill_stone.", StringComparison.Ordinal);
        SkillStoneInstance? stone = _skillStones.FirstOrDefault(item => item.DefinitionId == stoneId);
        if (stone is null)
        {
            _ = P2SkillStones.Get(stoneId);
            stone = new SkillStoneInstance($"starter-p23-{definition.StableId.Split('.').Last()}", stoneId);
            _skillStones.Add(stone);
        }
        SkillLinkConfiguration primary = _skillLinks.Single(link => link.ChainId == P5SkillChainIds.WeaponPrimary);
        int index = _skillLinks.IndexOf(primary);
        _skillLinks[index] = primary with
        {
            ActiveStoneInstanceId = stone.InstanceId,
            SupportStoneInstanceIds = [],
            SocketStoneInstanceIds = null,
        };
    }

    public static P2ManagementState Restore(P2ManagementSnapshot? snapshot, bool legacyMigration)
    {
        P2ManagementState state = snapshot is null ? CreateNew() : new P2ManagementState();
        if (snapshot is not null)
        {
            if (snapshot.SkillStones.Any(s => s.Quality is < 0 or > 20 || s.Level is < 1 or > 20))
                throw new InvalidDataException("技能石品质或等级无效。");
            state._sortingBag.AddRange(snapshot.SortingBag.Take(SortingBagCapacity).Select(P30EquipmentAffixes.RemoveForbiddenGlobalWeaponAffixes).Select(P6SocketRules.Ensure));
            state._recovery.AddRange(snapshot.Recovery.Select(P30EquipmentAffixes.RemoveForbiddenGlobalWeaponAffixes).Select(P6SocketRules.Ensure));
            state._recovery.AddRange(snapshot.SortingBag.Skip(SortingBagCapacity).Select(P30EquipmentAffixes.RemoveForbiddenGlobalWeaponAffixes).Select(P6SocketRules.Ensure));
            state._buyback.AddRange(snapshot.Buyback.TakeLast(BuybackCapacity)
                .Select(entry => entry with { Item = P6SocketRules.Ensure(P30EquipmentAffixes.RemoveForbiddenGlobalWeaponAffixes(entry.Item)) }));
            state._skillStones.AddRange(snapshot.SkillStones);
            state._skillLinks.AddRange(snapshot.SkillLinks);
            state._operationHistory.AddRange(snapshot.OperationHistory.TakeLast(HistoryCapacity));
            state._operationSequence = Math.Max(snapshot.OperationSequence, 0);
            state.FreeFullRespecAvailable = snapshot.FreeFullRespecAvailable;
            state.ActiveSkillScheme = snapshot.ActiveSkillScheme;
            foreach (P6SkillSchemeSnapshot scheme in snapshot.SkillSchemes ?? [])
            {
                state._skillSchemes[scheme.Kind] = CloneLinks(scheme.Links);
            }
        }

        if (legacyMigration)
        {
            state.FreeFullRespecAvailable = true;
            state.AddHistory("旧存档已迁移：获得一次免费完整洗点。");
        }

        foreach (SkillStoneDefinition definition in P2SkillStones.All.Where(definition => definition.StarterGranted &&
                     state._skillStones.All(stone => stone.DefinitionId != definition.StableId)))
        {
            state._skillStones.Add(new SkillStoneInstance(
                $"starter-{definition.StableId[(definition.StableId.LastIndexOf('.') + 1)..]}",
                definition.StableId));
        }

        state.EnsureP4SkillLink("core.skill_stone.earth_cleave", 2);
        state.EnsureP4SkillLink("core.skill_stone.spirit_blade", 3, "core.skill_stone.chain");
        state.EnsureP4SkillLink("core.skill_stone.war_cry", 4);
        if (state._skillSchemes.Count == 0)
        {
            state.SaveAllSchemesFromCurrent();
        }

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
        FreeFullRespecAvailable,
        _skillSchemes.Select(pair => new P6SkillSchemeSnapshot(pair.Key, CloneLinks(pair.Value))).ToArray(),
        ActiveSkillScheme);

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

    public bool TryLinkSupport(string activeStoneInstanceId, string supportStoneInstanceId, int maximumSupports = 5)
    {
        SkillStoneInstance? active = _skillStones.FirstOrDefault(item => item.InstanceId == activeStoneInstanceId);
        SkillStoneInstance? support = _skillStones.FirstOrDefault(item => item.InstanceId == supportStoneInstanceId);
        if (active?.Definition.Kind != SkillStoneKind.Active || support?.Definition.Kind != SkillStoneKind.Support)
        {
            return false;
        }

        SkillLinkConfiguration? previous = _skillLinks.FirstOrDefault(link => link.ActiveStoneInstanceId == activeStoneInstanceId);
        if (previous?.SupportStoneInstanceIds.Contains(supportStoneInstanceId, StringComparer.Ordinal) == true ||
            maximumSupports is < 0 or > 5 || previous?.SupportStoneInstanceIds.Count >= maximumSupports)
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
        string?[] sockets = previous?.SocketStoneInstanceIds?.ToArray() ??
            new string?[] { activeStoneInstanceId }.Concat(supports.Cast<string?>()).ToArray();
        if (!sockets.Contains(supportStoneInstanceId, StringComparer.Ordinal))
        {
            int empty = Array.FindIndex(sockets, string.IsNullOrEmpty);
            if (empty >= 0)
            {
                sockets[empty] = supportStoneInstanceId;
            }
            else
            {
                sockets = sockets.Append(supportStoneInstanceId).ToArray();
            }
        }
        _skillLinks.Add(new SkillLinkConfiguration(activeStoneInstanceId, supports,
            previous?.Priority ?? _skillLinks.Count + 1, previous?.ChainId ?? string.Empty, sockets));
        AddHistory($"{support.Definition.DisplayName} 已连接到 {active.Definition.DisplayName}。");
        return true;
    }

    public bool TryAssignActiveToChain(
        string activeStoneInstanceId,
        string chainId,
        IReadOnlyList<P5SkillChainDefinition> chains)
    {
        SkillStoneInstance? active = _skillStones.FirstOrDefault(item => item.InstanceId == activeStoneInstanceId);
        P5SkillChainDefinition? chain = chains.FirstOrDefault(item => item.StableId == chainId);
        if (active is null || chain is null || !P5SkillChainRules.Accepts(chain, active.Definition))
        {
            return false;
        }

        SkillLinkConfiguration? source = _skillLinks.FirstOrDefault(item => item.ActiveStoneInstanceId == activeStoneInstanceId);
        SkillLinkConfiguration? occupied = _skillLinks.FirstOrDefault(item => item.ChainId == chainId);
        string previousChain = source?.ChainId ?? string.Empty;
        if (source is null)
        {
            source = new SkillLinkConfiguration(activeStoneInstanceId, [], _skillLinks.Count + 1, chainId);
            _skillLinks.Add(source);
        }
        else
        {
            ReplaceLink(source, source with { ChainId = chainId });
        }

        if (occupied is not null && occupied.ActiveStoneInstanceId != activeStoneInstanceId)
        {
            ReplaceLink(occupied, occupied with { ChainId = previousChain });
        }

        AddHistory($"{active.Definition.DisplayName} 已装入 {chain.DisplayName}。");
        return true;
    }

    public void SortSortingBag(P16ItemSortMode mode) =>
        _sortingBag.Sort((left, right) => P16ItemSorting.Compare(left, right, mode));

    public bool TryPlaceStone(
        string chainId,
        int socketIndex,
        string stoneInstanceId,
        IReadOnlyList<P5SkillChainDefinition> chains)
    {
        P5SkillChainDefinition? chain = chains.FirstOrDefault(item => item.StableId == chainId);
        SkillStoneInstance? stone = _skillStones.FirstOrDefault(item => item.InstanceId == stoneInstanceId);
        if (chain is null || stone is null || socketIndex < 0 || socketIndex >= chain.TotalSockets)
        {
            return false;
        }

        SkillLinkConfiguration? target = _skillLinks.FirstOrDefault(link => link.ChainId == chainId);
        string?[] targetSockets = target is null
            ? new string?[chain.TotalSockets]
            : ResizeSockets(target, chain.TotalSockets);
        if (stone.Definition.Kind == SkillStoneKind.Active && targetSockets
                .Where((_, index) => index != socketIndex)
                .Select(Stone)
                .Any(other => other?.Definition.Kind == SkillStoneKind.Active))
        {
            return false;
        }

        SkillStoneInstance? active = stone.Definition.Kind == SkillStoneKind.Active
            ? stone
            : targetSockets.Select(Stone).FirstOrDefault(item => item?.Definition.Kind == SkillStoneKind.Active);
        if (stone.Definition.Kind == SkillStoneKind.Support && targetSockets
                .Where((_, index) => index != socketIndex)
                .Select(Stone)
                .Any(other => other?.DefinitionId == stone.DefinitionId))
        {
            return false;
        }

        SkillStoneDefinition[] installedSupports = targetSockets
            .Where((_, index) => index != socketIndex)
            .Select(Stone)
            .Where(item => item?.Definition.Kind == SkillStoneKind.Support)
            .Select(item => item!.Definition)
            .ToArray();
        if (stone.Definition.Kind == SkillStoneKind.Active && installedSupports.Any(support =>
                !P6SkillCompatibility.Check(stone.Definition, support).Compatible))
        {
            return false;
        }
        if (active is not null && stone.Definition.Kind == SkillStoneKind.Support &&
            !P6SkillCompatibility.CheckGroup(active.Definition, stone.Definition, installedSupports).Compatible)
        {
            return false;
        }

        RemoveStoneFromAllGroups(stoneInstanceId);
        target = _skillLinks.FirstOrDefault(link => link.ChainId == chainId);
        targetSockets = target is null ? new string?[chain.TotalSockets] : ResizeSockets(target, chain.TotalSockets);
        targetSockets[socketIndex] = stoneInstanceId;
        UpsertSocketLink(target, chainId, targetSockets);
        AddHistory($"{stone.Definition.DisplayName} 已装入 {chain.DisplayName} 第 {socketIndex + 1} 孔。");
        return true;
    }

    public bool UnsocketStone(string chainId, int socketIndex, IReadOnlyList<P5SkillChainDefinition> chains)
    {
        P5SkillChainDefinition? chain = chains.FirstOrDefault(item => item.StableId == chainId);
        SkillLinkConfiguration? link = _skillLinks.FirstOrDefault(item => item.ChainId == chainId);
        if (chain is null || link is null || socketIndex < 0 || socketIndex >= chain.TotalSockets)
        {
            return false;
        }

        string?[] sockets = ResizeSockets(link, chain.TotalSockets);
        if (string.IsNullOrEmpty(sockets[socketIndex]))
        {
            return false;
        }
        sockets[socketIndex] = null;
        UpsertSocketLink(link, chainId, sockets);
        AddHistory("技能石已卸下并返回角色技能石仓库。");
        return true;
    }

    public bool ReplaceSupports(
        string activeStoneInstanceId,
        IReadOnlyList<string> supportStoneInstanceIds,
        int maximumSupports = 5)
    {
        SkillLinkConfiguration? link = _skillLinks.FirstOrDefault(
            item => item.ActiveStoneInstanceId == activeStoneInstanceId);
        if (link is null || maximumSupports is < 0 or > 5 || supportStoneInstanceIds.Count > maximumSupports ||
            supportStoneInstanceIds.Distinct(StringComparer.Ordinal).Count() != supportStoneInstanceIds.Count ||
            supportStoneInstanceIds.Any(id => _skillStones.FirstOrDefault(stone => stone.InstanceId == id)?.Definition.Kind != SkillStoneKind.Support))
        {
            return false;
        }

        foreach (SkillLinkConfiguration other in _skillLinks.Where(item => item != link).ToArray())
        {
            string[] retained = other.SupportStoneInstanceIds
                .Where(id => !supportStoneInstanceIds.Contains(id, StringComparer.Ordinal))
                .ToArray();
            ReplaceLink(other, other with { SupportStoneInstanceIds = retained });
        }

        string?[] sockets = link.SocketStoneInstanceIds is null
            ? new string?[] { link.ActiveStoneInstanceId }.Concat(supportStoneInstanceIds.Cast<string?>()).ToArray()
            : link.SocketStoneInstanceIds.Select(id =>
                id == link.ActiveStoneInstanceId || supportStoneInstanceIds.Contains(id ?? string.Empty, StringComparer.Ordinal)
                    ? id : null).ToArray();
        foreach (string supportId in supportStoneInstanceIds.Where(id => !sockets.Contains(id, StringComparer.Ordinal)))
        {
            int empty = Array.FindIndex(sockets, string.IsNullOrEmpty);
            if (empty < 0)
            {
                break;
            }
            sockets[empty] = supportId;
        }
        ReplaceLink(link, link with
        {
            SupportStoneInstanceIds = supportStoneInstanceIds.ToArray(),
            SocketStoneInstanceIds = sockets,
        });
        return true;
    }

    public int NormalizeSkillChains(IReadOnlyList<P5SkillChainDefinition> chains)
    {
        HashSet<string> valid = chains.Select(item => item.StableId).ToHashSet(StringComparer.Ordinal);
        var occupied = new HashSet<string>(StringComparer.Ordinal);
        int removed = 0;
        foreach (SkillLinkConfiguration original in _skillLinks.OrderBy(item => item.Priority).ToArray())
        {
            SkillStoneInstance? active = string.IsNullOrEmpty(original.ActiveStoneInstanceId)
                ? null
                : _skillStones.FirstOrDefault(item => item.InstanceId == original.ActiveStoneInstanceId);
            string chainId = valid.Contains(original.ChainId) && !occupied.Contains(original.ChainId)
                ? original.ChainId
                : string.Empty;
            P5SkillChainDefinition? chain = chains.FirstOrDefault(item => item.StableId == chainId);
            if (chain is null || occupied.Contains(chainId) || active is not null && !P5SkillChainRules.Accepts(chain, active.Definition))
            {
                chain = chains.FirstOrDefault(candidate => !occupied.Contains(candidate.StableId) &&
                    (active is null || P5SkillChainRules.Accepts(candidate, active.Definition)));
                chainId = chain?.StableId ?? string.Empty;
            }

            if (chain is null)
            {
                removed += SocketIds(original).Count(id => !string.IsNullOrEmpty(id));
                ReplaceLink(original, original with
                {
                    ChainId = string.Empty,
                    ActiveStoneInstanceId = string.Empty,
                    SupportStoneInstanceIds = [],
                    SocketStoneInstanceIds = [],
                });
                continue;
            }

            occupied.Add(chainId);
            string?[] previous = SocketIds(original).ToArray();
            if (original.SocketStoneInstanceIds is null)
            {
                previous = new string?[] { original.ActiveStoneInstanceId }
                    .Concat(original.SupportStoneInstanceIds.Cast<string?>())
                    .ToArray();
            }
            string?[] retained = previous.Take(chain.TotalSockets).ToArray();
            Array.Resize(ref retained, chain.TotalSockets);
            removed += previous.Skip(chain.TotalSockets).Count(id => !string.IsNullOrEmpty(id));
            ReplaceLink(original, BuildLink(original, chainId, retained));
        }

        if (removed > 0)
        {
            AddHistory($"装备技能链容量变化：{removed} 个辅助技能石已安全解除连接。");
        }

        return removed;
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
            SocketStoneInstanceIds = link.SocketStoneInstanceIds?.Select(id => id == supportStoneInstanceId ? null : id).ToArray(),
        });
        AddHistory("辅助技能石已解除连接。");
        return true;
    }

    public void AddSkillExperience(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        IReadOnlySet<string> installed = EffectiveInstalledStoneIds();
        for (int index = 0; index < _skillStones.Count; index++)
        {
            SkillStoneInstance stone = _skillStones[index];
            if (!installed.Contains(stone.InstanceId) || stone.Level >= 20)
            {
                continue;
            }
            int total = checked(stone.Experience + amount);
            int level = Math.Min(20, 1 + total / 1_000);
            _skillStones[index] = stone with { Level = level, Experience = level >= 20 ? Math.Min(total, 19_000) : total };
        }
    }

    public bool ConfigureSkill(
        string activeStoneInstanceId,
        int priority,
        SkillAiRule aiRule,
        bool reservationEnabled)
    {
        SkillLinkConfiguration? link = _skillLinks.FirstOrDefault(item => item.ActiveStoneInstanceId == activeStoneInstanceId);
        if (link is null || priority is < 1 or > 999)
        {
            return false;
        }
        ReplaceLink(link, link with { Priority = priority, AiRule = aiRule, ReservationEnabled = reservationEnabled });
        return true;
    }

    public void SaveSkillScheme(P6SkillSchemeKind kind)
    {
        _skillSchemes[kind] = CloneLinks(_skillLinks);
        ActiveSkillScheme = kind;
        AddHistory($"已保存{kind}技能方案。");
    }

    public P6SchemeSwitchResult SwitchSkillScheme(
        P6SkillSchemeKind kind,
        IReadOnlyList<P5SkillChainDefinition> chains)
    {
        if (!_skillSchemes.TryGetValue(kind, out IReadOnlyList<SkillLinkConfiguration>? saved))
        {
            return new P6SchemeSwitchResult(false, 0, 0, "技能方案不存在。");
        }
        HashSet<string> owned = _skillStones.Select(stone => stone.InstanceId).ToHashSet(StringComparer.Ordinal);
        int missing = saved.SelectMany(SocketIds).Count(id => !string.IsNullOrEmpty(id) && !owned.Contains(id));
        _skillLinks.Clear();
        _skillLinks.AddRange(CloneLinks(saved).Select(link => link with
        {
            SocketStoneInstanceIds = link.SocketStoneInstanceIds?.Select(id =>
                string.IsNullOrEmpty(id) || owned.Contains(id) ? id : null).ToArray(),
        }));
        int ejected = NormalizeSkillChains(chains);
        ActiveSkillScheme = kind;
        AddHistory($"已切换到{kind}技能方案；缺失 {missing}，弹出 {ejected}。 ");
        return new P6SchemeSwitchResult(true, missing, ejected,
            missing + ejected == 0 ? "技能方案已完整切换。" : $"已恢复可完成部分：缺失 {missing}，弹出 {ejected}。");
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

    public static int SalePrice(ItemInstance item) => P20.P20ItemValue.SalePrice(item);

    private bool Contains(string instanceId) =>
        _sortingBag.Any(item => item.InstanceId == instanceId) ||
        _recovery.Any(item => item.InstanceId == instanceId) ||
        _buyback.Any(entry => entry.Item.InstanceId == instanceId);

    private IReadOnlySet<string> EffectiveInstalledStoneIds()
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (SkillLinkConfiguration link in _skillLinks.Where(link => !string.IsNullOrEmpty(link.ChainId) &&
                     !string.IsNullOrEmpty(link.ActiveStoneInstanceId)))
        {
            SkillStoneInstance? active = Stone(link.ActiveStoneInstanceId);
            if (active is null)
            {
                continue;
            }
            result.Add(active.InstanceId);
            foreach (string supportId in link.SupportStoneInstanceIds)
            {
                SkillStoneInstance? support = Stone(supportId);
                if (support is not null && P6SkillCompatibility.Check(active.Definition, support.Definition).Compatible)
                {
                    result.Add(support.InstanceId);
                }
            }
        }
        return result;
    }

    private void SaveAllSchemesFromCurrent()
    {
        foreach (P6SkillSchemeKind kind in Enum.GetValues<P6SkillSchemeKind>())
        {
            _skillSchemes[kind] = CloneLinks(_skillLinks);
        }
    }

    private static IReadOnlyList<SkillLinkConfiguration> CloneLinks(IEnumerable<SkillLinkConfiguration> links) => links
        .Select(link => link with
        {
            SupportStoneInstanceIds = link.SupportStoneInstanceIds.ToArray(),
            SocketStoneInstanceIds = link.SocketStoneInstanceIds?.ToArray(),
        }).ToArray();

    private void ReplaceLink(SkillLinkConfiguration previous, SkillLinkConfiguration next)
    {
        int index = _skillLinks.IndexOf(previous);
        if (index >= 0)
        {
            _skillLinks[index] = next;
        }
    }

    public SkillStoneInstance AddDroppedSkillStone(ulong seed, bool recordHistory = true, int quality = 0, bool mutated = false,
        IReadOnlySet<string>? preferredDefinitions = null)
    {
        SkillStoneDefinition[] pool = P2SkillStones.DropPool.Where(item => !mutated || item.Kind == SkillStoneKind.Active)
            .Where(item => _skillStones.Count(stone => stone.DefinitionId == item.StableId && stone.Mutated == mutated) < 5)
            .OrderBy(item => item.StableId, StringComparer.Ordinal).ToArray();
        if (pool.Length == 0)
        {
            if (recordHistory) AddHistory("技能石掉落池均已达到同名持有上限 5，未生成额外技能石。");
            return _skillStones.OrderBy(stone => stone.InstanceId, StringComparer.Ordinal).First();
        }
        var random = new Pcg32(seed);
        int Weight(SkillStoneDefinition candidate) => (_skillStones.Any(stone => stone.DefinitionId == candidate.StableId) ? 1 : 3) *
            (preferredDefinitions?.Contains(candidate.StableId) == true ? 4 : 1);
        int totalWeight = pool.Sum(Weight);
        int roll = (int)(random.NextUInt() % (uint)totalWeight);
        SkillStoneDefinition definition = pool[^1];
        foreach (SkillStoneDefinition candidate in pool)
        {
            int weight = Weight(candidate);
            if (roll < weight)
            {
                definition = candidate;
                break;
            }
            roll -= weight;
        }
        string id = $"drop-skill-{seed:x16}-{_operationSequence++:x8}";
        var stone = new SkillStoneInstance(id, definition.StableId, Quality: Math.Clamp(quality, 0, 20), Mutated: mutated);
        _skillStones.Add(stone);
        if (recordHistory) AddHistory($"获得技能石：{definition.DisplayName}。");
        return stone;
    }

    public int HeldSkillStoneCount(string definitionId, bool mutated) =>
        _skillStones.Count(stone => stone.DefinitionId == definitionId && stone.Mutated == mutated);

    private SkillStoneInstance? Stone(string? instanceId) => string.IsNullOrEmpty(instanceId)
        ? null
        : _skillStones.FirstOrDefault(stone => stone.InstanceId == instanceId);

    private static IEnumerable<string?> SocketIds(SkillLinkConfiguration link) =>
        link.SocketStoneInstanceIds ?? new string?[] { link.ActiveStoneInstanceId }
            .Concat(link.SupportStoneInstanceIds.Cast<string?>());

    private static string?[] ResizeSockets(SkillLinkConfiguration link, int count)
    {
        string?[] result = SocketIds(link).Take(count).ToArray();
        Array.Resize(ref result, count);
        return result;
    }

    private SkillLinkConfiguration BuildLink(SkillLinkConfiguration? previous, string chainId, IReadOnlyList<string?> sockets)
    {
        string activeId = sockets.Select(Stone)
            .FirstOrDefault(stone => stone?.Definition.Kind == SkillStoneKind.Active)?.InstanceId ?? string.Empty;
        string[] supports = sockets.Select(Stone)
            .Where(stone => stone?.Definition.Kind == SkillStoneKind.Support)
            .Select(stone => stone!.InstanceId)
            .ToArray();
        return new SkillLinkConfiguration(activeId, supports, previous?.Priority ?? _skillLinks.Count + 1,
            chainId, sockets.ToArray(), previous?.AiRule, previous?.ReservationEnabled ?? true);
    }

    private void UpsertSocketLink(SkillLinkConfiguration? previous, string chainId, IReadOnlyList<string?> sockets)
    {
        SkillLinkConfiguration next = BuildLink(previous, chainId, sockets);
        if (previous is null)
        {
            _skillLinks.Add(next);
        }
        else
        {
            ReplaceLink(previous, next);
        }
    }

    private void RemoveStoneFromAllGroups(string instanceId)
    {
        foreach (SkillLinkConfiguration link in _skillLinks.ToArray())
        {
            string?[] sockets = SocketIds(link).Select(id => id == instanceId ? null : id).ToArray();
            if (!sockets.SequenceEqual(SocketIds(link)))
            {
                ReplaceLink(link, BuildLink(link, link.ChainId, sockets));
            }
        }
    }

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
