namespace GameForWork.Core.Campaign.Progression;

internal static class PassiveClusterCatalog
{
    public const int ExpectedNodeCount = 1_200;
    public const float JewelRadius = 150f;

    private sealed record Theme(string Name, string Mastery, PassiveEffectKind Primary, PassiveEffectKind Secondary);
    private sealed record Sector(PassiveBranch Branch, string Name, PassiveEffectKind Attribute, Theme[] Themes);
    private sealed record StartTheme(string Name, PassiveEffectKind Primary, PassiveEffectKind Secondary);
    private sealed record StartAttribute(string Name, PassiveEffect[] Effects);
    private sealed record Start(PassiveStartKind Kind, string Name, int FirstSector, int SecondSector, float Angle,
        StartTheme[] Themes, StartAttribute[] Attributes);

    private static readonly Sector[] Sectors =
    [
        S(PassiveBranch.HeavyWeapon, "武备", PassiveEffectKind.FlatPhysique,
            T("双手重兵", "two_hand", PassiveEffectKind.IncreasedTwoHandDamageBasisPoints, PassiveEffectKind.IncreasedAttackSpeedBasisPoints),
            T("剑术", "sword", PassiveEffectKind.IncreasedSwordDamageBasisPoints, PassiveEffectKind.IncreasedCriticalChanceBasisPoints),
            T("战斧", "axe", PassiveEffectKind.IncreasedAxeDamageBasisPoints, PassiveEffectKind.IncreasedBleedDamageBasisPoints),
            T("战锤", "mace", PassiveEffectKind.IncreasedMaceDamageBasisPoints, PassiveEffectKind.IncreasedAreaDamageBasisPoints),
            T("单手武备", "one_hand", PassiveEffectKind.IncreasedOneHandDamageBasisPoints, PassiveEffectKind.BlockChanceBasisPoints),
            T("双持", "dual_wield", PassiveEffectKind.IncreasedDualWieldDamageBasisPoints, PassiveEffectKind.IncreasedAttackSpeedBasisPoints)),
        S(PassiveBranch.Bleed, "血战", PassiveEffectKind.FlatPhysique,
            T("流血", "bleed", PassiveEffectKind.IncreasedBleedDamageBasisPoints, PassiveEffectKind.IncreasedBleedChanceBasisPoints),
            T("生命偷取", "life_leech", PassiveEffectKind.IncreasedLifeLeechRateBasisPoints, PassiveEffectKind.IncreasedPhysicalDamageBasisPoints),
            T("击中回复", "life_on_hit", PassiveEffectKind.LifeOnHit, PassiveEffectKind.IncreasedAttackSpeedBasisPoints),
            T("低生命", "low_life", PassiveEffectKind.IncreasedMaximumLifeBasisPoints, PassiveEffectKind.FlatLifeRegeneration),
            T("战吼", "warcry", PassiveEffectKind.IncreasedWarCryCooldownRecoveryBasisPoints, PassiveEffectKind.IncreasedWarCryRangeBasisPoints),
            T("眩晕", "stun", PassiveEffectKind.IncreasedMaceDamageBasisPoints, PassiveEffectKind.IncreasedAreaDamageBasisPoints)),
        S(PassiveBranch.Defense, "坚守", PassiveEffectKind.FlatPhysique,
            T("生命", "life", PassiveEffectKind.IncreasedMaximumLifeBasisPoints, PassiveEffectKind.FlatMaximumLife),
            T("护甲", "armor", PassiveEffectKind.IncreasedArmorBasisPoints, PassiveEffectKind.FlatLifeRegeneration),
            T("盾击", "shield_attack", PassiveEffectKind.IncreasedShieldAttackDamageBasisPoints, PassiveEffectKind.BlockChanceBasisPoints),
            T("攻击格挡", "attack_block", PassiveEffectKind.BlockChanceBasisPoints, PassiveEffectKind.IncreasedArmorBasisPoints),
            T("反击", "counter", PassiveEffectKind.IncreasedMeleeDamageBasisPoints, PassiveEffectKind.IncreasedCooldownRecoveryBasisPoints),
            T("护卫", "guard", PassiveEffectKind.IncreasedMaximumLifeBasisPoints, PassiveEffectKind.IncreasedCooldownRecoveryBasisPoints)),
        S(PassiveBranch.Mobility, "游击", PassiveEffectKind.FlatDexterity,
            T("弓术", "bow", PassiveEffectKind.IncreasedBowDamageBasisPoints, PassiveEffectKind.IncreasedProjectileDamageBasisPoints),
            T("投射物", "projectile", PassiveEffectKind.IncreasedProjectileDamageBasisPoints, PassiveEffectKind.IncreasedSkillRangeBasisPoints),
            T("穿透", "pierce", PassiveEffectKind.IncreasedProjectileDamageBasisPoints, PassiveEffectKind.FlatAccuracy),
            T("连锁", "chain", PassiveEffectKind.IncreasedProjectileDamageBasisPoints, PassiveEffectKind.IncreasedCooldownRecoveryBasisPoints),
            T("移动攻击", "mobile_attack", PassiveEffectKind.IncreasedMovementSpeedBasisPoints, PassiveEffectKind.IncreasedAttackSpeedBasisPoints),
            T("疾射", "rapid_fire", PassiveEffectKind.IncreasedAttackSpeedBasisPoints, PassiveEffectKind.FlatAccuracy)),
        S(PassiveBranch.Critical, "暗刃", PassiveEffectKind.FlatDexterity,
            T("匕首", "dagger", PassiveEffectKind.IncreasedDaggerDamageBasisPoints, PassiveEffectKind.IncreasedCriticalChanceBasisPoints),
            T("暴击", "critical", PassiveEffectKind.IncreasedCriticalChanceBasisPoints, PassiveEffectKind.IncreasedCriticalMultiplierBasisPoints),
            T("背袭", "backstab", PassiveEffectKind.IncreasedCriticalMultiplierBasisPoints, PassiveEffectKind.IncreasedMeleeDamageBasisPoints),
            T("处决", "execute", PassiveEffectKind.IncreasedAttackSkillDamageBasisPoints, PassiveEffectKind.IncreasedCriticalChanceBasisPoints),
            T("闪避反击", "evasion_counter", PassiveEffectKind.IncreasedEvasionBasisPoints, PassiveEffectKind.IncreasedMeleeDamageBasisPoints),
            T("标记", "mark", PassiveEffectKind.IncreasedCriticalChanceBasisPoints, PassiveEffectKind.IncreasedSkillRangeBasisPoints)),
        S(PassiveBranch.Accuracy, "巧毒", PassiveEffectKind.FlatDexterity,
            T("毒素", "poison", PassiveEffectKind.IncreasedDamageOverTimeBasisPoints, PassiveEffectKind.IncreasedDaggerDamageBasisPoints),
            T("持续伤害", "dot", PassiveEffectKind.IncreasedDamageOverTimeBasisPoints, PassiveEffectKind.IncreasedSkillRangeBasisPoints),
            T("陷阱", "trap", PassiveEffectKind.IncreasedTrapDamageBasisPoints, PassiveEffectKind.IncreasedCooldownRecoveryBasisPoints),
            T("药剂", "flask", PassiveEffectKind.IncreasedLifeFlaskEffectBasisPoints, PassiveEffectKind.IncreasedMovementSpeedBasisPoints),
            T("法术压制", "suppression", PassiveEffectKind.SpellSuppressionBasisPoints, PassiveEffectKind.IncreasedEvasionBasisPoints),
            T("精准", "accuracy", PassiveEffectKind.FlatAccuracy, PassiveEffectKind.IncreasedAttackSpeedBasisPoints)),
        S(PassiveBranch.Mana, "灵契", PassiveEffectKind.FlatSpirit,
            T("召唤军团", "minion", PassiveEffectKind.IncreasedMinionDamageBasisPoints, PassiveEffectKind.IncreasedCooldownRecoveryBasisPoints),
            T("灵兽", "companion", PassiveEffectKind.IncreasedCompanionDamageBasisPoints, PassiveEffectKind.IncreasedMaximumLifeBasisPoints),
            T("构装体", "construct", PassiveEffectKind.IncreasedConstructDamageBasisPoints, PassiveEffectKind.IncreasedArmorBasisPoints),
            T("光环", "aura", PassiveEffectKind.IncreasedAuraEffectBasisPoints, PassiveEffectKind.ReducedSkillCostBasisPoints),
            T("祝福", "blessing", PassiveEffectKind.IncreasedAuraEffectBasisPoints, PassiveEffectKind.IncreasedCooldownRecoveryBasisPoints),
            T("诅咒", "curse", PassiveEffectKind.IncreasedCurseEffectBasisPoints, PassiveEffectKind.IncreasedSkillRangeBasisPoints)),
        S(PassiveBranch.WarCry, "心流", PassiveEffectKind.FlatSpirit,
            T("法力", "mana", PassiveEffectKind.FlatMaximumMana, PassiveEffectKind.IncreasedManaRegenerationBasisPoints),
            T("法力偷取", "mana_leech", PassiveEffectKind.IncreasedManaLeechRateBasisPoints, PassiveEffectKind.ManaOnHit),
            T("保留", "reservation", PassiveEffectKind.ReducedSkillCostBasisPoints, PassiveEffectKind.IncreasedAuraEffectBasisPoints),
            T("技能消耗", "skill_cost", PassiveEffectKind.ReducedSkillCostBasisPoints, PassiveEffectKind.FlatMaximumMana),
            T("冷却", "cooldown", PassiveEffectKind.IncreasedCooldownRecoveryBasisPoints, PassiveEffectKind.IncreasedSkillRangeBasisPoints),
            T("范围", "area", PassiveEffectKind.IncreasedAreaDamageBasisPoints, PassiveEffectKind.IncreasedSkillRangeBasisPoints)),
        S(PassiveBranch.Flask, "行者", PassiveEffectKind.FlatSpirit,
            T("徒手", "unarmed", PassiveEffectKind.IncreasedUnarmedDamageBasisPoints, PassiveEffectKind.IncreasedAttackSpeedBasisPoints),
            T("连击", "combo", PassiveEffectKind.IncreasedAttackSkillDamageBasisPoints, PassiveEffectKind.IncreasedAttackSpeedBasisPoints),
            T("姿态", "stance", PassiveEffectKind.IncreasedMeleeDamageBasisPoints, PassiveEffectKind.IncreasedEvasionBasisPoints),
            T("位移", "movement", PassiveEffectKind.IncreasedMovementSpeedBasisPoints, PassiveEffectKind.IncreasedCooldownRecoveryBasisPoints),
            T("触发", "trigger", PassiveEffectKind.IncreasedCooldownRecoveryBasisPoints, PassiveEffectKind.IncreasedSpellDamageBasisPoints),
            T("战歌", "battle_song", PassiveEffectKind.IncreasedWarCryRangeBasisPoints, PassiveEffectKind.IncreasedAuraEffectBasisPoints)),
        S(PassiveBranch.Elemental, "秘法", PassiveEffectKind.FlatEnergy,
            T("法杖", "wand", PassiveEffectKind.IncreasedWandDamageBasisPoints, PassiveEffectKind.IncreasedSpellDamageBasisPoints),
            T("法术", "spell", PassiveEffectKind.IncreasedSpellDamageBasisPoints, PassiveEffectKind.IncreasedCriticalChanceBasisPoints),
            T("元素", "elemental", PassiveEffectKind.IncreasedElementalDamageBasisPoints, PassiveEffectKind.IncreasedAreaDamageBasisPoints),
            T("火焰", "fire", PassiveEffectKind.IncreasedElementalDamageBasisPoints, PassiveEffectKind.FireResistanceBasisPoints),
            T("冰霜", "cold", PassiveEffectKind.IncreasedElementalDamageBasisPoints, PassiveEffectKind.ColdResistanceBasisPoints),
            T("闪电", "lightning", PassiveEffectKind.IncreasedElementalDamageBasisPoints, PassiveEffectKind.LightningResistanceBasisPoints)),
        S(PassiveBranch.Void, "虚界", PassiveEffectKind.FlatEnergy,
            T("虚空", "void", PassiveEffectKind.IncreasedVoidDamageBasisPoints, PassiveEffectKind.VoidResistanceBasisPoints),
            T("侵蚀", "erosion", PassiveEffectKind.IncreasedVoidDamageBasisPoints, PassiveEffectKind.IncreasedDamageOverTimeBasisPoints),
            T("凋零", "wither", PassiveEffectKind.IncreasedDamageOverTimeBasisPoints, PassiveEffectKind.IncreasedCurseEffectBasisPoints),
            T("禁咒", "void_curse", PassiveEffectKind.IncreasedCurseEffectBasisPoints, PassiveEffectKind.IncreasedVoidDamageBasisPoints),
            T("护盾吸收", "shield_leech", PassiveEffectKind.IncreasedShieldLeechRateBasisPoints, PassiveEffectKind.IncreasedShieldBasisPoints),
            T("献祭", "sacrifice", PassiveEffectKind.MoreDamageBasisPoints, PassiveEffectKind.IncreasedMaximumLifeBasisPoints)),
        S(PassiveBranch.Shield, "灵盾", PassiveEffectKind.FlatEnergy,
            T("能量护盾", "energy_shield", PassiveEffectKind.IncreasedShieldBasisPoints, PassiveEffectKind.IncreasedEnergyShieldRechargeBasisPoints),
            T("护盾充能", "shield_recharge", PassiveEffectKind.IncreasedEnergyShieldRechargeBasisPoints, PassiveEffectKind.IncreasedShieldBasisPoints),
            T("法术格挡", "spell_block", PassiveEffectKind.BlockChanceBasisPoints, PassiveEffectKind.SpellSuppressionBasisPoints),
            T("护盾施法", "shield_cast", PassiveEffectKind.IncreasedSpellDamageBasisPoints, PassiveEffectKind.IncreasedShieldBasisPoints),
            T("屏障", "barrier", PassiveEffectKind.IncreasedShieldBasisPoints, PassiveEffectKind.VoidResistanceBasisPoints),
            T("混合防御", "hybrid_defense", PassiveEffectKind.IncreasedArmorBasisPoints, PassiveEffectKind.IncreasedShieldBasisPoints)),
    ];

