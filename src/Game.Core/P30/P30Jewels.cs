namespace GameForWork.Core.P30;

public enum P30JewelBase { Crimson, Verdant, Golden, Azure, Quad }
public enum P30JewelRarity { Normal, Magic, Rare, Legendary }
public enum P30JewelRadius { None, Small, Medium, Large }
public enum P30JewelAffixPosition { Prefix, Suffix, CorruptedImplicit }
public enum P30JewelCorruptionResult { PowerfulImplicit, Locked, Damaged, Destroyed }
public enum P30JewelCraftOperation { RerollRare, DissolveAffix, Corrupt, RerollLegendaryRadius }

public sealed record P30JewelAffix(string StableId, string DisplayName, P30JewelAffixPosition Position,
    int Tier, int Value, string Effect, IReadOnlyList<string> Tags);

public sealed record P30LegendaryJewelDefinition(string StableId, string DisplayName, P30JewelRadius Radius,
    string Effect, string Source, P30VirtueViceKind? Oath = null, int MinimumRadius = 0, int MaximumRadius = 0);

public sealed record P30JewelInstance(string InstanceId, P30JewelBase Base, int ItemLevel, P30JewelRarity Rarity,
    int Resonance, IReadOnlyList<P30JewelAffix> Affixes, P30LegendaryJewelDefinition? Legendary = null,
    bool Corrupted = false, bool Locked = false, int RolledRadius = 0)
{
    public int RequiredLevel => Math.Clamp(ItemLevel, 1, 100);
    public string DisplayName => Legendary?.DisplayName ?? P30Jewels.BaseName(Base);
    public int EffectiveRadius => RolledRadius > 0 ? RolledRadius : P30Jewels.RadiusValue(Legendary?.Radius ?? P30JewelRadius.None);
}

public sealed record P30JewelStateSnapshot(IReadOnlyList<P30JewelInstance> Items,
    IReadOnlyDictionary<string, string> Socketed);

public sealed record P30JewelModifiers(int Physique = 0, int Dexterity = 0, int Spirit = 0, int Energy = 0,
    int IncreasedAttackDamageBasisPoints = 0, int IncreasedSpellDamageBasisPoints = 0,
    int IncreasedMaximumLifeBasisPoints = 0, int IncreasedMaximumManaBasisPoints = 0,
    int IncreasedMaximumShieldBasisPoints = 0, int IncreasedArmorBasisPoints = 0,
    int IncreasedEvasionBasisPoints = 0, int IncreasedAttackSpeedBasisPoints = 0,
    int FlatAccuracy = 0, int IncreasedCriticalChanceBasisPoints = 0,
    int IncreasedCriticalMultiplierBasisPoints = 0, int IncreasedSpiritBarrierBasisPoints = 0,
    int InstantLifeLeechBasisPoints = 0, int InstantManaLeechBasisPoints = 0,
    int InstantShieldLeechBasisPoints = 0, IReadOnlyList<P30Conversion>? Conversions = null,
    IReadOnlyDictionary<P30VirtueViceKind, int>? AdditionalVirtueViceMaximum = null,
    IReadOnlyList<P30VirtueViceKind>? Oaths = null,
    int MoreAttackDamageBasisPoints = 0, int MoreSpellDamageBasisPoints = 0,
    int MoreDamageOverTimeBasisPoints = 0, int MaximumElementalResistanceBasisPoints = 0,
    int MaximumVoidResistanceBasisPoints = 0, int IncreasedActionSpeedBasisPoints = 0,
    int ReservationEfficiencyBasisPoints = 0, int IncreasedPhysiqueBasisPoints = 0);

public sealed class P30JewelState
{
    public const int Capacity = 240;
    private readonly Dictionary<string, P30JewelInstance> _items = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _socketed = new(StringComparer.Ordinal);
    public IReadOnlyCollection<P30JewelInstance> Items => _items.Values;
    public IReadOnlyDictionary<string, string> Socketed => _socketed;

    public bool TryAdd(P30JewelInstance jewel)
    {
        ArgumentNullException.ThrowIfNull(jewel);
        return _items.Count < Capacity && _items.TryAdd(jewel.InstanceId, jewel);
    }

