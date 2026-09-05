using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Management;
using GameForWork.Core.Skills;

namespace GameForWork.Core.Expeditions;

public static class SkillChainIds
{
    public const string WeaponPrimary = "expeditions.chain.weapon.primary";
    public const string WeaponSecondary = "expeditions.chain.weapon.secondary";
    public const string Chest = "expeditions.chain.chest";
    public const string HelmetTool = "expeditions.chain.helmet.tool";
}

public sealed record SkillChainDefinition(
    string StableId,
    string DisplayName,
    EquipmentSlot SourceSlot,
    int SupportCapacity,
    bool ToolOnly)
{
    public int TotalSockets => SupportCapacity + 1;
}

public static class SkillChainRules
{
    public static IReadOnlyList<SkillChainDefinition> Build(EquipmentLoadout loadout)
    {
        ArgumentNullException.ThrowIfNull(loadout);
        var chains = new List<SkillChainDefinition>();
        EquipmentSlot[] order =
        {
            EquipmentSlot.MainHand, EquipmentSlot.OffHand, EquipmentSlot.Chest, EquipmentSlot.Helmet,
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
                EquipmentSlot.OffHand => "副手",
                EquipmentSlot.Chest => "胸甲",
                EquipmentSlot.Helmet => "头盔",
                EquipmentSlot.Gloves => "手套",
                EquipmentSlot.Boots => "鞋",
                _ => slot.ToString(),
            };
            chains.Add(new SkillChainDefinition(
                SocketGroupIds.For(slot), $"{slotName} · {ChineseLink(item.LinkedSocketCount)}",
                slot, item.LinkedSocketCount - 1, false));
        }
        return chains;
    }

    public static bool Accepts(SkillChainDefinition chain, SkillStoneDefinition skill) =>
        skill.Kind == SkillStoneKind.Active &&
        true;

    private static string ChineseLink(int count) => count switch
    {
        2 => "二连", 3 => "三连", 4 => "四连", 5 => "五连", 6 => "六连", _ => $"{count} 连",
    };
}
