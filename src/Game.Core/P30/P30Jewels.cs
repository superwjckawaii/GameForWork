namespace GameForWork.Core.P30;

public enum P30JewelBase { Crimson, Verdant, Golden, Azure, Quad }
public enum P30JewelRarity { Normal, Magic, Rare, Legendary }
public enum P30JewelRadius { None, Small, Medium, Large }
public enum P30JewelAffixPosition { Prefix, Suffix, CorruptedImplicit }
public enum P30JewelCorruptionResult { PowerfulImplicit, Locked, Damaged, Destroyed }

public sealed record P30JewelAffix(string StableId, string DisplayName, P30JewelAffixPosition Position,
    int Tier, int Value, string Effect, IReadOnlySet<string> Tags);

public sealed record P30LegendaryJewelDefinition(string StableId, string DisplayName, P30JewelRadius Radius,
    string Effect, string Source, P30VirtueViceKind? Oath = null);

public sealed record P30JewelInstance(string InstanceId, P30JewelBase Base, int ItemLevel, P30JewelRarity Rarity,
    int Resonance, IReadOnlyList<P30JewelAffix> Affixes, P30LegendaryJewelDefinition? Legendary = null,
    bool Corrupted = false, bool Locked = false)
{
    public int RequiredLevel => Math.Clamp(ItemLevel, 1, 100);
    public string DisplayName => Legendary?.DisplayName ?? P30Jewels.BaseName(Base);
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
    IReadOnlyList<P30VirtueViceKind>? Oaths = null);

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
        if (_socketed.TryGetValue(socketId, out string? replaced)) _items[replaced] = _items[replaced];
        _socketed[socketId] = instanceId;
        return true;
    }

    public bool TryUnsocket(string socketId) => _socketed.Remove(socketId);
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
    ];

    public static string BaseName(P30JewelBase value) => value switch
    { P30JewelBase.Crimson => "赤铁棱晶", P30JewelBase.Verdant => "翠影棱晶", P30JewelBase.Golden => "金魂棱晶", P30JewelBase.Azure => "苍晶棱晶", _ => "四相棱晶" };

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
                tier, value, $"{family} +{value}", new HashSet<string>(StringComparer.Ordinal) { family }));
        }
        return new(instanceId, jewelBase, itemLevel, rarity, (int)((seed >> 48) % 41), affixes);
    }

    public static P30JewelInstance CreateLegendary(string stableId, int itemLevel, string instanceId) =>
        new(instanceId, P30JewelBase.Quad, Math.Clamp(itemLevel, 1, 100), P30JewelRarity.Legendary, 0, [],
            Legendary.Single(item => item.StableId == $"p30.jewel.{stableId}"));

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
        var affix = new P30JewelAffix($"p30.jewel.corruption.{implicitId}", implicitId,
            P30JewelAffixPosition.CorruptedImplicit, 1, 1, implicitId,
            new HashSet<string>(StringComparer.Ordinal) { "corrupted" });
        return (P30JewelCorruptionResult.PowerfulImplicit, jewel with { Affixes = [.. jewel.Affixes, affix], Corrupted = true, Locked = true });
    }

    public static int MapCompletionDropChanceBasisPoints(int tier) => Math.Clamp(tier, 1, 20) switch
    { <= 5 => 180, <= 10 => 220, <= 15 => 270, _ => 330 };
    public static int BossDropChanceBasisPoints(int tier) => 100 + 8 * Math.Clamp(tier, 1, 20);

    public static P30JewelModifiers CalculateModifiers(P30JewelState state)
    {
        int physique = 0, dexterity = 0, spirit = 0, energy = 0, attack = 0, spell = 0, life = 0,
            mana = 0, shield = 0, armor = 0, evasion = 0, speed = 0, accuracy = 0, critical = 0,
            criticalMulti = 0, barrier = 0, instantLife = 0, instantMana = 0, instantShield = 0;
        var conversions = new List<P30Conversion>();
        var maxima = new Dictionary<P30VirtueViceKind, int>();
        var oaths = new HashSet<P30VirtueViceKind>();
        foreach (P30JewelInstance jewel in state.Socketed.Keys.Select(state.At).OfType<P30JewelInstance>())
        {
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
                }
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
            }
        }
        return new(physique, dexterity, spirit, energy, attack, spell, life, mana, shield, armor, evasion,
            speed, accuracy, critical, criticalMulti, barrier, instantLife, instantMana, instantShield,
            conversions, maxima, oaths.Order().ToArray());
    }

    private static int Tier(int level, ulong seed)
    {
        int best = Array.FindIndex(TierMinimumLevels, minimum => level >= minimum) + 1;
        int worst = 5;
        return Math.Clamp(best + (int)(seed % (ulong)(worst - best + 1)), best, worst);
    }
    private static int Weighted(ulong roll) => roll < 100 ? 0 : roll < 200 ? 1 : roll < 300 ? 2 : roll < 400 ? 3 : 4;
    private static P30LegendaryJewelDefinition L(string id, string name, P30JewelRadius radius, string effect, string source) =>
        new($"p30.jewel.{id}", name, radius, effect, source);
    private static P30LegendaryJewelDefinition O(string id, string name, P30VirtueViceKind kind, string source) =>
        new($"p30.jewel.{id}", name, P30JewelRadius.None, $"{kind}上限+1并提供专属获取规则", source, kind);
}