    public bool TrySocket(string socketId, string instanceId, int characterLevel, out string reason)
    {
        reason = string.Empty;
        P1.Progression.PassiveNodeDefinition? socket = P1.Progression.P1PassiveTree.Nodes
            .FirstOrDefault(node => node.StableId == socketId);
        if (socket?.Kind != P1.Progression.PassiveNodeKind.JewelSocket)
        { reason = "目标不是记忆棱孔。"; return false; }
        if (!_items.TryGetValue(instanceId, out P30JewelInstance? jewel))
        { reason = "珠宝不在珠宝仓。"; return false; }
        if (jewel.RequiredLevel > characterLevel)
        { reason = $"需求等级 {jewel.RequiredLevel}。"; return false; }
        if (jewel.Legendary is { } unique && _socketed.Any(pair => pair.Key != socketId &&
                _items.GetValueOrDefault(pair.Value)?.Legendary?.StableId == unique.StableId))
        { reason = "同名传奇珠宝全局只能镶嵌一枚。"; return false; }
        foreach (string previousSocket in _socketed.Where(pair => pair.Value == instanceId && pair.Key != socketId)
                     .Select(pair => pair.Key).ToArray())
            _socketed.Remove(previousSocket);
        _socketed[socketId] = instanceId;
        return true;
    }

    public bool TryUnsocket(string socketId) => _socketed.Remove(socketId);

    public void RestoreSnapshot(P30JewelStateSnapshot snapshot)
    {
        P30JewelState restored = Restore(snapshot);
        _items.Clear();
        _socketed.Clear();
        foreach ((string id, P30JewelInstance jewel) in restored._items) _items.Add(id, jewel);
        foreach ((string socket, string id) in restored._socketed) _socketed.Add(socket, id);
    }

    public bool TryReplace(P30JewelInstance jewel)
    {
        ArgumentNullException.ThrowIfNull(jewel);
        if (!_items.ContainsKey(jewel.InstanceId)) return false;
        _items[jewel.InstanceId] = jewel;
        return true;
    }

    public bool TryRemove(string instanceId)
    {
        if (!_items.Remove(instanceId)) return false;
        foreach (string socket in _socketed.Where(pair => pair.Value == instanceId).Select(pair => pair.Key).ToArray())
            _socketed.Remove(socket);
        return true;
    }
    public void UnsocketAll() => _socketed.Clear();
    public P30JewelInstance? At(string socketId) => _socketed.TryGetValue(socketId, out string? id)
        ? _items.GetValueOrDefault(id) : null;

    public P30JewelStateSnapshot Capture() => new(_items.Values.OrderBy(item => item.InstanceId).ToArray(),
        new Dictionary<string, string>(_socketed));

    public static P30JewelState Restore(P30JewelStateSnapshot? snapshot)
    {
        var state = new P30JewelState();
        if (snapshot is null) return state;
        foreach (P30JewelInstance jewel in snapshot.Items)
            if (!state.TryAdd(jewel)) throw new InvalidDataException("P30 jewel stash is invalid or exceeds 240 slots.");
        foreach ((string socket, string instance) in snapshot.Socketed)
        {
            P1.Progression.PassiveNodeDefinition? node = P1.Progression.P1PassiveTree.Nodes
                .FirstOrDefault(candidate => candidate.StableId == socket);
            if (!state._items.ContainsKey(instance) || node?.Kind != P1.Progression.PassiveNodeKind.JewelSocket)
                throw new InvalidDataException("P30 socket ownership is invalid.");
            state._socketed.Add(socket, instance);
        }
        if (state._socketed.Values.Count != state._socketed.Values.Distinct(StringComparer.Ordinal).Count())
            throw new InvalidDataException("One P30 jewel cannot occupy multiple sockets.");
        return state;
    }
}

public static class P30Jewels
{
    private static readonly int[] TierMinimumLevels = [85, 65, 45, 25, 1];
    private static readonly int[] TierScale = [10_000, 8_800, 7_500, 6_500, 5_500];

