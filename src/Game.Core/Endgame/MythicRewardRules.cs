using GameForWork.Core.Campaign.World;
using GameForWork.Core.Encounters;
using GameForWork.Core.Maps;

namespace GameForWork.Core.Endgame;

public static class MythicRewardRules
{
    public const string HeartOfAsh = "core.mythic.heart_of_ash";
    public const string WorldEater = "equipment.legendary.52.44a586da1f";
    public const string FinalMagic = "equipment.legendary.53.26839c4b94";
    public const string PrimalCrown = "equipment.legendary.54.915c91c995";
    public const string EternalCore = "equipment.legendary.55.54b1e3f6f0";

    public static IReadOnlyList<string> ForCompletion(MapItem map, MapRoute route)
    {
        if (EndgameState.IsCitadel(map)) return [HeartOfAsh];
        GameplayPolicy policy = Gameplay.Policy(map);
        var rewards = new List<string>(2);
        if (route == MapRoute.Abyss && policy.AbyssIntensity == 5 && policy.AbyssFinalGuardian)
            rewards.Add(WorldEater);
        if (map.Altar == MapAltar.BlueOath && policy.Blue == AltarMode.Extreme)
            rewards.Add(FinalMagic);
        if (route == MapRoute.LifeGarden && policy.Garden == GardenMode.Triple)
            rewards.Add(PrimalCrown);
        if (route == MapRoute.Warfront && policy.Warfront == WarfrontMode.Decisive)
            rewards.Add(EternalCore);
        return rewards;
    }
}
