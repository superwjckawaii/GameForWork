using GameForWork.Core.Maps;
using GameForWork.Core.Atlas;
using Godot;

namespace GameForWork.GodotClient;

public partial class MapCraftWindow : IndependentWindow
{
    private readonly HashSet<int> _excluded = [];
    private SpinBox? _quality;
    private OptionButton? _rarity;
    private MenuButton? _excludedMenu;
    private CheckBox? _fill;
    private CheckBox? _corrupt;
    private OptionButton? _failure;
    private Action<MapBatchRule>? _save;

    public void Initialize()
    {
        VBoxContainer root = InitializePixelWindow("做图目标设置", new Vector2I(620, 430), new Vector2I(520, 360));
        root.AddChild(new Label { Text = "批量制图按：品质 → 稀有度 → 排除词缀 → 崇高补满 → 腐化 的顺序执行。", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        var form = new HFlowContainer(); root.AddChild(form);
        _quality = new SpinBox { MinValue = 0, MaxValue = 20, Value = 20, Prefix = "目标品质 " }; form.AddChild(_quality);
        _rarity = new OptionButton(); _rarity.AddItem("目标：魔法", (int)MapRarity.Magic); _rarity.AddItem("目标：稀有", (int)MapRarity.Rare); form.AddChild(_rarity);
        _excludedMenu = new MenuButton { Text = "排除词缀（无）", TooltipText = "可多选；再次点击取消。" };
        PopupMenu popup = _excludedMenu.GetPopup();
        foreach (MapAffixDefinition definition in MapAffixCatalog.All) popup.AddCheckItem(definition.DisplayName, (int)definition.Kind);
        popup.IdPressed += id =>
        {
            int value = (int)id; int index = popup.GetItemIndex(value); bool enabled = !popup.IsItemChecked(index);
            popup.SetItemChecked(index, enabled); if (enabled) _excluded.Add(value); else _excluded.Remove(value);
            _excludedMenu.Text = _excluded.Count == 0 ? "排除词缀（无）" : $"已排除 {_excluded.Count} 项";
        };
        form.AddChild(_excludedMenu);
        _fill = new CheckBox { Text = "使用崇高金补满 6 条词缀" }; root.AddChild(_fill);
        _corrupt = new CheckBox { Text = "完成后腐化（可能永久摧毁地图）" }; root.AddChild(_corrupt);
        var failureRow = new HBoxContainer(); failureRow.AddChild(new Label { Text = "最终出现排除词缀" });
        _failure = new OptionButton(); _failure.AddItem("保留地图", (int)BatchFailureBehavior.Keep); _failure.AddItem("出售地图", (int)BatchFailureBehavior.Sell);
        failureRow.AddChild(_failure); root.AddChild(failureRow);
        root.AddChild(new Label { Text = "材料不足时批处理自动停止并保留当前地图；已消耗材料不返还。", Modulate = new Color("b7c1d4") });
        var save = new Button { Text = "保存做图目标" };
        save.Pressed += () => { _save?.Invoke(Read()); Hide(); };
        root.AddChild(save);
    }

    public void Open(MapBatchRule rule, Action<MapBatchRule> save)
    {
        _save = save;
        _quality!.Value = rule.MinimumQuality;
        _rarity!.Select(_rarity.GetItemIndex((int)rule.TargetRarity));
        _fill!.ButtonPressed = rule.FillAffixes;
        _corrupt!.ButtonPressed = rule.Corrupt;
        _failure!.Select(_failure.GetItemIndex((int)rule.ExcludedAffixBehavior));
        _excluded.Clear(); _excluded.UnionWith(rule.ExcludedAffixes?.Select(kind => (int)kind) ?? []);
        PopupMenu popup = _excludedMenu!.GetPopup();
        for (int index = 0; index < popup.ItemCount; index++) popup.SetItemChecked(index, _excluded.Contains(popup.GetItemId(index)));
        _excludedMenu.Text = _excluded.Count == 0 ? "排除词缀（无）" : $"已排除 {_excluded.Count} 项";
        OpenCentered();
    }

    private MapBatchRule Read() => new(
        (MapRarity)_rarity!.GetItemId(_rarity.Selected),
        (int)_quality!.Value,
        _excluded.Select(id => (MapAffixKind)id).ToArray(),
        _fill!.ButtonPressed,
        _corrupt!.ButtonPressed,
        (BatchFailureBehavior)_failure!.GetItemId(_failure.Selected));
}
