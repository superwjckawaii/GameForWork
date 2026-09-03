using System.Security.Cryptography;
using System.Text;
using GameForWork.Core.P1.Items;

namespace GameForWork.Core.Equipment;

public sealed record P32EquipmentAuditResult(
    int SampleCount,
    int CoveredBaseCount,
    int CatalogBaseCount,
    int ObservedAffixFamilyCount,
    int CatalogAffixFamilyCount,
    int BasicCount,
    int MagicCount,
    int RareCount,
    string DeterministicDigest,
    IReadOnlyList<string> Failures)
{
    public bool Succeeded => Failures.Count == 0;

    public string RenderMarkdown() => $"""
        # P32 装备固定种子自动审计

        | 项目 | 结果 |
        | --- | ---: |
        | 固定种子装备 | {SampleCount:N0} |
        | 覆盖底材 | {CoveredBaseCount}/{CatalogBaseCount} |
        | 观察到的自然词缀家族 | {ObservedAffixFamilyCount}/{CatalogAffixFamilyCount} |
        | 普通 / 魔法 / 稀有 | {BasicCount:N0} / {MagicCount:N0} / {RareCount:N0} |
        | 确定性摘要 | `{DeterministicDigest}` |
        | 失败 | {Failures.Count} |

        审计固定覆盖全部正式底材、1～120物品等级、三种非传奇稀有度、底材数值、全部基底组件、词缀合法性、数值范围、前后缀容量、互斥组、永久定义引用和跨运行确定性。
        """ + (Failures.Count == 0 ? "\n\n结果：通过。\n" : "\n\n## 失败\n\n" + string.Join("\n", Failures.Select(value => $"- {value}")) + "\n");
}

public static class P32EquipmentAudit
{
    public const int RequiredSampleCount = 100_000;
    private const int MaximumReportedFailures = 100;
    private const ulong SeedSalt = 0x32c0ffee5eed1234UL;

    public static P32EquipmentAuditResult Run(int sampleCount = RequiredSampleCount)
    {
        if (sampleCount < RequiredSampleCount)
            throw new ArgumentOutOfRangeException(nameof(sampleCount), $"P32 audit requires at least {RequiredSampleCount:N0} items.");

        ItemBaseDefinition[] bases = EquipmentCatalog.Bases.OrderBy(value => value.StableId, StringComparer.Ordinal).ToArray();
        var coveredBases = new HashSet<string>(StringComparer.Ordinal);
        var observedFamilies = new HashSet<string>(StringComparer.Ordinal);
        var failures = new List<string>();
        int basic = 0;
        int magic = 0;
        int rare = 0;
        using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        ValidateCatalogReachability(bases, failures);
        for (int index = 0; index < sampleCount; index++)
        {
            ItemBaseDefinition itemBase = bases[index % bases.Length];
            int cycle = index / bases.Length;
            int itemLevel = 1 + (cycle * 37 + index * 17) % 120;
            ItemRarity rarity = (cycle % 10) switch
            {
                0 or 1 => ItemRarity.Basic,
                2 or 3 or 4 => ItemRarity.Magic,
                _ => ItemRarity.Rare,
            };
            ulong seed = SeedFor(index);
            ItemInstance item = ItemGenerator.Generate(itemBase.StableId, itemLevel, rarity, seed, $"p32-audit-{index:D6}");
            coveredBases.Add(item.Base.StableId);
            foreach (AffixRoll affix in item.Affixes) observedFamilies.Add(affix.Definition.StableFamilyId);
            if (rarity == ItemRarity.Basic) basic++;
            else if (rarity == ItemRarity.Magic) magic++;
            else rare++;

            ValidateItem(index, itemBase, itemLevel, rarity, item, failures);
            byte[] signature = Encoding.UTF8.GetBytes(Signature(item));
            digest.AppendData(signature);
        }

        // Recreate a bounded prefix independently. This catches mutable catalog references and
        // process-local/random hash use without doubling the cost of the full audit.
        for (int index = 0; index < Math.Min(2_048, sampleCount); index++)
        {
            ItemBaseDefinition itemBase = bases[index % bases.Length];
            int cycle = index / bases.Length;
            int itemLevel = 1 + (cycle * 37 + index * 17) % 120;
            ItemRarity rarity = (cycle % 10) switch
            {
                0 or 1 => ItemRarity.Basic,
                2 or 3 or 4 => ItemRarity.Magic,
                _ => ItemRarity.Rare,
            };
            ItemInstance first = ItemGenerator.Generate(itemBase.StableId, itemLevel, rarity, SeedFor(index), $"p32-audit-{index:D6}");
            ItemInstance second = ItemGenerator.Generate(itemBase.StableId, itemLevel, rarity, SeedFor(index), $"p32-audit-{index:D6}");
            if (Signature(first) != Signature(second)) AddFailure(failures, $"#{index}: repeated generation is not deterministic");
        }

        return new P32EquipmentAuditResult(sampleCount, coveredBases.Count, bases.Length, observedFamilies.Count,
            EquipmentCatalog.Affixes.Select(value => value.StableFamilyId).Distinct(StringComparer.Ordinal).Count(),
            basic, magic, rare, Convert.ToHexString(digest.GetHashAndReset()).ToLowerInvariant(), failures);
    }

