using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P1.Progression;
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
        SkillConfiguration heavyStrike = AutonomousConfiguration.Skills.First(
            skill => skill.SkillId == P1SkillIds.HeavyStrike);
        AssembledCharacterBuild build = CharacterBuildAssembler.Assemble(
            level,
            FinalAttributes,
            Equipment,
            new PassiveTreeAllocation(memoryAshes: 0),
            heavyStrike);
        return new P1TeamBuild(
            build.Sheet,
            build.Equipment.Weapon ?? throw new InvalidOperationException("The mercenary needs a weapon."),
            heavyStrike,
            FlatAccuracy: checked(70 + build.FlatAccuracy),
            IncreasedDamageBasisPoints: build.IncreasedAttackDamageBasisPoints,
            IncreasedCriticalChanceBasisPoints: build.IncreasedCriticalChanceBasisPoints,
            IncreasedBleedChanceBasisPoints: build.IncreasedBleedChanceBasisPoints,
            UseWarCry: AutonomousConfiguration.Skills.Any(skill => skill.SkillId == P1SkillIds.WarCry),
            AiSummary: AutonomousConfiguration.AiSummary,
            LifeFlask: new LifeFlaskDefinition(BaseRecovery: 40, MaximumCharges: 30, ChargesPerUse: 10),
            IncreasedLifeFlaskEffectBasisPoints: build.Equipment.Modifiers.IncreasedLifeFlaskEffectBasisPoints,
            LifeFlaskUseThresholdBasisPoints: 5_000,
            AddedPhysicalDamage: build.AddedPhysicalDamage,
            HeavyStrikeProfile: build.HeavyStrike,
            WeaponLegendaryRule: build.Equipment.WeaponLegendaryRule,
            HasShield: build.Equipment.HasShield,
            BlockChanceBasisPoints: build.Equipment.HasShield ? 2_000 : 0);
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
        EquipStarter(equipment, EquipmentSlot.MainHand, "core.base.rusted_greatsword", seed, "weapon");
        EquipStarter(equipment, EquipmentSlot.Chest, "core.base.crude_chainmail", seed + 1, "chest");
        EquipStarter(equipment, EquipmentSlot.Helmet, "core.base.iron_helmet", seed + 2, "helmet");
        EquipStarter(equipment, EquipmentSlot.RingLeft, "core.base.life_ring", seed + 3, "ring");
        EquipStarter(equipment, EquipmentSlot.Flask1, "core.base.life_flask", seed + 4, "flask");

        var configuration = new MercenaryAutonomousConfiguration(
            [
                new SkillConfiguration(P1SkillIds.HeavyStrike, SkillSupport.Bleed),
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

    private static void EquipStarter(
        EquipmentLoadout equipment,
        EquipmentSlot slot,
        string baseId,
        ulong seed,
        string suffix)
    {
        ItemInstance item = ItemGenerator.Generate(
            baseId,
            1,
            ItemRarity.Basic,
            seed,
            $"mercenary-{seed:x16}-{suffix}");
        if (!equipment.TryEquip(slot, item))
        {
            throw new InvalidOperationException($"The generated mercenary item {baseId} could not be equipped.");
        }
    }
}

public sealed class TeleporterState
{
    private static readonly int[] MercenaryCapacityByLevel = [3, 4, 5, 6];

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