    public static IReadOnlyList<P30LegendaryJewelDefinition> Legendary { get; } =
    [
        L("crimson_memory", "赤铁记忆", P30JewelRadius.Large, "每50体魄：攻击伤害提高15%，护甲提高12%", "T6+地图Boss"),
        L("verdant_memory", "翠生记忆", P30JewelRadius.Large, "每50灵巧：攻击速度提高4%，闪避提高12%", "T6+地图Boss"),
        L("golden_memory", "金魂记忆", P30JewelRadius.Large, "每50精神：最大法力提高8%，恢复率提高4%", "T6+地图Boss"),
        L("azure_memory", "苍晶记忆", P30JewelRadius.Large, "每50能量：法术伤害提高15%，最大护盾提高12%", "T6+地图Boss"),
        L("titan_dream", "巨神旧梦", P30JewelRadius.Medium, "半径内普通属性节点改为等量体魄", "T11+地图Boss"),
        L("swift_dream", "疾影旧梦", P30JewelRadius.Medium, "半径内普通属性节点改为等量灵巧", "T11+地图Boss"),
        L("clear_dream", "澄心旧梦", P30JewelRadius.Medium, "半径内普通属性节点改为等量精神", "T11+地图Boss"),
        L("star_dream", "星核旧梦", P30JewelRadius.Medium, "半径内普通属性节点改为等量能量", "T11+地图Boss"),
        L("ember_core", "烬脉核心", P30JewelRadius.None, "40%物理转火；火焰伤害提高20%", "命能花园"),
        L("frost_core", "霜脉核心", P30JewelRadius.None, "40%物理转冰；冰霜伤害提高20%", "苍誓"),
        L("lightning_core", "雷脉核心", P30JewelRadius.None, "40%物理转电；闪电伤害提高20%", "赤誓"),
        L("void_core", "虚蚀核心", P30JewelRadius.None, "40%物理转虚空；虚空伤害提高20%", "深渊"),
        L("molten_circuit", "熔界回路", P30JewelRadius.None, "30%冰和30%电转火；火焰伤害提高20%", "赤誓高阶"),
        L("dark_circuit", "黯界回路", P30JewelRadius.None, "25%火冰电转虚空；虚空伤害提高20%", "深渊高阶"),
        O("mercy_oath", "仁恕之瞳", P30VirtueViceKind.Mercy, "深渊终战"), O("temperance_oath", "静律之芯", P30VirtueViceKind.Temperance, "命能花园终战"),
        O("humility_oath", "俯身之镜", P30VirtueViceKind.Humility, "地图Boss"), O("rage_oath", "裂怒之核", P30VirtueViceKind.Rage, "赤誓终战"),
        O("sloth_oath", "闲眠之眼", P30VirtueViceKind.Sloth, "亡旗战阵终战"), O("arrogance_oath", "孤峰之瞳", P30VirtueViceKind.Arrogance, "苍誓终战"),
        R("bloodbound_domain", "赤骸疆界", 180, 260,
            "半径内已配置小天赋各提供体魄 +20；已配置显著或专精各使体魄提高 5%"),
        R("pathless_chart", "无径星图", 140, 200,
            "半径内天赋可以无需连接直接配置；这些未连接天赋不能作为通往半径外天赋的起点"),
        R("bastion_abacus", "天垒算珠", 180, 240,
            "半径内已配置小天赋各使攻击伤害提高 4%；已配置显著或专精各使攻击速度提高 3%"),
        R("rampart_echo", "守垒余响", 180, 240,
            "半径内已配置小天赋各使护甲提高 4%；已配置显著或专精各使最大生命提高 2%"),
    ];

    public const int CitadelLegendaryDropChanceBasisPoints = 800;
    public static IReadOnlyList<string> CitadelLegendaryIds { get; } =
        ["bloodbound_domain", "pathless_chart", "bastion_abacus", "rampart_echo"];

    public static string BaseName(P30JewelBase value) => value switch
    { P30JewelBase.Crimson => "赤铁棱晶", P30JewelBase.Verdant => "翠影棱晶", P30JewelBase.Golden => "金魂棱晶", P30JewelBase.Azure => "苍晶棱晶", _ => "四相棱晶" };

    public static string RarityName(P30JewelRarity value) => value switch
    { P30JewelRarity.Normal => "普通", P30JewelRarity.Magic => "魔法", P30JewelRarity.Rare => "稀有", _ => "传奇" };

    public static string PositionName(P30JewelAffixPosition value) => value switch
    { P30JewelAffixPosition.Prefix => "前缀", P30JewelAffixPosition.Suffix => "后缀", _ => "腐化词缀" };