    private static readonly Start[] Starts =
    [
        MakeStart(PassiveStartKind.Physique, "斗士", 0, 1, -1.309f,
            [("双手重兵", PassiveEffectKind.IncreasedTwoHandDamageBasisPoints, PassiveEffectKind.IncreasedAttackSpeedBasisPoints),
             ("单手锤盾", PassiveEffectKind.IncreasedOneHandDamageBasisPoints, PassiveEffectKind.IncreasedShieldAttackDamageBasisPoints),
             ("双持战法", PassiveEffectKind.IncreasedDualWieldDamageBasisPoints, PassiveEffectKind.IncreasedAttackSpeedBasisPoints)],
            [A("灵巧", PassiveEffectKind.FlatDexterity), A("精神", PassiveEffectKind.FlatSpirit), A("能量", PassiveEffectKind.FlatEnergy)]),
        MakeStart(PassiveStartKind.Dexterity, "侠客", 3, 4, .262f,
            [("弓与箭袋", PassiveEffectKind.IncreasedBowDamageBasisPoints, PassiveEffectKind.IncreasedProjectileDamageBasisPoints),
             ("匕首双持", PassiveEffectKind.IncreasedDaggerDamageBasisPoints, PassiveEffectKind.IncreasedCriticalChanceBasisPoints),
             ("投射陷阱", PassiveEffectKind.IncreasedTrapDamageBasisPoints, PassiveEffectKind.IncreasedProjectileDamageBasisPoints)],
            [A("体魄", PassiveEffectKind.FlatPhysique), A("精神", PassiveEffectKind.FlatSpirit), A("能量", PassiveEffectKind.FlatEnergy)]),
        MakeStart(PassiveStartKind.Spirit, "灵能使", 6, 7, 1.833f,
            [("召唤媒介", PassiveEffectKind.IncreasedMinionDamageBasisPoints, PassiveEffectKind.IncreasedCooldownRecoveryBasisPoints),
             ("咒术法杖", PassiveEffectKind.IncreasedWandDamageBasisPoints, PassiveEffectKind.IncreasedCurseEffectBasisPoints),
             ("光环祝福", PassiveEffectKind.IncreasedAuraEffectBasisPoints, PassiveEffectKind.ReducedSkillCostBasisPoints)],
            [A("体魄", PassiveEffectKind.FlatPhysique), A("灵巧", PassiveEffectKind.FlatDexterity), A("能量", PassiveEffectKind.FlatEnergy)]),
        MakeStart(PassiveStartKind.Energy, "秘术师", 9, 10, 3.403f,
            [("法杖秘术", PassiveEffectKind.IncreasedWandDamageBasisPoints, PassiveEffectKind.IncreasedSpellDamageBasisPoints),
             ("焦点护盾", PassiveEffectKind.IncreasedShieldBasisPoints, PassiveEffectKind.IncreasedEnergyShieldRechargeBasisPoints),
             ("元素虚界", PassiveEffectKind.IncreasedElementalDamageBasisPoints, PassiveEffectKind.IncreasedVoidDamageBasisPoints)],
            [A("体魄", PassiveEffectKind.FlatPhysique), A("灵巧", PassiveEffectKind.FlatDexterity), A("精神", PassiveEffectKind.FlatSpirit)]),
        MakeStart(PassiveStartKind.DexteritySpirit, "僧侣", 5, 6, 1.309f,
            [("徒手缠带", PassiveEffectKind.IncreasedUnarmedDamageBasisPoints, PassiveEffectKind.IncreasedAttackSpeedBasisPoints),
             ("灵兽护符", PassiveEffectKind.IncreasedCompanionDamageBasisPoints, PassiveEffectKind.IncreasedMaximumLifeBasisPoints),
             ("幻身步法", PassiveEffectKind.IncreasedMovementSpeedBasisPoints, PassiveEffectKind.IncreasedCooldownRecoveryBasisPoints)],
            [A("体魄", PassiveEffectKind.FlatPhysique), A("能量", PassiveEffectKind.FlatEnergy),
             ("身心均衡", PassiveEffectKind.FlatDexterity, PassiveEffectKind.FlatSpirit)]),
        MakeStart(PassiveStartKind.PhysiqueEnergy, "隐士", 11, 0, -1.833f,
            [("符刃交错", PassiveEffectKind.IncreasedOneHandDamageBasisPoints, PassiveEffectKind.IncreasedSpellDamageBasisPoints),
             ("构装偶像", PassiveEffectKind.IncreasedConstructDamageBasisPoints, PassiveEffectKind.IncreasedCooldownRecoveryBasisPoints),
             ("魔铠一体", PassiveEffectKind.IncreasedArmorBasisPoints, PassiveEffectKind.IncreasedShieldBasisPoints)],
            [A("灵巧", PassiveEffectKind.FlatDexterity), A("精神", PassiveEffectKind.FlatSpirit),
             ("铠术均衡", PassiveEffectKind.FlatPhysique, PassiveEffectKind.FlatEnergy)]),
    ];

