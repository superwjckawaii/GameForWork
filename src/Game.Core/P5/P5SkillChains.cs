using GameForWork.Core.P1.Items;
using GameForWork.Core.P2;

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
    bool ToolOnly);

public static class P5SkillChainRules
{
    public static IReadOnlyList<P5SkillChainDefinition> Build(EquipmentLoadout loadout)
    {
        ArgumentNullException.ThrowIfNull(loadout);
        int links = loadout.CalculateSummary().SupportLinkCapacity;
        var chains = new List<P5SkillChainDefinition>();
        if (loadout.Items.ContainsKey(EquipmentSlot.MainHand))
        {
            chains.Add(new P5SkillChainDefinition(P5SkillChainIds.WeaponPrimary, "武器 · 主攻击链",
                EquipmentSlot.MainHand, 0, false));
            chains.Add(new P5SkillChainDefinition(P5SkillChainIds.WeaponSecondary, "武器 · 副攻击链",
                EquipmentSlot.MainHand, 0, false));
        }

        if (loadout.Items.ContainsKey(EquipmentSlot.Chest))
        {
            chains.Add(new P5SkillChainDefinition(P5SkillChainIds.Chest, "胸甲 · 第三攻击链",
                EquipmentSlot.Chest, 0, false));
        }

        if (loadout.Items.ContainsKey(EquipmentSlot.Helmet))
        {
            chains.Add(new P5SkillChainDefinition(P5SkillChainIds.HelmetTool, "头盔 · 工具链",
                EquipmentSlot.Helmet, 0, true));
        }

        if (chains.Count == 0)
        {
            return chains;
        }

        int[] capacities = new int[chains.Count];
        for (int index = 0; index < capacities.Length && links > 0; index++, links--)
        {
            capacities[index]++;
        }

        int cursor = 0;
        while (links-- > 0)
        {
            int index = cursor++ % chains.Count;
            capacities[index] = Math.Min(5, capacities[index] + 1);
        }

        return chains.Select((chain, index) => chain with { SupportCapacity = capacities[index] }).ToArray();
    }

    public static bool Accepts(P5SkillChainDefinition chain, SkillStoneDefinition skill) =>
        skill.Kind == SkillStoneKind.Active &&
        (chain.ToolOnly == (skill.StableId == "core.skill_stone.war_cry"));
}
