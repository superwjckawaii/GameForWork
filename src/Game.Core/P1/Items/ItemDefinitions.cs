using GameForWork.Core.P1.Combat;
using GameForWork.Core.Equipment;

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

public enum WeaponFamily
{
    None,
    Sword,
    Axe,
    Mace,
    Dagger,
    Bow,
    Wand,
    Runeblade,
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
    string ImplicitText = "",
    IReadOnlyList<ItemBaseImplicit>? AdditionalImplicits = null,
    int SpiritBarrier = 0,
    ItemModifierScope ImplicitScope = ItemModifierScope.Global,
    int SpiritBarrierMinimum = 0,
    int SpiritBarrierMaximum = 0)
{
    public IReadOnlyList<string> ItemTags => Tags ?? Array.Empty<string>();
    public IReadOnlyList<ItemBaseImplicit> ExtraImplicits => AdditionalImplicits ?? Array.Empty<ItemBaseImplicit>();

    public WeaponFamily WeaponFamily => ItemTags switch
    {
        var tags when tags.Contains("runeblade", StringComparer.Ordinal) => WeaponFamily.Runeblade,
        var tags when tags.Contains("dagger", StringComparer.Ordinal) => WeaponFamily.Dagger,
        var tags when tags.Contains("bow", StringComparer.Ordinal) => WeaponFamily.Bow,
        var tags when tags.Contains("wand", StringComparer.Ordinal) => WeaponFamily.Wand,
        var tags when tags.Contains("axe", StringComparer.Ordinal) => WeaponFamily.Axe,
        var tags when tags.Contains("mace", StringComparer.Ordinal) => WeaponFamily.Mace,
        var tags when tags.Contains("sword", StringComparer.Ordinal) || tags.Contains("rapier", StringComparer.Ordinal) => WeaponFamily.Sword,
        _ => WeaponFamily.None,
    };

    public string DetailedTypeName => (Category, WeaponFamily) switch
    {
        (ItemCategory.TwoHandWeapon, WeaponFamily.Sword) => "双手剑",
        (ItemCategory.TwoHandWeapon, WeaponFamily.Axe) => "双手斧",
        (ItemCategory.TwoHandWeapon, WeaponFamily.Mace) => "双手锤",
        (ItemCategory.TwoHandWeapon, WeaponFamily.Bow) => "弓",
        (ItemCategory.OneHandWeapon, WeaponFamily.Sword) => "单手剑",
        (ItemCategory.OneHandWeapon, WeaponFamily.Axe) => "单手斧",
        (ItemCategory.OneHandWeapon, WeaponFamily.Mace) => "单手锤",
        (ItemCategory.OneHandWeapon, WeaponFamily.Dagger) => "匕首",
        (ItemCategory.OneHandWeapon, WeaponFamily.Wand) => "法杖",
        (ItemCategory.OneHandWeapon, WeaponFamily.Runeblade) => "符刃",
        _ => Category.ToString(),
    };

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
    public static IReadOnlyCollection<ItemBaseDefinition> All => EquipmentCatalog.Bases;

    public static ItemBaseDefinition Get(string stableId) => EquipmentCatalog.GetBase(stableId);
}

public enum ItemModifierScope
{
    Global,
    LocalWeapon,
    LocalDefense,
    LocalBlock,
    Flask,
    Rule,
}