    public static IReadOnlyList<PassiveNodeDefinition> Build()
    {
        var nodes = new List<PassiveNodeDefinition>(ExpectedNodeCount);
        AddCentralRing(nodes);
        for (int sector = 0; sector < Sectors.Length; sector++) AddTravelSpine(nodes, sector);
        AddStarts(nodes);
        AddStartGardens(nodes);
        for (int sector = 0; sector < Sectors.Length; sector++)
        {
            AddClusters(nodes, sector);
            AddRules(nodes, sector);
            AddJewelSockets(nodes, sector);
        }
        if (nodes.Count != ExpectedNodeCount)
            throw new InvalidDataException($"Characters passive catalog produced {nodes.Count} nodes instead of {ExpectedNodeCount}.");
        return nodes;
    }

    public static string StartNode(PassiveStartKind start) => start switch
    {
        PassiveStartKind.Physique => "core.passive.v3.start.fighter",
        PassiveStartKind.Dexterity => "core.passive.v3.start.rogue",
        PassiveStartKind.Spirit => "core.passive.v3.start.psion",
        PassiveStartKind.Energy => "core.passive.v3.start.occultist",
        PassiveStartKind.DexteritySpirit => "core.passive.v3.start.monk",
        PassiveStartKind.PhysiqueEnergy => "core.passive.v3.start.hermit",
        _ => throw new ArgumentOutOfRangeException(nameof(start)),
    };