    public static string AffixText(P30JewelAffix affix)
    {
        string family = affix.StableId.Split('.').Last();
        string signed = affix.Value >= 0 ? "+" : string.Empty;
        return family switch
        {
            "life" => $"最大生命提高 {affix.Value / 100.0:0.#}%",
            "mana" => $"最大法力提高 {affix.Value / 100.0:0.#}%",
            "shield" => $"最大护盾提高 {affix.Value / 100.0:0.#}%",
            "barrier" => $"灵障提高 {affix.Value / 100.0:0.#}%",
            "armor" => $"护甲提高 {affix.Value / 100.0:0.#}%",
            "evasion" => $"闪避提高 {affix.Value / 100.0:0.#}%",
            "attack" => $"攻击伤害提高 {affix.Value / 100.0:0.#}%",
            "spell" => $"法术伤害提高 {affix.Value / 100.0:0.#}%",
            "physical" => $"物理伤害提高 {affix.Value / 100.0:0.#}%",
            "elemental" => $"元素伤害提高 {affix.Value / 100.0:0.#}%",
            "speed" => $"攻击速度提高 {affix.Value / 100.0:0.#}%",
            "accuracy" => $"命中值 {signed}{affix.Value}",
            "critical" => $"暴击率提高 {affix.Value / 100.0:0.#}%",
            "critical_multi" => $"暴击伤害倍率 {signed}{affix.Value / 100.0:0.#}%",
            "physique" => $"体魄 {signed}{affix.Value}",
            "dexterity" => $"灵巧 {signed}{affix.Value}",
            "spirit" => $"精神 {signed}{affix.Value}",
            "energy" => $"能量 {signed}{affix.Value}",
            "instant_life" => $"生命偷取的 {affix.Value / 100.0:0.#}% 立即恢复",
            "instant_mana" => $"法力偷取的 {affix.Value / 100.0:0.#}% 立即恢复",
            "instant_shield" => $"护盾偷取的 {affix.Value / 100.0:0.#}% 立即恢复",
            "attack_more" => "攻击伤害造成 6% 更多伤害",
            "spell_more" => "法术伤害造成 6% 更多伤害",
            "dot_more" => "持续伤害造成 8% 更多伤害",
            "elemental_max" => "最大火焰、冰霜、闪电抗性各 +1%",
            "void_max" => "最大虚空抗性 +2%",
            "action_speed" => "行动速度提高 3%",
            "instant_leech" => "生命、法力和护盾偷取中立即恢复的比例各 +15%",
            "reservation" => "所有技能保留效率提高 8%",
            _ when family.StartsWith("virtue_vice_max_", StringComparison.Ordinal) =>
                $"{VirtueViceName(family["virtue_vice_max_".Length..])}上限 +1",
            _ => string.IsNullOrWhiteSpace(affix.Effect) ? affix.DisplayName : affix.Effect,
        };
    }

    public static P30JewelInstance RollPrismatic(int itemLevel, ulong seed, string instanceId,
        P30JewelRarity rarity = P30JewelRarity.Rare)
    {
        if (rarity == P30JewelRarity.Legendary) throw new ArgumentOutOfRangeException(nameof(rarity));
        itemLevel = Math.Clamp(itemLevel, 1, 100);
        P30JewelBase jewelBase = (P30JewelBase)Weighted(seed % 420);
        int count = rarity switch { P30JewelRarity.Normal => 0, P30JewelRarity.Magic => 2, _ => 4 };
        string[] families = ["life", "mana", "shield", "barrier", "armor", "evasion", "attack", "spell",
            "physical", "elemental", "speed", "accuracy", "critical", "critical_multi", "physique", "dexterity", "spirit", "energy"];
        var affixes = new List<P30JewelAffix>();
        for (int i = 0; i < count; i++)
        {
            string family = families[(int)((seed >> (i * 7 + 9)) % (ulong)families.Length)];
            if (affixes.Any(a => a.StableId.EndsWith('.' + family, StringComparison.Ordinal))) { i--; seed += 7919; continue; }
            int tier = Tier(itemLevel, seed >> (i * 5));
            bool prefix = i < count / 2;
            int t1 = family switch { "life" => 800, "mana" or "shield" => 1_000, "barrier" => 1_200,
                "armor" or "evasion" or "physical" or "elemental" => 2_000, "attack" or "spell" => 1_800,
                "speed" => 600, "accuracy" => 450, "critical" => 3_500, "critical_multi" => 2_000, _ => 24 };
            int value = t1 * TierScale[tier - 1] / 10_000;
            affixes.Add(new($"p30.jewel.affix.{family}", family, prefix ? P30JewelAffixPosition.Prefix : P30JewelAffixPosition.Suffix,
                tier, value, $"{family} +{value}", [family]));
        }
        return new(instanceId, jewelBase, itemLevel, rarity, (int)((seed >> 48) % 41), affixes);
    }

