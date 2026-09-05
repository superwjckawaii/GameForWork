using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Content;

namespace GameForWork.Core.Builds;

public sealed record VirtueVicePair(ItemModifierKind Virtue, ItemModifierKind Vice, string DisplayName);

public static class VirtueViceEquipment
{
    private static readonly (ItemModifierKind Kind, string Name)[] Virtues =
    [
        (ItemModifierKind.HoldMercyAtMaximum, "慈悲"),
        (ItemModifierKind.HoldTemperanceAtMaximum, "节制"),
        (ItemModifierKind.HoldHumilityAtMaximum, "谦逊"),
    ];

    private static readonly (ItemModifierKind Kind, string Name)[] Vices =
    [
        (ItemModifierKind.HoldRageAtMaximum, "暴怒"),
        (ItemModifierKind.HoldSlothAtMaximum, "懒惰"),
        (ItemModifierKind.HoldArroganceAtMaximum, "傲慢"),
    ];

    public static IReadOnlyList<VirtueVicePair> BeltPool { get; } =
        (from virtue in Virtues from vice in Vices select new VirtueVicePair(
            virtue.Kind, vice.Kind, $"{virtue.Name}与{vice.Name}")).ToArray();

    public static ItemInstance CreateBelt(int itemLevel, ulong seed, string instanceId)
    {
        ItemInstance template = UniqueItems.Create("builds.unique.paired_virtue_girdle", itemLevel, instanceId);
        return ApplyBeltRoll(template, seed);
    }

    public static ItemInstance ApplyBeltRoll(ItemInstance template, ulong seed)
    {
        if (template.LegendaryRule?.StableId != "builds.unique.paired_virtue_girdle")
            throw new ArgumentException("Only the paired virtue girdle can roll this pool.", nameof(template));
        VirtueVicePair pair = BeltPool[(int)(seed % (ulong)BeltPool.Count)];
        AffixRoll[] retained = template.Affixes.Where(affix => !affix.Effects.Any(effect => IsHeldVirtueOrVice(effect.Kind))).ToArray();
        return template with
        {
            Affixes = [.. retained, Fixed(template.Base, pair.Virtue, pair.DisplayName + "·美德"),
                Fixed(template.Base, pair.Vice, pair.DisplayName + "·恶德")],
            RolledName = $"两极德印·{pair.DisplayName}",
        };
    }

    private static AffixRoll Fixed(ItemBaseDefinition itemBase, ItemModifierKind kind, string text)
    {
        var component = new AffixModifierComponent(kind, 1, 1, ItemModifierScope.Rule, text);
        var definition = new AffixDefinition($"builds.legendary.virtue_vice.{kind}", text, itemBase.Category,
            AffixPosition.Suffix, 0, 1, 1, 1, 0, kind, SourceId: "builds.unique.paired_virtue_girdle",
            RawText: text, Source: "传奇固定", Components: [component]);
        return new AffixRoll(definition, 1, Components: [new(kind, 1, ItemModifierScope.Rule, text)]);
    }

    private static bool IsHeldVirtueOrVice(ItemModifierKind kind) => kind is
        ItemModifierKind.HoldMercyAtMaximum or ItemModifierKind.HoldTemperanceAtMaximum or
        ItemModifierKind.HoldHumilityAtMaximum or ItemModifierKind.HoldRageAtMaximum or
        ItemModifierKind.HoldSlothAtMaximum or ItemModifierKind.HoldArroganceAtMaximum;
}
