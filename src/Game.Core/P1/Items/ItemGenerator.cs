using GameForWork.Core.Simulation;
using GameForWork.Core.P6;

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
        int clampedLevel = Math.Clamp(itemLevel, 1, 120);
        var random = new Pcg32(seed);
        int implicitValue = RollInclusive(
            random,
            itemBase.ImplicitMinimumValue,
            itemBase.ImplicitMaximumValue);
        IReadOnlyList<AffixRoll> affixes = rarity switch
        {
            ItemRarity.Basic => Array.Empty<AffixRoll>(),
            ItemRarity.Magic => RollAffixes(itemBase, clampedLevel, 1 + Next(random, 2), 1, random),
            ItemRarity.Rare => RollAffixes(itemBase, clampedLevel, 4 + Next(random, 3), 3, random),
            _ => throw new ArgumentOutOfRangeException(nameof(rarity)),
        };

        string id = instanceId ?? $"generated-{seed:x16}-{baseStableId[(baseStableId.LastIndexOf('.') + 1)..]}";
        int sockets = P6SocketRules.Roll(itemBase.Category, clampedLevel, seed);
        return new ItemInstance(id, itemBase, clampedLevel, rarity, affixes,
            ImplicitValue: implicitValue, LinkedSocketCount: sockets,
            RolledName: GenerateName(itemBase, rarity, affixes, seed));
    }

    private static IReadOnlyList<AffixRoll> RollAffixes(
        ItemBaseDefinition itemBase,
        int itemLevel,
        int desiredCount,
        int maximumPerPosition,
        Pcg32 random)
    {
        var available = P1Affixes.For(itemBase, itemLevel)
            .Where(affix => IsApplicableToBase(affix, itemBase))
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

            AffixDefinition definition = WeightedPick(candidates, itemBase, random);
            selected.Add(Roll(definition, random));
            available.RemoveAll(candidate =>
                candidate.StableFamilyId == definition.StableFamilyId ||
                candidate.MutualExclusionGroup == definition.MutualExclusionGroup);
        }

        return selected;
    }

    public static AffixRoll? RollAdditionalAffix(ItemInstance item, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(item);
        var random = new Pcg32(seed);
        AffixDefinition[] candidates = P1Affixes.For(item.Base, item.ItemLevel)
            .Where(affix => IsApplicableToBase(affix, item.Base))
            .Where(affix => item.Affixes.All(existing => existing.Definition.StableFamilyId != affix.StableFamilyId))
            .Where(affix => item.Affixes.All(existing =>
                existing.Definition.MutualExclusionGroup != affix.MutualExclusionGroup))
            .Where(affix => affix.Position == AffixPosition.Prefix ? item.PrefixCount < 3 : item.SuffixCount < 3)
            .ToArray();
        if (candidates.Length == 0) return null;
        AffixDefinition selected = WeightedPick(candidates, item.Base, random);
        return Roll(selected, random);
    }

    private static AffixRoll Roll(AffixDefinition definition, Pcg32 random)
    {
        RolledAffixComponent[] components = definition.EffectComponents
            .Select(component => new RolledAffixComponent(
                component.Kind,
                RollInclusive(random, component.MinimumValue, component.MaximumValue),
                component.Scope,
                component.DisplayText))
            .ToArray();
        int primary = components.Length == 0
            ? RollInclusive(random, definition.MinimumValue, definition.MaximumValue)
            : components[0].Value;
        return new AffixRoll(definition, primary, Components: components);
    }

    private static AffixDefinition WeightedPick(
        IReadOnlyList<AffixDefinition> candidates,
        ItemBaseDefinition itemBase,
        Pcg32 random)
    {
        int totalWeight = candidates.Sum(candidate => candidate.WeightFor(itemBase));
        int roll = Next(random, totalWeight);
        foreach (AffixDefinition candidate in candidates)
        {
            int weight = candidate.WeightFor(itemBase);
            if (roll < weight)
            {
                return candidate;
            }

            roll -= weight;
        }

        return candidates[^1];
    }

    private static bool IsApplicableToBase(AffixDefinition definition, ItemBaseDefinition itemBase) =>
        definition.EffectComponents.Where(component => component.Scope == ItemModifierScope.LocalDefense).All(component => component.Kind switch
        {
            ItemModifierKind.IncreasedArmorBasisPoints => itemBase.Armor > 0 || definition.EffectComponents.Any(effect => effect.Kind == ItemModifierKind.FlatArmor),
            ItemModifierKind.IncreasedEvasionBasisPoints => itemBase.Evasion > 0 || definition.EffectComponents.Any(effect => effect.Kind == ItemModifierKind.FlatEvasion),
            ItemModifierKind.IncreasedShieldBasisPoints => itemBase.Shield > 0 || definition.EffectComponents.Any(effect => effect.Kind == ItemModifierKind.FlatShield),
            ItemModifierKind.IncreasedSpiritBarrierBasisPoints => itemBase.SpiritBarrier > 0 || definition.EffectComponents.Any(effect => effect.Kind == ItemModifierKind.FlatSpiritBarrier),
            _ => true,
        });

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

    private static string GenerateName(
        ItemBaseDefinition itemBase,
        ItemRarity rarity,
        IReadOnlyList<AffixRoll> affixes,
        ulong seed)
    {
        if (rarity == ItemRarity.Basic || affixes.Count == 0) return itemBase.DisplayName;
        if (rarity == ItemRarity.Magic)
        {
            string labels = string.Join("·", affixes.OrderBy(affix => affix.Definition.Position)
                .Select(affix => affix.Definition.DisplayName).Distinct().Take(2));
            return $"{labels}的{itemBase.DisplayName}";
        }

        string[] first = ["灰烬", "夜幕", "断誓", "星骸", "深渊", "赤铁", "暮光", "无声"];
        string[] second = ["之锋", "之壁", "之心", "之握", "之印", "之足", "之环", "之契"];
        return first[(int)(seed % (ulong)first.Length)] + second[(int)((seed >> 8) % (ulong)second.Length)];
    }
}
