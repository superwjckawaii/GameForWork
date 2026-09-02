using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using GameForWork.Core.P1.Progression;

namespace GameForWork.Core.P30;

public sealed record P30PassiveTreeData(
    string Version,
    int CanvasWidth,
    int CanvasHeight,
    int InnerRadius,
    int MiddleRadius,
    int OuterRadius,
    IReadOnlyList<P30ClusterData> Clusters);

public sealed record P30ClusterData(
    string Slot,
    string StableSlug,
    string Name,
    string Theme,
    string Size,
    string SourceFile,
    IReadOnlyList<string> Descriptions,
    string MasteryKey,
    IReadOnlyList<string> MasteryOptions);

public sealed record P30MasteryChoice(PassiveEffect Effect, string Description);

public static partial class P30PassiveTreeCatalog
{
    public const string Version = "p30.v1";
    public const int ExpectedNodeCount = 1_475;
    public const int ExpectedMediumClusters = 131;
    public const int ExpectedLargeClusters = 37;
    public const float JewelRadius = 210f;
    public const float LayoutExtent = 3_320f;

    private static readonly P30PassiveTreeData Data = Load();
    private static readonly PassiveStartKind[] Starts =
    [
        PassiveStartKind.Physique,
        PassiveStartKind.PhysiqueEnergy,
        PassiveStartKind.Energy,
        PassiveStartKind.Spirit,
        PassiveStartKind.DexteritySpirit,
        PassiveStartKind.Dexterity,
    ];
    private static readonly PassiveBranch[] Branches =
    [
        PassiveBranch.HeavyWeapon,
        PassiveBranch.Shield,
        PassiveBranch.Void,
        PassiveBranch.Mana,
        PassiveBranch.Mobility,
        PassiveBranch.Critical,
    ];
    private static readonly PassiveEffectKind[] SectorAttributes =
    [
        PassiveEffectKind.FlatPhysique,
        PassiveEffectKind.FlatEnergy,
        PassiveEffectKind.FlatEnergy,
        PassiveEffectKind.FlatSpirit,
        PassiveEffectKind.FlatSpirit,
        PassiveEffectKind.FlatDexterity,
    ];

    public static IReadOnlyList<PassiveNodeDefinition> Build()
    {
        ValidateData();
        var nodes = new List<PassiveNodeDefinition>(ExpectedNodeCount);
        var positions = new Dictionary<string, (float X, float Y)>(StringComparer.Ordinal);
        AddStarts(nodes, positions);
        AddRing(nodes, positions, "inner", Data.InnerRadius, 7, startsAsVertices: true);
        AddRing(nodes, positions, "middle", Data.MiddleRadius, 9, startsAsVertices: false);
        AddRing(nodes, positions, "outer", Data.OuterRadius, 11, startsAsVertices: false);
        AddRadials(nodes, positions);
        AddAttributeMajors(nodes, positions);
        AddJewels(nodes, positions);
        AddClusters(nodes, positions);
        if (nodes.Count != ExpectedNodeCount)
            throw new InvalidDataException($"P30 passive catalog produced {nodes.Count} nodes instead of {ExpectedNodeCount}.");
        if (nodes.Select(node => node.StableId).Distinct(StringComparer.Ordinal).Count() != nodes.Count)
            throw new InvalidDataException("P30 passive catalog contains duplicate stable IDs.");
        return nodes;
    }

    public static string StartNode(PassiveStartKind start)
    {
        int index = Array.IndexOf(Starts, start);
        return index >= 0 ? $"p30.start.v{index}" : throw new ArgumentOutOfRangeException(nameof(start));
    }

    public static IReadOnlyList<PassiveEffect> MasteryOptions(PassiveNodeDefinition node) =>
        MasteryChoices(node).Select(choice => choice.Effect).ToArray();

    public static IReadOnlyList<string> MasteryOptionDescriptions(PassiveNodeDefinition node) =>
        MasteryChoices(node).Select(choice => choice.Description).ToArray();

    public static IReadOnlyList<P30MasteryChoice> MasteryChoices(PassiveNodeDefinition node)
    {
        if (node.Kind != PassiveNodeKind.Mastery) return [];
        P30ClusterData cluster = Data.Clusters.First(item =>
            $"p30.mastery.{item.MasteryKey}" == node.MasteryGroup);
        return cluster.MasteryOptions.Select((description, index) =>
        {
            PassiveEffectKind effect = ThemeEffect(description, index);
            return new P30MasteryChoice(new PassiveEffect(effect,
                ParseValue(description, effect, PassiveNodeKind.Mastery)), description);
        }).ToArray();
    }

