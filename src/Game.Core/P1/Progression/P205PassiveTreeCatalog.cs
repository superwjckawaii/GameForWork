namespace GameForWork.Core.P1.Progression;

internal static class P205PassiveTreeCatalog
{
    public const int ExpectedNodeCount = 1_200;
    public const float JewelRadius = 150f;

    private sealed record Sector(
        PassiveBranch Branch,
        string Name,
        PassiveEffectKind Attribute,
        PassiveStartKind Start = PassiveStartKind.None);

    private static readonly Sector[] Sectors =
    [
        new(PassiveBranch.HeavyWeapon, "巨兵", PassiveEffectKind.FlatPhysique, PassiveStartKind.Physique),
        new(PassiveBranch.Bleed, "血痕", PassiveEffectKind.FlatPhysique),
        new(PassiveBranch.Defense, "磐石", PassiveEffectKind.FlatPhysique),
        new(PassiveBranch.Mobility, "逐风", PassiveEffectKind.FlatDexterity, PassiveStartKind.Dexterity),
        new(PassiveBranch.Critical, "处决", PassiveEffectKind.FlatDexterity),
        new(PassiveBranch.Accuracy, "鹰眼", PassiveEffectKind.FlatDexterity),
        new(PassiveBranch.Mana, "源流", PassiveEffectKind.FlatSpirit, PassiveStartKind.Spirit),
        new(PassiveBranch.WarCry, "战令", PassiveEffectKind.FlatSpirit),
        new(PassiveBranch.Flask, "药理", PassiveEffectKind.FlatSpirit),
        new(PassiveBranch.Elemental, "元素", PassiveEffectKind.FlatEnergy, PassiveStartKind.Energy),
        new(PassiveBranch.Void, "虚蚀", PassiveEffectKind.FlatEnergy),
        new(PassiveBranch.Shield, "秘盾", PassiveEffectKind.FlatEnergy),
    ];

    private static readonly string[] ClusterNames =
    [
        "初式", "疾式", "强式", "锐式", "坚式", "回环", "蓄势", "破隙", "绵延",
        "共鸣", "猎径", "守势", "终击", "复起", "远征", "精研", "极意", "归一",
    ];

    public static IReadOnlyList<PassiveNodeDefinition> Build()
    {
        var nodes = new List<PassiveNodeDefinition>(ExpectedNodeCount);
        AddCentralCross(nodes);
        for (int sector = 0; sector < Sectors.Length; sector++) AddTravelSpine(nodes, sector);
        for (int sector = 0; sector < Sectors.Length; sector++)
        {
            AddClusters(nodes, sector);
            AddRules(nodes, sector);
            AddJewelSockets(nodes, sector);
        }
        if (nodes.Count != ExpectedNodeCount)
            throw new InvalidDataException($"P20.5 passive catalog produced {nodes.Count} nodes instead of {ExpectedNodeCount}.");
        return nodes;
    }

    public static string StartNode(PassiveStartKind start) => start switch
    {
        PassiveStartKind.Physique => "core.passive.start.physique",
        PassiveStartKind.Dexterity => "core.passive.start.dexterity",
        PassiveStartKind.Spirit => "core.passive.start.spirit",
        PassiveStartKind.Energy => "core.passive.start.energy",
        _ => throw new ArgumentOutOfRangeException(nameof(start)),
    };

    private static void AddCentralCross(ICollection<PassiveNodeDefinition> nodes)
    {
        PassiveEffectKind[] attributes =
        [
            PassiveEffectKind.FlatPhysique, PassiveEffectKind.FlatDexterity,
            PassiveEffectKind.FlatSpirit, PassiveEffectKind.FlatEnergy,
        ];
        string[] names = ["体魄交汇", "灵巧交汇", "精神交汇", "能量交汇"];
        for (int index = 0; index < 4; index++)
        {
            string id = CentralId(index);
            string previous = CentralId((index + 3) % 4);
            string next = CentralId((index + 1) % 4);
            string[] sectorLinks = Enumerable.Range(index * 3, 3).Select(sector => TravelId(sector, 0)).ToArray();
            nodes.Add(new PassiveNodeDefinition(id, names[index], Sectors[index * 3].Branch,
                PassiveNodeKind.Small, previous, [new PassiveEffect(attributes[index], 10)],
                [previous, next, .. sectorLinks],
                MathF.Cos(-MathF.PI / 2 + index * MathF.PI / 2) * 92,
                MathF.Sin(-MathF.PI / 2 + index * MathF.PI / 2) * 72,
                -1));
        }
    }

