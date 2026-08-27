using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.Items;
using GameForWork.Core.Simulation;

namespace GameForWork.Core.P1.World;

public enum MercenaryArchetype
{
    Cantor,
}

public sealed record MercenaryAutonomousConfiguration(
    IReadOnlyList<SkillConfiguration> Skills,
    string AiSummary,
    string GrowthSummary);

public sealed class P1MercenaryProfile
{
    public P1MercenaryProfile(
        string stableId,
        string name,
        MercenaryArchetype archetype,
        CharacterAttributes finalAttributes,
        IReadOnlyList<string> traits,
        MercenaryAutonomousConfiguration autonomousConfiguration,
        EquipmentLoadout equipment)
    {
        StableId = stableId;
        Name = name;
        Archetype = archetype;
        FinalAttributes = finalAttributes;
        Traits = traits;
        AutonomousConfiguration = autonomousConfiguration;
        Equipment = equipment;
    }

    public string StableId { get; }
    public string Name { get; }
    public MercenaryArchetype Archetype { get; }
    public CharacterAttributes FinalAttributes { get; }
    public IReadOnlyList<string> Traits { get; }
    public MercenaryAutonomousConfiguration AutonomousConfiguration { get; }
    public EquipmentLoadout Equipment { get; }

    public P1TeamBuild CreateTeamBuild(int level = 1)
    {
        EquipmentSummary equipment = Equipment.CalculateSummary();
        return new P1TeamBuild(
            new CharacterSheet(level, FinalAttributes, equipment.Defense),
            equipment.Weapon ?? throw new InvalidOperationException("The mercenary needs a weapon."),
            AutonomousConfiguration.Skills.First(skill => skill.SkillId == P1SkillIds.HeavyStrike),
            FlatAccuracy: 70,
            UseWarCry: AutonomousConfiguration.Skills.Any(skill => skill.SkillId == P1SkillIds.WarCry),
            AiSummary: AutonomousConfiguration.AiSummary);
    }
}

public static class P1MercenaryFactory
{
    private static readonly string[] Names = ["伊莱娅", "赫恩", "米蕾", "奥兰", "塞芙", "塔维"];
    private static readonly string[] PositiveTraits = ["沉着", "守序", "嘹亮", "坚韧"];
    private static readonly string[] NegativeTraits = ["寡言", "旧伤", "固执", "畏暗"];

    public static P1MercenaryProfile GenerateCantor(ulong seed)
    {
        var random = new Pcg32(seed);
        string name = Names[Next(random, Names.Length)];
        string positive = PositiveTraits[Next(random, PositiveTraits.Length)];
        string negative = NegativeTraits[Next(random, NegativeTraits.Length)];
        var equipment = new EquipmentLoadout();
        bool equipped = equipment.TryEquip(
            EquipmentSlot.MainHand,
            ItemGenerator.Generate(
                "core.base.rusted_greatsword",
                1,
                ItemRarity.Basic,
                seed,
                $"mercenary-{seed:x16}-weapon"));
        if (!equipped)
        {
            throw new InvalidOperationException("The generated mercenary weapon could not be equipped.");
        }

        var configuration = new MercenaryAutonomousConfiguration(
            [
                new SkillConfiguration(P1SkillIds.HeavyStrike, SkillSupport.IncreasedArea),
                new SkillConfiguration(P1SkillIds.WarCry, SkillSupport.None),
            ],
            "敌群接近后先战吼；保持生命安全时使用扩大范围重击；不读取隐藏掉落结果。",
            "颂仪者倾向：自主提升战吼覆盖与群体清理，玩家不可修改技能、辅助或 AI。");
        return new P1MercenaryProfile(
            $"core.mercenary.generated.{seed:x16}",
            name,
            MercenaryArchetype.Cantor,
            new CharacterAttributes(16, 12, 18, 12),
            [positive, negative],
            configuration,
            equipment);
    }

    private static int Next(Pcg32 random, int exclusiveMaximum) =>
        (int)(random.NextUInt() % (uint)exclusiveMaximum);
}

public sealed class TeleporterState
{
    private static readonly int[] MercenaryCapacityByLevel = [1, 2, 3, 4];

    public int Level { get; private set; } = 1;
    public int MercenaryTeamCapacity => MercenaryCapacityByLevel[Level - 1];

    public bool TrySetLevel(int level)
    {
        if (level is < 1 or > 4)
        {
            return false;
        }

        Level = level;
        return true;
    }
}