    public static P30JewelInstance CreateLegendary(string stableId, int itemLevel, string instanceId, ulong seed = 0)
    {
        P30LegendaryJewelDefinition definition = Legendary.Single(item => item.StableId == $"p30.jewel.{stableId}");
        int radius = definition.MinimumRadius <= 0 ? 0 : definition.MinimumRadius +
            (int)(Mix(seed ^ StableHash(definition.StableId)) % (ulong)(definition.MaximumRadius - definition.MinimumRadius + 1));
        return new(instanceId, P30JewelBase.Quad, Math.Clamp(itemLevel, 1, 100), P30JewelRarity.Legendary, 0, [],
            definition, RolledRadius: radius);
    }

    public static P30JewelInstance? RollCitadelLegendary(int itemLevel, ulong seed, string instanceId)
    {
        ulong roll = Mix(seed ^ 0x30c17ade1UL);
        if (roll % 10_000 >= CitadelLegendaryDropChanceBasisPoints) return null;
        string stableId = CitadelLegendaryIds[(int)((roll >> 16) % (ulong)CitadelLegendaryIds.Count)];
        return CreateLegendary(stableId, itemLevel, instanceId, roll);
    }

    public static (P30JewelCorruptionResult Result, P30JewelInstance? Jewel) Corrupt(P30JewelInstance jewel, ulong seed)
    {
        if (jewel.Rarity != P30JewelRarity.Rare || jewel.Corrupted) throw new InvalidOperationException("Only uncorrupted rare jewels can be corrupted.");
        int roll = (int)(seed % 100);
        if (roll >= 90) return (P30JewelCorruptionResult.Destroyed, null);
        if (roll >= 70)
        {
            P30JewelAffix[] remaining = jewel.Affixes.OrderBy(a => a.StableId).Skip(1).ToArray();
            return (P30JewelCorruptionResult.Damaged, jewel with { Resonance = 0, Affixes = remaining, Corrupted = true, Locked = true });
        }
        if (roll >= 40) return (P30JewelCorruptionResult.Locked, jewel with { Corrupted = true, Locked = true });
        string[] implicits = ["attack_more", "spell_more", "dot_more", "elemental_max", "void_max", "action_speed", "instant_leech", "reservation", "virtue_vice_max"];
        string implicitId = implicits[(int)((seed >> 8) % (ulong)implicits.Length)];
        if (implicitId == "virtue_vice_max")
            implicitId += "_" + ((P30VirtueViceKind)((seed >> 16) % 6)).ToString().ToLowerInvariant();
        int value = implicitId switch
        {
            "attack_more" or "spell_more" => 600,
            "dot_more" or "reservation" => 800,
            "elemental_max" => 100,
            "void_max" => 200,
            "action_speed" => 300,
            "instant_leech" => 1_500,
            _ => 1,
        };
        var affix = new P30JewelAffix($"p30.jewel.corruption.{implicitId}", implicitId,
            P30JewelAffixPosition.CorruptedImplicit, 1, value, implicitId,
            ["corrupted"]);
        return (P30JewelCorruptionResult.PowerfulImplicit, jewel with { Affixes = [.. jewel.Affixes, affix], Corrupted = true, Locked = true });
    }

    public static (bool Succeeded, string Message, P30JewelInstance? Jewel, bool Destroyed) Craft(
        P30JewelInstance jewel, P30JewelCraftOperation operation, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(jewel);
        if (operation == P30JewelCraftOperation.RerollLegendaryRadius)
        {
            if (jewel.Legendary is not { MinimumRadius: > 0 } legendary)
                return (false, "该传奇珠宝没有可重投的半径。", jewel, false);
            int radius = legendary.MinimumRadius + (int)(Mix(seed ^ StableHash(jewel.InstanceId)) %
                (ulong)(legendary.MaximumRadius - legendary.MinimumRadius + 1));
            return (true, $"半径已重投为 {radius}。", jewel with { RolledRadius = radius }, false);
        }
        if (jewel.Legendary is not null) return (false, "传奇珠宝不能进行该项加工。", jewel, false);
        if (jewel.Corrupted || jewel.Locked) return (false, "已腐化或锁定的珠宝不能继续加工。", jewel, false);
        switch (operation)
        {
            case P30JewelCraftOperation.RerollRare:
                if (jewel.Rarity != P30JewelRarity.Rare)
                    return (false, "混沌金只能重铸稀有珠宝。", jewel, false);
                P30JewelInstance rerolled = RollPrismatic(jewel.ItemLevel, seed, jewel.InstanceId, jewel.Rarity) with
                {
                    Base = jewel.Base,
                    Resonance = jewel.Resonance,
                };
                return (true, "已重铸珠宝的四条显式词缀。", rerolled, false);
            case P30JewelCraftOperation.DissolveAffix:
                P30JewelAffix[] removable = jewel.Affixes
                    .Where(affix => affix.Position is P30JewelAffixPosition.Prefix or P30JewelAffixPosition.Suffix)
                    .ToArray();
                if (removable.Length == 0) return (false, "该珠宝没有可剥离的显式词缀。", jewel, false);
                P30JewelAffix removed = removable[(int)(seed % (ulong)removable.Length)];
                return (true, $"已剥离：{AffixText(removed)}。",
                    jewel with { Affixes = jewel.Affixes.Where(affix => !ReferenceEquals(affix, removed)).ToArray() }, false);
            case P30JewelCraftOperation.Corrupt:
                if (jewel.Rarity != P30JewelRarity.Rare)
                    return (false, "赤蚀铁只能腐化未腐化的稀有珠宝。", jewel, false);
                (P30JewelCorruptionResult result, P30JewelInstance? corrupted) = Corrupt(jewel, seed);
                return result switch
                {
                    P30JewelCorruptionResult.PowerfulImplicit => (true, "强力腐化：获得一条腐化隐式。", corrupted, false),
                    P30JewelCorruptionResult.Locked => (true, "腐化锁定：数值不变，无法继续加工。", corrupted, false),
                    P30JewelCorruptionResult.Damaged => (true, "负面腐化：共鸣度归零并失去一条词缀。", corrupted, false),
                    _ => (true, "腐化失控：珠宝已被摧毁。", null, true),
                };
            default:
                throw new ArgumentOutOfRangeException(nameof(operation));
        }
    }

