using GameForWork.Core.P1.Combat;
using GameForWork.Core.P19;

namespace GameForWork.Core.P1.Items;

public enum EquipmentSlot
{
    MainHand,
    OffHand,
    Chest,
    Helmet,
    Gloves,
    Boots,
    Belt,
    Amulet,
    RingLeft,
    RingRight,
    Flask1,
    Flask2,
    Flask3,
    Flask4,
    Flask5,
}

public enum ItemCategory
{
    TwoHandWeapon = 0,
    BodyArmor = 1,
    Helmet = 2,
    Ring = 3,
    LifeFlask = 4,
    Gloves = 5,
    Boots = 6,
    Belt = 7,
    Amulet = 8,
    OneHandWeapon = 9,
    Shield = 10,
}

public enum ItemRarity
{
    Basic,
    Magic,
    Rare,
    Legendary,
}

public sealed record ItemBaseDefinition(
    string StableId,
    string DisplayName,
    ItemCategory Category,
    EquipmentSlot PrimarySlot,
    int MinimumPhysicalDamage = 0,
    int MaximumPhysicalDamage = 0,
    int AttacksPerSecondMilli = 0,
    int CriticalChanceBasisPoints = 0,
    int Armor = 0,
    int Evasion = 0,
    int Shield = 0,
    int CoreSkillCapacity = 0,
    int SupportLinkCapacity = 0,
    ItemModifierKind ImplicitModifier = ItemModifierKind.None,
    int ImplicitMinimumValue = 0,
    int ImplicitMaximumValue = 0,
    int RequiredLevel = 1,
    int RequiredPhysique = 0,
    int RequiredDexterity = 0,
    int RequiredSpirit = 0,
    int RequiredEnergy = 0,
    string SourceId = "",
    IReadOnlyList<string>? Tags = null,
    int ArmorMinimum = 0,
    int ArmorMaximum = 0,
    int EvasionMinimum = 0,
    int EvasionMaximum = 0,
    int ShieldMinimum = 0,
    int ShieldMaximum = 0,
    int BlockChanceBasisPoints = 0,
    int MovementPenaltyBasisPoints = 0,
    int SocketLimit = 0,
    string ImplicitText = "")
{
    public IReadOnlyList<string> ItemTags => Tags ?? Array.Empty<string>();

    public bool MeetsRequirements(int level, CharacterAttributes attributes) =>
        level >= RequiredLevel &&
        attributes.Physique >= RequiredPhysique &&
        attributes.Dexterity >= RequiredDexterity &&
        attributes.Spirit >= RequiredSpirit &&
        attributes.Energy >= RequiredEnergy;

    public WeaponProfile ToWeaponProfile() => Category is ItemCategory.TwoHandWeapon or ItemCategory.OneHandWeapon
        ? new WeaponProfile(
            StableId,
            MinimumPhysicalDamage,
            MaximumPhysicalDamage,
            AttacksPerSecondMilli,
            CriticalChanceBasisPoints)
        : throw new InvalidOperationException($"{StableId} is not a weapon.");
}

public static class P1ItemBases
{
    private static readonly IReadOnlyDictionary<string, ItemBaseDefinition> BaseMap = Build()
        .ToDictionary(item => item.StableId, StringComparer.Ordinal);

    public static IReadOnlyCollection<ItemBaseDefinition> All => BaseMap.Values.ToArray();

    public static ItemBaseDefinition Get(string stableId) =>
        BaseMap.TryGetValue(stableId, out ItemBaseDefinition? definition)
            ? definition
            : throw new KeyNotFoundException($"Unknown item base: {stableId}");

    private static IReadOnlyList<ItemBaseDefinition> Build() => P19Catalog.Bases;
}

public enum ItemModifierKind
{
    None,
    AddedPhysicalDamage,
    IncreasedPhysicalDamageBasisPoints,
    FlatAccuracy,
    IncreasedAttackSpeedBasisPoints,
    IncreasedCriticalChanceBasisPoints,
    IncreasedBleedChanceBasisPoints,
    Physique,
    Spirit,
    FlatMaximumLife,
    FlatMaximumMana,
    IncreasedArmorBasisPoints,
    IncreasedEvasionBasisPoints,
    IncreasedShieldBasisPoints,
    IncreasedLifeFlaskEffectBasisPoints,
    ExtraSupportLinkCapacity,
    IncreasedManaRegenerationBasisPoints,
    Dexterity,
    Energy,
    FireResistanceBasisPoints,
    ColdResistanceBasisPoints,
    LightningResistanceBasisPoints,
    VoidResistanceBasisPoints,
    IncreasedMovementSpeedBasisPoints,
    BlockChanceBasisPoints,
    SpellSuppressionBasisPoints,
    FlatLifeRegeneration,
}

