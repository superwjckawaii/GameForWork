using GameForWork.Core.P1.Items;
using GameForWork.Core.P2;
using Godot;

namespace GameForWork.GodotClient;

public partial class P2CraftingWorkbench : PanelContainer
{
    private Action<ItemContainerKind, int>? _itemDropped;
    private TextureRect? _icon;
    private Label? _dropLabel;
    private RichTextLabel? _details;
    private Label? _result;
    private P21ArtAtlas? _art;

    public void Initialize(Action<ItemContainerKind, int> itemDropped)
    {
        _itemDropped = itemDropped;
        _art = new P21ArtAtlas();
        AddThemeStyleboxOverride("panel", Frame(new Color("111720"), new Color("786747"), 2));

        var body = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Pass,
        };
        body.AddThemeConstantOverride("separation", 8);
        AddChild(body);
        body.AddChild(new Label
        {
            Text = "工艺台",
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        });
        body.AddChild(new Label
        {
            Text = "将整理背包、仓库或已装备物品拖到这里；装备仍保留在原位置。",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        });

        var target = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0, 86),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        target.AddThemeStyleboxOverride("panel", Frame(new Color("171d27"), new Color("9b8252"), 1));
        body.AddChild(target);
        var targetRow = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        targetRow.AddThemeConstantOverride("separation", 12);
        target.AddChild(targetRow);
        _icon = new TextureRect
        {
            CustomMinimumSize = new Vector2(68, 68),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        targetRow.AddChild(_icon);
        _dropLabel = new Label
        {
            Text = "拖入装备",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        targetRow.AddChild(_dropLabel);

        _details = new RichTextLabel
        {
            BbcodeEnabled = true,
            ScrollActive = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 210),
            MouseFilter = MouseFilterEnum.Pass,
        };
        body.AddChild(_details);
        _result = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _result.AddThemeColorOverride("font_color", new Color("d5c79a"));
        body.AddChild(_result);
    }

    public void Refresh(EquipmentCraftTarget? target, string result)
    {
        if (_details is null || _dropLabel is null || _icon is null || _result is null) return;
        _result.Text = result;
        if (target is null)
        {
            _dropLabel.Text = "拖入装备以设为打造目标";
            _icon.Texture = null;
            _details.Text = "[color=#8f98a6]工艺台为空。[/color]";
            AddThemeStyleboxOverride("panel", Frame(new Color("111720"), new Color("786747"), 2));
            return;
        }

        ItemInstance item = target.Item;
        Color rarity = P1UiText.RarityColor(item.Rarity);
        _dropLabel.Text = $"{item.DisplayName}\n{ContainerName(target.Container)} · 物品等级 {item.ItemLevel}";
        _icon.Texture = _art?.ItemIcon(item);
        _details.Text = P2ItemTooltipPanel.ItemBbCode(item, includeAffixDetails: true);
        AddThemeStyleboxOverride("panel", Frame(new Color("111720"), rarity.Darkened(0.12f), 2));
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        bool valid = TryParse(data, out _, out _);
        SelfModulate = valid ? new Color("b7efbd") : new Color("ef9b91");
        return valid;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        SelfModulate = Colors.White;
        if (TryParse(data, out ItemContainerKind container, out int index))
            _itemDropped?.Invoke(container, index);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationDragEnd) SelfModulate = Colors.White;
    }

    private static bool TryParse(Variant data, out ItemContainerKind container, out int index)
    {
        container = default;
        index = -1;
        if (data.VariantType != Variant.Type.String) return false;
        string[] parts = data.AsString().Split('|');
        if (parts.Length != 3 || parts[0] != "p2-item" || !int.TryParse(parts[1], out int raw) ||
            !Enum.IsDefined(typeof(ItemContainerKind), raw) || !int.TryParse(parts[2], out index) || index < 0)
            return false;
        container = (ItemContainerKind)raw;
        return container is ItemContainerKind.Storage or ItemContainerKind.SortingBag or ItemContainerKind.Equipped;
    }

    private static string ContainerName(ItemContainerKind container) => container switch
    {
        ItemContainerKind.Storage => "仓库",
        ItemContainerKind.SortingBag => "整理背包",
        ItemContainerKind.Equipped => "已装备",
        _ => container.ToString(),
    };

    private static StyleBoxFlat Frame(Color background, Color border, int width) => new()
    {
        BgColor = background,
        BorderColor = border,
        BorderWidthLeft = width,
        BorderWidthTop = width,
        BorderWidthRight = width,
        BorderWidthBottom = width,
        CornerRadiusTopLeft = 3,
        CornerRadiusTopRight = 3,
        CornerRadiusBottomLeft = 3,
        CornerRadiusBottomRight = 3,
        ContentMarginLeft = 10,
        ContentMarginTop = 8,
        ContentMarginRight = 10,
        ContentMarginBottom = 8,
    };
}
