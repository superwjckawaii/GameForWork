using GameForWork.Core.P1;
using GameForWork.Core.P1.World;
using GameForWork.Core.P5;
using GameForWork.Core.P6;
using GameForWork.Core.P10;
using GameForWork.Core.P12;
using GameForWork.Core.P14;
using GameForWork.Core.P26;
using Godot;

namespace GameForWork.GodotClient;

public partial class P5ExpeditionPanel : VBoxContainer
{
    public event Action? ReportsViewed;
    private readonly Dictionary<ExpeditionTeamKind, TeamControls> _teams = [];
    private Func<P1GameSession>? _session;
    private Action<string>? _changed;
    private Label? _resources;
    private VBoxContainer? _mapInventory;
    private VBoxContainer? _reports;
    private Label? _mapDetails;
    private int _selectedMapIndex = -1;
    private string _mapSignature = string.Empty;
    private string _reportSignature = string.Empty;
    private string _dispatchSignature = string.Empty;

    public void Initialize(Func<P1GameSession> session, Action<string> changed)
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        _session = session;
        _changed = changed;
        _resources = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        AddChild(_resources);

        var body = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0, 280),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        body.AddThemeConstantOverride("separation", 14);
        AddChild(body);

        var warehouse = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(310, 0),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        warehouse.AddChild(new Label { Text = "路印仓与制图" });
        var mapScroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 230), SizeFlagsVertical = SizeFlags.ExpandFill };
        _mapInventory = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        mapScroll.AddChild(_mapInventory); warehouse.AddChild(mapScroll);
        _mapDetails = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(0, 116) };
        warehouse.AddChild(_mapDetails);
        var craft = new HFlowContainer(); warehouse.AddChild(craft);
        AddCraftButton(craft, "精磨 +5%", P12MapCraftOperation.PolishQuality);
        AddCraftButton(craft, "升魔法", P12MapCraftOperation.AwakenMagic);
        AddCraftButton(craft, "升稀有", P12MapCraftOperation.AlchemicalRare);
        AddCraftButton(craft, "混沌重铸", P12MapCraftOperation.ChaosReroll);
        AddCraftButton(craft, "腐化", P12MapCraftOperation.Corrupt);
        warehouse.AddChild(new Label { Text = "批量筛选器（打造与出售各自保存快照）" });
        var filterBar = new HFlowContainer(); warehouse.AddChild(filterBar);
        var filterMinTier = new SpinBox { MinValue = 1, MaxValue = 20, Value = 1, Prefix = "T" }; filterBar.AddChild(filterMinTier);
        var filterMaxTier = new SpinBox { MinValue = 1, MaxValue = 20, Value = 20, Prefix = "～T" }; filterBar.AddChild(filterMaxTier);
        var filterMinMonster = new SpinBox { MinValue = 0, MaxValue = 120, Step = 5, Value = 0, Prefix = "怪物≥", Suffix = "%" }; filterBar.AddChild(filterMinMonster);
        var filterMinItem = new SpinBox { MinValue = 0, MaxValue = 250, Step = 5, Value = 0, Prefix = "物品≥", Suffix = "%" }; filterBar.AddChild(filterMinItem);
        var filterQuality = new SpinBox { MinValue = 0, MaxValue = 20, Value = 0, Prefix = "品质≥" }; filterBar.AddChild(filterQuality);
        (MenuButton filterArea, HashSet<int> filterAreaIds) = AddMultiSelect(filterBar, "区域（全部）",
            P12MapCatalog.Areas.Select((area, index) => (area.DisplayName, index)));
        (MenuButton filterRequired, HashSet<int> filterRequiredIds) = AddMultiSelect(filterBar, "必含词缀（无）",
            P26MapAffixCatalog.All.Select(definition => (definition.DisplayName, (int)definition.Kind)));
        (MenuButton filterExcluded, HashSet<int> filterExcludedIds) = AddMultiSelect(filterBar, "排除词缀（无）",
            P26MapAffixCatalog.All.Select(definition => (definition.DisplayName, (int)definition.Kind)));
        var filterRarity = new OptionButton(); filterRarity.AddItem("全部稀有度", -1); filterRarity.AddItem("普通", (int)P12MapRarity.Basic);
        filterRarity.AddItem("魔法", (int)P12MapRarity.Magic); filterRarity.AddItem("稀有", (int)P12MapRarity.Rare); filterBar.AddChild(filterRarity);
        var filterCorruption = new OptionButton(); filterCorruption.AddItem("腐化不限", 0); filterCorruption.AddItem("仅未腐化", 1); filterCorruption.AddItem("仅腐化", 2); filterBar.AddChild(filterCorruption);

        P26MapFilter WarehouseFilter()
        {
            int rarityId = filterRarity.GetItemId(filterRarity.Selected);
            int corruption = filterCorruption.GetItemId(filterCorruption.Selected);
            return new P26MapFilter((int)filterMinTier.Value, (int)filterMaxTier.Value,
                (int)filterMinItem.Value * 100, 25_000, (int)filterMinMonster.Value * 100, 12_000,
                filterAreaIds.Select(index => P12MapCatalog.Areas[index].StableId).ToArray(),
                rarityId < 0 ? [] : [(P12MapRarity)rarityId], corruption != 2, corruption != 1,
                (int)filterQuality.Value,
                filterRequiredIds.Select(id => (P12MapAffixKind)id).ToArray(),
                filterExcludedIds.Select(id => (P12MapAffixKind)id).ToArray());
        }
        var batchRules = new HFlowContainer(); warehouse.AddChild(batchRules);
        var batchQuality = new SpinBox { MinValue = 0, MaxValue = 20, Step = 5, Value = 20, Prefix = "品质 " }; batchRules.AddChild(batchQuality);
        var batchRarity = new OptionButton(); batchRarity.AddItem("升魔法", (int)P12MapRarity.Magic); batchRarity.AddItem("升稀有", (int)P12MapRarity.Rare); batchRarity.Select(1); batchRules.AddChild(batchRarity);
        var batchExclude = new OptionButton(); batchExclude.AddItem("不排除词缀", -1);
        foreach (P12MapAffixKind kind in Enum.GetValues<P12MapAffixKind>()) batchExclude.AddItem($"排除 {kind}", (int)kind);
        batchRules.AddChild(batchExclude);
        var batchBudget = new SpinBox { MinValue = 1, MaxValue = 30, Value = 8, Prefix = "单图预算 " }; batchRules.AddChild(batchBudget);
        var batchCorrupt = new CheckBox { Text = "最终腐化（10%摧毁）", TooltipText = "只处理未腐化稀有地图；腐化铁必定消耗，地图有 10% 概率永久摧毁，其余四种规则各 22.5%。" }; batchRules.AddChild(batchCorrupt);
        var batchFailure = new OptionButton(); batchFailure.AddItem("失败保留继续", (int)P12BatchFailureBehavior.Keep); batchFailure.AddItem("失败跳过", (int)P12BatchFailureBehavior.Skip); batchFailure.AddItem("失败即停", (int)P12BatchFailureBehavior.Stop); batchRules.AddChild(batchFailure);
        var batch = new Button { Text = "执行批量制图", TooltipText = "材料消耗严格受单图预算限制。" };
        batch.Pressed += () =>
        {
            int excludedId = batchExclude.GetItemId(batchExclude.Selected);
            P26MapFilter filter = WarehouseFilter();
            P12MapBatchResult result = _session!().BatchCraftMaps(new P12MapBatchRule(
                (P12MapRarity)batchRarity.GetItemId(batchRarity.Selected), (int)batchQuality.Value,
                excludedId < 0 ? [] : [(P12MapAffixKind)excludedId], (int)batchBudget.Value,
                batchCorrupt.ButtonPressed, (P12BatchFailureBehavior)batchFailure.GetItemId(batchFailure.Selected)), filter);
            _mapSignature = string.Empty; _changed?.Invoke(result.Summary); RefreshState();
        };
        warehouse.AddChild(batch);
        var sell = new Button { Text = "出售筛选结果", TooltipText = "锁定、运行中、手动优先队列和任务地图不会出售。" };
        sell.Pressed += () =>
        {
            (int sold, int gold) = _session!().SellMaps(WarehouseFilter());
            _selectedMapIndex = -1; _mapSignature = string.Empty;
            _changed?.Invoke($"已出售 {sold} 张地图，获得 {gold} 金币。"); RefreshState();
        };
        warehouse.AddChild(sell);
        var autoSell = new Button { Text = "设为满仓自动出售筛选器", TooltipText = "库存超过 2000 张时优先出售符合该快照且未受保护的低价值地图。" };
        autoSell.Pressed += () =>
        {
            _session!().SetMapAutoSellFilter(WarehouseFilter());
            _changed?.Invoke("已保存独立的满仓自动出售筛选器快照。");
        };
        warehouse.AddChild(autoSell);
        var warehouseScroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(325, 0),
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        warehouseScroll.AddChild(warehouse);
        body.AddChild(Frame(warehouseScroll));

        var dispatches = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        dispatches.AddChild(BuildTeamCard(ExpeditionTeamKind.Hero, "主角"));
        dispatches.AddChild(BuildTeamCard(ExpeditionTeamKind.Mercenaries, "佣兵队"));
        var help = new Label
        {
            Text = "选择队伍目标后开始即可。地图耗尽、连续失败 3 次或缺少 Boss 门票时会明确停止。",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        dispatches.AddChild(help);
        var dispatchScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        dispatchScroll.AddChild(dispatches);
        body.AddChild(dispatchScroll);

        var reportToggle = new Button { Text = "展开最近 50 次战斗报告", ToggleMode = true };
        AddChild(reportToggle);
        _reports = new VBoxContainer { Visible = false };
        AddChild(_reports);
        reportToggle.Toggled += expanded =>
        {
            _reports.Visible = expanded;
            reportToggle.Text = expanded ? "收起战斗报告" : "展开最近 50 次战斗报告";
            if (expanded)
            {
                _reportSignature = string.Empty;
                ReportsViewed?.Invoke();
                RefreshState();
            }
        };
    }

    public void RefreshState()
    {
        if (_session is null || _resources is null || _mapInventory is null || _reports is null)
        {
            return;
        }

        P1GameSession session = _session();
        int formalMapCount = Math.Min(200, session.World.MapInventory.Count);
        for (int index = 0; index < formalMapCount; index++)
        {
            P1MapItem source = session.World.MapInventory[index];
            P1MapItem formal = source.EnsureFormal(session.Seed ^ (ulong)index);
            if (!ReferenceEquals(source, formal)) session.World.MapInventory[index] = formal;
        }
        var mapGroups = session.World.MapInventory
            .Select((map, index) => (Map: map, Index: index))
            .GroupBy(entry => (entry.Map.Tier, entry.Map.AreaId))
            .OrderByDescending(group => group.Key.Tier)
            .ThenBy(group => group.Key.AreaId, StringComparer.Ordinal)
            .ToArray();
        _resources.Text =
            $"地图 {session.World.MapInventory.Count}　深渊监守者碎片 {session.World.Expedition.AbyssWardenFragments}/{P5ExpeditionDirector.FragmentsPerTicket}　" +
            $"Boss 门票 {session.World.Expedition.AbyssWardenTickets}";
        string mapSignature = $"{session.World.Expedition.AbyssWardenFragments}:{session.World.Expedition.AbyssWardenTickets}:" +
            $"{session.World.Expedition.MapsTowardNextFragment}|" +
            string.Join(',', session.World.MapInventory.Select(map => $"{map.InstanceId}:{map.Tier}:{map.Rarity}:{map.Quality}:{map.CorruptionRule}:{map.SelectedRoute}:{string.Join('-', map.EffectiveAffixes.Select(affix => affix.Kind))}"));
        if (mapSignature != _mapSignature)
        {
            _mapSignature = mapSignature;
            Clear(_mapInventory);
            if (_selectedMapIndex >= session.World.MapInventory.Count) _selectedMapIndex = session.World.MapInventory.Count - 1;
            _mapInventory.AddChild(new Label
            {
                Text = "地图仓按阶级与区域汇总；点击一组可预览并操作其中价值最高的一张。",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                Modulate = new Color("b7c1d4"),
            });
            foreach (var group in mapGroups)
            {
                var entries = group.ToArray();
                var representative = entries
                    .OrderByDescending(entry => entry.Map.IsCorrupted)
                    .ThenByDescending(entry => entry.Map.Rarity)
                    .ThenByDescending(entry => entry.Map.ItemQuantityBonusBasisPoints)
                    .ThenByDescending(entry => entry.Map.MonsterQuantityBasisPoints)
                    .ThenByDescending(entry => entry.Map.Quality)
                    .ThenBy(entry => entry.Map.AcquiredSequence)
                    .First();
                int mapIndex = representative.Index;
                P1MapItem map = representative.Map;
                P12MapArea area = ResolveArea(map);
                int rare = entries.Count(entry => entry.Map.Rarity == P12MapRarity.Rare);
                int magic = entries.Count(entry => entry.Map.Rarity == P12MapRarity.Magic);
                int corrupted = entries.Count(entry => entry.Map.IsCorrupted);
                int locked = entries.Count(entry => entry.Map.IsLocked);
                bool selected = _selectedMapIndex >= 0 && _selectedMapIndex < session.World.MapInventory.Count &&
                    session.World.MapInventory[_selectedMapIndex].Tier == group.Key.Tier &&
                    string.Equals(session.World.MapInventory[_selectedMapIndex].AreaId, group.Key.AreaId, StringComparison.Ordinal);
                var button = new Button
                {
                    Text = $"T{map.Tier} · {area.DisplayName}　×{entries.Length}　稀有 {rare} / 魔法 {magic}" +
                           (corrupted > 0 ? $"　腐化 {corrupted}" : string.Empty) +
                           (locked > 0 ? $"　锁定 {locked}" : string.Empty),
                    Alignment = HorizontalAlignment.Left,
                    TooltipText = $"该组共 {entries.Length} 张；最高物品数量 +{entries.Max(entry => entry.Map.ItemQuantityBonusBasisPoints) / 100.0:0}%、" +
                                  $"最高怪物数量 +{entries.Max(entry => entry.Map.MonsterQuantityBasisPoints) / 100.0:0}%。\n点击后选中组内价值最高的一张进行单图操作。",
                    ButtonPressed = selected,
                    ToggleMode = true,
                };
                button.Pressed += () => { _selectedMapIndex = mapIndex; _mapSignature = string.Empty; RefreshState(); };
                _mapInventory.AddChild(button);
            }
            if (session.World.MapInventory.Count == 0) _mapInventory.AddChild(new Label { Text = "地图仓库为空" });
            _mapInventory.AddChild(new HSeparator());
            _mapInventory.AddChild(new Label
            {
                Text = $"碎片进度：地图 Boss {session.World.Expedition.MapsTowardNextFragment}/{P5ExpeditionDirector.MapsPerFragment}\n" +
                       $"深渊监守者碎片 {session.World.Expedition.AbyssWardenFragments}/{P5ExpeditionDirector.FragmentsPerTicket}\n" +
                       $"完整门票 ×{session.World.Expedition.AbyssWardenTickets}",
            });
            RefreshMapDetails(session);
        }

        string reportSignature = string.Join('|', session.World.Expedition.Reports.Select(report => report.StableId));
        if (_reports.Visible && reportSignature != _reportSignature)
        {
            _reportSignature = reportSignature;
            Clear(_reports);
            foreach (P6CombatReport report in session.World.Expedition.Reports.Reverse())
            {
                var card = new VBoxContainer();
                card.AddChild(new Label { Text = $"{report.Context} · {report.Outcome} · {report.DurationMilliseconds / 1_000.0:0.0}s" + (report.Offline ? " · 离线" : string.Empty) });
                string skills = report.Skills.Count == 0 ? "无有效输出" : string.Join(" · ", report.Skills.Take(6).Select(skill => $"{skill.Skill} {skill.Damage}({skill.DamageBasisPoints / 100.0:0.#}%)/{skill.Uses}次"));
                string sources = report.DamageSources.Count == 0 ? "无承伤" : string.Join(" · ", report.DamageSources.Take(4).Select(source => $"{source.Source} {source.Damage}({source.DamageBasisPoints / 100.0:0.#}%)"));
                string supports = report.Supports.Count == 0 ? "无可归因辅助触发" : string.Join(" · ", report.Supports.Take(6).Select(support => $"{support.Support} {support.Triggers}次/贡献约{support.EstimatedDamageContribution:+#;-#;0}"));
                card.AddChild(new Label
                {
                    Text = $"输出 {report.DamageDealt}：{skills}\n辅助：{supports}\n承伤 {report.DamageTaken}：{sources}\n" +
                           $"战吼覆盖 {report.WarCryCoverageBasisPoints / 100.0:0.#}% · 战旗覆盖 {report.BannerCoverageBasisPoints / 100.0:0.#}% · " +
                           $"护盾覆盖 {report.ShieldCoverageBasisPoints / 100.0:0.#}% · 药剂 {report.FlaskUses}次/+{report.FlaskRecovery} · 击杀充能 +{report.FlaskChargesGained} · 资源失败 {report.ResourceFailureCount}" +
                           (string.IsNullOrEmpty(report.TimeoutReason) ? string.Empty : $"\n超时归因：{report.TimeoutReason}") +
                           $"\n最后 5 秒：{string.Join("；", report.LastFiveSeconds.TakeLast(12))}",
                    AutowrapMode = TextServer.AutowrapMode.WordSmart,
                });
                if (report.DeathReport is { } death)
                {
                    card.AddChild(new Label
                    {
                        Text = $"死亡归因：{death.FatalSkill} · {death.RawDamageType} {death.FatalDamage} · " +
                               $"可规避：{(death.Avoidable ? "是" : "否")}\n" +
                               $"防御层：{string.Join('、', death.DefensiveLayers)} · 异常：" +
                               (death.Ailments.Count == 0 ? "无" : string.Join('、', death.Ailments)),
                        AutowrapMode = TextServer.AutowrapMode.WordSmart,
                    });
                }
                _reports.AddChild(Frame(card));
            }
            if (session.World.Expedition.Reports.Count == 0) _reports.AddChild(new Label { Text = "尚无战斗报告；完成主线战斗或远征后自动生成。" });
        }

        string dispatchSignature = string.Join('|', session.World.Expedition.Dispatches.Values.OrderBy(item => item.Team));
        foreach ((ExpeditionTeamKind kind, TeamControls controls) in _teams)
        {
            P1TeamExpeditionState team = kind == ExpeditionTeamKind.Hero ? session.World.Hero : session.World.Mercenaries;
            P5TeamDispatchSnapshot? dispatch = session.World.Expedition.Get(kind);
            if (dispatch is not null && dispatchSignature != _dispatchSignature)
            {
                controls.Target.Select(controls.Target.GetItemIndex((int)dispatch.Target));
                controls.Mode.Select(controls.Mode.GetItemIndex((int)dispatch.Mode));
                controls.Mode.Disabled = IsBossTarget(dispatch.Target);
            }
            controls.Status.Text = TeamStatus(team, dispatch);
        }
        _dispatchSignature = dispatchSignature;
    }

    private Control BuildTeamCard(ExpeditionTeamKind kind, string title)
    {
        var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddChild(new Label { Text = title });
        var selectors = new HFlowContainer();
        content.AddChild(selectors);
        var target = new OptionButton();
        target.AddItem("安全探索", (int)P5ExpeditionTarget.SafeMaps);
        target.AddItem("裂渊追猎", (int)P5ExpeditionTarget.AbyssMaps);
        target.AddItem("命能花园", (int)P5ExpeditionTarget.LifeGardenMaps);
        target.AddItem("亡旗战阵", (int)P5ExpeditionTarget.WarfrontMaps);
        target.AddItem("最高阶推进", (int)P5ExpeditionTarget.HighestTierMaps);
        target.AddItem("深渊监守者", (int)P5ExpeditionTarget.AbyssWarden);
        target.AddItem("Boss 练习", (int)P5ExpeditionTarget.AbyssWardenPractice);
        selectors.AddChild(target);
        var mode = new OptionButton();
        mode.AddItem("执行一次", (int)P5DispatchMode.Once);
        mode.AddItem("重复同类", (int)P5DispatchMode.Repeat);
        mode.AddItem("最高阶持续推进", (int)P5DispatchMode.HighestAvailable);
        selectors.AddChild(mode);
        var minimumTier = new SpinBox { MinValue = 1, MaxValue = 20, Value = 1, Prefix = "最低 T" }; selectors.AddChild(minimumTier);
        var maximumTier = new SpinBox { MinValue = 1, MaxValue = 20, Value = 20, Prefix = "最高 T" }; selectors.AddChild(maximumTier);
        var minimumMonsterQuantity = new SpinBox { MinValue = 0, MaxValue = 120, Step = 5, Value = 0, Prefix = "怪物至少 ", Suffix = "%" }; selectors.AddChild(minimumMonsterQuantity);
        var minimumItemQuantity = new SpinBox { MinValue = 0, MaxValue = 250, Step = 5, Value = 0, Prefix = "物品至少 ", Suffix = "%" }; selectors.AddChild(minimumItemQuantity);
        var minimumQuality = new SpinBox { MinValue = 0, MaxValue = 20, Step = 1, Value = 0, Prefix = "品质至少 " }; selectors.AddChild(minimumQuality);
        (_, HashSet<int> areaIds) = AddMultiSelect(selectors, "区域（全部）",
            P12MapCatalog.Areas.Select((area, index) => (area.DisplayName, index)));
        var rarityFilter = new OptionButton(); rarityFilter.AddItem("稀有度不限", -1); rarityFilter.AddItem("普通", (int)P12MapRarity.Basic);
        rarityFilter.AddItem("魔法", (int)P12MapRarity.Magic); rarityFilter.AddItem("稀有", (int)P12MapRarity.Rare); selectors.AddChild(rarityFilter);
        var corruptionFilter = new OptionButton(); corruptionFilter.AddItem("腐化不限", 0); corruptionFilter.AddItem("仅未腐化", 1); corruptionFilter.AddItem("仅腐化", 2); selectors.AddChild(corruptionFilter);
        (_, HashSet<int> requiredAffixes) = AddMultiSelect(selectors, "必含词缀（无）",
            P26MapAffixCatalog.All.Select(definition => (definition.DisplayName, (int)definition.Kind)));
        (_, HashSet<int> excludedAffixes) = AddMultiSelect(selectors, "排除词缀（无）",
            P26MapAffixCatalog.All.Select(definition => (definition.DisplayName, (int)definition.Kind)));
        var order = new OptionButton(); order.AddItem("推荐排序", (int)P26MapOrder.Recommended); order.AddItem("低阶优先", (int)P26MapOrder.TierAscending);
        order.AddItem("最早入库优先", (int)P26MapOrder.OldestFirst); selectors.AddChild(order);
        var noMatch = new OptionButton(); noMatch.AddItem("无匹配时等待", (int)P26NoMatchBehavior.Wait); noMatch.AddItem("无匹配时停止", (int)P26NoMatchBehavior.Stop); selectors.AddChild(noMatch);
        var altar = new OptionButton(); altar.AddItem("祭坛不限", (int)MapAltarPreference.Any); altar.AddItem("避开祭坛", (int)MapAltarPreference.Avoid); altar.AddItem("偏好赤誓", (int)MapAltarPreference.RedOath); altar.AddItem("偏好苍誓", (int)MapAltarPreference.BlueOath); selectors.AddChild(altar);
        var blockAbyss = new CheckBox { Text = "屏蔽裂渊" }; selectors.AddChild(blockAbyss);
        var blockGarden = new CheckBox { Text = "屏蔽花园" }; selectors.AddChild(blockGarden);
        var fragments = new CheckBox { Text = "使用稀有碎片" }; selectors.AddChild(fragments);
        target.ItemSelected += index =>
        {
            P5ExpeditionTarget selected = (P5ExpeditionTarget)target.GetItemId((int)index);
            mode.Disabled = IsBossTarget(selected);
            if (mode.Disabled)
            {
                mode.Select(mode.GetItemIndex((int)P5DispatchMode.Once));
            }
        };
        var start = new Button { Text = "开始派遣" };
        start.Pressed += () =>
        {
            P5ExpeditionTarget selectedTarget = (P5ExpeditionTarget)target.GetItemId(target.Selected);
            P5DispatchMode selectedMode = (P5DispatchMode)mode.GetItemId(mode.Selected);
            P1TeamExpeditionState team = kind == ExpeditionTeamKind.Hero ? _session!().World.Hero : _session!().World.Mercenaries;
            MapRoute preferred = selectedTarget switch
            {
                P5ExpeditionTarget.AbyssMaps => MapRoute.Abyss,
                P5ExpeditionTarget.LifeGardenMaps => MapRoute.LifeGarden,
                P5ExpeditionTarget.WarfrontMaps => MapRoute.Warfront,
                _ => MapRoute.Safe,
            };
            var blocked = new List<MapRoute>();
            if (blockAbyss.ButtonPressed && preferred != MapRoute.Abyss) blocked.Add(MapRoute.Abyss);
            if (blockGarden.ButtonPressed && preferred != MapRoute.LifeGarden) blocked.Add(MapRoute.LifeGarden);
            int rarityId = rarityFilter.GetItemId(rarityFilter.Selected);
            int corruption = corruptionFilter.GetItemId(corruptionFilter.Selected);
            _session!().SetExpeditionPolicy(kind, team.Policy with
            {
                PreferredRoute = preferred,
                RoutePriority = new[] { preferred, MapRoute.Safe, MapRoute.LifeGarden, MapRoute.Abyss, MapRoute.Warfront }.Distinct().ToArray(),
                BlockedRoutes = blocked,
                MaximumMapTier = (int)maximumTier.Value,
                MapFilter = new P26MapFilter((int)minimumTier.Value, (int)maximumTier.Value,
                    (int)minimumItemQuantity.Value * 100, 25_000, (int)minimumMonsterQuantity.Value * 100,
                    12_000, areaIds.Select(index => P12MapCatalog.Areas[index].StableId).ToArray(),
                    rarityId < 0 ? [] : [(P12MapRarity)rarityId], corruption != 2, corruption != 1,
                    (int)minimumQuality.Value,
                    requiredAffixes.Select(id => (P12MapAffixKind)id).ToArray(),
                    excludedAffixes.Select(id => (P12MapAffixKind)id).ToArray()),
                MapOrder = (P26MapOrder)order.GetItemId(order.Selected),
                NoMatchBehavior = (P26NoMatchBehavior)noMatch.GetItemId(noMatch.Selected),
                AltarPreference = (MapAltarPreference)altar.GetItemId(altar.Selected),
                UseRareFragments = fragments.ButtonPressed,
            });
            _session!().AssignExpedition(kind, selectedTarget, selectedMode);
            _mapSignature = string.Empty;
            _changed?.Invoke($"{title}已派往{TargetName(selectedTarget)}。");
            RefreshState();
        };
        selectors.AddChild(start);
        var stop = new Button { Text = "停止" };
        stop.Pressed += () =>
        {
            _session!().CancelExpedition(kind);
            _mapSignature = string.Empty;
            _changed?.Invoke($"{title}已停止派遣。");
            RefreshState();
        };
        selectors.AddChild(stop);
        var status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        content.AddChild(status);
        _teams.Add(kind, new TeamControls(target, mode, status));
        return Frame(content);
    }

    private static string TeamStatus(P1TeamExpeditionState team, P5TeamDispatchSnapshot? dispatch)
    {
        if (team.ActiveMap is not null)
        {
            string target = P10EndgameState.IsCitadel(team.ActiveMap) ? "灰烬天垒" :
                P5ExpeditionDirector.IsPractice(team.ActiveMap) ? "Boss 练习" :
                P5ExpeditionDirector.IsBoss(team.ActiveMap) ? "深渊监守者" : $"T{team.ActiveMap.Tier} · Lv{team.ActiveMap.MonsterLevel} 地图";
            return $"执行中：{target} · 剩余约 {Math.Max(1, team.RemainingMapTimeMilliseconds / 1_000)} 秒\n" +
                   $"路线 {team.ActiveRoute} · 最近 {(team.LastRun?.Succeeded == true ? "成功" : team.LastRun is null ? "暂无结算" : "失败")}";
        }

        if (team.IsStopped)
        {
            return $"已停止：{StopReason(team.StopReason)}\n完成 {team.MapsCompleted} · 失败 {team.MapsFailed}";
        }

        if (team.Queue.Count > 0)
        {
            P1MapItem map = team.Queue.Maps[0];
            return $"准备中：{TargetName(dispatch?.Target ?? P5ExpeditionTarget.SafeMaps)} · T{map.Tier} · Lv{map.MonsterLevel}";
        }

        return dispatch is null ? "空闲：请选择目标。" :
            $"{(dispatch.Enabled ? "等待执行" : "已完成本次派遣")}：{TargetName(dispatch.Target)}\n完成 {team.MapsCompleted} · 失败 {team.MapsFailed}";
    }

    private static string TeamSignature(P1TeamExpeditionState team) =>
        $"{team.Kind}:{team.ActiveMap?.InstanceId}:{team.Queue.Count}:{team.IsStopped}:{team.StopReason}:" +
        $"{team.MapsCompleted}:{team.MapsFailed}:{team.RemainingMapTimeMilliseconds / 1_000}";

    private static string TargetName(P5ExpeditionTarget target) => target switch
    {
        P5ExpeditionTarget.SafeMaps => "安全探索",
        P5ExpeditionTarget.AbyssMaps => "裂渊追猎",
        P5ExpeditionTarget.LifeGardenMaps => "命能花园",
        P5ExpeditionTarget.WarfrontMaps => "亡旗战阵",
        P5ExpeditionTarget.HighestTierMaps => "最高阶推进",
        P5ExpeditionTarget.AbyssWarden => "深渊监守者",
        P5ExpeditionTarget.AbyssWardenPractice => "Boss 练习",
        _ => "未知目标",
    };

    private static bool IsBossTarget(P5ExpeditionTarget target) =>
        target is P5ExpeditionTarget.AbyssWarden or P5ExpeditionTarget.AbyssWardenPractice;

    private static string StopReason(string reason) => reason switch
    {
        "maps_exhausted" => "没有可用地图",
        "boss_ticket_missing" => "缺少 Boss 门票",
        "consecutive_failures" => "连续失败达到 3 次",
        "storage_full" => "仓库已满",
        "tier_locked" => "T17–T20 尚未通过门扉突破",
        "map_policy_limit" => "地图不符合本队筛选条件或阶级上限",
        "manual_stop" => "玩家手动停止",
        "cancelled" => "玩家取消派遣",
        _ when string.IsNullOrWhiteSpace(reason) => "等待重新派遣",
        _ => reason,
    };

    private static PanelContainer Frame(Control content)
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("151a22"),
            BorderColor = new Color("786747"),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            ContentMarginLeft = 10,
            ContentMarginTop = 8,
            ContentMarginRight = 10,
            ContentMarginBottom = 8,
        });
        panel.AddChild(content);
        return panel;
    }

    private static void Clear(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            child.QueueFree();
        }
    }

    private static (MenuButton Button, HashSet<int> Selected) AddMultiSelect(Control parent, string emptyText,
        IEnumerable<(string Label, int Id)> options)
    {
        var selected = new HashSet<int>();
        var button = new MenuButton { Text = emptyText, TooltipText = "可多选；再次点击取消。" };
        PopupMenu popup = button.GetPopup();
        foreach ((string label, int id) in options) popup.AddCheckItem(label, id);
        popup.IdPressed += id =>
        {
            int value = (int)id;
            int itemIndex = popup.GetItemIndex(value);
            bool enabled = !popup.IsItemChecked(itemIndex);
            popup.SetItemChecked(itemIndex, enabled);
            if (enabled) selected.Add(value); else selected.Remove(value);
            button.Text = selected.Count == 0 ? emptyText : $"已选 {selected.Count} 项";
        };
        parent.AddChild(button);
        return (button, selected);
    }

    private void AddCraftButton(Control parent, string text, P12MapCraftOperation operation)
    {
        var button = new Button { Text = text };
        button.Pressed += () =>
        {
            if (_selectedMapIndex < 0) { _changed?.Invoke("请先选择一张路印。"); return; }
            P12MapCraftResult result = _session!().CraftMap(_selectedMapIndex, operation);
            if (result.Destroyed) _selectedMapIndex = -1;
            _mapSignature = string.Empty;
            _changed?.Invoke(result.Destroyed ? "腐化铁已消耗，地图被永久摧毁。" :
                result.Succeeded ? $"制图完成：{text}。" : $"制图失败：{result.Summary}。");
            RefreshState();
        };
        parent.AddChild(button);
    }

    private void RefreshMapDetails(P1GameSession session)
    {
        if (_mapDetails is null) return;
        if (_selectedMapIndex < 0 || _selectedMapIndex >= session.World.MapInventory.Count)
        { _mapDetails.Text = "选择一个地图分组后，可预览该组价值最高的一张并进行单图加工。批量打造、出售和远征仍统一使用筛选器。"; return; }
        P1MapItem map = session.World.MapInventory[_selectedMapIndex];
        _mapDetails.Text = "当前组代表地图：\n" + DescribeMap(map);
        var lockButton = new Button { Text = map.IsLocked ? "解除地图锁定" : "锁定地图",
            TooltipText = "锁定地图不会被批量打造、出售、满仓自动出售或自动远征消耗。" };
        lockButton.Pressed += () =>
        {
            _session!().TrySetMapLocked(_selectedMapIndex, !map.IsLocked);
            _mapSignature = string.Empty; _changed?.Invoke(map.IsLocked ? "地图已解除锁定。" : "地图已锁定。"); RefreshState();
        };
        _mapInventory!.AddChild(lockButton);
        var routeBar = new HFlowContainer();
        _mapInventory.AddChild(new Label { Text = "选定路线（每图仅一条）：" });
        _mapInventory.AddChild(routeBar);
        foreach (MapRoute route in map.EffectiveRouteCandidates)
        {
            var button = new Button { Text = RouteName(route), ToggleMode = true, ButtonPressed = map.SelectedRoute == route };
            button.Pressed += () =>
            {
                _session!().TrySelectMapRoute(_selectedMapIndex, route);
                _mapSignature = string.Empty; _changed?.Invoke($"已选路线：{RouteName(route)}。"); RefreshState();
            };
            routeBar.AddChild(button);
        }
    }

    private static string DescribeMap(P1MapItem map)
    {
        P12MapArea area = ResolveArea(map);
        MapRoute route = map.SelectedRoute ?? map.EffectiveRouteCandidates.FirstOrDefault();
        P14MapPlan plan = P14MapPlanner.Build(map, route, map.AtlasSnapshot ?? [], 0);
        P14PreflightReport preflight = P14Preflight.ForMap(map, P14Bosses.ForArea(area.StableId));
        string affixes = map.EffectiveAffixes.Count == 0 ? "无显式词缀" :
            string.Join("；", map.EffectiveAffixes.Select(affix =>
            {
                P26MapAffixDefinition definition = P26MapAffixCatalog.Get(affix.Kind);
                string family = affix.Family == P26MapAffixFamily.DangerousPrefix ? "危险前缀" : "收益后缀";
                return $"{family} R{affix.Rank} {affix.DisplayName}：{string.Format(definition.EffectTemplate, affix.Value)}";
            }));
        string routes = string.Join(" / ", map.EffectiveRouteCandidates.Select(RouteName));
        string altar = map.Altar switch { P12MapAltar.RedOath => "赤誓祭坛", P12MapAltar.BlueOath => "苍誓祭坛", _ => "无祭坛" };
        string chain = string.Join(" → ", plan.Nodes.Select(node => node.DisplayName));
        string corruption = map.CorruptionRule == P26CorruptionRule.None ? string.Empty : $" · 腐化规则 {map.CorruptionRule}";
        return $"{area.DisplayName} · {area.Environment} · Boss {area.BossName}\nT{map.Tier} · 怪物等级 {map.MonsterLevel} · {RarityMark(map.Rarity)} · 品质 {map.Quality}% · 怪物数量 +{map.MonsterQuantityBasisPoints / 100.0:0}% · 物品数量 +{map.ItemQuantityBonusBasisPoints / 100.0:0}%{corruption}\n{affixes}\n候选 {routes} · {altar}\n节点链：{chain}\n战前：{string.Join('、', preflight.DamageTypes)} · {preflight.EnrageCondition}";
    }

    private static P12MapArea ResolveArea(P1MapItem map) =>
        P12MapCatalog.TryGet(map.AreaId, out P12MapArea area)
            ? area
            : new P12MapArea(map.AreaId, "未登记路印", "未知区域", "未知敌群", "未知首领");

    private static string RarityMark(P12MapRarity rarity) => rarity switch
    { P12MapRarity.Basic => "普通", P12MapRarity.Magic => "魔法", _ => "稀有" };
    private static string RouteName(MapRoute route) => route switch
    { MapRoute.Safe => "安全探索", MapRoute.Abyss => "裂渊追猎", MapRoute.LifeGarden => "命能花园", _ => "亡旗战阵" };

    private sealed record TeamControls(OptionButton Target, OptionButton Mode, Label Status);
}