    private static void AddTravelSpine(ICollection<PassiveNodeDefinition> nodes, int sectorIndex)
    {
        Sector sector = Sectors[sectorIndex];
        float angle = SectorAngle(sectorIndex);
        int central = sectorIndex / 3;
        for (int index = 0; index < 12; index++)
        {
            bool start = index == 11 && sector.Start != PassiveStartKind.None;
            string id = start ? StartNode(sector.Start) : TravelId(sectorIndex, index);
            string previous = index == 0 ? CentralId(central) : TravelOrStartId(sectorIndex, index - 1);
            string? next = index == 11 ? null : TravelOrStartId(sectorIndex, index + 1);
            float radius = 165 + index * 39;
            var links = new List<string> { previous };
            if (next is not null) links.Add(next);
            int value = start ? 20 : 10;
            nodes.Add(new PassiveNodeDefinition(id,
                start ? $"{StartName(sector.Start)}起点" : $"{sector.Name}之路 {index + 1:00}",
                sector.Branch, PassiveNodeKind.Small, start ? null : previous,
                [new PassiveEffect(sector.Attribute, value)], links,
                MathF.Cos(angle) * radius, MathF.Sin(angle) * radius * .78f,
                sectorIndex, start ? sector.Start : PassiveStartKind.None));
        }
    }

    private static void AddClusters(ICollection<PassiveNodeDefinition> nodes, int sectorIndex)
    {
        Sector sector = Sectors[sectorIndex];
        float baseAngle = SectorAngle(sectorIndex);
        for (int cluster = 0; cluster < 18; cluster++)
        {
            int smallCount = cluster % 2 == 0 ? 3 : 4;
            int anchorIndex = 1 + cluster * 5 % 10;
            string previous = TravelOrStartId(sectorIndex, anchorIndex);
            int band = cluster / 6;
            int local = cluster % 6;
            float centerRadius = 270 + band * 128 + local * 26;
            float side = (local - 2.5f) * .052f + (band - 1) * .018f;
            PassiveEffectKind primary = ClusterEffect(sector.Branch, cluster % 3);
            for (int index = 0; index < smallCount; index++)
            {
                string id = ClusterSmallId(sectorIndex, cluster, index);
                float radius = centerRadius + index * 24;
                float angle = baseAngle + side;
                nodes.Add(new PassiveNodeDefinition(id,
                    $"{sector.Name}·{ClusterNames[cluster]}·{index + 1}", sector.Branch,
                    PassiveNodeKind.Small, previous, [new PassiveEffect(primary, SmallValue(primary, index))],
                    [previous, index + 1 < smallCount ? ClusterSmallId(sectorIndex, cluster, index + 1) : ClusterCapId(sectorIndex, cluster)],
                    MathF.Cos(angle) * radius, MathF.Sin(angle) * radius * .78f,
                    sectorIndex));
                previous = id;
            }

            bool mastery = cluster % 3 == 2;
            PassiveNodeKind kind = mastery ? PassiveNodeKind.Mastery : PassiveNodeKind.Notable;
            PassiveEffectKind secondary = ClusterEffect(sector.Branch, (cluster + 1) % 3);
            float capRadius = centerRadius + smallCount * 24 + 7;
            float capAngle = baseAngle + side;
            nodes.Add(new PassiveNodeDefinition(ClusterCapId(sectorIndex, cluster),
                $"{sector.Name}·{ClusterNames[cluster]}{(mastery ? "专精" : "显著")}", sector.Branch, kind,
                previous,
                mastery
                    ? [new PassiveEffect(primary, SmallValue(primary, 1))]
                    : [new PassiveEffect(primary, NotableValue(primary)), new PassiveEffect(secondary, SmallValue(secondary, 1))],
                [previous], MathF.Cos(capAngle) * capRadius, MathF.Sin(capAngle) * capRadius * .78f,
                sectorIndex));
        }
    }

