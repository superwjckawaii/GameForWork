using GameForWork.Core.Campaign;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Resources;
using Godot;

namespace GameForWork.GodotClient;

public partial class LootFilterPanel : VBoxContainer
{
    private Func<GameSession>? _session;
    private Action<string>? _changed;
    private VBoxContainer? _rows;
    private LootFilterEditorWindow? _editor;
    private OptionButton? _rarityMode;
    private OptionButton? _rarityMinimum;
    private OptionButton? _rarityMaximum;
    private OptionButton? _category;
    private OptionButton? _base;
    private SpinBox? _minimumItemLevel;
    private SpinBox? _maximumItemLevel;
    private SpinBox? _minimumLinks;
    private SpinBox? _maximumLinks;
    private OptionButton? _affixFamily;
    private OptionButton? _minimumAffixTier;
    private OptionButton? _baseTier;
    private CheckBox? _gameplayBiased;
    private CheckBox? _schemeNeed;
    private OptionButton? _disposition;
    private VBoxContainer? _advancedEditor;
    private Label? _editorSummary;
    private readonly List<string> _affixFamilyIds = [];
    private readonly Dictionary<string, string> _affixFamilyNames = new(StringComparer.Ordinal);
    private int _editingIndex = -1;
    private string _lastSignature = string.Empty;

