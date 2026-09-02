using GameForWork.Core.P1.Items;
using GameForWork.Core.P19;
using GameForWork.Core.P29;
using Godot;

namespace GameForWork.GodotClient;

public partial class P19AffixPanel : VBoxContainer
{
    private OptionButton? _category;
    private OptionButton? _base;
    private OptionButton? _position;
    private SpinBox? _itemLevel;
    private LineEdit? _search;
    private RichTextLabel? _results;
    private readonly List<string> _baseIds = [];

    public override void _Ready()
    {
        Name = "词缀库";
        var controls = new HFlowContainer();
        AddChild(controls);
        _category = Options(controls, "装备类别", ["全部", .. Enum.GetValues<ItemCategory>().Select(CategoryName)]);
        _base = Options(controls, "精确底材", ["任意底材"]);
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
        _category.ItemSelected += _ => { RebuildBases(); Refresh(); };
        _base.ItemSelected += _ => Refresh();
        _position.ItemSelected += _ => Refresh();
        _itemLevel.ValueChanged += _ => Refresh();
        _search.TextChanged += _ => Refresh();
        RebuildBases();
        Refresh();
    }

    public void Refresh()
    {
        if (_results is null || _category is null || _base is null || _position is null || _itemLevel is null || _search is null) return;
        int level = (int)_itemLevel.Value;
        ItemCategory? category = _category.Selected <= 0 ? null : (ItemCategory)(_category.Selected - 1);
        string? baseId = _base.Selected <= 0 ? null : _baseIds[_base.Selected - 1];
        AffixPosition? position = _position.Selected switch { 1 => AffixPosition.Prefix, 2 => AffixPosition.Suffix, _ => null };
        string query = _search.Text.Trim();
        IReadOnlyList<P19AffixView> rows = P19AffixBrowser.Query(new(level, category, baseId, position, query));
        var lines = new List<string>
        {
            $"[color=#d5c79a]当前显示 {rows.Count} 个真实档位 / 实时目录总计 {P1Affixes.All.Count(affix => affix.SourceId.Length > 0)}" +
            $"{(baseId is null ? string.Empty : $" · {Escape(P1ItemBases.Get(baseId).DisplayName)}专用 T 级与权重")}[/color]"
        };
        foreach (P19AffixView row in rows)
        {
            AffixDefinition affix = row.Definition;
            string side = affix.Position == AffixPosition.Prefix ? "前" : "后";
            string tags = string.Join(",", affix.ModTags ?? []);
            string color = P1UiText.AffixTierColor(row.Tier).ToHtml(false);
            lines.Add($"[color=#{color}]{Escape(affix.DisplayName)} T{row.Tier}[/color] · {side}缀 · " +
                $"{Escape(ComponentRanges(affix))} · 需求物等 {affix.MinimumItemLevel} · 权重 {row.Weight} · " +
                $"组 {Escape(affix.GroupId)} · 标签 {Escape(tags)} · 来源 {Escape(affix.Source)}");
        }
        _results.Text = string.Join('\n', lines);
    }

    private void RebuildBases()
    {
        if (_base is null || _category is null) return;
        ItemCategory? category = _category.Selected <= 0 ? null : (ItemCategory)(_category.Selected - 1);
        _base.Clear();
        _base.AddItem("任意底材");
        _baseIds.Clear();
        foreach (ItemBaseDefinition item in P1ItemBases.All
                     .Where(item => category is null || item.Category == category)
                     .OrderBy(item => item.DisplayName, StringComparer.Ordinal))
        {
            _base.AddItem($"{item.DisplayName} · {P29DropCatalog.BaseTierName(P29DropCatalog.BaseTier(item))}");
            _baseIds.Add(item.StableId);
        }
        _base.Select(0);
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
        $"{P1UiText.AffixComponentRange(component.Kind, component.MinimumValue, component.MaximumValue)} [{ScopeName(component.Scope)}]"));

    private static string ScopeName(ItemModifierScope scope) => scope switch
    {
        ItemModifierScope.LocalWeapon => "本件武器",
        ItemModifierScope.LocalDefense => "本件防具",
        ItemModifierScope.LocalBlock => "本件格挡",
        ItemModifierScope.Flask => "药剂",
        ItemModifierScope.Rule => "规则",
        _ => "全局",
    };

    private static string CategoryName(ItemCategory category) => category switch
    {
        ItemCategory.TwoHandWeapon => "双手武器",
        ItemCategory.OneHandWeapon => "单手武器",
        ItemCategory.Shield => "盾牌",
        ItemCategory.BodyArmor => "胸甲",
        ItemCategory.Helmet => "头盔",
        ItemCategory.Gloves => "手套",
        ItemCategory.Boots => "鞋子",
        ItemCategory.Belt => "腰带",
        ItemCategory.Amulet => "项链",
        ItemCategory.Ring => "戒指",
        ItemCategory.LifeFlask => "药剂",
        _ => category.ToString(),
    };
}