    public static P4.MetalCurrencyKind CraftCurrency(P30JewelCraftOperation operation) => operation switch
    {
        P30JewelCraftOperation.RerollRare => P4.MetalCurrencyKind.ChaosGold,
        P30JewelCraftOperation.DissolveAffix => P4.MetalCurrencyKind.DissolutionSilver,
        P30JewelCraftOperation.Corrupt => P4.MetalCurrencyKind.CorruptionIron,
        P30JewelCraftOperation.RerollLegendaryRadius => P4.MetalCurrencyKind.DivineSilver,
        _ => throw new ArgumentOutOfRangeException(nameof(operation)),
    };

    public static int MapCompletionDropChanceBasisPoints(int tier) => Math.Clamp(tier, 1, 20) switch
    { <= 5 => 180, <= 10 => 220, <= 15 => 270, _ => 330 };
    public static int BossDropChanceBasisPoints(int tier) => 100 + 8 * Math.Clamp(tier, 1, 20);

    public static P30JewelModifiers CalculateModifiers(P30JewelState state,
        P1.Progression.PassiveTreeAllocation? allocation = null)
    {
        int physique = 0, dexterity = 0, spirit = 0, energy = 0, attack = 0, spell = 0, life = 0,
            mana = 0, shield = 0, armor = 0, evasion = 0, speed = 0, accuracy = 0, critical = 0,
            criticalMulti = 0, barrier = 0, instantLife = 0, instantMana = 0, instantShield = 0,
            moreAttack = 0, moreSpell = 0, moreDot = 0, maximumElemental = 0, maximumVoid = 0,
            actionSpeed = 0, reservation = 0, increasedPhysique = 0;
        var conversions = new List<P30Conversion>();
        var maxima = new Dictionary<P30VirtueViceKind, int>();
        var oaths = new HashSet<P30VirtueViceKind>();
        foreach ((string socketId, string _) in state.Socketed)
        {
            P30JewelInstance? jewel = state.At(socketId);
            if (jewel is null) continue;
            int implicitValue = jewel.Base == P30JewelBase.Quad ? 8 : 20;
            implicitValue = implicitValue * (100 + jewel.Resonance) / 100;
            switch (jewel.Base)
            {
                case P30JewelBase.Crimson: physique += implicitValue; break;
                case P30JewelBase.Verdant: dexterity += implicitValue; break;
                case P30JewelBase.Golden: spirit += implicitValue; break;
                case P30JewelBase.Azure: energy += implicitValue; break;
                default: physique += implicitValue; dexterity += implicitValue; spirit += implicitValue; energy += implicitValue; break;
            }
            foreach (P30JewelAffix affix in jewel.Affixes)
            {
                switch (affix.StableId.Split('.').Last())
                {
                    case "physique": physique += affix.Value; break; case "dexterity": dexterity += affix.Value; break;
                    case "spirit": spirit += affix.Value; break; case "energy": energy += affix.Value; break;
                    case "attack": attack += affix.Value; break; case "spell": spell += affix.Value; break;
                    case "life": life += affix.Value; break; case "mana": mana += affix.Value; break;
                    case "shield": shield += affix.Value; break; case "barrier": barrier += affix.Value; break;
                    case "armor": armor += affix.Value; break; case "evasion": evasion += affix.Value; break;
                    case "speed": speed += affix.Value; break; case "accuracy": accuracy += affix.Value; break;
                    case "critical": critical += affix.Value; break; case "critical_multi": criticalMulti += affix.Value; break;
                    case "instant_life": instantLife += affix.Value; break; case "instant_mana": instantMana += affix.Value; break;
                    case "instant_shield": instantShield += affix.Value; break;
                    case "attack_more": moreAttack += affix.Value; break; case "spell_more": moreSpell += affix.Value; break;
                    case "dot_more": moreDot += affix.Value; break; case "elemental_max": maximumElemental += affix.Value; break;
                    case "void_max": maximumVoid += affix.Value; break; case "action_speed": actionSpeed += affix.Value; break;
                    case "instant_leech": instantLife += affix.Value; instantMana += affix.Value; instantShield += affix.Value; break;
                    case "reservation": reservation += affix.Value; break;
                }
                const string maximumPrefix = "p30.jewel.corruption.virtue_vice_max_";
                if (affix.StableId.StartsWith(maximumPrefix, StringComparison.Ordinal) &&
                    Enum.TryParse(affix.StableId[maximumPrefix.Length..], true, out P30VirtueViceKind maximumKind))
                    maxima[maximumKind] = maxima.GetValueOrDefault(maximumKind) + affix.Value;
            }
            if (jewel.Legendary is { Oath: { } oath })
            { maxima[oath] = maxima.GetValueOrDefault(oath) + 1; oaths.Add(oath); }
            switch (jewel.Legendary?.StableId)
            {
                case "p30.jewel.ember_core": conversions.Add(new(P30DamageType.Physical, P30DamageType.Fire, 4_000, "jewel.ember")); break;
                case "p30.jewel.frost_core": conversions.Add(new(P30DamageType.Physical, P30DamageType.Cold, 4_000, "jewel.frost")); break;
                case "p30.jewel.lightning_core": conversions.Add(new(P30DamageType.Physical, P30DamageType.Lightning, 4_000, "jewel.lightning")); break;
                case "p30.jewel.void_core": conversions.Add(new(P30DamageType.Physical, P30DamageType.Void, 4_000, "jewel.void")); break;
                case "p30.jewel.molten_circuit":
                    conversions.Add(new(P30DamageType.Cold, P30DamageType.Fire, 3_000, "jewel.molten.cold"));
                    conversions.Add(new(P30DamageType.Lightning, P30DamageType.Fire, 3_000, "jewel.molten.lightning")); break;
                case "p30.jewel.dark_circuit":
                    conversions.Add(new(P30DamageType.Fire, P30DamageType.Void, 2_500, "jewel.dark.fire"));
                    conversions.Add(new(P30DamageType.Cold, P30DamageType.Void, 2_500, "jewel.dark.cold"));
                    conversions.Add(new(P30DamageType.Lightning, P30DamageType.Void, 2_500, "jewel.dark.lightning")); break;
                case "p30.jewel.bloodbound_domain" when allocation is not null:
                    foreach (P1.Progression.PassiveNodeDefinition node in AllocatedNodesInRadius(socketId, jewel, allocation.Allocated))
                    {
                        if (node.Kind == P1.Progression.PassiveNodeKind.Small) physique += 20;
                        else if (node.Kind is P1.Progression.PassiveNodeKind.Notable or P1.Progression.PassiveNodeKind.Mastery)
                            increasedPhysique += 500;
                    }
                    break;
                case "p30.jewel.bastion_abacus" when allocation is not null:
                    foreach (P1.Progression.PassiveNodeDefinition node in AllocatedNodesInRadius(socketId, jewel, allocation.Allocated))
                    {
                        if (node.Kind == P1.Progression.PassiveNodeKind.Small) attack += 400;
                        else if (node.Kind is P1.Progression.PassiveNodeKind.Notable or P1.Progression.PassiveNodeKind.Mastery)
                            speed += 300;
                    }
                    break;
                case "p30.jewel.rampart_echo" when allocation is not null:
                    foreach (P1.Progression.PassiveNodeDefinition node in AllocatedNodesInRadius(socketId, jewel, allocation.Allocated))
                    {
                        if (node.Kind == P1.Progression.PassiveNodeKind.Small) armor += 400;
                        else if (node.Kind is P1.Progression.PassiveNodeKind.Notable or P1.Progression.PassiveNodeKind.Mastery)
                            life += 200;
                    }
                    break;
            }
        }
        return new(physique, dexterity, spirit, energy, attack, spell, life, mana, shield, armor, evasion,
            speed, accuracy, critical, criticalMulti, barrier, instantLife, instantMana, instantShield,
            conversions, maxima, oaths.Order().ToArray(), moreAttack, moreSpell, moreDot, maximumElemental,
            maximumVoid, actionSpeed, reservation, increasedPhysique);
    }

