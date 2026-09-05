using GameForWork.Core.Campaign.World;
using GameForWork.Core.Content;

namespace GameForWork.Core.Encounters;

public sealed record EncounterModifiers(int Life = 10_000, int Damage = 10_000, int Speed = 10_000,
    int MaximumLife = 10_000, int Defenses = 10_000, int IncomingHits = 10_000,
    int FlaskRecovery = 10_000, bool ExtraPhase = false, int MoreBleedDamage = 0)
{
    public static EncounterModifiers For(MapPlan? plan, int nodeIndex, MapItem? map)
    {
        MapNode? node = plan?.Nodes.FirstOrDefault(n => n.Index == nodeIndex);
        EncounterRule? rule = node?.Gameplay;
        var result = new EncounterModifiers(rule?.Life ?? 10_000, rule?.Damage ?? 10_000);
        if (plan is null || map is null) return result;
        // Costs begin with the selected altar's guards and last for this map only.
        foreach (MapNode prior in plan.Nodes.Where(n => n.Index <= nodeIndex && n.Gameplay?.Mechanic == Mechanic.Red))
        {
            Choice choice = prior.Gameplay!.Choice!;
            result = choice.Cost switch
            {
                Cost.MaximumLife => result with { MaximumLife = Gameplay.Scale(result.MaximumLife, 10_000 - choice.Magnitude) },
                Cost.Defenses => result with { Defenses = Gameplay.Scale(result.Defenses, 10_000 - choice.Magnitude) },
                Cost.IncomingHits => result with { IncomingHits = Gameplay.Scale(result.IncomingHits, 10_000 + choice.Magnitude) },
                Cost.FlaskRecovery => result with { FlaskRecovery = Gameplay.Scale(result.FlaskRecovery, 10_000 - choice.Magnitude) },
                _ => result,
            };
            if (map.EquipmentSnapshot?.RedVow == true) result = result with { MoreBleedDamage = 6_000 };
        }
        if (nodeIndex == plan.Nodes[^1].Index)
        {
            MapNode[] blueAltars = plan.Nodes.Where(n => n.Gameplay?.Mechanic == Mechanic.Blue).ToArray();
            AltarMode blue = Gameplay.Policy(map).Blue;
            foreach (MapNode altar in blueAltars)
            {
                Choice choice = altar.Gameplay!.Choice!;
                result = choice.Cost switch
                {
                    Cost.BossLife when blue == AltarMode.Normal =>
                        result with { Life = Gameplay.Scale(result.Life, 10_000 + choice.Magnitude) },
                    Cost.BossDamage when blue == AltarMode.Normal =>
                        result with { Damage = Gameplay.Scale(result.Damage, 10_000 + choice.Magnitude) },
                    Cost.BossSpeed => result with { Speed = Gameplay.Scale(result.Speed, 10_000 + choice.Magnitude) },
                    Cost.BossPhase => result with { ExtraPhase = true }, _ => result,
                };
            }
            if (blueAltars.Length > 0 && blue != AltarMode.Normal)
                result = result with
                {
                    Life = Gameplay.Scale(result.Life, blue == AltarMode.Extreme ? 25_000 : 17_500),
                    Damage = Gameplay.Scale(result.Damage, blue == AltarMode.Extreme ? 20_000 : 15_000),
                };
            if (blueAltars.Length > 0 && map.EquipmentSnapshot?.BlueVow == true)
                result = result with { Life = Gameplay.Scale(result.Life, 12_500), Damage = Gameplay.Scale(result.Damage, 12_500) };
        }
        return result;
    }

    public TeamBuild Apply(TeamBuild build)
    {
        var sheet = build.Sheet;
        return build with { MoreBleedDamageBasisPoints = GameForWork.Core.Builds.CombatRules.CombineMoreBasisPoints(build.MoreBleedDamageBasisPoints, MoreBleedDamage), Sheet = sheet with
        {
            FlatMaximumLife = Math.Max(1, Gameplay.Scale(sheet.MaximumLife().Value, MaximumLife)) - 80 - 8 * sheet.Level - sheet.Attributes.Physique,
            IncreasedMaximumLifeBasisPoints = 0,
            Equipment = sheet.Equipment with
            {
                Armor = Gameplay.Scale(sheet.Armor().Value, Defenses),
                Evasion = Gameplay.Scale(sheet.Evasion().Value, Defenses) - sheet.Attributes.Dexterity,
            },
            IncreasedArmorBasisPoints = 0, IncreasedEvasionBasisPoints = 0,
        }};
    }
}