    public static IReadOnlyList<PassiveEffect> MasteryOptions(PassiveNodeDefinition node)
    {
        if (node.Kind != PassiveNodeKind.Mastery) return [];
        PassiveEffectKind primary = node.Effects[0].Kind;
        PassiveEffectKind secondary = node.Effects.Count > 1 ? node.Effects[1].Kind : PassiveEffectKind.IncreasedAttackSkillDamageBasisPoints;
        if (node.MasteryGroup.Contains("leech", StringComparison.Ordinal))
            return [new(primary, 2_500), new(secondary, 1_600), new(PassiveEffectKind.LifeOnHit, 12), new(PassiveEffectKind.ManaOnHit, 8), new(PassiveEffectKind.IncreasedMaximumLifeBasisPoints, 700), new(PassiveEffectKind.ReducedSkillCostBasisPoints, 600)];
        if (node.MasteryGroup is "minion" or "companion" or "construct" or "trap")
            return [new(primary, 2_500), new(secondary, 1_500), new(PassiveEffectKind.IncreasedCooldownRecoveryBasisPoints, 800), new(PassiveEffectKind.IncreasedAreaDamageBasisPoints, 1_600), new(PassiveEffectKind.FlatMaximumMana, 30), new(PassiveEffectKind.IncreasedSkillRangeBasisPoints, 1_200)];
        if (node.MasteryGroup is "aura" or "blessing" or "curse" or "reservation")
            return [new(primary, 1_500), new(secondary, 1_500), new(PassiveEffectKind.ReducedSkillCostBasisPoints, 700), new(PassiveEffectKind.IncreasedSkillRangeBasisPoints, 1_200), new(PassiveEffectKind.IncreasedCooldownRecoveryBasisPoints, 700), new(PassiveEffectKind.FlatSpirit, 20)];
        if (node.MasteryGroup is "armor" or "attack_block" or "spell_block" or "energy_shield" or "shield_recharge" or "barrier" or "hybrid_defense")
            return [new(primary, 2_500), new(secondary, 1_500), new(PassiveEffectKind.IncreasedMaximumLifeBasisPoints, 700), new(PassiveEffectKind.FlatLifeRegeneration, 20), new(PassiveEffectKind.VoidResistanceBasisPoints, 500), new(PassiveEffectKind.SpellSuppressionBasisPoints, 500)];
        return [new(primary, 2_500), new(secondary, 2_000), new(PassiveEffectKind.IncreasedAttackSpeedBasisPoints, 800), new(PassiveEffectKind.IncreasedCriticalChanceBasisPoints, 2_000), new(PassiveEffectKind.FlatAccuracy, 75), new(PassiveEffectKind.IncreasedSkillRangeBasisPoints, 1_200)];
    }

