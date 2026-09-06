using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Progression;
using GameForWork.Core.Combat;
using GameForWork.Core.Skills;

namespace GameForWork.Tests;

public sealed class SkillPaymentTests
{
    private const string Spell = "archetypes.skill.withering_ray";
    private static ResourceState Hero(params string[] rules) => new(new(1, new(0, 0, 0, 0), new(0, 0, 100),
        FlatMaximumLife: 912, FlatMaximumMana: 958, IncreasedManaRegenerationBasisPoints: -10_000,
        IncreasedShieldRechargeRateBasisPoints: -10_000), passives: PassiveModifiers.Empty with
        { MasteryMechanics = string.Join('|', rules.Select(rule => "builds.mastery.rule." + rule)) });

    [Fact]
    public void ManaWardRequiresAWandAndSharesTheHigherDamageRedirect()
    {
        var sheet = Hero().Sheet;
        var wand = new WeaponProfile("equipment.base.wand.1", 10, 10, 1_000, 0);
        var ward = PassiveModifiers.Empty with { MasteryMechanics = "builds.mastery.rule.法杖.6" };
        var equipped = new ResourceState(sheet, passives: ward, weapon: wand);
        equipped.ApplyEnemyDamage(100, true, 1);
        Assert.Equal(980, equipped.Mana);
        Assert.Equal(20, equipped.Shield);
        var unequipped = new ResourceState(sheet, passives: ward);
        unequipped.ApplyEnemyDamage(100, true, 1);
        Assert.Equal(1_000, unequipped.Mana);
        var combined = new ResourceState(sheet, passives: ward with { MasteryMechanics = ward.MasteryMechanics + "|builds.mastery.rule.法力.0" }, weapon: wand);
        combined.ApplyEnemyDamage(100, true, 1);
        Assert.Equal(970, combined.Mana);
        Assert.Equal(30, combined.Shield);
        Assert.Equal(0, combined.LastSkillManaPaid);
    }

    [Fact]
    public void OrdinaryShieldSubstitutionCannotClaimFullOverchargeFunding()
    {
        var profile = new GameForWork.Core.Ascendancies.CombatProfile(GameForWork.Core.Ascendancies.Ascendancy.AegisMage,
            ["core.ascendancy.aegis_mage.casting.core"]);
        var hero = new ResourceState(Hero().Sheet with { Equipment = new(0, 0, 1_000), IncreasedShieldRechargeRateBasisPoints = 0 },
            ascendancy: profile, passives: PassiveModifiers.Empty with { MasteryMechanics = "builds.mastery.rule.能量护盾.6" });
        for (int tick = 0; tick < 60; tick++) hero.AdvanceRegenerationTick(tick);
        Assert.Equal(300, hero.Overcharge);
        Assert.True(hero.TryPaySkillCost(SkillIds.EmberNova, 0, 101));
        Assert.Equal(950, hero.Shield);
        Assert.Equal(249, hero.Overcharge);
        Assert.Equal(1_000, hero.Mana);
        Assert.Equal(0, hero.LastSkillManaPaid);
        Assert.False(hero.LastSpellFullyFunded);
    }

    [Fact]
    public void ShieldPaymentFloorsHalfAndNeverPartiallySpendsEitherResource()
    {
        var hero = Hero("能量护盾.6");
        Assert.True(hero.TryPaySkillCost(Spell, 0, 101));
        Assert.Equal(50, hero.Shield);
        Assert.Equal(949, hero.Mana);
        Assert.Equal(51, hero.LastSkillManaPaid);
        Assert.False(hero.LastSpellFullyFunded);
        Assert.True(hero.LastDamageTick < 0);
        Assert.False(hero.TryPaySkillCost(Spell, 0, 102));
        Assert.Equal(50, hero.Shield);
        Assert.Equal(949, hero.Mana);
        Assert.True(hero.TryPayMana(949));
        Assert.False(hero.TryPaySkillCost(Spell, 0, 1));
        Assert.Equal(50, hero.Shield);
        Assert.False(hero.TryPaySkillCost(Spell, 0, 0, extraShield: 51));
    }

    [Fact]
    public void ShieldSubstitutionDoesNotApplyToAttackOrLifeCosts()
    {
        var hero = Hero("能量护盾.6");
        Assert.True(hero.TryPaySkillCost(SkillIds.HeavyStrike, 0, 100));
        Assert.True(hero.TryPaySkillCost(Spell, 100, 0));
        Assert.Equal(100, hero.Shield);
        Assert.Equal(900, hero.Life);
        Assert.Equal(900, hero.Mana);
    }

