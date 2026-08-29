using GameForWork.Core.P1.Progression;
using Godot;

namespace GameForWork.GodotClient;

/// <summary>One canvas and one spatial index; no per-node Godot controls.</summary>
public partial class P1PassiveTreeView : Control
{
    private static readonly Color LockedColor = new("333843");
    private static readonly Color AvailableColor = new("477d79");
    private static readonly Color AllocatedColor = new("c28b3c");
    private static readonly Color SelectedColor = new("f0cf72");
    private static readonly Color PlannedColor = new("8e78c8");
    private const float DragThreshold = 7f;
    private const float SpatialCell = 140f;
    private readonly Dictionary<string, Vector2> _centers = new(StringComparer.Ordinal);
    private readonly Dictionary<Vector2I, List<PassiveNodeDefinition>> _spatial = [];
    private readonly HashSet<string> _planned = new(StringComparer.Ordinal);
    private PassiveNodeDefinition[] _nodes = [];
    private IReadOnlySet<string> _allocated = new HashSet<string>();
    private IReadOnlyDictionary<string, PassiveJewelKind> _socketedJewels = new Dictionary<string, PassiveJewelKind>();
    private int _earnedPoints;
    private PassiveStartKind _start = PassiveStartKind.Physique;
    private string _search = string.Empty;
    private Vector2 _pan;
    private float _zoom = 0.42f;
    private bool _leftPressed;
    private bool _dragging;
    private Vector2 _pressPosition;
    private string? _hovered;
    private string _stateSignature = string.Empty;
    private Texture2D? _backdrop;