    private static void AddCentralRing(ICollection<PassiveNodeDefinition> nodes)
    {
        PassiveEffectKind[] attributes = [PassiveEffectKind.FlatPhysique, PassiveEffectKind.FlatDexterity,
            PassiveEffectKind.FlatSpirit, PassiveEffectKind.FlatEnergy, PassiveEffectKind.FlatSpirit, PassiveEffectKind.FlatEnergy];
        for (int index = 0; index < 6; index++)
        {
            float angle = -MathF.PI / 2 + index * MathF.Tau / 6;
            string previous = CenterId((index + 5) % 6);
            string next = CenterId((index + 1) % 6);
            string[] sectorLinks = [TravelId(index * 2, 0), TravelId(index * 2 + 1, 0)];
            nodes.Add(new(CenterId(index), $"六途交汇·{index + 1}", Sectors[index * 2].Branch,
                PassiveNodeKind.Small, previous, [new(attributes[index], 10)], [previous, next, .. sectorLinks],
                MathF.Cos(angle) * 92, MathF.Sin(angle) * 92, -1));
        }
    }

    private static void AddTravelSpine(ICollection<PassiveNodeDefinition> nodes, int sectorIndex)
    {
        Sector sector = Sectors[sectorIndex];
        float angle = SectorAngle(sectorIndex);
        for (int index = 0; index < 16; index++)
        {
            string previous = index == 0 ? CenterId(sectorIndex / 2) : TravelId(sectorIndex, index - 1);
            string? next = index == 15 ? null : TravelId(sectorIndex, index + 1);
            var links = new List<string> { previous };
            if (next is not null) links.Add(next);
            float radius = 150 + index * 38;
            nodes.Add(new(TravelId(sectorIndex, index), $"{sector.Name}之路 {index + 1:00}", sector.Branch,
                PassiveNodeKind.Small, previous, [new(sector.Attribute, 10)], links,
                MathF.Cos(angle) * radius, MathF.Sin(angle) * radius, sectorIndex));
        }
    }