    private static void AddStarts(ICollection<PassiveNodeDefinition> nodes,
        IDictionary<string, (float X, float Y)> positions)
    {
        for (int vertex = 0; vertex < 6; vertex++)
        {
            string id = $"p30.start.v{vertex}";
            (float x, float y) = Polar(Data.InnerRadius, VertexAngle(vertex));
            string[] links =
            [
                RingId("inner", vertex, 1),
                RingId("inner", (vertex + 5) % 6, 7),
                RadialId(vertex, "i2m", 1),
            ];
            nodes.Add(new(id, $"{StartName(Starts[vertex])}起点", Branches[vertex], PassiveNodeKind.Start,
                null, [], links, x, y, vertex, Starts[vertex], "免费点亮且不可退还的职业起点"));
            positions[id] = (x, y);
        }
    }

    private static void AddRing(ICollection<PassiveNodeDefinition> nodes,
        IDictionary<string, (float X, float Y)> positions, string ring, int radius, int pointsPerEdge,
        bool startsAsVertices)
    {
        for (int edge = 0; edge < 6; edge++)
        {
            string from = startsAsVertices ? $"p30.start.v{edge}" : VertexId(ring, edge);
            string to = startsAsVertices ? $"p30.start.v{(edge + 1) % 6}" : VertexId(ring, (edge + 1) % 6);
            if (!startsAsVertices && !positions.ContainsKey(from))
            {
                (float vx, float vy) = Polar(radius, VertexAngle(edge));
                string[] vertexLinks =
                [
                    RingId(ring, edge, 1), RingId(ring, (edge + 5) % 6, pointsPerEdge),
                    RadialId(edge, ring == "middle" ? "i2m" : "m2o", 5),
                ];
                if (ring == "middle") vertexLinks = [.. vertexLinks, RadialId(edge, "m2o", 1)];
                nodes.Add(new(from, $"{RingName(ring)}顶点 V{edge}", Branches[edge], PassiveNodeKind.Small,
                    vertexLinks[0], [new(SectorAttributes[edge], 10)], vertexLinks, vx, vy, edge));
                positions[from] = (vx, vy);
            }
            (float fromX, float fromY) = Polar(radius, VertexAngle(edge));
            (float toX, float toY) = Polar(radius, VertexAngle((edge + 1) % 6));
            for (int point = 1; point <= pointsPerEdge; point++)
            {
                float t = point / (float)(pointsPerEdge + 1);
                float x = Snap(fromX + (toX - fromX) * t);
                float y = Snap(fromY + (toY - fromY) * t);
                string id = RingId(ring, edge, point);
                string previous = point == 1 ? from : RingId(ring, edge, point - 1);
                string next = point == pointsPerEdge ? to : RingId(ring, edge, point + 1);
                nodes.Add(new(id, $"{RingName(ring)}属性路径 {edge + 1}-{point:00}", Branches[edge],
                    PassiveNodeKind.Small, previous, [new(SectorAttributes[edge], 10)], [previous, next],
                    x, y, edge));
                positions[id] = (x, y);
            }
        }
    }

    private static void AddRadials(ICollection<PassiveNodeDefinition> nodes,
        IDictionary<string, (float X, float Y)> positions)
    {
        for (int vertex = 0; vertex < 6; vertex++)
        {
            AddRadial(nodes, positions, vertex, "i2m", $"p30.start.v{vertex}", VertexId("middle", vertex),
                Data.InnerRadius, Data.MiddleRadius);
            AddRadial(nodes, positions, vertex, "m2o", VertexId("middle", vertex), VertexId("outer", vertex),
                Data.MiddleRadius, Data.OuterRadius);
        }
    }

    private static void AddRadial(ICollection<PassiveNodeDefinition> nodes,
        IDictionary<string, (float X, float Y)> positions, int vertex, string segment, string from, string to,
        int fromRadius, int toRadius)
    {
        float angle = VertexAngle(vertex);
        for (int point = 1; point <= 5; point++)
        {
            float radius = fromRadius + (toRadius - fromRadius) * point / 6f;
            (float x, float y) = Polar(radius, angle);
            string id = RadialId(vertex, segment, point);
            string previous = point == 1 ? from : RadialId(vertex, segment, point - 1);
            string next = point == 5 ? to : RadialId(vertex, segment, point + 1);
            nodes.Add(new(id, $"V{vertex} 径向属性 {segment}-{point:00}", Branches[vertex], PassiveNodeKind.Small,
                previous, [new(SectorAttributes[vertex], 10)], [previous, next], x, y, vertex));
            positions[id] = (x, y);
        }
    }