    private static void AddRules(ICollection<PassiveNodeDefinition> nodes, int sectorIndex)
    {
        Sector sector = Sectors[sectorIndex];
        float angle = SectorAngle(sectorIndex);
        string[] suffixes = ["逆誓", "孤途", "界限", "终律"];
        for (int index = 0; index < 4; index++)
        {
            string anchor = TravelOrStartId(sectorIndex, 3 + index * 2);
            float radius = 340 + index * 73;
            float offset = index % 2 == 0 ? -.105f : .105f;
            (IReadOnlyList<PassiveEffect> effects, string rule) = RuleEffects(sectorIndex, index);
            nodes.Add(new PassiveNodeDefinition($"core.passive.v2.rule.{sectorIndex:00}.{index:00}",
                $"{sector.Name}·{suffixes[index]}", sector.Branch, PassiveNodeKind.Rule, anchor,
                effects, [anchor], MathF.Cos(angle + offset) * radius,
                MathF.Sin(angle + offset) * radius * .78f, sectorIndex, SpecialRule: rule));
        }
    }

    private static void AddJewelSockets(ICollection<PassiveNodeDefinition> nodes, int sectorIndex)
    {
        int count = sectorIndex < 8 ? 3 : 2;
        float angle = SectorAngle(sectorIndex);
        for (int index = 0; index < count; index++)
        {
            int anchorIndex = 2 + index * 4;
            string anchor = TravelOrStartId(sectorIndex, anchorIndex);
            float radius = 285 + index * 145;
            float offset = index % 2 == 0 ? .14f : -.14f;
            nodes.Add(new PassiveNodeDefinition($"core.passive.v2.jewel.{sectorIndex:00}.{index:00}",
                "记忆棱孔", Sectors[sectorIndex].Branch, PassiveNodeKind.JewelSocket, anchor, [], [anchor],
                MathF.Cos(angle + offset) * radius, MathF.Sin(angle + offset) * radius * .78f,
                sectorIndex));
        }
    }

    private static (IReadOnlyList<PassiveEffect> Effects, string Rule) RuleEffects(int sector, int index)
    {
        PassiveEffectKind primary = ClusterEffect(Sectors[sector].Branch, index % 3);
        var effects = new List<PassiveEffect>
        {
            new(primary, NotableValue(primary) + SmallValue(primary, 0)),
        };
        string rule = index switch
        {
            0 => "主要能力大幅提高，但最大生命降低 8%",
            1 => "造成 15% 更多伤害，但移动速度降低 3%",
            2 => "技能消耗降低 12%，但药剂效果降低 20%",
            _ => "主要能力大幅提高，但法力恢复速度降低 20%",
        };
        switch (index)
        {
            case 0: effects.Add(new(PassiveEffectKind.IncreasedMaximumLifeBasisPoints, -800)); break;
            case 1:
                effects.Add(new(PassiveEffectKind.MoreDamageBasisPoints, 1_500));
                effects.Add(new(PassiveEffectKind.IncreasedMovementSpeedBasisPoints, -300));
                break;
            case 2:
                effects.Add(new(PassiveEffectKind.ReducedSkillCostBasisPoints, 1_200));
                effects.Add(new(PassiveEffectKind.IncreasedLifeFlaskEffectBasisPoints, -2_000));
                break;
            default: effects.Add(new(PassiveEffectKind.IncreasedManaRegenerationBasisPoints, -2_000)); break;
        }
        if (sector == 0 && index == 0)
        {
            effects.Add(new(PassiveEffectKind.RuleResoluteTechnique));
            rule = "攻击必定命中但无法暴击；最大生命降低 8%";
        }
        else if (sector == 3 && index == 0)
        {
            effects.Add(new(PassiveEffectKind.RuleIronReflexes));
            rule = "全部闪避转化为护甲；最大生命降低 8%";
        }
        else if (sector == 8 && index == 0)
        {
            effects.Add(new(PassiveEffectKind.RuleFlaskless));
            effects.Add(new(PassiveEffectKind.FlatLifeRegeneration, 80));
            rule = "不能使用药剂；每秒生命恢复大幅提高";
        }
        return (effects, rule);
    }

