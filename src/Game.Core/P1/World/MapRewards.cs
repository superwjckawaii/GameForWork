using GameForWork.Core.P1.Items;
using GameForWork.Core.Simulation;

namespace GameForWork.Core.P1.World;

public sealed record MapStackableRewards(
    int Gold,
    int IronScraps,
    int MemoryAshes,
    int WardenMarks,
    int SkillStones);

public sealed record P1MapRewards(
    int Experience,
    IReadOnlyList<ItemInstance> Equipment,
    IReadOnlyList<P1MapItem> Maps,
    MapStackableRewards Stackables,
    bool LegendaryDropped);

public static class P1MapRewardGenerator
{
    public const int ExperiencePerMap = 190;
    private static readonly string[] EquipmentBases =
    [
        "core.base.rusted_greatsword",
        "core.base.heavy_battleaxe",
        "core.base.pole_warhammer",
        "core.base.crude_chainmail",
        "core.base.hide_coat",
        "core.base.runed_robe",
        "core.base.iron_helmet",
        "core.base.hunter_hood",
        "core.base.ash_circlet",
        "core.base.iron_ring",
        "core.base.life_ring",
        "core.base.focus_ring",
        "core.base.life_flask",
    ];

    public static P1MapRewards Generate(P1MapItem completedMap, MapRoute route, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(completedMap);
        completedMap.Validate();
        var random = new Pcg32(seed);
        int itemCount = 3 + Next(random, 3) + (route == MapRoute.Safe ? 2 : 0);
        var equipment = new List<ItemInstance>(itemCount + 1);
        for (int index = 0; index < itemCount; index++)
        {
            string baseId = EquipmentBases[Next(random, EquipmentBases.Length)];
            ItemRarity rarity = baseId == "core.base.life_flask" ? ItemRarity.Basic : RollRarity(random);
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
            equipment.Add(P1Legendary.Create(completedMap.AreaLevel) with
            {
                InstanceId = $"drop-{completedMap.InstanceId}-echoing-oathbreaker-{seed:x8}",
            });
        }

        var maps = new List<P1MapItem>(2);
        if (random.NextBasisPoints() < 8_500)
        {
            maps.Add(CreateDroppedMap(completedMap, random, maps.Count));
        }

        if (random.NextBasisPoints() < 1_000)
        {
            maps.Add(CreateDroppedMap(completedMap, random, maps.Count));
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

        return new P1MapRewards(
            ExperiencePerMap,
            equipment,
            maps,
            new MapStackableRewards(gold, scraps, MemoryAshes: 1, WardenMarks: 1, skillStones),
            legendary);
    }

    private static P1MapItem CreateDroppedMap(P1MapItem source, Pcg32 random, int ordinal)
    {
        int areaLevel = random.NextBasisPoints() < 6_000
            ? Math.Min(P1MapItem.MaximumAreaLevel, source.AreaLevel + 1)
            : source.AreaLevel;
        return new P1MapItem($"map-{source.InstanceId}-{ordinal}-{random.NextUInt():x8}", areaLevel);
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

    private static int Next(Pcg32 random, int exclusiveMaximum) =>
        (int)(random.NextUInt() % (uint)exclusiveMaximum);
}