    private static void AddAttributeMajors(ICollection<PassiveNodeDefinition> nodes,
        IDictionary<string, (float X, float Y)> positions)
    {
        PassiveEffectKind[][] effects =
        [
            [PassiveEffectKind.FlatPhysique, PassiveEffectKind.FlatDexterity, PassiveEffectKind.FlatEnergy],
            [PassiveEffectKind.FlatPhysique, PassiveEffectKind.FlatEnergy, PassiveEffectKind.FlatSpirit],
            [PassiveEffectKind.FlatEnergy, PassiveEffectKind.FlatSpirit, PassiveEffectKind.FlatPhysique],
            [PassiveEffectKind.FlatSpirit, PassiveEffectKind.FlatEnergy, PassiveEffectKind.FlatDexterity],
            [PassiveEffectKind.FlatSpirit, PassiveEffectKind.FlatDexterity, PassiveEffectKind.FlatPhysique],
            [PassiveEffectKind.FlatDexterity, PassiveEffectKind.FlatPhysique, PassiveEffectKind.FlatSpirit],
        ];
        for (int vertex = 0; vertex < 6; vertex++)
        for (int index = 0; index < 3; index++)
        {
            string anchor = VertexId("middle", vertex);
            (float anchorX, float anchorY) = positions[anchor];
            float angle = VertexAngle(vertex) + MathF.PI + (index switch
            {
                0 => -.72f,
                1 => .72f,
                _ => .28f,
            });
            float distance = index < 2 ? 112 : 70;
            (float x, float y) = Offset(anchorX, anchorY, angle, distance, 0);
            PassiveEffectKind effect = effects[vertex][index];
            string id = $"p30.attr.major.v{vertex}.{AttributeSlug(effect)}";
            nodes.Add(new(id, $"{AttributeName(effect)} +30", Branches[vertex], PassiveNodeKind.Notable,
                anchor, [new(effect, 30)], [anchor], x, y, vertex, SpecialRule: $"{AttributeName(effect)} +30"));
            positions[id] = (x, y);
        }
    }

    private static void AddJewels(ICollection<PassiveNodeDefinition> nodes,
        IDictionary<string, (float X, float Y)> positions)
    {
        string[] suffixes = ["ji", "jm0", "jm1", "jo"];
        for (int vertex = 0; vertex < 6; vertex++)
        for (int index = 0; index < suffixes.Length; index++)
        {
            string anchor = index switch
            {
                0 => RadialId(vertex, "i2m", 2),
                1 => RingId("middle", vertex, 3),
                2 => RingId("middle", (vertex + 5) % 6, 7),
                _ => RadialId(vertex, "m2o", 4),
            };
            (float anchorX, float anchorY) = positions[anchor];
            float angle = MathF.Atan2(anchorY, anchorX);
            (float forward, float sideways) = index switch
            {
                0 => (0, 82),
                1 => (76, 0),
                2 => (76, 0),
                _ => (0, -82),
            };
            (float x, float y) = Offset(anchorX, anchorY, angle, forward, sideways);
            string id = $"p30.jewel.v{vertex}.{suffixes[index]}";
            nodes.Add(new(id, "记忆棱孔", Branches[vertex], PassiveNodeKind.JewelSocket, anchor, [], [anchor],
                x, y, vertex, SpecialRule: "可镶嵌一枚棱晶或传奇珠宝；半径、塑形与腐化读取当前珠宝规则"));
            positions[id] = (x, y);
        }
    }