    private static PassiveEffectKind ClusterEffect(PassiveBranch branch, int variant) => branch switch
    {
        PassiveBranch.HeavyWeapon => variant switch { 0 => PassiveEffectKind.IncreasedTwoHandDamageBasisPoints, 1 => PassiveEffectKind.IncreasedMeleeDamageBasisPoints, _ => PassiveEffectKind.IncreasedAttackSpeedBasisPoints },
        PassiveBranch.Bleed => variant switch { 0 => PassiveEffectKind.IncreasedBleedDamageBasisPoints, 1 => PassiveEffectKind.IncreasedPhysicalDamageOverTimeBasisPoints, _ => PassiveEffectKind.IncreasedBleedChanceBasisPoints },
        PassiveBranch.Defense => variant switch { 0 => PassiveEffectKind.IncreasedMaximumLifeBasisPoints, 1 => PassiveEffectKind.IncreasedArmorBasisPoints, _ => PassiveEffectKind.FlatLifeRegeneration },
        PassiveBranch.Mobility => variant switch { 0 => PassiveEffectKind.IncreasedEvasionBasisPoints, 1 => PassiveEffectKind.IncreasedMovementSpeedBasisPoints, _ => PassiveEffectKind.IncreasedAttackSpeedBasisPoints },
        PassiveBranch.Critical => variant switch { 0 => PassiveEffectKind.IncreasedCriticalChanceBasisPoints, 1 => PassiveEffectKind.IncreasedCriticalMultiplierBasisPoints, _ => PassiveEffectKind.IncreasedAttackSkillDamageBasisPoints },
        PassiveBranch.Accuracy => variant switch { 0 => PassiveEffectKind.FlatAccuracy, 1 => PassiveEffectKind.IncreasedProjectileDamageBasisPoints, _ => PassiveEffectKind.IncreasedAttackSpeedBasisPoints },
        PassiveBranch.Mana => variant switch { 0 => PassiveEffectKind.FlatMaximumMana, 1 => PassiveEffectKind.IncreasedManaRegenerationBasisPoints, _ => PassiveEffectKind.ReducedSkillCostBasisPoints },
        PassiveBranch.WarCry => variant switch { 0 => PassiveEffectKind.IncreasedWarCryCooldownRecoveryBasisPoints, 1 => PassiveEffectKind.IncreasedWarCryRangeBasisPoints, _ => PassiveEffectKind.IncreasedAreaDamageBasisPoints },
        PassiveBranch.Flask => variant switch { 0 => PassiveEffectKind.IncreasedLifeFlaskEffectBasisPoints, 1 => PassiveEffectKind.FlatLifeRegeneration, _ => PassiveEffectKind.IncreasedMaximumLifeBasisPoints },
        PassiveBranch.Elemental => variant switch { 0 => PassiveEffectKind.IncreasedElementalDamageBasisPoints, 1 => PassiveEffectKind.IncreasedSpellDamageBasisPoints, _ => PassiveEffectKind.IncreasedAreaDamageBasisPoints },
        PassiveBranch.Void => variant switch { 0 => PassiveEffectKind.IncreasedVoidDamageBasisPoints, 1 => PassiveEffectKind.IncreasedDamageOverTimeBasisPoints, _ => PassiveEffectKind.IncreasedSpellDamageBasisPoints },
        _ => variant switch { 0 => PassiveEffectKind.IncreasedShieldBasisPoints, 1 => PassiveEffectKind.SpellSuppressionBasisPoints, _ => PassiveEffectKind.BlockChanceBasisPoints },
    };

