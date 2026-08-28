using GameForWork.Core.P1.Items;
using GameForWork.Core.Simulation;
using GameForWork.Core.P4;
using GameForWork.Core.P5;
using GameForWork.Core.P14;

namespace GameForWork.Core.P1.World;

public sealed record MapStackableRewards(
    int Gold,
    int IronScraps,
    int MemoryAshes,
    int WardenMarks,
    int SkillStones,
    IReadOnlyList<MetalCurrencyStack>? Metals = null);

public sealed record P1MapRewards(
    int Experience,
    IReadOnlyList<ItemInstance> Equipment,
    IReadOnlyList<P1MapItem> Maps,
    MapStackableRewards Stackables,
    bool LegendaryDropped);

public static class P1MapRewardGenerator
{
    public const int ExperiencePerMap = 190;
    private static readonly string[] EquipmentBases = P1ItemBases.All.Select(item => item.StableId).ToArray();

    public static P1MapRewards Generate(P1MapItem completedMap, MapRoute route, ulong seed, int maximumUnlockedTier = P1MapItem.MaximumAreaLevel)
    {
        ArgumentNullException.ThrowIfNull(completedMap);
        completedMap.Validate();
        var random = new Pcg32(seed);
        completedMap = completedMap.EnsureFormal(seed);
        int quantityBonus = Math.Max(0, completedMap.ItemQuantityBasisPoints - 10_000);
        int itemCount = 3 + Next(random, 3) + (route == MapRoute.Safe ? 2 : 0) + quantityBonus / 4_000;
        var equipment = new List<ItemInstance>(itemCount + 1);
        for (int index = 0; index < itemCount; index++)
        {
            string baseId = EquipmentBases[Next(random, EquipmentBases.Length)];
            ItemRarity rarity = P1ItemBases.Get(baseId).Category == ItemCategory.LifeFlask ? ItemRarity.Basic : RollRarity(random);
            ulong itemSeed = NextSeed(random);
            equipment.Add(ItemGenerator.Generate(
                baseId,
                completedMap.AreaLevel,
                rarity,
                itemSeed,
                $"drop-{completedMap.InstanceId}-{index}-{itemSeed:x8}"));
        }

        bool legendary = random.NextBasisPoints() < 1_000;
        if (legendary)
        {
            P14UniqueDefinition unique = P14UniqueItems.All.Where(item => !item.Mythic)
                .ElementAt(Next(random, P14UniqueItems.All.Count(item => !item.Mythic)));
            equipment.Add(P14UniqueItems.Create(unique.StableId, completedMap.AreaLevel,
                $"drop-{completedMap.InstanceId}-{unique.StableId.Split('.')[^1]}-{seed:x8}"));
        }

        var maps = new List<P1MapItem>(2);
        if (random.NextBasisPoints() < 8_500)
        {
            maps.Add(CreateDroppedMap(completedMap, random, maps.Count, maximumUnlockedTier));
        }

        if (random.NextBasisPoints() < 1_000)
        {
            maps.Add(CreateDroppedMap(completedMap, random, maps.Count, maximumUnlockedTier));
        }

        int gold = 15 + Next(random, 11) + (route == MapRoute.Safe ? 10 : 0);
        int scraps = 2 + Next(random, 3);
        int skillStones = 0;
        if (route == MapRoute.Abyss)
        {
            int abyssRewardCount = 1 + Next(random, 2);
            for (int index = 0; index < abyssRewardCount; index++)
            {
                if (Next(random, 2) == 0)
                {
                    skillStones++;
                }
                else
                {
                    scraps++;
                }
            }
        }
        else if (route == MapRoute.LifeGarden)
        {
            gold += 4;
            scraps += 2;
        }

        MetalCurrencyKind commonMetal = RollMetal(random, allowDangerous: false);
        var metals = new List<MetalCurrencyStack> { new(commonMetal, 1 + Next(random, 2)) };
        if (random.NextBasisPoints() < (route == MapRoute.Abyss ? 3_000 : 1_000))
            metals.Add(new MetalCurrencyStack(RollMetal(random, allowDangerous: route == MapRoute.Abyss), 1));
        if (route == MapRoute.Abyss || P5ExpeditionDirector.IsBoss(completedMap) || random.NextBasisPoints() < 3_000)
        {
            metals.Add(new MetalCurrencyStack(MetalCurrencyKind.ChainSteel,
                P5ExpeditionDirector.IsBoss(completedMap) ? 3 : route == MapRoute.Abyss ? 2 : 1));
        }

        return new P1MapRewards(
            ExperiencePerMap,
            equipment,
            maps,
            new MapStackableRewards(gold, scraps, MemoryAshes: 1, WardenMarks: 1, skillStones, metals),
            legendary);
    }

    private static P1MapItem CreateDroppedMap(P1MapItem source, Pcg32 random, int ordinal, int maximumUnlockedTier)
    {
        int areaLevel = random.NextBasisPoints() < 6_000
            ? Math.Min(maximumUnlockedTier, source.AreaLevel + 1)
            : source.AreaLevel;
        string id = $"map-{source.InstanceId}-{ordinal}-{random.NextUInt():x8}";
        return new P1MapItem(id, Math.Min(maximumUnlockedTier, areaLevel)).EnsureFormal(random.NextUInt());
    }

    private static ItemRarity RollRarity(Pcg32 random)
    {
        int roll = random.NextBasisPoints();
        return roll switch
        {
            < 5_000 => ItemRarity.Basic,
            < 8_500 => ItemRarity.Magic,
            _ => ItemRarity.Rare,
        };
    }

    private static ulong NextSeed(Pcg32 random) => ((ulong)random.NextUInt() << 32) | random.NextUInt();

    private static MetalCurrencyKind RollMetal(Pcg32 random, bool allowDangerous)
    {
        MetalCurrencyDefinition[] candidates = P4MetalCurrencies.All
            .Where(item => allowDangerous || item.Tier != MetalCurrencyTier.Dangerous).ToArray();
        int total = candidates.Sum(item => item.DropWeight);
        int roll = Next(random, total);
        foreach (MetalCurrencyDefinition candidate in candidates)
        {
            if (roll < candidate.DropWeight) return candidate.Kind;
            roll -= candidate.DropWeight;
        }
        return candidates[^1].Kind;
    }

    private static int Next(Pcg32 random, int exclusiveMaximum) =>
        (int)(random.NextUInt() % (uint)exclusiveMaximum);
}