    private static void ValidateCatalogReachability(IReadOnlyList<ItemBaseDefinition> bases, List<string> failures)
    {
        foreach (IGrouping<string, AffixDefinition> family in EquipmentCatalog.Affixes.GroupBy(value => value.StableFamilyId, StringComparer.Ordinal))
        {
            if (family.All(affix => affix.Weight <= 0 || !bases.Any(affix.Supports)))
                AddFailure(failures, $"unreachable affix family: {family.Key}");
            if (family.SelectMany(affix => affix.EffectComponents).Any(component => component.Kind == ItemModifierKind.None))
                AddFailure(failures, $"affix family has placeholder component: {family.Key}");
        }
        foreach (ItemBaseDefinition itemBase in bases)
        {
            if (!itemBase.StableId.StartsWith("equipment.base.", StringComparison.Ordinal)) AddFailure(failures, $"non-permanent base id: {itemBase.StableId}");
            if (P1Affixes.For(itemBase, 120).Count == 0 && itemBase.Category != ItemCategory.LifeFlask)
                AddFailure(failures, $"base has no natural affixes: {itemBase.StableId}");
            int corruptionCandidates = EquipmentCatalog.CorruptionImplicits.Count(value => EquipmentCorruptionCatalog.Supports(value, itemBase));
            if (corruptionCandidates == 0)
                AddFailure(failures, $"base has no corruption candidate: {itemBase.StableId}");
        }
        foreach (EquipmentCorruptionImplicitEntry corruption in EquipmentCatalog.CorruptionImplicits)
        {
            IReadOnlyList<AffixModifierComponent> components = EquipmentCorruptionCatalog.Components(corruption);
            if (components.Count == 0 || components.Any(value => value.Kind == ItemModifierKind.None))
                AddFailure(failures, $"corruption has no executable components: {corruption.Id}");
            if (!bases.Any(itemBase => EquipmentCorruptionCatalog.Supports(corruption, itemBase)))
                AddFailure(failures, $"unreachable corruption implicit: {corruption.Id}");
        }
        foreach (ItemEnchantment enchantment in EquipmentEnchantmentCatalog.All)
        {
            if (enchantment.EffectComponents.Count == 0 ||
                enchantment.EffectComponents.Any(value => value.Kind == ItemModifierKind.None && value.Scope != ItemModifierScope.Rule))
                AddFailure(failures, $"enchantment has placeholder component: {enchantment.StableId}");
            if (!bases.Any(enchantment.Supports)) AddFailure(failures, $"unreachable enchantment: {enchantment.StableId}");
        }
        foreach ((EquipmentLegendaryEntry legendary, int index) in EquipmentCatalog.LegendaryItems.Select((value, index) => (value, index)))
        {
            ItemInstance item = EquipmentLegendaryFactory.Create(legendary.Id, 100, $"p32-legendary-audit-{index}", (ulong)index + SeedSalt);
            if (item.Affixes.Count == 0 || item.Affixes.SelectMany(value => value.Effects).Any(value => value.Kind == ItemModifierKind.None))
                AddFailure(failures, $"legendary has placeholder fixed affix: {legendary.Id}");
        }
        string[] operationKinds = ["Metal", "LifeEnergy", "Oath", "Enchantment", "LegendaryExchange"];
        foreach (EquipmentCraftingOperationEntry operation in EquipmentCatalog.CraftingOperations)
            if (!operationKinds.Contains(operation.Kind, StringComparer.Ordinal))
                AddFailure(failures, $"crafting operation has no executor: {operation.Id} ({operation.Kind})");
    }

