using GameForWork.Core.P1.Progression;
using Godot;

namespace GameForWork.GodotClient;

public partial class P1PassiveTreeView : Control
{
    private static readonly Color LockedColor = new("333843");
    private static readonly Color AvailableColor = new("477d79");
    private static readonly Color AllocatedColor = new("c28b3c");
    private static readonly Color SelectedColor = new("f0cf72");
    private static readonly Color PlannedColor = new("8e78c8");
    private readonly Dictionary<string, Button> _buttons = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Vector2> _worldCenters = new(StringComparer.Ordinal);
    private readonly Dictionary<string, float> _nodeSizes = new(StringComparer.Ordinal);
    private IReadOnlySet<string> _allocated = new HashSet<string>();
    private int _earnedPoints;
    private readonly HashSet<string> _planned = new(StringComparer.Ordinal);
    private string _search = string.Empty;
    private Vector2 _pan;
    private float _zoom = 0.55f;
    private Vector2 _lastSize;
    private bool _fitInitialized;

    public event Action<string>? NodeSelected;
    public event Action<string>? NodeAllocateRequested;
    public event Action<string>? NodeRefundRequested;

    public string? SelectedStableId { get; private set; }

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(820, 470);
        MouseFilter = MouseFilterEnum.Pass;
        BuildNodes();
        SetState(_allocated, _earnedPoints);
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("11151d"), true);
        Vector2 origin = ToScreen(new Vector2(600, 450));
        DrawCircle(origin, 22, new Color("242c38"));
        DrawCircle(origin, 22, new Color("b79a62"), false, 3);
        DrawString(ThemeDB.FallbackFont, origin + new Vector2(-14, 5), "起点", HorizontalAlignment.Left, -1, 11, new Color("e8dcc0"));
        foreach (PassiveNodeDefinition node in P1PassiveTree.Nodes)
        {
            Vector2 from = node.PrerequisiteId is null ? origin : ToScreen(_worldCenters[node.PrerequisiteId]);
            Color color = _allocated.Contains(node.StableId)
                ? new Color("98713b")
                : _planned.Contains(node.StableId) ? PlannedColor.Darkened(0.2f) : new Color("3b4552");
            DrawLine(from, ToScreen(_worldCenters[node.StableId]), color, _allocated.Contains(node.StableId) ? 3 : 1, true);
        }

        foreach (PassiveBranch branch in Enum.GetValues<PassiveBranch>())
        {
            int index = (int)branch;
            float angle = -MathF.PI / 2 + index * MathF.Tau / 10;
            Vector2 labelWorld = new Vector2(600, 450) + new Vector2(MathF.Cos(angle) * 505, MathF.Sin(angle) * 370);
            DrawString(ThemeDB.FallbackFont, ToScreen(labelWorld), BranchLabel(branch), HorizontalAlignment.Center,
                70, 13, BranchColor(branch));
        }
    }

    public override void _Process(double delta)
    {
        if (Size != _lastSize)
        {
            _lastSize = Size;
            if (!_fitInitialized && Size.X > 0 && Size.Y > 0)
            {
                _zoom = Math.Clamp(Math.Min(Size.X / 1_250f, Size.Y / 930f), 0.38f, 0.85f);
                _fitInitialized = true;
            }

            ApplyLayout();
        }
    }

    public override void _GuiInput(InputEvent inputEvent)
    {
        if (inputEvent is InputEventMouseButton wheel && wheel.Pressed &&
            wheel.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
        {
            float oldZoom = _zoom;
            _zoom = Math.Clamp(_zoom * (wheel.ButtonIndex == MouseButton.WheelUp ? 1.12f : 0.89f), 0.35f, 1.35f);
            Vector2 center = Size / 2;
            _pan = wheel.Position - center - (wheel.Position - center - _pan) * (_zoom / oldZoom);
            ApplyLayout();
            AcceptEvent();
        }
        else if (inputEvent is InputEventMouseMotion motion &&
                 motion.ButtonMask.HasFlag(MouseButtonMask.Middle))
        {
            _pan += motion.Relative;
            ApplyLayout();
            AcceptEvent();
        }
    }

    public void SetState(IReadOnlySet<string> allocated, int earnedPoints)
    {
        _allocated = allocated;
        _earnedPoints = earnedPoints;
        foreach (PassiveNodeDefinition node in P1PassiveTree.Nodes)
        {
            UpdateNode(node);
        }

        QueueRedraw();
    }

    public void SetSearch(string query)
    {
        _search = query?.Trim() ?? string.Empty;
        foreach (PassiveNodeDefinition node in P1PassiveTree.Nodes)
        {
            UpdateNode(node);
        }
    }

    public bool PlanPathToSelected()
    {
        if (SelectedStableId is null)
        {
            return false;
        }

        string? current = SelectedStableId;
        while (current is not null && !_allocated.Contains(current))
        {
            _planned.Add(current);
            current = P1PassiveTree.Get(current).PrerequisiteId;
        }

        foreach (PassiveNodeDefinition node in P1PassiveTree.Nodes)
        {
            UpdateNode(node);
        }

        QueueRedraw();
        return true;
    }

    public void ClearPlan()
    {
        _planned.Clear();
        foreach (PassiveNodeDefinition node in P1PassiveTree.Nodes)
        {
            UpdateNode(node);
        }

        QueueRedraw();
    }

    private void BuildNodes()
    {
        foreach (IGrouping<PassiveBranch, PassiveNodeDefinition> branch in P1PassiveTree.Nodes.GroupBy(node => node.Branch))
        {
            PassiveNodeDefinition[] branchNodes = branch.ToArray();
            int branchIndex = (int)branch.Key;
            float clusterAngle = -MathF.PI / 2 + branchIndex * MathF.Tau / 10;
            Vector2 cluster = new Vector2(600, 450) +
                              new Vector2(MathF.Cos(clusterAngle) * 390, MathF.Sin(clusterAngle) * 280);
            for (int index = 0; index < branchNodes.Length; index++)
            {
                PassiveNodeDefinition node = branchNodes[index];
                int orbitIndex = index / 6;
                float orbit = 34 + orbitIndex * 31;
                float angle = clusterAngle + (index % 6) * MathF.Tau / 6 + orbitIndex * 0.22f;
                Vector2 center = cluster + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * orbit;
                float size = node.Kind switch
                {
                    PassiveNodeKind.Small => 14,
                    PassiveNodeKind.Notable => 21,
                    PassiveNodeKind.Mastery => 24,
                    _ => 27,
                };
                var button = new Button
                {
                    Text = node.Kind switch
                    {
                        PassiveNodeKind.Small => string.Empty,
                        PassiveNodeKind.Notable => "◆",
                        PassiveNodeKind.Mastery => "专",
                        _ => "律",
                    },
                    Size = Vector2.One * size,
                    CustomMinimumSize = Vector2.One * size,
                    FocusMode = FocusModeEnum.None,
                };
                button.AddThemeFontSizeOverride("font_size", 12);
                button.Pressed += () => SelectNode(node.StableId);
                button.GuiInput += inputEvent =>
                {
                    if (inputEvent is not InputEventMouseButton { Pressed: true, DoubleClick: true } mouse)
                    {
                        return;
                    }

                    SelectNode(node.StableId);
                    if (mouse.ButtonIndex == MouseButton.Left)
                    {
                        NodeAllocateRequested?.Invoke(node.StableId);
                    }
                    else if (mouse.ButtonIndex == MouseButton.Right)
                    {
                        NodeRefundRequested?.Invoke(node.StableId);
                    }

                    button.AcceptEvent();
                };
                AddChild(button);
                _buttons[node.StableId] = button;
                _worldCenters[node.StableId] = center;
                _nodeSizes[node.StableId] = size;
            }
        }

        ApplyLayout();
    }

    private void ApplyLayout()
    {
        foreach ((string stableId, Button button) in _buttons)
        {
            float size = _nodeSizes[stableId];
            button.Position = ToScreen(_worldCenters[stableId]) - Vector2.One * size / 2;
        }

        QueueRedraw();
    }

    private Vector2 ToScreen(Vector2 world) => Size / 2 + (world - new Vector2(600, 450)) * _zoom + _pan;

    private static string BranchLabel(PassiveBranch branch) => branch switch
    {
        PassiveBranch.HeavyWeapon => "重兵",
        PassiveBranch.Bleed => "流血",
        PassiveBranch.Defense => "守御",
        PassiveBranch.WarCry => "战吼",
        PassiveBranch.Mobility => "行路",
        PassiveBranch.Critical => "暴烈",
        PassiveBranch.Accuracy => "洞察",
        PassiveBranch.Mana => "源流",
        PassiveBranch.Shield => "壁垒",
        PassiveBranch.Flask => "炼金",
        _ => branch.ToString(),
    };

    private static Color BranchColor(PassiveBranch branch) => new Color[]
    {
        new("d0a55a"), new("c86b62"), new("7cb1c4"), new("ae8bc5"), new("77b78b"),
        new("d17e54"), new("c6c26a"), new("6f8bc6"), new("70b2b6"), new("b58f68"),
    }[(int)branch];

    private void SelectNode(string stableId)
    {
        SelectedStableId = stableId;
        foreach (PassiveNodeDefinition node in P1PassiveTree.Nodes)
        {
            UpdateNode(node);
        }

        NodeSelected?.Invoke(stableId);
    }

    private void UpdateNode(PassiveNodeDefinition node)
    {
        if (!_buttons.TryGetValue(node.StableId, out Button? button))
        {
            return;
        }

        bool allocated = _allocated.Contains(node.StableId);
        bool pathOpen = node.PrerequisiteId is null || _allocated.Contains(node.PrerequisiteId);
        bool available = !allocated && pathOpen && _allocated.Count < Math.Min(_earnedPoints, PassiveTreeAllocation.MaximumAllocatedPoints);
        Color background = allocated ? AllocatedColor : available ? AvailableColor : LockedColor;
        bool searchMatch = _search.Length > 0 &&
            (node.DisplayName.Contains(_search, StringComparison.OrdinalIgnoreCase) ||
             node.Effects.Any(effect => P1UiText.PassiveEffect(effect).Contains(_search, StringComparison.OrdinalIgnoreCase)));
        Color border = SelectedStableId == node.StableId || searchMatch
            ? SelectedColor
            : _planned.Contains(node.StableId) ? PlannedColor : background.Lightened(0.28f);
        button.TooltipText = P1UiText.PassiveTooltip(node, allocated, available);
        button.AddThemeStyleboxOverride("normal", Style(background, border, 2));
        button.AddThemeStyleboxOverride("hover", Style(background.Lightened(0.13f), SelectedColor, 3));
        button.AddThemeStyleboxOverride("pressed", Style(background.Darkened(0.12f), SelectedColor, 3));
    }

    private static StyleBoxFlat Style(Color background, Color border, int width) => new()
    {
        BgColor = background,
        BorderColor = border,
        BorderWidthLeft = width,
        BorderWidthTop = width,
        BorderWidthRight = width,
        BorderWidthBottom = width,
        CornerRadiusTopLeft = 24,
        CornerRadiusTopRight = 24,
        CornerRadiusBottomLeft = 24,
        CornerRadiusBottomRight = 24,
    };
}
