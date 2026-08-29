using GameForWork.Core.P1;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P1.World;
using Godot;

namespace GameForWork.GodotClient;

public partial class P2LootFilterPanel : VBoxContainer
{
    private Func<P1GameSession>? _session;
    private Action<string>? _changed;
    private VBoxContainer? _rows;
    private ConfirmationDialog? _editor;
    private OptionButton? _rarityMode;
    private OptionButton? _rarityMinimum;
    private OptionButton? _rarityMaximum;
    private OptionButton? _category;
    private OptionButton? _slot;
    private OptionButton? _base;
    private SpinBox? _minimumItemLevel;
    private SpinBox? _maximumItemLevel;
    private SpinBox? _minimumEstimatedValue;
    private SpinBox? _maximumEstimatedValue;
    private SpinBox? _minimumLinks;
    private SpinBox? _maximumLinks;
    private LineEdit? _affixFamily;
    private SpinBox? _minimumAffixValue;
    private LineEdit? _baseTag;
    private SpinBox? _bestAffixTier;
    private SpinBox? _worstAffixTier;
    private CheckBox? _schemeNeed;
    private OptionButton? _disposition;
    private int _editingIndex = -1;
    private string _lastSignature = string.Empty;

    public void Initialize(Func<P1GameSession> session, Func<ItemInstance?> selectedItem, Action<string> changed)
    {
        _session = session;
        _changed = changed;
        AddChild(new Label
        {
            Text = "系统保护：锁定、任务/关键物品、首次获得、五连、六连不会被自动处理。用户规则从上到下首次匹配；未匹配时保留。",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        AddChild(new Label
        {
            Text = "一条规则中的 Match 条件全部满足（AND）；需要 OR 时新增多条规则。拖拽或箭头可调整优先级。",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        var add = new Button { Text = "新增规则" };
        add.Pressed += () => OpenEditor(-1);
        AddChild(add);
        _rows = new VBoxContainer();
        AddChild(_rows);
        BuildEditor();
    }

    public void RefreshRules()
    {
        if (_session is null) return;
        string signature = Signature(_session().World.Filter.Rules);
        if (signature != _lastSignature) Rebuild();
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data) =>
        data.VariantType == Variant.Type.String && data.AsString().StartsWith("p2-filter|", StringComparison.Ordinal);

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (_rows is null || !int.TryParse(data.AsString().Split('|').Last(), out int source)) return;
        int target = 0;
        for (int index = 0; index < _rows.GetChildCount(); index++)
        {
            Control row = _rows.GetChild<Control>(index);
            if (atPosition.Y > _rows.Position.Y + row.Position.Y + row.Size.Y / 2) target = index + 1;
        }
        Move(source, Math.Clamp(target, 0, Math.Max(0, _rows.GetChildCount() - 1)));
    }

    private void BuildEditor()
    {
        _editor = new ConfirmationDialog
        {
            Title = "过滤规则",
            OkButtonText = "保存规则",
            CancelButtonText = "取消",
            Unresizable = false,
            Exclusive = false,
            Transient = true,
        };
        var body = new VBoxContainer { CustomMinimumSize = new Vector2(520, 420) };
        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(540, 420),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        scroll.AddChild(body);
        _editor.AddChild(scroll);
        _rarityMode = AddOptions(body, "稀有度 Match", ["任意", "等于", "至少", "至多", "区间"]);
        var rarityRow = new HBoxContainer();
        body.AddChild(rarityRow);
        _rarityMinimum = AddOptions(rarityRow, "下限/等于", ["基础", "魔法", "稀有", "传奇"]);
        _rarityMaximum = AddOptions(rarityRow, "上限", ["基础", "魔法", "稀有", "传奇"]);
        _rarityMaximum.Select((int)ItemRarity.Legendary);
        _category = AddOptions(body, "装备类别", ["任意", .. Enum.GetNames<ItemCategory>()]);
        _slot = AddOptions(body, "装备槽位", ["任意", .. Enum.GetNames<EquipmentSlot>()]);
        _base = AddOptions(body, "指定底材", ["任意", .. P1ItemBases.All.OrderBy(item => item.DisplayName).Select(item => item.DisplayName)]);
        var itemLevelRow = new HBoxContainer();
        body.AddChild(itemLevelRow);
        _minimumItemLevel = AddSpin(itemLevelRow, "最低物品等级（0=任意）", 0, 120);
        _maximumItemLevel = AddSpin(itemLevelRow, "最高物品等级（0=任意）", 0, 120);
        var valueRow = new HBoxContainer();
        body.AddChild(valueRow);
        _minimumEstimatedValue = AddSpin(valueRow, "最低公开估值（0=任意）", 0, 100_000);
        _maximumEstimatedValue = AddSpin(valueRow, "最高公开估值（0=任意）", 0, 100_000);
        var linkRow = new HBoxContainer();
        body.AddChild(linkRow);
        _minimumLinks = AddSpin(linkRow, "最低连接数", 0, 6);
        _maximumLinks = AddSpin(linkRow, "最高连接数（0=任意）", 0, 6);
        body.AddChild(new Label { Text = "词缀族 Stable ID（留空为任意）" });
        _affixFamily = new LineEdit { PlaceholderText = "例如 core.affix.ring.life" };
        body.AddChild(_affixFamily);
        _minimumAffixValue = AddSpin(body, "词缀最低数值（0=任意）", 0, 100_000);
        body.AddChild(new Label { Text = "底材标签（留空为任意）" });
        _baseTag = new LineEdit { PlaceholderText = "例如 ring / str_armour / shield" };
        body.AddChild(_baseTag);
        var tierRow = new HBoxContainer();
        body.AddChild(tierRow);
        _bestAffixTier = AddSpin(tierRow, "最高T级（0=任意）", 0, 20);
        _worstAffixTier = AddSpin(tierRow, "最低T级（0=任意）", 0, 20);
        _schemeNeed = new CheckBox { Text = "满足当前技能方案的连接缺口" };
        body.AddChild(_schemeNeed);
        _disposition = AddOptions(body, "处理", ["保留", "出售", "分解", "忽略"]);
        _editor.Confirmed += SaveEditor;
        AddChild(_editor);
    }

    private void Rebuild()
    {
        if (_rows is null || _session is null) return;
        foreach (Node child in _rows.GetChildren()) child.QueueFree();
        LootFilterRule[] rules = _session().World.Filter.Rules.ToArray();
        _lastSignature = Signature(rules);
        for (int index = 0; index < rules.Length; index++)
        {
            int captured = index;
            LootFilterRule rule = rules[index];
            var row = new P2LootFilterRuleRow { SourceIndex = index, CustomMinimumSize = new Vector2(0, 36) };
            var enabled = new CheckBox { ButtonPressed = rule.Enabled, TooltipText = "启用或停用规则" };
            enabled.Toggled += value => Update(captured, rule with { Enabled = value }, "规则启用状态已更新。");
            row.AddChild(enabled);
            var edit = new Button
            {
                Text = $"{index + 1}. {Describe(rule)}",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                Alignment = HorizontalAlignment.Left,
                TooltipText = "点击编辑 Match 条件与处理方式",
            };
            edit.Pressed += () => OpenEditor(captured);
            row.AddChild(edit);
            AddRowButton(row, "↑", () => Move(captured, captured - 1));
            AddRowButton(row, "↓", () => Move(captured, captured + 1));
            AddRowButton(row, "删除", () => Delete(captured));
            _rows.AddChild(row);
        }
    }

    private void OpenEditor(int index)
    {
        if (_session is null || _editor is null) return;
        _editingIndex = index;
        LootFilterRule? rule = index >= 0 && index < _session().World.Filter.Rules.Count
            ? _session().World.Filter.Rules[index]
            : null;
        LoadEditor(rule);
        _editor.Title = rule is null ? "新增过滤规则" : "编辑过滤规则";
        _editor.PopupCentered(new Vector2I(600, 560));
    }

    private void LoadEditor(LootFilterRule? rule)
    {
        _rarityMode!.Select(rule?.Rarity is not null ? 1 : rule?.MinimumRarity is not null && rule.MaximumRarity is not null ? 4 : rule?.MinimumRarity is not null ? 2 : rule?.MaximumRarity is not null ? 3 : 0);
        _rarityMinimum!.Select((int)(rule?.Rarity ?? rule?.MinimumRarity ?? ItemRarity.Basic));
        _rarityMaximum!.Select((int)(rule?.Rarity ?? rule?.MaximumRarity ?? ItemRarity.Legendary));
        SelectEnum(_category!, rule?.Category);
        SelectEnum(_slot!, rule?.Slot);
        string? baseId = rule?.BaseStableId;
        P1ItemBases.All.OrderBy(item => item.DisplayName).Select((item, index) => (item, index))
            .Where(entry => entry.item.StableId == baseId).ToList().ForEach(entry => _base!.Select(entry.index + 1));
        if (baseId is null) _base!.Select(0);
        _minimumItemLevel!.Value = rule?.MinimumItemLevel ?? 0;
        _maximumItemLevel!.Value = rule?.MaximumItemLevel ?? 0;
        _minimumEstimatedValue!.Value = rule?.MinimumEstimatedValue ?? 0;
        _maximumEstimatedValue!.Value = rule?.MaximumEstimatedValue ?? 0;
        _minimumLinks!.Value = rule?.MinimumLinkedSockets ?? 0;
        _maximumLinks!.Value = rule?.MaximumLinkedSockets ?? 0;
        _affixFamily!.Text = rule?.AffixFamilyId ?? string.Empty;
        _minimumAffixValue!.Value = rule?.MinimumAffixValue ?? 0;
        _baseTag!.Text = rule?.BaseTag ?? string.Empty;
        _bestAffixTier!.Value = rule?.MaximumAffixTier ?? 0;
        _worstAffixTier!.Value = rule?.MinimumAffixTier ?? 0;
        _schemeNeed!.ButtonPressed = rule?.RequireCurrentSchemeNeed ?? false;
        _disposition!.Select((int)(rule?.Disposition ?? LootDisposition.Keep));
    }

    private void SaveEditor()
    {
        List<LootFilterRule> rules = _session!().World.Filter.Rules.ToList();
        LootFilterRule? previous = _editingIndex >= 0 && _editingIndex < rules.Count ? rules[_editingIndex] : null;
        int rarityMode = _rarityMode!.Selected;
        ItemRarity low = (ItemRarity)_rarityMinimum!.Selected;
        ItemRarity high = (ItemRarity)_rarityMaximum!.Selected;
        if (low > high) (low, high) = (high, low);
        ItemBaseDefinition? itemBase = _base!.Selected <= 0 ? null : P1ItemBases.All.OrderBy(item => item.DisplayName).ElementAt(_base.Selected - 1);
        string? affix = string.IsNullOrWhiteSpace(_affixFamily!.Text) ? null : _affixFamily.Text.Trim();
        var rule = new LootFilterRule(
            previous?.StableId ?? $"user.filter.{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            (LootDisposition)_disposition!.Selected,
            Rarity: rarityMode == 1 ? low : null,
            BaseStableId: itemBase?.StableId,
            AffixFamilyId: affix,
            MinimumAffixValue: affix is null || _minimumAffixValue!.Value <= 0 ? null : (int)_minimumAffixValue.Value,
            Enabled: previous?.Enabled ?? true,
            Slot: EnumValue<EquipmentSlot>(_slot!),
            MinimumLinkedSockets: (int)_minimumLinks!.Value,
            RequireCurrentSchemeNeed: _schemeNeed!.ButtonPressed,
            MinimumRarity: rarityMode is 2 or 4 ? low : null,
            MaximumRarity: rarityMode is 3 or 4 ? high : null,
            Category: EnumValue<ItemCategory>(_category!),
            MinimumItemLevel: _minimumItemLevel!.Value <= 0 ? null : (int)_minimumItemLevel.Value,
            MaximumItemLevel: _maximumItemLevel!.Value <= 0 ? null : (int)_maximumItemLevel.Value,
            MaximumLinkedSockets: _maximumLinks!.Value <= 0 ? null : (int)_maximumLinks.Value,
            BaseTag: string.IsNullOrWhiteSpace(_baseTag!.Text) ? null : _baseTag.Text.Trim(),
            MinimumAffixTier: _worstAffixTier!.Value <= 0 ? null : (int)_worstAffixTier.Value,
            MaximumAffixTier: _bestAffixTier!.Value <= 0 ? null : (int)_bestAffixTier.Value,
            MinimumEstimatedValue: _minimumEstimatedValue!.Value <= 0 ? null : (int)_minimumEstimatedValue.Value,
            MaximumEstimatedValue: _maximumEstimatedValue!.Value <= 0 ? null : (int)_maximumEstimatedValue.Value);
        if (_editingIndex >= 0 && _editingIndex < rules.Count) rules[_editingIndex] = rule;
        else rules.Add(rule);
        Replace(rules, previous is null ? "过滤规则已新增。" : "过滤规则已更新。");
    }

    private void Move(int source, int target)
    {
        List<LootFilterRule> rules = _session!().World.Filter.Rules.ToList();
        if (source < 0 || source >= rules.Count || target < 0 || target >= rules.Count || source == target) return;
        LootFilterRule rule = rules[source];
        rules.RemoveAt(source);
        rules.Insert(target, rule);
        Replace(rules, "过滤规则顺序已更新。");
    }

    private void Delete(int index)
    {
        List<LootFilterRule> rules = _session!().World.Filter.Rules.ToList();
        if (index < 0 || index >= rules.Count) return;
        rules.RemoveAt(index);
        Replace(rules, "过滤规则已删除。");
    }

    private void Update(int index, LootFilterRule updated, string message)
    {
        List<LootFilterRule> rules = _session!().World.Filter.Rules.ToList();
        rules[index] = updated;
        Replace(rules, message);
    }

    private void Replace(IEnumerable<LootFilterRule> rules, string message)
    {
        _session!().World.Filter.ReplaceRules(rules);
        _changed?.Invoke(message);
        Rebuild();
    }

    private static string Describe(LootFilterRule rule)
    {
        var conditions = new List<string>();
        if (rule.Rarity is not null) conditions.Add($"稀有度={RarityName(rule.Rarity.Value)}");
        if (rule.MinimumRarity is not null) conditions.Add($"稀有度≥{RarityName(rule.MinimumRarity.Value)}");
        if (rule.MaximumRarity is not null) conditions.Add($"稀有度≤{RarityName(rule.MaximumRarity.Value)}");
        if (rule.Category is not null) conditions.Add($"类别={rule.Category}");
        if (rule.Slot is not null) conditions.Add($"槽位={rule.Slot}");
        if (rule.BaseStableId is not null) conditions.Add($"底材={rule.BaseStableId}");
        if (rule.MinimumItemLevel is not null) conditions.Add($"物等≥{rule.MinimumItemLevel}");
        if (rule.MaximumItemLevel is not null) conditions.Add($"物等≤{rule.MaximumItemLevel}");
        if (rule.MinimumEstimatedValue is not null) conditions.Add($"估值≥{rule.MinimumEstimatedValue}");
        if (rule.MaximumEstimatedValue is not null) conditions.Add($"估值≤{rule.MaximumEstimatedValue}");
        if (rule.MinimumLinkedSockets > 0) conditions.Add($"连接≥{rule.MinimumLinkedSockets}");
        if (rule.MaximumLinkedSockets is not null) conditions.Add($"连接≤{rule.MaximumLinkedSockets}");
        if (rule.AffixFamilyId is not null) conditions.Add($"{rule.AffixFamilyId}≥{rule.MinimumAffixValue ?? 0}");
        if (rule.BaseTag is not null) conditions.Add($"底材标签={rule.BaseTag}");
        if (rule.MaximumAffixTier is not null) conditions.Add($"最高T≤{rule.MaximumAffixTier}");
        if (rule.MinimumAffixTier is not null) conditions.Add($"最低T≥{rule.MinimumAffixTier}");
        if (rule.RequireCurrentSchemeNeed) conditions.Add("当前方案缺口");
        return $"{(conditions.Count == 0 ? "任意物品" : string.Join(" 且 ", conditions))} → {DispositionName(rule.Disposition)}";
    }

    private static string Signature(IEnumerable<LootFilterRule> rules) => string.Join('|', rules.Select(rule => rule.ToString()));
    private static string RarityName(ItemRarity rarity) => rarity switch { ItemRarity.Basic => "基础", ItemRarity.Magic => "魔法", ItemRarity.Rare => "稀有", _ => "传奇" };
    private static string DispositionName(LootDisposition value) => value switch
    {
        LootDisposition.Keep => "保留",
        LootDisposition.Sell => "出售",
        LootDisposition.Dismantle => "分解",
        _ => "忽略",
    };

    private static OptionButton AddOptions(Container parent, string label, IEnumerable<string> values)
    {
        parent.AddChild(new Label { Text = label });
        var option = new OptionButton();
        foreach (string value in values) option.AddItem(value);
        parent.AddChild(option);
        return option;
    }

    private static SpinBox AddSpin(Container parent, string label, int minimum, int maximum)
    {
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddChild(new Label { Text = label });
        var spin = new SpinBox { MinValue = minimum, MaxValue = maximum, Step = 1, AllowGreater = false, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddChild(spin);
        parent.AddChild(column);
        return spin;
    }

    private static void AddRowButton(Container row, string text, Action action)
    {
        var button = new Button { Text = text };
        button.Pressed += action;
        row.AddChild(button);
    }

    private static T? EnumValue<T>(OptionButton option) where T : struct, Enum => option.Selected <= 0
        ? null
        : (T)Enum.ToObject(typeof(T), option.Selected - 1);
    private static void SelectEnum<T>(OptionButton option, T? value) where T : struct, Enum => option.Select(value is null ? 0 : Convert.ToInt32(value.Value) + 1);
}

public partial class P2LootFilterRuleRow : HBoxContainer
{
    public int SourceIndex { get; set; }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        var preview = new Label { Text = $"规则 {SourceIndex + 1}" };
        SetDragPreview(preview);
        return Variant.From($"p2-filter|{SourceIndex}");
    }
}
