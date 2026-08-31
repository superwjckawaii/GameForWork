using GameForWork.Core.P1.Items;
using GameForWork.Core.P29;

namespace GameForWork.Core.P25;

public static class P25ItemBaseIdentity
{
    private static readonly IReadOnlyDictionary<string, WeaponFamily> WeaponFamilies =
        new Dictionary<string, WeaponFamily>(StringComparer.Ordinal)
        {
            ["core.base.rusted_greatsword"] = WeaponFamily.Sword,
            ["core.base.heavy_battleaxe"] = WeaponFamily.Axe,
            ["core.base.pole_warhammer"] = WeaponFamily.Mace,
            ["core.base.ash_glaive"] = WeaponFamily.Axe,
            ["core.base.warden_maul"] = WeaponFamily.Mace,
            ["core.base.blood_halberd"] = WeaponFamily.Axe,
            ["core.base.glass_greatblade"] = WeaponFamily.Sword,
            ["core.base.oathbreaker_axe"] = WeaponFamily.Axe,
            ["p19.base.headman_s_sword"] = WeaponFamily.Sword,
            ["p19.base.ezomyte_blade"] = WeaponFamily.Sword,
            ["p19.base.imperial_maul"] = WeaponFamily.Mace,
            ["p19.base.void_axe"] = WeaponFamily.Axe,
            ["core.base.rusted_warhammer"] = WeaponFamily.Mace,
            ["p19.base.broad_sword"] = WeaponFamily.Sword,
            ["p19.base.ceremonial_mace"] = WeaponFamily.Mace,
            ["p19.base.cutlass"] = WeaponFamily.Sword,
            ["p19.base.flanged_mace"] = WeaponFamily.Mace,
            ["p19.base.karui_axe"] = WeaponFamily.Axe,
            ["p19.base.butcher_axe"] = WeaponFamily.Axe,
            ["p19.base.harpy_rapier"] = WeaponFamily.Sword,
        };

    public static ItemBaseDefinition Normalize(ItemBaseDefinition item)
    {
        if (!WeaponFamilies.TryGetValue(item.StableId, out WeaponFamily family))
            return item;

        var tags = item.ItemTags.Where(tag => tag is not ("sword" or "rapier" or "axe" or "mace" or "dagger" or "bow" or "wand" or "runeblade"))
            .Append(FamilyTag(family)).Distinct(StringComparer.Ordinal).ToArray();
        return item with { Tags = tags };
    }

    private static string FamilyTag(WeaponFamily family) => family switch
    {
        WeaponFamily.Sword => "sword",
        WeaponFamily.Axe => "axe",
        WeaponFamily.Mace => "mace",
        WeaponFamily.Dagger => "dagger",
        WeaponFamily.Bow => "bow",
        WeaponFamily.Wand => "wand",
        WeaponFamily.Runeblade => "runeblade",
        _ => throw new ArgumentOutOfRangeException(nameof(family)),
    };
}

public static class P25ItemImplicitCatalog
{
    public static ItemBaseDefinition Ensure(ItemBaseDefinition item)
    {
        if (item.ImplicitModifier != ItemModifierKind.None)
            return item with { ImplicitText = item.SourceId == "P29" && !string.IsNullOrWhiteSpace(item.ImplicitText)
                ? item.ImplicitText : ImplicitLabel(item, item.ImplicitModifier) };
        (ItemModifierKind kind, int minimum, int maximum, string text) = Resolve(item);
        return item with
        {
            ImplicitModifier = kind,
            ImplicitMinimumValue = minimum,
            ImplicitMaximumValue = maximum,
            ImplicitText = item.SourceId == "P24" && !string.IsNullOrWhiteSpace(item.ImplicitText)
                ? $"{item.ImplicitText}；{text}"
                : text,
        };
    }

