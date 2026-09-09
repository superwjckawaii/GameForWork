using GameForWork.Core.Campaign;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Harbor;
using GameForWork.Core.Spatial;
using Godot;

namespace GameForWork.GodotClient;

public partial class HarborPanel : VBoxContainer
{
    private Func<GameSession> _session = null!;
    private Action<string> _changed = null!;
    private readonly OptionButton _team = new();
    private readonly OptionButton _difficulty = new();
    private readonly SpinBox _runs = new() { MinValue = 1, MaxValue = 999, Value = 1 };
    private readonly CheckBox _continue = new() { Text = "失败后继续" };
    private readonly Label _status = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly VBoxContainer _chests = new();
    private readonly HarborReplayView _view = new();
    private string _chestSignature = "";
    private readonly OptionButton _chestDifficulty = new();
    private readonly Label _chestPage = new();
    private int _page;
    private const int ChestsPerPage = 10;

    public void Initialize(Func<GameSession> session, Action<string> changed)
    {
        _session = session; _changed = changed;
        Name = "沉金港";
        AddChild(new Label { Text = "百级突破后解锁；考验实际移速与生存。三个港区全自动，无提前撤离。\n当前候选池：18件沉金港特殊巅峰底材与 8 件成功箱专属传奇。", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        var controls = new HFlowContainer();
        _team.AddItem("主角队伍"); _team.AddItem("佣兵远征队");
        foreach (HarborDifficulty difficulty in HarborDifficulty.All)
            _difficulty.AddItem($"难度{difficulty.Level} · {difficulty.Fee}金币 · 敌人{difficulty.ItemLevel}级");
        controls.AddChild(_team); controls.AddChild(_difficulty); controls.AddChild(new Label { Text = "次数" });
        controls.AddChild(_runs); controls.AddChild(_continue);
        AddButton(controls, "开始", () => _session().StartHarbor(SelectedTeam, _difficulty.Selected + 1, (int)_runs.Value, _continue.ButtonPressed)
            ? "港口行动已开始，金币已支付。" : "无法开始：需要百级突破、空闲队伍及足够金币。");
        AddButton(controls, "停止重复", () => _session().StopHarborRepeat(SelectedTeam) ? "当前局完成后停止。" : "没有进行中的港口行动。");
        var cancel = new ConfirmationDialog { Title = "放弃港口行动", DialogText = "立即放弃本局，不退金币且没有奖励。", OkButtonText = "放弃", CancelButtonText = "返回" };
        AddChild(cancel);
        ExpeditionTeamKind cancelTeam = ExpeditionTeamKind.Hero;
        cancel.Confirmed += () => { _changed(_session().CancelHarbor(cancelTeam) ? "已放弃港口行动。" : "行动已经结束。"); RefreshState(); };
        var cancelButton = new Button { Text = "立即放弃" };
        cancelButton.Pressed += () => { cancelTeam = SelectedTeam; cancel.PopupCentered(); };
        controls.AddChild(cancelButton); AddChild(controls); AddChild(_status);
        _view.CustomMinimumSize = new(0, 260); _view.ClipContents = true;
        AddChild(_view);
        var chestControls = new HFlowContainer();
        _chestDifficulty.AddItem("全部港口箱");
        foreach (HarborDifficulty difficulty in HarborDifficulty.All) _chestDifficulty.AddItem($"难度{difficulty.Level}");
        _chestDifficulty.ItemSelected += _ => { _page = 0; _chestSignature = ""; RefreshState(); };
        chestControls.AddChild(_chestDifficulty);
        AddButton(chestControls, "上一页", () => { _page = Math.Max(0, _page - 1); return "浏览宝箱，不会领取奖励。"; });
        AddButton(chestControls, "下一页", () => { _page++; return "浏览宝箱，不会领取奖励。"; });
        chestControls.AddChild(_chestPage);
        AddChild(chestControls);
        var scroll = new ScrollContainer { CustomMinimumSize = new(0, 140), SizeFlagsVertical = SizeFlags.ExpandFill };
        _chests.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(_chests); AddChild(scroll);
    }

    private ExpeditionTeamKind SelectedTeam => _team.Selected == 0 ? ExpeditionTeamKind.Hero : ExpeditionTeamKind.Mercenaries;
    private void AddButton(Control parent, string title, Func<string> action, string tooltip = "")
    {
        var button = new Button { Text = title, TooltipText = tooltip };
        button.Pressed += () => { _changed(action()); RefreshState(); };
        parent.AddChild(button);
    }

    public void RefreshState()
    {
        GameSession session = _session();
        HarborDispatch? dispatch = session.HarborDispatchFor(SelectedTeam);
        HarborRun? run = dispatch?.ActiveRun;
        HarborRegion? region = run?.Regions.LastOrDefault(value => value.StartsAtMilliseconds <= dispatch!.ElapsedMilliseconds);
        _status.Text = $"已领取金币 {session.World.Economy.Gold} · 待开港口箱 {session.HarborChests.Count}\n" +
            (run is null ? dispatch?.Status ?? "空闲" : $"{region?.Name} · 难度{run.Difficulty} · 已付{run.PaidGold}金币 · 已进行{dispatch!.ElapsedMilliseconds / 1000}秒 · 后续{dispatch.RemainingRuns}次");
        _view.ShowRegion(region, (dispatch?.ElapsedMilliseconds ?? 0) - (region?.StartsAtMilliseconds ?? 0));
        for (int level = 1; level <= 3; level++)
            _chestDifficulty.SetItemText(level, $"难度{level} · {session.HarborChests.Count(chest => chest.Difficulty == level)}箱");
        HarborChest[] filtered = session.HarborChests.Where(chest => _chestDifficulty.Selected == 0 ||
            chest.Difficulty == _chestDifficulty.Selected).ToArray();
        int pages = Math.Max(1, (filtered.Length + ChestsPerPage - 1) / ChestsPerPage);
        _page = Math.Clamp(_page, 0, pages - 1);
        _chestPage.Text = $"第{_page + 1}/{pages}页 · 新获得{filtered.Count(chest => chest.IsNew)}箱";
        string signature = $"{_page}:{_chestDifficulty.Selected}:" + string.Join('|', filtered.Select(chest => $"{chest.Id}:{chest.IsNew}"));
        if (signature == _chestSignature) return;
        _chestSignature = signature;
        foreach (Node child in _chests.GetChildren()) { _chests.RemoveChild(child); child.QueueFree(); }
        foreach (HarborChest chest in filtered.Skip(_page * ChestsPerPage).Take(ChestsPerPage))
        {
            _chests.AddChild(new Label { Text = $"{(chest.IsNew ? "【新】" : "")}沉金港宝箱 · 难度{chest.Difficulty} · 三选一（手选装备锁定保护）" });
            var row = new HFlowContainer();
            if (chest.IsNew) AddButton(row, "标为已查看", () => _session().MarkHarborChestViewed(chest.Id)
                ? "已查看，宝箱与候选奖励完整保留。" : "宝箱状态已经更新。");
            for (int index = 0; index < chest.Candidates.Count; index++)
            {
                int choice = index;
                var item = chest.Candidates[index];
                AddButton(row, $"{item.DisplayName} · 等级{item.ItemLevel} · 品质{item.Quality}\n{item.Base.ImplicitText}",
                    () => _session().ClaimHarborChest(chest.Id, choice) ? "装备已领取并锁定；仓满时进入恢复箱。" : "未领取，宝箱保持原状。",
                    UiText.ItemTooltip(item, includeAffixDetails: true));
            }
            _chests.AddChild(row);
        }
    }
}

public partial class HarborReplayView : Control
{
    private HarborRegion? _region;
    private long _time;
    public void ShowRegion(HarborRegion? region, long milliseconds) { _region = region; _time = milliseconds; QueueRedraw(); }
    private Vector2 Project(Point point) => new(point.XRaw / 12_000f * Size.X, point.YRaw / 24_000f * Size.Y);
    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("182735"));
        if (_region is null) return;
        SpatialFrame? frame = _region.Combat.Frames.LastOrDefault(value => value.AtMilliseconds <= _time);
        if (frame is null) return;
        DrawCircle(Project(new(6_000, 2_000)), 8, Colors.Gold);
        DrawCircle(Project(new(11_000, 2_000)), 8, Colors.LightGreen);
        foreach (SpatialEvent entry in _region.Combat.Events.Where(value => value.Presentation is not null &&
                     value.AtMilliseconds <= _time && value.Presentation.EndsAtMilliseconds > _time))
        {
            SpatialPresentation effect = entry.Presentation!;
            if (effect.Shape == "circle")
            {
                Vector2[] outline = Enumerable.Range(0, 49).Select(index =>
                {
                    double angle = index * Math.Tau / 48;
                    return Project(new(entry.TargetPosition.XRaw + (int)(Math.Cos(angle) * effect.RadiusRaw),
                        entry.TargetPosition.YRaw + (int)(Math.Sin(angle) * effect.RadiusRaw)));
                }).ToArray();
                DrawPolyline(outline, _time < effect.StartsAtMilliseconds ? Colors.Yellow : Colors.OrangeRed, 2);
            }
        }
        foreach (EnemyFrame enemy in frame.Enemies.Where(value => value.Life > 0))
            DrawCircle(Project(enemy.Position), enemy.Boss ? 7 : 4, enemy.Boss ? Colors.Orange : Colors.IndianRed);
        DrawCircle(Project(frame.HeroPosition), 6, Colors.Cyan);
        DrawString(ThemeDB.FallbackFont, new(8, 20), $"原型回放 · 生命 {frame.HeroLife}/{frame.HeroMaximumLife} · 金点宝库 / 绿点出口", fontSize: 14);
    }
}
