using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.World;
using GameForWork.Core.P12;
using GameForWork.Core.P14;
using GameForWork.Core.Simulation;

namespace GameForWork.Core.P28;

public enum P28RewardPreference { Balanced, Weapons, Armor, Jewelry, Materials, SkillStones, HighBases, Legendary }
public enum P28GardenMode { Single, Twin, Triple }
public enum P28AltarMode { Normal, HighPressure, Extreme }
public enum P28WarfrontMode { Normal, Expanded, Decisive }
public enum P28Cost { MaximumLife, Defenses, IncomingHits, FlaskRecovery, BossLife, BossDamage, BossSpeed, BossPhase }
public enum P28Refresh { Never, UntilPreferred, Always }
public enum P28Mechanic { None, Abyss, Garden, Red, Blue, Warfront }

/// <summary>Saved per team; copied into the map at dispatch, never read live during a run.</summary>
public sealed record P28GameplayPolicy(
    P28RewardPreference Reward = P28RewardPreference.Balanced,
    int AbyssIntensity = 1, int AbyssStages = 4, bool AbyssFinalGuardian = false,
    P28GardenMode Garden = P28GardenMode.Single,
    P28AltarMode Red = P28AltarMode.Normal, P28AltarMode Blue = P28AltarMode.Normal,
    P28WarfrontMode Warfront = P28WarfrontMode.Normal,
    IReadOnlyList<P28Cost>? RejectedCosts = null, P28Refresh Refresh = P28Refresh.UntilPreferred,
    string GardenTag = "life")
{
    public P28GameplayPolicy Validate()
    {
        if (!Enum.IsDefined(Reward) || !Enum.IsDefined(Garden) || !Enum.IsDefined(Red) ||
            !Enum.IsDefined(Blue) || !Enum.IsDefined(Warfront) || !Enum.IsDefined(Refresh) ||
            AbyssIntensity is < 1 or > 5 || AbyssStages is < 0 or > 4 ||
            (RejectedCosts?.Any(cost => !Enum.IsDefined(cost)) ?? false) ||
            GardenTag is not ("life" or "defense" or "attack" or "spell"))
            throw new ArgumentException("玩法策略无效。");
        return this;
    }
}

public sealed record P28Choice(string Id, string Name, P28RewardPreference Reward, P28Cost? Cost,
    int Magnitude, string Tag, string Enemy, string Rule);

public sealed record P28EncounterRule(P28Mechanic Mechanic,
    int Life = 10_000, int Damage = 10_000, int Reward = 10_000,
    int Units = 1, bool Terminal = false, bool NoRescue = false,
    P28Choice? Choice = null, IReadOnlyList<P28Choice>? Candidates = null,
    int Refreshes = 0, bool Locked = false, IReadOnlyList<P28Choice>? Selections = null,
    int TerminalReward = 10_000);

public static class P28Gameplay
{
    public static bool Has(IEnumerable<string>? atlas, string branch, int node) =>
        atlas?.Contains($"p26.atlas.{branch}.{node:00}", StringComparer.Ordinal) == true;
    public static int Scale(int value, int multiplier) => checked((int)((long)value * multiplier / 10_000));
    public static P28GameplayPolicy Policy(P1MapItem map)
    {
        P28GameplayPolicy policy = (map.Gameplay ?? new()).Validate();
        IReadOnlyList<string>? atlas = map.AtlasSnapshot;
        int intensity = policy.AbyssIntensity;
        if (intensity >= 5 && !Has(atlas, "abyss", 11)) intensity = 4;
        if (intensity >= 4 && !Has(atlas, "abyss", 8)) intensity = 3;
        if (intensity >= 3 && !Has(atlas, "abyss", 4)) intensity = 2;
        return policy with
        {
            AbyssIntensity = intensity,
            AbyssFinalGuardian = policy.AbyssFinalGuardian && intensity == 5 && Has(atlas, "abyss", 12),
            Garden = policy.Garden == P28GardenMode.Triple && Has(atlas, "garden", 12) ? P28GardenMode.Triple :
                policy.Garden != P28GardenMode.Single && Has(atlas, "garden", 11) ? P28GardenMode.Twin : P28GardenMode.Single,
            Red = AltarMode(policy.Red, atlas, "red"), Blue = AltarMode(policy.Blue, atlas, "blue"),
            Warfront = policy.Warfront == P28WarfrontMode.Decisive && Has(atlas, "warfront", 12) ? P28WarfrontMode.Decisive :
                policy.Warfront != P28WarfrontMode.Normal && Has(atlas, "warfront", 11) ? P28WarfrontMode.Expanded : P28WarfrontMode.Normal,
        };
    }

