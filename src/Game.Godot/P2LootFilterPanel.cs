using GameForWork.Core.P1;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P1.World;
using Godot;

namespace GameForWork.GodotClient;

public partial class P2LootFilterPanel : VBoxContainer
{
    private Func<P1GameSession>? _session;
    private Func<ItemInstance?>? _selectedItem;
    private Action<string>? _changed;
    private VBoxContainer? _rows;
    private string _lastSignature = string.Empty;

    public void Initialize(Func<P1GameSession> session, Func<ItemInstance?> selectedItem, Action<string> changed)
    {
        _session = session;
        _selectedItem = selectedItem;
        _changed = changed;
        AddChild(new Label
        {
            Text = "拖拽规则可排序；也可用箭头精确移动。规则从上到下首次匹配。",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        _rows = new VBoxContainer();
        AddChild(_rows);
        var add = new Button { Text = "新增保留规则" };
        add.Pressed += AddRule;
        AddChild(add);
    }

    public void RefreshRules()
    {
        if (_session is null)
        {
            return;
        }

        string signature = string.Join('|', _session().World.Filter.Rules.Select(rule => $"{rule.StableId}:{rule.Enabled}"));
        if (signature != _lastSignature)
        {
            Rebuild();
        }
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data) =>
        data.VariantType == Variant.Type.String && data.AsString().StartsWith("p2-filter|", StringComparison.Ordinal);

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (_rows is null || !int.TryParse(data.AsString().Split('|').Last(), out int source))
        {
            return;
        }

        int target = 0;
        for (int index = 0; index < _rows.GetChildCount(); index++)
        {
            Control row = _rows.GetChild<Control>(index);
            if (atPosition.Y > _rows.Position.Y + row.Position.Y + row.Size.Y / 2)
            {
                target = index + 1;
            }
        }

        Move(source, Math.Clamp(target, 0, Math.Max(0, _rows.GetChildCount() - 1)));
    }

    private void Rebuild()
    {
        if (_rows is null || _session is null)
        {
            return;
        }

        foreach (Node child in _rows.GetChildren())
        {
            child.QueueFree();
        }

        LootFilterRule[] rules = _session().World.Filter.Rules.ToArray();
        _lastSignature = string.Join('|', rules.Select(rule => $"{rule.StableId}:{rule.Enabled}"));
        for (int index = 0; index < rules.Length; index++)
        {
            int captured = index;
            LootFilterRule rule = rules[index];
            var row = new P2LootFilterRuleRow { SourceIndex = index, CustomMinimumSize = new Vector2(0, 34) };
            var enabled = new CheckBox { ButtonPressed = rule.Enabled, TooltipText = "启用或停用规则" };
            enabled.Toggled += value => Update(captured, rule with { Enabled = value }, "规则启用状态已更新。");
            row.AddChild(enabled);
            row.AddChild(new Label
            {
                Text = $"{index + 1}. {Describe(rule)}",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                TooltipText = rule.StableId,
            });
            AddRowButton(row, "↑", () => Move(captured, captured - 1));
            AddRowButton(row, "↓", () => Move(captured, captured + 1));
            AddRowButton(row, "克隆", () => Clone(captured));
            AddRowButton(row, "测试", () => Test(rule));
            AddRowButton(row, "删除", () => Delete(captured));
            _rows.AddChild(row);
        }
    }

    private void AddRule()
    {
        List<LootFilterRule> rules = _session!().World.Filter.Rules.ToList();
        rules.Add(new LootFilterRule(
            $"user.filter.{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            LootDisposition.Keep,
            ItemRarity.Rare));
        Replace(rules, "已新增稀有物品保留规则。");
    }

    private void Move(int source, int target)
    {
        List<LootFilterRule> rules = _session!().World.Filter.Rules.ToList();
        if (source < 0 || source >= rules.Count || target < 0 || target >= rules.Count || source == target)
        {
            return;
        }

        LootFilterRule rule = rules[source];
        rules.RemoveAt(source);
        rules.Insert(target, rule);
        Replace(rules, "过滤规则顺序已更新。");
    }

    private void Clone(int index)
    {
        List<LootFilterRule> rules = _session!().World.Filter.Rules.ToList();
        LootFilterRule source = rules[index];
        rules.Insert(index + 1, source with { StableId = $"{source.StableId}.copy.{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}" });
        Replace(rules, "过滤规则已克隆。");
    }

    private void Delete(int index)
    {
        List<LootFilterRule> rules = _session!().World.Filter.Rules.ToList();
        rules.RemoveAt(index);
        Replace(rules, "过滤规则已删除。");
    }

    private void Update(int index, LootFilterRule updated, string message)
    {
        List<LootFilterRule> rules = _session!().World.Filter.Rules.ToList();
        rules[index] = updated;
        Replace(rules, message);
    }

    private void Test(LootFilterRule rule)
    {
        ItemInstance? item = _selectedItem?.Invoke();
        _changed?.Invoke(item is null
            ? "请先在仓库或整理背包中选择一个物品。"
            : $"测试 {item.Base.DisplayName}：{(rule.Matches(item) ? $"匹配 → {rule.Disposition}" : "不匹配")}。");
    }

    private void Replace(IEnumerable<LootFilterRule> rules, string message)
    {
        _session!().World.Filter.ReplaceRules(rules);
        _changed?.Invoke(message);
        Rebuild();
    }

    private static string Describe(LootFilterRule rule)
    {
        string match = rule.Rarity?.ToString() ?? "任意品质";
        if (rule.BaseStableId is not null)
        {
            match += $" · {rule.BaseStableId}";
        }

        if (rule.AffixFamilyId is not null)
        {
            match += $" · {rule.AffixFamilyId} ≥ {rule.MinimumAffixValue ?? 0}";
        }

        return $"{match} → {rule.Disposition}";
    }

    private static void AddRowButton(Container row, string text, Action action)
    {
        var button = new Button { Text = text };
        button.Pressed += action;
        row.AddChild(button);
    }
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