    public event Action<string>? NodeSelected;
    public event Action<string>? NodeAllocateRequested;
    public event Action<string>? NodeRefundRequested;
    public event Action<string, PassiveJewelKind>? JewelDropRequested;
    public string? SelectedStableId { get; private set; }

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(820, 470);
        MouseFilter = MouseFilterEnum.Stop;
        const string backdrop = "res://assets/p21/trees/p21-passive-backdrop.png";
        if (ResourceLoader.Exists(backdrop)) _backdrop = GD.Load<Texture2D>(backdrop);
        _nodes = P1PassiveTree.Nodes.OrderBy(node => node.StableId, StringComparer.Ordinal).ToArray();
        BuildLayoutAndIndex();
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("11151d"), true);
        if (_backdrop is not null)
        {
            float side = Math.Min(Size.X, Size.Y) * .98f;
            DrawTextureRect(_backdrop, new Rect2((Size - new Vector2(side, side)) / 2, new Vector2(side, side)),
                false, new Color(1, 1, 1, .26f));
        }
        var drawnEdges = new HashSet<string>(StringComparer.Ordinal);
        foreach (PassiveNodeDefinition node in _nodes)
        {
            Vector2 to = ToScreen(_centers[node.StableId]);
            if (!VisibleWithMargin(to, 80)) continue;
            foreach (string neighbor in P1PassiveTree.Neighbors(node.StableId))
            {
                string edge = string.CompareOrdinal(node.StableId, neighbor) < 0 ? node.StableId + '|' + neighbor : neighbor + '|' + node.StableId;
                if (!drawnEdges.Add(edge) || !_centers.TryGetValue(neighbor, out Vector2 linked)) continue;
                bool active = _allocated.Contains(node.StableId) && _allocated.Contains(neighbor);
                DrawLine(to, ToScreen(linked), active ? new Color("98713b") : new Color("303946"), active ? 2.2f : 1f, true);
            }
        }

        if (SelectedStableId is { } selectedId && P1PassiveTree.Get(selectedId).Kind == PassiveNodeKind.JewelSocket)
        {
            DrawCircle(ToScreen(_centers[selectedId]), 150f * _zoom, new Color("6b84ad44"));
            DrawCircle(ToScreen(_centers[selectedId]), 150f * _zoom, new Color("86a3cf"), false, 1.5f);
        }

        foreach (IGrouping<PassiveBranch, PassiveNodeDefinition> sector in _nodes.GroupBy(node => node.Branch))
        {
            PassiveNodeDefinition marker = sector.OrderByDescending(node => node.X * node.X + node.Y * node.Y).First();
            Vector2 position = ToScreen(new Vector2(marker.X, marker.Y) * 1.12f);
            if (VisibleWithMargin(position, 80))
                DrawString(ThemeDB.FallbackFont, position, SectorName(sector.Key), HorizontalAlignment.Center,
                    72, Math.Max(9, (int)(14 * Math.Clamp(_zoom, .7f, 1.1f))), new Color("837b69"));
        }

        foreach (PassiveNodeDefinition node in _nodes)
        {
            Vector2 center = ToScreen(_centers[node.StableId]);
            if (!VisibleWithMargin(center, 30)) continue;
            if (_zoom < .28f && node.Kind == PassiveNodeKind.Small && node.Start == PassiveStartKind.None &&
                !_allocated.Contains(node.StableId) && !_planned.Contains(node.StableId)) continue;
            float radius = NodeRadius(node) * Math.Clamp(_zoom, 0.62f, 1.25f);
            bool allocated = _allocated.Contains(node.StableId);
            bool available = IsAvailable(node);
            bool selected = SelectedStableId == node.StableId;
            bool search = SearchMatch(node);
            Color fill = allocated ? AllocatedColor : available ? AvailableColor : LockedColor;
            if (_socketedJewels.TryGetValue(node.StableId, out PassiveJewelKind socketedJewel))
                fill = P205JewelVisual.ColorFor(socketedJewel).Darkened(.2f);
            Color border = selected || search ? SelectedColor : _planned.Contains(node.StableId) ? PlannedColor : fill.Lightened(0.3f);
            DrawCircle(center, radius, fill);
            DrawCircle(center, radius, border, false, selected || search ? 3 : 1.5f);
            if ((node.Kind != PassiveNodeKind.Small || node.Start != PassiveStartKind.None) && radius >= 5)
            {
                string glyph = node.Kind switch
                { PassiveNodeKind.Notable => "◆", PassiveNodeKind.Mastery => "专", PassiveNodeKind.Rule => "律", PassiveNodeKind.JewelSocket => "◇", _ => "始" };
                DrawString(ThemeDB.FallbackFont, center + new Vector2(-radius * .55f, radius * .4f), glyph,
                    HorizontalAlignment.Center, radius * 1.1f, Math.Max(8, (int)(radius * .9f)), new Color("f2e3bd"));
            }
        }

        DrawString(ThemeDB.FallbackFont, new Vector2(12, 22),
            $"铁誓星盘 · {_nodes.Length:N0} 节点 · 左键拖曳 / 滚轮缩放 · 双击分配 · 右键双击洗点",
            HorizontalAlignment.Left, -1, 13, new Color("cbbd9d"));
        DrawMiniMap();
    }

    public override void _GuiInput(InputEvent inputEvent)
    {
        switch (inputEvent)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown, Pressed: true } wheel:
                ZoomAt(wheel.Position, wheel.ButtonIndex == MouseButton.WheelUp ? 1.12f : .89f); AcceptEvent(); break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } left:
                HandleLeft(left); AcceptEvent(); break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true, DoubleClick: true } right:
                if (HitTest(right.Position) is { } refund) { SelectNode(refund.StableId); NodeRefundRequested?.Invoke(refund.StableId); }
                AcceptEvent(); break;
            case InputEventMouseMotion motion:
                HandleMotion(motion); break;
        }
    }

    public void SetState(IReadOnlySet<string> allocated, int earnedPoints, PassiveStartKind start = PassiveStartKind.Physique,
        IReadOnlyDictionary<string, PassiveJewelKind>? socketedJewels = null)
    {
        socketedJewels ??= new Dictionary<string, PassiveJewelKind>();
        string signature = earnedPoints + "|" + start + "|" + string.Join('|', allocated.OrderBy(id => id, StringComparer.Ordinal)) +
                           "|" + string.Join('|', socketedJewels.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}:{pair.Value}"));
        if (signature == _stateSignature) return;
        _stateSignature = signature; _allocated = allocated; _earnedPoints = earnedPoints; _start = start;
        _socketedJewels = socketedJewels; QueueRedraw();
    }

    public void SetSearch(string query) { _search = query?.Trim() ?? string.Empty; QueueRedraw(); }

    public bool PlanPathToSelected()
    {
        if (SelectedStableId is null) return false;
        IReadOnlyList<string> path = P1PassiveTree.FindShortestPath(SelectedStableId, _allocated, _start);
        if (path.Count == 0) return false;
        _planned.Clear();
        foreach (string id in path) _planned.Add(id);
        QueueRedraw(); return true;
    }

    public int PlannedCost => _planned.Count;

    public void CenterOnStart()
    {
        PassiveNodeDefinition start = P1PassiveTree.Get(P205StartNode(_start));
        _zoom = .72f;
        _pan = -new Vector2(start.X, start.Y) * _zoom;
        QueueRedraw();
    }

    public void FitAll() { _zoom = .27f; _pan = Vector2.Zero; QueueRedraw(); }

    public void ClearPlan() { _planned.Clear(); QueueRedraw(); }

    public override bool _CanDropData(Vector2 atPosition, Variant data) =>
        TryParseJewel(data, out _) && HitTest(atPosition) is { Kind: PassiveNodeKind.JewelSocket } node &&
        _allocated.Contains(node.StableId);

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (TryParseJewel(data, out PassiveJewelKind jewel) &&
            HitTest(atPosition) is { Kind: PassiveNodeKind.JewelSocket } node && _allocated.Contains(node.StableId))
            JewelDropRequested?.Invoke(node.StableId, jewel);
    }

    private void HandleLeft(InputEventMouseButton input)
    {
        if (input.Pressed)
        {
            _leftPressed = true; _dragging = false; _pressPosition = input.Position;
            if (input.DoubleClick && HitTest(input.Position) is { } node)
            { SelectNode(node.StableId); NodeAllocateRequested?.Invoke(node.StableId); _leftPressed = false; }
            return;
        }
        if (_leftPressed && !_dragging)
        { if (HitTest(input.Position) is { } node) SelectNode(node.StableId); else SelectNode(null); }
        _leftPressed = false; _dragging = false;
    }

    private void HandleMotion(InputEventMouseMotion motion)
    {
        if (_leftPressed)
        {
            if (!_dragging && motion.Position.DistanceTo(_pressPosition) >= DragThreshold) _dragging = true;
            if (_dragging) { _pan += motion.Relative; QueueRedraw(); AcceptEvent(); return; }
        }
        PassiveNodeDefinition? hovered = HitTest(motion.Position);
        string? next = hovered?.StableId;
        if (next == _hovered) return;
        _hovered = next;
        TooltipText = hovered is null ? string.Empty : P1UiText.PassiveTooltip(hovered, _allocated.Contains(hovered.StableId), IsAvailable(hovered));
    }

    private void BuildLayoutAndIndex()
    {
        foreach (IGrouping<PassiveBranch, PassiveNodeDefinition> branch in _nodes.Where(node => node.X == 0 && node.Y == 0).GroupBy(node => node.Branch))
        {
            PassiveNodeDefinition[] legacy = branch.ToArray();
            float clusterAngle = -MathF.PI / 2 + (int)branch.Key * MathF.Tau / 10;
            Vector2 cluster = new(MathF.Cos(clusterAngle) * 310, MathF.Sin(clusterAngle) * 245);
            for (int index = 0; index < legacy.Length; index++)
            {
                float orbit = 42 + index / 8 * 29;
                float angle = clusterAngle + index % 8 * MathF.Tau / 8 + index / 8 * .18f;
                _centers[legacy[index].StableId] = cluster + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * orbit;
            }
        }
        foreach (PassiveNodeDefinition node in _nodes.Where(node => node.X != 0 || node.Y != 0)) _centers[node.StableId] = new Vector2(node.X, node.Y);
        foreach (PassiveNodeDefinition node in _nodes)
        {
            Vector2 center = _centers[node.StableId];
            var key = new Vector2I(Mathf.FloorToInt(center.X / SpatialCell), Mathf.FloorToInt(center.Y / SpatialCell));
            if (!_spatial.TryGetValue(key, out List<PassiveNodeDefinition>? bucket)) _spatial[key] = bucket = [];
            bucket.Add(node);
        }
    }

    private PassiveNodeDefinition? HitTest(Vector2 screen)
    {
        Vector2 world = ToWorld(screen);
        var cell = new Vector2I(Mathf.FloorToInt(world.X / SpatialCell), Mathf.FloorToInt(world.Y / SpatialCell));
        PassiveNodeDefinition? best = null; float bestDistance = float.MaxValue;
        for (int y = -1; y <= 1; y++) for (int x = -1; x <= 1; x++)
        {
            if (!_spatial.TryGetValue(cell + new Vector2I(x, y), out List<PassiveNodeDefinition>? bucket)) continue;
            foreach (PassiveNodeDefinition node in bucket)
            {
                float distance = world.DistanceTo(_centers[node.StableId]);
                if (distance <= NodeRadius(node) / Math.Max(_zoom, .42f) + 7 / _zoom && distance < bestDistance)
                { best = node; bestDistance = distance; }
            }
        }
        return best;
    }

    private void ZoomAt(Vector2 position, float factor)
    { Vector2 before = ToWorld(position); _zoom = Math.Clamp(_zoom * factor, .18f, 1.5f); _pan = position - Size / 2 - before * _zoom; QueueRedraw(); }

    private void SelectNode(string? stableId)
    { SelectedStableId = stableId; if (stableId is not null) NodeSelected?.Invoke(stableId); QueueRedraw(); }

    private bool IsAvailable(PassiveNodeDefinition node) => !_allocated.Contains(node.StableId) &&
        (node.Start == _start && _allocated.Count == 0 || P1PassiveTree.Neighbors(node.StableId).Any(_allocated.Contains)) &&
        _allocated.Count < Math.Min(_earnedPoints, PassiveTreeAllocation.MaximumAllocatedPoints);
    private bool SearchMatch(PassiveNodeDefinition node) => _search.Length > 0 &&
        (node.DisplayName.Contains(_search, StringComparison.OrdinalIgnoreCase) || node.Effects.Any(effect => P1UiText.PassiveEffect(effect).Contains(_search, StringComparison.OrdinalIgnoreCase)));
    private Vector2 ToScreen(Vector2 world) => Size / 2 + world * _zoom + _pan;
    private Vector2 ToWorld(Vector2 screen) => (screen - Size / 2 - _pan) / _zoom;
    private bool VisibleWithMargin(Vector2 point, float margin) => point.X >= -margin && point.Y >= -margin && point.X <= Size.X + margin && point.Y <= Size.Y + margin;
    private static float NodeRadius(PassiveNodeDefinition node) => node.Kind switch
    { PassiveNodeKind.Small when node.Start != PassiveStartKind.None => 16, PassiveNodeKind.Small => 7, PassiveNodeKind.Notable => 11, PassiveNodeKind.Mastery => 13, PassiveNodeKind.Rule => 15, _ => 12 };

    private static string P205StartNode(PassiveStartKind start) => start switch
    {
        PassiveStartKind.Dexterity => "core.passive.start.dexterity",
        PassiveStartKind.Spirit => "core.passive.start.spirit",
        PassiveStartKind.Energy => "core.passive.start.energy",
        _ => "core.passive.start.physique",
    };

    private static string SectorName(PassiveBranch branch) => branch switch
    {
        PassiveBranch.HeavyWeapon => "重兵", PassiveBranch.Bleed => "流血", PassiveBranch.Defense => "防御",
        PassiveBranch.Mobility => "机动", PassiveBranch.Critical => "暴击", PassiveBranch.Accuracy => "命中",
        PassiveBranch.Mana => "法力", PassiveBranch.WarCry => "战吼", PassiveBranch.Flask => "药剂",
        PassiveBranch.Elemental => "元素", PassiveBranch.Void => "虚空", _ => "护盾",
    };

    private static bool TryParseJewel(Variant data, out PassiveJewelKind jewel)
    {
        jewel = default;
        if (data.VariantType != Variant.Type.String) return false;
        string[] parts = data.AsString().Split('|');
        if (parts.Length != 2 || parts[0] != "p205-jewel" || !int.TryParse(parts[1], out int raw) ||
            !Enum.IsDefined(typeof(PassiveJewelKind), raw)) return false;
        jewel = (PassiveJewelKind)raw;
        return true;
    }

    private void DrawMiniMap()
    {
        Rect2 area = new(Size.X - 132, Size.Y - 98, 120, 86);
        DrawRect(area, new Color("0b0e14cc"), true);
        DrawRect(area, new Color("596473"), false, 1);
        foreach (PassiveNodeDefinition node in _nodes.Where(node => node.Kind != PassiveNodeKind.Small || node.Start != PassiveStartKind.None))
        {
            Vector2 point = area.GetCenter() + new Vector2(node.X / 1700f * area.Size.X * .45f, node.Y / 1350f * area.Size.Y * .45f);
            DrawCircle(point, node.Start == PassiveStartKind.None ? 1.2f : 2.4f, _allocated.Contains(node.StableId) ? AllocatedColor : LockedColor.Lightened(.25f));
        }
    }
}
