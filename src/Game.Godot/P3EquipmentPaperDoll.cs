using GameForWork.Core.P1.Items;
using GameForWork.Core.P2;
using Godot;

namespace GameForWork.GodotClient;

public partial class P3EquipmentPaperDoll : Control
{
    private readonly Dictionary<EquipmentSlot, P1ItemGrid> _slots = [];
    private Func<ItemInstance, string>? _extraTooltip;

    public event Action<int>? ItemActivated;
    public event Action<int>? ItemSelected;
    public event Action<int, Vector2>? ItemContextRequested;
    public event Action<ItemContainerKind, int, int>? ItemDropped;
    public event Action<int>? QuickTransferRequested;

    public Func<ItemInstance, string>? ExtraTooltip
    {
        get => _extraTooltip;
        set
        {
            _extraTooltip = value;
            foreach (P1ItemGrid grid in _slots.Values)
            {
                grid.ExtraTooltip = value;
            }
        }
    }

    public Func<ItemContainerKind, int, int, bool>? DropValidator { get; set; }

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(272, 364);
        MouseFilter = MouseFilterEnum.Stop;
        IReadOnlyDictionary<EquipmentSlot, Vector2> positions = new Dictionary<EquipmentSlot, Vector2>
        {
            [EquipmentSlot.Helmet] = new(113, 4),
            [EquipmentSlot.Amulet] = new(218, 148),
            [EquipmentSlot.MainHand] = new(6, 92),
            [EquipmentSlot.Chest] = new(113, 80),
            [EquipmentSlot.OffHand] = new(218, 92),
            [EquipmentSlot.Gloves] = new(18, 170),
            [EquipmentSlot.Belt] = new(113, 162),
            [EquipmentSlot.RingLeft] = new(218, 204),
            [EquipmentSlot.RingRight] = new(218, 258),
            [EquipmentSlot.Boots] = new(113, 258),
            [EquipmentSlot.Flask1] = new(25, 322),
            [EquipmentSlot.Flask2] = new(67, 322),
            [EquipmentSlot.Flask3] = new(109, 322),
            [EquipmentSlot.Flask4] = new(151, 322),
            [EquipmentSlot.Flask5] = new(193, 322),
        };
        foreach ((EquipmentSlot slot, Vector2 position) in positions)
        {
            var grid = new P1ItemGrid
            {
                ContainerKind = ItemContainerKind.Equipped,
                IndexOffset = (int)slot,
                Position = position,
                ExtraTooltip = _extraTooltip,
                EmptyLabel = ShortSlotName(slot),
            };
            grid.Configure(1, 1, slot is >= EquipmentSlot.Flask1 and <= EquipmentSlot.Flask5 ? 38 : 46);
            grid.DropValidator = (source, sourceIndex, targetIndex) =>
                DropValidator?.Invoke(source, sourceIndex, targetIndex) ?? true;
            grid.ItemSelected += index =>
            {
                foreach (P1ItemGrid other in _slots.Values.Where(other => other != grid)) other.ClearSelection();
                ItemSelected?.Invoke(index);
            };
            grid.ItemActivated += index => ItemActivated?.Invoke(index);
            grid.ItemContextRequested += (index, screen) => ItemContextRequested?.Invoke(index, screen);
            grid.ItemDropped += (source, sourceIndex, targetIndex) => ItemDropped?.Invoke(source, sourceIndex, targetIndex);
            grid.QuickTransferRequested += index => QuickTransferRequested?.Invoke(index);
            grid.TooltipText = SlotName(slot);
            AddChild(grid);
            _slots[slot] = grid;
        }
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("12161d"), true);
        Color silhouette = new("29313d");
        DrawCircle(new Vector2(136, 54), 20, silhouette);
        DrawRect(new Rect2(101, 76, 70, 126), silhouette, true);
        DrawLine(new Vector2(110, 98), new Vector2(72, 208), silhouette, 22);
        DrawLine(new Vector2(162, 98), new Vector2(200, 208), silhouette, 22);
        DrawLine(new Vector2(118, 198), new Vector2(101, 304), silhouette, 24);
        DrawLine(new Vector2(154, 198), new Vector2(171, 304), silhouette, 24);
        DrawString(ThemeDB.FallbackFont, new Vector2(100, 316), "药剂腰带", HorizontalAlignment.Center, 72, 11,
            new Color("91836c"));
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("665b4c"), false, 2);
    }

    public void SetSlots(IReadOnlyList<ItemInstance?> items)
    {
        foreach ((EquipmentSlot slot, P1ItemGrid grid) in _slots)
        {
            grid.SetSlots([items.Count > (int)slot ? items[(int)slot] : null]);
        }
    }

    public override void _GuiInput(InputEvent inputEvent)
    {
        if (inputEvent is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }) return;
        foreach (P1ItemGrid grid in _slots.Values) grid.ClearSelection();
        ItemSelected?.Invoke(-1);
    }

    private static string SlotName(EquipmentSlot slot) => slot switch
    {
        EquipmentSlot.MainHand => "主手",
        EquipmentSlot.OffHand => "副手",
        EquipmentSlot.Chest => "胸甲",
        EquipmentSlot.Helmet => "头盔",
        EquipmentSlot.Gloves => "手套",
        EquipmentSlot.Boots => "鞋子",
        EquipmentSlot.Belt => "腰带",
        EquipmentSlot.Amulet => "项链",
        EquipmentSlot.RingLeft => "左戒指",
        EquipmentSlot.RingRight => "右戒指",
        _ => "药剂",
    };

    private static string ShortSlotName(EquipmentSlot slot) => slot switch
    {
        EquipmentSlot.MainHand => "主手",
        EquipmentSlot.OffHand => "副手",
        EquipmentSlot.Chest => "胸",
        EquipmentSlot.Helmet => "头",
        EquipmentSlot.Gloves => "手",
        EquipmentSlot.Boots => "靴",
        EquipmentSlot.Belt => "腰",
        EquipmentSlot.Amulet => "链",
        EquipmentSlot.RingLeft or EquipmentSlot.RingRight => "戒",
        _ => "药",
    };
}
