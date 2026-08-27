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
    private readonly Texture2D?[] _actTextures = new Texture2D?[5];

    public string? SelectedStableId => _selectedId;

    public void Initialize(Func<P1GameSession> session, Action<string> selected)
    {
        _session = session;
        _selected = selected;
        string[] actPaths =
        [
            "res://assets/p3/campaign/act-1-ash-camp.png",
            "res://assets/p3/campaign/act-2-frost-town.png",
            "res://assets/p3/campaign/act-3-drowned-crypt.png",
            "res://assets/p3/campaign/act-4-crimson-foundry.png",
            "res://assets/p3/campaign/act-5-void-citadel.png",
        ];
        for (int index = 0; index < actPaths.Length; index++)
        {
            _actTextures[index] = ResourceLoader.Exists(actPaths[index])
                ? GD.Load<Texture2D>(actPaths[index])
                : null;
        }
        for (int act = 1; act <= 5; act++)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 6);
            Texture2D? actTexture = ActTexture(act);
            if (actTexture is not null)
            {
                row.AddChild(new TextureRect
                {
                    Texture = actTexture,
                    CustomMinimumSize = new Vector2(78, 42),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                    MouseFilter = MouseFilterEnum.Ignore,
                });
            }

            row.AddChild(new Label
            {
                Text = $"{act}. {P2CampaignCatalog.ActNames[act - 1]}",
                CustomMinimumSize = new Vector2(106, 0),
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

    private Texture2D? ActTexture(int act)
    {
        return act is >= 1 and <= 5 ? _actTextures[act - 1] : null;
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
