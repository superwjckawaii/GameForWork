using GameForWork.Core.Equipment;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Campaign.Progression;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.SkillCatalog;
using GameForWork.Core.Art;
using GameForWork.Core.Builds;
using GameForWork.Core.Spatial;
using GameForWork.Core.Skills;

namespace GameForWork.Tests;

public sealed class ImplementationClosureTests
{
    [Fact]
    public void EveryCurrentSkillStoneHasOneDistinctCellAndUnknownIdsFail()
    {
        string[] ids = ActiveSkillCatalog.Active.Select(skill => skill.Combat.StoneId)
            .Concat(ActiveSkillCatalog.Supports.Select(skill => skill.StoneId)).ToArray();
        Assert.Equal(184, ids.Length);
        Assert.Equal(Enumerable.Range(0, 184), ids.Select(ArtContract.SkillStoneIndex).Order());
        Assert.Throws<KeyNotFoundException>(() => ArtContract.SkillStoneIndex("missing.stone"));
    }

    [Fact]
    public void SingleElementMaximumDoesNotRaiseOtherElementsAndRainbowRaisesVoid()
    {
        var loadout = new EquipmentLoadout();
        ItemInstance ring = ItemGenerator.Generate("core.base.life_ring", 100, ItemRarity.Basic, 43);
        ring = ring with { Affixes = [Fixed(ring, ItemModifierKind.MaximumFireResistanceBasisPoints, 300)],
            Enchantment = EquipmentEnchantmentCatalog.All.Single(enchantment => enchantment.DisplayName == "虹彩王印") };
        Assert.True(loadout.TryEquip(EquipmentSlot.RingLeft, ring));
        var build = Assemble(loadout);
        Assert.Equal(7_800, build.Sheet.ResistanceMaximum(EnemyDamageType.Fire));
        Assert.Equal(7_500, build.Sheet.ResistanceMaximum(EnemyDamageType.Cold));
        Assert.Equal(7_500, build.Sheet.ResistanceMaximum(EnemyDamageType.Lightning));
        Assert.Equal(8_000, build.Sheet.ResistanceMaximum(EnemyDamageType.Void));
    }

    [Theory]
    [InlineData("无名谦冠", EquipmentSlot.Helmet, VirtueViceKind.Humility)]
    [InlineData("傲慢之握", EquipmentSlot.Gloves, VirtueViceKind.Arrogance)]
    [InlineData("怒节同契", EquipmentSlot.Chest, VirtueViceKind.Rage)]
    public void LegendarySignaturesActuallyHoldTheirLayers(string name, EquipmentSlot slot, VirtueViceKind kind)
    {
        var loadout = new EquipmentLoadout();
        Assert.True(loadout.TryEquip(slot, EquipmentLegendaryFactory.CreateByName(name, 100, "closure")));
        VirtueViceLoadout virtues = Assemble(loadout).VirtueViceLoadout!;
        Assert.Contains(kind, virtues.HeldAtMaximum);
        var state = new VirtueViceState(virtues.AdditionalMaximum, virtues.HeldAtMaximum);
        state.Advance(60_000);
        Assert.Equal(state.Maximum(kind), state.Layers(kind));
    }

    [Fact]
    public void PairedVirtueRollIsConsumedByBuildAndKeepsBothSelectedKinds()
    {
        for (ulong seed = 1; seed <= 9; seed++)
        {
            var loadout = new EquipmentLoadout();
            loadout.TryEquip(EquipmentSlot.Belt, EquipmentLegendaryFactory.CreateByName("两极德印", 100, $"pair-{seed}", seed));
            Assert.Equal(2, Assemble(loadout).VirtueViceLoadout!.HeldAtMaximum.Count);
        }
    }

    [Fact]
    public void RecoveryModifiersAffectResourcesIncludingSmallPerTickAmounts()
    {
        var sheet = Sheet() with { MaximumLifeRegenerationBasisPoints = 1_000, MaximumShieldRegenerationBasisPoints = 1_000,
            LifeRecoveryMultiplierBasisPoints = 7_000, IncreasedRecoveryRateBasisPoints = 5_000 };
        var hero = new ResourceState(sheet, 10, initialShield: 0);
        for (int tick = 0; tick < 20; tick++) { hero.ApplyDamage(1, tick); hero.AdvanceRegenerationTick(tick); }
        Assert.True(hero.Life > 10);
        Assert.True(hero.Shield > 0); // Regeneration continues while damage prevents recharge.
        var small = new ResourceState(Sheet() with { LifeRecoveryMultiplierBasisPoints = 7_000 }, 1);
        for (int i = 0; i < 10; i++) small.HealLife(1);
        Assert.Equal(8, small.Life);
    }

