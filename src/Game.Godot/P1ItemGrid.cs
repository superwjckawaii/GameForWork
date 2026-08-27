using GameForWork.Core.P1.Items;
using GameForWork.Core.P2;
using Godot;

namespace GameForWork.GodotClient;

public partial class P1ItemGrid : GridContainer
{
    private readonly List<P2ItemCell> _cells = [];
    private IReadOnlyList<ItemInstance?> _items = [];
    private int _selectedIndex = -1;
    private Texture2D? _iconAtlas;

    public event Action<int>? ItemSelected;
    public event Action<int>? ItemActivated;
    public event Action<int, Vector2>? ItemContextRequested;
    public event Action<ItemContainerKind, int, int>? ItemDropped;
    public event Action<int>? QuickTransferRequested;

    public int SelectedIndex => _selectedIndex;
    public int IndexOffset { get; set; }
    public ItemContainerKind ContainerKind { get; set; }
    public Func<ItemInstance, string>? ExtraTooltip { get; set; }
    public Func<ItemContainerKind, int, int, bool>? DropValidator { get; set; }
    public string EmptyLabel { get; set; } = string.Empty;
    public ItemInstance? SelectedItem => _selectedIndex >= 0 && _selectedIndex < _items.Count
        ? _items[_selectedIndex]
        : null;

    public override void _Ready()
    {
        const string path = "res://assets/p2/ui/p2-item-grid.png";
        if (ResourceLoader.Exists(path))
        {
            _iconAtlas = GD.Load<Texture2D>(path);
            ApplyCells();
        }
    }

    public void Configure(int columns, int capacity, float cellSize = 38)
    {
        Columns = columns;
        AddThemeConstantOverride("h_separation", 3);
        AddThemeConstantOverride("v_separation", 3);
        for (int index = 0; index < capacity; index++)
        {
            int captured = index;
            var cell = new P2ItemCell
            {
                Grid = this,
                CellIndex = index,
                CustomMinimumSize = new Vector2(cellSize, cellSize),
                ToggleMode = true,
                FocusMode = FocusModeEnum.None,
                Alignment = HorizontalAlignment.Center,
                IconAlignment = HorizontalAlignment.Center,
                TooltipText = $"空格 {index + 1}",
            };
            cell.AddThemeFontSizeOverride("font_size", 13);
            cell.Pressed += () => Select(captured);
            AddChild(cell);
            _cells.Add(cell);
        }

        SetItems([]);
    }

    public void SetItems(IReadOnlyList<ItemInstance> items)
    {
        _items = items.Cast<ItemInstance?>().ToArray();
        if (_selectedIndex >= items.Count)
        {
            _selectedIndex = -1;
        }

        ApplyCells();
    }

    public void SetSlots(IReadOnlyList<ItemInstance?> items)
    {
        _items = items;
        if (_selectedIndex >= items.Count || _selectedIndex >= 0 && items[_selectedIndex] is null)
        {
            _selectedIndex = -1;
        }

        ApplyCells();
    }

    public int ToExternalIndex(int index) => checked(index + IndexOffset);

    public void Activate(int index) => ItemActivated?.Invoke(ToExternalIndex(index));

    public void OpenContext(int index, Vector2 screenPosition) =>
        ItemContextRequested?.Invoke(ToExternalIndex(index), screenPosition);

    public void ReceiveDrop(ItemContainerKind source, int sourceIndex, int targetIndex) =>
        ItemDropped?.Invoke(source, sourceIndex, ToExternalIndex(targetIndex));

    public bool CanReceiveDrop(ItemContainerKind source, int sourceIndex, int targetIndex) =>
        DropValidator?.Invoke(source, sourceIndex, ToExternalIndex(targetIndex)) ?? true;

    public void QuickTransfer(int index) => QuickTransferRequested?.Invoke(ToExternalIndex(index));

    private void ApplyCells()
    {
        for (int index = 0; index < _cells.Count; index++)
        {
            P2ItemCell cell = _cells[index];
            bool occupied = index < _items.Count && _items[index] is not null;
            ItemInstance? item = occupied ? _items[index] : null;
            cell.HasItem = occupied;
            cell.Icon = occupied ? IconFor(item!.Base.Category) : null;
            cell.ExpandIcon = occupied && cell.Icon is not null;
            cell.Text = occupied && cell.Icon is null ? P1UiText.ItemGlyph(item!.Base.Category) :
                occupied ? string.Empty : EmptyLabel;
            string extra = occupied ? ExtraTooltip?.Invoke(item!) ?? string.Empty : string.Empty;
            cell.TooltipText = occupied
                ? P1UiText.ItemTooltip(item!) + (extra.Length == 0 ? string.Empty : $"\n\n{extra}")
                : $"空格 {index + 1}";
            cell.Disabled = false;
            cell.SetPressedNoSignal(index == _selectedIndex);
            Color color = occupied ? P1UiText.RarityColor(item!.Rarity) : new Color("71695e");
            cell.TooltipRarityColor = color;
            cell.AddThemeColorOverride("font_color", color);
            cell.AddThemeColorOverride("font_pressed_color", color.Lightened(0.15f));
            cell.AddThemeStyleboxOverride("normal", Frame(new Color("171b22"), color.Darkened(0.32f), 1));
            cell.AddThemeStyleboxOverride("hover", Frame(new Color("252b35"), color, 2));
            cell.AddThemeStyleboxOverride("pressed", Frame(new Color("303744"), color.Lightened(0.16f), 2));
            cell.AddThemeStyleboxOverride("disabled", Frame(new Color("11141a"), new Color("353b45"), 1));
        }
    }

    private static StyleBoxFlat Frame(Color background, Color border, int width) => new()
    {
        BgColor = background,
        BorderColor = border,
        BorderWidthLeft = width,
        BorderWidthTop = width,
        BorderWidthRight = width,
        BorderWidthBottom = width,
        CornerRadiusTopLeft = 2,
        CornerRadiusTopRight = 2,
        CornerRadiusBottomLeft = 2,
        CornerRadiusBottomRight = 2,
    };

    private Texture2D? IconFor(ItemCategory category)
    {
        if (_iconAtlas is null)
        {
            return null;
        }

        (int column, int row) = category switch
        {
            ItemCategory.TwoHandWeapon => (0, 0),
            ItemCategory.BodyArmor => (1, 0),
            ItemCategory.Helmet => (2, 0),
            ItemCategory.Ring => (3, 0),
            ItemCategory.LifeFlask => (4, 0),
            ItemCategory.Gloves => (5, 0),
            ItemCategory.Boots => (6, 0),
            ItemCategory.Belt => (7, 0),
            ItemCategory.Amulet => (0, 1),
            _ => (0, 0),
        };
        float height = _iconAtlas.GetHeight() / 2f;
        float[] starts = [32, 280, 580, 850, 1_120, 1_360, 1_630, 1_880];
        float[] widths = [236, 280, 240, 220, 220, 265, 240, 270];
        float scaleX = _iconAtlas.GetWidth() / 2_172f;
        return new AtlasTexture
        {
            Atlas = _iconAtlas,
            Region = new Rect2(starts[column] * scaleX, height * row, widths[column] * scaleX, height),
            FilterClip = true,
        };
    }

    private void Select(int index)
    {
        if (index < 0 || index >= _items.Count)
        {
            return;
        }

        _selectedIndex = index;
        for (int cellIndex = 0; cellIndex < _cells.Count; cellIndex++)
        {
            _cells[cellIndex].SetPressedNoSignal(cellIndex == index);
        }

        ItemSelected?.Invoke(ToExternalIndex(index));
    }
}
