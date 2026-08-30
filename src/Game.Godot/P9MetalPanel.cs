using GameForWork.Core.P1;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P2;
using GameForWork.Core.P4;
using GameForWork.Core.P6;
using GameForWork.Core.P9;
using GameForWork.Core.P14;
using Godot;

namespace GameForWork.GodotClient;

public sealed record P9CraftTarget(ItemContainerKind Container, int Index, ItemInstance Item,
    P2CharacterKind Character, string MercenaryId);

public partial class P9MetalPanel : VBoxContainer
{
    private Func<P1GameSession>? _session;
    private Func<P9CraftTarget?>? _target;
    private Action<string>? _changed;
    private GridContainer? _grid;
    private VBoxContainer? _body;
    private Label? _status;
    private HFlowContainer? _enchants;
    private HFlowContainer? _alchemy;
    private HFlowContainer? _garden;
    private Texture2D? _metalAtlas;
    private ConfirmationDialog? _confirm;
    private Action? _pending;
    private string _signature = string.Empty;

    public void Initialize(Func<P1GameSession> session, Func<P9CraftTarget?> target, Action<string> changed)
    {
        _session = session;
        _target = target;
        _changed = changed;
        const string p21 = "res://assets/p21/ui/p21-metal-atlas.png";
        _metalAtlas = GD.Load<Texture2D>(ResourceLoader.Exists(p21) ? p21 : "res://assets/p9/ui/p9-metal-atlas.png");
        Name = "打造";
        SizeFlagsVertical = SizeFlags.ExpandFill;
        var outerScroll = new ScrollContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        AddChild(outerScroll);
        _body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        outerScroll.AddChild(_body);
        _body.AddChild(new Label { Text = "打造材料 · 不占普通仓库 · 悬浮任意材料或配方查看完整效果", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _body.AddChild(_status);
        _grid = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _body.AddChild(_grid);
        _body.AddChild(new Label { Text = "工坊附魔 · 覆盖不返还金币", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        _enchants = new HFlowContainer();
        _body.AddChild(_enchants);
        _body.AddChild(new Label { Text = "炼金所 · 固定公开配方" });
        _alchemy = new HFlowContainer();
        _body.AddChild(_alchemy);
        _body.AddChild(new Label { Text = "命能花园 · 确定性定向加工" });
        _garden = new HFlowContainer();
        _body.AddChild(_garden);
        _confirm = new ConfirmationDialog { Title = "确认打造", OkButtonText = "确认使用", CancelButtonText = "取消", Exclusive = true };
        _confirm.Confirmed += () => { Action? action = _pending; _pending = null; action?.Invoke(); };
        _confirm.Canceled += () => _pending = null;
        AddChild(_confirm);
    }

    public void Refresh(bool force = false)
    {
        if (_session is null) return;
        P1GameSession session = _session();
        P9CraftTarget? target = _target?.Invoke();
        string signature = string.Join('|', P4MetalCurrencies.All.Select(metal => session.World.Economy.MetalAmount(metal.Kind))) +
            $"|{session.World.Economy.Gold}|{session.Endgame.LifeForce}|{session.Town.Level(P9BuildingKind.Workshop)}|{session.Town.Level(P9BuildingKind.Alchemy)}|{target?.Item.InstanceId}|{target?.Item.Affixes.Count}|{target?.Item.Quality}";
        if (!force && signature == _signature) return;
        _signature = signature;
        _status!.Text = target is null ? "尚未选择装备：先在装备栏、整理背包或仓库中单击一件物品。" :
            $"当前目标：{target.Item.Base.DisplayName} · {target.Item.Rarity} · {target.Item.Affixes.Count} 词缀 · 品质 {target.Item.Quality}%" +
            (target.Item.IsCorrupted ? " · 已腐化" : string.Empty);
        foreach (Node child in _grid!.GetChildren()) child.QueueFree();
        foreach (MetalCurrencyDefinition metal in P4MetalCurrencies.All)
        {
            int count = session.World.Economy.MetalAmount(metal.Kind);
            var cell = new VBoxContainer { CustomMinimumSize = new Vector2(88, 78) };
            cell.AddChild(new TextureRect
            {
                Texture = MetalIcon(metal.Kind),
                CustomMinimumSize = new Vector2(0, 38),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = MouseFilterEnum.Ignore,
                TooltipText = $"{metal.DisplayName}\n{metal.Description}\n档位：{TierText(metal.Tier)}\n持有：{count}",
            });
            var button = new Button
            {
                Text = $"{metal.DisplayName} ×{count}",
                CustomMinimumSize = new Vector2(82, 34),
                TooltipText = $"{metal.Description}\n档位：{TierText(metal.Tier)}\n持有：{count}",
                Disabled = count <= 0 || target is null,
            };
            button.AddThemeFontSizeOverride("font_size", 11);
            button.Pressed += () => UseMetal(metal.Kind);
            cell.AddChild(button);
            _grid.AddChild(cell);
        }
        RebuildEnchantments(session, target);
        RebuildAlchemy(session);
        RebuildGarden(session, target);
    }

    private void UseMetal(MetalCurrencyKind kind)
    {
        P9CraftTarget? target = _target?.Invoke();
        if (target is null) return;
        if (kind is MetalCurrencyKind.TemperingIron or MetalCurrencyKind.WardSteel or MetalCurrencyKind.VitalSilver)
        {
            P2WorkshopRecipe recipe = kind switch
            { MetalCurrencyKind.TemperingIron => P2WorkshopRecipe.WeaponPhysical, MetalCurrencyKind.WardSteel => P2WorkshopRecipe.ReinforceDefense, _ => P2WorkshopRecipe.VitalityEtching };
            Confirm($"{P4MetalCurrencies.Get(kind).DisplayName}\n\n{P4MetalCurrencies.Get(kind).Description}\n消耗：1", () =>
            {
                P2WorkshopPreview result = new P2ItemCommandService(_session!(), target.Character, target.MercenaryId).Craft(target.Container, target.Index, recipe);
                _changed?.Invoke(result.Summary);
            });
            return;
        }
        if (kind == MetalCurrencyKind.ChainSteel)
        {
            bool rerollRequested = Input.IsKeyPressed(Key.Shift);
            P6CraftOperation linkOperation = rerollRequested ? P6CraftOperation.RerollLinks : P6CraftOperation.UpgradeLinks;
            P6CraftPreview preview = P6CraftingRules.Preview(target.Item, linkOperation);
            if (!preview.Succeeded) { _changed?.Invoke(preview.Summary); return; }
            Confirm($"{preview.Summary}\n消耗：{preview.Cost} 链铸钢\n\n默认保证升连；按住 Shift 点击金属可重铸连接。", () =>
            {
                P6CraftPreview result = new P2ItemCommandService(_session!(), target.Character, target.MercenaryId).CraftP6(target.Container, target.Index, linkOperation);
                _changed?.Invoke(result.Summary);
            });
            return;
        }
        P9CraftOperation? operation = OperationFor(kind);
        if (operation is null) { _changed?.Invoke("该金属没有可用操作。"); return; }
        P9CraftResult p9 = P9CraftingRules.Preview(target.Item, operation.Value);
        if (!p9.Succeeded) { _changed?.Invoke(p9.Summary); return; }
        string danger = kind == MetalCurrencyKind.CorruptionIron ? "\n\n警告：10% 概率彻底摧毁装备，此操作不可撤销。" : string.Empty;
        Confirm($"{p9.Summary}\n消耗：{p9.Cost} {P4MetalCurrencies.Get(kind).DisplayName}{danger}", () =>
        {
            P9CraftResult result = new P2ItemCommandService(_session!(), target.Character, target.MercenaryId).CraftP9(target.Container, target.Index, operation.Value);
            _changed?.Invoke(result.Summary);
        });
    }

    private void RebuildEnchantments(P1GameSession session, P9CraftTarget? target)
    {
        foreach (Node child in _enchants!.GetChildren()) child.QueueFree();
        foreach (ItemEnchantment enchantment in P9EnchantmentCatalog.All)
        {
            var button = new Button
            {
                Text = $"Lv.{enchantment.WorkshopLevel} {enchantment.DisplayName}\n{enchantment.GoldCost:N0} 金币",
                TooltipText = $"{enchantment.DisplayName}\n完整效果：{ModifierText(enchantment.ModifierKind, enchantment.Value)}\n" +
                              $"覆盖现有附魔 · 需要工坊 Lv.{enchantment.WorkshopLevel} · 消耗 {enchantment.GoldCost:N0} 金币",
                Disabled = target is null || session.Town.Level(P9BuildingKind.Workshop) < enchantment.WorkshopLevel || session.World.Economy.Gold < enchantment.GoldCost,
            };
            button.Pressed += () =>
            {
                P9CraftTarget? current = _target?.Invoke();
                if (current is null) return;
                P9CraftResult preview = P9EnchantmentCatalog.Preview(current.Item, enchantment.StableId, session.Town.Level(P9BuildingKind.Workshop));
                if (!preview.Succeeded) { _changed?.Invoke(preview.Summary); return; }
                Confirm($"{preview.Summary}\n消耗：{enchantment.GoldCost:N0} 金币", () =>
                {
                    P9CraftResult result = new P2ItemCommandService(_session!(), current.Character, current.MercenaryId).EnchantP9(current.Container, current.Index, enchantment.StableId);
                    _changed?.Invoke(result.Summary);
                });
            };
            _enchants.AddChild(button);
        }
    }

    private void RebuildAlchemy(P1GameSession session)
    {
        foreach (Node child in _alchemy!.GetChildren()) child.QueueFree();
        foreach (P9MetalTransmutationRecipe recipe in P9TownState.AlchemyRecipes)
        {
            string input = P4MetalCurrencies.Get(recipe.Input).DisplayName;
            string output = P4MetalCurrencies.Get(recipe.Output).DisplayName;
            var button = new Button
            {
                Text = $"Lv.{recipe.AlchemyLevel}  {recipe.InputCount}×{input} + {recipe.GoldCost:N0} 金\n→ 1×{output}",
                TooltipText = $"完整炼金效果\n消耗：{recipe.InputCount}×{input} + {recipe.GoldCost:N0} 金币\n" +
                              $"获得：1×{output}\n需要炼金所 Lv.{recipe.AlchemyLevel} · 固定配方，不受随机数影响",
                Disabled = session.Town.Level(P9BuildingKind.Alchemy) < recipe.AlchemyLevel ||
                    session.World.Economy.MetalAmount(recipe.Input) < recipe.InputCount ||
                    session.World.Economy.Gold < recipe.GoldCost,
            };
            button.Pressed += () => _changed?.Invoke(session.TryTransmuteMetal(recipe.Output)
                ? $"炼金完成：获得 {output}。" : "炼金所等级、金币或原料金属不足。");
            _alchemy.AddChild(button);
        }
    }

    private void RebuildGarden(P1GameSession session, P9CraftTarget? target)
    {
        foreach (Node child in _garden!.GetChildren()) child.QueueFree();
        foreach (P14GardenCraft craft in Enum.GetValues<P14GardenCraft>())
        {
            int cost = P14GardenCrafting.Cost(craft);
            var button = new Button
            {
                Text = $"{GardenName(craft)}\n{cost} 命能",
                TooltipText = "只保留或偏向公开类别；相同存档种子产生相同结果。",
                Disabled = target is null || target.Item.Rarity != ItemRarity.Rare || !target.Item.CanModify || session.Endgame.LifeForce < cost,
            };
            button.Pressed += () =>
            {
                P9CraftTarget? current = _target?.Invoke();
                if (current is null) return;
                Confirm($"{GardenName(craft)}\n消耗：{cost} 命能", () =>
                {
                    P14GardenCraftResult result = new P2ItemCommandService(_session!(), current.Character, current.MercenaryId)
                        .CraftP14(current.Container, current.Index, craft);
                    _changed?.Invoke(result.Summary);
                });
            };
            _garden.AddChild(button);
        }
    }

    private static string GardenName(P14GardenCraft craft) => craft switch
    {
        P14GardenCraft.KeepPrefixes => "保留前缀重铸",
        P14GardenCraft.KeepSuffixes => "保留后缀重铸",
        P14GardenCraft.BiasLife => "生命偏向重铸",
        P14GardenCraft.BiasDefense => "防御偏向重铸",
        _ => "攻击偏向重铸",
    };

    private void Confirm(string text, Action action)
    {
        _pending = action;
        _confirm!.DialogText = text;
        _confirm.PopupCentered(new Vector2I(520, 260));
    }

    private static P9CraftOperation? OperationFor(MetalCurrencyKind kind) => kind switch
    {
        MetalCurrencyKind.AwakeningCopper => P9CraftOperation.AwakenMagic,
        MetalCurrencyKind.AugmentingTin => P9CraftOperation.AugmentMagic,
        MetalCurrencyKind.MutableMercury => P9CraftOperation.RerollMagic,
        MetalCurrencyKind.FatefulGold => P9CraftOperation.FatefulUpgrade,
        MetalCurrencyKind.AlchemicalGold => P9CraftOperation.AlchemicalRare,
        MetalCurrencyKind.RegalGold => P9CraftOperation.RegalUpgrade,
        MetalCurrencyKind.ChaosGold => P9CraftOperation.ChaosReroll,
        MetalCurrencyKind.ExaltedGold => P9CraftOperation.ExaltedAdd,
        MetalCurrencyKind.DissolutionSilver => P9CraftOperation.DissolveAffix,
        MetalCurrencyKind.ScouringLead => P9CraftOperation.Scour,
        MetalCurrencyKind.DivineSilver => P9CraftOperation.DivineReroll,
        MetalCurrencyKind.BlessedSilver => P9CraftOperation.BlessedReroll,
        MetalCurrencyKind.FractureSteel => P9CraftOperation.Fracture,
        MetalCurrencyKind.PolishingCobalt => P9CraftOperation.PolishQuality,
        MetalCurrencyKind.CorruptionIron => P9CraftOperation.Corrupt,
        _ => null,
    };

    private static string TierText(MetalCurrencyTier tier) => tier switch
    { MetalCurrencyTier.Basic => "基础", MetalCurrencyTier.Advanced => "进阶", MetalCurrencyTier.High => "高阶", _ => "危险" };

    private static string ModifierText(ItemModifierKind kind, int value)
    {
        string name = kind switch
        {
            ItemModifierKind.FlatAccuracy => "命中值",
            ItemModifierKind.FlatMaximumLife => "最大生命",
            ItemModifierKind.IncreasedAttackSpeedBasisPoints => "攻击速度提高",
            ItemModifierKind.IncreasedPhysicalDamageBasisPoints => "物理伤害提高",
            ItemModifierKind.IncreasedArmorBasisPoints => "护甲提高",
            ItemModifierKind.ExtraSupportLinkCapacity => "额外连接容量",
            _ => kind.ToString(),
        };
        return kind.ToString().Contains("BasisPoints", StringComparison.Ordinal)
            ? $"{name} {value / 100.0:0.#}%"
            : $"{name} +{value}";
    }

    private AtlasTexture? MetalIcon(MetalCurrencyKind kind)
    {
        if (_metalAtlas is null) return null;
        float width = _metalAtlas.GetWidth() / 5f;
        float height = _metalAtlas.GetHeight() / 4f;
        int index = (int)kind;
        return new AtlasTexture
        {
            Atlas = _metalAtlas,
            Region = new Rect2(index % 5 * width, index / 5 * height, width, height),
            FilterClip = true,
        };
    }
}
