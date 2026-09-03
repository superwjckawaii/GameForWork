using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using GameForWork.Core.P1.Items;

namespace GameForWork.Core.Equipment;

/// <summary>
/// The only production equipment definition entry point.  Human authoring may be split,
/// but the game loads this validated snapshot once and never branches on milestone names.
/// </summary>
public static class EquipmentCatalog
{
    private const string ResourceSuffix = "Equipment.Data.equipment_catalog.json";
    private static readonly EquipmentCatalogSnapshot SnapshotValue = Load();
    private static readonly IReadOnlyDictionary<string, ItemBaseDefinition> BaseById = BuildBases();
    private static readonly IReadOnlyDictionary<string, string> BaseAliases = BuildBaseAliases();
    private static readonly IReadOnlyList<AffixDefinition> AffixValues = BuildAffixes();
    private static readonly IReadOnlyDictionary<string, string> AffixAliases = BuildAffixAliases();

    public static EquipmentCatalogSnapshot Snapshot => SnapshotValue;
    public static IReadOnlyCollection<ItemBaseDefinition> Bases => BaseById.Values.ToArray();
    public static IReadOnlyList<AffixDefinition> Affixes => AffixValues;
    public static IReadOnlyList<EquipmentEnchantmentEntry> Enchantments => SnapshotValue.Enchantments;
    public static IReadOnlyList<EquipmentLegendaryEntry> LegendaryItems => SnapshotValue.LegendaryItems;
    public static IReadOnlyList<EquipmentCraftingOperationEntry> CraftingOperations => SnapshotValue.CraftingOperations;
    public static IReadOnlyList<EquipmentCorruptionImplicitEntry> CorruptionImplicits => SnapshotValue.CorruptionImplicits;

    public static ItemBaseDefinition GetBase(string id)
    {
        string canonical = BaseAliases.GetValueOrDefault(id, id);
        return BaseById.TryGetValue(canonical, out ItemBaseDefinition? value)
            ? value
            : throw new KeyNotFoundException($"Unknown item base: {id}");
    }

    public static string ResolveBaseId(string id) => BaseAliases.GetValueOrDefault(id, id);
    public static string ResolveAffixId(string id) => AffixAliases.GetValueOrDefault(id, id);

    public static AffixDefinition GetAffix(string id, int tier)
    {
        string canonical = ResolveAffixId(id);
        return AffixValues.Single(value => value.StableFamilyId == canonical && value.Tier == tier);
    }