    [Fact]
    public void SpellDamageNoLongerReceivesAttackOnlyIncreasesAndCastSpeedChangesDelay()
    {
        var config = new SkillConfiguration(SkillIds.EmberNova, SkillSupport.None);
        ResolvedSkill skill = CombatSkillRules.Resolve(config, 1000);
        TeamBuild build = Team();
        int baseline = CombatSkillRules.ScaleOffensiveDamage(100, skill, config, build, SkillTag.Spell | SkillTag.Fire, 100, 100);
        Assert.Equal(baseline, CombatSkillRules.ScaleOffensiveDamage(100, skill, config,
            build with { IncreasedDamageBasisPoints = 50_000 }, SkillTag.Spell | SkillTag.Fire, 100, 100));
        var fast = build with { CombatEquipment = Loadout(modifiers: new() { [ItemModifierKind.IncreasedCastSpeedBasisPoints] = 10_000 }) };
        Assert.Equal(10, CombatSkillRules.ActionDelay(fast, 20, SkillTag.Spell));
    }

    [Fact]
    public void HitRecoveryAndLeechReachActualResourcePools()
    {
        var runtime = new EquipmentCombatRuntime(Loadout(modifiers: new()
        {
            [ItemModifierKind.LifeOnHit] = 20, [ItemModifierKind.ManaOnHit] = 10,
            [ItemModifierKind.ShieldOnHit] = 15, [ItemModifierKind.ManaLeechBasisPoints] = 1_000,
        }), 1);
        var hero = new ResourceState(Sheet(), 10, 10, 10);
        runtime.OnHit(hero, SkillTag.Attack, "enemy", false, false, 200, null);
        Assert.Equal(30, hero.Life); Assert.Equal(20, hero.Mana); Assert.Equal(25, hero.Shield);
        for (int tick = 0; tick < 20; tick++) hero.AdvanceRegenerationTick(tick);
        Assert.True(hero.Mana > 20);
    }

    [Fact]
    public void EnchantmentRollsOncePerActionAndLayersExpire()
    {
        var runtime = new EquipmentCombatRuntime(Loadout(enchantments: new() { ["傲慢之印"] = 3 }), 42);
        var state = new VirtueViceState(new Dictionary<VirtueViceKind, int> { [VirtueViceKind.Arrogance] = 3 }, []);
        var hero = new ResourceState(Sheet());
        for (int action = 0; action < 100; action++)
        {
            runtime.BeginAction(SkillIds.HeavyStrike, 0, 1, false, state);
            int before = state.Layers(VirtueViceKind.Arrogance);
            for (int hit = 0; hit < 30; hit++) runtime.OnHit(hero, SkillTag.Attack, "enemy", false, true, 1, state);
            Assert.InRange(state.Layers(VirtueViceKind.Arrogance) - before, 0, 1);
        }
        Assert.Equal(state.Maximum(VirtueViceKind.Arrogance), state.Layers(VirtueViceKind.Arrogance));
        state.Advance(12_001);
        Assert.Equal(0, state.Layers(VirtueViceKind.Arrogance));
    }

    [Fact]
    public void OnDamageDefensesApplyAfterHitAndExpireOrResetBetweenBattles()
    {
        var runtime = new EquipmentCombatRuntime(Loadout("终夜守望", "荆生树皮"), 1);
        Assert.Equal(10_000, runtime.IncomingMultiplier(Sheet(), EnemyDamageType.Physical, true, 0));
        runtime.DamageTaken(10, true, 0, null);
        Assert.Equal(7_600, runtime.IncomingMultiplier(Sheet(), EnemyDamageType.Physical, true, 1));
        Assert.Equal(9_500, runtime.IncomingMultiplier(Sheet(), EnemyDamageType.Physical, true, 40));
        Assert.Equal(10_000, new EquipmentCombatRuntime(Loadout("终夜守望"), 1).IncomingMultiplier(Sheet(), EnemyDamageType.Physical, true, 0));
    }

    [Fact]
    public void AshenHeartHasExactlyTwoRekindlesAndSetsResourcesInsteadOfHealing()
    {
        var runtime = new EquipmentCombatRuntime(Loadout("灰烬之心"), 2);
        var hero = new ResourceState(Sheet() with { LifeRecoveryMultiplierBasisPoints = 0 });
        foreach (int ratio in new[] { 7_500, 5_000 })
        {
            hero.ApplyDamage(int.MaxValue, 1);
            Assert.True(runtime.TryRekindle(hero));
            Assert.Equal(hero.MaximumLife * ratio / 10_000, hero.Life);
            Assert.Equal(hero.MaximumShield * ratio / 10_000, hero.Shield);
        }
        hero.ApplyDamage(int.MaxValue, 2);
        Assert.False(runtime.TryRekindle(hero));
        Assert.False(hero.IsAlive);
    }

