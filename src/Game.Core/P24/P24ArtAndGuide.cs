using GameForWork.Core.P18;
using GameForWork.Core.P23;

namespace GameForWork.Core.P24;

public sealed record P24CharacterArt(P23BaseClass Class, int AtlasColumn, string Palette, string RuntimePath);
public sealed record P24UnitArt(string StableId, P24CombatUnitKind Kind, int AtlasColumn, string RuntimePath);
public sealed record P24AscendancyArt(P18Ascendancy Ascendancy, int AtlasIndex, string RuntimePath);
public sealed record P24SkillVfxArt(string SkillId, int AtlasIndex, string RuntimePath);

public static class P24ArtContract
{
    public const int DirectionCount = 4;
    public const int PlayableCharacterCount = 5;
    public const int AlliedUnitCount = 5;
    public const int AscendancySetCount = 15;
    public const string CharacterAtlasPath = "res://assets/p24/characters/p24-characters-and-units.png";
    public const string CharacterDirectionAtlasPath = "res://assets/p24/characters/p24-character-directions.png";
    public const string AscendancyAtlasPath = "res://assets/p24/trees/p24-ascendancy-emblems.png";
    public const string SkillVfxAtlasPath = "res://assets/p24/vfx/p24-class-vfx.png";

    public static IReadOnlyList<P24CharacterArt> Characters { get; } =
    [
        new(P23BaseClass.Rogue, 0, "苔绿/黄铜", CharacterDirectionAtlasPath),
        new(P23BaseClass.Psion, 1, "骨白/灵紫", CharacterDirectionAtlasPath),
        new(P23BaseClass.Occultist, 2, "秘蓝/棱彩", CharacterDirectionAtlasPath),
        new(P23BaseClass.Monk, 3, "象牙/青绿", CharacterDirectionAtlasPath),
        new(P23BaseClass.Hermit, 4, "锈红/符青", CharacterDirectionAtlasPath),
    ];

    public static IReadOnlyList<P24UnitArt> Units { get; } =
    [
        new("p24.unit.boneguard", P24CombatUnitKind.Minion, 5, CharacterAtlasPath),
        new("p24.unit.soulbow", P24CombatUnitKind.Minion, 6, CharacterAtlasPath),
        new("p24.unit.spirit_beast", P24CombatUnitKind.Companion, 7, CharacterAtlasPath),
        new("p24.unit.martial_phantom", P24CombatUnitKind.Phantom, 8, CharacterAtlasPath),
        new("p24.unit.rune_turret", P24CombatUnitKind.Construct, 9, CharacterAtlasPath),
    ];

    public static IReadOnlyList<P24AscendancyArt> Ascendancies { get; } =
    new[]
    {
        P18Ascendancy.Marksman, P18Ascendancy.Shadowblade, P18Ascendancy.Venomist,
        P18Ascendancy.SoulShepherd, P18Ascendancy.SpiritCantor, P18Ascendancy.Hexbinder,
        P18Ascendancy.Elementalist, P18Ascendancy.VoidScholar, P18Ascendancy.AegisMage,
        P18Ascendancy.MartialMonk, P18Ascendancy.BeastKeeper, P18Ascendancy.PhantomMaster,
        P18Ascendancy.Runecarver, P18Ascendancy.Spellarmor, P18Ascendancy.IdolForger,
    }.Select((ascendancy, index) => new P24AscendancyArt(ascendancy, index, AscendancyAtlasPath)).ToArray();

    public static IReadOnlyList<P24SkillVfxArt> SkillVfx { get; } =
    [
        V("cloudpiercer_arrow", 0), V("summon_boneguard", 1), V("molten_orb", 2), V("chain_fists", 3), V("runeblade_slash", 4),
        V("returning_arrow", 5), V("summon_soulbow", 6), V("ice_lance", 7), V("skyquake_palm", 8), V("sixfold_burst", 9),
        V("venom_blades", 10), V("courage_hymn", 11), V("thunderstorm", 12), V("yin_yang_stance", 13), V("shieldbreak_counter", 14),
        V("corrosive_trap", 15), V("doom_brand", 16), V("forbidden_collapse", 17), V("hundred_shadows", 18), V("selfdestruct_rebuild", 19),
    ];

    private static P24SkillVfxArt V(string suffix, int index) => new($"p24.skill.{suffix}", index, SkillVfxAtlasPath);
}

public sealed record P24GuideEntry(string StableId, string Title, string Summary, IReadOnlyList<string> Rules);

public static class P24GuideCatalog
{
    public static IReadOnlyList<P24GuideEntry> Entries { get; } =
    [
        new("p24.guide.skill_stones", "P24 技能石与连接", "所有技能石全职业共享；装备只决定连接孔，不绑定职业。",
            ["主动石占用连接组首孔。", "辅助石必须满足行为标签。", "同一颗技能石实例不能同时装入两组连接。"]),
        new("p24.guide.ranged", "远程与危险规避", "远程攻击保持8米、施法保持7米、召唤体系保持9米。",
            ["近战以1.5米为接敌距离。", "移动攻击可以在施放中调整距离。", "构装优先稀有怪和Boss。"]),
        new("p24.guide.units", "召唤、灵兽、构装与幻身", "四类盟友使用独立上限和死亡规则。",
            ["召唤物基础6、硬上限16。", "灵兽唯一且没有独立装备栏。", "构装基础3、硬上限8。", "幻身不视为召唤物、硬上限6。"]),
        new("p24.guide.traps", "陷阱", "陷阱持续8秒，敌人进入2米时触发。",
            ["基础上限3、硬上限8。", "超过上限时替换最早布置的陷阱。", "地雷和图腾不进入v0.3。"]),
        new("p24.guide.party", "祝福与参战队伍", "祝福升华允许主角携带一名佣兵参战。",
            ["佣兵不占召唤或伙伴上限。", "只有主角与附属佣兵均倒下才判定失败。", "光环只作用于实际参战单位。"]),
        new("p24.guide.modifiers", "提高、更多与总降", "提高位于同一加法乘区；每个更多独立相乘；总降按乘法降低。",
            ["多个提高先相加。", "多个更多依次相乘。", "总降不会与提高相减。"]),
        new("p24.guide.item_families", "P24 装备与词缀库", "新增50个底材、44个保留机制族及5个P30补充机制族；法武共鸣已删除。",
            P24ItemCatalog.Families.Select(family => $"{family.DisplayName}：{family.RuleText}").ToArray()),
    ];
}