    private static string ImplicitLabel(ItemBaseDefinition item, ItemModifierKind kind)
    {
        string identity = item.WeaponFamily switch
        {
            WeaponFamily.Sword => "剑锋校准",
            WeaponFamily.Axe => "斧刃破甲",
            WeaponFamily.Mace => "重击震荡",
            WeaponFamily.Dagger => "匕首精准",
            WeaponFamily.Bow => "弓术校准",
            WeaponFamily.Wand => "秘术导流",
            WeaponFamily.Runeblade => "符文灌注",
            _ => item.DisplayName,
        };
        string effect = kind switch
        {
            ItemModifierKind.AddedPhysicalDamage => "攻击附加物理伤害",
            ItemModifierKind.IncreasedPhysicalDamageBasisPoints => "物理伤害提高",
            ItemModifierKind.FlatAccuracy => "命中值提高",
            ItemModifierKind.IncreasedAttackSpeedBasisPoints => "攻击速度提高",
            ItemModifierKind.IncreasedCriticalChanceBasisPoints => "暴击率提高",
            ItemModifierKind.Physique => "体魄提高",
            ItemModifierKind.Dexterity => "灵巧提高",
            ItemModifierKind.Spirit => "精神提高",
            ItemModifierKind.Energy => "能量提高",
            ItemModifierKind.FlatMaximumLife => "最大生命提高",
            ItemModifierKind.FlatMaximumMana => "最大法力提高",
            ItemModifierKind.IncreasedArmorBasisPoints => "护甲提高",
            ItemModifierKind.IncreasedEvasionBasisPoints => "闪避提高",
            ItemModifierKind.IncreasedShieldBasisPoints => "最大能量护盾提高",
            ItemModifierKind.FireResistanceBasisPoints => "火焰抗性提高",
            ItemModifierKind.ColdResistanceBasisPoints => "寒霜抗性提高",
            ItemModifierKind.LightningResistanceBasisPoints => "闪电抗性提高",
            ItemModifierKind.VoidResistanceBasisPoints => "虚空抗性提高",
            ItemModifierKind.IncreasedMovementSpeedBasisPoints => "移动速度提高",
            ItemModifierKind.BlockChanceBasisPoints => "格挡概率提高",
            ItemModifierKind.SpellSuppressionBasisPoints => "法术压制提高",
            ItemModifierKind.IncreasedManaRegenerationBasisPoints => "法力恢复提高",
            ItemModifierKind.ExtraSupportLinkCapacity => "连接容量提高",
            _ => "底材属性提高",
        };
        return $"{identity}：{effect}";
    }

    private static (ItemModifierKind Kind, int Minimum, int Maximum, string Text) Resolve(ItemBaseDefinition item)
    {
        IReadOnlyList<string> tags = item.ItemTags;
        bool Has(string tag) => tags.Contains(tag, StringComparer.Ordinal);
        if (Has("bow")) return (ItemModifierKind.IncreasedCriticalChanceBasisPoints, 1_500, 2_500, "弓术暴击率提高");
        if (Has("quiver")) return (ItemModifierKind.AddedPhysicalDamage, 4, 9, "攻击附加物理伤害");
        if (Has("dagger")) return (ItemModifierKind.IncreasedCriticalChanceBasisPoints, 2_000, 3_500, "匕首暴击率提高");
        if (Has("wand")) return (ItemModifierKind.IncreasedManaRegenerationBasisPoints, 1_200, 2_000, "法力恢复提高");
        if (Has("focus") || Has("summoning_focus"))
            return (ItemModifierKind.IncreasedShieldBasisPoints, 1_500, 2_800, "最大能量护盾提高");
        if (Has("unarmed") || Has("wrap"))
            return (ItemModifierKind.IncreasedAttackSpeedBasisPoints, 600, 1_000, "徒手攻击速度提高");
        if (Has("beast_talisman")) return (ItemModifierKind.FlatMaximumLife, 24, 48, "主角与灵兽最大生命提高");
        if (Has("runeblade")) return (ItemModifierKind.IncreasedPhysicalDamageBasisPoints, 1_500, 2_500, "符刃物理伤害提高");
        if (Has("construct_idol")) return (ItemModifierKind.IncreasedArmorBasisPoints, 1_500, 2_800, "护甲与构装耐久提高");
        return item.Category switch
        {
            ItemCategory.TwoHandWeapon => (ItemModifierKind.IncreasedPhysicalDamageBasisPoints, 1_200, 2_500, "物理伤害提高"),
            ItemCategory.OneHandWeapon => (ItemModifierKind.IncreasedCriticalChanceBasisPoints, 1_500, 2_500, "暴击率提高"),
            ItemCategory.Shield => (ItemModifierKind.BlockChanceBasisPoints, 300, 600, "格挡概率提高"),
            ItemCategory.BodyArmor => (ItemModifierKind.FlatMaximumLife, 30, 80, "最大生命提高"),
            ItemCategory.Helmet => (ItemModifierKind.VoidResistanceBasisPoints, 800, 1_600, "虚空抗性提高"),
            ItemCategory.Gloves => (ItemModifierKind.IncreasedAttackSpeedBasisPoints, 500, 1_000, "攻击速度提高"),
            ItemCategory.Boots => (ItemModifierKind.IncreasedMovementSpeedBasisPoints, 500, 1_000, "移动速度提高"),
            ItemCategory.Belt => (ItemModifierKind.FlatMaximumLife, 24, 60, "最大生命提高"),
            ItemCategory.Amulet => (ItemModifierKind.Spirit, 12, 30, "精神提高"),
            ItemCategory.Ring => (ItemModifierKind.FireResistanceBasisPoints, 800, 1_600, "火焰抗性提高"),
            ItemCategory.LifeFlask => (ItemModifierKind.IncreasedLifeFlaskEffectBasisPoints, 500, 1_200, "药剂效果提高"),
            _ => (ItemModifierKind.FlatMaximumLife, 10, 20, "最大生命提高"),
        };
    }
}

