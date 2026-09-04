using GameForWork.Core.P1;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P2;
using GameForWork.Core.P4;
using GameForWork.Core.P6;
using GameForWork.Core.P9;
using GameForWork.Core.P14;
using GameForWork.Core.P29;
using GameForWork.Core.Equipment;
using Godot;

namespace GameForWork.GodotClient;

public sealed record EquipmentCraftTarget(ItemContainerKind Container, int Index, ItemInstance Item,
    P2CharacterKind Character, string MercenaryId);

public partial class EquipmentCraftingPanel : VBoxContainer
{
    private Func<P1GameSession>? _session;
    private Func<EquipmentCraftTarget?>? _target;
    private Action<string>? _changed;
    private GridContainer? _grid;
    private VBoxContainer? _body;
    private Label? _status;
    private HFlowContainer? _enchants;
    private HFlowContainer? _alchemy;
    private HFlowContainer? _garden;
    private Texture2D? _metalAtlas;
    private string _signature = string.Empty;

    public void Initialize(Func<P1GameSession> session, Func<EquipmentCraftTarget?> target, Action<string> changed)
    {
        _session = session;
        _target = target;
        _changed = changed;
        const string p21 = "res://assets/p21/ui/p21-metal-atlas.png";
        _metalAtlas = ResourceLoader.Exists(p21) ? GD.Load<Texture2D>(p21) : null;
        Name = "打造";
        SizeFlagsVertical = SizeFlags.ExpandFill;
        var outerScroll = new ScrollContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        AddChild(outerScroll);
        _body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        outerScroll.AddChild(_body);
        _body.AddChild(new Label { Text = "打造材料 · 不占普通仓库 · 点击立即执行，无二次确认 · 悬浮查看完整效果", AutowrapMode = TextServer.AutowrapMode.WordSmart });
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
    }

    public void Refresh(bool force = false)
    {
        if (_session is null) return;
        P1GameSession session = _session();
        EquipmentCraftTarget? target = _target?.Invoke();
        string signature = string.Join('|', P4MetalCurrencies.All.Select(metal => session.World.Economy.MetalAmount(metal.Kind))) +
            $"|{session.World.Economy.Gold}|{session.Endgame.LifeForce}|{session.Town.Level(P9BuildingKind.Workshop)}|{session.Town.Level(P9BuildingKind.Alchemy)}|{TargetSignature(target)}";
        if (!force && signature == _signature) return;
        _signature = signature;
        _status!.Text = target is null ? "尚未选择装备：将装备拖到工艺台，或在装备栏、整理背包、仓库中单击一件物品。" :
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
        EquipmentCraftTarget? target = _target?.Invoke();
        if (target is null) return;
        string? operationName = OperationNameFor(kind, Input.IsKeyPressed(Key.Shift));
        EquipmentCraftingOperationEntry? operation = EquipmentCatalog.CraftingOperations
            .FirstOrDefault(value => value.DisplayName == operationName);
        if (operation is null) { _changed?.Invoke("该金属没有正式做装操作。"); return; }
        EquipmentCraftingPreview preview = EquipmentCraftingService.Preview(target.Item, new(operation.Id));
        if (!preview.Available) { _changed?.Invoke(preview.Summary); return; }
        EquipmentCraftingResult result = new P2ItemCommandService(_session!(), target.Character, target.MercenaryId)
            .CraftEquipment(target.Container, target.Index, operation.DisplayName);
        _changed?.Invoke(result.Summary);
    }

