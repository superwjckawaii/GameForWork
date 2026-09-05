using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Management;
using Godot;

namespace GameForWork.GodotClient;

public partial class EquipmentPaperDoll : Control
{
    private readonly Dictionary<EquipmentSlot, ItemGrid> _slots = [];
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
            foreach (ItemGrid grid in _slots.Values)
            {
                grid.ExtraTooltip = value;
            }
        }
    }

    public Func<ItemContainerKind, int, int, bool>? DropValidator { get; set; }

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(272, 248);
        MouseFilter = MouseFilterEnum.Stop;
        IReadOnlyDictionary<EquipmentSlot, Vector2> positions = new Dictionary<EquipmentSlot, Vector2>
        {
            [EquipmentSlot.Helmet] = new(115, 0),
            [EquipmentSlot.MainHand] = new(8, 55),
            [EquipmentSlot.Chest] = new(115, 48),
            [EquipmentSlot.OffHand] = new(230, 24),
            [EquipmentSlot.Gloves] = new(20, 112),
            [EquipmentSlot.Belt] = new(115, 96),
            [EquipmentSlot.Amulet] = new(230, 65),
            [EquipmentSlot.RingLeft] = new(230, 106),
            [EquipmentSlot.RingRight] = new(230, 147),
            [EquipmentSlot.Boots] = new(115, 158),
            [EquipmentSlot.Flask1] = new(18, 208),
            [EquipmentSlot.Flask2] = new(58, 208),
            [EquipmentSlot.Flask3] = new(98, 208),
            [EquipmentSlot.Flask4] = new(138, 208),
            [EquipmentSlot.Flask5] = new(178, 208),
        };
        foreach ((EquipmentSlot slot, Vector2 position) in positions)
        {
            var grid = new ItemGrid
            {
                ContainerKind = ItemContainerKind.Equipped,
                IndexOffset = (int)slot,
                Position = position,
                ExtraTooltip = _extraTooltip,
                EmptyLabel = ShortSlotName(slot),
            };
            int cellSize = slot is >= EquipmentSlot.Flask1 and <= EquipmentSlot.Flask5 ? 36 :
                slot is EquipmentSlot.OffHand or EquipmentSlot.Amulet or EquipmentSlot.RingLeft or EquipmentSlot.RingRight ? 36 : 42;
            grid.Configure(1, 1, cellSize);
            grid.DropValidator = (source, sourceIndex, targetIndex) =>
                DropValidator?.Invoke(source, sourceIndex, targetIndex) ?? true;
            grid.ItemSelected += index =>
            {
                foreach (ItemGrid other in _slots.Values.Where(other => other != grid)) other.ClearSelection();
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
        DrawCircle(new Vector2(136, 38), 16, silhouette);
        DrawRect(new Rect2(106, 55, 60, 78), silhouette, true);
        DrawLine(new Vector2(112, 72), new Vector2(77, 151), silhouette, 18);
        DrawLine(new Vector2(160, 72), new Vector2(195, 151), silhouette, 18);
        DrawLine(new Vector2(121, 130), new Vector2(108, 200), silhouette, 20);
        DrawLine(new Vector2(151, 130), new Vector2(164, 200), silhouette, 20);
        DrawString(ThemeDB.FallbackFont, new Vector2(100, 203), "药剂腰带", HorizontalAlignment.Center, 72, 11,
            new Color("91836c"));
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("665b4c"), false, 2);
    }

    public void SetSlots(IReadOnlyList<ItemInstance?> items)
    {
        foreach ((EquipmentSlot slot, ItemGrid grid) in _slots)
        {
            grid.SetSlots([items.Count > (int)slot ? items[(int)slot] : null]);
        }
    }

    public override void _GuiInput(InputEvent inputEvent)
    {
        if (inputEvent is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }) return;
        foreach (ItemGrid grid in _slots.Values) grid.ClearSelection();
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