    private static void AddStarts(ICollection<PassiveNodeDefinition> nodes)
    {
        foreach (Start start in Starts)
        {
            string[] links = Enumerable.Range(0, 3).Select(index => StartGardenId(start.Kind, $"cluster.{index}.small.0"))
                .Concat(Enumerable.Range(0, 3).Select(index => StartGardenId(start.Kind, $"attribute.{index}.path")))
                .Concat([TravelId(start.FirstSector, 15), TravelId(start.SecondSector, 15)]).ToArray();
            nodes.Add(new(StartNode(start.Kind), $"{start.Name}起点", Sectors[start.FirstSector].Branch,
                PassiveNodeKind.Start, null, [], links, MathF.Cos(start.Angle) * 820,
                MathF.Sin(start.Angle) * 820, -1, start.Kind, "免费且不可退还的职业锚点"));
        }
    }

    private static void AddStartGardens(ICollection<PassiveNodeDefinition> nodes)
    {
        foreach (Start start in Starts)
        {
            string startId = StartNode(start.Kind);
            float[] offsets = [-.17f, 0, .17f];
            for (int cluster = 0; cluster < 3; cluster++)
            {
                StartTheme theme = start.Themes[cluster];
                string previous = startId;
                for (int index = 0; index < 4; index++)
                {
                    string id = StartGardenId(start.Kind, $"cluster.{cluster}.small.{index}");
                    string next = index == 3
                        ? StartGardenId(start.Kind, $"cluster.{cluster}.notable")
                        : StartGardenId(start.Kind, $"cluster.{cluster}.small.{index + 1}");
                    float angle = start.Angle + offsets[cluster];
                    float radius = 780 - index * 38;
                    PassiveEffectKind effect = index == 2 ? theme.Secondary : theme.Primary;
                    nodes.Add(new(id, $"{theme.Name}·{index + 1}", Sectors[start.FirstSector].Branch,
                        PassiveNodeKind.Small, previous, [new(effect, SmallValue(effect, index))], [previous, next],
                        MathF.Cos(angle) * radius, MathF.Sin(angle) * radius, -2));
                    previous = id;
                }
                string notable = StartGardenId(start.Kind, $"cluster.{cluster}.notable");
                string connector = StartGardenId(start.Kind, $"cluster.{cluster}.connector");
                int targetSector = cluster < 2 ? start.FirstSector : start.SecondSector;
                float notableAngle = start.Angle + offsets[cluster];
                float connectorAngle = (SectorAngle(targetSector) + start.Angle) * .5f + (cluster - 1) * .028f;
                nodes.Add(new(notable, $"{theme.Name}·起势", Sectors[targetSector].Branch, PassiveNodeKind.Notable,
                    previous, [new(theme.Primary, NotableValue(theme.Primary)), new(theme.Secondary, NotableValue(theme.Secondary))],
                    [previous, connector], MathF.Cos(notableAngle) * 620, MathF.Sin(notableAngle) * 620, -2));
                nodes.Add(new(connector, $"{start.Name}·通途 {cluster + 1}", Sectors[targetSector].Branch,
                    PassiveNodeKind.Small, notable, [new(Sectors[targetSector].Attribute, 10)],
                    [notable, TravelId(targetSector, 15)], MathF.Cos(connectorAngle) * 675,
                    MathF.Sin(connectorAngle) * 675, -2));
            }

            for (int attribute = 0; attribute < 3; attribute++)
            {
                StartAttribute bonus = start.Attributes[attribute];
                float angle = start.Angle + offsets[attribute] * .8f;
                string path = StartGardenId(start.Kind, $"attribute.{attribute}.path");
                string notable = StartGardenId(start.Kind, $"attribute.{attribute}.notable");
                nodes.Add(new(path, $"{bonus.Name}之径", Sectors[start.FirstSector].Branch, PassiveNodeKind.Small,
                    startId, bonus.Effects.Select(effect => effect with { Value = Math.Min(10, effect.Value) }).ToArray(),
                    [startId, notable], MathF.Cos(angle) * 852, MathF.Sin(angle) * 852, -2));
                nodes.Add(new(notable, $"{bonus.Name} +30", Sectors[start.FirstSector].Branch, PassiveNodeKind.Notable,
                    path, bonus.Effects, [path], MathF.Cos(angle) * 888, MathF.Sin(angle) * 888, -2));
            }
        }
    }

