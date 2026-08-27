using GameForWork.Core.P1.Progression;
using Godot;

namespace GameForWork.GodotClient;

public partial class P1PassiveTreeView : Control
{
    private static readonly Color LockedColor = new("333843");
    private static readonly Color AvailableColor = new("477d79");
    private static readonly Color AllocatedColor = new("c28b3c");
    private static readonly Color SelectedColor = new("f0cf72");
    private readonly Dictionary<string, Button> _buttons = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Vector2> _centers = new(StringComparer.Ordinal);
    private IReadOnlySet<string> _allocated = new HashSet<string>();
    private int _earnedPoints;

    public event Action<string>? NodeSelected;

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
        DrawCircle(new Vector2(410, 235), 28, new Color("242c38"));
        DrawCircle(new Vector2(410, 235), 28, new Color("b79a62"), false, 3);
        DrawString(ThemeDB.FallbackFont, new Vector2(392, 240), "起点", HorizontalAlignment.Left, -1, 12, new Color("e8dcc0"));
        foreach (PassiveNodeDefinition node in P1PassiveTree.Nodes)
        {
            Vector2 from = node.PrerequisiteId is null ? new Vector2(410, 235) : _centers[node.PrerequisiteId];
            Color color = _allocated.Contains(node.StableId) ? new Color("98713b") : new Color("3b4552");
            DrawLine(from, _centers[node.StableId], color, _allocated.Contains(node.StableId) ? 4 : 2, true);
        }

        DrawString(ThemeDB.FallbackFont, new Vector2(42, 24), "重兵", HorizontalAlignment.Left, -1, 15, new Color("d0a55a"));
        DrawString(ThemeDB.FallbackFont, new Vector2(42, 454), "流血", HorizontalAlignment.Left, -1, 15, new Color("c86b62"));
        DrawString(ThemeDB.FallbackFont, new Vector2(733, 24), "防御", HorizontalAlignment.Left, -1, 15, new Color("7cb1c4"));
        DrawString(ThemeDB.FallbackFont, new Vector2(733, 454), "战吼", HorizontalAlignment.Left, -1, 15, new Color("ae8bc5"));
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

    private void BuildNodes()
    {
        IReadOnlyDictionary<PassiveBranch, Vector2[]> layout = new Dictionary<PassiveBranch, Vector2[]>
        {
            [PassiveBranch.HeavyWeapon] = [new(355, 205), new(305, 172), new(252, 142), new(199, 112), new(144, 88), new(91, 65), new(45, 44)],
            [PassiveBranch.Bleed] = [new(355, 265), new(305, 298), new(252, 328), new(199, 358), new(144, 382), new(91, 405), new(45, 426)],
            [PassiveBranch.Defense] = [new(465, 205), new(515, 172), new(568, 142), new(621, 112), new(676, 88), new(729, 65)],
            [PassiveBranch.WarCry] = [new(465, 265), new(515, 298), new(568, 328), new(621, 358), new(676, 382), new(729, 405)],
        };

        foreach (IGrouping<PassiveBranch, PassiveNodeDefinition> branch in P1PassiveTree.Nodes.GroupBy(node => node.Branch))
        {
            int index = 0;
            foreach (PassiveNodeDefinition node in branch)
            {
                Vector2 center = layout[branch.Key][index++];
                float size = node.Kind switch
                {
                    PassiveNodeKind.Small => 30,
                    PassiveNodeKind.Notable => 40,
                    _ => 48,
                };
                var button = new Button
                {
                    Text = node.Kind == PassiveNodeKind.Small ? string.Empty : node.Kind == PassiveNodeKind.Notable ? "显" : "律",
                    Position = center - Vector2.One * size / 2,
                    Size = Vector2.One * size,
                    CustomMinimumSize = Vector2.One * size,
                    FocusMode = FocusModeEnum.None,
                };
                button.AddThemeFontSizeOverride("font_size", 12);
                button.Pressed += () => SelectNode(node.StableId);
                AddChild(button);
                _buttons[node.StableId] = button;
                _centers[node.StableId] = center;
            }
        }
    }

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
        Color border = SelectedStableId == node.StableId ? SelectedColor : background.Lightened(0.28f);
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
