using GameForWork.Core.Ascendancies;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Spatial;

namespace GameForWork.Core.Combat;

public sealed class RuneFieldState(CombatProfile? profile)
{
    private Point[] _centers = [];
    public void Update(IEnumerable<Point> centers) => _centers = centers.ToArray();
    public int Layers(Point position) => profile?.Has("core.ascendancy.idol_forger.rune_field.small") == true
        ? Math.Min(3, _centers.Count(center => Point.DistanceSquared(center, position) <= 9_000_000)) : 0;
    public int DamageIncrease(Point position) => profile?.Has("core.ascendancy.idol_forger.rune_field.core") == true ? Layers(position) * 1_000 : 0;
    public int HitMultiplier(Point position) => profile?.Has("core.ascendancy.idol_forger.rune_field.core") == true ? 10_000 - Layers(position) * 500 : 10_000;
    public CharacterSheet Apply(CharacterSheet sheet, Point position) => Layers(position) == 0 ? sheet : sheet with
    { IncreasedArmorBasisPoints = sheet.IncreasedArmorBasisPoints + 1_500, IncreasedShieldBasisPoints = sheet.IncreasedShieldBasisPoints + 1_500 };
}