    [Fact]
    public void ManaAbsorbsHitsBeforeShieldAndShortfallOverflowsWithoutAffectingDamageOverTime()
    {
        var hero = Hero("法力.0");
        Assert.Equal(100, hero.ApplyEnemyDamage(100, true, 1));
        Assert.Equal(970, hero.Mana);
        Assert.Equal(30, hero.Shield);
        hero.TryPayMana(960);
        Assert.Equal(100, hero.ApplyEnemyDamage(100, true, 2));
        Assert.Equal(0, hero.Mana);
        Assert.Equal(940, hero.Life);
        hero.RestoreMana(100);
        hero.ApplyEnemyDamage(50, false, 3);
        Assert.Equal(100, hero.Mana);
        Assert.Equal(890, hero.Life);
    }

    [Fact]
    public void HighManaChecksBeforePaymentAndExcludesTriggers()
    {
        var hero = Hero("法力.1");
        Assert.True(hero.TryPaySkillCost(SkillIds.HeavyStrike, 0, 200));
        Assert.Equal(14_000, hero.LastSkillPaymentMultiplier);
        var reactions = new ReactionState();
        reactions.Begin("first", SkillIds.HeavyStrike, paymentMultiplier: hero.LastSkillPaymentMultiplier);
        Assert.True(hero.TryPaySkillCost(SkillIds.HeavyStrike, 0, 0));
        Assert.Equal(10_000, hero.LastSkillPaymentMultiplier);
        Assert.Equal(14_000, reactions.ActionMultiplier("first"));
        hero.RestoreMana(200);
        Assert.True(hero.TryPaySkillCost(SkillIds.HeavyStrike, 0, 0, selfCast: false));
        Assert.Equal(10_000, hero.LastSkillPaymentMultiplier);
    }

    [Fact]
    public void EveryThirdSelfCastRefundsOnlyActualManaPayments()
    {
        var hero = Hero("法力.5", "能量护盾.6");
        hero.TryPaySkillCost(SkillIds.HeavyStrike, 0, 100);
        hero.TryPaySkillCost(SkillIds.HeavyStrike, 0, 100, selfCast: false);
        hero.TryPaySkillCost(SkillIds.HeavyStrike, 10, 0);
        Assert.Equal(800, hero.Mana);
        hero.TryPaySkillCost(SkillIds.EmberNova, 0, 100);
        Assert.Equal(780, hero.Mana); // 50 paid, then (100 + 0 + 50) / 5 returned.
        Assert.Equal(50, hero.Shield);
    }

    [Fact]
    public void ChannelCountsOnceAndRetainsInitialManaConditionAcrossPulses()
    {
        var hero = Hero("法力.1", "法力.5");
        hero.TryPaySkillCost(SkillIds.HeavyStrike, 0, 50);
        hero.TryPaySkillCost(SkillIds.HeavyStrike, 0, 50);
        for (int tick = 0; tick <= 15; tick += 5)
        {
            hero.AdvanceRegenerationTick(tick);
            Assert.True(hero.TryPaySkillCost(Spell, 0, 100));
            Assert.Equal(14_000, hero.LastSkillPaymentMultiplier);
        }
        Assert.Equal(500, hero.Mana);
        hero.AdvanceRegenerationTick(20);
        Assert.Equal(500, hero.Mana);
        hero.AdvanceRegenerationTick(21);
        Assert.Equal(600, hero.Mana);
    }

    [Fact]
    public void RecentLifePaymentsExcludeDamageAndExpireAtFourSeconds()
    {
        var hero = Hero("生命.4");
        hero.ApplyDamage(500, 0);
        Assert.False(hero.HasRecentSkillLifePayment);
        hero.TryPaySkillCost(SkillIds.HeavyStrike, 49, 0);
        Assert.False(hero.HasRecentSkillLifePayment);
        hero.TryPaySkillCost(SkillIds.HeavyStrike, 1, 0);
        Assert.True(hero.HasRecentSkillLifePayment);
        hero.AdvanceRegenerationTick(79);
        Assert.True(hero.HasRecentSkillLifePayment);
        hero.AdvanceRegenerationTick(80);
        Assert.False(hero.HasRecentSkillLifePayment);
    }

    [Fact]
    public void ChannelAvailabilityUsesTheSameFractionalCreditAsPayment()
    {
        var hero = Hero();
        hero.TryPayMana(999);
        var channel = new ChannelCostState();
        var skill = CombatSkillRules.Resolve(new(Spell, SkillSupport.None, 1), hero.MaximumLife) with { ManaCost = 1, LifeCost = 0 };
        Assert.True(channel.CanPay(hero, skill));
        Assert.True(channel.TryPay(hero, skill, out int paid));
        Assert.Equal(1, paid);
        for (int i = 0; i < 3; i++)
        {
            Assert.True(channel.CanPay(hero, skill));
            Assert.True(channel.TryPay(hero, skill, out paid));
            Assert.Equal(0, paid);
        }
        Assert.False(channel.CanPay(hero, skill));
        Assert.False(channel.TryPay(hero, skill, out _));
    }
}