public sealed record ItemBaseImplicit(
    ItemModifierKind ModifierKind,
    int Value,
    string DisplayText,
    ItemModifierScope Scope = ItemModifierScope.Global);

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
    IncreasedCooldownRecoveryBasisPoints,
    IncreasedFlaskChargeGainBasisPoints,
    IncreasedFlaskDurationBasisPoints,
    IncreasedMaximumLifeBasisPoints,
    IncreasedMaximumManaBasisPoints,
    IncreasedMaximumShieldBasisPoints,
    MaximumAllResistanceBasisPoints,
    MoreRareBossDamageBasisPoints,
    ActiveSkillGemLevels,
    SupportSkillGemLevels,
    AddedMinimumPhysicalDamage,
    AddedMaximumPhysicalDamage,
    AddedMinimumFireDamage,
    AddedMaximumFireDamage,
    AddedMinimumColdDamage,
    AddedMaximumColdDamage,
    AddedMinimumLightningDamage,
    AddedMaximumLightningDamage,
    AddedMinimumVoidDamage,
    AddedMaximumVoidDamage,
    FlatArmor,
    FlatEvasion,
    FlatShield,
    FlatSpiritBarrier,
    IncreasedSpiritBarrierBasisPoints,
    IncreasedLocalBlockBasisPoints,
    IncreasedAttackDamageBasisPoints,
    IncreasedSpellDamageBasisPoints,
    IncreasedElementalDamageBasisPoints,
    IncreasedFireDamageBasisPoints,
    IncreasedColdDamageBasisPoints,
    IncreasedLightningDamageBasisPoints,
    IncreasedVoidDamageBasisPoints,
    IncreasedMeleeDamageBasisPoints,
    IncreasedProjectileDamageBasisPoints,
    IncreasedAreaDamageBasisPoints,
    IncreasedDamageOverTimeBasisPoints,
    DamageOverTimeMultiplierBasisPoints,
    IncreasedBleedDamageBasisPoints,
    IncreasedPoisonDamageBasisPoints,
    IncreasedIgniteDamageBasisPoints,
    FasterBleedBasisPoints,
    FasterPoisonBasisPoints,
    FasterIgniteBasisPoints,
    IncreasedCriticalMultiplierBasisPoints,
    IncreasedCastSpeedBasisPoints,
    FirePenetrationBasisPoints,
    ColdPenetrationBasisPoints,
    LightningPenetrationBasisPoints,
    VoidPenetrationBasisPoints,
    BleedChanceBasisPoints,
    PoisonChanceBasisPoints,
    IgniteChanceBasisPoints,
    ShockChanceBasisPoints,
    ChillEffectBasisPoints,
    FreezeEffectBasisPoints,
    ShockEffectBasisPoints,
    ProjectileSpeedBasisPoints,
    SkillAreaBasisPoints,
    SkillRangeBasisPoints,
    AdditionalProjectile,
    AdditionalChain,
    AdditionalStrikeTarget,
    AdditionalPierce,
    MaximumLifeRegenerationBasisPoints,
    MaximumShieldRegenerationBasisPoints,
    IncreasedResourceRecoveryRateBasisPoints,
    ReducedShieldRechargeDelayBasisPoints,
    PhysicalResistanceBasisPoints,
    SpellSuppressionEffectBasisPoints,
    AilmentAvoidanceBasisPoints,
    ReducedCurseEffectBasisPoints,
    ReducedDebuffDurationBasisPoints,
    MaximumFireResistanceBasisPoints,
    MaximumColdResistanceBasisPoints,
    MaximumLightningResistanceBasisPoints,
    MaximumVoidResistanceBasisPoints,
    LifeLeechBasisPoints,
    ManaLeechBasisPoints,
    ShieldLeechBasisPoints,
    IncreasedLeechRecoveryRateBasisPoints,
    IncreasedMaximumLeechRateBasisPoints,
    LifeOnHit,
    ManaOnHit,
    ShieldOnHit,
    PhysicalToFireConversionBasisPoints,
    PhysicalToColdConversionBasisPoints,
    PhysicalToLightningConversionBasisPoints,
    PhysicalToVoidConversionBasisPoints,
    ColdToFireConversionBasisPoints,
    LightningToFireConversionBasisPoints,
    FireToVoidConversionBasisPoints,
    ColdToVoidConversionBasisPoints,
    LightningToVoidConversionBasisPoints,
    PhysicalAsExtraFireBasisPoints,
    PhysicalAsExtraColdBasisPoints,
    PhysicalAsExtraLightningBasisPoints,
    ElementalAsExtraVoidBasisPoints,
    ReservationEfficiencyBasisPoints,
    IncreasedAuraEffectBasisPoints,
    IncreasedCurseEffectBasisPoints,
    IncreasedCurseDurationBasisPoints,
    IncreasedCurseRangeBasisPoints,
    IncreasedWarcryEffectBasisPoints,
    IncreasedWarcryRangeBasisPoints,
    IncreasedTemporaryBuffEffectBasisPoints,
    IncreasedTemporaryBuffDurationBasisPoints,
    AllActiveSkillGemLevels,
    AllSupportSkillGemLevels,
    AdditionalUnitMaximum,
    IncreasedMinionDamageBasisPoints,
    IncreasedMinionLifeBasisPoints,
    IncreasedMinionSpeedBasisPoints,
    IncreasedConstructDamageBasisPoints,
    IncreasedConstructLifeBasisPoints,
    IncreasedConstructRebuildRateBasisPoints,
    IncreasedCompanionDamageBasisPoints,
    IncreasedCompanionLifeBasisPoints,
    IncreasedCompanionReviveRateBasisPoints,
    IncreasedTrapDamageBasisPoints,
    IncreasedTrapDeploymentSpeedBasisPoints,
    IncreasedTrapRangeBasisPoints,
    PhantomDamageRatioBasisPoints,
    IncreasedPhantomDurationBasisPoints,
    AdditionalMinionMaximum,
    AdditionalConstructMaximum,
    AdditionalTrapMaximum,
    AdditionalPhantomMaximum,
    HumilityMaximum,
    ArroganceMaximum,
    RageMaximum,
    TemperanceMaximum,
    MercyMaximum,
    SlothMaximum,
    HoldHumilityAtMaximum,
    HoldArroganceAtMaximum,
    HoldRageAtMaximum,
    HoldTemperanceAtMaximum,
    HoldMercyAtMaximum,
    HoldSlothAtMaximum,
    IncreasedPhysiqueBasisPoints,
    IncreasedDexterityBasisPoints,
    IncreasedSpiritBasisPoints,
    IncreasedEnergyBasisPoints,
    IncreasedAllAttributesBasisPoints,
    AdditionalCoreSkillCapacity,
    IncreasedFlaskRecoveryAmountBasisPoints,
    IncreasedFlaskRecoveryRateBasisPoints,
    IncreasedFlaskChargesPerUseBasisPoints,
    InstantFlaskRecoveryPortionBasisPoints,
    FlaskRecoveryAtEnd,
    FlaskLifeRemovedFromManaBasisPoints,
    FlaskManaRemovedFromLifeBasisPoints,
    FlaskBuffArmorBasisPoints,
    FlaskBuffEvasionBasisPoints,
    FlaskBuffCriticalChanceBasisPoints,
    FlaskBuffMovementSpeedBasisPoints,
    MoreSpellDamageBasisPoints,
    MoreElementalDamageBasisPoints,
    MoreVoidDamageBasisPoints,
    MoreLocalShieldBasisPoints,
    AttackBlockChanceBasisPoints,
    SpellBlockChanceBasisPoints,
    MaximumVoidResistanceBonusBasisPoints,
    ReturnProjectiles,
    TrapRearm,
    MinionAutomaticResummon,
    AdditionalCurseMaximum,
    CompanionCheatDeath,
    ConstructExplodeAndRebuild,
    UnarmedDefenseToMoreDamage,
    RunebladeAttackSpellBridge,
    FlaskCleanseBleedPoison,
    FlaskCleanseElementalAilments,
    FlaskCleanseCurses,
    FlaskOverflowCharges,
    FlaskRepeatEffect,
    VirtueViceGainChanceBasisPoints,
    BaseImplicitRule,
}