    private static void AddClusters(ICollection<PassiveNodeDefinition> nodes,
        IReadOnlyDictionary<string, (float X, float Y)> positions)
    {
        foreach (P30ClusterData cluster in Data.Clusters.OrderBy(item => item.Slot, StringComparer.Ordinal))
        {
            bool large = cluster.Size == "large";
            int sector = int.Parse(cluster.Slot.AsSpan(1, 1));
            if (large)
            {
                string anchor = LargeAnchor(cluster.Slot);
                (float anchorX, float anchorY) = positions[anchor];
                AddLargeCluster(nodes, cluster, sector, anchor, anchorX, anchorY,
                    MathF.Atan2(anchorY, anchorX), SlotOrdinal(cluster.Slot) % 2 == 0 ? 1f : -1f,
                    SlotOrdinal(cluster.Slot) % 3 * 14f);
            }
            else
            {
                int ordinal = SlotOrdinal(cluster.Slot);
                string anchor = MediumAnchor(cluster.Slot);
                (float originX, float originY) = positions[anchor];
                float radialAngle = MathF.Atan2(originY, originX);
                float angle = anchor.Contains(".radial.", StringComparison.Ordinal)
                    ? radialAngle + (ordinal == 8 ? -MathF.PI / 2 : ordinal % 2 == 0 ? MathF.PI / 2 : -MathF.PI / 2)
                    : radialAngle + MathF.PI + (ordinal == 1 ? -.38f : ordinal == 4 ? .38f : 0f);
                AddMediumCluster(nodes, cluster, sector, anchor, originX, originY, angle,
                    ordinal % 2 == 0 ? 1f : -1f);
            }
        }
    }

    private static void AddMediumCluster(ICollection<PassiveNodeDefinition> nodes, P30ClusterData cluster,
        int sector, string anchor, float anchorX, float anchorY, float angle, float tangent)
    {
        string notable = ClusterId(cluster, "notable01");
        string mastery = ClusterId(cluster, "mastery");
        string[] smalls = Enumerable.Range(1, 4).Select(index => ClusterId(cluster, $"small{index:00}")).ToArray();
        (string[] smallDescriptions, string[] notableDescriptions) = ClusterDescriptions(cluster, 4, 1);
        AddClusterNode(nodes, cluster, sector, smalls[0], PassiveNodeKind.Small, anchor,
            [anchor, smalls[1]], anchorX, anchorY, angle, 42, -26 * tangent, smallDescriptions[0], 0);
        AddClusterNode(nodes, cluster, sector, smalls[1], PassiveNodeKind.Small, smalls[0],
            [smalls[0], notable], anchorX, anchorY, angle, 84, -42 * tangent, smallDescriptions[1], 1);
        AddClusterNode(nodes, cluster, sector, smalls[2], PassiveNodeKind.Small, anchor,
            [anchor, smalls[3]], anchorX, anchorY, angle, 42, 26 * tangent, smallDescriptions[2], 2);
        AddClusterNode(nodes, cluster, sector, smalls[3], PassiveNodeKind.Small, smalls[2],
            [smalls[2], notable], anchorX, anchorY, angle, 84, 42 * tangent, smallDescriptions[3], 3);
        AddClusterNode(nodes, cluster, sector, notable, PassiveNodeKind.Notable, smalls[1],
            [smalls[1], smalls[3], mastery], anchorX, anchorY, angle, 126, 0, notableDescriptions[0], 4);
        AddClusterNode(nodes, cluster, sector, mastery, PassiveNodeKind.Mastery, notable,
            [notable], anchorX, anchorY, angle, 172, 0, $"从“{cluster.MasteryKey}”共享专精池选择 1 项", 5);
    }

    private static void AddLargeCluster(ICollection<PassiveNodeDefinition> nodes, P30ClusterData cluster,
        int sector, string anchor, float anchorX, float anchorY, float angle, float tangent, float forwardShift)
    {
        string mastery = ClusterId(cluster, "mastery");
        (string[] smallDescriptions, string[] notableDescriptions) = ClusterDescriptions(cluster, 8, 2);
        for (int branch = 0; branch < 2; branch++)
        {
            string previous = anchor;
            string notable = ClusterId(cluster, $"notable{branch + 1:00}");
            for (int index = 0; index < 4; index++)
            {
                string id = ClusterId(cluster, $"small{branch * 4 + index + 1:00}");
                string next = index == 3 ? notable : ClusterId(cluster, $"small{branch * 4 + index + 2:00}");
                float side = (branch == 0 ? -1 : 1) * tangent;
                AddClusterNode(nodes, cluster, sector, id, PassiveNodeKind.Small, previous, [previous, next],
                    anchorX, anchorY, angle, 58 + index * 44 + forwardShift, side * (34 + index * 8),
                    smallDescriptions[branch * 4 + index], branch * 5 + index);
                previous = id;
            }
            AddClusterNode(nodes, cluster, sector, notable, PassiveNodeKind.Notable, previous,
                [previous, mastery], anchorX, anchorY, angle, 244 + forwardShift, (branch == 0 ? -1 : 1) * tangent * 66,
                notableDescriptions[branch], branch * 5 + 4);
        }
        AddClusterNode(nodes, cluster, sector, mastery, PassiveNodeKind.Mastery,
            ClusterId(cluster, "notable01"), [ClusterId(cluster, "notable01"), ClusterId(cluster, "notable02")],
            anchorX, anchorY, angle, 292 + forwardShift, 0,
            $"从“{cluster.MasteryKey}”共享专精池选择 1 项", 10);
    }