    public static bool GrantsUnlinkedAllocation(P30JewelState? state, IReadOnlySet<string> allocated,
        string targetId, string? ignoredSocket = null)
    {
        if (state is null) return false;
        P1.Progression.PassiveNodeDefinition target = P1.Progression.P1PassiveTree.Get(targetId);
        if (target.Kind is P1.Progression.PassiveNodeKind.Start or P1.Progression.PassiveNodeKind.JewelSocket ||
            target.Start != P1.Progression.PassiveStartKind.None) return false;
        return state.Socketed.Keys.Where(socket => socket != ignoredSocket && allocated.Contains(socket))
            .Any(socket => state.At(socket)?.Legendary?.StableId == "p30.jewel.pathless_chart" &&
                IsInRadius(socket, targetId, state.At(socket)!.EffectiveRadius));
    }

    public static int RadiusValue(P30JewelRadius radius) => radius switch
    {
        P30JewelRadius.Small => 140,
        P30JewelRadius.Medium => 180,
        P30JewelRadius.Large => 220,
        _ => 0,
    };

    private static IEnumerable<P1.Progression.PassiveNodeDefinition> AllocatedNodesInRadius(
        string socketId, P30JewelInstance jewel, IReadOnlySet<string> allocated) => allocated
        .Where(id => id != socketId && IsInRadius(socketId, id, jewel.EffectiveRadius))
        .Select(P1.Progression.P1PassiveTree.Get);

