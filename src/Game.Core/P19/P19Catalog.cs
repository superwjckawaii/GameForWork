using System.Reflection;
using System.Text.Json;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P30;

namespace GameForWork.Core.P19;

public sealed record P19SourceFile(string Path, string Sha256);

public sealed record P19SourceSnapshot(string Snapshot, string SourceRoot, IReadOnlyList<P19SourceFile> Files);

public static class P19Catalog
{
    private const string ResourceName = "GameForWork.Core.P19.Data.p19_catalog.json";
    private static readonly Snapshot Data = Load();

    public static P19SourceSnapshot Source => new(
        Data.Source.Snapshot,
        Data.Source.SourceRoot,
        Data.Source.Files.Select(file => new P19SourceFile(file.Path, file.Sha256)).ToArray());

    public static IReadOnlyList<ItemBaseDefinition> Bases { get; } = Data.Bases.Select(ToBase).ToArray();

    public static IReadOnlyList<AffixDefinition> Affixes { get; } = Data.Affixes
        .Where(value => !P30EquipmentAffixes.IsRemovedImportedFamily(value.StableFamilyId))
        .Select(ToAffix)
        .Select(P30EquipmentAffixes.NormalizeImported)
        .ToArray();

    private static Snapshot Load()
    {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Missing embedded P19 catalog: {ResourceName}");
        return JsonSerializer.Deserialize<Snapshot>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        }) ?? throw new InvalidDataException("P19 catalog is empty.");
    }

    private static ItemBaseDefinition ToBase(BaseDto value)
    {
        int armor = Midpoint(value.ArmorMinimum, value.ArmorMaximum);
        int evasion = Midpoint(value.EvasionMinimum, value.EvasionMaximum);
        int shield = Midpoint(value.ShieldMinimum, value.ShieldMaximum);
        return new ItemBaseDefinition(
            value.StableId,
            value.DisplayName,
            Enum.Parse<ItemCategory>(value.Category),
            Enum.Parse<EquipmentSlot>(value.PrimarySlot),
            value.MinimumPhysicalDamage,
            value.MaximumPhysicalDamage,
            value.AttacksPerSecondMilli,
            value.CriticalChanceBasisPoints,
            armor,
            evasion,
            shield,
            value.CoreSkillCapacity,
            value.SupportLinkCapacity,
            Enum.Parse<ItemModifierKind>(value.ImplicitModifier),
            value.ImplicitMinimumValue,
            value.ImplicitMaximumValue,
            value.RequiredLevel,
            value.RequiredPhysique,
            value.RequiredDexterity,
            value.RequiredSpirit,
            value.RequiredEnergy,
            value.SourceId,
            value.Tags,
            value.ArmorMinimum,
            value.ArmorMaximum,
            value.EvasionMinimum,
            value.EvasionMaximum,
            value.ShieldMinimum,
            value.ShieldMaximum,
            value.BlockChanceBasisPoints,
            value.MovementPenaltyBasisPoints,
            value.SocketLimit,
            value.ImplicitText);
    }

    private static AffixDefinition ToAffix(AffixDto value) => new(
        value.StableFamilyId,
        value.DisplayName,
        ItemCategory.TwoHandWeapon,
        Enum.Parse<AffixPosition>(value.Position),
        value.Tier,
        value.MinimumItemLevel,
        value.MinimumValue,
        value.MaximumValue,
        value.Weight,
        Enum.Parse<ItemModifierKind>(value.ModifierKind),
        value.GroupId,
        CategoriesFor(value.TagWeights.Keys),
        value.TagWeights,
        value.SourceId,
        value.RawText,
        value.ModTags,
        value.Local,
        value.Source);

    private static IReadOnlyList<ItemCategory> CategoriesFor(IEnumerable<string> tags)
    {
        var result = new HashSet<ItemCategory>();
        foreach (string tag in tags)
        {
            if (tag is "weapon" or "two_hand_weapon" or "twohand" or "sword" or "axe" or "mace" or "staff")
            {
                result.Add(ItemCategory.TwoHandWeapon);
                result.Add(ItemCategory.OneHandWeapon);
            }
            if (tag is "one_hand_weapon" or "onehand") result.Add(ItemCategory.OneHandWeapon);
            if (tag.Contains("shield", StringComparison.Ordinal)) result.Add(ItemCategory.Shield);
            if (tag is "body_armour") result.Add(ItemCategory.BodyArmor);
            if (tag is "helmet") result.Add(ItemCategory.Helmet);
            if (tag is "gloves") result.Add(ItemCategory.Gloves);
            if (tag is "boots") result.Add(ItemCategory.Boots);
            if (tag is "belt") result.Add(ItemCategory.Belt);
            if (tag is "amulet") result.Add(ItemCategory.Amulet);
            if (tag is "ring") result.Add(ItemCategory.Ring);
            if (tag.Contains("flask", StringComparison.Ordinal)) result.Add(ItemCategory.LifeFlask);
            if (tag.Contains("armour", StringComparison.Ordinal))
            {
                result.Add(ItemCategory.BodyArmor);
                result.Add(ItemCategory.Helmet);
                result.Add(ItemCategory.Gloves);
                result.Add(ItemCategory.Boots);
                result.Add(ItemCategory.Shield);
            }
        }
        return result.OrderBy(value => value).ToArray();
    }

    private static int Midpoint(int minimum, int maximum) => maximum > 0 ? (minimum + maximum) / 2 : minimum;

    private sealed class Snapshot
    {
        public required SourceDto Source { get; init; }
        public required BaseDto[] Bases { get; init; }
        public required AffixDto[] Affixes { get; init; }
    }

    private sealed class SourceDto
    {
        public required string Snapshot { get; init; }
        public required string SourceRoot { get; init; }
        public required SourceFileDto[] Files { get; init; }
    }

    private sealed class SourceFileDto
    {
        public required string Path { get; init; }
        public required string Sha256 { get; init; }
    }

    private sealed class BaseDto
    {
        public required string StableId { get; init; }
        public required string DisplayName { get; init; }
        public required string SourceId { get; init; }
        public required string Category { get; init; }
        public required string PrimarySlot { get; init; }
        public int RequiredLevel { get; init; }
        public int RequiredPhysique { get; init; }
        public int RequiredDexterity { get; init; }
        public int RequiredSpirit { get; init; }
        public int RequiredEnergy { get; init; }
        public required string[] Tags { get; init; }
        public int MinimumPhysicalDamage { get; init; }
        public int MaximumPhysicalDamage { get; init; }
        public int AttacksPerSecondMilli { get; init; }
        public int CriticalChanceBasisPoints { get; init; }
        public int ArmorMinimum { get; init; }
        public int ArmorMaximum { get; init; }
        public int EvasionMinimum { get; init; }
        public int EvasionMaximum { get; init; }
        public int ShieldMinimum { get; init; }
        public int ShieldMaximum { get; init; }
        public int BlockChanceBasisPoints { get; init; }
        public int MovementPenaltyBasisPoints { get; init; }
        public int SocketLimit { get; init; }
        public int CoreSkillCapacity { get; init; }
        public int SupportLinkCapacity { get; init; }
        public required string ImplicitText { get; init; }
        public required string ImplicitModifier { get; init; }
        public int ImplicitMinimumValue { get; init; }
        public int ImplicitMaximumValue { get; init; }
    }

    private sealed class AffixDto
    {
        public required string StableFamilyId { get; init; }
        public required string SourceId { get; init; }
        public required string DisplayName { get; init; }
        public required string RawText { get; init; }
        public required string Position { get; init; }
        public required string GroupId { get; init; }
        public int Tier { get; init; }
        public int MinimumItemLevel { get; init; }
        public int MinimumValue { get; init; }
        public int MaximumValue { get; init; }
        public int Weight { get; init; }
        public required string ModifierKind { get; init; }
        public required Dictionary<string, int> TagWeights { get; init; }
        public required string[] ModTags { get; init; }
        public bool Local { get; init; }
        public required string Source { get; init; }
    }
}