    private void RebuildEnchantments(P1GameSession session, EquipmentCraftTarget? target)
    {
        foreach (Node child in _enchants!.GetChildren()) child.QueueFree();
        foreach (ItemEnchantment enchantment in P9EnchantmentCatalog.All)
        {
            EquipmentEnchantmentEntry entry = EquipmentEnchantmentCatalog.Entry(enchantment);
            var button = new Button
            {
                Text = $"Lv.{enchantment.WorkshopLevel} {enchantment.DisplayName}\n{enchantment.GoldCost:N0} 金币",
                TooltipText = $"{entry.DisplayName}（{entry.WorkshopLevel} 阶附魔）\n" +
                              $"完整效果：{entry.RuleText}\n" +
                              $"适用装备：{entry.ApplicableEquipment}\n" +
                              $"获得方式：工坊 Lv.{entry.WorkshopLevel} · 消耗 {entry.GoldCost:N0} 金币\n" +
                              "覆盖现有附魔；点击即执行；失败不扣费。",
                Disabled = target is null || session.Town.Level(P9BuildingKind.Workshop) < enchantment.WorkshopLevel || session.World.Economy.Gold < enchantment.GoldCost,
            };
            button.Pressed += () =>
            {
                EquipmentCraftTarget? current = _target?.Invoke();
                if (current is null) return;
                EquipmentCraftingOperationEntry operation = EquipmentCatalog.CraftingOperations.Single(value =>
                    value.Kind == "Enchantment" && value.DisplayName == $"附魔：{enchantment.DisplayName}");
                var request = new EquipmentCraftingRequest(operation.Id, enchantment.StableId,
                    WorkshopLevel: session.Town.Level(P9BuildingKind.Workshop));
                EquipmentCraftingPreview preview = EquipmentCraftingService.Preview(current.Item, request);
                if (!preview.Available) { _changed?.Invoke(preview.Summary); return; }
                EquipmentCraftingResult result = new P2ItemCommandService(_session!(), current.Character, current.MercenaryId)
                    .CraftEquipment(current.Container, current.Index, operation.DisplayName, enchantment.StableId);
                _changed?.Invoke(result.Summary);
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

    private void RebuildGarden(P1GameSession session, EquipmentCraftTarget? target)
    {
        foreach (Node child in _garden!.GetChildren()) child.QueueFree();
        foreach (EquipmentCraftingOperationEntry operation in EquipmentCatalog.CraftingOperations
                     .Where(value => value.Kind is "LifeEnergy" or "Oath"))
        {
            if (operation.DisplayName == "赤誓升降")
            {
                if (target is null) continue;
                foreach (AffixRoll affix in target.Item.Affixes.Where(value => !value.Crafted))
                    AddOperation(operation, affix.Definition.StableFamilyId, $"{operation.DisplayName}：{affix.Definition.DisplayName}");
                continue;
            }
            AddOperation(operation, string.Empty, operation.DisplayName);
        }
        return;

        void AddOperation(EquipmentCraftingOperationEntry operation, string familyId, string title)
        {
            EquipmentCraftingPreview? preview = target is null ? null : EquipmentCraftingService.Preview(target.Item,
                new EquipmentCraftingRequest(operation.Id, SelectedAffixFamilyId: familyId));
            string resource = preview?.Resource ?? CostResource(operation.CostText);
            int cost = preview?.Cost ?? CostAmount(operation.CostText);
            var button = new Button
            {
                Text = $"{title}\n{cost:N0} {resource}",
                TooltipText = $"{operation.RuleText}\n点击即执行；失败不扣费；随机结果在执行时产生。",
                Disabled = target is null || preview?.Available != true || !HasResource(session, resource, cost),
            };
            button.Pressed += () =>
            {
                EquipmentCraftTarget? current = _target?.Invoke();
                if (current is null) return;
                EquipmentCraftingResult result = new P2ItemCommandService(_session!(), current.Character, current.MercenaryId)
                    .CraftEquipment(current.Container, current.Index, operation.DisplayName,
                        selectedAffixFamilyId: familyId);
                _changed?.Invoke(result.Summary);
            };
            _garden.AddChild(button);
        }
    }

    private static bool HasResource(P1GameSession session, string resource, int cost)
    {
        MetalCurrencyDefinition? metal = P4MetalCurrencies.All.FirstOrDefault(value => value.DisplayName == resource);
        if (metal is not null) return session.World.Economy.MetalAmount(metal.Kind) >= cost;
        return resource switch
        {
            "金币" => session.World.Economy.Gold >= cost, "命能" => session.Endgame.LifeForce >= cost,
            "赤誓收益" => session.Endgame.RedFavor >= cost, "苍誓收益" => session.Endgame.BlueFavor >= cost,
            "监守印记" => session.World.Economy.WardenMarks >= cost, _ => cost == 0,
        };
    }

    private static int CostAmount(string text)
    {
        string token = new(text.Reverse().SkipWhile(character => !char.IsDigit(character)).TakeWhile(char.IsDigit).Reverse().ToArray());
        return int.TryParse(token, out int value) ? value : 0;
    }

    private static string CostResource(string text)
    {
        string tail = text.Contains('；') ? text.Split('；')[1] : text;
        return new string(tail.TakeWhile(character => !char.IsDigit(character) && character != '×').ToArray()).Trim();
    }

    private static string? OperationNameFor(MetalCurrencyKind kind, bool rerollLinks) => kind switch
    {
        MetalCurrencyKind.TemperingIron => "淬刃打造", MetalCurrencyKind.WardSteel => "守壁打造",
        MetalCurrencyKind.VitalSilver => "活血打造", MetalCurrencyKind.AwakeningCopper => "启灵",
        MetalCurrencyKind.AugmentingTin => "添铸", MetalCurrencyKind.MutableMercury => "易变重铸",
        MetalCurrencyKind.FatefulGold => "命铸", MetalCurrencyKind.AlchemicalGold => "炼真",
        MetalCurrencyKind.RegalGold => "王铸", MetalCurrencyKind.ChaosGold => "混沌重铸",
        MetalCurrencyKind.ExaltedGold => "崇高增附", MetalCurrencyKind.DissolutionSilver => "消解",
        MetalCurrencyKind.ScouringLead => "洗炼", MetalCurrencyKind.DivineSilver => "神铸重掷",
        MetalCurrencyKind.BlessedSilver => "祝铸重掷", MetalCurrencyKind.FractureSteel => "破裂",
        MetalCurrencyKind.ChainSteel => rerollLinks ? "连接重铸" : "稳固增连",
        MetalCurrencyKind.PolishingCobalt => "精磨品质", MetalCurrencyKind.CorruptionIron => "赤蚀腐化",
        _ => null,
    };

    private static string TierText(MetalCurrencyTier tier) => tier switch
    { MetalCurrencyTier.Basic => "基础", MetalCurrencyTier.Advanced => "进阶", MetalCurrencyTier.High => "高阶", _ => "危险" };

    private static string TargetSignature(EquipmentCraftTarget? target)
    {
        if (target is null) return "none";
        ItemInstance item = target.Item;
        return $"{target.Container}:{target.Index}:{item.InstanceId}:{item.Rarity}:{item.Quality}:{item.LinkedSocketCount}:" +
               $"{item.IsCorrupted}:{item.CorruptionOutcome}:{item.Enchantment?.StableId}:{item.FracturedAffixFamilyId}:" +
               string.Join(',', item.Affixes.Select(affix =>
                   $"{affix.Definition.StableFamilyId}:{affix.EffectiveValue}:{affix.Crafted}"));
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