    [Fact]
    public void ActualSpatialBattleConsumesConditionalLegendaryDamage()
    {
        TeamBuild baseline = Team() with { AlwaysHit = true, CannotCrit = true, UseWarCry = false };
        NodeCombatRequest Request(TeamBuild build) => new(build, 1, 1, 1, false, false, false, 0,
            MaximumTicks: 300, EnemyLifeBasisPoints: 100_000);
        var plain = new SpatialCombatRunner().Run(Request(baseline), 73);
        var equipped = new SpatialCombatRunner().Run(Request(baseline with { CombatEquipment = Loadout("统帅之负") }), 73);
        int FirstHit(NodeCombatResult result) => result.Events.First(e => e.Kind == SpatialEventKind.HeavyStrike && e.Value > 0).Value;
        Assert.True(FirstHit(equipped) > FirstHit(plain));
    }

    private static CharacterSheet Sheet() => new(20, new(100, 100, 100, 100), new(0, 0, 100));

    [Fact]
    public void SummonsExistBeforeActionsAndDealTheirOwnDamage()
    {
        var build = Team() with { UseWarCry = false,
            ActiveSkills = [new("archetypes.skill.summon_boneguard", SkillSupport.None)],
            CombatEquipment = Loadout() };
        NodeCombatResult Run(TeamBuild team) => new SpatialCombatRunner().Run(new(team,
            1, 1, 1, false, false, false, 0, MaximumTicks: 300, EnemyLifeBasisPoints: 100_000,
            EnemyPool: [Enemies.CorruptedWorker with { Life = 10_000,
                Skills = [new(EnemySkillKind.BasicStrike, "attack", EnemyDamageType.Physical, 10_000)] }]), 821);
        var first = Run(build);
        var second = Run(build with { Weapon = new("huge", 100_000, 100_000, 1000, 10_000),
            IncreasedDamageBasisPoints = 100_000 });
        Assert.Equal(6, first.Frames[0].Allies!.Count(a => a.SkillId == "archetypes.skill.summon_boneguard"));
        Assert.DoesNotContain(first.Events, e => e.SourceId == "hero" && e.Detail.Contains("summon_boneguard") && e.Value > 0);
        var hits = first.Events.Where(e => e.SourceId.StartsWith("army:") && e.Value > 0).Select(e => e.Value).ToArray();
        Assert.NotEmpty(hits);
        Assert.Equal(hits, second.Events.Where(e => e.SourceId.StartsWith("army:") && e.Value > 0).Select(e => e.Value));
        Assert.Contains(first.Events, e => e.Kind == SpatialEventKind.EnemyAttack && e.TargetId.StartsWith("army:"));
    }

    [Fact]
    public void AutomaticArmySplitsGroupsAndHonorsHardCap()
    {
        var build = Team() with { ActiveSkills = [new("archetypes.skill.summon_boneguard", SkillSupport.None),
            new("archetypes.skill.summon_soulbow", SkillSupport.None), new("archetypes.skill.summon_spirit_beast", SkillSupport.None)],
            CombatEquipment = Loadout(modifiers: new() { [ItemModifierKind.AdditionalMinionMaximum] = 100 }) };
        var result = new SpatialCombatRunner().Run(new(build, 1, 1, 1, false, false, false, 0, MaximumTicks: 1), 1);
        Assert.Equal(8, result.Frames[0].Allies!.Count(a => a.SkillId == "archetypes.skill.summon_boneguard"));
        Assert.Equal(8, result.Frames[0].Allies!.Count(a => a.SkillId == "archetypes.skill.summon_soulbow"));
        Assert.Single(result.Frames[0].Allies!, a => a.SkillId == "archetypes.skill.summon_spirit_beast");
    }

    [Fact]
    public void OrdinaryHealingCannotResurrect()
    {
        var hero = new ResourceState(Sheet());
        hero.ApplyDamage(int.MaxValue, 0);
        Assert.Equal(0, hero.HealLife(int.MaxValue));
        hero.AdvanceRegenerationTick(100);
        Assert.False(hero.IsAlive);
    }

    [Fact]
    public void LethalHitsInSameTickResolveEachRekindleImmediately()
    {
        var runtime = new EquipmentCombatRuntime(Loadout("灰烬之心"), 7);
        var hero = new ResourceState(Sheet());
        hero.LifeDepleted = () => runtime.TryRekindle(hero);
        hero.ApplyDamage(int.MaxValue, 10);
        Assert.True(hero.IsAlive);
        Assert.Equal(1, runtime.Rekindles);
        hero.ApplyDamage(int.MaxValue, 10);
        Assert.True(hero.IsAlive);
        Assert.Equal(2, runtime.Rekindles);
        hero.ApplyDamage(int.MaxValue, 10);
        Assert.False(hero.IsAlive);
    }

