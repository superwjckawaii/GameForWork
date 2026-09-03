using GameForWork.Core.Equipment;

namespace GameForWork.Core.P24;

public sealed record P24GuideEntry(string StableId, string Title, string Summary, IReadOnlyList<string> Rules);

public static class P24GuideCatalog
{
    private static readonly HashSet<string> AddedP24Families =
    [
        "equipment.affix.minion.maximum",
        "equipment.affix.construct.maximum",
        "equipment.affix.phantom.maximum",
    ];

    public static IReadOnlyList<IReadOnlyList<EquipmentAffixEntry>> SpecialAffixFamilies { get; } =
        EquipmentCatalog.Snapshot.AffixFamilies.Where(family =>
            family[0].LegacyIds.Any(id => id.StartsWith("p24.affix.", StringComparison.Ordinal)) ||
            AddedP24Families.Contains(family[0].Id)).ToArray();

    public static IReadOnlyList<P24GuideEntry> Entries { get; } =
    [
        new("p24.guide.skill_stones", "技能石与连接", "所有技能石全职业共享；装备只决定连接孔，不绑定职业。",
            ["主动石占用连接组首孔。", "辅助石必须满足行为标签。", "同一颗技能石实例不能同时装入两组连接。"]),
        new("p24.guide.ranged", "远程与危险规避", "远程攻击保持8米、施法保持7米、召唤体系保持9米。",
            ["近战以1.5米为接敌距离。", "移动攻击可以在施放中调整距离。", "构装优先稀有怪和Boss。"]),
        new("p24.guide.units", "召唤、灵兽、构装与幻身", "四类盟友使用独立上限和死亡规则。",
            ["召唤物基础6、硬上限16。", "灵兽唯一且没有独立装备栏。", "构装基础3、硬上限8。", "幻身不视为召唤物、硬上限6。"]),
        new("p24.guide.traps", "陷阱", "陷阱持续8秒，敌人进入2米时触发。",
            ["基础上限3、硬上限8。", "超过上限时替换最早布置的陷阱。", "当前版本不包含地雷和图腾。"]),
        new("p24.guide.party", "祝福与参战队伍", "祝福升华允许主角携带一名佣兵参战。",
            ["佣兵不占召唤或伙伴上限。", "只有主角与附属佣兵均倒下才判定失败。", "光环只作用于实际参战单位。"]),
        new("p24.guide.modifiers", "提高、更多与总降", "提高位于同一加法乘区；每个更多独立相乘；总降按乘法降低。",
            ["多个提高先相加。", "多个更多依次相乘。", "总降不会与提高相减。"]),
        new("p24.guide.item_families", "装备与词缀库", "正式目录包含244个底材和212个自然词缀族，其中49个来自特殊装备体系；法武共鸣不属于当前词缀库。",
            SpecialAffixFamilies.Select(family => family[0])
                .Select(affix => $"{affix.DisplayName}：{affix.RawText}").ToArray()),
    ];
}
