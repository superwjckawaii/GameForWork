using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Maps;
using GameForWork.Core.Content;
using GameForWork.Core.Simulation;

namespace GameForWork.Core.Encounters;

public enum RewardPreference { Balanced, Weapons, Armor, Jewelry, Materials, SkillStones, HighBases, Legendary }
public enum GardenMode { Single, Twin, Triple }
public enum AltarMode { Normal, HighPressure, Extreme }
public enum WarfrontMode { Normal, Expanded, Decisive }
public enum Cost { MaximumLife, Defenses, IncomingHits, FlaskRecovery, BossLife, BossDamage, BossSpeed, BossPhase }
public enum Refresh { Never, UntilPreferred, Always }
public enum Mechanic { None, Abyss, Garden, Red, Blue, Warfront }

/// <summary>Saved per team; copied into the map at dispatch, never read live during a run.</summary>
public sealed record GameplayPolicy(
    RewardPreference Reward = RewardPreference.Balanced,
    int AbyssIntensity = 1, int AbyssStages = 4, bool AbyssFinalGuardian = false,
    GardenMode Garden = GardenMode.Single,
    AltarMode Red = AltarMode.Normal, AltarMode Blue = AltarMode.Normal,
    WarfrontMode Warfront = WarfrontMode.Normal,
    IReadOnlyList<Cost>? RejectedCosts = null, Refresh Refresh = Refresh.UntilPreferred,
    string GardenTag = "life")
{
    public GameplayPolicy Validate()
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

public sealed record Choice(string Id, string Name, RewardPreference Reward, Cost? Cost,
    int Magnitude, string Tag, string Enemy, string Rule);

public sealed record EncounterRule(Mechanic Mechanic,
    int Life = 10_000, int Damage = 10_000, int Reward = 10_000,
    int Units = 1, bool Terminal = false, bool NoRescue = false,
    Choice? Choice = null, IReadOnlyList<Choice>? Candidates = null,
    int Refreshes = 0, bool Locked = false, IReadOnlyList<Choice>? Selections = null,
    int TerminalReward = 10_000);

public static class Gameplay
{
    public static bool Has(IEnumerable<string>? atlas, string branch, int node) =>
        atlas?.Contains($"atlas.atlas.{branch}.{node:00}", StringComparer.Ordinal) == true;
    public static int Scale(int value, int multiplier) => checked((int)((long)value * multiplier / 10_000));
    public static GameplayPolicy Policy(MapItem map)
    {
        GameplayPolicy policy = (map.Gameplay ?? new()).Validate();
        IReadOnlyList<string>? atlas = map.AtlasSnapshot;
        int intensity = policy.AbyssIntensity;
        if (intensity >= 5 && !Has(atlas, "abyss", 11)) intensity = 4;
        if (intensity >= 4 && !Has(atlas, "abyss", 8)) intensity = 3;
        if (intensity >= 3 && !Has(atlas, "abyss", 4)) intensity = 2;
        return policy with
        {
            AbyssIntensity = intensity,
            AbyssFinalGuardian = policy.AbyssFinalGuardian && intensity == 5 && Has(atlas, "abyss", 12),
            Garden = policy.Garden == GardenMode.Triple && Has(atlas, "garden", 12) ? GardenMode.Triple :
                policy.Garden != GardenMode.Single && Has(atlas, "garden", 11) ? GardenMode.Twin : GardenMode.Single,
            Red = ResolveAltarMode(policy.Red, atlas, "red"), Blue = ResolveAltarMode(policy.Blue, atlas, "blue"),
            Warfront = policy.Warfront == WarfrontMode.Decisive && Has(atlas, "warfront", 12) ? WarfrontMode.Decisive :
                policy.Warfront != WarfrontMode.Normal && Has(atlas, "warfront", 11) ? WarfrontMode.Expanded : WarfrontMode.Normal,
        };
    }

    private static AltarMode ResolveAltarMode(AltarMode mode, IEnumerable<string>? atlas, string branch) =>
        mode == AltarMode.Extreme && Has(atlas, branch, 12) ? mode :
        mode != AltarMode.Normal && Has(atlas, branch, 10) ? AltarMode.HighPressure : AltarMode.Normal;

    public static EncounterRule Abyss(GameplayPolicy policy, bool terminal)
    {
        (int life, int damage, int reward) = policy.AbyssIntensity switch
        {
            2 => (12_500, 11_500, 13_500), 3 => (15_000, 13_000, 17_500),
            4 => (20_000, 16_000, 25_000), 5 => (25_000, 19_000, 35_000), _ => (10_000, 10_000, 10_000),
        };
        return new(Mechanic.Abyss, life, damage, reward,
            Terminal: terminal, NoRescue: policy.AbyssFinalGuardian,
            TerminalReward: terminal && policy.AbyssFinalGuardian ? 20_000 : 10_000);
    }

    public static MapPlan Build(MapItem map, MapRoute route, IReadOnlyList<string> atlas, ulong seed)
    {
        GameplayPolicy policy = Policy(map with { AtlasSnapshot = atlas });
        var random = new Pcg32(seed);
        var nodes = new List<MapNode>();
        void Add(MapNodeKind kind, string name, int count, EncounterRule? rule = null, string boss = "") =>
            nodes.Add(new(nodes.Count + 1, kind, name, count, BossStableId: boss, Gameplay: rule));
        if (route == MapRoute.Warfront)
        {
            int life = policy.Warfront == WarfrontMode.Decisive ? 25_000 : 10_000;
            int damage = policy.Warfront == WarfrontMode.Decisive ? 19_000 : 10_000;
            int reward = policy.Warfront switch { WarfrontMode.Expanded => 25_000, WarfrontMode.Decisive => 40_000, _ => 10_000 };
            var rule = new EncounterRule(Mechanic.Warfront, life, damage, reward,
                NoRescue: policy.Warfront == WarfrontMode.Decisive);
            Add(MapNodeKind.WarfrontEncounter, "侦察接敌", 9, rule);
            Add(MapNodeKind.WarfrontEncounter, "突破前线", 14, rule);
            Choice[] supplies = Choices(Mechanic.Warfront, Has(atlas, "warfront", 4) ? 4 : 3, random);
            Choice supply = Choose(supplies, Has(atlas, "warfront", 6) ? policy : policy with { Reward = RewardPreference.Balanced })!;
            Add(MapNodeKind.RouteChoice, $"战地补给：{supply.Name}", 0, rule with { Choice = supply, Candidates = supplies });
            int officers = policy.Warfront == WarfrontMode.Normal ? 1 : 2;
            for (int i = 0; i < officers; i++)
            {
                if (i > 0) Add(MapNodeKind.WarfrontEncounter, "侧翼军阵", 12, rule);
                BossDefinition officer = Bosses.WarfrontOfficers[(int)(random.NextUInt() % (uint)Bosses.WarfrontOfficers.Count)];
                bool elite = random.NextBasisPoints() < (Has(atlas, "warfront", 7) ? 4_500 : 3_000);
                Add(MapNodeKind.WarfrontOfficer, (elite ? "精锐·" : "") + officer.DisplayName, elite ? 10 : 7, rule, officer.StableId);
            }
            if (policy.Warfront == WarfrontMode.Decisive)
            {
                Add(MapNodeKind.WarfrontEncounter, "决战炮阵", 16, rule);
                Add(MapNodeKind.WarfrontEncounter, "统帅亲卫", 16, rule);
            }
            // Altars are independent encounters, not silently replaced by the route.
            AddAltars();
            Add(MapNodeKind.WarfrontCommander, Bosses.WarfrontCommander.DisplayName, 9,
                rule with { Terminal = true, Choice = supply }, Bosses.WarfrontCommander.StableId);
        }
        else
        {
            Add(MapNodeKind.Entrance, "地图入口", 0);
            Add(MapNodeKind.Encounter, "区域怪群", 10 + map.Tier / 2);
            if (route == MapRoute.Safe) Add(MapNodeKind.Encounter, "区域巡猎", 8 + map.Tier / 3);
            if (route == MapRoute.Abyss)
            {
                string[] names = ["裂隙开启", "裂隙追猎·一", "裂隙追猎·二", "裂隙终巢"];
                for (int i = 0; i < policy.AbyssStages; i++)
                {
                    bool terminal = i == 3;
                    bool guardian = terminal && (policy.AbyssFinalGuardian || random.NextBasisPoints() < (Has(atlas, "abyss", 7) ? 4_500 : 3_000));
                    Add(MapNodeKind.AbyssFissure, guardian ? "深渊监守者·裂隙终巢" : names[i], 12 + map.Tier / 2,
                        Abyss(policy, terminal), guardian ? Enemies.AbyssWarden.StableId : "");
                }
                if (policy.AbyssStages < 4) Add(MapNodeKind.RouteChoice, "主动离开裂隙；保留已得收获，无终巢宝箱", 0);
            }
            if (route == MapRoute.LifeGarden)
            {
                int budget = Has(atlas, "garden", 8) ? 2 : Has(atlas, "garden", 4) ? 1 : 0;
                var plots = new List<EncounterRule>();
                for (int i = 0; i < 3; i++)
                {
                    Choice[] candidates = Choices(Mechanic.Garden, Has(atlas, "garden", 2) ? 4 : 3, random);
                    int spent = 0;
                    while (budget > 0 && policy.Refresh != Refresh.Never &&
                        (policy.Refresh == Refresh.Always || !candidates.Any(c => c.Tag == policy.GardenTag)))
                    {
                        Choice? locked = Has(atlas, "garden", 9) ? candidates.FirstOrDefault(c => c.Tag == policy.GardenTag) : null;
                        candidates = Choices(Mechanic.Garden, candidates.Length, random);
                        if (locked is not null) candidates[0] = locked;
                        budget--; spent++;
                    }
                    Choice selected = candidates.FirstOrDefault(c => c.Tag == policy.GardenTag) ?? Choose(candidates, policy)!;
                    plots.Add(new(Mechanic.Garden, Choice: selected, Candidates: candidates, Refreshes: spent,
                        Locked: Has(atlas, "garden", 9)));
                }
                int batch = policy.Garden switch { GardenMode.Twin => 2, GardenMode.Triple => 3, _ => 1 };
                for (int i = 0; i < 3; i += batch)
                {
                    int units = Math.Min(batch, 3 - i);
                    EncounterRule rule = plots[i] with
                    {
                        Units = units, Terminal = i + units == 3,
                        Life = batch == 3 ? 25_000 : batch == 2 ? 17_500 : 10_000,
                        Damage = batch == 3 ? 19_000 : batch == 2 ? 14_500 : 10_000,
                        Reward = batch == 3 ? 35_000 : batch == 2 ? 20_000 : 10_000,
                        Candidates = plots.Skip(i).Take(units).SelectMany(p => p.Candidates!).ToArray(),
                        Selections = plots.Skip(i).Take(units).Select(p => p.Choice!).ToArray(),
                        Refreshes = plots.Skip(i).Take(units).Sum(p => p.Refreshes),
                    };
                    Add(MapNodeKind.GardenPlot, $"命能苗圃·{units}块：{string.Join('、', plots.Skip(i).Take(units).Select(p => p.Choice!.Name))}",
                        (8 + map.Tier / 3) * units, rule,
                        random.NextBasisPoints() < (Has(atlas, "garden", 10) ? 3_000 : 2_000) ? "monsters.enemy.harvest_avatar" : "");
                }
            }
            AddAltars();
            Add(MapNodeKind.Elite, "精英据点", 8);
            BossDefinition boss = Bosses.ForArea(map.AreaId);
            Add(MapNodeKind.Boss, boss.DisplayName, 5, boss: boss.StableId);
        }
        return new(map.InstanceId, route, nodes, map.Altar, atlas, 0, nodes[^1].BossStableId);

        void AddAltars()
        {
            if (map.Altar == MapAltar.None) return;
            bool red = map.Altar == MapAltar.RedOath;
            string branch = red ? "red" : "blue";
            AltarMode mode = red ? policy.Red : policy.Blue;
            int count = red ? mode == AltarMode.Extreme ? 3 : Has(atlas, branch, 7) ? 2 : 1 : 1;
            for (int i = 0; i < count; i++)
            {
                int refreshBudget = Has(atlas, branch, 6) ? 1 : 0;
                Mechanic mechanic = red ? Mechanic.Red : Mechanic.Blue;
                Choice[] candidates = Choices(mechanic, Has(atlas, branch, 4) ? 4 : 3, random);
                Choice? choice = Choose(candidates, policy);
                int spent = 0;
                if (refreshBudget > 0 && policy.Refresh != Refresh.Never &&
                    (policy.Refresh == Refresh.Always || choice is null || policy.Reward != RewardPreference.Balanced && choice.Reward != policy.Reward))
                {
                    candidates = Choices(mechanic, candidates.Length, random); choice = Choose(candidates, policy); refreshBudget--; spent++;
                }
                if (choice is null) { Add(MapNodeKind.RouteChoice, "祭坛代价均被拒绝，自动跳过", 0); continue; }
                int penalty = mode == AltarMode.Extreme ? 25_000 : mode == AltarMode.HighPressure ? 17_500 : 10_000;
                choice = choice with { Magnitude = Scale(choice.Magnitude, penalty) };
                var rule = new EncounterRule(mechanic, Reward: mode == AltarMode.Extreme ? 40_000 : mode == AltarMode.HighPressure ? 25_000 : 10_000,
                    Choice: choice, Candidates: candidates, Refreshes: spent,
                    NoRescue: route == MapRoute.Warfront && policy.Warfront == WarfrontMode.Decisive);
                Add(MapNodeKind.Altar, $"{(red ? "赤誓" : "苍誓")}：{choice.Name} · {choice.Rule}", 8 + map.Tier / 2, rule,
                    red && random.NextBasisPoints() < (Has(atlas, "red", 11) ? 4_500 : 3_000) ? "monsters.enemy.armor_executioner" : "");
            }
        }
    }

    public static Choice[] Choices(Mechanic mechanic, int count, Pcg32 random)
    {
        Choice[] pool = mechanic switch
        {
            Mechanic.Red =>
            [new("red.life", "血契金库", RewardPreference.Materials, Cost.MaximumLife, 800, "gold", "赤誓卫士", "最大生命总降；击败守卫获得金币金属"),
             new("red.defense", "破甲遗物", RewardPreference.Armor, Cost.Defenses, 1_500, "gear", "赤誓卫士", "护甲闪避总降；击败守卫获得稀有装备"),
             new("red.hit", "猎杀赐福", RewardPreference.Weapons, Cost.IncomingHits, 1_200, "metal", "赤誓处刑者", "受到更多击中伤害；击败守卫获得金属"),
             new("red.flask", "枯泉路标", RewardPreference.HighBases, Cost.FlaskRecovery, 2_000, "map", "赤誓卫士", "药剂恢复总降；击败守卫获得地图")],
            Mechanic.Blue =>
            [new("blue.life", "坚壁藏品", RewardPreference.HighBases, Cost.BossLife, 2_000, "base", "苍誓守卫", "最终Boss更多生命；通关结算高阶底材"),
             new("blue.damage", "残酷晶簇", RewardPreference.SkillStones, Cost.BossDamage, 2_500, "gem", "苍誓守卫", "最终Boss更多伤害；通关结算技能石"),
             new("blue.speed", "疾风秘宝", RewardPreference.Legendary, Cost.BossSpeed, 1_800, "unique", "苍誓守卫", "最终Boss施放加速；通关结算传奇机会"),
             new("blue.phase", "终幕契约", RewardPreference.Legendary, Cost.BossPhase, 1, "unique", "苍誓守卫", "最终Boss追加回响阶段；通关结算传奇机会")],
            Mechanic.Garden =>
            [new("garden.life", "血芽苗圃", RewardPreference.Armor, null, 0, "life", "血根兽群", "治疗与缠根；生命词缀底材与命能"),
             new("garden.defense", "铁皮苗圃", RewardPreference.Armor, null, 0, "defense", "铁皮植群", "护盾链接；防御词缀底材与命能"),
             new("garden.attack", "荆刃苗圃", RewardPreference.Weapons, null, 0, "attack", "荆刃猎群", "包抄突袭；攻击词缀底材与命能"),
             new("garden.spell", "灵蕾苗圃", RewardPreference.Jewelry, null, 0, "spell", "灵蕾术群", "持续危险地面；法术词缀底材与命能")],
            _ =>
            [new("supply.weapon", "武器补给", RewardPreference.Weapons, null, 0, "weapon", "", "终战武器补给"),
             new("supply.armor", "护甲补给", RewardPreference.Armor, null, 0, "armor", "", "终战护甲补给"),
             new("supply.jewelry", "饰品补给", RewardPreference.Jewelry, null, 0, "jewelry", "", "终战饰品补给"),
             new("supply.material", "材料补给", RewardPreference.Materials, null, 0, "metal", "", "终战金属补给")],
        };
        return pool.OrderBy(_ => random.NextUInt()).Take(count).ToArray();
    }

    public static Choice? Choose(IEnumerable<Choice> choices, GameplayPolicy policy) => choices
        .Where(c => c.Cost is null || !(policy.RejectedCosts ?? []).Contains(c.Cost.Value))
        .OrderByDescending(c => c.Reward == policy.Reward).FirstOrDefault();
}