    private static P28AltarMode AltarMode(P28AltarMode mode, IEnumerable<string>? atlas, string branch) =>
        mode == P28AltarMode.Extreme && Has(atlas, branch, 12) ? mode :
        mode != P28AltarMode.Normal && Has(atlas, branch, 10) ? P28AltarMode.HighPressure : P28AltarMode.Normal;

    public static P28EncounterRule Abyss(P28GameplayPolicy policy, bool terminal)
    {
        (int life, int damage, int reward) = policy.AbyssIntensity switch
        {
            2 => (12_500, 11_500, 13_500), 3 => (15_000, 13_000, 17_500),
            4 => (20_000, 16_000, 25_000), 5 => (25_000, 19_000, 35_000), _ => (10_000, 10_000, 10_000),
        };
        return new(P28Mechanic.Abyss, life, damage, reward,
            Terminal: terminal, NoRescue: policy.AbyssFinalGuardian,
            TerminalReward: terminal && policy.AbyssFinalGuardian ? 20_000 : 10_000);
    }

    public static P14MapPlan Build(P1MapItem map, MapRoute route, IReadOnlyList<string> atlas, ulong seed)
    {
        P28GameplayPolicy policy = Policy(map with { AtlasSnapshot = atlas });
        var random = new Pcg32(seed);
        var nodes = new List<P14MapNode>();
        void Add(P14MapNodeKind kind, string name, int count, P28EncounterRule? rule = null, string boss = "") =>
            nodes.Add(new(nodes.Count + 1, kind, name, count, BossStableId: boss, Gameplay: rule));
        if (route == MapRoute.Warfront)
        {
            int life = policy.Warfront == P28WarfrontMode.Decisive ? 25_000 : 10_000;
            int damage = policy.Warfront == P28WarfrontMode.Decisive ? 19_000 : 10_000;
            int reward = policy.Warfront switch { P28WarfrontMode.Expanded => 25_000, P28WarfrontMode.Decisive => 40_000, _ => 10_000 };
            var rule = new P28EncounterRule(P28Mechanic.Warfront, life, damage, reward,
                NoRescue: policy.Warfront == P28WarfrontMode.Decisive);
            Add(P14MapNodeKind.WarfrontEncounter, "侦察接敌", 9, rule);
            Add(P14MapNodeKind.WarfrontEncounter, "突破前线", 14, rule);
            P28Choice[] supplies = Choices(P28Mechanic.Warfront, Has(atlas, "warfront", 4) ? 4 : 3, random);
            P28Choice supply = Choose(supplies, Has(atlas, "warfront", 6) ? policy : policy with { Reward = P28RewardPreference.Balanced })!;
            Add(P14MapNodeKind.RouteChoice, $"战地补给：{supply.Name}", 0, rule with { Choice = supply, Candidates = supplies });
            int officers = policy.Warfront == P28WarfrontMode.Normal ? 1 : 2;
            for (int i = 0; i < officers; i++)
            {
                if (i > 0) Add(P14MapNodeKind.WarfrontEncounter, "侧翼军阵", 12, rule);
                P14BossDefinition officer = P14Bosses.WarfrontOfficers[(int)(random.NextUInt() % (uint)P14Bosses.WarfrontOfficers.Count)];
                bool elite = random.NextBasisPoints() < (Has(atlas, "warfront", 7) ? 4_500 : 3_000);
                Add(P14MapNodeKind.WarfrontOfficer, (elite ? "精锐·" : "") + officer.DisplayName, elite ? 10 : 7, rule, officer.StableId);
            }
            if (policy.Warfront == P28WarfrontMode.Decisive)
            {
                Add(P14MapNodeKind.WarfrontEncounter, "决战炮阵", 16, rule);
                Add(P14MapNodeKind.WarfrontEncounter, "统帅亲卫", 16, rule);
            }
            // Altars are independent encounters, not silently replaced by the route.
            AddAltars();
            Add(P14MapNodeKind.WarfrontCommander, P14Bosses.WarfrontCommander.DisplayName, 9,
                rule with { Terminal = true, Choice = supply }, P14Bosses.WarfrontCommander.StableId);
        }
        else
        {
            Add(P14MapNodeKind.Entrance, "地图入口", 0);
            Add(P14MapNodeKind.Encounter, "区域怪群", 10 + map.Tier / 2);
            if (route == MapRoute.Safe) Add(P14MapNodeKind.Encounter, "区域巡猎", 8 + map.Tier / 3);
            if (route == MapRoute.Abyss)
            {
                string[] names = ["裂隙开启", "裂隙追猎·一", "裂隙追猎·二", "裂隙终巢"];
                for (int i = 0; i < policy.AbyssStages; i++)
                {
                    bool terminal = i == 3;
                    bool guardian = terminal && (policy.AbyssFinalGuardian || random.NextBasisPoints() < (Has(atlas, "abyss", 7) ? 4_500 : 3_000));
                    Add(P14MapNodeKind.AbyssFissure, guardian ? "深渊监守者·裂隙终巢" : names[i], 12 + map.Tier / 2,
                        Abyss(policy, terminal), guardian ? P1Enemies.AbyssWarden.StableId : "");
                }
                if (policy.AbyssStages < 4) Add(P14MapNodeKind.RouteChoice, "主动离开裂隙；保留已得收获，无终巢宝箱", 0);
            }
            if (route == MapRoute.LifeGarden)
            {
                int budget = Has(atlas, "garden", 8) ? 2 : Has(atlas, "garden", 4) ? 1 : 0;
                var plots = new List<P28EncounterRule>();
                for (int i = 0; i < 3; i++)
                {
                    P28Choice[] candidates = Choices(P28Mechanic.Garden, Has(atlas, "garden", 2) ? 4 : 3, random);
                    int spent = 0;
                    while (budget > 0 && policy.Refresh != P28Refresh.Never &&
                        (policy.Refresh == P28Refresh.Always || !candidates.Any(c => c.Tag == policy.GardenTag)))
                    {
                        P28Choice? locked = Has(atlas, "garden", 9) ? candidates.FirstOrDefault(c => c.Tag == policy.GardenTag) : null;
                        candidates = Choices(P28Mechanic.Garden, candidates.Length, random);
                        if (locked is not null) candidates[0] = locked;
                        budget--; spent++;
                    }
                    P28Choice selected = candidates.FirstOrDefault(c => c.Tag == policy.GardenTag) ?? Choose(candidates, policy)!;
                    plots.Add(new(P28Mechanic.Garden, Choice: selected, Candidates: candidates, Refreshes: spent,
                        Locked: Has(atlas, "garden", 9)));
                }
                int batch = policy.Garden switch { P28GardenMode.Twin => 2, P28GardenMode.Triple => 3, _ => 1 };
                for (int i = 0; i < 3; i += batch)
                {
                    int units = Math.Min(batch, 3 - i);
                    P28EncounterRule rule = plots[i] with
                    {
                        Units = units, Terminal = i + units == 3,
                        Life = batch == 3 ? 25_000 : batch == 2 ? 17_500 : 10_000,
                        Damage = batch == 3 ? 19_000 : batch == 2 ? 14_500 : 10_000,
                        Reward = batch == 3 ? 35_000 : batch == 2 ? 20_000 : 10_000,
                        Candidates = plots.Skip(i).Take(units).SelectMany(p => p.Candidates!).ToArray(),
                        Selections = plots.Skip(i).Take(units).Select(p => p.Choice!).ToArray(),
                        Refreshes = plots.Skip(i).Take(units).Sum(p => p.Refreshes),
                    };
                    Add(P14MapNodeKind.GardenPlot, $"命能苗圃·{units}块：{string.Join('、', plots.Skip(i).Take(units).Select(p => p.Choice!.Name))}",
                        (8 + map.Tier / 3) * units, rule,
                        random.NextBasisPoints() < (Has(atlas, "garden", 10) ? 3_000 : 2_000) ? "p27.enemy.harvest_avatar" : "");
                }
            }
            AddAltars();
            Add(P14MapNodeKind.Elite, "精英据点", 8);
            P14BossDefinition boss = P14Bosses.ForArea(map.AreaId);
            Add(P14MapNodeKind.Boss, boss.DisplayName, 5, boss: boss.StableId);
        }
        return new(map.InstanceId, route, nodes, map.Altar, atlas, 0, nodes[^1].BossStableId);

        void AddAltars()
        {
            if (map.Altar == P12MapAltar.None) return;
            bool red = map.Altar == P12MapAltar.RedOath;
            string branch = red ? "red" : "blue";
            P28AltarMode mode = red ? policy.Red : policy.Blue;
            int count = red ? mode == P28AltarMode.Extreme ? 3 : Has(atlas, branch, 7) ? 2 : 1 : 1;
            for (int i = 0; i < count; i++)
            {
                int refreshBudget = Has(atlas, branch, 6) ? 1 : 0;
                P28Mechanic mechanic = red ? P28Mechanic.Red : P28Mechanic.Blue;
                P28Choice[] candidates = Choices(mechanic, Has(atlas, branch, 4) ? 4 : 3, random);
                P28Choice? choice = Choose(candidates, policy);
                int spent = 0;
                if (refreshBudget > 0 && policy.Refresh != P28Refresh.Never &&
                    (policy.Refresh == P28Refresh.Always || choice is null || policy.Reward != P28RewardPreference.Balanced && choice.Reward != policy.Reward))
                {
                    candidates = Choices(mechanic, candidates.Length, random); choice = Choose(candidates, policy); refreshBudget--; spent++;
                }
                if (choice is null) { Add(P14MapNodeKind.RouteChoice, "祭坛代价均被拒绝，自动跳过", 0); continue; }
                int penalty = mode == P28AltarMode.Extreme ? 25_000 : mode == P28AltarMode.HighPressure ? 17_500 : 10_000;
                choice = choice with { Magnitude = Scale(choice.Magnitude, penalty) };
                var rule = new P28EncounterRule(mechanic, Reward: mode == P28AltarMode.Extreme ? 40_000 : mode == P28AltarMode.HighPressure ? 25_000 : 10_000,
                    Choice: choice, Candidates: candidates, Refreshes: spent,
                    NoRescue: route == MapRoute.Warfront && policy.Warfront == P28WarfrontMode.Decisive);
                Add(P14MapNodeKind.Altar, $"{(red ? "赤誓" : "苍誓")}：{choice.Name} · {choice.Rule}", 8 + map.Tier / 2, rule,
                    red && random.NextBasisPoints() < (Has(atlas, "red", 11) ? 4_500 : 3_000) ? "p27.enemy.armor_executioner" : "");
            }
        }
    }

