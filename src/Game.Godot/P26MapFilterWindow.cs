using GameForWork.Core.P1.World;
using GameForWork.Core.P12;
using GameForWork.Core.P26;
using Godot;

namespace GameForWork.GodotClient;

public partial class P26MapFilterWindow : P30IndependentWindow
{
    private readonly HashSet<int> _areas = [];
    private readonly HashSet<int> _required = [];
    private readonly HashSet<int> _excluded = [];
    private SpinBox? _minimumTier;
    private SpinBox? _maximumTier;
    private SpinBox? _minimumMonster;
    private SpinBox? _minimumItem;
    private SpinBox? _minimumQuality;
    private OptionButton? _rarity;
    private OptionButton? _corruption;
    private MenuButton? _areaMenu;
    private MenuButton? _requiredMenu;
    private MenuButton? _excludedMenu;
    private Label? _scope;
    private Action<P26MapFilter>? _save;

    public void Initialize()
    {
        VBoxContainer root = InitializePixelWindow("通用地图筛选", new Vector2I(720, 520), new Vector2I(580, 420));
        _scope = new Label { Text = "通用地图筛选", Modulate = new Color("e5ca8a") };
        root.AddChild(_scope);
        root.AddChild(new Label
        {
            Text = "设置只保存到当前入口；主角、佣兵与批量做图互不覆盖。",
            Modulate = new Color("b7c1d4"),
        });

        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        root.AddChild(scroll);
        var form = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(form);
        var common = new HFlowContainer();
        form.AddChild(common);
        _minimumTier = new SpinBox { MinValue = 1, MaxValue = 20, Value = 1, Prefix = "最低 T" };
        _maximumTier = new SpinBox { MinValue = 1, MaxValue = 20, Value = 20, Prefix = "最高 T" };
        _rarity = new OptionButton();
        _rarity.AddItem("全部稀有度", -1);
        _rarity.AddItem("普通", (int)P12MapRarity.Basic);
        _rarity.AddItem("魔法", (int)P12MapRarity.Magic);
        _rarity.AddItem("稀有", (int)P12MapRarity.Rare);
        _corruption = new OptionButton();
        _corruption.AddItem("腐化不限", 0);
        _corruption.AddItem("仅未腐化", 1);
        _corruption.AddItem("仅腐化", 2);
        common.AddChild(_minimumTier);
        common.AddChild(_maximumTier);
        common.AddChild(_rarity);
        common.AddChild(_corruption);

        var quantities = new HFlowContainer();
        form.AddChild(quantities);
        _minimumMonster = new SpinBox { MinValue = 0, MaxValue = 120, Step = 5, Prefix = "怪物数量≥", Suffix = "%" };
        _minimumItem = new SpinBox { MinValue = 0, MaxValue = 250, Step = 5, Prefix = "物品数量≥", Suffix = "%" };
        _minimumQuality = new SpinBox { MinValue = 0, MaxValue = 20, Prefix = "品质≥" };
        quantities.AddChild(_minimumMonster);
        quantities.AddChild(_minimumItem);
        quantities.AddChild(_minimumQuality);

        var advanced = new HFlowContainer();
        form.AddChild(advanced);
        _areaMenu = AddMultiSelect(advanced, "区域（全部）",
            P12MapCatalog.Areas.Select((area, index) => (area.DisplayName, index)), _areas);
        _requiredMenu = AddMultiSelect(advanced, "必含词缀（无）",
            P26MapAffixCatalog.All.Select(definition => (definition.DisplayName, (int)definition.Kind)), _required);
        _excludedMenu = AddMultiSelect(advanced, "排除词缀（无）",
            P26MapAffixCatalog.All.Select(definition => (definition.DisplayName, (int)definition.Kind)), _excluded);

        var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        var reset = new Button { Text = "恢复全部地图" };
        reset.Pressed += () => LoadFilter(P26MapFilter.All);
        var save = new Button { Text = "保存筛选" };
        save.Pressed += () =>
        {
            P26MapFilter filter = ReadFilter().Validate();
            _save?.Invoke(filter);
            Hide();
        };
        actions.AddChild(reset);
        actions.AddChild(save);
        root.AddChild(actions);
    }