public static class P25LegendaryCatalog
{
    public static IReadOnlyList<AffixRoll> CreateAffixes(ItemBaseDefinition itemBase)
    {
        var result = new List<AffixRoll>(6);
        var groups = new HashSet<string>(StringComparer.Ordinal);
        foreach (AffixPosition position in Enum.GetValues<AffixPosition>())
        {
            foreach (AffixDefinition source in P1Affixes.For(itemBase, 120)
                         .Where(affix => affix.Position == position)
                         .OrderBy(affix => P1Affixes.TierFor(itemBase, affix))
                         .ThenByDescending(affix => affix.MaximumValue)
                         .ThenBy(affix => affix.StableFamilyId, StringComparer.Ordinal))
            {
                if (result.Count(affix => affix.Definition.Position == position) >= 3) break;
                if (!groups.Add(source.MutualExclusionGroup)) continue;
                AffixDefinition legendary = source with { Source = "传奇", Weight = 0 };
                result.Add(new AffixRoll(legendary, source.MaximumValue));
            }
        }
        return result;
    }
}

public static class P25EquipmentArt
{
    public const int Columns = 13;
    public const int Rows = 12;
    public static IReadOnlyList<string> ItemBaseIds { get; } =
        P19.P19Catalog.Bases.Select(item => item.StableId)
            .Concat(P24.P24ItemCatalog.Bases.Select(item => item.StableId))
            .OrderBy(stableId => stableId, StringComparer.Ordinal)
            .Concat(P29WarfrontBases.All.Select(item => item.StableId)).ToArray();
    private static readonly IReadOnlyDictionary<string, int> Indices = ItemBaseIds
        .Select((stableId, index) => (stableId, index)).ToDictionary(pair => pair.stableId, pair => pair.index,
            StringComparer.Ordinal);

    public static int IconIndex(ItemBaseDefinition itemBase)
    {
        return Indices.TryGetValue(itemBase.StableId, out int index)
            ? index : throw new KeyNotFoundException($"P25 equipment art mapping missing for {itemBase.StableId}.");
    }
}

public static class P25LegendaryArt
{
    public const int Columns = 5;
    public static IReadOnlyList<string> StableIds { get; } =
    [
        "core.unique.echoing_oathbreaker", "core.unique.march_without_end", "core.unique.ravens_answer",
        "core.unique.red_vow", "core.unique.blue_vow", "core.unique.gardeners_sinew", "core.unique.warden_shell",
        "core.unique.glass_horizon", "core.unique.funeral_bell", "core.unique.black_tide",
        "core.unique.starless_prayer", "core.unique.last_banner", "core.unique.iron_moon", "core.unique.hollow_guard",
        "core.unique.thorn_procession", "core.unique.pilgrims_debt", "core.unique.cinder_chain",
        "core.unique.fourth_testament", "core.unique.silent_anvil", "core.unique.hunters_eclipse",
        "core.unique.ashes_memory", "core.unique.grave_plate", "core.unique.famine_ring", "core.unique.last_watch",
        "p29.unique.rift_fang", "p29.unique.deep_echo", "p29.unique.seed_of_rebirth", "p29.unique.thorned_bark",
        "p29.unique.executioners_due", "p29.unique.blood_tithe", "p29.unique.frozen_moment", "p29.unique.starfall_lens",
        "p29.unique.commanders_burden", "p29.unique.broken_standard", "p29.unique.wayfarers_compass", "p29.unique.void_balance",
        "core.mythic.heart_of_ash",
    ];
    private static readonly IReadOnlyDictionary<string, int> Indices = StableIds
        .Select((stableId, index) => (stableId, index)).ToDictionary(pair => pair.stableId, pair => pair.index,
            StringComparer.Ordinal);

