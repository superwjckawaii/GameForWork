using GameForWork.Core.P1.World;
using GameForWork.Core.P14;

namespace GameForWork.Core.P28;

public sealed record P28EncounterModifiers(int Life = 10_000, int Damage = 10_000, int Speed = 10_000,
    int MaximumLife = 10_000, int Defenses = 10_000, int IncomingHits = 10_000,
    int FlaskRecovery = 10_000, bool ExtraPhase = false)
{
    public static P28EncounterModifiers For(P14MapPlan? plan, int nodeIndex, P1MapItem? map)
    {
        P14MapNode? node = plan?.Nodes.FirstOrDefault(n => n.Index == nodeIndex);
        P28EncounterRule? rule = node?.Gameplay;
        var result = new P28EncounterModifiers(rule?.Life ?? 10_000, rule?.Damage ?? 10_000);
        if (plan is null || map is null) return result;
        // Costs begin with the selected altar's guards and last for this map only.
        foreach (P14MapNode prior in plan.Nodes.Where(n => n.Index <= nodeIndex && n.Gameplay?.Mechanic == P28Mechanic.Red))
        {
            P28Choice choice = prior.Gameplay!.Choice!;
            result = choice.Cost switch
            {
                P28Cost.MaximumLife => result with { MaximumLife = P28Gameplay.Scale(result.MaximumLife, 10_000 - choice.Magnitude) },
                P28Cost.Defenses => result with { Defenses = P28Gameplay.Scale(result.Defenses, 10_000 - choice.Magnitude) },
                P28Cost.IncomingHits => result with { IncomingHits = P28Gameplay.Scale(result.IncomingHits, 10_000 + choice.Magnitude) },
                P28Cost.FlaskRecovery => result with { FlaskRecovery = P28Gameplay.Scale(result.FlaskRecovery, 10_000 - choice.Magnitude) },
                _ => result,
            };
        }
        if (nodeIndex == plan.Nodes[^1].Index)
        {
            foreach (P14MapNode altar in plan.Nodes.Where(n => n.Gameplay?.Mechanic == P28Mechanic.Blue))
            {
                P28Choice choice = altar.Gameplay!.Choice!;
                result = choice.Cost switch
                {
                    P28Cost.BossLife => result with { Life = P28Gameplay.Scale(result.Life, 10_000 + choice.Magnitude) },
                    P28Cost.BossDamage => result with { Damage = P28Gameplay.Scale(result.Damage, 10_000 + choice.Magnitude) },
                    P28Cost.BossSpeed => result with { Speed = P28Gameplay.Scale(result.Speed, 10_000 + choice.Magnitude) },
                    P28Cost.BossPhase => result with { ExtraPhase = true }, _ => result,
                };
                P28AltarMode blue = P28Gameplay.Policy(map).Blue;
                result = result with
                {
                    Life = P28Gameplay.Scale(result.Life, blue == P28AltarMode.Extreme ? 25_000 : blue == P28AltarMode.HighPressure ? 17_500 : 10_000),
                    Damage = P28Gameplay.Scale(result.Damage, blue == P28AltarMode.Extreme ? 20_000 : blue == P28AltarMode.HighPressure ? 15_000 : 10_000),
                };
            }
        }
        return result;
    }

    public P1TeamBuild Apply(P1TeamBuild build)
    {
        var sheet = build.Sheet;
        return build with { Sheet = sheet with
        {
            FlatMaximumLife = Math.Max(1, P28Gameplay.Scale(sheet.MaximumLife().Value, MaximumLife)) - 80 - 8 * sheet.Level - sheet.Attributes.Physique,
            IncreasedMaximumLifeBasisPoints = 0,
            Equipment = sheet.Equipment with
            {
                Armor = P28Gameplay.Scale(sheet.Armor().Value, Defenses),
                Evasion = P28Gameplay.Scale(sheet.Evasion().Value, Defenses) - sheet.Attributes.Dexterity,
            },
            IncreasedArmorBasisPoints = 0, IncreasedEvasionBasisPoints = 0,
        }};
    }
}