    public static P28Choice[] Choices(P28Mechanic mechanic, int count, Pcg32 random)
    {
        P28Choice[] pool = mechanic switch
        {
            P28Mechanic.Red =>
            [new("red.life", "血契金库", P28RewardPreference.Materials, P28Cost.MaximumLife, 800, "gold", "赤誓卫士", "最大生命总降；击败守卫获得金币金属"),
             new("red.defense", "破甲遗物", P28RewardPreference.Armor, P28Cost.Defenses, 1_500, "gear", "赤誓卫士", "护甲闪避总降；击败守卫获得稀有装备"),
             new("red.hit", "猎杀赐福", P28RewardPreference.Weapons, P28Cost.IncomingHits, 1_200, "metal", "赤誓处刑者", "受到更多击中伤害；击败守卫获得金属"),
             new("red.flask", "枯泉路标", P28RewardPreference.HighBases, P28Cost.FlaskRecovery, 2_000, "map", "赤誓卫士", "药剂恢复总降；击败守卫获得地图")],
            P28Mechanic.Blue =>
            [new("blue.life", "坚壁藏品", P28RewardPreference.HighBases, P28Cost.BossLife, 2_000, "base", "苍誓守卫", "最终Boss更多生命；通关结算高阶底材"),
             new("blue.damage", "残酷晶簇", P28RewardPreference.SkillStones, P28Cost.BossDamage, 2_500, "gem", "苍誓守卫", "最终Boss更多伤害；通关结算技能石"),
             new("blue.speed", "疾风秘宝", P28RewardPreference.Legendary, P28Cost.BossSpeed, 1_800, "unique", "苍誓守卫", "最终Boss施放加速；通关结算传奇机会"),
             new("blue.phase", "终幕契约", P28RewardPreference.Legendary, P28Cost.BossPhase, 1, "unique", "苍誓守卫", "最终Boss追加回响阶段；通关结算传奇机会")],
            P28Mechanic.Garden =>
            [new("garden.life", "血芽苗圃", P28RewardPreference.Armor, null, 0, "life", "血根兽群", "治疗与缠根；生命词缀底材与命能"),
             new("garden.defense", "铁皮苗圃", P28RewardPreference.Armor, null, 0, "defense", "铁皮植群", "护盾链接；防御词缀底材与命能"),
             new("garden.attack", "荆刃苗圃", P28RewardPreference.Weapons, null, 0, "attack", "荆刃猎群", "包抄突袭；攻击词缀底材与命能"),
             new("garden.spell", "灵蕾苗圃", P28RewardPreference.Jewelry, null, 0, "spell", "灵蕾术群", "持续危险地面；法术词缀底材与命能")],
            _ =>
            [new("supply.weapon", "武器补给", P28RewardPreference.Weapons, null, 0, "weapon", "", "终战武器补给"),
             new("supply.armor", "护甲补给", P28RewardPreference.Armor, null, 0, "armor", "", "终战护甲补给"),
             new("supply.jewelry", "饰品补给", P28RewardPreference.Jewelry, null, 0, "jewelry", "", "终战饰品补给"),
             new("supply.material", "材料补给", P28RewardPreference.Materials, null, 0, "metal", "", "终战金属补给")],
        };
        return pool.OrderBy(_ => random.NextUInt()).Take(count).ToArray();
    }

    public static P28Choice? Choose(IEnumerable<P28Choice> choices, P28GameplayPolicy policy) => choices
        .Where(c => c.Cost is null || !(policy.RejectedCosts ?? []).Contains(c.Cost.Value))
        .OrderByDescending(c => c.Reward == policy.Reward).FirstOrDefault();
}
