using System.Globalization;
using System.Text.RegularExpressions;
using GameForWork.Core.P1.Items;

namespace GameForWork.Core.P30;

public static partial class P30EquipmentAffixes
{
    private static readonly HashSet<ItemModifierKind> ForbiddenGlobalWeaponDamageIncreases =
    [
        ItemModifierKind.IncreasedAttackDamageBasisPoints,
        ItemModifierKind.IncreasedSpellDamageBasisPoints,
        ItemModifierKind.IncreasedElementalDamageBasisPoints,
        ItemModifierKind.IncreasedPhysicalDamageBasisPoints,
        ItemModifierKind.IncreasedFireDamageBasisPoints,
        ItemModifierKind.IncreasedColdDamageBasisPoints,
        ItemModifierKind.IncreasedLightningDamageBasisPoints,
        ItemModifierKind.IncreasedVoidDamageBasisPoints,
        ItemModifierKind.IncreasedMeleeDamageBasisPoints,
        ItemModifierKind.IncreasedProjectileDamageBasisPoints,
        ItemModifierKind.IncreasedAreaDamageBasisPoints,
        ItemModifierKind.IncreasedDamageOverTimeBasisPoints,
        ItemModifierKind.IncreasedBleedDamageBasisPoints,
        ItemModifierKind.IncreasedPoisonDamageBasisPoints,
        ItemModifierKind.IncreasedIgniteDamageBasisPoints,
    ];

    private static readonly HashSet<string> RemovedImportedFamilies = new(StringComparer.Ordinal)
    {
        "p19.affix.localarmourandenergyshieldandstunrecovery",
        "p19.affix.localarmourandevasionandstunrecovery",
        "p19.affix.localenergyshieldandstunrecoverypercent",
        "p19.affix.localevasionratingandstunrecoveryincreasepercent",
        "p19.affix.localphysicaldamagereductionratingandstunrecoverypercent",
    };

    private static readonly ItemCategory[] Weapons = [ItemCategory.OneHandWeapon, ItemCategory.TwoHandWeapon];
    private static readonly ItemCategory[] Armour = [ItemCategory.BodyArmor, ItemCategory.Helmet, ItemCategory.Gloves, ItemCategory.Boots, ItemCategory.Shield];
    private static readonly ItemCategory[] Jewellery = [ItemCategory.Ring, ItemCategory.Amulet, ItemCategory.Belt];
    private static readonly ItemCategory[] General = [.. Weapons, .. Armour, .. Jewellery];

    public static IReadOnlyList<AffixDefinition> Ordinary { get; } = BuildOrdinary();