    [Fact]
    public void LastPageCourtUsesUnusedSlotsBeforeForcingOneMinion()
    {
        var build = Team() with { ActiveSkills = [new("archetypes.skill.summon_boneguard", SkillSupport.None)],
            CombatEquipment = Loadout("末页王庭") };
        var result = new SpatialCombatRunner().Run(new(build, 1, 1, 1, false, false, false, 0, MaximumTicks: 1), 1);
        var unit = Assert.Single(result.Frames[0].Allies!, a => a.SkillId == "archetypes.skill.summon_boneguard");
        Assert.Equal(450, unit.MaximumLife);
    }

    [Fact]
    public void GardenerPreviewAndExecutionAgreeAndPreserveAnExtraMutableAffix()
    {
        var item = ItemGenerator.Generate("core.base.life_ring", 100, ItemRarity.Rare, 481);
        var operation = EquipmentCatalog.CraftingOperations.First(o => o.DisplayName.StartsWith("保留前缀"));
        var ordinary = new EquipmentCraftingRequest(operation.Id, Seed: 68);
        var equipped = ordinary with { Equipment = Loadout("园丁筋络") };
        var preview = EquipmentCraftingService.Preview(item, equipped);
        Assert.Equal((EquipmentCraftingService.Preview(item, ordinary).Cost * 7 + 9) / 10, preview.Cost);
        var wallet = new EquipmentCraftingWallet(); wallet.Credit(preview.Resource, preview.Cost);
        var result = EquipmentCraftingService.Execute(wallet, item, equipped);
        Assert.True(result.Succeeded, result.FailureReason);
        Assert.Equal(preview.Cost, result.Cost);
        Assert.Equal(0, wallet[preview.Resource]);
        Assert.Contains(result.Item!.Affixes, a => a.Definition.Position == AffixPosition.Suffix && item.Affixes.Contains(a));
        Assert.InRange(result.Item.PrefixCount, 0, 3);
        Assert.InRange(result.Item.SuffixCount, 0, 3);
    }

    [Fact]
    public void HalfRerollKeepsFractureProtectionForTheNextCraft()
    {
        var item = ItemGenerator.Generate("core.base.life_ring", 100, ItemRarity.Rare, 481);
        var fracture = item.Affixes.First(a => a.Definition.Position == AffixPosition.Suffix);
        item = item with { FracturedAffixFamilyId = fracture.Definition.StableFamilyId };
        var operation = EquipmentCatalog.CraftingOperations.First(o => o.DisplayName.StartsWith("保留前缀"));
        for (ulong seed = 1; seed <= 2; seed++)
        {
            var request = new EquipmentCraftingRequest(operation.Id, Seed: seed);
            var preview = EquipmentCraftingService.Preview(item, request);
            var wallet = new EquipmentCraftingWallet(); wallet.Credit(preview.Resource, preview.Cost);
            var result = EquipmentCraftingService.Execute(wallet, item, request);
            Assert.True(result.Succeeded);
            item = result.Item!;
            Assert.Equal(fracture.Definition.StableFamilyId, item.FracturedAffixFamilyId);
            Assert.Contains(fracture, item.Affixes);
        }
    }

    [Fact]
    public void ReducedRecoveryDoesNotExtendLeechVolume()
    {
        var baseline = new ResourceState(Sheet() with { LifeRecoveryMultiplierBasisPoints = 5_000 }, 10);
        baseline.AddLifeLeech(100);
        for (int tick = 0; tick < 80; tick++) baseline.AdvanceRegenerationTick(tick);
        Assert.Equal(60, baseline.Life);
    }
    private static TeamBuild Team() => new(Sheet(), new("audit-weapon", 80, 100, 1000, 0),
        new(SkillIds.HeavyStrike, SkillSupport.None), FlatAccuracy: 10_000,
        ActiveSkills: [new(SkillIds.HeavyStrike, SkillSupport.None)]);
    private static AssembledCharacterBuild Assemble(EquipmentLoadout loadout) => CharacterBuildAssembler.Assemble(20,
        new(100, 100, 100, 100), loadout, new PassiveTreeAllocation(0), new(SkillIds.HeavyStrike, SkillSupport.None));
    private static AffixRoll Fixed(ItemInstance item, ItemModifierKind kind, int value) => new(
        new("audit-affix", "audit", item.Base.Category, AffixPosition.Suffix, 1, 1, value, value, 1, kind), value);
    private static EquipmentCombatLoadout Loadout(params string[] names) => Loadout(names, null, null);
    private static EquipmentCombatLoadout Loadout(string[]? names = null, Dictionary<ItemModifierKind, int>? modifiers = null,
        Dictionary<string, int>? enchantments = null) => new(modifiers ?? [],
        (names ?? []).Select(name => EquipmentCatalog.LegendaryItems.Single(item => item.DisplayName == name).Id).ToArray(), enchantments ?? []);
}
