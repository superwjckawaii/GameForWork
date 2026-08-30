using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P1.World;
using GameForWork.Core.P4;
using GameForWork.Core.P14;
using GameForWork.Core.P20;
using GameForWork.Core.Simulation;

namespace GameForWork.Core.P28;

public sealed record P28EarnedEncounter(P14MapNode Node, bool Cleared, int Kills, int Total);
public sealed record P28RewardLedger(IReadOnlyList<P28EarnedEncounter> Encounters,
    int LifeForce, int RedFavor, int BlueFavor, int Merit, int Reputation, int Fragments,
    IReadOnlyList<ItemInstance> Equipment, IReadOnlyList<P1MapItem> Maps,
    MapStackableRewards Stackables, P28RewardPreference? BlueTarget = null, bool BlueTargetHit = false,
    int QualityStones = 0, int MutatedStones = 0);

public static class P28Rewards
{
    public static IReadOnlyList<P28EarnedEncounter> Progress(P1MapRunResult run)
    {
        P14MapNode[] nodes = run.Attempts.SelectMany(a => a.Timeline?.PlannedNodes ?? [])
            .DistinctBy(n => n.Index).ToArray();
        var kills = P20DropFormula.ExtractDefeated(run, run.Map.MonsterLevel);
        return nodes.Where(n => n.Gameplay is not null).Select(node =>
        {
            int killed = kills.Count(k => k.EntityKey.StartsWith(node.Index + ":", StringComparison.Ordinal));
            int total = run.Attempts.SelectMany(a => a.Timeline?.Events ?? [])
                .Where(e => e.NodeIndex == node.Index && e.Kind == P3.P3SceneEventKind.WaveStarted)
                .Select(e => e.Value).DefaultIfEmpty(node.EnemyCount).Max();
            bool cleared = run.Attempts.Any(a => a.Nodes.Any(n => n.NodeIndex == node.Index && n.Outcome == P1BattleOutcome.HeroVictory));
            return new P28EarnedEncounter(node, cleared, killed, Math.Max(1, total));
        }).ToArray();
    }