public enum AffixPosition
{
    Prefix,
    Suffix,
}

public sealed record AffixDefinition(
    string StableFamilyId,
    string DisplayName,
    ItemCategory Category,
    AffixPosition Position,
    int Tier,
    int MinimumItemLevel,
    int MinimumValue,
    int MaximumValue,
    int Weight,
    ItemModifierKind ModifierKind,
    string GroupId = "",
    IReadOnlyList<ItemCategory>? ApplicableCategories = null,
    IReadOnlyDictionary<string, int>? TagWeights = null,
    string SourceId = "",
    string RawText = "",
    IReadOnlyList<string>? ModTags = null,
    bool Local = false,
    string Source = "Natural")
{
    public string MutualExclusionGroup => string.IsNullOrWhiteSpace(GroupId) ? StableFamilyId : GroupId;

    public bool Supports(ItemBaseDefinition itemBase) =>
        (ApplicableCategories?.Contains(itemBase.Category) ?? Category == itemBase.Category) && WeightFor(itemBase) > 0;

    public int WeightFor(ItemBaseDefinition itemBase)
    {
        if (TagWeights is null || TagWeights.Count == 0) return Weight;
        int resolved = itemBase.ItemTags
            .Where(TagWeights.ContainsKey)
            .Select(tag => TagWeights[tag])
            .DefaultIfEmpty(0)
            .Max();
        return resolved;
    }
}

public sealed record AffixRoll(AffixDefinition Definition, int Value, bool Crafted = false);

public sealed record ItemEnchantment(
    string StableId,
    string DisplayName,
    ItemModifierKind ModifierKind,
    int Value,
    int WorkshopLevel,
    int GoldCost);

public sealed record LegendaryRule(
    string StableId,
    int HeavyStrikeAttackSpeedMultiplierBasisPoints,
    int AftershockDamageMultiplierBasisPoints);

public sealed record ItemInstance(
    string InstanceId,
    ItemBaseDefinition Base,
    int ItemLevel,
    ItemRarity Rarity,
    IReadOnlyList<AffixRoll> Affixes,
    LegendaryRule? LegendaryRule = null,
    bool IsIdentified = true,
    int ImplicitValue = 0,
    bool IsLocked = false,
    bool IsCraftingBase = false,
    int LinkedSocketCount = 0,
    string FracturedAffixFamilyId = "",
    int Quality = 0,
    ItemEnchantment? Enchantment = null,
    bool IsCorrupted = false,
    string CorruptionOutcome = "",
    bool IsKeyItem = false,
    string RolledName = "")
{
    public string DisplayName => string.IsNullOrWhiteSpace(RolledName) ? Base.DisplayName : RolledName;
    public int PrefixCount => Affixes.Count(affix => affix.Definition.Position == AffixPosition.Prefix);
    public int SuffixCount => Affixes.Count(affix => affix.Definition.Position == AffixPosition.Suffix);
    public int ExtraSupportLinkCapacity => Affixes
        .Where(affix => affix.Definition.ModifierKind == ItemModifierKind.ExtraSupportLinkCapacity)
        .Select(affix => affix.Value)
        .DefaultIfEmpty()
        .Max();

    public ItemInstance WithLocked(bool locked) => this with { IsLocked = locked };

    public ItemInstance WithCraftingBase(bool marked) => this with { IsCraftingBase = marked };

    public bool IsFractured(AffixRoll affix) =>
        string.Equals(FracturedAffixFamilyId, affix.Definition.StableFamilyId, StringComparison.Ordinal);

    public bool CanModify => !IsLocked && !IsCorrupted;
}

public static class P1Legendary
{
    public static readonly LegendaryRule EchoingOathbreakerRule = new(
        "core.legendary_rule.echoing_oathbreaker",
        HeavyStrikeAttackSpeedMultiplierBasisPoints: 7_000,
        AftershockDamageMultiplierBasisPoints: 7_000);

    public static ItemInstance Create(int itemLevel) => new(
        $"legendary-{itemLevel}-echoing-oathbreaker",
        P1ItemBases.Get("core.base.heavy_battleaxe"),
        Math.Clamp(itemLevel, 1, 10),
        ItemRarity.Legendary,
        Array.Empty<AffixRoll>(),
        EchoingOathbreakerRule,
        LinkedSocketCount: 5,
        RolledName: "回响破誓者");
}
