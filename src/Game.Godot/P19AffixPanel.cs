using GameForWork.Core.P1.Items;
using Godot;

namespace GameForWork.GodotClient;

public partial class P19AffixPanel : VBoxContainer
{
    private OptionButton? _category;
    private OptionButton? _position;
    private SpinBox? _itemLevel;
    private LineEdit? _search;
    private RichTextLabel? _results;

    public override void _Ready()
    {
        Name = "词缀库";
        var controls = new HFlowContainer();
        AddChild(controls);
        _category = Options(controls, "装备类别", ["全部", .. Enum.GetNames<ItemCategory>()]);
        _position = Options(controls, "位置", ["全部", "前缀", "后缀"]);
        controls.AddChild(new Label { Text = "物品等级" });
        _itemLevel = new SpinBox { MinValue = 1, MaxValue = 120, Value = 120, Step = 1, CustomMinimumSize = new Vector2(80, 0) };
        controls.AddChild(_itemLevel);
        _search = new LineEdit
        {
            PlaceholderText = "搜索名称 / 标签 / 来源 / Stable ID",
            CustomMinimumSize = new Vector2(260, 0),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        controls.AddChild(_search);
        AddChild(new Label
        {
            Text = "公开基础自然词缀的全部档位、数值、门槛、权重与标签；结果与装备生成共用同一目录。",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        _results = new RichTextLabel
        {
            BbcodeEnabled = true,
            ScrollActive = true,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        AddChild(_results);
        _category.ItemSelected += _ => Refresh();
        _position.ItemSelected += _ => Refresh();
        _itemLevel.ValueChanged += _ => Refresh();
        _search.TextChanged += _ => Refresh();
        Refresh();
    }

    public void Refresh()
    {
        if (_results is null || _category is null || _position is null || _itemLevel is null || _search is null) return;
        int level = (int)_itemLevel.Value;
        ItemCategory? category = _category.Selected <= 0 ? null : (ItemCategory)(_category.Selected - 1);
        AffixPosition? position = _position.Selected switch { 1 => AffixPosition.Prefix, 2 => AffixPosition.Suffix, _ => null };
        string query = _search.Text.Trim();
        AffixDefinition[] rows = P1Affixes.All
            .Where(affix => affix.SourceId.Length > 0)
            .Where(affix => affix.MinimumItemLevel <= level)
            .Where(affix => category is null || affix.ApplicableCategories?.Contains(category.Value) == true)
            .Where(affix => position is null || affix.Position == position)
            .Where(affix => query.Length == 0 ||
                $"{affix.DisplayName} {affix.StableFamilyId} {affix.SourceId} {affix.Source} {string.Join(' ', affix.ModTags ?? [])}"
                    .Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(affix => affix.DisplayName, StringComparer.Ordinal)
            .ThenBy(affix => affix.Tier)
            .Take(600)
            .ToArray();
        var lines = new List<string>
        {
            $"[color=#d5c79a]当前显示 {rows.Length} 个档位 / 目录总计 {P1Affixes.All.Count(affix => affix.SourceId.Length > 0)}[/color]"
        };
        foreach (AffixDefinition affix in rows)
        {
            string side = affix.Position == AffixPosition.Prefix ? "前" : "后";
            string tags = string.Join(",", affix.ModTags ?? []);
            lines.Add($"[color=#bfcbd7]{Escape(affix.DisplayName)} T{affix.Tier}[/color] · {side}缀 · " +
                $"{Escape(ComponentRanges(affix))} · ilvl {affix.MinimumItemLevel} · 权重 {affix.Weight} · " +
                $"组 {Escape(affix.GroupId)} · 标签 {Escape(tags)} · {Escape(affix.SourceId)}");
        }
        _results.Text = string.Join('\n', lines);
    }

    private static OptionButton Options(Container parent, string label, IEnumerable<string> values)
    {
        parent.AddChild(new Label { Text = label });
        var option = new OptionButton();
        foreach (string value in values) option.AddItem(value);
        parent.AddChild(option);
        return option;
    }

    private static string Escape(string text) => text.Replace("[", "[​", StringComparison.Ordinal);

    private static string ComponentRanges(AffixDefinition affix) => string.Join("；", affix.EffectComponents.Select(component =>
        $"{component.Kind} {component.MinimumValue}–{component.MaximumValue} [{component.Scope}]"));
}