public sealed record AffixModifierComponent(
    ItemModifierKind Kind,
    int MinimumValue,
    int MaximumValue,
    ItemModifierScope Scope = ItemModifierScope.Global,
    string DisplayText = "");

public sealed record RolledAffixComponent(
    ItemModifierKind Kind,
    int Value,
    ItemModifierScope Scope = ItemModifierScope.Global,
    string DisplayText = "");

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
    string Source = "Natural",
    IReadOnlyList<AffixModifierComponent>? Components = null,
    IReadOnlyList<string>? RequiredBaseTags = null)
{
    public string MutualExclusionGroup => string.IsNullOrWhiteSpace(GroupId) ? StableFamilyId : GroupId;

    public bool Supports(ItemBaseDefinition itemBase) =>
        (ApplicableCategories?.Contains(itemBase.Category) ?? Category == itemBase.Category) &&
        (!string.Equals(Source, "P24Special", StringComparison.Ordinal) || string.Equals(itemBase.SourceId, "P24", StringComparison.Ordinal)) &&
        (RequiredBaseTags is null || RequiredBaseTags.Count == 0 || RequiredBaseTags.Any(tag => itemBase.ItemTags.Contains(tag, StringComparer.Ordinal))) &&
        WeightFor(itemBase) > 0;

    public IReadOnlyList<AffixModifierComponent> EffectComponents => Components is { Count: > 0 }
        ? Components
        : [new AffixModifierComponent(ModifierKind, MinimumValue, MaximumValue,
            Local ? ItemModifierScope.LocalDefense : ItemModifierScope.Global, RawText)];

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

public sealed record AffixRoll(
    AffixDefinition Definition,
    int Value,
    bool Crafted = false,
    IReadOnlyList<RolledAffixComponent>? Components = null)
{
    private bool IsLegacyUnderscaledAttribute =>
        Definition.StableFamilyId.StartsWith("core.affix.", StringComparison.Ordinal) &&
        Definition.ModifierKind is ItemModifierKind.Physique or ItemModifierKind.Dexterity or
            ItemModifierKind.Spirit or ItemModifierKind.Energy &&
        Value <= 3;

    public int EffectiveValue => IsLegacyUnderscaledAttribute
        ? Definition.Tier == 1 ? 51 + Math.Max(0, Value - 2) * 4 : 8 + Math.Max(0, Value - 1) * 4
        : Value;

    public int EffectiveMinimumValue => IsLegacyUnderscaledAttribute ? Definition.Tier == 1 ? 51 : 8 : Definition.MinimumValue;

    public int EffectiveMaximumValue => IsLegacyUnderscaledAttribute ? Definition.Tier == 1 ? 55 : 12 : Definition.MaximumValue;

    public IReadOnlyList<RolledAffixComponent> Effects => Components is { Count: > 0 }
        ? Components
        : [new RolledAffixComponent(
            Definition.ModifierKind,
            EffectiveValue,
            Definition.EffectComponents[0].Scope,
            Definition.EffectComponents[0].DisplayText)];
}

public sealed record ItemEnchantment(
    string StableId,
    string DisplayName,
    ItemModifierKind ModifierKind,
    int Value,
    int WorkshopLevel,
    int GoldCost,
    ItemModifierScope Scope = ItemModifierScope.Global,
    IReadOnlyList<AffixModifierComponent>? Components = null,
    IReadOnlyList<ItemCategory>? ApplicableCategories = null,
    IReadOnlyList<string>? RequiredTags = null)
{
    public IReadOnlyList<AffixModifierComponent> EffectComponents => Components is { Count: > 0 }
        ? Components
        : [new AffixModifierComponent(ModifierKind, Value, Value, Scope, DisplayName)];

    public bool Supports(ItemBaseDefinition itemBase) =>
        (ApplicableCategories is null || ApplicableCategories.Contains(itemBase.Category)) &&
        (RequiredTags is null || RequiredTags.Count == 0 || RequiredTags.Any(tag => itemBase.ItemTags.Contains(tag, StringComparer.Ordinal)));
}

public sealed record LegendaryRule(
    string StableId,
    int HeavyStrikeAttackSpeedMultiplierBasisPoints,
    int AftershockDamageMultiplierBasisPoints,
    string DisplayText = "");

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
    string RolledName = "",
    string DropSource = "",
    int RolledBaseArmor = 0,
    int RolledBaseEvasion = 0,
    int RolledBaseShield = 0,
    int RolledBaseSpiritBarrier = 0,
    IReadOnlyList<RolledAffixComponent>? RolledImplicitComponents = null,
    bool ProtectPrefixesNextCraft = false,
    bool ProtectSuffixesNextCraft = false,
    long CraftSequence = 0,
    string LegendaryCatalogId = "",
    string CorruptionImplicitId = "")
{
    public string DisplayName => string.IsNullOrWhiteSpace(RolledName) ? Base.DisplayName : RolledName;
    public int PrefixCount => Affixes.Count(affix => affix.Definition.Position == AffixPosition.Prefix);
    public int SuffixCount => Affixes.Count(affix => affix.Definition.Position == AffixPosition.Suffix);
    public int ExtraSupportLinkCapacity => Affixes
        .SelectMany(affix => affix.Effects)
        .Where(effect => effect.Kind == ItemModifierKind.ExtraSupportLinkCapacity)
        .Select(effect => effect.Value)
        .DefaultIfEmpty()
        .Max();
    public int EffectiveImplicitValue => Base.ImplicitModifier == ItemModifierKind.None
        ? 0
        : ImplicitValue > 0 ? ImplicitValue : Base.ImplicitMinimumValue;
    public int EffectiveBaseArmor => RolledBaseArmor > 0 ? RolledBaseArmor : Base.Armor;
    public int EffectiveBaseEvasion => RolledBaseEvasion > 0 ? RolledBaseEvasion : Base.Evasion;
    public int EffectiveBaseShield => RolledBaseShield > 0 ? RolledBaseShield : Base.Shield;
    public int EffectiveBaseSpiritBarrier => RolledBaseSpiritBarrier > 0 ? RolledBaseSpiritBarrier : Base.SpiritBarrier;
    public IReadOnlyList<RolledAffixComponent> EffectiveImplicitComponents => RolledImplicitComponents is { Count: > 0 }
        ? RolledImplicitComponents
        : Base.ImplicitModifier == ItemModifierKind.None
            ? []
            : [new RolledAffixComponent(Base.ImplicitModifier, EffectiveImplicitValue, Base.ImplicitScope, Base.ImplicitText)];

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
        AftershockDamageMultiplierBasisPoints: 7_000,
        DisplayText: "重击攻击速度总降30%；命中后产生一次造成原伤害70%的余震");

    public static ItemInstance Create(int itemLevel) =>
        EquipmentLegendaryFactory.CreateByName(
            "回响破誓者",
            itemLevel,
            $"legendary-{itemLevel}-echoing-oathbreaker") with
        {
            LegendaryRule = EchoingOathbreakerRule,
        };
}
