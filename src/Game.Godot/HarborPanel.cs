using GameForWork.Core.Campaign;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Harbor;
using GameForWork.Core.Spatial;
using GameForWork.Core.Art;
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
    private double _clock;
    private Texture2D? _regionAtlas;

    public override void _Ready()
    {
        ClipContents = true;
        _regionAtlas = ResourceLoader.Exists("res://assets/art/regions/art-region-atlas.png")
            ? GD.Load<Texture2D>("res://assets/art/regions/art-region-atlas.png")
            : null;
    }

    public override void _Process(double delta)
    {
        _clock += delta;
        if (_region is not null) QueueRedraw();
    }

    public void ShowRegion(HarborRegion? region, long milliseconds) { _region = region; _time = milliseconds; QueueRedraw(); }
    private Vector2 Project(Point point) => new(point.XRaw / 12_000f * Size.X, point.YRaw / 24_000f * Size.Y);
    public override void _Draw()
    {
        Rect2 canvas = new(Vector2.Zero, Size);
        if (_regionAtlas is not null)
        {
            int index = _region?.Name switch { "外港栈桥" => 0, "沉没仓区" => 1, "宝库内港" => 2, _ => 0 };
            Rect2 source = new((index % 4) * _regionAtlas.GetWidth() / 4f,
                (index / 4) * _regionAtlas.GetHeight() / 3f,
                _regionAtlas.GetWidth() / 4f, _regionAtlas.GetHeight() / 3f);
            DrawTextureRectRegion(_regionAtlas, canvas, source);
            DrawRect(canvas, new Color(0.015f, 0.03f, 0.055f, .48f), true);
        }
        else DrawRect(canvas, new Color("182735"), true);
        if (_region is null) return;
        SpatialFrame? frame = _region.Combat.Frames.LastOrDefault(value => value.AtMilliseconds <= _time);
        if (frame is null) return;

        DrawGrid(canvas);
        DrawObjective(Project(new(6_000, 2_000)), new Color("e4b957"), "宝");
        DrawObjective(Project(new(11_000, 2_000)), new Color("75d39a"), "出");
        foreach (SpatialEvent entry in _region.Combat.Events.Where(value => value.Presentation is not null &&
                     value.AtMilliseconds <= _time && value.Presentation.EndsAtMilliseconds > _time))
        {
            SpatialPresentation effect = entry.Presentation!;
            if (effect.Shape == "circle")
            {
                Vector2 center = Project(entry.TargetPosition);
                float radius = effect.RadiusRaw / 12_000f * Size.X;
                float age = Math.Clamp((_time - effect.StartsAtMilliseconds) /
                    (float)Math.Max(1, effect.EndsAtMilliseconds - effect.StartsAtMilliseconds), 0, 1);
                Color color = _time < effect.StartsAtMilliseconds ? new Color("f4d369") : new Color("ed704b");
                DrawCircle(center, radius, new Color(color, .12f + .08f * (1 - age)));
                DrawArc(center, radius, 0, MathF.Tau, 32, new Color(color, .92f), 2.2f);
                DrawArc(center, radius * (.8f + .15f * MathF.Sin((float)_clock * 5)), 0, MathF.Tau, 24,
                    new Color(color, .38f), 1);
            }
        }
        foreach (EnemyFrame enemy in frame.Enemies.Where(value => value.Life > 0))
            DrawEnemyToken(Project(enemy.Position), enemy, enemy.EntityId == frame.HeroTargetId);
        DrawHeroToken(Project(frame.HeroPosition), frame);
        DrawString(ThemeDB.FallbackFont, new(12, 22), $"{_region.Name} · 原型回放 · 生命 {frame.HeroLife}/{frame.HeroMaximumLife}",
            HorizontalAlignment.Left, -1, 14, new Color("f1e1bf"));
    }

    private void DrawGrid(Rect2 canvas)
    {
        for (int column = 1; column < 12; column++)
        {
            float x = canvas.Size.X * column / 12f;
            DrawLine(new Vector2(x, 30), new Vector2(x, canvas.Size.Y), new Color(.55f, .65f, .7f, .12f), 1);
        }
        for (int row = 1; row < 8; row++)
        {
            float y = 30 + (canvas.Size.Y - 30) * row / 8f;
            DrawLine(new Vector2(0, y), new Vector2(canvas.Size.X, y), new Color(.55f, .65f, .7f, .1f), 1);
        }
    }

    private void DrawObjective(Vector2 position, Color color, string glyph)
    {
        DrawSetTransform(position + new Vector2(0, 4), 0, new Vector2(1, .45f));
        DrawCircle(Vector2.Zero, 13, new Color(0, 0, 0, .38f));
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        DrawCircle(position, 9, new Color(color, .24f));
        DrawArc(position, 10 + MathF.Sin((float)_clock * 3) * 1.2f, 0, MathF.Tau, 20, color, 2);
        DrawString(ThemeDB.FallbackFont, position + new Vector2(-5, 5), glyph,
            HorizontalAlignment.Center, 10, 10, new Color("f8f1d9"));
    }

    private void DrawEnemyToken(Vector2 position, EnemyFrame enemy, bool targeted)
    {
        float radius = enemy.Boss ? 12 : enemy.Elite ? 9 : 7;
        Color color = enemy.Role switch
        {
            UnitRole.Melee => new Color("d15762"), UnitRole.Ranged => new Color("54b5d5"),
            UnitRole.Caster => new Color("b17de0"), UnitRole.Charger => new Color("e28b4f"),
            UnitRole.Summoner => new Color("61c28f"), _ => new Color("d15762")
        };
        DrawSetTransform(position + new Vector2(0, 4), 0, new Vector2(1, .4f));
        DrawCircle(Vector2.Zero, radius + 4, new Color(0, 0, 0, .38f));
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        DrawColoredPolygon([position + new Vector2(0, -radius), position + new Vector2(radius, 0),
            position + new Vector2(0, radius), position + new Vector2(-radius, 0)], new Color(color, .88f));
        DrawPolyline([position + new Vector2(0, -radius), position + new Vector2(radius, 0),
            position + new Vector2(0, radius), position + new Vector2(-radius, 0), position + new Vector2(0, -radius)],
            targeted ? new Color("ffe08a") : new Color(color, .95f), targeted ? 2.5f : 1.5f);
        if (enemy.Boss) DrawArc(position, radius + 5, 0, MathF.Tau, 24, new Color("ffe08a"), 2);
        DrawRect(new Rect2(position + new Vector2(-14, radius + 5), new Vector2(28, 4)), new Color("281d27"), true);
        DrawRect(new Rect2(position + new Vector2(-14, radius + 5), new Vector2(28 * Math.Clamp(enemy.Life / (float)Math.Max(1, enemy.MaximumLife), 0, 1), 4)),
            enemy.Boss ? new Color("ed7b64") : new Color("d04a5c"), true);
    }

    private void DrawHeroToken(Vector2 position, SpatialFrame frame)
    {
        DrawSetTransform(position + new Vector2(0, 5), 0, new Vector2(1, .4f));
        DrawCircle(Vector2.Zero, 10, new Color(0, 0, 0, .4f));
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        DrawCircle(position + new Vector2(0, -5), 6, new Color("b9ecf1"));
        DrawColoredPolygon([position + new Vector2(-8, 1), position + new Vector2(8, 1),
            position + new Vector2(5, 12), position + new Vector2(-5, 12)], new Color("36a8c6"));
        DrawLine(position + new Vector2(6, 3), position + new Vector2(16, -8), new Color("f2d18c"), 2);
        DrawRect(new Rect2(position + new Vector2(-18, 16), new Vector2(36, 4)), new Color("281d27"), true);
        DrawRect(new Rect2(position + new Vector2(-18, 16), new Vector2(36 * Math.Clamp(frame.HeroLife / (float)Math.Max(1, frame.HeroMaximumLife), 0, 1), 4)),
            new Color("d55b68"), true);
    }
}
