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
        CustomMinimumSize = new Vector2(284, 430);
        MouseFilter = MouseFilterEnum.Ignore;
        IReadOnlyDictionary<EquipmentSlot, Vector2> positions = new Dictionary<EquipmentSlot, Vector2>
        {
            [EquipmentSlot.Helmet] = new(117, 8),
            [EquipmentSlot.Amulet] = new(192, 66),
            [EquipmentSlot.MainHand] = new(8, 106),
            [EquipmentSlot.Chest] = new(117, 94),
            [EquipmentSlot.OffHand] = new(226, 106),
            [EquipmentSlot.Gloves] = new(24, 188),
            [EquipmentSlot.Belt] = new(117, 178),
            [EquipmentSlot.RingLeft] = new(65, 235),
            [EquipmentSlot.RingRight] = new(169, 235),
            [EquipmentSlot.Boots] = new(117, 286),
            [EquipmentSlot.Flask1] = new(14, 374),
            [EquipmentSlot.Flask2] = new(64, 374),
            [EquipmentSlot.Flask3] = new(114, 374),
            [EquipmentSlot.Flask4] = new(164, 374),
            [EquipmentSlot.Flask5] = new(214, 374),
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
            grid.Configure(1, 1, slot is >= EquipmentSlot.Flask1 and <= EquipmentSlot.Flask5 ? 42 : 50);
            grid.DropValidator = (source, sourceIndex, targetIndex) =>
                DropValidator?.Invoke(source, sourceIndex, targetIndex) ?? true;
            grid.ItemSelected += index => ItemSelected?.Invoke(index);
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
        DrawCircle(new Vector2(142, 60), 22, silhouette);
        DrawRect(new Rect2(105, 84, 74, 145), silhouette, true);
        DrawLine(new Vector2(114, 108), new Vector2(74, 224), silhouette, 24);
        DrawLine(new Vector2(170, 108), new Vector2(210, 224), silhouette, 24);
        DrawLine(new Vector2(124, 222), new Vector2(105, 344), silhouette, 26);
        DrawLine(new Vector2(160, 222), new Vector2(179, 344), silhouette, 26);
        DrawString(ThemeDB.FallbackFont, new Vector2(106, 366), "药剂腰带", HorizontalAlignment.Center, 72, 11,
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