    public void Open(string scope, P26MapFilter filter, Action<P26MapFilter> save)
    {
        _scope!.Text = scope;
        _save = save;
        LoadFilter(filter);
        OpenCentered();
    }

    private P26MapFilter ReadFilter()
    {
        int rarityId = _rarity!.GetItemId(_rarity.Selected);
        int corruption = _corruption!.GetItemId(_corruption.Selected);
        int minimumTier = Math.Min((int)_minimumTier!.Value, (int)_maximumTier!.Value);
        int maximumTier = Math.Max((int)_minimumTier.Value, (int)_maximumTier.Value);
        return new P26MapFilter(
            minimumTier,
            maximumTier,
            (int)_minimumItem!.Value * 100,
            25_000,
            (int)_minimumMonster!.Value * 100,
            12_000,
            _areas.Select(index => P12MapCatalog.Areas[index].StableId).ToArray(),
            rarityId < 0 ? [] : [(P12MapRarity)rarityId],
            corruption != 2,
            corruption != 1,
            (int)_minimumQuality!.Value,
            _required.Select(id => (P12MapAffixKind)id).ToArray(),
            _excluded.Select(id => (P12MapAffixKind)id).ToArray());
    }

    private void LoadFilter(P26MapFilter filter)
    {
        filter = filter.Validate();
        _minimumTier!.Value = filter.MinimumTier;
        _maximumTier!.Value = filter.MaximumTier;
        _minimumMonster!.Value = filter.MinimumMonsterQuantityBasisPoints / 100;
        _minimumItem!.Value = filter.MinimumItemQuantityBasisPoints / 100;
        _minimumQuality!.Value = filter.MinimumQuality;
        int rarityId = filter.Rarities is { Count: 1 } ? (int)filter.Rarities[0] : -1;
        _rarity!.Select(_rarity.GetItemIndex(rarityId));
        int corruption = filter.IncludeUncorrupted && filter.IncludeCorrupted ? 0 : filter.IncludeUncorrupted ? 1 : 2;
        _corruption!.Select(_corruption.GetItemIndex(corruption));
        LoadSelections(_areaMenu!, _areas, filter.AreaIds?.Select(id =>
            P12MapCatalog.Areas.Select((area, index) => (area, index)).First(pair => pair.area.StableId == id).index) ?? []);
        LoadSelections(_requiredMenu!, _required, filter.RequiredAffixes?.Select(kind => (int)kind) ?? []);
        LoadSelections(_excludedMenu!, _excluded, filter.ExcludedAffixes?.Select(kind => (int)kind) ?? []);
    }

    private static MenuButton AddMultiSelect(Control parent, string emptyText,
        IEnumerable<(string Label, int Id)> options, HashSet<int> selected)
    {
        var button = new MenuButton { Text = emptyText, TooltipText = "可多选；再次点击取消。" };
        button.SetMeta("empty_text", emptyText);
        PopupMenu popup = button.GetPopup();
        foreach ((string label, int id) in options) popup.AddCheckItem(label, id);
        popup.IdPressed += id =>
        {
            int value = (int)id;
            int itemIndex = popup.GetItemIndex(value);
            bool enabled = !popup.IsItemChecked(itemIndex);
            popup.SetItemChecked(itemIndex, enabled);
            if (enabled) selected.Add(value); else selected.Remove(value);
            UpdateMenuText(button, selected.Count);
        };
        parent.AddChild(button);
        return button;
    }

    private static void LoadSelections(MenuButton button, HashSet<int> selected, IEnumerable<int> values)
    {
        selected.Clear();
        selected.UnionWith(values);
        PopupMenu popup = button.GetPopup();
        for (int index = 0; index < popup.ItemCount; index++)
            popup.SetItemChecked(index, selected.Contains(popup.GetItemId(index)));
        UpdateMenuText(button, selected.Count);
    }

    private static void UpdateMenuText(MenuButton button, int count)
    {
        string empty = button.GetMeta("empty_text").AsString();
        button.Text = count == 0 ? empty : $"已选 {count} 项";
    }
}
