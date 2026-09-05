using GameForWork.Core.Campaign.World;

namespace GameForWork.Core.Maps;

/// <summary>Captured before route preview and retained through rescue, save and rewards.</summary>
public sealed record MapEquipmentSnapshot(bool RedVow = false, bool BlueVow = false)
{
    public static MapEquipmentSnapshot From(TeamBuild build) => new(
        build.CombatEquipment?.Has("赤誓之环") == true, build.CombatEquipment?.Has("苍誓之环") == true);
    public int RewardMultiplier(GameForWork.Core.Encounters.Mechanic mechanic) => mechanic switch
    {
        GameForWork.Core.Encounters.Mechanic.Red when RedVow => 12_500,
        GameForWork.Core.Encounters.Mechanic.Blue when BlueVow => 20_000,
        _ => 10_000,
    };
}