    private static void ValidateItem(int index, ItemBaseDefinition itemBase, int itemLevel, ItemRarity rarity,
        ItemInstance item, List<string> failures)
    {
        string prefix = $"#{index} {itemBase.StableId} ilvl {itemLevel} {rarity}";
        if (!ReferenceEquals(item.Base, itemBase)) AddFailure(failures, $"{prefix}: base definition is not canonical");
        if (item.ItemLevel != itemLevel) AddFailure(failures, $"{prefix}: item level changed to {item.ItemLevel}");
        CheckRange(item.RolledBaseArmor, itemBase.ArmorMinimum, itemBase.ArmorMaximum, "armor");
        CheckRange(item.RolledBaseEvasion, itemBase.EvasionMinimum, itemBase.EvasionMaximum, "evasion");
        CheckRange(item.RolledBaseShield, itemBase.ShieldMinimum, itemBase.ShieldMaximum, "shield");
        CheckRange(item.RolledBaseSpiritBarrier, itemBase.SpiritBarrierMinimum, itemBase.SpiritBarrierMaximum, "spirit barrier");

        EquipmentBaseEntry entry = EquipmentCatalog.Snapshot.Bases.Single(value => value.Id == itemBase.StableId);
        IReadOnlyList<RolledAffixComponent> implicitComponents = item.EffectiveImplicitComponents;
        if (implicitComponents.Count != entry.ImplicitComponents.Count)
            AddFailure(failures, $"{prefix}: implicit component count {implicitComponents.Count}/{entry.ImplicitComponents.Count}");
        foreach ((RolledAffixComponent rolled, EquipmentComponentEntry definition) in implicitComponents.Zip(entry.ImplicitComponents))
        {
            if (!Enum.TryParse(definition.Kind, out ItemModifierKind kind) || rolled.Kind != kind ||
                rolled.Value < definition.MinimumValue || rolled.Value > definition.MaximumValue)
                AddFailure(failures, $"{prefix}: illegal implicit {rolled.Kind}={rolled.Value}, expected {definition.Kind} {definition.MinimumValue}..{definition.MaximumValue}");
        }

        int maximumPerPosition = rarity == ItemRarity.Magic ? 1 : rarity == ItemRarity.Rare ? 3 : 0;
        if (item.PrefixCount > maximumPerPosition || item.SuffixCount > maximumPerPosition)
            AddFailure(failures, $"{prefix}: prefix/suffix capacity exceeded ({item.PrefixCount}/{item.SuffixCount})");
        int availableCapacity = P1Affixes.For(itemBase, itemLevel)
            .GroupBy(value => value.MutualExclusionGroup, StringComparer.Ordinal)
            .Select(group => group.First().Position)
            .GroupBy(position => position)
            .Sum(group => Math.Min(maximumPerPosition, group.Count()));
        int minimumExpected = rarity switch
        {
            ItemRarity.Basic => 0,
            ItemRarity.Magic => Math.Min(1, availableCapacity),
            ItemRarity.Rare => Math.Min(4, availableCapacity),
            _ => 0,
        };
        int maximumExpected = rarity switch { ItemRarity.Basic => 0, ItemRarity.Magic => 2, ItemRarity.Rare => 6, _ => 0 };
        if (item.Affixes.Count < minimumExpected || item.Affixes.Count > maximumExpected)
            AddFailure(failures, $"{prefix}: illegal affix count {item.Affixes.Count}");
        if (item.Affixes.Select(value => value.Definition.StableFamilyId).Distinct(StringComparer.Ordinal).Count() != item.Affixes.Count)
            AddFailure(failures, $"{prefix}: duplicate affix family");
        if (item.Affixes.Select(value => value.Definition.MutualExclusionGroup).Distinct(StringComparer.Ordinal).Count() != item.Affixes.Count)
            AddFailure(failures, $"{prefix}: duplicate mutual-exclusion group");
        foreach (AffixRoll roll in item.Affixes)
        {
            AffixDefinition definition = roll.Definition;
            if (!EquipmentCatalog.Affixes.Contains(definition) || !definition.Supports(itemBase) || definition.MinimumItemLevel > itemLevel)
                AddFailure(failures, $"{prefix}: illegal affix definition {definition.StableFamilyId}/T{definition.Tier}");
            if (roll.Effects.Count != definition.EffectComponents.Count)
                AddFailure(failures, $"{prefix}: component count mismatch for {definition.StableFamilyId}");
            foreach ((RolledAffixComponent value, AffixModifierComponent component) in roll.Effects.Zip(definition.EffectComponents))
            {
                if (value.Kind != component.Kind || value.Scope != component.Scope || value.Value < component.MinimumValue || value.Value > component.MaximumValue)
                    AddFailure(failures, $"{prefix}: component out of range for {definition.StableFamilyId}: {value.Kind}={value.Value}");
            }
        }
        return;

        void CheckRange(int value, int minimum, int maximum, string label)
        {
            if (minimum == 0 && maximum == 0)
            {
                if (value != 0) AddFailure(failures, $"{prefix}: {label} {value}, expected 0");
            }
            else if (value < minimum || value > maximum) AddFailure(failures, $"{prefix}: {label} {value}, expected {minimum}..{maximum}");
        }
    }

    private static ulong SeedFor(int index) => SeedSalt + (ulong)index * 0x9e3779b97f4a7c15UL;

    private static string Signature(ItemInstance item) => string.Join('|',
        item.InstanceId, item.Base.StableId, item.ItemLevel, item.Rarity, item.RolledBaseArmor,
        item.RolledBaseEvasion, item.RolledBaseShield, item.RolledBaseSpiritBarrier, item.ImplicitValue,
        item.LinkedSocketCount, item.RolledName,
        string.Join(';', item.EffectiveImplicitComponents.Select(value => $"{value.Kind}:{value.Scope}:{value.Value}")),
        string.Join(';', item.Affixes.Select(value => $"{value.Definition.StableFamilyId}:T{value.Definition.Tier}:{string.Join(',', value.Effects.Select(effect => effect.Value))}")));

    private static void AddFailure(List<string> failures, string failure)
    {
        if (failures.Count < MaximumReportedFailures) failures.Add(failure);
    }
}
