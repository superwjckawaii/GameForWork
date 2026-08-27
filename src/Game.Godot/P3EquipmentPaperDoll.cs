using GameForWork.Core.P1.Items;
using GameForWork.Core.P2;
using Godot;

namespace GameForWork.GodotClient;

public partial class P3EquipmentPaperDoll : Control
{
    private readonly Dictionary<EquipmentSlot, P1ItemGrid> _slots = [];
    private Func<ItemInstance, string>? _extraTooltip;

    public event Action<int>? ItemActivated;
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

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(224, 390);
        MouseFilter = MouseFilterEnum.Ignore;
        IReadOnlyDictionary<EquipmentSlot, Vector2> positions = new Dictionary<EquipmentSlot, Vector2>
        {
            [EquipmentSlot.Helmet] = new(88, 10),
            [EquipmentSlot.Amulet] = new(150, 62),
            [EquipmentSlot.MainHand] = new(12, 102),
            [EquipmentSlot.Chest] = new(88, 82),
            [EquipmentSlot.OffHand] = new(164, 102),
            [EquipmentSlot.Gloves] = new(24, 166),
            [EquipmentSlot.Belt] = new(88, 158),
            [EquipmentSlot.RingLeft] = new(38, 222),
            [EquipmentSlot.RingRight] = new(138, 222),
            [EquipmentSlot.Boots] = new(88, 245),
            [EquipmentSlot.Flask1] = new(4, 326),
            [EquipmentSlot.Flask2] = new(47, 326),
            [EquipmentSlot.Flask3] = new(90, 326),
            [EquipmentSlot.Flask4] = new(133, 326),
            [EquipmentSlot.Flask5] = new(176, 326),
        };
        foreach ((EquipmentSlot slot, Vector2 position) in positions)
        {
            var grid = new P1ItemGrid
            {
                ContainerKind = ItemContainerKind.Equipped,
                IndexOffset = (int)slot,
                Position = position,
                ExtraTooltip = _extraTooltip,
            };
            grid.Configure(1, 1, slot is >= EquipmentSlot.Flask1 and <= EquipmentSlot.Flask5 ? 38 : 46);
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
        DrawCircle(new Vector2(111, 61), 20, silhouette);
        DrawRect(new Rect2(78, 83, 66, 127), silhouette, true);
        DrawLine(new Vector2(86, 104), new Vector2(54, 204), silhouette, 22);
        DrawLine(new Vector2(136, 104), new Vector2(168, 204), silhouette, 22);
        DrawLine(new Vector2(96, 203), new Vector2(84, 298), silhouette, 24);
        DrawLine(new Vector2(126, 203), new Vector2(138, 298), silhouette, 24);
        DrawString(ThemeDB.FallbackFont, new Vector2(77, 319), "药剂腰带", HorizontalAlignment.Center, 70, 11,
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
}