    public static int IconIndex(string stableId)
    {
        return Indices.TryGetValue(stableId, out int index)
            ? index : throw new KeyNotFoundException($"P25 legendary art mapping missing for {stableId}.");
    }
}

public static class P25SkillStoneArt
{
    public const int Columns = 10;
    public const int Rows = 9;

    public static int IconIndex(string stableId)
    {
        int active = P24.P24SkillCatalog.Active.ToList().FindIndex(skill => skill.Combat.StoneId == stableId);
        if (active >= 0) return active;
        int support = P24.P24SkillCatalog.Supports.ToList().FindIndex(skill => skill.StoneId == stableId);
        if (support >= 0) return P24.P24SkillCatalog.Active.Count + support;
        throw new KeyNotFoundException($"P25 skill-stone art mapping missing for {stableId}.");
    }
}

public sealed record P25LegendaryContext(
    int DistanceRaw = 0,
    int AttackIntervalTicks = 20,
    bool FullLife = false,
    bool Slam = false,
    bool ReturningProjectile = false,
    bool Boss = false,
    bool SpellHit = false,
    bool Blocked = false,
    bool Suppressed = false,
    bool Evaded = false,
    bool Moving = false,
    int MovingSeconds = 0,
    int UnusedFlaskUses = 0,
    int BastionStacks = 0);

public sealed record P25LegendaryEffect(
    int OutgoingDamageMultiplierBasisPoints = 10_000,
    int IncomingDamageMultiplierBasisPoints = 10_000,
    int IncreasedArmorBasisPoints = 0,
    int IncreasedMovementSpeedBasisPoints = 0,
    int RestoreLifeBasisPoints = 0,
    int RestoreShieldBasisPoints = 0,
    int RestoreManaBasisPoints = 0,
    int FlaskChargeGainMultiplierBasisPoints = 10_000,
    int IncreasedFlaskEffectBasisPoints = 0,
    int FlaskDurationMultiplierBasisPoints = 10_000,
    int ExtraProjectileChains = 0,
    bool ProjectilesReturn = false,
    int BannerReservationMultiplierBasisPoints = 10_000,
    int IncreasedBannerEffectBasisPoints = 0,
    int AdditionalGardenPreservedAffixes = 0,
    int GardenCostMultiplierBasisPoints = 10_000,
    int TriggerDamageBasisPoints = 0,
    bool GuaranteedCritical = false,
    bool ReviveOnce = false);