    public static ItemInstance RemoveForbiddenGlobalWeaponAffixes(ItemInstance item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Base.Category is not (ItemCategory.OneHandWeapon or ItemCategory.TwoHandWeapon)) return item;
        AffixRoll[] retained = item.Affixes.Where(affix => !affix.Effects.Any(effect =>
            effect.Scope == ItemModifierScope.Global && ForbiddenGlobalWeaponDamageIncreases.Contains(effect.Kind))).ToArray();
        if (retained.Length == item.Affixes.Count) return item;
        string fractured = retained.Any(affix => affix.Definition.StableFamilyId == item.FracturedAffixFamilyId)
            ? item.FracturedAffixFamilyId
            : string.Empty;
        return item with { Affixes = retained, FracturedAffixFamilyId = fractured };
    }

    public static bool IsRemovedImportedFamily(string stableFamilyId) => RemovedImportedFamilies.Contains(stableFamilyId);

    public static AffixDefinition NormalizeImported(AffixDefinition affix)
    {
        IReadOnlyList<AffixModifierComponent> components = affix.StableFamilyId switch
        {
            "p19.affix.localphysicaldamage" or "p19.affix.localphysicaldamagetwohanded" => AddedDamageComponents(
                affix.RawText, ItemModifierKind.AddedMinimumPhysicalDamage, ItemModifierKind.AddedMaximumPhysicalDamage,
                ItemModifierScope.LocalWeapon),
            "p19.affix.physicaldamage" => AddedDamageComponents(
                affix.RawText, ItemModifierKind.AddedMinimumPhysicalDamage, ItemModifierKind.AddedMaximumPhysicalDamage,
                ItemModifierScope.Global),
            "p19.affix.localbasearmourandlife" => PairComponents(affix.RawText,
                ItemModifierKind.FlatArmor, ItemModifierScope.LocalDefense, ItemModifierKind.FlatMaximumLife),
            "p19.affix.localbaseevasionratingandlife" => PairComponents(affix.RawText,
                ItemModifierKind.FlatEvasion, ItemModifierScope.LocalDefense, ItemModifierKind.FlatMaximumLife),
            "p19.affix.localbaseenergyshieldandlife" => PairComponents(affix.RawText,
                ItemModifierKind.FlatShield, ItemModifierScope.LocalDefense, ItemModifierKind.FlatMaximumLife),
            "p19.affix.localbaseenergyshieldandmana" => PairComponents(affix.RawText,
                ItemModifierKind.FlatShield, ItemModifierScope.LocalDefense, ItemModifierKind.FlatMaximumMana),
            "p19.affix.localarmourandenergyshield" => SameRange(affix,
                (ItemModifierKind.IncreasedArmorBasisPoints, ItemModifierScope.LocalDefense),
                (ItemModifierKind.IncreasedShieldBasisPoints, ItemModifierScope.LocalDefense)),
            "p19.affix.localarmourandevasion" => SameRange(affix,
                (ItemModifierKind.IncreasedArmorBasisPoints, ItemModifierScope.LocalDefense),
                (ItemModifierKind.IncreasedEvasionBasisPoints, ItemModifierScope.LocalDefense)),
            "p19.affix.localarmourandevasionandenergyshield" => SameRange(affix,
                (ItemModifierKind.IncreasedArmorBasisPoints, ItemModifierScope.LocalDefense),
                (ItemModifierKind.IncreasedEvasionBasisPoints, ItemModifierScope.LocalDefense),
                (ItemModifierKind.IncreasedShieldBasisPoints, ItemModifierScope.LocalDefense)),
            "p19.affix.localincreasedphysicaldamagepercentandaccuracyrating" => PairComponents(affix.RawText,
                ItemModifierKind.IncreasedPhysicalDamageBasisPoints, ItemModifierScope.LocalWeapon,
                ItemModifierKind.FlatAccuracy, secondIsPercent: false),
            "p19.affix.localaccuracyrating" => SameRange(affix,
                (ItemModifierKind.FlatAccuracy, ItemModifierScope.Global)),
            "p19.affix.localincreasedblockpercentage" => SameRange(affix,
                (ItemModifierKind.IncreasedLocalBlockBasisPoints, ItemModifierScope.LocalBlock)),
            "p19.affix.energyshieldregeneration" => SameRange(affix,
                (ItemModifierKind.IncreasedResourceRecoveryRateBasisPoints, ItemModifierScope.Global)),
            "p19.affix.flaskbuffarmourwhilehealing" => SameRange(affix,
                (ItemModifierKind.FlaskBuffArmorBasisPoints, ItemModifierScope.Flask)),
            "p19.affix.flaskbuffevasionwhilehealing" => SameRange(affix,
                (ItemModifierKind.FlaskBuffEvasionBasisPoints, ItemModifierScope.Flask)),
            "p19.affix.flaskbuffcriticalwhilehealing" => SameRange(affix,
                (ItemModifierKind.FlaskBuffCriticalChanceBasisPoints, ItemModifierScope.Flask)),
            "p19.affix.flaskbuffmovementspeedwhilehealing" => SameRange(affix,
                (ItemModifierKind.FlaskBuffMovementSpeedBasisPoints, ItemModifierScope.Flask)),
            "p19.affix.flaskextralifecostsmana" =>
                [new(ItemModifierKind.IncreasedFlaskRecoveryAmountBasisPoints, affix.MinimumValue, affix.MaximumValue, ItemModifierScope.Flask, "生命恢复量提高"),
                 new(ItemModifierKind.FlaskLifeRemovedFromManaBasisPoints, 1_000, 1_000, ItemModifierScope.Flask, "使用时从法力移除生命恢复量")],
            "p19.affix.flaskextramanacostslife" =>
                [new(ItemModifierKind.IncreasedFlaskRecoveryAmountBasisPoints, affix.MinimumValue, affix.MaximumValue, ItemModifierScope.Flask, "法力恢复量提高"),
                 new(ItemModifierKind.FlaskManaRemovedFromLifeBasisPoints, 1_500, 1_500, ItemModifierScope.Flask, "使用时从生命移除法力恢复量")],
            "p19.affix.flaskincreasedhealingcharges" => PairComponents(affix.RawText,
                ItemModifierKind.IncreasedFlaskChargesPerUseBasisPoints, ItemModifierScope.Flask,
                ItemModifierKind.IncreasedFlaskRecoveryAmountBasisPoints, secondIsPercent: true,
                secondScope: ItemModifierScope.Flask),
            "p19.affix.flaskincreasedrecoveryamount" =>
                [new(ItemModifierKind.IncreasedFlaskRecoveryAmountBasisPoints, affix.MinimumValue, affix.MaximumValue, ItemModifierScope.Flask, "恢复量提高"),
                 new(ItemModifierKind.IncreasedFlaskRecoveryRateBasisPoints, -3_300, -3_300, ItemModifierScope.Flask, "恢复速度降低")],
            "p19.affix.flaskincreasedrecoveryspeed" => SameRange(affix,
                (ItemModifierKind.IncreasedFlaskRecoveryRateBasisPoints, ItemModifierScope.Flask)),
            "p19.affix.flaskmanarecoveryatend" =>
                [new(ItemModifierKind.IncreasedFlaskRecoveryAmountBasisPoints, affix.MinimumValue, affix.MaximumValue, ItemModifierScope.Flask, "法力恢复量提高"),
                 new(ItemModifierKind.FlaskRecoveryAtEnd, 1, 1, ItemModifierScope.Rule, "效果结束时立即结算法力恢复")],
            "p19.affix.flaskpartialinstantrecovery" =>
                [new(ItemModifierKind.IncreasedFlaskRecoveryAmountBasisPoints, -affix.MaximumValue, -affix.MinimumValue, ItemModifierScope.Flask, "恢复量降低"),
                 new(ItemModifierKind.IncreasedFlaskRecoveryRateBasisPoints, 13_500, 13_500, ItemModifierScope.Flask, "恢复速度提高"),
                 new(ItemModifierKind.InstantFlaskRecoveryPortionBasisPoints, 5_000, 5_000, ItemModifierScope.Flask, "立即恢复比例")],
            "p19.affix.liferegeneration" =>
                [new AffixModifierComponent(ItemModifierKind.MaximumLifeRegenerationBasisPoints,
                    affix.Tier == 1 ? 180 : 150, affix.Tier == 1 ? 200 : 179,
                    ItemModifierScope.Global, "每秒恢复最大生命")],
            _ when affix.Local => SameRange(affix, (affix.ModifierKind, LocalScope(affix.ModifierKind))),
            _ => SameRange(affix, (affix.ModifierKind, ItemModifierScope.Global)),
        };
        AffixModifierComponent primary = components[0];
        return affix with
        {
            ModifierKind = primary.Kind,
            MinimumValue = primary.MinimumValue,
            MaximumValue = primary.MaximumValue,
            Components = components,
        };
    }

    private static IReadOnlyList<AffixDefinition> BuildOrdinary()
    {
        var result = new List<AffixDefinition>();

        // Global damage increases are deliberately not weapon affixes. Weapons scale through
        // their local damage, speed and critical rolls; global increases remain jewellery rolls.
        AddSix(result, "attack.damage", "攻击伤害提高", AffixPosition.Prefix, ItemModifierKind.IncreasedAttackDamageBasisPoints, 3_500, 4_500, Jewellery);
        AddSix(result, "spell.damage", "法术伤害提高", AffixPosition.Prefix, ItemModifierKind.IncreasedSpellDamageBasisPoints, 3_500, 4_500, Jewellery);
        AddSix(result, "elemental.damage", "元素伤害提高", AffixPosition.Prefix, ItemModifierKind.IncreasedElementalDamageBasisPoints, 3_500, 4_500, Jewellery);
        AddSix(result, "physical.damage", "物理伤害提高", AffixPosition.Prefix, ItemModifierKind.IncreasedPhysicalDamageBasisPoints, 3_500, 4_500, Jewellery);
        AddSix(result, "fire.damage", "火焰伤害提高", AffixPosition.Prefix, ItemModifierKind.IncreasedFireDamageBasisPoints, 4_500, 5_500, Jewellery);
        AddSix(result, "cold.damage", "冰霜伤害提高", AffixPosition.Prefix, ItemModifierKind.IncreasedColdDamageBasisPoints, 4_500, 5_500, Jewellery);
        AddSix(result, "lightning.damage", "闪电伤害提高", AffixPosition.Prefix, ItemModifierKind.IncreasedLightningDamageBasisPoints, 4_500, 5_500, Jewellery);
        AddSix(result, "void.damage", "虚空伤害提高", AffixPosition.Prefix, ItemModifierKind.IncreasedVoidDamageBasisPoints, 4_500, 5_500, Jewellery);
        AddSix(result, "melee.damage", "近战伤害提高", AffixPosition.Prefix, ItemModifierKind.IncreasedMeleeDamageBasisPoints, 3_500, 4_500, Jewellery);
        AddSix(result, "projectile.damage", "投射物伤害提高", AffixPosition.Prefix, ItemModifierKind.IncreasedProjectileDamageBasisPoints, 3_500, 4_500, Jewellery);
        AddSix(result, "area.damage", "范围伤害提高", AffixPosition.Prefix, ItemModifierKind.IncreasedAreaDamageBasisPoints, 3_500, 4_500, Jewellery);
        AddSix(result, "dot.damage", "持续伤害提高", AffixPosition.Prefix, ItemModifierKind.IncreasedDamageOverTimeBasisPoints, 4_000, 5_000, Jewellery);
        AddSix(result, "dot.multiplier", "持续伤害倍率", AffixPosition.Suffix, ItemModifierKind.DamageOverTimeMultiplierBasisPoints, 1_800, 2_400, Weapons.Concat(Jewellery), weight: 500);
        AddSix(result, "bleed.damage", "流血伤害提高", AffixPosition.Prefix, ItemModifierKind.IncreasedBleedDamageBasisPoints, 4_500, 6_000, Jewellery);
        AddSix(result, "poison.damage", "中毒伤害提高", AffixPosition.Prefix, ItemModifierKind.IncreasedPoisonDamageBasisPoints, 4_500, 6_000, Jewellery);
        AddSix(result, "ignite.damage", "点燃伤害提高", AffixPosition.Prefix, ItemModifierKind.IncreasedIgniteDamageBasisPoints, 4_500, 6_000, Jewellery);
        AddSix(result, "bleed.faster", "流血伤害加快", AffixPosition.Suffix, ItemModifierKind.FasterBleedBasisPoints, 1_500, 2_000, Weapons.Concat(Jewellery), weight: 500);
        AddSix(result, "poison.faster", "中毒伤害加快", AffixPosition.Suffix, ItemModifierKind.FasterPoisonBasisPoints, 1_500, 2_000, Weapons.Concat(Jewellery), weight: 500);
        AddSix(result, "ignite.faster", "点燃伤害加快", AffixPosition.Suffix, ItemModifierKind.FasterIgniteBasisPoints, 1_500, 2_000, Weapons.Concat(Jewellery), weight: 500);
        AddSix(result, "critical.chance", "全局暴击率提高", AffixPosition.Suffix, ItemModifierKind.IncreasedCriticalChanceBasisPoints, 8_000, 10_000, Weapons.Concat(Jewellery));
        AddSix(result, "critical.multiplier", "暴击伤害倍率", AffixPosition.Suffix, ItemModifierKind.IncreasedCriticalMultiplierBasisPoints, 3_000, 4_000, Weapons.Concat(Jewellery), weight: 600);
        AddSix(result, "cast.speed", "施法速度提高", AffixPosition.Suffix, ItemModifierKind.IncreasedCastSpeedBasisPoints, 1_400, 1_800, Weapons.Concat(Jewellery), requiredTags: ["caster", "wand", "focus", "runeblade"]);

        AddElementalWeaponDamage(result, "fire", "火焰", ItemModifierKind.AddedMinimumFireDamage, ItemModifierKind.AddedMaximumFireDamage, 32, 58);
        AddElementalWeaponDamage(result, "cold", "冰霜", ItemModifierKind.AddedMinimumColdDamage, ItemModifierKind.AddedMaximumColdDamage, 28, 52);
        AddElementalWeaponDamage(result, "lightning", "闪电", ItemModifierKind.AddedMinimumLightningDamage, ItemModifierKind.AddedMaximumLightningDamage, 8, 82);
        AddElementalWeaponDamage(result, "void", "虚空", ItemModifierKind.AddedMinimumVoidDamage, ItemModifierKind.AddedMaximumVoidDamage, 24, 64);

        AddSix(result, "fire.penetration", "火焰穿透", AffixPosition.Suffix, ItemModifierKind.FirePenetrationBasisPoints, 1_000, 1_400, Weapons.Concat(Jewellery), weight: 300);
        AddSix(result, "cold.penetration", "冰霜穿透", AffixPosition.Suffix, ItemModifierKind.ColdPenetrationBasisPoints, 1_000, 1_400, Weapons.Concat(Jewellery), weight: 300);
        AddSix(result, "lightning.penetration", "闪电穿透", AffixPosition.Suffix, ItemModifierKind.LightningPenetrationBasisPoints, 1_000, 1_400, Weapons.Concat(Jewellery), weight: 300);
        AddSix(result, "void.penetration", "虚空穿透", AffixPosition.Suffix, ItemModifierKind.VoidPenetrationBasisPoints, 1_000, 1_400, Weapons.Concat(Jewellery), weight: 300);
        AddSix(result, "bleed.chance", "流血概率", AffixPosition.Suffix, ItemModifierKind.BleedChanceBasisPoints, 2_500, 3_500, Weapons.Concat(Jewellery));
        AddSix(result, "poison.chance", "中毒概率", AffixPosition.Suffix, ItemModifierKind.PoisonChanceBasisPoints, 2_500, 3_500, Weapons.Concat(Jewellery));
        AddSix(result, "ignite.chance", "点燃概率", AffixPosition.Suffix, ItemModifierKind.IgniteChanceBasisPoints, 2_500, 3_500, Weapons.Concat(Jewellery));
        AddSix(result, "shock.chance", "感电概率", AffixPosition.Suffix, ItemModifierKind.ShockChanceBasisPoints, 2_500, 3_500, Weapons.Concat(Jewellery));
        AddSix(result, "chill.effect", "冰缓效果提高", AffixPosition.Suffix, ItemModifierKind.ChillEffectBasisPoints, 2_000, 3_000, Weapons.Concat(Jewellery));
        AddSix(result, "freeze.effect", "冻结效果提高", AffixPosition.Suffix, ItemModifierKind.FreezeEffectBasisPoints, 2_000, 3_000, Weapons.Concat(Jewellery));
        AddSix(result, "shock.effect", "感电效果提高", AffixPosition.Suffix, ItemModifierKind.ShockEffectBasisPoints, 2_000, 3_000, Weapons.Concat(Jewellery));
        AddSix(result, "projectile.speed", "投射物速度提高", AffixPosition.Suffix, ItemModifierKind.ProjectileSpeedBasisPoints, 3_500, 4_500, Weapons.Concat(Jewellery), requiredTags: ["projectile", "bow", "quiver"]);
        AddSix(result, "skill.area", "技能范围效果提高", AffixPosition.Suffix, ItemModifierKind.SkillAreaBasisPoints, 1_800, 2_500, General);
        AddSix(result, "skill.range", "技能距离提高", AffixPosition.Suffix, ItemModifierKind.SkillRangeBasisPoints, 1_500, 2_000, General);
        AddSix(result, "cooldown.recovery", "冷却恢复速度提高", AffixPosition.Suffix, ItemModifierKind.IncreasedCooldownRecoveryBasisPoints, 1_500, 2_200, General);
        AddDiscrete(result, "extra.projectile", "额外投射物", AffixPosition.Prefix, ItemModifierKind.AdditionalProjectile, 1, Weapons.Concat(Jewellery), 85, 50, ["projectile", "bow", "quiver"]);
        AddDiscrete(result, "extra.chain", "额外连锁", AffixPosition.Prefix, ItemModifierKind.AdditionalChain, 1, Weapons.Concat(Jewellery), 85, 30, ["projectile", "bow", "quiver"]);
        AddDiscrete(result, "extra.strike", "额外打击目标", AffixPosition.Prefix, ItemModifierKind.AdditionalStrikeTarget, 1, Weapons.Concat(Jewellery), 85, 50, ["melee"]);
        AddSix(result, "extra.pierce", "额外穿透目标", AffixPosition.Prefix, ItemModifierKind.AdditionalPierce, 1, 2, Weapons.Concat(Jewellery), weight: 300, requiredTags: ["projectile", "bow", "quiver"]);

        AddSix(result, "armour.flat", "护甲", AffixPosition.Prefix, ItemModifierKind.FlatArmor, 260, 380, Armour, localScope: ItemModifierScope.LocalDefense);
        AddSix(result, "evasion.flat", "闪避", AffixPosition.Prefix, ItemModifierKind.FlatEvasion, 260, 380, Armour, localScope: ItemModifierScope.LocalDefense);
        AddSix(result, "shield.flat", "最大护盾", AffixPosition.Prefix, ItemModifierKind.FlatShield, 90, 140, Armour, localScope: ItemModifierScope.LocalDefense);
        AddSix(result, "barrier.flat", "灵障", AffixPosition.Prefix, ItemModifierKind.FlatSpiritBarrier, 120, 180, Armour, localScope: ItemModifierScope.LocalDefense);
        AddSix(result, "barrier.local", "局部灵障提高", AffixPosition.Prefix, ItemModifierKind.IncreasedSpiritBarrierBasisPoints, 9_000, 11_000, Armour, localScope: ItemModifierScope.LocalDefense);
        AddSix(result, "barrier.global", "灵障提高", AffixPosition.Prefix, ItemModifierKind.IncreasedSpiritBarrierBasisPoints, 3_500, 4_500, Jewellery);
        AddSix(result, "maximum.life", "最大生命提高", AffixPosition.Prefix, ItemModifierKind.IncreasedMaximumLifeBasisPoints, 1_000, 1_400, Armour.Concat(Jewellery), weight: 200);
        AddSix(result, "maximum.mana", "最大法力提高", AffixPosition.Prefix, ItemModifierKind.IncreasedMaximumManaBasisPoints, 1_200, 1_600, General);
        AddSix(result, "maximum.shield", "最大护盾提高", AffixPosition.Prefix, ItemModifierKind.IncreasedMaximumShieldBasisPoints, 1_200, 1_600, Armour.Concat(Jewellery));
        AddSix(result, "resource.recovery", "资源恢复率提高", AffixPosition.Suffix, ItemModifierKind.IncreasedResourceRecoveryRateBasisPoints, 1_000, 1_500, General);
        AddSix(result, "life.regen", "每秒恢复最大生命", AffixPosition.Suffix, ItemModifierKind.MaximumLifeRegenerationBasisPoints, 180, 200, Armour.Concat(Jewellery));
        AddSix(result, "shield.regen", "每秒恢复最大护盾", AffixPosition.Suffix, ItemModifierKind.MaximumShieldRegenerationBasisPoints, 150, 200, Armour.Concat(Jewellery));
        AddSix(result, "shield.delay", "护盾充能延迟缩短", AffixPosition.Suffix, ItemModifierKind.ReducedShieldRechargeDelayBasisPoints, 2_000, 2_500, Armour.Concat(Jewellery));
        AddSix(result, "physical.resistance", "物理抗性", AffixPosition.Suffix, ItemModifierKind.PhysicalResistanceBasisPoints, 600, 800, Armour.Concat(Jewellery), weight: 300);
        AddDiscreteRange(result, "suppression.effect", "法术压制效果", AffixPosition.Suffix,
            ItemModifierKind.SpellSuppressionEffectBasisPoints, 300, 500, Armour.Concat(Jewellery), 85, 100);
        AddSix(result, "ailment.avoid", "避免元素异常", AffixPosition.Suffix, ItemModifierKind.AilmentAvoidanceBasisPoints, 3_000, 4_000, Armour.Concat(Jewellery));
        AddSix(result, "curse.reduction", "受到诅咒效果降低", AffixPosition.Suffix, ItemModifierKind.ReducedCurseEffectBasisPoints, 3_000, 4_000, Armour.Concat(Jewellery));
        AddSix(result, "debuff.duration", "非异常负面持续时间降低", AffixPosition.Suffix, ItemModifierKind.ReducedDebuffDurationBasisPoints, 2_500, 3_500, Armour.Concat(Jewellery));
        AddDiscrete(result, "maximum.fire.resistance", "最大火焰抗性", AffixPosition.Suffix, ItemModifierKind.MaximumFireResistanceBasisPoints, 100, Armour.Concat(Jewellery), 85, 50);
        AddDiscrete(result, "maximum.cold.resistance", "最大冰霜抗性", AffixPosition.Suffix, ItemModifierKind.MaximumColdResistanceBasisPoints, 100, Armour.Concat(Jewellery), 85, 50);
        AddDiscrete(result, "maximum.lightning.resistance", "最大闪电抗性", AffixPosition.Suffix, ItemModifierKind.MaximumLightningResistanceBasisPoints, 100, Armour.Concat(Jewellery), 85, 50);
        AddDiscrete(result, "maximum.void.resistance", "最大虚空抗性", AffixPosition.Suffix, ItemModifierKind.MaximumVoidResistanceBasisPoints, 100, Armour.Concat(Jewellery), 85, 50);

        AddSix(result, "life.leech", "击中伤害生命偷取", AffixPosition.Suffix, ItemModifierKind.LifeLeechBasisPoints, 120, 180, Weapons.Concat(Jewellery), weight: 500);
        AddSix(result, "mana.leech", "击中伤害法力偷取", AffixPosition.Suffix, ItemModifierKind.ManaLeechBasisPoints, 60, 100, Weapons.Concat(Jewellery), weight: 500);
        AddSix(result, "shield.leech", "击中伤害护盾偷取", AffixPosition.Suffix, ItemModifierKind.ShieldLeechBasisPoints, 60, 100, Weapons.Concat(Jewellery), weight: 500);
        AddSix(result, "leech.rate", "偷取恢复速度提高", AffixPosition.Suffix, ItemModifierKind.IncreasedLeechRecoveryRateBasisPoints, 2_500, 3_500, General);
        AddSix(result, "leech.maximum", "偷取每秒总恢复上限提高", AffixPosition.Suffix, ItemModifierKind.IncreasedMaximumLeechRateBasisPoints, 2_000, 3_000, General, weight: 300);
        AddSix(result, "life.onhit", "击中回复生命", AffixPosition.Suffix, ItemModifierKind.LifeOnHit, 15, 25, Weapons.Concat(Jewellery));
        AddSix(result, "mana.onhit", "击中回复法力", AffixPosition.Suffix, ItemModifierKind.ManaOnHit, 10, 18, Weapons.Concat(Jewellery));
        AddSix(result, "shield.onhit", "击中回复护盾", AffixPosition.Suffix, ItemModifierKind.ShieldOnHit, 10, 18, Weapons.Concat(Jewellery));

        AddConversion(result, "phys.fire", "物理转火焰", ItemModifierKind.PhysicalToFireConversionBasisPoints, 2_500, 3_500, 100);
        AddConversion(result, "phys.cold", "物理转冰霜", ItemModifierKind.PhysicalToColdConversionBasisPoints, 2_500, 3_500, 100);
        AddConversion(result, "phys.lightning", "物理转闪电", ItemModifierKind.PhysicalToLightningConversionBasisPoints, 2_500, 3_500, 100);
        AddConversion(result, "phys.void", "物理转虚空", ItemModifierKind.PhysicalToVoidConversionBasisPoints, 2_000, 3_000, 80);
        AddConversion(result, "cold.fire", "冰霜转火焰", ItemModifierKind.ColdToFireConversionBasisPoints, 1_500, 2_500, 80);
        AddConversion(result, "lightning.fire", "闪电转火焰", ItemModifierKind.LightningToFireConversionBasisPoints, 1_500, 2_500, 80);
        AddConversion(result, "fire.void", "火焰转虚空", ItemModifierKind.FireToVoidConversionBasisPoints, 1_200, 2_000, 60);
        AddConversion(result, "cold.void", "冰霜转虚空", ItemModifierKind.ColdToVoidConversionBasisPoints, 1_200, 2_000, 60);
        AddConversion(result, "lightning.void", "闪电转虚空", ItemModifierKind.LightningToVoidConversionBasisPoints, 1_200, 2_000, 60);
        AddConversion(result, "phys.extra.fire", "物理伤害额外获得火焰", ItemModifierKind.PhysicalAsExtraFireBasisPoints, 800, 1_500, 50);
        AddConversion(result, "phys.extra.cold", "物理伤害额外获得冰霜", ItemModifierKind.PhysicalAsExtraColdBasisPoints, 800, 1_500, 50);
        AddConversion(result, "phys.extra.lightning", "物理伤害额外获得闪电", ItemModifierKind.PhysicalAsExtraLightningBasisPoints, 800, 1_500, 50);
        AddConversion(result, "elemental.extra.void", "元素伤害额外获得虚空", ItemModifierKind.ElementalAsExtraVoidBasisPoints, 800, 1_200, 40);

        AddSix(result, "reservation", "所有技能保留效率", AffixPosition.Suffix, ItemModifierKind.ReservationEfficiencyBasisPoints, 1_000, 1_400, General, weight: 300);
        AddSix(result, "aura.effect", "光环效果提高", AffixPosition.Suffix, ItemModifierKind.IncreasedAuraEffectBasisPoints, 1_000, 1_400, General);
        AddSix(result, "curse.effect", "诅咒效果提高", AffixPosition.Suffix, ItemModifierKind.IncreasedCurseEffectBasisPoints, 1_000, 1_400, General);
        AddSix(result, "curse.duration", "诅咒持续时间提高", AffixPosition.Suffix, ItemModifierKind.IncreasedCurseDurationBasisPoints, 2_000, 3_000, General);
        AddSix(result, "curse.range", "诅咒范围提高", AffixPosition.Suffix, ItemModifierKind.IncreasedCurseRangeBasisPoints, 2_000, 3_000, General);
        AddSix(result, "warcry.effect", "战吼效果提高", AffixPosition.Suffix, ItemModifierKind.IncreasedWarcryEffectBasisPoints, 2_000, 3_000, General);
        AddSix(result, "warcry.range", "战吼范围提高", AffixPosition.Suffix, ItemModifierKind.IncreasedWarcryRangeBasisPoints, 2_000, 3_000, General);
        AddSix(result, "buff.effect", "临时增益效果提高", AffixPosition.Suffix, ItemModifierKind.IncreasedTemporaryBuffEffectBasisPoints, 1_500, 2_500, General);
        AddSix(result, "buff.duration", "临时增益持续时间提高", AffixPosition.Suffix, ItemModifierKind.IncreasedTemporaryBuffDurationBasisPoints, 1_500, 2_500, General);
        AddSix(result, "active.levels", "指定主动技能石等级", AffixPosition.Prefix, ItemModifierKind.ActiveSkillGemLevels, 2, 3, Weapons.Concat([ItemCategory.Amulet, ItemCategory.Shield]), weight: 200);
        AddSix(result, "support.levels", "指定辅助技能石等级", AffixPosition.Prefix, ItemModifierKind.SupportSkillGemLevels, 2, 3, Weapons.Concat([ItemCategory.Amulet, ItemCategory.Shield]), weight: 200);
        AddDiscrete(result, "all.active.levels", "所有主动技能石等级", AffixPosition.Prefix, ItemModifierKind.AllActiveSkillGemLevels, 1, Weapons.Concat([ItemCategory.Amulet, ItemCategory.Shield]), 85, 50);
        AddDiscrete(result, "all.support.levels", "所有辅助技能石等级", AffixPosition.Prefix, ItemModifierKind.AllSupportSkillGemLevels, 1, Weapons.Concat([ItemCategory.Amulet, ItemCategory.Shield]), 85, 50);
        AddDiscrete(result, "unit.maximum", "一种单位上限", AffixPosition.Prefix, ItemModifierKind.AdditionalUnitMaximum, 1, [ItemCategory.Helmet, ItemCategory.Amulet, ItemCategory.Shield], 85, 50);

        AddAttributeStacking(result);
        return result;
    }

    private static void AddAttributeStacking(ICollection<AffixDefinition> target)
    {
        AddCompound(target, "attributes.physique_dexterity", "体魄与灵巧", AffixPosition.Suffix, Jewellery, 85, 500,
            new(ItemModifierKind.Physique, 32, 40), new(ItemModifierKind.Dexterity, 32, 40));
        AddCompound(target, "attributes.spirit_energy", "精神与能量", AffixPosition.Suffix, Jewellery, 85, 500,
            new(ItemModifierKind.Spirit, 32, 40), new(ItemModifierKind.Energy, 32, 40));
        AddCompound(target, "attributes.all", "所有属性", AffixPosition.Suffix, Jewellery, 85, 200,
            new(ItemModifierKind.Physique, 20, 28), new(ItemModifierKind.Dexterity, 20, 28),
            new(ItemModifierKind.Spirit, 20, 28), new(ItemModifierKind.Energy, 20, 28));
        AddDiscreteRange(target, "attributes.physique.percent", "体魄提高", AffixPosition.Suffix, ItemModifierKind.IncreasedPhysiqueBasisPoints, 800, 1_000, Jewellery, 85, 200);
        AddDiscreteRange(target, "attributes.dexterity.percent", "灵巧提高", AffixPosition.Suffix, ItemModifierKind.IncreasedDexterityBasisPoints, 800, 1_000, Jewellery, 85, 200);
        AddDiscreteRange(target, "attributes.spirit.percent", "精神提高", AffixPosition.Suffix, ItemModifierKind.IncreasedSpiritBasisPoints, 800, 1_000, Jewellery, 85, 200);
        AddDiscreteRange(target, "attributes.energy.percent", "能量提高", AffixPosition.Suffix, ItemModifierKind.IncreasedEnergyBasisPoints, 800, 1_000, Jewellery, 85, 200);
        AddDiscreteRange(target, "attributes.all.percent", "所有属性提高", AffixPosition.Suffix, ItemModifierKind.IncreasedAllAttributesBasisPoints, 400, 600, Jewellery, 85, 50);
    }

    private static void AddElementalWeaponDamage(ICollection<AffixDefinition> target, string id, string name,
        ItemModifierKind minimumKind, ItemModifierKind maximumKind, int minimum, int maximum)
    {
        for (int tier = 6; tier >= 1; tier--)
        {
            (int ilvl, int scale) = TierScale(tier);
            int low = Math.Max(1, minimum * scale / 100);
            int high = Math.Max(low + 1, maximum * scale / 100);
            AddCompound(target, $"weapon.added_{id}.t{tier}", $"武器附加{name}伤害", AffixPosition.Prefix,
                Weapons, ilvl, TierWeight(tier, 1_000),
                new(minimumKind, low, Math.Max(low, low * 12 / 10), ItemModifierScope.LocalWeapon),
                new(maximumKind, Math.Max(low + 1, high * 85 / 100), high, ItemModifierScope.LocalWeapon),
                stableFamilyOverride: $"p30.affix.weapon.added_{id}", tier: tier);
        }
    }

    private static void AddConversion(ICollection<AffixDefinition> target, string id, string name,
        ItemModifierKind kind, int minimum, int maximum, int weight) =>
        AddDiscreteRange(target, $"conversion.{id}", name, AffixPosition.Prefix, kind, minimum, maximum,
            Weapons.Concat(Jewellery), 85, weight);

    private static void AddSix(ICollection<AffixDefinition> target, string id, string name, AffixPosition position,
        ItemModifierKind kind, int tierOneMinimum, int tierOneMaximum, IEnumerable<ItemCategory> categories,
        int weight = 1_000, ItemModifierScope localScope = ItemModifierScope.Global,
        IReadOnlyList<string>? requiredTags = null, int firstItemLevel = 1)
    {
        ItemCategory[] applicable = categories.Distinct().ToArray();
        for (int tier = 6; tier >= 1; tier--)
        {
            (int ilvl, int scale) = TierScale(tier);
            ilvl = Math.Max(ilvl, firstItemLevel);
            int minimum = Math.Max(1, tierOneMinimum * scale / 100);
            int maximum = Math.Max(minimum, tierOneMaximum * scale / 100);
            var component = new AffixModifierComponent(kind, minimum, maximum, localScope, name);
            target.Add(new AffixDefinition($"p30.affix.{id}", name, applicable[0], position, tier, ilvl,
                minimum, maximum, TierWeight(tier, weight), kind, $"p30.group.{id}", applicable,
                SourceId: $"P30:{id}:T{tier}", RawText: name, Local: localScope != ItemModifierScope.Global,
                Source: "P30", Components: [component], RequiredBaseTags: requiredTags));
        }
    }

    private static void AddDiscrete(ICollection<AffixDefinition> target, string id, string name, AffixPosition position,
        ItemModifierKind kind, int value, IEnumerable<ItemCategory> categories, int itemLevel, int weight,
        IReadOnlyList<string>? requiredTags = null) =>
        AddDiscreteRange(target, id, name, position, kind, value, value, categories, itemLevel, weight, requiredTags);

    private static void AddDiscreteRange(ICollection<AffixDefinition> target, string id, string name, AffixPosition position,
        ItemModifierKind kind, int minimum, int maximum, IEnumerable<ItemCategory> categories, int itemLevel, int weight,
        IReadOnlyList<string>? requiredTags = null)
    {
        ItemCategory[] applicable = categories.Distinct().ToArray();
        target.Add(new AffixDefinition($"p30.affix.{id}", name, applicable[0], position, 1, itemLevel,
            minimum, maximum, weight, kind, $"p30.group.{id}", applicable, SourceId: $"P30:{id}:T1",
            RawText: name, Source: "P30", Components: [new(kind, minimum, maximum, DisplayText: name)],
            RequiredBaseTags: requiredTags));
    }

    private static void AddCompound(ICollection<AffixDefinition> target, string id, string name,
        AffixPosition position, IEnumerable<ItemCategory> categories, int itemLevel, int weight,
        AffixModifierComponent first, AffixModifierComponent second,
        AffixModifierComponent? third = null, AffixModifierComponent? fourth = null,
        string? stableFamilyOverride = null, int tier = 1)
    {
        ItemCategory[] applicable = categories.Distinct().ToArray();
        AffixModifierComponent[] components = [first, second, .. new[] { third, fourth }.Where(value => value is not null).Cast<AffixModifierComponent>()];
        string family = stableFamilyOverride ?? $"p30.affix.{id}";
        target.Add(new AffixDefinition(family, name, applicable[0], position, tier, itemLevel,
            first.MinimumValue, first.MaximumValue, weight, first.Kind, $"p30.group.{family}", applicable,
            SourceId: $"P30:{id}:T{tier}", RawText: name, Source: "P30", Components: components));
    }

    private static IReadOnlyList<AffixModifierComponent> AddedDamageComponents(string rawText,
        ItemModifierKind minimumKind, ItemModifierKind maximumKind, ItemModifierScope scope)
    {
        (int Min, int Max)[] ranges = Ranges(rawText, percent: false);
        return ranges.Length >= 2
            ? [new(minimumKind, ranges[0].Min, ranges[0].Max, scope), new(maximumKind, ranges[1].Min, ranges[1].Max, scope)]
            : [new(minimumKind, 1, 1, scope), new(maximumKind, 2, 2, scope)];
    }

    private static IReadOnlyList<AffixModifierComponent> PairComponents(string rawText,
        ItemModifierKind firstKind, ItemModifierScope firstScope, ItemModifierKind secondKind,
        bool secondIsPercent = false, ItemModifierScope secondScope = ItemModifierScope.Global)
    {
        (int Min, int Max)[] ranges = Ranges(rawText, percent: false);
        if (ranges.Length < 2) return [new(firstKind, 1, 1, firstScope), new(secondKind, 1, 1)];
        int firstScale = rawText.IndexOf('%') >= 0 && rawText.IndexOf('%') < rawText.IndexOf('/') ? 100 : 1;
        int secondScale = secondIsPercent ? 100 : 1;
        return [
            new(firstKind, ranges[0].Min * firstScale, ranges[0].Max * firstScale, firstScope),
            new(secondKind, ranges[1].Min * secondScale, ranges[1].Max * secondScale, secondScope),
        ];
    }

    private static IReadOnlyList<AffixModifierComponent> SameRange(AffixDefinition affix,
        params (ItemModifierKind Kind, ItemModifierScope Scope)[] effects) => effects
        .Select(effect => new AffixModifierComponent(effect.Kind, affix.MinimumValue, affix.MaximumValue, effect.Scope, affix.RawText))
        .ToArray();

    private static ItemModifierScope LocalScope(ItemModifierKind kind) => kind switch
    {
        ItemModifierKind.IncreasedPhysicalDamageBasisPoints or ItemModifierKind.AddedPhysicalDamage or
        ItemModifierKind.AddedMinimumPhysicalDamage or ItemModifierKind.AddedMaximumPhysicalDamage or
        ItemModifierKind.IncreasedAttackSpeedBasisPoints or ItemModifierKind.IncreasedCriticalChanceBasisPoints => ItemModifierScope.LocalWeapon,
        ItemModifierKind.BlockChanceBasisPoints or ItemModifierKind.IncreasedLocalBlockBasisPoints => ItemModifierScope.LocalBlock,
        _ => ItemModifierScope.LocalDefense,
    };

    private static (int Min, int Max)[] Ranges(string text, bool percent) => NumberRangeRegex().Matches(text)
        .Select(match => (
            Parse(match.Groups[1].Value, percent),
            Parse(match.Groups[2].Value, percent)))
        .ToArray();

    private static int Parse(string value, bool percent) => (int)Math.Round(
        double.Parse(value, CultureInfo.InvariantCulture) * (percent ? 100 : 1), MidpointRounding.AwayFromZero);

    private static (int ItemLevel, int Scale) TierScale(int tier) => tier switch
    {
        6 => (1, 35),
        5 => (20, 45),
        4 => (40, 60),
        3 => (60, 75),
        2 => (75, 88),
        _ => (85, 100),
    };

    private static int TierWeight(int tier, int baseWeight) => Math.Max(1, baseWeight * (tier switch
    {
        6 => 100,
        5 => 90,
        4 => 75,
        3 => 60,
        2 => 45,
        _ => 30,
    }) / 100);

    [GeneratedRegex(@"(-?\d+(?:\.\d+)?)\s*-\s*(-?\d+(?:\.\d+)?)")]
    private static partial Regex NumberRangeRegex();
}

