using GameForWork.Core.P1.Items;
using Godot;

namespace GameForWork.GodotClient;

public partial class P1ItemGrid : GridContainer
{
    private readonly List<Button> _cells = [];
    private IReadOnlyList<ItemInstance> _items = [];
    private int _selectedIndex = -1;
    private Texture2D? _iconAtlas;

    public event Action<int>? ItemSelected;

    public int SelectedIndex => _selectedIndex;

    public override void _Ready()
    {
        const string path = "res://assets/p1b/ui/item-atlas.png";
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
            var cell = new Button
            {
                CustomMinimumSize = new Vector2(cellSize, cellSize),
                ToggleMode = true,
                FocusMode = FocusModeEnum.None,
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
        _items = items;
        if (_selectedIndex >= items.Count)
        {
            _selectedIndex = -1;
        }

        ApplyCells();
    }

    private void ApplyCells()
    {
        for (int index = 0; index < _cells.Count; index++)
        {
            Button cell = _cells[index];
            bool occupied = index < _items.Count;
            cell.Icon = occupied ? IconFor(_items[index].Base.Category) : null;
            cell.ExpandIcon = occupied && cell.Icon is not null;
            cell.Text = occupied && cell.Icon is null ? P1UiText.ItemGlyph(_items[index].Base.Category) : string.Empty;
            cell.TooltipText = occupied ? P1UiText.ItemTooltip(_items[index]) : $"空格 {index + 1}";
            cell.Disabled = !occupied;
            cell.SetPressedNoSignal(index == _selectedIndex);
            Color color = occupied ? P1UiText.RarityColor(_items[index].Rarity) : new Color("71695e");
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

        int column = category switch
        {
            ItemCategory.TwoHandWeapon => 0,
            ItemCategory.BodyArmor => 1,
            ItemCategory.Helmet => 2,
            ItemCategory.Ring => 3,
            ItemCategory.LifeFlask => 4,
            _ => 0,
        };
        float width = _iconAtlas.GetWidth() / 5f;
        return new AtlasTexture
        {
            Atlas = _iconAtlas,
            Region = new Rect2(width * column, 0, width, _iconAtlas.GetHeight()),
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

        ItemSelected?.Invoke(index);
    }
}
