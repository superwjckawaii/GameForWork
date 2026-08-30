using GameForWork.Core.P1;
using GameForWork.Core.P1.World;
using GameForWork.Core.P28;
using Godot;

namespace GameForWork.GodotClient;

public partial class P28GameplayPanel : VBoxContainer
{
    public void Initialize(Func<P1GameSession> session, ExpeditionTeamKind teamKind, Action<string> changed)
    {
        P1TeamExpeditionState Team() => teamKind == ExpeditionTeamKind.Hero ? session().World.Hero : session().World.Mercenaries;
        // The dashboard is constructed before a character exists. Read the session only on opening.
        P28GameplayPolicy saved = new();
        var body = new VBoxContainer { Visible = false };
        var toggle = new Button { Text = "玩法设置 ▸", ToggleMode = true, TooltipText = "每队独立保存，修改下一张地图生效。困难选项需要对应异界天赋。" };
        toggle.Toggled += open => { body.Visible = open; toggle.Text = open ? "玩法设置 ▾" : "玩法设置 ▸"; };
        AddChild(toggle); AddChild(body);
        var row = new HFlowContainer(); body.AddChild(row);
        OptionButton Option(string title, string[] names, int value)
        {
            var box = new VBoxContainer(); box.AddChild(new Label { Text = title });
            var option = new OptionButton(); foreach (string name in names) option.AddItem(name);
            option.Select(value); box.AddChild(option); row.AddChild(box); return option;
        }
        var reward = Option("奖励偏好", ["均衡", "武器", "护甲", "饰品", "材料", "技能石", "高阶底材", "传奇"], (int)saved.Reward);
        var intensity = Option("深渊强度", ["强度1", "强度2", "强度3（需天赋）", "强度4（需天赋）", "强度5（需天赋）"], saved.AbyssIntensity - 1);
        var stages = Option("深渊追猎", ["跳过", "1阶段后离开", "2阶段后离开", "3阶段后离开", "完成终巢"], saved.AbyssStages);
        var garden = Option("花园地块", ["单块 1+1+1", "双生 2+1（需天赋）", "三重 3（需天赋）"], (int)saved.Garden);
        string[] tags = ["life", "defense", "attack", "spell"];
        var tag = Option("花园标签", ["生命", "防御", "攻击", "法术"], Array.IndexOf(tags, saved.GardenTag));
        var red = Option("赤誓", ["普通", "高压（需天赋）", "极限（需天赋）"], (int)saved.Red);
        var blue = Option("苍誓", ["普通", "高压（需天赋）", "极限（需天赋）"], (int)saved.Blue);
        var war = Option("战阵", ["普通5节点", "扩大7节点（需天赋）", "决战9节点（需天赋）"], (int)saved.Warfront);
        var refresh = Option("刷新策略", ["不刷新", "未命中偏好时刷新", "用尽刷新次数"], (int)saved.Refresh);
        var guardian = new CheckBox { Text = "强度5最终守望者（需末点；深渊内禁救援）", ButtonPressed = saved.AbyssFinalGuardian }; body.AddChild(guardian);
        var rejected = new HFlowContainer(); body.AddChild(rejected);
        rejected.AddChild(new Label { Text = "拒绝代价：" });
        string[] costNames = ["生命降低", "护甲闪避降低", "承受更多击中", "药剂恢复降低", "Boss更多生命", "Boss更多伤害", "Boss更快施放", "Boss回响阶段"];
        var costs = new List<CheckBox>();
        foreach (P28Cost cost in Enum.GetValues<P28Cost>())
        {
            var check = new CheckBox { Text = costNames[(int)cost], ButtonPressed = saved.RejectedCosts?.Contains(cost) == true };
            costs.Add(check); rejected.AddChild(check);
        }
        body.AddChild(new Label { Text = "未解锁的困难设置按已解锁档位执行；代价全被拒绝则跳过祭坛。花园刷新按整座计算。", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        var save = new Button { Text = "保存玩法设置（下一张生效）" }; body.AddChild(save);
        toggle.Toggled += open =>
        {
            if (!open) return;
            saved = (Team().PendingPolicy ?? Team().Policy).Gameplay ?? new();
            reward.Select((int)saved.Reward); intensity.Select(saved.AbyssIntensity - 1); stages.Select(saved.AbyssStages);
            garden.Select((int)saved.Garden); tag.Select(Array.IndexOf(tags, saved.GardenTag));
            red.Select((int)saved.Red); blue.Select((int)saved.Blue); war.Select((int)saved.Warfront); refresh.Select((int)saved.Refresh);
            guardian.ButtonPressed = saved.AbyssFinalGuardian;
            for (int i = 0; i < costs.Count; i++) costs[i].ButtonPressed = saved.RejectedCosts?.Contains((P28Cost)i) == true;
        };
        save.Pressed += () =>
        {
            P28GameplayPolicy policy = new((P28RewardPreference)reward.Selected, intensity.Selected + 1, stages.Selected,
                guardian.ButtonPressed, (P28GardenMode)garden.Selected, (P28AltarMode)red.Selected, (P28AltarMode)blue.Selected,
                (P28WarfrontMode)war.Selected, costs.Select((c, i) => (c, i)).Where(p => p.c.ButtonPressed).Select(p => (P28Cost)p.i).ToArray(),
                (P28Refresh)refresh.Selected, tags[tag.Selected]);
            session().SetExpeditionPolicy(teamKind, (Team().PendingPolicy ?? Team().Policy) with { Gameplay = policy, RouteDecisionTimeoutSeconds = 0 });
            changed("玩法设置已保存；正在进行的地图保持原策略，下一张生效。");
        };
    }
}