    private static void AddClusterNode(ICollection<PassiveNodeDefinition> nodes, P30ClusterData cluster,
        int sector, string id, PassiveNodeKind kind, string prerequisite, IReadOnlyList<string> links,
        float anchorX, float anchorY, float angle, float forward, float sideways, string description, int ordinal)
    {
        float cos = MathF.Cos(angle);
        float sin = MathF.Sin(angle);
        float x = Snap(anchorX + cos * forward - sin * sideways);
        float y = Snap(anchorY + sin * forward + cos * sideways);
        PassiveEffectKind effect = ThemeEffect(cluster.Theme + " " + description, ordinal);
        int value = ParseValue(description, effect, kind);
        nodes.Add(new(id, NodeName(cluster, kind, description, ordinal), Branches[sector], kind, prerequisite,
            [new(effect, value)], links, x, y, sector, SpecialRule: DescriptionEffect(description),
            MasteryGroup: $"p30.mastery.{cluster.MasteryKey}", ClusterTheme: cluster.Theme));
    }

    private static (string[] Smalls, string[] Notables) ClusterDescriptions(P30ClusterData cluster,
        int smallCount, int notableCount)
    {
        string[] source = cluster.Descriptions.Where(text => !string.IsNullOrWhiteSpace(text))
            .Select(NormalizeDescription).ToArray();
        string[] notables = source.Where(text => text.Contains("显著", StringComparison.Ordinal)).
            SelectMany(SplitCombinedNotables).ToArray();
        string[] smalls = source.Where(text => !text.Contains("显著", StringComparison.Ordinal) &&
                                               !text.Contains("专精", StringComparison.Ordinal)).ToArray();
        if (smalls.Length == 0) smalls = [$"{cluster.Name}：{cluster.Theme}提高"];
        if (notables.Length == 0) notables = [$"{cluster.Name}·显著：{cluster.Theme}获得强化规则"];
        return (Enumerable.Range(0, smallCount).Select(index => smalls[index % smalls.Length]).ToArray(),
            Enumerable.Range(0, notableCount).Select(index => notables[index % notables.Length]).ToArray());
    }

    private static string NormalizeDescription(string description)
    {
        int pipe = description.IndexOf('|');
        if (pipe < 0) return description.Trim().TrimEnd('。');
        string routeAndName = description[..pipe].Trim();
        string effect = description[(pipe + 1)..].Trim().TrimEnd('。');
        int colon = routeAndName.IndexOf('：');
        string name = colon >= 0 ? routeAndName[(colon + 1)..].Trim() : routeAndName;
        return $"{name}：{effect}";
    }

    private static IEnumerable<string> SplitCombinedNotables(string description)
    {
        string text = description.StartsWith("显著：", StringComparison.Ordinal) ? description[3..] : description;
        MatchCollection headings = CombinedNotableRegex().Matches(text);
        if (headings.Count < 2) return [description];
        var result = new List<string>(headings.Count);
        for (int index = 0; index < headings.Count; index++)
        {
            int start = headings[index].Index;
            int end = index + 1 < headings.Count ? headings[index + 1].Index : text.Length;
            result.Add(text[start..end].Trim().TrimStart('；').TrimEnd('；'));
        }
        return result;
    }

    private static string DescriptionEffect(string description)
    {
        string normalized = NormalizeDescription(description);
        int colon = normalized.IndexOf('：');
        return (colon >= 0 ? normalized[(colon + 1)..] : normalized).Trim();
    }