    private static int SmallValue(PassiveEffectKind kind, int index) => kind switch
    {
        PassiveEffectKind.IncreasedAttackSpeedBasisPoints => 500 + index % 2 * 100,
        PassiveEffectKind.IncreasedMovementSpeedBasisPoints => 300,
        PassiveEffectKind.IncreasedMaximumLifeBasisPoints => 500 + index % 2 * 100,
        PassiveEffectKind.FlatLifeRegeneration => 18 + index * 2,
        PassiveEffectKind.FlatAccuracy => 45 + index * 5,
        PassiveEffectKind.FlatMaximumMana => 24 + index * 4,
        PassiveEffectKind.IncreasedBleedChanceBasisPoints => 800 + index * 100,
        PassiveEffectKind.IncreasedCriticalChanceBasisPoints => 900 + index * 100,
        PassiveEffectKind.IncreasedCriticalMultiplierBasisPoints => 1_200 + index * 100,
        PassiveEffectKind.BlockChanceBasisPoints => 300,
        PassiveEffectKind.SpellSuppressionBasisPoints => 500,
        PassiveEffectKind.ReducedSkillCostBasisPoints => 300,
        PassiveEffectKind.IncreasedArmorBasisPoints or PassiveEffectKind.IncreasedEvasionBasisPoints or PassiveEffectKind.IncreasedShieldBasisPoints => 2_000,
        _ => 1_600 + index % 3 * 200,
    };

    private static int NotableValue(PassiveEffectKind kind) => kind switch
    {
        PassiveEffectKind.IncreasedAttackSpeedBasisPoints => 1_200,
        PassiveEffectKind.IncreasedMovementSpeedBasisPoints => 600,
        PassiveEffectKind.IncreasedMaximumLifeBasisPoints => 1_000,
        PassiveEffectKind.FlatLifeRegeneration => 50,
        PassiveEffectKind.FlatAccuracy => 120,
        PassiveEffectKind.FlatMaximumMana => 60,
        PassiveEffectKind.IncreasedBleedChanceBasisPoints => 2_000,
        PassiveEffectKind.IncreasedCriticalChanceBasisPoints => 2_400,
        PassiveEffectKind.IncreasedCriticalMultiplierBasisPoints => 3_000,
        PassiveEffectKind.BlockChanceBasisPoints => 800,
        PassiveEffectKind.SpellSuppressionBasisPoints => 1_200,
        PassiveEffectKind.ReducedSkillCostBasisPoints => 700,
        PassiveEffectKind.IncreasedArmorBasisPoints or PassiveEffectKind.IncreasedEvasionBasisPoints or PassiveEffectKind.IncreasedShieldBasisPoints => 4_500,
        _ => 4_000,
    };

    private static float SectorAngle(int sector) => -MathF.PI / 2 + sector * MathF.Tau / 12;
    private static string CentralId(int index) => $"core.passive.v2.center.{index:00}";
    private static string TravelId(int sector, int index) => $"core.passive.v2.travel.{sector:00}.{index:00}";
    private static string TravelOrStartId(int sector, int index) => index == 11 && Sectors[sector].Start != PassiveStartKind.None
        ? StartNode(Sectors[sector].Start) : TravelId(sector, index);
    private static string ClusterSmallId(int sector, int cluster, int index) => $"core.passive.v2.cluster.{sector:00}.{cluster:00}.{index:00}";
    private static string ClusterCapId(int sector, int cluster) => $"core.passive.v2.cluster.{sector:00}.{cluster:00}.cap";
    private static string StartName(PassiveStartKind start) => start switch
    {
        PassiveStartKind.Physique => "体魄",
        PassiveStartKind.Dexterity => "灵巧",
        PassiveStartKind.Spirit => "精神",
        PassiveStartKind.Energy => "能量",
        _ => string.Empty,
    };
}