    private static void AddClusters(ICollection<PassiveNodeDefinition> nodes, int sectorIndex)
    {
        Sector sector = Sectors[sectorIndex];
        float baseAngle = SectorAngle(sectorIndex);
        float[] offsets = [-.22f, -.14f, -.07f, .07f, .14f, .22f];
        for (int cluster = 0; cluster < sector.Themes.Length; cluster++)
        {
            Theme theme = sector.Themes[cluster];
            string previous = TravelId(sectorIndex, 2 + cluster * 2);
            float centerRadius = 270 + cluster / 2 * 175;
            for (int index = 0; index < 8; index++)
            {
                string id = ClusterId(sectorIndex, cluster, $"small.{index:00}");
                string next = index == 7 ? ClusterId(sectorIndex, cluster, "notable.00") : ClusterId(sectorIndex, cluster, $"small.{index + 1:00}");
                float radius = centerRadius + index * 16;
                float angle = baseAngle + offsets[cluster] + (index - 4.5f) * .006f;
                PassiveEffectKind effect = index % 3 == 2 ? theme.Secondary : theme.Primary;
                nodes.Add(new(id, $"{theme.Name}·{index + 1}", sector.Branch, PassiveNodeKind.Small, previous,
                    [new(effect, SmallValue(effect, index))], [previous, next], MathF.Cos(angle) * radius,
                    MathF.Sin(angle) * radius, sectorIndex));
                previous = id;
            }
            string firstNotable = ClusterId(sectorIndex, cluster, "notable.00");
            string secondNotable = ClusterId(sectorIndex, cluster, "notable.01");
            string mastery = ClusterId(sectorIndex, cluster, "mastery");
            float capAngle = baseAngle + offsets[cluster];
            nodes.Add(new(firstNotable, $"{theme.Name}·精研", sector.Branch, PassiveNodeKind.Notable, previous,
                [new(theme.Primary, NotableValue(theme.Primary)), new(theme.Secondary, SmallValue(theme.Secondary, 1))],
                [previous, secondNotable], MathF.Cos(capAngle) * (centerRadius + 175), MathF.Sin(capAngle) * (centerRadius + 175), sectorIndex));
            nodes.Add(new(secondNotable, $"{theme.Name}·极意", sector.Branch, PassiveNodeKind.Notable, firstNotable,
                [new(theme.Primary, NotableValue(theme.Primary)), new(theme.Secondary, NotableValue(theme.Secondary))],
                [firstNotable, mastery], MathF.Cos(capAngle) * (centerRadius + 201), MathF.Sin(capAngle) * (centerRadius + 201), sectorIndex));
            nodes.Add(new(mastery, $"{theme.Name}专精", sector.Branch, PassiveNodeKind.Mastery, secondNotable,
                [new(theme.Primary, SmallValue(theme.Primary, 2)), new(theme.Secondary, SmallValue(theme.Secondary, 2))],
                [secondNotable], MathF.Cos(capAngle) * (centerRadius + 230), MathF.Sin(capAngle) * (centerRadius + 230),
                sectorIndex, MasteryGroup: theme.Mastery));
        }
    }

    private static void AddRules(ICollection<PassiveNodeDefinition> nodes, int sectorIndex)
    {
        string[] names = ["逆律", "孤途", "终誓"];
        for (int index = 0; index < 3; index++)
        {
            string anchor = TravelId(sectorIndex, 4 + index * 5);
            Theme theme = Sectors[sectorIndex].Themes[index * 2];
            var effects = new List<PassiveEffect> { new(theme.Primary, NotableValue(theme.Primary)) };
            string rule = index switch
            {
                0 => "主题能力大幅提高，但最大生命降低 8%",
                1 => "造成 15% 更多伤害，但移动速度降低 3%",
                _ => "技能消耗降低 12%，但药剂效果降低 20%",
            };
            if (index == 0) effects.Add(new(PassiveEffectKind.IncreasedMaximumLifeBasisPoints, -800));
            if (index == 1) { effects.Add(new(PassiveEffectKind.MoreDamageBasisPoints, 1_500)); effects.Add(new(PassiveEffectKind.IncreasedMovementSpeedBasisPoints, -300)); }
            if (index == 2) { effects.Add(new(PassiveEffectKind.ReducedSkillCostBasisPoints, 1_200)); effects.Add(new(PassiveEffectKind.IncreasedLifeFlaskEffectBasisPoints, -2_000)); }
            if (sectorIndex == 0 && index == 0) { effects.Add(new(PassiveEffectKind.RuleResoluteTechnique)); rule = "攻击必定命中但无法暴击；最大生命降低 8%"; }
            if (sectorIndex == 3 && index == 0) { effects.Add(new(PassiveEffectKind.RuleIronReflexes)); rule = "全部闪避转化为护甲；最大生命降低 8%"; }
            if (sectorIndex == 8 && index == 0) { effects.Add(new(PassiveEffectKind.RuleFlaskless)); effects.Add(new(PassiveEffectKind.FlatLifeRegeneration, 80)); rule = "不能使用药剂；每秒生命恢复大幅提高"; }
            float angle = SectorAngle(sectorIndex) + (index - 1) * .11f;
            float radius = 340 + index * 205;
            nodes.Add(new($"core.passive.v3.rule.{sectorIndex:00}.{index:00}", $"{Sectors[sectorIndex].Name}·{names[index]}",
                Sectors[sectorIndex].Branch, PassiveNodeKind.Rule, anchor, effects, [anchor],
                MathF.Cos(angle) * radius, MathF.Sin(angle) * radius, sectorIndex, SpecialRule: rule));
        }
    }

