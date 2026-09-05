using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Spatial;
using GameForWork.Core.Content;
using GameForWork.Core.Economy;
using GameForWork.Core.Simulation;

namespace GameForWork.Core.Encounters;

public sealed record EarnedEncounter(MapNode Node, bool Cleared, int Kills, int Total);
public sealed record RewardLedger(IReadOnlyList<EarnedEncounter> Encounters,
    int LifeForce, int RedFavor, int BlueFavor, int Merit, int Reputation, int Fragments,
    IReadOnlyList<ItemInstance> Equipment, IReadOnlyList<MapItem> Maps,
    MapStackableRewards Stackables, RewardPreference? BlueTarget = null, bool BlueTargetHit = false,
    int QualityStones = 0, int MutatedStones = 0);

public static class Rewards
{
    public static IReadOnlyList<EarnedEncounter> Progress(MapRunResult run)
    {
        MapNode[] nodes = run.Attempts.SelectMany(a => a.Timeline?.PlannedNodes ?? [])
            .DistinctBy(n => n.Index).ToArray();
        var kills = DropFormula.ExtractDefeated(run, run.Map.MonsterLevel);
        return nodes.Where(n => n.Gameplay is not null).Select(node =>
        {
            int killed = kills.Count(k => k.EntityKey.StartsWith(node.Index + ":", StringComparison.Ordinal));
            int total = run.Attempts.SelectMany(a => a.Timeline?.Events ?? [])
                .Where(e => e.NodeIndex == node.Index && e.Kind == Scenes.SceneEventKind.WaveStarted)
                .Select(e => e.Value).DefaultIfEmpty(node.EnemyCount).Max();
            bool cleared = run.Attempts.Any(a => a.Nodes.Any(n => n.NodeIndex == node.Index && n.Outcome == BattleOutcome.HeroVictory));
            return new EarnedEncounter(node, cleared, killed, Math.Max(1, total));
        }).ToArray();
    }

