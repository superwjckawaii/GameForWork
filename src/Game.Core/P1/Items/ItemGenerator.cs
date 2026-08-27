using GameForWork.Core.Simulation;

namespace GameForWork.Core.P1.Items;

public static class ItemGenerator
{
    public static ItemInstance Generate(
        string baseStableId,
        int itemLevel,
        ItemRarity rarity,
        ulong seed,
        string? instanceId = null)
    {
        if (rarity == ItemRarity.Legendary)
        {
            throw new ArgumentException("Legendary items use their explicit factory.", nameof(rarity));
        }

        ItemBaseDefinition itemBase = P1ItemBases.Get(baseStableId);
        int clampedLevel = Math.Clamp(itemLevel, 1, 60);
        var random = new Pcg32(seed);
        int implicitValue = RollInclusive(
            random,
            itemBase.ImplicitMinimumValue,
            itemBase.ImplicitMaximumValue);
        IReadOnlyList<AffixRoll> affixes = rarity switch
        {
            ItemRarity.Basic => Array.Empty<AffixRoll>(),
            ItemRarity.Magic => RollAffixes(itemBase, clampedLevel, 1 + Next(random, 2), 1, random),
            ItemRarity.Rare => RollAffixes(itemBase, clampedLevel, 2 + Next(random, 3), 3, random),
            _ => throw new ArgumentOutOfRangeException(nameof(rarity)),
        };

        string id = instanceId ?? $"generated-{seed:x16}-{baseStableId[(baseStableId.LastIndexOf('.') + 1)..]}";
        return new ItemInstance(id, itemBase, clampedLevel, rarity, affixes, ImplicitValue: implicitValue);
    }

    private static IReadOnlyList<AffixRoll> RollAffixes(
        ItemBaseDefinition itemBase,
        int itemLevel,
        int desiredCount,
        int maximumPerPosition,
        Pcg32 random)
    {
        var available = P1Affixes.For(itemBase.Category, itemLevel)
            .Where(affix => IsApplicableToBase(affix.ModifierKind, itemBase))
            .ToList();
        var selected = new List<AffixRoll>();
        while (selected.Count < desiredCount)
        {
            AffixDefinition[] candidates = available
                .Where(candidate => selected.Count(roll => roll.Definition.Position == candidate.Position) < maximumPerPosition)
                .ToArray();
            if (candidates.Length == 0)
            {
                break;
            }

            AffixDefinition definition = WeightedPick(candidates, random);
            selected.Add(new AffixRoll(
                definition,
                RollInclusive(random, definition.MinimumValue, definition.MaximumValue)));
            available.RemoveAll(candidate => candidate.StableFamilyId == definition.StableFamilyId);
        }

        return selected;
    }

    private static AffixDefinition WeightedPick(IReadOnlyList<AffixDefinition> candidates, Pcg32 random)
    {
        int totalWeight = candidates.Sum(candidate => candidate.Weight);
        int roll = Next(random, totalWeight);
        foreach (AffixDefinition candidate in candidates)
        {
            if (roll < candidate.Weight)
            {
                return candidate;
            }

            roll -= candidate.Weight;
        }

        return candidates[^1];
    }

    private static bool IsApplicableToBase(ItemModifierKind kind, ItemBaseDefinition itemBase) => kind switch
    {
        ItemModifierKind.IncreasedArmorBasisPoints => itemBase.Armor > 0,
        ItemModifierKind.IncreasedEvasionBasisPoints => itemBase.Evasion > 0,
        ItemModifierKind.IncreasedShieldBasisPoints => itemBase.Shield > 0,
        _ => true,
    };

    private static int RollInclusive(Pcg32 random, int minimum, int maximum)
    {
        if (maximum < minimum)
        {
            throw new ArgumentOutOfRangeException(nameof(maximum));
        }

        return minimum == maximum ? minimum : checked(minimum + Next(random, maximum - minimum + 1));
    }

    private static int Next(Pcg32 random, int exclusiveMaximum)
    {
        if (exclusiveMaximum <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(exclusiveMaximum));
        }

        return (int)(random.NextUInt() % (uint)exclusiveMaximum);
    }
}
