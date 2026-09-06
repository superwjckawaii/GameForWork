using GameForWork.Core.Ascendancies;
using GameForWork.Core.Combat;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Skills;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Spatial;

namespace GameForWork.Tests;

public sealed partial class CombatClosureTests
{
    [Fact]
    public void AuraRecipientsShareEffectsButKeepTheirOwnEquipmentAndReservation()
    {
        var source = Team() with
        {
            Ascendancy = new(Ascendancy.SpiritCantor,
            ["core.ascendancy.spirit_cantor.aura.small", "core.ascendancy.spirit_cantor.aura.core", "core.ascendancy.spirit_cantor.mercenary.core"]),
            ActiveSkills = [new(SkillIds.ElementalResonance, SkillSupport.None)]
        };
        var aura = AuraCombatProfile.Resolve(source);
        var unit = aura.ForUnit(11_700);
        Assert.Equal(2_625, unit.Build.Sheet.FireResistanceBasisPoints);
        Assert.Equal(7_000, unit.Build.CombatEquipment!.Value(ItemModifierKind.IncreasedElementalDamageBasisPoints));
        Assert.Empty(aura.ForUnit(11_701).ActiveIds);
        Assert.Equal(0, aura.ForUnit(11_701).Build.Sheet.FireResistanceBasisPoints);
        var mercenary = Team() with { Sheet = Team().Sheet with { FireResistanceBasisPoints = 1_000 } };
        Assert.Equal(4_375, aura.ForMercenary(mercenary, 11_700).Build.Sheet.FireResistanceBasisPoints);
        Assert.Equal(1_000, aura.ForMercenary(mercenary, 11_701).Build.Sheet.FireResistanceBasisPoints);
        Assert.Equal(aura.ReservedMana, unit.ReservedMana);
    }

    [Fact]
    public void SanctuaryAndArmyDistinguishHeroPenaltiesFromUnitBonusesAndRange()
    {
        var aura = AuraCombatProfile.Resolve(Team() with
        {
            ActiveSkills =
            [new("builds.skill.undying_sanctuary", SkillSupport.None), new("builds.skill.hundred_soul_army", SkillSupport.None)]
        });
        Assert.Equal(-3_600, aura.Build.PassiveProfile!.MoreDamageBasisPoints);
        var inside = aura.ForUnit(10_000);
        Assert.Equal(-2_000, inside.Build.PassiveProfile!.MoreDamageBasisPoints);
        Assert.Equal(13_500, inside.UnitDamageMultiplier);
        Assert.Equal(5_000, inside.UnitLifeIncrease);
        Assert.Equal(7_700, inside.Build.Sheet.MaximumElementalResistanceBasisPoints);
        Assert.Equal(8_500, inside.IncomingHitMultiplier);
        Assert.Equal(10_000, aura.ForUnit(10_001).UnitDamageMultiplier);
        Assert.Equal(0, aura.ForUnit(10_001).UnitLifeIncrease);
    }

    [Fact]
    public void TeamProtectionTriggersOnCrossingLowLifeAndSharesCooldownWithSeparatePools()
    {
        var protection = new TeamProtectionState(true);
        protection.Update("hero", 1_000, 1_000, 500, true, 0);
        protection.Update("unit", 200, 200, 0, false, 0);
        Assert.True(protection.Update("hero", 500, 1_000, 500, true, 1));
        Assert.Equal(300, protection.Barrier("hero", 1));
        Assert.Equal(40, protection.Barrier("unit", 1));
        Assert.Equal(40, protection.Absorb("unit", 100, true, 2));
        Assert.Equal(0, protection.Barrier("unit", 2));
        Assert.Equal(0, protection.Absorb("hero", 100, false, 2));
        Assert.Equal(200, protection.Barrier("hero", 2));
        Assert.False(protection.Update("unit", 100, 200, 0, true, 3));
        Assert.Equal(0, protection.Barrier("hero", 81));
        Assert.False(protection.Update("hero", 500, 1_000, 500, true, 201));
        protection.Update("hero", 501, 1_000, 500, true, 202);
        Assert.True(protection.Update("hero", 500, 1_000, 500, true, 203));
    }

    [Fact]
    public void InstantWarsongDoesNotDisplaceTheFirstWeaponAction()
    {
        var baseline = Run(Team(), 250);
        var team = Team() with
        {
            Ascendancy = new(Ascendancy.SpiritCantor, ["core.ascendancy.spirit_cantor.war_song.core"]),
            ActiveSkills = [new("archetypes.skill.soul_warsong", SkillSupport.None, Priority: 0), new(SkillIds.HeavyStrike, SkillSupport.None)]
        };
        var actual = Run(team, 250);
        Assert.Contains(actual.Events, item => item.Detail.Contains("buff-applied|instant"));
        Assert.Equal(baseline.Events.First(item => item.Kind == SpatialEventKind.HeavyStrike).AtMilliseconds,
            actual.Events.First(item => item.Kind == SpatialEventKind.HeavyStrike).AtMilliseconds);
        Assert.True(actual.Events.First(item => item.Kind == SpatialEventKind.HeavyStrike).Value > baseline.Events.First(item => item.Kind == SpatialEventKind.HeavyStrike).Value);
    }

    [Fact]
    public void CantorBlessingUsesAdditiveRecipientEffectAndFiniteDuration()
    {
        var buffs = new CombatBuffState(new(Ascendancy.SpiritCantor,
            ["core.ascendancy.spirit_cantor.blessing.small", "core.ascendancy.spirit_cantor.blessing.core"]));
        Assert.True(buffs.Activate(new("archetypes.skill.fellowship_blessing", SkillSupport.None), false, 0));
        Assert.Equal(4_000, buffs.Apply(Team(), 207).IncreasedGenericDamageBasisPoints - Team().IncreasedGenericDamageBasisPoints);
        Assert.Equal(3_000, buffs.ForUnit(207, new(0, 0), new(0, 0)).DamageIncrease);
        Assert.Equal(0, buffs.ForUnit(207, new(0, 0), new(9_001, 0)).DamageIncrease);
        var team = Team();
        Assert.Same(team, buffs.Apply(team, 208));
        Assert.Equal(2_500, buffs.CooldownRecovery("archetypes.skill.fellowship_blessing"));
    }

    [Fact]
    public void CantorWarsongStacksOneMoreFactorForWholePartyWithSharedExpiry()
    {
        var buffs = new CombatBuffState(new(Ascendancy.SpiritCantor,
            ["core.ascendancy.spirit_cantor.war_song.small", "core.ascendancy.spirit_cantor.war_song.core"]));
        var song = new SkillConfiguration("archetypes.skill.soul_warsong", SkillSupport.None);
        Assert.True(buffs.Instant(song.SkillId));
        buffs.Activate(song, false, 0);
        buffs.Activate(song, false, 60);
        buffs.Activate(song, false, 119);
        buffs.Activate(song, false, 120);
        Assert.Equal(4_500, buffs.Apply(Team(), 239).PassiveProfile!.MoreDamageBasisPoints);
        Assert.Equal(4_500, buffs.ForUnit(239, new(0, 0), new(30_000, 0)).MoreDamage);
        Assert.Equal(3_125, buffs.ForUnit(121, new(0, 0), new(10_000, 0)).ActionSpeed);
        Assert.Equal(0, buffs.WarSongMore(240));
        buffs.Activate(song, false, 240);
        Assert.Equal(1_500, buffs.WarSongMore(240));
    }
}