    private static void AddJewelSockets(ICollection<PassiveNodeDefinition> nodes, int sectorIndex)
    {
        for (int index = 0; index < 2; index++)
        {
            string anchor = TravelId(sectorIndex, 7 + index * 6);
            float angle = SectorAngle(sectorIndex) + (index == 0 ? -.045f : .045f);
            float radius = 470 + index * 260;
            nodes.Add(new($"core.passive.v3.jewel.{sectorIndex:00}.{index:00}", "记忆棱孔", Sectors[sectorIndex].Branch,
                PassiveNodeKind.JewelSocket, anchor, [], [anchor], MathF.Cos(angle) * radius,
                MathF.Sin(angle) * radius, sectorIndex));
        }
    }

    private static int SmallValue(PassiveEffectKind kind, int index) => kind switch
    {
        PassiveEffectKind.FlatAccuracy => 45 + index * 3,
        PassiveEffectKind.FlatMaximumLife or PassiveEffectKind.FlatMaximumMana => 18 + index * 2,
        PassiveEffectKind.FlatLifeRegeneration or PassiveEffectKind.LifeOnHit or PassiveEffectKind.ManaOnHit => 8 + index,
        PassiveEffectKind.BlockChanceBasisPoints => 250,
        PassiveEffectKind.SpellSuppressionBasisPoints => 400,
        PassiveEffectKind.IncreasedAttackSpeedBasisPoints or PassiveEffectKind.IncreasedMovementSpeedBasisPoints or
            PassiveEffectKind.IncreasedCooldownRecoveryBasisPoints or PassiveEffectKind.ReducedSkillCostBasisPoints => 400 + index % 2 * 100,
        PassiveEffectKind.IncreasedMaximumLifeBasisPoints => 450,
        _ => 1_600 + index % 3 * 200,
    };

    private static int NotableValue(PassiveEffectKind kind) => kind switch
    {
        PassiveEffectKind.FlatAccuracy => 120,
        PassiveEffectKind.FlatMaximumLife or PassiveEffectKind.FlatMaximumMana => 55,
        PassiveEffectKind.FlatLifeRegeneration or PassiveEffectKind.LifeOnHit or PassiveEffectKind.ManaOnHit => 30,
        PassiveEffectKind.BlockChanceBasisPoints => 700,
        PassiveEffectKind.SpellSuppressionBasisPoints => 1_000,
        PassiveEffectKind.IncreasedAttackSpeedBasisPoints or PassiveEffectKind.IncreasedMovementSpeedBasisPoints or
            PassiveEffectKind.IncreasedCooldownRecoveryBasisPoints or PassiveEffectKind.ReducedSkillCostBasisPoints => 1_000,
        PassiveEffectKind.IncreasedMaximumLifeBasisPoints => 900,
        _ => 3_500,
    };

    private static float SectorAngle(int sector) => -MathF.PI / 2 + sector * MathF.Tau / 12;
    private static string CenterId(int index) => $"core.passive.v3.center.{index:00}";
    private static string TravelId(int sector, int index) => $"core.passive.v3.travel.{sector:00}.{index:00}";
    private static string ClusterId(int sector, int cluster, string suffix) => $"core.passive.v3.cluster.{sector:00}.{cluster:00}.{suffix}";
    private static string StartGardenId(PassiveStartKind start, string suffix) => $"core.passive.v3.start_garden.{start.ToString().ToLowerInvariant()}.{suffix}";
    private static Start MakeStart(PassiveStartKind kind, string name, int firstSector, int secondSector, float angle,
        (string Name, PassiveEffectKind Primary, PassiveEffectKind Secondary)[] themes,
        params (string Name, PassiveEffectKind Primary, PassiveEffectKind? Secondary)[] attributes) =>
        new(kind, name, firstSector, secondSector, angle,
            themes.Select(theme => new StartTheme(theme.Name, theme.Primary, theme.Secondary)).ToArray(),
            attributes.Select(attribute => new StartAttribute(attribute.Name, attribute.Secondary is { } secondary
                ? [new(attribute.Primary, 15), new(secondary, 15)]
                : [new(attribute.Primary, 30)])).ToArray());
    private static (string Name, PassiveEffectKind Primary, PassiveEffectKind? Secondary) A(string name,
        PassiveEffectKind primary) => (name, primary, null);
    private static Theme T(string name, string mastery, PassiveEffectKind primary, PassiveEffectKind secondary) => new(name, mastery, primary, secondary);
    private static Sector S(PassiveBranch branch, string name, PassiveEffectKind attribute, params Theme[] themes) => new(branch, name, attribute, themes);
}