    private static PassiveEffectKind ThemeEffect(string text, int ordinal)
    {
        if (text.Contains("体魄", StringComparison.Ordinal)) return PassiveEffectKind.FlatPhysique;
        if (text.Contains("灵巧", StringComparison.Ordinal)) return PassiveEffectKind.FlatDexterity;
        if (text.Contains("精神", StringComparison.Ordinal)) return PassiveEffectKind.FlatSpirit;
        if (text.Contains("能量", StringComparison.Ordinal) && !text.Contains("护盾", StringComparison.Ordinal)) return PassiveEffectKind.FlatEnergy;
        if (text.Contains("命中", StringComparison.Ordinal)) return PassiveEffectKind.FlatAccuracy;
        if (text.Contains("攻击速度", StringComparison.Ordinal)) return PassiveEffectKind.IncreasedAttackSpeedBasisPoints;
        if (text.Contains("暴击伤害", StringComparison.Ordinal)) return PassiveEffectKind.IncreasedCriticalMultiplierBasisPoints;
        if (text.Contains("暴击", StringComparison.Ordinal)) return PassiveEffectKind.IncreasedCriticalChanceBasisPoints;
        if (text.Contains("流血", StringComparison.Ordinal)) return PassiveEffectKind.IncreasedBleedDamageBasisPoints;
        if (text.Contains("中毒", StringComparison.Ordinal) || text.Contains("点燃", StringComparison.Ordinal) ||
            text.Contains("持续伤害", StringComparison.Ordinal)) return PassiveEffectKind.IncreasedDamageOverTimeBasisPoints;
        if (text.Contains("生命", StringComparison.Ordinal)) return PassiveEffectKind.IncreasedMaximumLifeBasisPoints;
        if (text.Contains("法力", StringComparison.Ordinal) || text.Contains("保留", StringComparison.Ordinal)) return PassiveEffectKind.FlatMaximumMana;
        if (text.Contains("护盾", StringComparison.Ordinal)) return PassiveEffectKind.IncreasedShieldBasisPoints;
        if (text.Contains("灵障", StringComparison.Ordinal)) return PassiveEffectKind.IncreasedShieldBasisPoints;
        if (text.Contains("护甲", StringComparison.Ordinal)) return PassiveEffectKind.IncreasedArmorBasisPoints;
        if (text.Contains("闪避", StringComparison.Ordinal)) return PassiveEffectKind.IncreasedEvasionBasisPoints;
        if (text.Contains("压制", StringComparison.Ordinal)) return PassiveEffectKind.SpellSuppressionBasisPoints;
        if (text.Contains("格挡", StringComparison.Ordinal)) return PassiveEffectKind.BlockChanceBasisPoints;
        if (text.Contains("虚空抗", StringComparison.Ordinal)) return PassiveEffectKind.VoidResistanceBasisPoints;
        if (text.Contains("火焰抗", StringComparison.Ordinal)) return PassiveEffectKind.FireResistanceBasisPoints;
        if (text.Contains("冰霜抗", StringComparison.Ordinal)) return PassiveEffectKind.ColdResistanceBasisPoints;
        if (text.Contains("闪电抗", StringComparison.Ordinal)) return PassiveEffectKind.LightningResistanceBasisPoints;
        if (text.Contains("召唤", StringComparison.Ordinal)) return PassiveEffectKind.IncreasedMinionDamageBasisPoints;
        if (text.Contains("伙伴", StringComparison.Ordinal) || text.Contains("灵兽", StringComparison.Ordinal)) return PassiveEffectKind.IncreasedCompanionDamageBasisPoints;
        if (text.Contains("构装", StringComparison.Ordinal)) return PassiveEffectKind.IncreasedConstructDamageBasisPoints;
        if (text.Contains("陷阱", StringComparison.Ordinal)) return PassiveEffectKind.IncreasedTrapDamageBasisPoints;
        if (text.Contains("光环", StringComparison.Ordinal)) return PassiveEffectKind.IncreasedAuraEffectBasisPoints;
        if (text.Contains("诅咒", StringComparison.Ordinal)) return PassiveEffectKind.IncreasedCurseEffectBasisPoints;
        if (text.Contains("范围", StringComparison.Ordinal) || text.Contains("距离", StringComparison.Ordinal)) return PassiveEffectKind.IncreasedSkillRangeBasisPoints;
        if (text.Contains("冷却", StringComparison.Ordinal)) return PassiveEffectKind.IncreasedCooldownRecoveryBasisPoints;
        if (text.Contains("法术", StringComparison.Ordinal)) return PassiveEffectKind.IncreasedSpellDamageBasisPoints;
        if (text.Contains("元素", StringComparison.Ordinal) || text.Contains("火焰", StringComparison.Ordinal) ||
            text.Contains("冰霜", StringComparison.Ordinal) || text.Contains("闪电", StringComparison.Ordinal))
            return PassiveEffectKind.IncreasedElementalDamageBasisPoints;
        if (text.Contains("虚空", StringComparison.Ordinal)) return PassiveEffectKind.IncreasedVoidDamageBasisPoints;
        if (text.Contains("剑", StringComparison.Ordinal)) return PassiveEffectKind.IncreasedSwordDamageBasisPoints;
        if (text.Contains("斧", StringComparison.Ordinal)) return PassiveEffectKind.IncreasedAxeDamageBasisPoints;
        if (text.Contains("锤", StringComparison.Ordinal)) return PassiveEffectKind.IncreasedMaceDamageBasisPoints;
        if (text.Contains("匕首", StringComparison.Ordinal)) return PassiveEffectKind.IncreasedDaggerDamageBasisPoints;
        if (text.Contains("弓", StringComparison.Ordinal)) return PassiveEffectKind.IncreasedBowDamageBasisPoints;
        if (text.Contains("法杖", StringComparison.Ordinal)) return PassiveEffectKind.IncreasedWandDamageBasisPoints;
        if (text.Contains("盾击", StringComparison.Ordinal)) return PassiveEffectKind.IncreasedShieldAttackDamageBasisPoints;
        if (text.Contains("徒手", StringComparison.Ordinal)) return PassiveEffectKind.IncreasedUnarmedDamageBasisPoints;
        if (text.Contains("双持", StringComparison.Ordinal)) return PassiveEffectKind.IncreasedDualWieldDamageBasisPoints;
        if (text.Contains("双手", StringComparison.Ordinal)) return PassiveEffectKind.IncreasedTwoHandDamageBasisPoints;
        if (text.Contains("单手", StringComparison.Ordinal)) return PassiveEffectKind.IncreasedOneHandDamageBasisPoints;
        return PassiveEffectKind.IncreasedAttackSkillDamageBasisPoints;
    }