    private static EquipmentCatalogSnapshot Load()
    {
        Assembly assembly = typeof(EquipmentCatalog).Assembly;
        string resource = assembly.GetManifestResourceNames().Single(name => name.EndsWith(ResourceSuffix, StringComparison.Ordinal));
        using Stream stream = assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Missing embedded equipment catalog {resource}.");
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };
        EquipmentCatalogSnapshot snapshot = JsonSerializer.Deserialize<EquipmentCatalogSnapshot>(stream, options)
            ?? throw new InvalidOperationException("Equipment catalog is empty.");
        Validate(snapshot);
        return snapshot;
    }

    private static void Validate(EquipmentCatalogSnapshot snapshot)
    {
        if (snapshot.SchemaVersion != 1 || snapshot.ContentId != "equipment.catalog")
            throw new InvalidOperationException("Unsupported equipment catalog schema.");
        Require(snapshot.Bases, 244, value => value.Id, "bases");
        Require(snapshot.AffixFamilies, 212, value => value[0].Id, "affix families");
        if (snapshot.AffixFamilies.Any(family => family.Count == 0 || family.Any(row => row.Id != family[0].Id)))
            throw new InvalidOperationException("An affix family is empty or mixes IDs.");
        Require(snapshot.Enchantments, 41, value => value.Id, "enchantments");
        Require(snapshot.LegendaryItems, 55, value => value.Id, "legendary items");
        Require(snapshot.CraftingOperations, 92, value => value.Id, "crafting operations");
        Require(snapshot.CorruptionImplicits, 37, value => value.Id, "corruption implicits");
    }

    private static void Require<T>(IReadOnlyList<T> rows, int count, Func<T, string> id, string label)
    {
        if (rows.Count != count) throw new InvalidOperationException($"Equipment catalog requires {count} {label}, found {rows.Count}.");
        string[] duplicates = rows.GroupBy(id, StringComparer.Ordinal).Where(group => group.Count() != 1).Select(group => group.Key).ToArray();
        if (duplicates.Length > 0) throw new InvalidOperationException($"Duplicate {label}: {string.Join(", ", duplicates)}");
    }

    private static IReadOnlyDictionary<string, ItemBaseDefinition> BuildBases() => SnapshotValue.Bases
        .Select(ToBaseDefinition)
        .ToDictionary(value => value.StableId, StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, string> BuildBaseAliases() => SnapshotValue.Bases
        .SelectMany(value => value.LegacyIds.Select(alias => KeyValuePair.Create(alias, value.Id)).Append(KeyValuePair.Create(value.Id, value.Id)))
        .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, string> BuildAffixAliases() => SnapshotValue.AffixFamilies
        .Select(family => family[0])
        .SelectMany(value => value.LegacyIds.Select(alias => KeyValuePair.Create(alias, value.Id)).Append(KeyValuePair.Create(value.Id, value.Id)))
        .GroupBy(pair => pair.Key, StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.First().Value, StringComparer.Ordinal);

    private static IReadOnlyList<AffixDefinition> BuildAffixes() => SnapshotValue.AffixFamilies
        .SelectMany(family => family)
        .Select(ToAffixDefinition)
        .OrderBy(value => value.StableFamilyId, StringComparer.Ordinal)
        .ThenBy(value => value.Tier)
        .ToArray();

    private static ItemBaseDefinition ToBaseDefinition(EquipmentBaseEntry value)
    {
        var category = Enum.Parse<ItemCategory>(value.Category);
        var slot = Enum.Parse<EquipmentSlot>(value.PrimarySlot);
        ItemModifierKind implicitKind = ParseEnum(value.ImplicitModifier, ItemModifierKind.None);
        ItemModifierScope implicitScope = ParseEnum(value.ImplicitScope, ItemModifierScope.Global);
        EquipmentComponentEntry? primary = value.ImplicitComponents.FirstOrDefault(component => component.MaximumValue > 0)
            ?? value.ImplicitComponents.FirstOrDefault();
        IReadOnlyList<ItemBaseImplicit> extras = value.ImplicitComponents.Where(component => !ReferenceEquals(component, primary))
            .Select(component => new ItemBaseImplicit(ParseEnum(component.Kind, ItemModifierKind.None), component.MinimumValue,
                component.DisplayText, ParseEnum(component.Scope, ItemModifierScope.Global), component.MaximumValue))
            .ToArray();
        return new ItemBaseDefinition(
            value.Id, value.DisplayName, category, slot,
            value.MinimumPhysicalDamage, value.MaximumPhysicalDamage, value.AttacksPerSecondMilli, value.CriticalChanceBasisPoints,
            Midpoint(value.ArmorMinimum, value.ArmorMaximum), Midpoint(value.EvasionMinimum, value.EvasionMaximum), Midpoint(value.ShieldMinimum, value.ShieldMaximum),
            value.CoreSkillCapacity, value.SupportLinkCapacity,
            primary is null ? implicitKind : ParseEnum(primary.Kind, implicitKind), primary?.MinimumValue ?? 0, primary?.MaximumValue ?? 0,
            value.RequiredLevel, value.RequiredPhysique, value.RequiredDexterity, value.RequiredSpirit, value.RequiredEnergy,
            "equipment.catalog", value.Tags, value.ArmorMinimum, value.ArmorMaximum, value.EvasionMinimum, value.EvasionMaximum,
            value.ShieldMinimum, value.ShieldMaximum, value.BlockChanceBasisPoints, value.MovementPenaltyBasisPoints,
            value.SocketLimit, value.ImplicitText, extras, Midpoint(value.SpiritBarrierMinimum, value.SpiritBarrierMaximum),
            primary is null ? implicitScope : ParseEnum(primary.Scope, implicitScope),
            value.SpiritBarrierMinimum, value.SpiritBarrierMaximum,
            value.ImplicitComponents.Select(component => new ItemBaseImplicit(
                ParseEnum(component.Kind, ItemModifierKind.None), component.MinimumValue, component.DisplayText,
                ParseEnum(component.Scope, ItemModifierScope.Global), component.MaximumValue)).ToArray());
    }

    private static AffixDefinition ToAffixDefinition(EquipmentAffixEntry value)
    {
        ItemCategory[] categories = value.ApplicableCategories.Select(Enum.Parse<ItemCategory>).ToArray();
        AffixModifierComponent[] components = value.Components.Select(component => new AffixModifierComponent(
            ParseEnum(component.Kind, ItemModifierKind.None), component.MinimumValue, component.MaximumValue,
            ParseEnum(component.Scope, ItemModifierScope.Global), component.DisplayText)).ToArray();
        return new AffixDefinition(value.Id, value.DisplayName, categories[0], Enum.Parse<AffixPosition>(value.Position),
            value.Tier, value.MinimumItemLevel, value.MinimumValue, value.MaximumValue, value.Weight,
            ParseEnum(value.ModifierKind, ItemModifierKind.None), value.GroupId, categories, value.TagWeights,
            "equipment.catalog", value.RawText, value.ModTags, value.Local, "Natural", components, value.RequiredBaseTags);
    }

    private static T ParseEnum<T>(string value, T fallback) where T : struct, Enum => Enum.TryParse(value, out T result) ? result : fallback;
    private static int Midpoint(int minimum, int maximum) => minimum == 0 && maximum == 0 ? 0 : (minimum + maximum) / 2;
}

