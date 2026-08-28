using GameForWork.Core.P1.Items;
using GameForWork.Core.P2;
using GameForWork.Core.P6;

namespace GameForWork.Core.P5;

public static class P5SkillChainIds
{
    public const string WeaponPrimary = "p5.chain.weapon.primary";
    public const string WeaponSecondary = "p5.chain.weapon.secondary";
    public const string Chest = "p5.chain.chest";
    public const string HelmetTool = "p5.chain.helmet.tool";
}

public sealed record P5SkillChainDefinition(
    string StableId,
    string DisplayName,
    EquipmentSlot SourceSlot,
    int SupportCapacity,
    bool ToolOnly)
{
    public int TotalSockets => SupportCapacity + 1;
}

public static class P5SkillChainRules
{
    public static IReadOnlyList<P5SkillChainDefinition> Build(EquipmentLoadout loadout)
    {
        ArgumentNullException.ThrowIfNull(loadout);
        var chains = new List<P5SkillChainDefinition>();
        EquipmentSlot[] order =
        {
            EquipmentSlot.MainHand, EquipmentSlot.Chest, EquipmentSlot.Helmet,
            EquipmentSlot.Gloves, EquipmentSlot.Boots,
        };
        foreach (EquipmentSlot slot in order)
        {
            if (!loadout.Items.TryGetValue(slot, out ItemInstance? item) || item.LinkedSocketCount <= 0)
            {
                continue;
            }
            string slotName = slot switch
            {
                EquipmentSlot.MainHand => "武器",
                EquipmentSlot.Chest => "胸甲",
                EquipmentSlot.Helmet => "头盔",
                EquipmentSlot.Gloves => "手套",
                EquipmentSlot.Boots => "鞋",
                _ => slot.ToString(),
            };
            chains.Add(new P5SkillChainDefinition(
                P6SocketGroupIds.For(slot), $"{slotName} · {ChineseLink(item.LinkedSocketCount)}",
                slot, item.LinkedSocketCount - 1, false));
        }
        return chains;
    }

    public static bool Accepts(P5SkillChainDefinition chain, SkillStoneDefinition skill) =>
        skill.Kind == SkillStoneKind.Active &&
        true;

    private static string ChineseLink(int count) => count switch
    {
        2 => "二连", 3 => "三连", 4 => "四连", 5 => "五连", 6 => "六连", _ => $"{count} 连",
    };
}
