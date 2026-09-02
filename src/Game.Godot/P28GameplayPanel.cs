using GameForWork.Core.P1;
using GameForWork.Core.P1.World;
using GameForWork.Core.P28;
using Godot;

namespace GameForWork.GodotClient;

public partial class P28GameplayPanel : VBoxContainer
{
    public static P28GameplayPolicy AutomaticDifficulty() => new(
        Reward: P28RewardPreference.Balanced,
        AbyssIntensity: 5,
        AbyssStages: 4,
        AbyssFinalGuardian: true,
        Garden: P28GardenMode.Triple,
        Red: P28AltarMode.Extreme,
        Blue: P28AltarMode.Extreme,
        Warfront: P28WarfrontMode.Decisive,
        RejectedCosts: [],
        Refresh: P28Refresh.Always,
        GardenTag: "life");

    public void Initialize(Func<P1GameSession> session, ExpeditionTeamKind teamKind, Action<string> changed)
    {
        P1TeamExpeditionState Team() => teamKind == ExpeditionTeamKind.Hero
            ? session().World.Hero
            : session().World.Mercenaries;
        var body = new VBoxContainer { Visible = false };
        var toggle = new Button
        {
            Text = "玩法设置 ▸",
            ToggleMode = true,
            TooltipText = "每队独立保存；正在进行的地图保持原设置。",
        };
        toggle.Toggled += open =>
        {
            body.Visible = open;
            toggle.Text = open ? "玩法设置 ▾" : "玩法设置 ▸";
        };
        AddChild(toggle);
        AddChild(body);

        var routes = new HFlowContainer();
        body.AddChild(routes);
        var blockAbyss = new CheckBox { Text = "屏蔽裂渊追猎" };
        var blockGarden = new CheckBox { Text = "屏蔽命能花园" };
        var blockWarfront = new CheckBox { Text = "屏蔽亡旗战阵" };
        routes.AddChild(blockAbyss);
        routes.AddChild(blockGarden);
        routes.AddChild(blockWarfront);

        var altarRow = new HBoxContainer();
        altarRow.AddChild(new Label { Text = "祭坛选择" });
        var altar = new OptionButton();
        altar.AddItem("自动选择", (int)MapAltarPreference.Any);
        altar.AddItem("选择赤誓", (int)MapAltarPreference.RedOath);
        altar.AddItem("选择苍誓", (int)MapAltarPreference.BlueOath);
        altar.AddItem("屏蔽祭坛", (int)MapAltarPreference.Avoid);
        altarRow.AddChild(altar);
        body.AddChild(altarRow);
        body.AddChild(new Label
        {
            Text = "未屏蔽的玩法会启用；已分配的异界天赋会自动采用该玩法当前可用的最高难度。",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Modulate = new Color("b7c1d4"),
        });

        var save = new Button { Text = "保存玩法设置" };
        body.AddChild(save);
        toggle.Toggled += open =>
        {
            if (!open) return;
            ExpeditionPolicy policy = Team().PendingPolicy ?? Team().Policy;
            IReadOnlyList<MapRoute> blocked = policy.BlockedRoutes ?? [];
            blockAbyss.ButtonPressed = blocked.Contains(MapRoute.Abyss);
            blockGarden.ButtonPressed = blocked.Contains(MapRoute.LifeGarden);
            blockWarfront.ButtonPressed = blocked.Contains(MapRoute.Warfront);
            int altarIndex = altar.GetItemIndex((int)policy.AltarPreference);
            altar.Select(altarIndex < 0 ? 0 : altarIndex);
        };
        save.Pressed += () =>
        {
            var blocked = new List<MapRoute>();
            if (blockAbyss.ButtonPressed) blocked.Add(MapRoute.Abyss);
            if (blockGarden.ButtonPressed) blocked.Add(MapRoute.LifeGarden);
            if (blockWarfront.ButtonPressed) blocked.Add(MapRoute.Warfront);
            MapAltarPreference preference = (MapAltarPreference)altar.GetItemId(altar.Selected);
            ExpeditionPolicy current = Team().PendingPolicy ?? Team().Policy;
            session().SetExpeditionPolicy(teamKind, current with
            {
                BlockedRoutes = blocked,
                AltarPreference = preference,
                Gameplay = AutomaticDifficulty(),
                RouteDecisionTimeoutSeconds = 0,
            });
            changed("玩法设置已保存；下一张地图会按异界天赋采用最高可用难度。");
        };
    }
}