    public void Initialize(Func<GameSession> session, Func<ItemInstance?> selectedItem, Action<string> changed)
    {
        _session = session;
        _changed = changed;
        AddChild(new Label
        {
            Text = "规则从上到下执行，物品命中第一条后停止；未匹配物品默认保留。锁定和关键物品始终受保护。",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        var add = new Button { Text = "新增规则", CustomMinimumSize = new Vector2(132, 34), SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        add.Pressed += () => OpenEditor(-1);
        AddChild(add);
        _rows = new VBoxContainer();
        AddChild(_rows);
        BuildEditor();
    }

    public void RefreshRules()
    {
        if (_session is null) return;
        RemoveDeprecatedRuleConditions();
        string signature = Signature(_session().World.Filter.Rules);
        if (signature != _lastSignature) Rebuild();
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data) =>
        data.VariantType == Variant.Type.String && data.AsString().StartsWith("management-filter|", StringComparison.Ordinal);

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
        _editor = new LootFilterEditorWindow { Theme = Theme };
        VBoxContainer windowContent = _editor.Build();
        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scroll.AddChild(body);
        windowContent.AddChild(scroll);

        body.AddChild(new Label { Text = "处理结果" });
        _disposition = AddOptions(body, string.Empty, ["保留", "出售", "分解", "忽略"]);
        body.AddChild(new Label { Text = "快速模板" });
        var presets = new HFlowContainer();
        body.AddChild(presets);
        AddPresetButton(presets, "保留做装底材", 0);
        AddPresetButton(presets, "保留高T词缀", 1);
        AddPresetButton(presets, "处理低稀有度", 2);
        AddPresetButton(presets, "自定义", 3);

        body.AddChild(new Label { Text = "常用条件（同一条规则中的条件必须全部满足）" });
        var common = new HFlowContainer();
        body.AddChild(common);
        _category = AddOptions(common, "装备类别", ["任意", .. Enum.GetValues<ItemCategory>().Select(CategoryName)]);
        _baseTier = AddOptions(common, "底材阶级", ["任意", "普通", "进阶", "高阶", "巅峰"]);
        _rarityMode = AddOptions(common, "稀有度", ["任意", "等于", "至少", "至多", "区间"]);
        var rarityRow = new HBoxContainer();
        body.AddChild(rarityRow);
        _rarityMinimum = AddOptions(rarityRow, "下限/等于", ["基础", "魔法", "稀有", "传奇"]);
        _rarityMaximum = AddOptions(rarityRow, "上限", ["基础", "魔法", "稀有", "传奇"]);
        _rarityMaximum.Select((int)ItemRarity.Legendary);
        _base = AddOptions(body, "指定底材", ["任意", .. ItemBases.All.OrderBy(item => item.DisplayName).Select(item => item.DisplayName)]);
        var advancedToggle = new Button { Text = "＋ 更多条件", ToggleMode = true, SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        body.AddChild(advancedToggle);
        _advancedEditor = new VBoxContainer { Visible = false };
        body.AddChild(_advancedEditor);
        advancedToggle.Toggled += expanded =>
        {
            _advancedEditor.Visible = expanded;
            advancedToggle.Text = expanded ? "－ 收起更多条件" : "＋ 更多条件";
        };

        _gameplayBiased = new CheckBox { Text = "仅匹配当前玩法偏向底材" }; _advancedEditor.AddChild(_gameplayBiased);
        var itemLevelRow = new HBoxContainer();
        _advancedEditor.AddChild(itemLevelRow);
        _minimumItemLevel = AddSpin(itemLevelRow, "最低物品等级（0=任意）", 0, 120);
        _maximumItemLevel = AddSpin(itemLevelRow, "最高物品等级（0=任意）", 0, 120);
        var linkRow = new HBoxContainer();
        _advancedEditor.AddChild(linkRow);
        _minimumLinks = AddSpin(linkRow, "最低连接数", 0, 6);
        _maximumLinks = AddSpin(linkRow, "最高连接数（0=任意）", 0, 6);
        _affixFamily = AddOptions(_advancedEditor, "指定词缀", ["任意词缀"]);
        var usedLabels = new HashSet<string>(StringComparer.Ordinal);
        foreach (IGrouping<string, AffixDefinition> family in Affixes.All.Where(affix => affix.SourceId.Length > 0)
                     .GroupBy(affix => affix.StableFamilyId)
                     .OrderBy(group => AffixFamilyLabel(group), StringComparer.Ordinal))
        {
            string label = AffixFamilyLabel(family);
            if (!usedLabels.Add(label))
            {
                label = $"{label} · {AffixPositionName(family.First().Position)}";
                if (!usedLabels.Add(label))
                {
                    label = $"{label} · {SourceName(family.First().Source)}";
                    string baseLabel = label;
                    int variant = 2;
                    while (!usedLabels.Add(label)) label = $"{baseLabel} · 变体 {variant++}";
                }
            }
            _affixFamily.AddItem(label);
            _affixFamilyIds.Add(family.Key);
            _affixFamilyNames[family.Key] = label;
        }
        _minimumAffixTier = AddOptions(_advancedEditor, "词缀最低 T 级（所选 T 或更好）",
            ["任意 T 级", .. Enumerable.Range(1, 20).Select(tier => $"T{tier} 或更好")]);
        _schemeNeed = new CheckBox { Text = "满足当前技能方案的连接缺口" };
        _advancedEditor.AddChild(_schemeNeed);

        _editorSummary = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        body.AddChild(_editorSummary);
        var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        var cancel = new Button { Text = "取消" }; cancel.Pressed += _editor.Hide; actions.AddChild(cancel);
        var save = new Button { Text = "保存规则" }; save.Pressed += SaveEditor; actions.AddChild(save);
        windowContent.AddChild(actions);
        AddChild(_editor);

        foreach (OptionButton option in new[] { _disposition, _rarityMode, _rarityMinimum, _rarityMaximum, _category, _base, _baseTier, _affixFamily, _minimumAffixTier })
            option.ItemSelected += _ => RefreshEditorSummary();
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
            var row = new LootFilterRuleRow { SourceIndex = index, CustomMinimumSize = new Vector2(0, 36) };
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
            var menu = new MenuButton { Text = "…", TooltipText = "规则操作", CustomMinimumSize = new Vector2(38, 0) };
            PopupMenu popup = menu.GetPopup();
            popup.AddItem("上移", 0);
            popup.AddItem("下移", 1);
            popup.AddSeparator();
            popup.AddItem("删除", 2);
            popup.IdPressed += id =>
            {
                if (id == 0) Move(captured, captured - 1);
                else if (id == 1) Move(captured, captured + 1);
                else Delete(captured);
            };
            row.AddChild(menu);
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
        _editor.SetPixelTitle(rule is null ? "新增过滤规则" : "编辑过滤规则");
        _editor.OpenCentered();
    }

    private void LoadEditor(LootFilterRule? rule)
    {
        _rarityMode!.Select(rule?.Rarity is not null ? 1 : rule?.MinimumRarity is not null && rule.MaximumRarity is not null ? 4 : rule?.MinimumRarity is not null ? 2 : rule?.MaximumRarity is not null ? 3 : 0);
        _rarityMinimum!.Select((int)(rule?.Rarity ?? rule?.MinimumRarity ?? ItemRarity.Basic));
        _rarityMaximum!.Select((int)(rule?.Rarity ?? rule?.MaximumRarity ?? ItemRarity.Legendary));
        SelectEnum(_category!, rule?.Category);
        string? baseId = rule?.BaseStableId;
        ItemBases.All.OrderBy(item => item.DisplayName).Select((item, index) => (item, index))
            .Where(entry => entry.item.StableId == baseId).ToList().ForEach(entry => _base!.Select(entry.index + 1));
        if (baseId is null) _base!.Select(0);
        _baseTier!.Select(rule?.BaseTier is null ? 0 : (int)rule.BaseTier.Value);
        _gameplayBiased!.ButtonPressed = rule?.RequireGameplayBiasedBase ?? false;
        _minimumItemLevel!.Value = rule?.MinimumItemLevel ?? 0;
        _maximumItemLevel!.Value = rule?.MaximumItemLevel ?? 0;
        _minimumLinks!.Value = rule?.MinimumLinkedSockets ?? 0;
        _maximumLinks!.Value = rule?.MaximumLinkedSockets ?? 0;
        SelectString(_affixFamily!, _affixFamilyIds, rule?.AffixFamilyId);
        _minimumAffixTier!.Select(Math.Clamp(rule?.MaximumAffixTier ?? 0, 0, 20));
        _schemeNeed!.ButtonPressed = rule?.RequireCurrentSchemeNeed ?? false;
        _disposition!.Select((int)(rule?.Disposition ?? LootDisposition.Keep));
        RefreshEditorSummary();
    }

    private void SaveEditor()
    {
        List<LootFilterRule> rules = _session!().World.Filter.Rules.ToList();
        LootFilterRule? previous = _editingIndex >= 0 && _editingIndex < rules.Count ? rules[_editingIndex] : null;
        int rarityMode = _rarityMode!.Selected;
        ItemRarity low = (ItemRarity)_rarityMinimum!.Selected;
        ItemRarity high = (ItemRarity)_rarityMaximum!.Selected;
        if (low > high) (low, high) = (high, low);
        ItemBaseDefinition? itemBase = _base!.Selected <= 0 ? null : ItemBases.All.OrderBy(item => item.DisplayName).ElementAt(_base.Selected - 1);
        string? affix = SelectedString(_affixFamily!, _affixFamilyIds);
        var rule = new LootFilterRule(
            previous?.StableId ?? $"user.filter.{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            (LootDisposition)_disposition!.Selected,
            Rarity: rarityMode == 1 ? low : null,
            BaseStableId: itemBase?.StableId,
            AffixFamilyId: affix,
            Enabled: previous?.Enabled ?? true,
            MinimumLinkedSockets: (int)_minimumLinks!.Value,
            RequireCurrentSchemeNeed: _schemeNeed!.ButtonPressed,
            MinimumRarity: rarityMode is 2 or 4 ? low : null,
            MaximumRarity: rarityMode is 3 or 4 ? high : null,
            Category: EnumValue<ItemCategory>(_category!),
            MinimumItemLevel: _minimumItemLevel!.Value <= 0 ? null : (int)_minimumItemLevel.Value,
            MaximumItemLevel: _maximumItemLevel!.Value <= 0 ? null : (int)_maximumItemLevel.Value,
            MaximumLinkedSockets: _maximumLinks!.Value <= 0 ? null : (int)_maximumLinks.Value,
            MaximumAffixTier: affix is null || _minimumAffixTier!.Selected <= 0 ? null : _minimumAffixTier.Selected,
            BaseTier: _baseTier!.Selected <= 0 ? null : (BaseTier)_baseTier.Selected,
            RequireGameplayBiasedBase: _gameplayBiased!.ButtonPressed);
        if (_editingIndex >= 0 && _editingIndex < rules.Count) rules[_editingIndex] = rule;
        else rules.Add(rule);
        Replace(rules, previous is null ? "过滤规则已新增。" : "过滤规则已更新。");
        _editor!.Hide();
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

    private void RemoveDeprecatedRuleConditions()
    {
        LootFilterRule[] source = _session!().World.Filter.Rules.ToArray();
        if (!source.Any(rule => rule.Slot is not null || rule.DropSource is not null ||
                rule.MinimumEstimatedValue is not null || rule.MaximumEstimatedValue is not null ||
                rule.BaseTag is not null || rule.MinimumAffixValue is not null || rule.MinimumAffixTier is not null ||
                rule.MaximumAffixTier is not null && rule.AffixFamilyId is null)) return;
        _session().World.Filter.ReplaceRules(source.Select(rule => rule with
        {
            Slot = null,
            DropSource = null,
            MinimumEstimatedValue = null,
            MaximumEstimatedValue = null,
            BaseTag = null,
            MinimumAffixValue = null,
            MinimumAffixTier = null,
            MaximumAffixTier = rule.AffixFamilyId is null ? null : rule.MaximumAffixTier,
        }));
    }

    private string Describe(LootFilterRule rule)
    {
        var conditions = new List<string>();
        if (rule.Rarity is not null) conditions.Add($"稀有度={RarityName(rule.Rarity.Value)}");
        if (rule.MinimumRarity is not null) conditions.Add($"稀有度≥{RarityName(rule.MinimumRarity.Value)}");
        if (rule.MaximumRarity is not null) conditions.Add($"稀有度≤{RarityName(rule.MaximumRarity.Value)}");
        if (rule.Category is not null) conditions.Add($"类别={CategoryName(rule.Category.Value)}");
        if (rule.BaseStableId is not null) conditions.Add($"底材={BaseName(rule.BaseStableId)}");
        if (rule.MinimumItemLevel is not null) conditions.Add($"物等≥{rule.MinimumItemLevel}");
        if (rule.MaximumItemLevel is not null) conditions.Add($"物等≤{rule.MaximumItemLevel}");
        if (rule.MinimumLinkedSockets > 0) conditions.Add($"连接≥{rule.MinimumLinkedSockets}");
        if (rule.MaximumLinkedSockets is not null) conditions.Add($"连接≤{rule.MaximumLinkedSockets}");
        if (rule.AffixFamilyId is not null) conditions.Add(AffixRuleName(rule.AffixFamilyId, rule.MaximumAffixTier));
        if (rule.BaseTier is not null) conditions.Add($"底材阶级={DropCatalog.BaseTierName(rule.BaseTier.Value)}");
        if (rule.RequireGameplayBiasedBase) conditions.Add("玩法偏向底材");
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

    private string AffixRuleName(string stableFamilyId, int? maximumTier)
    {
        string name = _affixFamilyNames.GetValueOrDefault(stableFamilyId, stableFamilyId);
        return maximumTier is null ? $"词缀={name}" : $"词缀={name}，T{maximumTier} 或更好";
    }

    private static string BaseName(string stableId) =>
        ItemBases.All.FirstOrDefault(item => item.StableId == stableId)?.DisplayName ?? stableId;

    private static string AffixFamilyLabel(IEnumerable<AffixDefinition> family)
    {
        AffixDefinition definition = family.OrderBy(affix => affix.Tier).First();
        IReadOnlyList<AffixModifierComponent> components = definition.EffectComponents;
        string effect = IsAddedDamageRange(components)
            ? definition.DisplayName
            : string.Join(" + ", components.Select(component => UiText.ModifierName(component.Kind)).Distinct(StringComparer.Ordinal));
        if (string.IsNullOrWhiteSpace(effect)) effect = definition.DisplayName;
        string context = AffixContext(definition);
        return context.Length == 0 ? effect : $"{effect}（{context}）";
    }

    private static bool IsAddedDamageRange(IReadOnlyList<AffixModifierComponent> components)
    {
        if (components.Count != 2) return false;
        return DamageChannel(components[0].Kind) is { } left && left == DamageChannel(components[1].Kind);
    }

    private static string? DamageChannel(ItemModifierKind kind) => kind switch
    {
        ItemModifierKind.AddedMinimumPhysicalDamage or ItemModifierKind.AddedMaximumPhysicalDamage => "physical",
        ItemModifierKind.AddedMinimumFireDamage or ItemModifierKind.AddedMaximumFireDamage => "fire",
        ItemModifierKind.AddedMinimumColdDamage or ItemModifierKind.AddedMaximumColdDamage => "cold",
        ItemModifierKind.AddedMinimumLightningDamage or ItemModifierKind.AddedMaximumLightningDamage => "lightning",
        ItemModifierKind.AddedMinimumVoidDamage or ItemModifierKind.AddedMaximumVoidDamage => "void",
        _ => null,
    };

    private static string AffixContext(AffixDefinition definition)
    {
        string[] tags = (definition.TagWeights ?? new Dictionary<string, int>())
            .Where(pair => pair.Value > 0).Select(pair => pair.Key).ToArray();
        string? tag = new[] { "one_hand_weapon", "two_hand_weapon", "weapon", "str_armour", "dex_armour", "int_armour", "shield", "ring", "amulet", "belt", "flask" }
            .FirstOrDefault(candidate => tags.Contains(candidate, StringComparer.Ordinal));
        if (tag is not null) return TagName(tag);
        ItemCategory[] categories = definition.ApplicableCategories?.Distinct().ToArray() ?? [definition.Category];
        if (categories.All(category => category is ItemCategory.Ring or ItemCategory.Amulet or ItemCategory.Belt)) return "首饰";
        if (categories.All(category => category is ItemCategory.OneHandWeapon or ItemCategory.TwoHandWeapon)) return "武器";
        return categories.Length <= 2 ? string.Join("/", categories.Select(CategoryName)) : "多部位";
    }

    private static string AffixPositionName(AffixPosition position) => position == AffixPosition.Prefix ? "前缀" : "后缀";

    private static string SourceName(string source) => source switch
    {
        "Builds" => "通用",
        "ArchetypesSpecial" => "特殊",
        "Natural" => "底材库",
        _ => source,
    };

    private void AddPresetButton(Container parent, string text, int preset)
    {
        var button = new Button { Text = text };
        button.Pressed += () => ApplyPreset(preset);
        parent.AddChild(button);
    }

    private void ApplyPreset(int preset)
    {
        _rarityMode!.Select(0);
        _category!.Select(0);
        _baseTier!.Select(0);
        _base!.Select(0);
        _minimumAffixTier!.Select(0);
        switch (preset)
        {
            case 0:
                _disposition!.Select((int)LootDisposition.Keep);
                _baseTier.Select((int)BaseTier.Pinnacle);
                break;
            case 1:
                _disposition!.Select((int)LootDisposition.Keep);
                _minimumAffixTier.Select(2);
                _advancedEditor!.Visible = true;
                break;
            case 2:
                _disposition!.Select((int)LootDisposition.Sell);
                _rarityMode.Select(3);
                _rarityMaximum!.Select((int)ItemRarity.Magic);
                break;
            default:
                break;
        }
        RefreshEditorSummary();
    }

    private void RefreshEditorSummary()
    {
        if (_editorSummary is null || _disposition is null) return;
        OptionButton rarityMode = _rarityMode!;
        OptionButton rarityMinimum = _rarityMinimum!;
        OptionButton rarityMaximum = _rarityMaximum!;
        OptionButton affixFamily = _affixFamily!;
        OptionButton minimumAffixTier = _minimumAffixTier!;
        var preview = new LootFilterRule("preview", (LootDisposition)_disposition.Selected,
            Rarity: rarityMode.Selected == 1 ? (ItemRarity)rarityMinimum.Selected : null,
            MinimumRarity: rarityMode.Selected is 2 or 4 ? (ItemRarity)rarityMinimum.Selected : null,
            MaximumRarity: rarityMode.Selected is 3 or 4 ? (ItemRarity)rarityMaximum.Selected : null,
            Category: EnumValue<ItemCategory>(_category!),
            BaseStableId: _base!.Selected <= 0 ? null : ItemBases.All.OrderBy(item => item.DisplayName).ElementAt(_base.Selected - 1).StableId,
            BaseTier: _baseTier!.Selected <= 0 ? null : (BaseTier)_baseTier.Selected,
            AffixFamilyId: SelectedString(affixFamily, _affixFamilyIds),
            MaximumAffixTier: affixFamily.Selected <= 0 || minimumAffixTier.Selected <= 0 ? null : minimumAffixTier.Selected);
        _editorSummary.Text = $"规则预览：{Describe(preview)}";
    }

    private static string? SelectedString(OptionButton option, IReadOnlyList<string> values) =>
        option.Selected <= 0 || option.Selected - 1 >= values.Count ? null : values[option.Selected - 1];

    private static void SelectString(OptionButton option, IReadOnlyList<string> values, string? value)
    {
        int index = -1;
        if (value is not null)
        {
            for (int candidate = 0; candidate < values.Count; candidate++)
            {
                if (string.Equals(values[candidate], value, StringComparison.Ordinal)) { index = candidate; break; }
            }
        }
        option.Select(index < 0 ? 0 : index + 1);
    }

    private static string TagName(string tag) => tag switch
    {
        "one_hand_weapon" => "单手武器",
        "two_hand_weapon" => "双手武器",
        "weapon" => "武器",
        "str_armour" => "体魄护甲",
        "dex_armour" => "灵巧护甲",
        "int_armour" => "精神护甲",
        "flask" => "药剂",
        "sword" => "剑",
        "axe" => "斧",
        "mace" => "锤",
        "dagger" => "匕首",
        "bow" => "弓",
        "wand" => "法杖",
        "staff" => "长杖",
        "shield" => "盾牌",
        "ring" => "戒指",
        "amulet" => "项链",
        "belt" => "腰带",
        "top_tier_base_item_type" => "巅峰底材标记",
        _ => tag.Replace('_', ' '),
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

    private static T? EnumValue<T>(OptionButton option) where T : struct, Enum => option.Selected <= 0
        ? null
        : (T)Enum.ToObject(typeof(T), option.Selected - 1);
    private static void SelectEnum<T>(OptionButton option, T? value) where T : struct, Enum => option.Select(value is null ? 0 : Convert.ToInt32(value.Value) + 1);
}

public partial class LootFilterEditorWindow : IndependentWindow
{
    private Label? _pixelTitle;

    public VBoxContainer Build()
    {
        VBoxContainer content = InitializePixelWindow("新增过滤规则", new Vector2I(720, 620), new Vector2I(640, 480));
        _pixelTitle = FindChild("PixelWindowTitle", recursive: true, owned: false) as Label;
        return content;
    }

    public void SetPixelTitle(string title)
    {
        Title = title;
        if (_pixelTitle is not null) _pixelTitle.Text = $"◆ {title}";
    }
}

public partial class LootFilterRuleRow : HBoxContainer
{
    public int SourceIndex { get; set; }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        var preview = new Label { Text = $"规则 {SourceIndex + 1}" };
        SetDragPreview(preview);
        return Variant.From($"management-filter|{SourceIndex}");
    }
}