    public static P28RewardLedger Roll(P1MapRunResult run, ulong seed)
    {
        var random = new Pcg32(seed ^ 0x703238UL);
        P1MapItem map = run.Map;
        bool Has(string branch, int n) => P28Gameplay.Has(map.AtlasSnapshot, branch, n);
        int Count(int units, int multiplier) => P20DropFormula.RollScaledCount(units, multiplier, Next());
        ulong Next() => ((ulong)random.NextUInt() << 32) | random.NextUInt();
        var equipment = new List<ItemInstance>(); var maps = new List<P1MapItem>(); var metals = new List<MetalCurrencyStack>();
        int life = 0, red = 0, blue = 0, merit = 0, reputation = 0, fragments = 0, gold = 0, stones = 0, quality = 0, mutation = 0;
        P28RewardPreference? blueTarget = null; bool blueHit = false;
        IReadOnlyList<P28EarnedEncounter> progress = Progress(run);
        foreach (P28EarnedEncounter encounter in progress)
        {
            P28EncounterRule rule = encounter.Node.Gameplay!;
            if (encounter.Kills == 0) continue;
            string branch = rule.Mechanic switch
            {
                P28Mechanic.Abyss => "abyss", P28Mechanic.Garden => "garden", P28Mechanic.Red => "red",
                P28Mechanic.Blue => "blue", _ => "warfront",
            };
            int general = Has("map", 9) ? 4_000 : 0;
            int quantity = P28Gameplay.Scale(map.ItemQuantityBasisPoints, rule.Reward);
            int resource = 10_000 + general;
            int all = 10_000 + (Has(branch, 1) && rule.Mechanic is P28Mechanic.Abyss or P28Mechanic.Red or P28Mechanic.Blue ? branch == "abyss" ? 4_000 : 5_000 : 0);
            int killsRatio = Math.Min(10_000, encounter.Kills * 10_000 / encounter.Total);
            int earned = P28Gameplay.Scale(quantity, killsRatio);
            int completed = encounter.Cleared ? quantity : 0;
            P28RewardPreference preference = rule.Choice?.Reward ?? P28Gameplay.Policy(map).Reward;
            int equipmentRate = 0, metalRate = 0, stoneRate = 0, goldRate = 0;
            switch (rule.Mechanic)
            {
                case P28Mechanic.Abyss:
                    int rare = Has(branch, 5) && run.Attempts.SelectMany(a => a.Timeline?.SpatialFrames ?? [])
                        .Where(f => f.NodeIndex == encounter.Node.Index).SelectMany(f => f.Enemies)
                        .Any(e => e.Rarity == EnemyRarity.Rare && e.Life == 0) ? 8_000 : 0;
                    equipmentRate = P28Gameplay.Scale(earned, all + rare) / 2;
                    metalRate = P28Gameplay.Scale(earned, all + (Has(branch, 3) ? 6_000 : 0));
                    stoneRate = P28Gameplay.Scale(earned, all + (Has(branch, 2) ? 6_000 : 0));
                    if (rule.Terminal && encounter.Cleared)
                    {
                        int cacheQuantity = P28Gameplay.Scale(quantity, rule.TerminalReward);
                        equipmentRate += P28Gameplay.Scale(cacheQuantity, all);
                        metalRate += P28Gameplay.Scale(cacheQuantity, all + (Has(branch, 3) ? 6_000 : 0));
                        stoneRate += P28Gameplay.Scale(cacheQuantity, all + (Has(branch, 2) ? 6_000 : 0));
                        fragments += Count(1, P28Gameplay.Scale(P28Gameplay.Scale(quantity, rule.TerminalReward), all + (Has(branch, 9) ? 10_000 : 0)));
                        if (random.NextBasisPoints() < Math.Min(10_000, P28Gameplay.Scale(800,
                            P28Gameplay.Scale(cacheQuantity, all + (Has(branch, 10) ? 10_000 : 0)))))
                            equipment.Add(P14UniqueItems.Create("core.unique.echoing_oathbreaker", map.MonsterLevel, $"p28-abyss-{map.InstanceId}"));
                    }
                    break;
                case P28Mechanic.Garden:
                    life += Count(map.Tier * rule.Units * 3, P28Gameplay.Scale(earned, resource + (Has(branch, 1) ? 5_000 : 0)));
                    equipmentRate = metalRate = P28Gameplay.Scale(earned, 10_000 + (Has(branch, 3) ? 5_000 : 0));
                    break;
                case P28Mechanic.Red:
                    // Guard kills retain normal loot; the pact bonus vests only after guards die.
                    red += Count(100, P28Gameplay.Scale(completed, all + general));
                    goldRate = P28Gameplay.Scale(completed, all + (Has(branch, 2) ? 8_000 : 0));
                    metalRate = P28Gameplay.Scale(completed, all + (Has(branch, 3) ? 8_000 : 0));
                    equipmentRate = P28Gameplay.Scale(completed, all + (Has(branch, 8) ? 8_000 : 0));
                    if (encounter.Cleared && rule.Choice?.Tag == "map")
                        for (int i = 0; i < Count(1, P28Gameplay.Scale(quantity, all + (Has(branch, 5) ? 8_000 : 0))); i++)
                            maps.Add(new P1MapItem($"p28-red-map-{map.InstanceId}-{encounter.Node.Index}-{i}", map.Tier).EnsureFormal(Next()));
                    if (encounter.Cleared && !string.IsNullOrEmpty(encounter.Node.BossStableId) && Has(branch, 11) && random.NextBasisPoints() < 1_200)
                        equipment.Add(P14UniqueItems.Create("core.unique.red_vow", map.MonsterLevel, $"p28-red-{map.InstanceId}-{encounter.Node.Index}"));
                    break;
                case P28Mechanic.Blue:
                    if (!run.Succeeded || !encounter.Cleared) continue;
                    int delayed = all + (Has(branch, 7) ? 8_000 : 0) + (Has(branch, 8) ? 8_000 : 0);
                    blue += Count(100, P28Gameplay.Scale(quantity, delayed + general));
                    blueTarget = preference;
                    equipmentRate = P28Gameplay.Scale(quantity, delayed + (Has(branch, 2) ? 8_000 : 0));
                    stoneRate = P28Gameplay.Scale(quantity, delayed + (Has(branch, 3) ? 8_000 : 0));
                    if (preference == P28RewardPreference.Legendary)
                    {
                        blueHit = random.NextBasisPoints() < Math.Min(9_500, P28Gameplay.Scale(800, P28Gameplay.Scale(quantity, delayed + (Has(branch, 5) ? 10_000 : 0))));
                        if (blueHit) equipment.Add(P14UniqueItems.Create("core.unique.blue_vow", map.MonsterLevel, $"p28-blue-{map.InstanceId}"));
                    }
                    break;
                case P28Mechanic.Warfront:
                    bool officer = encounter.Node.Kind == P14MapNodeKind.WarfrontOfficer;
                    int officerBonus = officer && Has(branch, 5) ? 8_000 : 0;
                    equipmentRate = P28Gameplay.Scale(earned, 10_000 + officerBonus + (Has(branch, 2) ? 5_000 : 0));
                    metalRate = P28Gameplay.Scale(earned, 10_000 + officerBonus + (Has(branch, 3) ? 5_000 : 0));
                    if (encounter.Cleared && rule.Terminal)
                    {
                        reputation += Count(1 + map.Tier / 5, Has(branch, 8) ? 20_000 : 10_000);
                        fragments += Count(1, P28Gameplay.Scale(quantity, Has(branch, 10) ? 20_000 : 10_000));
                    }
                    break;
            }
            if (preference == P28RewardPreference.Materials) { metalRate += equipmentRate; equipmentRate = 0; }
            gold += Count(map.Tier * 12, goldRate);
            int number = Count(rule.Units, equipmentRate);
            for (int i = 0; i < number; i++)
            {
                bool high = rule.Mechanic == P28Mechanic.Blue || rule.Mechanic == P28Mechanic.Warfront &&
                    encounter.Node.Kind == P14MapNodeKind.WarfrontOfficer && random.NextBasisPoints() < (Has(branch, 9) ? 6_000 : 3_000);
                int itemLevel = Math.Min(120, map.MonsterLevel + (rule.Mechanic == P28Mechanic.Garden && Has(branch, 7) ? 1 : 0) +
                    (rule.Mechanic == P28Mechanic.Blue && Has(branch, 9) ? 2 : 0));
                ItemInstance item = Equipment(preference, itemLevel, high, Next(), $"p28-{map.InstanceId}-{encounter.Node.Index}-{i}");
                if (rule.Mechanic == P28Mechanic.Garden)
                {
                    if (random.NextBasisPoints() < (Has(branch, 5) ? 6_000 : 3_000))
                    {
                        string tag = (rule.Selections ?? [rule.Choice!])[i % rule.Units].Tag;
                        P14GardenCraft craft = tag switch { "life" => P14GardenCraft.BiasLife, "defense" => P14GardenCraft.BiasDefense,
                            "spell" => P14GardenCraft.BiasSpell, _ => P14GardenCraft.BiasAttack };
                        if (P14GardenCrafting.CanApply(item, craft)) item = P14GardenCrafting.Apply(item, craft, Next());
                    }
                    item = item with { IsCraftingBase = random.NextBasisPoints() < (Has(branch, 6) ? 6_000 : 3_000) };
                }
                equipment.Add(item);
            }
            int metalCount = Count(rule.Units, metalRate);
            MetalCurrencyKind metal = rule.Mechanic == P28Mechanic.Abyss ? MetalCurrencyKind.CorruptionIron :
                rule.Mechanic == P28Mechanic.Red && random.NextBasisPoints() < (Has(branch, 9) ? 5_000 : 2_000)
                    ? MetalCurrencyKind.ExaltedGold : MetalCurrencyKind.AlchemicalGold;
            if (metalCount > 0) metals.Add(new(metal, metalCount));
            int stoneCount = Count(1, stoneRate / 3);
            stones += stoneCount;
            if (rule.Mechanic == P28Mechanic.Blue && preference == P28RewardPreference.SkillStones) blueHit = stoneCount > 0;
            if (rule.Mechanic == P28Mechanic.Blue && preference == P28RewardPreference.HighBases) blueHit = number > 0;
            if (rule.Mechanic == P28Mechanic.Abyss)
                for (int i = 0; i < stoneCount; i++)
                {
                    if (random.NextBasisPoints() < (Has(branch, 6) ? 2_000 : 1_000)) mutation++;
                    else if (random.NextBasisPoints() < (Has(branch, 6) ? 4_000 : 2_000)) quality++;
                }
        }
        // Fixed 10T base merit, apportioned among actual kills, officers and commander, not a flat failure award.
        P28EarnedEncounter[] war = progress.Where(p => p.Node.Gameplay?.Mechanic == P28Mechanic.Warfront && p.Node.EnemyCount > 0).ToArray();
        if (war.Length > 0)
        {
            int Weight(P28EarnedEncounter p) => p.Node.Kind == P14MapNodeKind.WarfrontCommander ? 4 :
                p.Node.Kind == P14MapNodeKind.WarfrontOfficer ? 3 : 1;
            int fraction = war.Sum(p => Weight(p) * Math.Min(10_000, p.Kills * 10_000 / p.Total)) / war.Sum(Weight);
            int multiplier = P28Gameplay.Scale(P28Gameplay.Scale(map.ItemQuantityBasisPoints, war[0].Node.Gameplay!.Reward),
                10_000 + (Has("map", 9) ? 4_000 : 0) + (Has("warfront", 1) ? 5_000 : 0));
            merit = P28Gameplay.Scale(P28Gameplay.Scale(map.Tier * 10, fraction), multiplier);
        }
        return new(progress, life, red, blue, merit, reputation, fragments, equipment, maps,
            new(gold, 0, 0, 0, stones, metals), blueTarget, blueHit, quality, mutation);
    }

    public static ItemInstance Equipment(P28RewardPreference preference, int level, bool high, ulong seed, string id)
    {
        ItemBaseDefinition[] pool = P1ItemBases.All.Where(b => b.RequiredLevel <= level && b.Category != ItemCategory.LifeFlask && preference switch
        {
            P28RewardPreference.Weapons => b.Category is ItemCategory.OneHandWeapon or ItemCategory.TwoHandWeapon,
            P28RewardPreference.Armor => b.Category is ItemCategory.BodyArmor or ItemCategory.Helmet or ItemCategory.Gloves or ItemCategory.Boots or ItemCategory.Shield,
            P28RewardPreference.Jewelry => b.Category is ItemCategory.Ring or ItemCategory.Amulet or ItemCategory.Belt, _ => true,
        }).OrderByDescending(b => b.RequiredLevel).ThenBy(b => b.StableId, StringComparer.Ordinal).ToArray();
        if (high) pool = pool.Take(Math.Max(1, pool.Length / 5)).ToArray();
        ItemBaseDefinition selected = pool[(int)(seed % (ulong)pool.Length)];
        return ItemGenerator.Generate(selected.StableId, level, ItemRarity.Rare, seed, id);
    }
}
