using GameForWork.Core.Campaign;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Spatial;
using Godot;

namespace GameForWork.GodotClient;

public partial class LootChestPanel : VBoxContainer
{
    private Func<GameSession> _session = null!;
    private Action<string> _changed = null!;
    private readonly VBoxContainer _rows = new();
    private readonly Label _status = new();
    private string _signature = string.Empty;

    public void Initialize(Func<GameSession> session, Action<string> changed)
    {
        _session = session;
        _changed = changed;
        Name = "普通战利品箱";
        AddChild(new Label
        {
            Text = "普通刷图的装备、金币和打造材料在结算时冻结到宝箱；可单箱或批量领取，领取时才执行过滤和仓储处理。",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        var toolbar = new HFlowContainer();
        AddButton(toolbar, "全部领取", () =>
        {
            int count = _session().ClaimLootChests();
            return count > 0 ? $"已领取 {count} 个普通战利品箱。" : "没有可领取的普通战利品箱，或仓储处理未能继续。";
        });
        toolbar.AddChild(_status);
        AddChild(toolbar);
        var scroll = new ScrollContainer { CustomMinimumSize = new(0, 220), SizeFlagsVertical = SizeFlags.ExpandFill };
        _rows.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(_rows);
        AddChild(scroll);
    }

    private void AddButton(Control parent, string title, Func<string> action)
    {
        var button = new Button { Text = title };
        button.Pressed += () => { _changed(action()); RefreshState(); };
        parent.AddChild(button);
    }

    public void RefreshState()
    {
        if (_session is null) return;
        GameSession session = _session();
        _status.Text = $"待开普通箱 {session.LootChests.Count} · 新获得 {session.LootChests.Count(chest => chest.IsNew)}";
        string signature = string.Join('|', session.LootChests.Select(chest =>
            $"{chest.Id}:{chest.IsNew}:{chest.Equipment.Count}:{chest.Gold}:{chest.IronScraps}:{string.Join(',', chest.Metals ?? [])}"));
        if (signature == _signature) return;
        _signature = signature;
        foreach (Node child in _rows.GetChildren()) { _rows.RemoveChild(child); child.QueueFree(); }
        foreach (LootChest chest in session.LootChests)
        {
            _rows.AddChild(new Label
            {
                Text = $"{(chest.IsNew ? "【新】" : "")}普通战利品箱 · T{chest.Difficulty} · 装备 {chest.Equipment.Count} · 金币 {chest.Gold} · 材料 {chest.IronScraps + (chest.Metals ?? []).Sum(stack => stack.Amount)}",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            });
            var row = new HFlowContainer();
            if (chest.IsNew)
                AddButton(row, "标为已查看", () => _session().MarkLootChestViewed(chest.Id) ? "已查看，箱内内容保持不变。" : "宝箱状态已经更新。");
            AddButton(row, "领取本箱", () => _session().ClaimLootChest(chest.Id) ? "已领取普通战利品箱。" : "未领取，宝箱保持原状。");
            _rows.AddChild(row);
        }
        if (session.LootChests.Count == 0) _rows.AddChild(new Label { Text = "暂无普通战利品箱" });
    }
}