    private static int ParseValue(string description, PassiveEffectKind effect, PassiveNodeKind kind)
    {
        Match percentage = PercentageRegex().Match(description);
        Match integer = IntegerRegex().Match(description);
        if (effect is PassiveEffectKind.FlatPhysique or PassiveEffectKind.FlatDexterity or
            PassiveEffectKind.FlatSpirit or PassiveEffectKind.FlatEnergy or PassiveEffectKind.FlatAccuracy or
            PassiveEffectKind.FlatMaximumLife or PassiveEffectKind.FlatMaximumMana)
            return integer.Success ? int.Parse(integer.Groups[1].Value) : kind == PassiveNodeKind.Notable ? 50 : 20;
        if (percentage.Success) return checked((int)Math.Round(double.Parse(percentage.Groups[1].Value) * 100));
        return kind switch { PassiveNodeKind.Small => 1_800, PassiveNodeKind.Notable => 4_000, _ => 2_500 };
    }

    private static string MediumAnchor(string slot)
    {
        int vertex = int.Parse(slot.AsSpan(1, 1));
        char band = slot[3];
        int ordinal = SlotOrdinal(slot);
        return band switch
        {
            'I' => ordinal <= 4 ? RingId("inner", vertex, 1 + (ordinal - 1) * 2 % 7) :
                RadialId(vertex, "i2m", 1 + (ordinal - 5) % 5),
            'R' => ordinal <= 5 ? RingId("middle", vertex, 1 + (ordinal - 1) * 2 % 9) :
                RadialId(vertex, "m2o", ordinal - 5),
            _ => RingId("outer", vertex, 1 + (ordinal - 1) * 2 % 11),
        };
    }


    private static string LargeAnchor(string slot)
    {
        int edge = int.Parse(slot.AsSpan(1, 1));
        int point = int.Parse(slot.AsSpan(4, 2));
        return RingId("outer", edge, point);
    }

