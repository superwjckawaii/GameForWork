using GameForWork.Core.P1;
using Godot;

namespace GameForWork.GodotClient;

public partial class P22InformationWindow : Window
{
    private Func<P1GameSession?>? _session;
    private RichTextLabel? _journey;

    public void Initialize(Func<P1GameSession?> session)
    {
        _session = session;
        Title = "游戏信息与指引";
        Size = new Vector2I(780, 520);
        MinSize = new Vector2I(640, 440);
        Exclusive = true;
        Transient = true;
        CloseRequested += Hide;

        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);
        root.AddChild(new Label
        {
            Text = "暗门远征指南 · 这里集中保存流程、构筑、操作与词缀资料。",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        var tabs = new TabContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        root.AddChild(tabs);
        tabs.AddChild(GuidePage("快速指引", QuickGuide));
        _journey = GuidePage("旅程路线", "切换到本页时读取当前存档的完整旅程状态。");
        tabs.AddChild(_journey);
        tabs.AddChild(GuidePage("系统与操作", SystemGuide));
        tabs.AddChild(new P19AffixPanel
        {
            Name = "词缀库",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        });
        tabs.TabChanged += index =>
        {
            if (tabs.GetTabControl((int)index) == _journey) RefreshJourney();
        };
        var close = new Button { Text = "关闭", SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd };
        close.Pressed += Hide;
        root.AddChild(close);
    }

    public void Open()
    {
        RefreshJourney();
        PopupCentered();
    }

    private void RefreshJourney()
    {
        if (_journey is null) return;
        P1GameSession? session = _session?.Invoke();
        if (session is null)
        {
            _journey.Text = "尚未创建角色。创建角色后，本页会显示当前旅程位置和全部后续目标。";
            return;
        }

        session.Journey.Synchronize(session);
        string steps = string.Join('\n', session.Journey.AllSteps.Select((definition, index) =>
            $"{(index < session.Journey.CurrentStepIndex ? "✓" : index == session.Journey.CurrentStepIndex ? "▶" : "·")} " +
            $"{definition.Title}\n    {definition.Instruction}\n    {definition.HelpText}"));
        _journey.Text =
            $"当前等级：Lv.{session.World.Hero.Progression.Level} / {session.World.Hero.Progression.LevelCap}\n" +
            $"升华点：已用 {session.Endgame.AscendancyPassives.Count} / 已获 {session.Endgame.BreakthroughPoints} / 最终 8\n\n" +
            steps;
    }

    private static RichTextLabel GuidePage(string name, string text) => new()
    {
        Name = name,
        Text = text,
        ScrollActive = true,
        AutowrapMode = TextServer.AutowrapMode.WordSmart,
        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        SizeFlagsVertical = Control.SizeFlags.ExpandFill,
    };

    private const string QuickGuide =
        "核心流程\n" +
        "1. 完成五幕主线，逐步开放装备、技能、天赋、城镇和远征。\n" +
        "2. 在远征中消耗路印，完成 T1～T16 并持续优化装备与技能孔组。\n" +
        "3. 完成 T16 后继续刷图升到 100 级。\n" +
        "4. 前往‘远征 → 异界与突破’，完成门扉突破；胜利开放 T17～T20，并获得 2 点升华点。\n" +
        "5. 完成 T20；刷 T11+ 自动收集天垒碎片，8 枚合成一张门票。\n" +
        "6. 击败灰烬天垒；首次胜利再获得 2 点升华点。\n\n" +
        "升华点来源\n" +
        "第三幕 2 点、第五幕 2 点、100 级门扉突破 2 点、灰烬天垒首杀 2 点。4/4 表示当前四点均已使用，不是进度卡死。";

    private const string SystemGuide =
        "装备与技能\n" +
        "装备决定技能连接孔组；每组放置一个主动技能石与兼容辅助技能石。物品悬浮可查看底材、词缀档位、来源与孔组。\n\n" +
        "天赋画布\n" +
        "空白处左键拖动，滚轮以鼠标位置为中心缩放。主天赋左键双击分配、右键双击退还；退还不能切断其他已分配节点的必要路径。升华和异界天赋按页面提示操作。\n\n" +
        "远征与结算\n" +
        "选择目标、执行模式、最高阶级与风险后开始派遣。战斗失败仍会结算已经击败敌人的经验和物品；地图耗尽、连续失败或方针限制会明确停止。\n\n" +
        "过滤器与制作\n" +
        "掉落过滤器只决定自动保留、出售或分解方式，不改变实际掉落。制作会消耗对应金属，预览与确认结果分离。\n\n" +
        "后台与存档\n" +
        "隐藏到托盘后游戏继续运行；暂停战斗不会暂停城镇生产。三个存档槽彼此独立，离线收益在下次启动时统一结算。";
}