    private static bool IsInRadius(string socketId, string nodeId, int radius)
    {
        P1.Progression.PassiveNodeDefinition socket = P1.Progression.P1PassiveTree.Get(socketId);
        P1.Progression.PassiveNodeDefinition node = P1.Progression.P1PassiveTree.Get(nodeId);
        double dx = node.X - socket.X;
        double dy = node.Y - socket.Y;
        return dx * dx + dy * dy <= radius * radius;
    }

    private static int Tier(int level, ulong seed)
    {
        int best = Array.FindIndex(TierMinimumLevels, minimum => level >= minimum) + 1;
        int worst = 5;
        return Math.Clamp(best + (int)(seed % (ulong)(worst - best + 1)), best, worst);
    }
    private static int Weighted(ulong roll) => roll < 100 ? 0 : roll < 200 ? 1 : roll < 300 ? 2 : roll < 400 ? 3 : 4;
    private static ulong Mix(ulong value)
    {
        value += 0x9e3779b97f4a7c15UL;
        value = (value ^ (value >> 30)) * 0xbf58476d1ce4e5b9UL;
        value = (value ^ (value >> 27)) * 0x94d049bb133111ebUL;
        return value ^ (value >> 31);
    }
    private static ulong StableHash(string value) => value.Aggregate(1469598103934665603UL,
        (hash, character) => (hash ^ character) * 1099511628211UL);
    private static string VirtueViceName(string value) => value switch
    {
        "mercy" => "慈悲", "temperance" => "节制", "humility" => "谦逊",
        "rage" => "暴怒", "sloth" => "懒惰", "arrogance" => "傲慢", _ => value,
    };
    private static P30LegendaryJewelDefinition L(string id, string name, P30JewelRadius radius, string effect, string source) =>
        new($"p30.jewel.{id}", name, radius, effect, source);
    private static P30LegendaryJewelDefinition O(string id, string name, P30VirtueViceKind kind, string source) =>
        new($"p30.jewel.{id}", name, P30JewelRadius.None, $"{kind}上限+1并提供专属获取规则", source, kind);
    private static P30LegendaryJewelDefinition R(string id, string name, int minimumRadius, int maximumRadius, string effect) =>
        new($"p30.jewel.{id}", name, P30JewelRadius.None, effect, "灰烬天垒", MinimumRadius: minimumRadius,
            MaximumRadius: maximumRadius);
}
