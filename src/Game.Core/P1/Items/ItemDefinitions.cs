using GameForWork.Core.P1.Combat;

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
    int ImplicitMaximumValue = 0)
{
    public WeaponProfile ToWeaponProfile() => Category == ItemCategory.TwoHandWeapon
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

    private static IReadOnlyList<ItemBaseDefinition> Build() =>
    [
        Weapon("core.base.rusted_greatsword", "生锈巨剑", P1Weapons.RustedGreatsword),
        Weapon("core.base.heavy_battleaxe", "沉重战斧", P1Weapons.HeavyBattleaxe),
        Weapon("core.base.pole_warhammer", "长柄战锤", P1Weapons.PoleWarhammer),
        new("core.base.crude_chainmail", "粗制链甲", ItemCategory.BodyArmor, EquipmentSlot.Chest,
            Armor: 30, CoreSkillCapacity: 1, SupportLinkCapacity: 2),
        new("core.base.hide_coat", "兽皮外衣", ItemCategory.BodyArmor, EquipmentSlot.Chest,
            Evasion: 25, CoreSkillCapacity: 1, SupportLinkCapacity: 2),
        new("core.base.runed_robe", "符文长袍", ItemCategory.BodyArmor, EquipmentSlot.Chest,
            Shield: 20, CoreSkillCapacity: 1, SupportLinkCapacity: 2),
        new("core.base.iron_helmet", "铁制盔", ItemCategory.Helmet, EquipmentSlot.Helmet,
            Armor: 15, SupportLinkCapacity: 1),
        new("core.base.hunter_hood", "猎手兜帽", ItemCategory.Helmet, EquipmentSlot.Helmet,
            Evasion: 12, SupportLinkCapacity: 1),
        new("core.base.ash_circlet", "灰纹头冠", ItemCategory.Helmet, EquipmentSlot.Helmet,
            Shield: 10, SupportLinkCapacity: 1),
        new("core.base.iron_gauntlets", "铁鳞护手", ItemCategory.Gloves, EquipmentSlot.Gloves, Armor: 10),
        new("core.base.ritual_gloves", "仪式手套", ItemCategory.Gloves, EquipmentSlot.Gloves, Shield: 8),
        new("core.base.march_boots", "行军铁靴", ItemCategory.Boots, EquipmentSlot.Boots, Armor: 12),
        new("core.base.shadow_treads", "影行短靴", ItemCategory.Boots, EquipmentSlot.Boots, Evasion: 12),
        new("core.base.chain_belt", "锁链腰带", ItemCategory.Belt, EquipmentSlot.Belt,
            ImplicitModifier: ItemModifierKind.FlatMaximumLife, ImplicitMinimumValue: 6, ImplicitMaximumValue: 10),
        new("core.base.ration_belt", "补给腰带", ItemCategory.Belt, EquipmentSlot.Belt,
            ImplicitModifier: ItemModifierKind.IncreasedLifeFlaskEffectBasisPoints,
            ImplicitMinimumValue: 500, ImplicitMaximumValue: 800),
        new("core.base.ember_amulet", "余烬护符", ItemCategory.Amulet, EquipmentSlot.Amulet,
            ImplicitModifier: ItemModifierKind.Physique, ImplicitMinimumValue: 1, ImplicitMaximumValue: 2),
        new("core.base.spirit_amulet", "祷灵护符", ItemCategory.Amulet, EquipmentSlot.Amulet,
            ImplicitModifier: ItemModifierKind.Spirit, ImplicitMinimumValue: 1, ImplicitMaximumValue: 2),
        new("core.base.iron_ring", "铁环", ItemCategory.Ring, EquipmentSlot.RingLeft,
            ImplicitModifier: ItemModifierKind.AddedPhysicalDamage, ImplicitMinimumValue: 1, ImplicitMaximumValue: 2),
        new("core.base.life_ring", "生命戒", ItemCategory.Ring, EquipmentSlot.RingLeft,
            ImplicitModifier: ItemModifierKind.FlatMaximumLife, ImplicitMinimumValue: 8, ImplicitMaximumValue: 8),
        new("core.base.focus_ring", "专注戒", ItemCategory.Ring, EquipmentSlot.RingLeft,
            ImplicitModifier: ItemModifierKind.FlatMaximumMana, ImplicitMinimumValue: 8, ImplicitMaximumValue: 8),
        new("core.base.life_flask", "生命药剂", ItemCategory.LifeFlask, EquipmentSlot.Flask1),
    ];

    private static ItemBaseDefinition Weapon(string id, string name, WeaponProfile weapon) => new(
        id,
        name,
        ItemCategory.TwoHandWeapon,
        EquipmentSlot.MainHand,
        weapon.MinimumPhysicalDamage,
        weapon.MaximumPhysicalDamage,
        weapon.AttacksPerSecondMilli,
        weapon.CriticalChanceBasisPoints,
        CoreSkillCapacity: 1,
        SupportLinkCapacity: 2);
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
    ItemModifierKind ModifierKind);

public sealed record AffixRoll(AffixDefinition Definition, int Value, bool Crafted = false);

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
    string FracturedAffixFamilyId = "")
{
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
        LinkedSocketCount: 5);
}
