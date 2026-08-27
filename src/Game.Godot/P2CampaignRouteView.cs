using GameForWork.Core.P1;
using GameForWork.Core.P2;
using Godot;

namespace GameForWork.GodotClient;

public partial class P2CampaignRouteView : VBoxContainer
{
    private readonly Dictionary<string, Button> _buttons = new(StringComparer.Ordinal);
    private Func<P1GameSession>? _session;
    private Action<string>? _selected;
    private string? _selectedId;

    public string? SelectedStableId => _selectedId;

    public void Initialize(Func<P1GameSession> session, Action<string> selected)
    {
        _session = session;
        _selected = selected;
        for (int act = 1; act <= 5; act++)
        {
            var row = new HBoxContainer();
            row.AddChild(new Label
            {
                Text = $"{act}. {P2CampaignCatalog.ActNames[act - 1]}",
                CustomMinimumSize = new Vector2(110, 0),
            });
            foreach (CampaignNodeDefinition node in P2CampaignCatalog.Nodes.Where(node => node.Act == act))
            {
                var button = new Button
                {
                    Text = NodeGlyph(node.Kind),
                    CustomMinimumSize = new Vector2(46, 38),
                    TooltipText = $"{node.DisplayName}\n{KindName(node.Kind)} · {node.DurationMilliseconds / 1_000}s",
                    FocusMode = FocusModeEnum.None,
                };
                button.Pressed += () =>
                {
                    _selectedId = node.StableId;
                    _selected?.Invoke(node.StableId);
                    RefreshState();
                };
                row.AddChild(button);
                _buttons[node.StableId] = button;
            }

            AddChild(row);
        }
    }

    public void RefreshState()
    {
        if (_session is null)
        {
            return;
        }

        P2CampaignState campaign = _session().Campaign;
        foreach (CampaignNodeDefinition node in P2CampaignCatalog.Nodes)
        {
            Button button = _buttons[node.StableId];
            bool completed = campaign.CompletedNodeIds.Contains(node.StableId);
            bool current = campaign.CurrentNode?.StableId == node.StableId;
            bool selected = _selectedId == node.StableId;
            Color background = completed
                ? new Color("4b694d")
                : current ? new Color("8a6435") : new Color("242a33");
            Color border = selected ? new Color("f0d37a") : background.Lightened(0.3f);
            button.AddThemeStyleboxOverride("normal", Frame(background, border, selected ? 3 : 1));
            button.AddThemeStyleboxOverride("hover", Frame(background.Lightened(0.12f), new Color("f0d37a"), 2));
            button.Text = completed ? "✓" : current ? "▶" : NodeGlyph(node.Kind);
        }
    }

    private static string NodeGlyph(CampaignNodeKind kind) => kind switch
    {
        CampaignNodeKind.NormalCombat => "战",
        CampaignNodeKind.StoryEvent => "文",
        CampaignNodeKind.EliteCombat => "精",
        CampaignNodeKind.ActBoss => "王",
        _ => "·",
    };

    private static string KindName(CampaignNodeKind kind) => kind switch
    {
        CampaignNodeKind.NormalCombat => "普通战斗",
        CampaignNodeKind.StoryEvent => "剧情事件",
        CampaignNodeKind.EliteCombat => "精英战斗",
        CampaignNodeKind.ActBoss => "幕 Boss",
        _ => kind.ToString(),
    };

    private static StyleBoxFlat Frame(Color background, Color border, int width) => new()
    {
        BgColor = background,
        BorderColor = border,
        BorderWidthLeft = width,
        BorderWidthTop = width,
        BorderWidthRight = width,
        BorderWidthBottom = width,
        CornerRadiusTopLeft = 2,
        CornerRadiusTopRight = 2,
        CornerRadiusBottomLeft = 2,
        CornerRadiusBottomRight = 2,
    };
}