    public static RewardLedger Roll(MapRunResult run, ulong seed)
    {
        var random = new Pcg32(seed ^ 0x703238UL);
        MapItem map = run.Map;
        bool Has(string branch, int n) => Gameplay.Has(map.AtlasSnapshot, branch, n);
        int Count(int units, int multiplier) => DropFormula.RollScaledCount(units, multiplier, Next());
        ulong Next() => ((ulong)random.NextUInt() << 32) | random.NextUInt();
        var equipment = new List<ItemInstance>(); var maps = new List<MapItem>(); var metals = new List<MetalCurrencyStack>();
        int life = 0, red = 0, blue = 0, merit = 0, reputation = 0, fragments = 0, gold = 0, stones = 0, quality = 0, mutation = 0;
        RewardPreference? blueTarget = null; bool blueHit = false;
        IReadOnlyList<EarnedEncounter> progress = Progress(run);
        foreach (EarnedEncounter encounter in progress)
        {
            EncounterRule rule = encounter.Node.Gameplay!;
            if (encounter.Kills == 0) continue;
            string branch = rule.Mechanic switch
            {
                Mechanic.Abyss => "abyss", Mechanic.Garden => "garden", Mechanic.Red => "red",
                Mechanic.Blue => "blue", _ => "warfront",
            };
            int general = Has("map", 9) ? 4_000 : 0;
            int quantity = Gameplay.Scale(map.ItemQuantityBasisPoints, rule.Reward);
            quantity = Gameplay.Scale(quantity, map.EquipmentSnapshot?.RewardMultiplier(rule.Mechanic) ?? 10_000);
            int resource = 10_000 + general;
            int all = 10_000 + (Has(branch, 1) && rule.Mechanic is Mechanic.Abyss or Mechanic.Red or Mechanic.Blue ? branch == "abyss" ? 4_000 : 5_000 : 0);
            int killsRatio = Math.Min(10_000, encounter.Kills * 10_000 / encounter.Total);
            int earned = Gameplay.Scale(quantity, killsRatio);
            int completed = encounter.Cleared ? quantity : 0;
            RewardPreference preference = rule.Choice?.Reward ?? Gameplay.Policy(map).Reward;
            int equipmentRate = 0, metalRate = 0, stoneRate = 0, goldRate = 0;
            switch (rule.Mechanic)
            {
                case Mechanic.Abyss:
                    int rare = Has(branch, 5) && run.Attempts.SelectMany(a => a.Timeline?.SpatialFrames ?? [])
                        .Where(f => f.NodeIndex == encounter.Node.Index).SelectMany(f => f.Enemies)
                        .Any(e => e.Rarity == EnemyRarity.Rare && e.Life == 0) ? 8_000 : 0;
                    equipmentRate = Gameplay.Scale(earned, all + rare) / 2;
                    metalRate = Gameplay.Scale(earned, all + (Has(branch, 3) ? 6_000 : 0));
                    stoneRate = Gameplay.Scale(earned, all + (Has(branch, 2) ? 6_000 : 0));
                    if (rule.Terminal && encounter.Cleared)
                    {
                        int cacheQuantity = Gameplay.Scale(quantity, rule.TerminalReward);
                        equipmentRate += Gameplay.Scale(cacheQuantity, all);
                        metalRate += Gameplay.Scale(cacheQuantity, all + (Has(branch, 3) ? 6_000 : 0));
                        stoneRate += Gameplay.Scale(cacheQuantity, all + (Has(branch, 2) ? 6_000 : 0));
                        fragments += Count(1, Gameplay.Scale(Gameplay.Scale(quantity, rule.TerminalReward), all + (Has(branch, 9) ? 10_000 : 0)));
                        if (random.NextBasisPoints() < Math.Min(10_000, Gameplay.Scale(800,
                            Gameplay.Scale(cacheQuantity, all + (Has(branch, 10) ? 10_000 : 0)))))
                            equipment.Add(UniqueItems.Create("core.unique.echoing_oathbreaker", map.MonsterLevel, $"encounters-abyss-{map.InstanceId}"));
                    }
                    break;
                case Mechanic.Garden:
                    life += Count(map.Tier * rule.Units * 3, Gameplay.Scale(earned, resource + (Has(branch, 1) ? 5_000 : 0)));
                    equipmentRate = metalRate = Gameplay.Scale(earned, 10_000 + (Has(branch, 3) ? 5_000 : 0));
                    break;
                case Mechanic.Red:
                    // Guard kills retain normal loot; the pact bonus vests only after guards die.
                    red += Count(100, Gameplay.Scale(completed, all + general));
                    goldRate = Gameplay.Scale(completed, all + (Has(branch, 2) ? 8_000 : 0));
                    metalRate = Gameplay.Scale(completed, all + (Has(branch, 3) ? 8_000 : 0));
                    equipmentRate = Gameplay.Scale(completed, all + (Has(branch, 8) ? 8_000 : 0));
                    if (encounter.Cleared && rule.Choice?.Tag == "map")
                        for (int i = 0; i < Count(1, Gameplay.Scale(quantity, all + (Has(branch, 5) ? 8_000 : 0))); i++)
                            maps.Add(new MapItem($"encounters-red-map-{map.InstanceId}-{encounter.Node.Index}-{i}", map.Tier).EnsureFormal(Next()));
                    if (encounter.Cleared && !string.IsNullOrEmpty(encounter.Node.BossStableId) && Has(branch, 11) && random.NextBasisPoints() < 1_200)
                        equipment.Add(UniqueItems.Create("core.unique.red_vow", map.MonsterLevel, $"encounters-red-{map.InstanceId}-{encounter.Node.Index}"));
                    break;
                case Mechanic.Blue:
                    if (!run.Succeeded || !encounter.Cleared) continue;
                    int delayed = all + (Has(branch, 7) ? 8_000 : 0) + (Has(branch, 8) ? 8_000 : 0);
                    blue += Count(100, Gameplay.Scale(quantity, delayed + general));
                    blueTarget = preference;
                    equipmentRate = Gameplay.Scale(quantity, delayed + (Has(branch, 2) ? 8_000 : 0));
                    stoneRate = Gameplay.Scale(quantity, delayed + (Has(branch, 3) ? 8_000 : 0));
                    if (preference == RewardPreference.Legendary)
                    {
                        blueHit = random.NextBasisPoints() < Math.Min(9_500, Gameplay.Scale(800, Gameplay.Scale(quantity, delayed + (Has(branch, 5) ? 10_000 : 0))));
                        if (blueHit) equipment.Add(UniqueItems.Create("core.unique.blue_vow", map.MonsterLevel, $"encounters-blue-{map.InstanceId}"));
                    }
                    break;
                case Mechanic.Warfront:
                    bool officer = encounter.Node.Kind == MapNodeKind.WarfrontOfficer;
                    int officerBonus = officer && Has(branch, 5) ? 8_000 : 0;
                    equipmentRate = Gameplay.Scale(earned, 10_000 + officerBonus + (Has(branch, 2) ? 5_000 : 0));
                    metalRate = Gameplay.Scale(earned, 10_000 + officerBonus + (Has(branch, 3) ? 5_000 : 0));
                    if (encounter.Cleared && rule.Terminal)
                    {
                        reputation += Count(1 + map.Tier / 5, Has(branch, 8) ? 20_000 : 10_000);
                        fragments += Count(1, Gameplay.Scale(quantity, Has(branch, 10) ? 20_000 : 10_000));
                    }
                    break;
            }
            if (preference == RewardPreference.Materials) { metalRate += equipmentRate; equipmentRate = 0; }
            gold += Count(map.Tier * 12, goldRate);
            int number = Count(rule.Units, equipmentRate);
            for (int i = 0; i < number; i++)
            {
                bool high = rule.Mechanic == Mechanic.Blue || rule.Mechanic == Mechanic.Warfront &&
                    encounter.Node.Kind == MapNodeKind.WarfrontOfficer && random.NextBasisPoints() < (Has(branch, 9) ? 6_000 : 3_000);
                int itemLevel = Math.Min(120, map.MonsterLevel + (rule.Mechanic == Mechanic.Garden && Has(branch, 7) ? 1 : 0) +
                    (rule.Mechanic == Mechanic.Blue && Has(branch, 9) ? 2 : 0));
                ItemInstance item = Equipment(preference, itemLevel, high, Next(), $"encounters-{map.InstanceId}-{encounter.Node.Index}-{i}");
                item = item with { DropSource = $"resources.source.{rule.Mechanic}.{(high ? "rare" : "normal")}".ToLowerInvariant() };
                if (rule.Mechanic == Mechanic.Garden)
                {
                    if (random.NextBasisPoints() < (Has(branch, 5) ? 6_000 : 3_000))
                    {
                        string tag = (rule.Selections ?? [rule.Choice!])[i % rule.Units].Tag;
                        GardenCraft craft = tag switch { "life" => GardenCraft.BiasLife, "defense" => GardenCraft.BiasDefense,
                            "spell" => GardenCraft.BiasSpell, _ => GardenCraft.BiasAttack };
                        if (GardenCrafting.CanApply(item, craft)) item = GardenCrafting.Apply(item, craft, Next());
                    }
                    item = item with { IsCraftingBase = random.NextBasisPoints() < (Has(branch, 6) ? 6_000 : 3_000) };
                }
                equipment.Add(item);
            }
            int metalCount = Count(rule.Units, metalRate);
            MetalCurrencyKind metal = rule.Mechanic == Mechanic.Abyss ? MetalCurrencyKind.CorruptionIron :
                rule.Mechanic == Mechanic.Red && random.NextBasisPoints() < (Has(branch, 9) ? 5_000 : 2_000)
                    ? MetalCurrencyKind.ExaltedGold : MetalCurrencyKind.AlchemicalGold;
            if (metalCount > 0) metals.Add(new(metal, metalCount));
            int stoneCount = Count(1, stoneRate / 3);
            stones += stoneCount;
            if (rule.Mechanic == Mechanic.Blue && preference == RewardPreference.SkillStones) blueHit = stoneCount > 0;
            if (rule.Mechanic == Mechanic.Blue && preference == RewardPreference.HighBases) blueHit = number > 0;
            if (rule.Mechanic == Mechanic.Abyss)
                for (int i = 0; i < stoneCount; i++)
                {
                    if (random.NextBasisPoints() < (Has(branch, 6) ? 2_000 : 1_000)) mutation++;
                    else if (random.NextBasisPoints() < (Has(branch, 6) ? 4_000 : 2_000)) quality++;
                }
        }
        // Fixed 10T base merit, apportioned among actual kills, officers and commander, not a flat failure award.
        EarnedEncounter[] war = progress.Where(p => p.Node.Gameplay?.Mechanic == Mechanic.Warfront && p.Node.EnemyCount > 0).ToArray();
        if (war.Length > 0)
        {
            int Weight(EarnedEncounter p) => p.Node.Kind == MapNodeKind.WarfrontCommander ? 4 :
                p.Node.Kind == MapNodeKind.WarfrontOfficer ? 3 : 1;
            int fraction = war.Sum(p => Weight(p) * Math.Min(10_000, p.Kills * 10_000 / p.Total)) / war.Sum(Weight);
            int multiplier = Gameplay.Scale(Gameplay.Scale(map.ItemQuantityBasisPoints, war[0].Node.Gameplay!.Reward),
                10_000 + (Has("map", 9) ? 4_000 : 0) + (Has("warfront", 1) ? 5_000 : 0));
            merit = Gameplay.Scale(Gameplay.Scale(map.Tier * 10, fraction), multiplier);
        }
        return new(progress, life, red, blue, merit, reputation, fragments, equipment, maps,
            new(gold, 0, 0, 0, stones, metals), blueTarget, blueHit, quality, mutation);
    }

    public static ItemInstance Equipment(RewardPreference preference, int level, bool high, ulong seed, string id)
    {
        ItemBaseDefinition[] pool = ItemBases.All.Where(b => b.RequiredLevel <= level && b.Category != ItemCategory.LifeFlask && preference switch
        {
            RewardPreference.Weapons => b.Category is ItemCategory.OneHandWeapon or ItemCategory.TwoHandWeapon,
            RewardPreference.Armor => b.Category is ItemCategory.BodyArmor or ItemCategory.Helmet or ItemCategory.Gloves or ItemCategory.Boots or ItemCategory.Shield,
            RewardPreference.Jewelry => b.Category is ItemCategory.Ring or ItemCategory.Amulet or ItemCategory.Belt, _ => true,
        }).OrderByDescending(b => b.RequiredLevel).ThenBy(b => b.StableId, StringComparer.Ordinal).ToArray();
        if (high) pool = pool.Take(Math.Max(1, pool.Length / 5)).ToArray();
        ItemBaseDefinition selected = pool[(int)(seed % (ulong)pool.Length)];
        return ItemGenerator.Generate(selected.StableId, level, ItemRarity.Rare, seed, id);
    }
}