public sealed record EquipmentCatalogSnapshot(
    int SchemaVersion,
    string ContentId,
    IReadOnlyList<EquipmentBaseEntry> Bases,
    IReadOnlyList<IReadOnlyList<EquipmentAffixEntry>> AffixFamilies,
    IReadOnlyList<EquipmentEnchantmentEntry> Enchantments,
    IReadOnlyList<EquipmentLegendaryEntry> LegendaryItems,
    IReadOnlyList<EquipmentCraftingOperationEntry> CraftingOperations,
    IReadOnlyList<EquipmentCorruptionImplicitEntry> CorruptionImplicits);

public sealed record EquipmentBaseEntry(
    string Id, IReadOnlyList<string> LegacyIds, string DisplayName, string Category, string PrimarySlot,
    string EquipmentType, string RequirementText, string BaseStatsText, string ImplicitText,
    int MinimumPhysicalDamage, int MaximumPhysicalDamage, int AttacksPerSecondMilli, int CriticalChanceBasisPoints,
    int ArmorMinimum, int ArmorMaximum, int EvasionMinimum, int EvasionMaximum, int ShieldMinimum, int ShieldMaximum,
    int SpiritBarrierMinimum, int SpiritBarrierMaximum, int BlockChanceBasisPoints, int MovementPenaltyBasisPoints,
    int CoreSkillCapacity, int SupportLinkCapacity, int SocketLimit, int RequiredLevel, int RequiredPhysique,
    int RequiredDexterity, int RequiredSpirit, int RequiredEnergy, IReadOnlyList<string> Tags,
    string ImplicitModifier, string ImplicitScope, IReadOnlyList<EquipmentComponentEntry> ImplicitComponents);

public sealed record EquipmentComponentEntry(string Kind, int MinimumValue, int MaximumValue, string Scope, string DisplayText, int Order = 0);

public sealed record EquipmentAffixEntry(
    string Id, IReadOnlyList<string> LegacyIds, string DisplayName, string Position, int Tier, int MinimumItemLevel,
    int MinimumValue, int MaximumValue, int Weight, string ModifierKind, string GroupId,
    IReadOnlyList<string> ApplicableCategories, IReadOnlyDictionary<string, int>? TagWeights, string RawText,
    IReadOnlyList<string>? ModTags, bool Local, IReadOnlyList<EquipmentComponentEntry> Components,
    IReadOnlyList<string> RequiredBaseTags);

public sealed record EquipmentEnchantmentEntry(
    string Id, IReadOnlyList<string> LegacyIds, string DisplayName, int WorkshopLevel, int GoldCost,
    string ApplicableEquipment, IReadOnlyList<EquipmentComponentEntry> Components, string RuleId, string RuleText);

public sealed record EquipmentLegendaryEntry(
    string Id, IReadOnlyList<string> LegacyIds, string DisplayName, string Rarity, string BaseAndSource,
    string FixedAffixesText, string RuleId, string RuleText);

public sealed record EquipmentCraftingOperationEntry(
    string Id, string DisplayName, string CostText, string TargetText, string RuleText, string Kind);

public sealed record EquipmentCorruptionImplicitEntry(string Id, string DisplayName, string ApplicableEquipment, string EffectText)
{
    // Schema v1 accidentally emitted these two textual columns in reverse. Keep the on-disk
    // compatibility names, but expose unambiguous semantic accessors to every runtime caller.
    public string ModifierText => ApplicableEquipment;
    public string ApplicabilityText => EffectText;
}