/// <summary>Single deterministic execution table for every shipped legendary rule.</summary>
public static class P25LegendaryRules
{
    public static P25LegendaryEffect Resolve(string stableId, P25LegendaryContext context) => stableId switch
    {
        "core.unique.echoing_oathbreaker" or "core.legendary_rule.echoing_oathbreaker" => new(TriggerDamageBasisPoints: 7_000),
        "core.unique.march_without_end" => new(IncreasedArmorBasisPoints: context.Moving ? Math.Min(8_000, context.MovingSeconds * 800) : 0),
        "core.unique.ravens_answer" => new(context.ReturningProjectile ? 13_000 : 10_000, ExtraProjectileChains: 2, ProjectilesReturn: true),
        "core.unique.red_vow" => new(16_000),
        "core.unique.blue_vow" => new(context.Boss ? 12_500 : 10_000, context.Boss ? 12_500 : 10_000),
        "core.unique.gardeners_sinew" => new(AdditionalGardenPreservedAffixes: 1, GardenCostMultiplierBasisPoints: 7_000),
        "core.unique.warden_shell" => new(TriggerDamageBasisPoints: context.Blocked ? 25_000 : 0),
        "core.unique.glass_horizon" => new(context.DistanceRaw >= 6_000 ? 13_500 : 10_000),
        "core.unique.funeral_bell" => new(13_000),
        "core.unique.black_tide" => new(IncreasedMovementSpeedBasisPoints: 2_000),
        "core.unique.starless_prayer" => new(RestoreShieldBasisPoints: context.Suppressed ? 800 : 0),
        "core.unique.last_banner" => new(BannerReservationMultiplierBasisPoints: 0, IncreasedBannerEffectBasisPoints: 8_000),
        "core.unique.iron_moon" => new(context.FullLife && context.Slam ? 17_000 : 10_000),
        "core.unique.hollow_guard" => new(IncomingDamageMultiplierBasisPoints: context.Blocked && context.SpellHit ? 3_000 : 10_000),
        "core.unique.thorn_procession" => new(TriggerDamageBasisPoints: 15_000),
        "core.unique.pilgrims_debt" => new(IncreasedMovementSpeedBasisPoints: Math.Min(4_500, context.UnusedFlaskUses * 300)),
        "core.unique.cinder_chain" => new(FlaskChargeGainMultiplierBasisPoints: 20_000,
            IncreasedFlaskEffectBasisPoints: 3_000, FlaskDurationMultiplierBasisPoints: 7_500),
        "core.unique.fourth_testament" => new(RestoreShieldBasisPoints: 2_000),
        "core.unique.silent_anvil" => new(SlowAttackMultiplier(context.AttackIntervalTicks)),
        "core.unique.hunters_eclipse" => new(context.Evaded ? 15_000 : 10_000, GuaranteedCritical: context.Evaded),
        "core.unique.ashes_memory" => new(RestoreManaBasisPoints: 1_000),
        "core.unique.grave_plate" => new(IncomingDamageMultiplierBasisPoints: context.SpellHit ? 8_000 : 10_000),
        "core.unique.famine_ring" => new(IncomingDamageMultiplierBasisPoints: 10_000,
            FlaskChargeGainMultiplierBasisPoints: 20_000, IncreasedFlaskEffectBasisPoints: 5_000),
        "core.unique.last_watch" => new(IncomingDamageMultiplierBasisPoints: Math.Max(7_500, 10_000 - context.BastionStacks * 500)),
        "p29.unique.rift_fang" => new(context.Boss ? 15_500 : 10_000),
        "p29.unique.deep_echo" => new(context.ReturningProjectile ? 14_800 : 11_200, ExtraProjectileChains: 1),
        "p29.unique.seed_of_rebirth" => new(context.FullLife ? 12_500 : 10_000, RestoreLifeBasisPoints: context.FullLife ? 0 : 400),
        "p29.unique.thorned_bark" => new(IncomingDamageMultiplierBasisPoints: context.Blocked ? 10_000 : 8_000),
        "p29.unique.executioners_due" => new(context.Boss ? 20_000 : 10_000),
        "p29.unique.blood_tithe" => new(16_000, RestoreLifeBasisPoints: -2_000),
        "p29.unique.frozen_moment" => new(context.Boss ? 14_000 : 10_000),
        "p29.unique.starfall_lens" => new(context.Evaded ? 13_500 : 10_000, ExtraProjectileChains: context.Evaded ? 3 : 0),
        "p29.unique.commanders_burden" => new(context.Boss ? 14_500 : 10_000, IncreasedArmorBasisPoints: context.Boss ? 0 : 7_500),
        "p29.unique.broken_standard" => new(IncreasedMovementSpeedBasisPoints: 3_500),
        "p29.unique.wayfarers_compass" => new(IncomingDamageMultiplierBasisPoints: context.Moving ? 8_500 : 10_000, IncreasedMovementSpeedBasisPoints: 3_000),
        "p29.unique.void_balance" => new(13_500, IncomingDamageMultiplierBasisPoints: 8_800),
        "core.mythic.heart_of_ash" => new(13_000, ReviveOnce: true),
        _ => throw new KeyNotFoundException($"Unknown P25 legendary effect: {stableId}"),
    };

    public static bool HasImplementation(string stableId)
    {
        try { _ = Resolve(stableId, new P25LegendaryContext()); return true; }
        catch (KeyNotFoundException) { return false; }
    }

    private static int SlowAttackMultiplier(int attackIntervalTicks)
    {
        int attacksPerSecondHundred = attackIntervalTicks <= 0 ? 10_000 : 2_000 / attackIntervalTicks;
        int missingTenths = Math.Clamp((150 - attacksPerSecondHundred) / 10, 0, 10);
        return 10_000 + missingTenths * 800;
    }
}
