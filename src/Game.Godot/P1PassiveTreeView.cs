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
    private int _earnedPoints;
    private string _search = string.Empty;
    private Vector2 _pan;
    private float _zoom = 0.42f;
    private bool _leftPressed;
    private bool _dragging;
    private Vector2 _pressPosition;
    private string? _hovered;
    private string _stateSignature = string.Empty;

    public event Action<string>? NodeSelected;
    public event Action<string>? NodeAllocateRequested;
    public event Action<string>? NodeRefundRequested;
    public string? SelectedStableId { get; private set; }

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(820, 470);
        MouseFilter = MouseFilterEnum.Stop;
        _nodes = P1PassiveTree.Nodes.OrderBy(node => node.StableId, StringComparer.Ordinal).ToArray();
        BuildLayoutAndIndex();
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("11151d"), true);
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

        foreach (PassiveNodeDefinition node in _nodes)
        {
            Vector2 center = ToScreen(_centers[node.StableId]);
            if (!VisibleWithMargin(center, 30)) continue;
            float radius = NodeRadius(node) * Math.Clamp(_zoom, 0.62f, 1.25f);
            bool allocated = _allocated.Contains(node.StableId);
            bool available = IsAvailable(node);
            bool selected = SelectedStableId == node.StableId;
            bool search = SearchMatch(node);
            Color fill = allocated ? AllocatedColor : available ? AvailableColor : LockedColor;
            Color border = selected || search ? SelectedColor : _planned.Contains(node.StableId) ? PlannedColor : fill.Lightened(0.3f);
            DrawCircle(center, radius, fill);
            DrawCircle(center, radius, border, false, selected || search ? 3 : 1.5f);
            if (node.Kind != PassiveNodeKind.Small && radius >= 5)
            {
                string glyph = node.Kind switch
                { PassiveNodeKind.Notable => "◆", PassiveNodeKind.Mastery => "专", PassiveNodeKind.Rule => "律", PassiveNodeKind.JewelSocket => "◇", _ => string.Empty };
                DrawString(ThemeDB.FallbackFont, center + new Vector2(-radius * .55f, radius * .4f), glyph,
                    HorizontalAlignment.Center, radius * 1.1f, Math.Max(8, (int)(radius * .9f)), new Color("f2e3bd"));
            }
        }

        DrawString(ThemeDB.FallbackFont, new Vector2(12, 22),
            $"铁誓星盘 · {_nodes.Length:N0} 节点 · 左键拖曳 / 滚轮缩放 · 双击分配 · 右键双击洗点",
            HorizontalAlignment.Left, -1, 13, new Color("cbbd9d"));
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

    public void SetState(IReadOnlySet<string> allocated, int earnedPoints)
    {
        string signature = earnedPoints + "|" + string.Join('|', allocated.OrderBy(id => id, StringComparer.Ordinal));
        if (signature == _stateSignature) return;
        _stateSignature = signature; _allocated = allocated; _earnedPoints = earnedPoints; QueueRedraw();
    }

    public void SetSearch(string query) { _search = query?.Trim() ?? string.Empty; QueueRedraw(); }

    public bool PlanPathToSelected()
    {
        if (SelectedStableId is null) return false;
        string? current = SelectedStableId;
        var visited = new HashSet<string>(StringComparer.Ordinal);
        while (current is not null && !_allocated.Contains(current) && visited.Add(current))
        { _planned.Add(current); current = P1PassiveTree.Get(current).PrerequisiteId; }
        QueueRedraw(); return true;
    }

    public void ClearPlan() { _planned.Clear(); QueueRedraw(); }

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
        (node.PrerequisiteId is null || P1PassiveTree.Neighbors(node.StableId).Any(_allocated.Contains)) &&
        _allocated.Count < Math.Min(_earnedPoints, PassiveTreeAllocation.MaximumAllocatedPoints);
    private bool SearchMatch(PassiveNodeDefinition node) => _search.Length > 0 &&
        (node.DisplayName.Contains(_search, StringComparison.OrdinalIgnoreCase) || node.Effects.Any(effect => P1UiText.PassiveEffect(effect).Contains(_search, StringComparison.OrdinalIgnoreCase)));
    private Vector2 ToScreen(Vector2 world) => Size / 2 + world * _zoom + _pan;
    private Vector2 ToWorld(Vector2 screen) => (screen - Size / 2 - _pan) / _zoom;
    private bool VisibleWithMargin(Vector2 point, float margin) => point.X >= -margin && point.Y >= -margin && point.X <= Size.X + margin && point.Y <= Size.Y + margin;
    private static float NodeRadius(PassiveNodeDefinition node) => node.Kind switch
    { PassiveNodeKind.Small => 7, PassiveNodeKind.Notable => 11, PassiveNodeKind.Mastery => 13, PassiveNodeKind.Rule => 15, _ => 12 };
}