    private static int SlotOrdinal(string slot) => int.Parse(slot.AsSpan(slot.Length - 2, 2));
    private static string ClusterId(P30ClusterData cluster, string suffix) => $"p30.cluster.{cluster.StableSlug}.{suffix}";
    private static string RingId(string ring, int edge, int point) => $"p30.path.ring.{ring}.e{edge}.p{point:00}";
    private static string RadialId(int vertex, string segment, int point) => $"p30.path.radial.v{vertex}.{segment}.p{point:00}";
    private static string VertexId(string ring, int vertex) => $"p30.vertex.{ring}.v{vertex}";
    private static float VertexAngle(int vertex) => -MathF.PI / 2 + vertex * MathF.Tau / 6;
    private static (float X, float Y) Polar(float radius, float angle) =>
        (Snap(MathF.Cos(angle) * radius), Snap(MathF.Sin(angle) * radius));
    private static (float X, float Y) Offset(float originX, float originY, float angle,
        float forward, float sideways) =>
        (Snap(originX + MathF.Cos(angle) * forward - MathF.Sin(angle) * sideways),
            Snap(originY + MathF.Sin(angle) * forward + MathF.Cos(angle) * sideways));
    private static float Snap(float value) => MathF.Round(value / 2f) * 2f;
    private static string RingName(string ring) => ring switch { "inner" => "内环", "middle" => "中环", _ => "外环" };
    private static string StartName(PassiveStartKind start) => start switch
    {
        PassiveStartKind.Physique => "斗士",
        PassiveStartKind.PhysiqueEnergy => "隐士",
        PassiveStartKind.Energy => "秘术师",
        PassiveStartKind.Spirit => "灵能使",
        PassiveStartKind.DexteritySpirit => "僧侣",
        _ => "侠客",
    };
    private static string AttributeName(PassiveEffectKind kind) => kind switch
    {
        PassiveEffectKind.FlatPhysique => "体魄",
        PassiveEffectKind.FlatDexterity => "灵巧",
        PassiveEffectKind.FlatSpirit => "精神",
        _ => "能量",
    };
    private static string AttributeSlug(PassiveEffectKind kind) => kind switch
    {
        PassiveEffectKind.FlatPhysique => "physique",
        PassiveEffectKind.FlatDexterity => "dexterity",
        PassiveEffectKind.FlatSpirit => "spirit",
        _ => "energy",
    };
    private static string NodeName(P30ClusterData cluster, PassiveNodeKind kind, string description, int ordinal) => kind switch
    {
        PassiveNodeKind.Mastery => cluster.MasteryKey,
        _ => DescriptionTitle(description, cluster.Name, kind, ordinal),
    };

    private static string DescriptionTitle(string description, string clusterName, PassiveNodeKind kind, int ordinal)
    {
        string normalized = NormalizeDescription(description);
        int colon = normalized.IndexOf('：');
        string title = (colon >= 0 ? normalized[..colon] : normalized).Trim();
        title = Regex.Replace(title, "·?(?:小点(?:\\s*×\\d+)?|显著)$", string.Empty).Trim();
        if (title is "小点" or "显著" or "中央" || title.Length == 0)
            return kind == PassiveNodeKind.Notable ? clusterName : $"{clusterName}·小点{ordinal + 1}";
        return title;
    }
    private static void ValidateData()
    {
        if (Data.Version != Version || Data.Clusters.Count != ExpectedMediumClusters + ExpectedLargeClusters ||
            Data.Clusters.Count(cluster => cluster.Size == "medium") != ExpectedMediumClusters ||
            Data.Clusters.Count(cluster => cluster.Size == "large") != ExpectedLargeClusters)
            throw new InvalidDataException("P30 passive-tree resource does not match p30.v1 totals.");
        if (Data.Clusters.Any(cluster => string.IsNullOrWhiteSpace(cluster.Name) ||
            string.IsNullOrWhiteSpace(cluster.StableSlug) || cluster.Descriptions.Count == 0))
            throw new InvalidDataException("P30 passive-tree resource contains an incomplete cluster.");
    }

    private static P30PassiveTreeData Load()
    {
        const string resource = "GameForWork.Core.P30.Data.p30-passive-tree.json";
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource) ??
            throw new InvalidDataException($"Missing embedded P30 passive resource: {resource}");
        return JsonSerializer.Deserialize<P30PassiveTreeData>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ??
            throw new InvalidDataException("P30 passive resource is invalid.");
    }

    [GeneratedRegex(@"(-?\d+(?:\.\d+)?)\s*%")]
    private static partial Regex PercentageRegex();
    [GeneratedRegex(@"\+?\s*(\d+)")]
    private static partial Regex IntegerRegex();
    [GeneratedRegex(@"(?:^|；)([^；：]{2,12}·?显著?[^；：]{0,4})：")]
    private static partial Regex CombinedNotableRegex();
}