public sealed record P30ConversionAllocation(
    int ToFireBasisPoints,
    int ToColdBasisPoints,
    int ToLightningBasisPoints,
    int ToVoidBasisPoints)
{
    public int TotalBasisPoints => ToFireBasisPoints + ToColdBasisPoints + ToLightningBasisPoints + ToVoidBasisPoints;
}

public static class P30ConversionRules
{
    public static P30ConversionAllocation NormalizePhysical(IEnumerable<RolledAffixComponent> effects)
    {
        int Sum(ItemModifierKind kind) => effects.Where(effect => effect.Kind == kind).Sum(effect => Math.Max(0, effect.Value));
        int fire = Sum(ItemModifierKind.PhysicalToFireConversionBasisPoints);
        int cold = Sum(ItemModifierKind.PhysicalToColdConversionBasisPoints);
        int lightning = Sum(ItemModifierKind.PhysicalToLightningConversionBasisPoints);
        int @void = Sum(ItemModifierKind.PhysicalToVoidConversionBasisPoints);
        int total = fire + cold + lightning + @void;
        if (total <= 10_000) return new(fire, cold, lightning, @void);
        int scaledFire = fire * 10_000 / total;
        int scaledCold = cold * 10_000 / total;
        int scaledLightning = lightning * 10_000 / total;
        int scaledVoid = 10_000 - scaledFire - scaledCold - scaledLightning;
        return new(scaledFire, scaledCold, scaledLightning, scaledVoid);
    }
}
