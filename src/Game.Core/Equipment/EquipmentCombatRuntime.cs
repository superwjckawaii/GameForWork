using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.SkillCatalog;
using GameForWork.Core.Builds;
using GameForWork.Core.Skills;
using GameForWork.Core.Simulation;

namespace GameForWork.Core.Equipment;

public sealed record EnemyDamageResult(int Damage, int ShieldLoss, bool Hit, bool Blocked, bool ShieldBroken, int Tick);

public sealed record EquipmentActionContext(string Id, bool LifePaid, bool FallingStar, bool Triggered,
    HashSet<string> Triggers, string ProjectilePrimaryTarget = "", bool Copy = false);
public sealed record EquipmentOffenseSnapshot(bool FullLife, bool CompanionAlive, bool LifePaid, bool FallingStar, int Rekindles);

/// <summary>One battle's mutable equipment state. No state is shared between simulations.</summary>
public sealed class EquipmentCombatRuntime(EquipmentCombatLoadout loadout, ulong seed)
{
    private readonly Pcg32 _random = new(seed ^ 0x65717569706d656eUL);
    private readonly HashSet<string> _firstBossHits = [];
    private HashSet<string> _actionTriggers = [];
    private readonly Dictionary<string, int> _debuffUntil = [];
    private int _actionSequence, _blackTide, _blackTideUntil, _marchTicks, _stationaryTicks;
    private int _reverseTideTicks, _reverseTideStacks, _lastHoldReady, _attackBuffUntil, _spellBuffUntil, _movementBuffUntil;
    private int _returningShieldUntil, _quiverMovementUntil, _currentTick;
    private bool _wasMoving;
    private readonly Queue<(int Tick, int Mana)> _tidalMana = [];
    private int _barkUntil, _compassUntil, _bannerUntil, _suppressionUntil, _suppressionReady, _shieldHealReady;
    private int _bulwark, _rekindles;
    private int _seaReady, _seaUntil;
    private string _cohuntTarget = "";
    private string _projectilePrimaryTarget = "";
    private int _cohuntLayers;
    private bool _hunt, _fallingStar, _actionFallingStar, _lifePaid;
    private string _action = "";
    private bool _triggered;
    private bool _copy;
    public EquipmentActionContext CaptureAction() => new(_action, _lifePaid, _actionFallingStar, _triggered, _actionTriggers, _projectilePrimaryTarget, _copy);
    public EquipmentActionContext CreateTriggeredAction(string primaryTarget, bool copy = false) =>
        new($"equipment-action:{++_actionSequence}", false, false, true, [], primaryTarget, copy);
    public void InAction(EquipmentActionContext action, Action resolve)
    {
        EquipmentActionContext previous = CaptureAction();
        Set(action);
        try { resolve(); }
        finally { Set(previous); }
        void Set(EquipmentActionContext value)
        {
            _action = value.Id; _lifePaid = value.LifePaid; _actionFallingStar = value.FallingStar;
            _triggered = value.Triggered; _actionTriggers = value.Triggers;
            _projectilePrimaryTarget = value.ProjectilePrimaryTarget;
            _copy = value.Copy;
        }
    }
    public EquipmentCombatLoadout Loadout { get; } = loadout;
    public int Rekindles => _rekindles;
    public int ExternalSkillCostMultiplier { get; set; } = 10_000;
    public Func<int>? NearbyEnemyCount { get; set; }
    public bool Has(string name) => Loadout.Has(name);
    public bool HasBase(string stableId) => Loadout.HasBase(stableId);
    public int BaseRuleValue(string stableId) => Loadout.BaseRuleValue(stableId);
    public int CohuntLayers => _cohuntLayers;
    public bool BeginProjectileAction(string targetId)
    {
        _projectilePrimaryTarget = targetId;
        if (!Has("合猎箭匣")) return false;
        if (_cohuntTarget != targetId) { _cohuntTarget = targetId; _cohuntLayers = 0; }
        if (_cohuntLayers < 5) return false;
        _cohuntLayers = 0;
        return true;
    }
    public void ProjectileHit(bool rareOrBoss)
    {
        if (Has("合猎箭匣") && rareOrBoss && _projectilePrimaryTarget == _cohuntTarget &&
            _projectilePrimaryTarget.Length > 0 && _actionTriggers.Add("cohunt"))
            _cohuntLayers = Math.Min(5, _cohuntLayers + 1);
    }
    public void EndEncounter() { _cohuntLayers = 0; _cohuntTarget = ""; }
    public int Value(ItemModifierKind kind) => Loadout.Value(kind);
    public string ActionId => _action;
    public Action? PhysicalMeleeHit { get; set; }
    public bool ForceCritical(SkillTag tags) => _hunt && tags.HasFlag(SkillTag.Attack);
    public int SuppressionBonus(int tick) => tick < _suppressionUntil ? 10_000 : 0;
    public int SpeedBonus(int tick) => (tick < _bannerUntil ? 3_500 : 0) + _rekindles * 1_500;
    public int AttackSpeedBonus(int tick) => HasBase("harbor.base.tidewalker_rapier") && tick < _movementBuffUntil
        ? BaseRuleValue("harbor.base.tidewalker_rapier") : 0;
    public int CastSpeedBonus(int tick) => HasBase("harbor.base.wavebreaker_gloves") && tick < _movementBuffUntil
        ? BaseRuleValue("harbor.base.wavebreaker_gloves") : 0;
    public bool IsImmune(Ailment kind) => HasBase("harbor.base.tidewading_boots") && kind is Ailment.Bleed or Ailment.Ignite;
    public int MovementBonus(int tick) => (tick < _blackTideUntil ? _blackTide * 2_000 : 0) +
        (HasBase("harbor.base.downstream_quiver") && tick < _quiverMovementUntil
            ? BaseRuleValue("harbor.base.downstream_quiver") : 0);
    public int ArmorIncrease(int nearbyEnemies) => (Has("无尽行军") ? Math.Min(10, _marchTicks / 20) * 800 : 0) +
        (Has("统帅之负") ? Math.Min(5, nearbyEnemies) * 1_500 : 0);
    public int SpiritBarrier(CharacterSheet sheet, int tick) => Scale(sheet.SpiritBarrier().Value, tick < _seaUntil ? 20_000 : 10_000);
    public Func<int, bool, int>? RedirectDamage { get; set; }
    public Func<bool>? CompanionAlive { get; set; }
    public int LastEnemyShieldLoss { get; private set; }
    public bool LastEnemyHitBrokeShield { get; private set; }
    public Func<int, bool, int, int>? AbsorbEnemyDamage { get; set; }
    public Action<ResourceState, EnemyDamageResult>? EnemyDamageApplied { get; set; }

    public int ApplyEnemyDamage(ResourceState hero, int damage, bool hit, int tick, VirtueViceState? virtues, bool blocked = false)
    {
        LastEnemyShieldLoss = 0;
        LastEnemyHitBrokeShield = false;
        if (!hero.IsAlive || damage <= 0) return 0;
        damage = AbsorbEnemyDamage?.Invoke(damage, hit, tick) ?? damage;
        damage = RedirectDamage?.Invoke(damage, hit) ?? damage;
        if (hit && HasBase("harbor.base.anchored_belt") && hero.Life * 100L >= hero.MaximumLife * 90L)
            damage = Scale(damage, 10_000 - BaseRuleValue("harbor.base.anchored_belt"));
        if (damage <= 0) return 0;
        if (hit && Has("最后一舱") && tick >= _lastHoldReady && damage >= hero.Life + hero.Shield &&
            virtues?.Consume(VirtueViceKind.Mercy, 2) == 2)
        {
            damage = Math.Max(0, hero.Life + hero.Shield - 1);
            _lastHoldReady = tick + 400;
        }
        int shieldBefore = hero.Shield;
        int lifeBefore = hero.Life;
        int previousDamageTick = hero.LastDamageTick;
        int generation = hero.HarmfulStatus.Generation;
        int actual = hero.ApplyEnemyDamage(damage, hit, tick);
        if (hit && Has("无眠领航者")) hero.PreserveShieldRecharge(previousDamageTick);
        bool shieldBroken = shieldBefore > 0 && hero.Shield == 0;
        LastEnemyShieldLoss = Math.Max(0, shieldBefore - hero.Shield);
        LastEnemyHitBrokeShield = hit && shieldBroken;
        bool lifeLost = !hit && lifeBefore > hero.Life;
        if (hero.IsAlive && generation == hero.HarmfulStatus.Generation)
            EnemyDamageApplied?.Invoke(hero, new(actual, LastEnemyShieldLoss, hit, blocked, shieldBroken, tick));
        DamageTaken(actual, hit && !blocked, tick, virtues);
        if (Has("静海双壁") && tick >= _seaReady && (shieldBroken || lifeLost))
        {
            _seaReady = tick + 120;
            if (shieldBroken) _seaUntil = tick + 120;
            else if (hero.IsAlive) hero.RestoreShield(hero.MaximumShield);
        }
        return actual;
    }

    public int MitigateDamageOverTime(CharacterSheet sheet, int raw, EnemyDamageType type, int tick, int pulsesPerSecond)
    {
        int resistance = type switch
        {
            EnemyDamageType.Physical => sheet.CappedPhysicalResistance(Value(ItemModifierKind.PhysicalResistanceBasisPoints)),
            EnemyDamageType.Fire => sheet.CappedResistance(sheet.FireResistanceBasisPoints, type),
            EnemyDamageType.Cold => sheet.CappedResistance(sheet.ColdResistanceBasisPoints, type),
            EnemyDamageType.Lightning => sheet.CappedResistance(sheet.LightningResistanceBasisPoints, type),
            _ => sheet.CappedResistance(sheet.VoidResistanceBasisPoints, type),
        };
        int damage = CombatRules.MitigateByResistance(raw,
            CombatRules.EffectiveResistance(resistance, sheet.ResistanceMaximum(type)));
        if (type == EnemyDamageType.Physical)
            damage = Scale(damage, 10_000 - CombatRules.PhysicalDotArmorReduction(sheet.Armor().Value, Scale(raw, pulsesPerSecond * 10_000)));
        damage = Scale(damage, 10_000 - CombatRules.SpiritBarrierReduction(SpiritBarrier(sheet, tick), Scale(damage, pulsesPerSecond * 10_000)));
        return Scale(damage, IncomingMultiplier(sheet, type, false, tick));
    }

    public void Advance(int tick, ResourceState hero, bool moved, VirtueViceState? virtues = null)
    {
        _wasMoving = moved;
        if (moved) _movementBuffUntil = tick + 80;
        if (moved) { _marchTicks++; _stationaryTicks = 0; }
        else if (++_stationaryTicks >= 40) _marchTicks = 0;
        if (Has("逆潮之锋"))
        {
            if (moved)
            {
                if (++_reverseTideTicks >= 20) { _reverseTideTicks = 0; _reverseTideStacks = Math.Min(3, _reverseTideStacks + 1); }
            }
            else _reverseTideTicks = 0;
        }
        if (tick >= _blackTideUntil) _blackTide = 0;
        if (tick > 0 && tick % 100 == 0 && HasBase("harbor.base.ballast_plate"))
            virtues?.Gain(VirtueViceKind.Mercy);
        while (_tidalMana.Count > 0 && _tidalMana.Peek().Tick <= tick - 100) _tidalMana.Dequeue();
        if (tick % 20 == 0 && Has("复生之种") && hero.Life * 100L < hero.MaximumLife * 35L)
            hero.HealLife(hero.MaximumLife * 400 / 10_000);
    }
    public void UsedMovementSkill(int tick) { if (Has("界行罗盘")) _compassUntil = tick + 60; }
    public void BeginTick(int tick) { _currentTick = tick; }

    public ResolvedSkill Resolve(ResolvedSkill skill)
    {
        bool projectile = SkillDefinitions.Get(skill.SkillId).Tags.HasFlag(SkillTag.Projectile);
        int cost = Scale(Has("怒节同契") ? 12_000 : 10_000, ExternalSkillCostMultiplier);
        return skill with
        {
            ManaCost = Scale(skill.ManaCost, cost),
            LifeCost = Scale(skill.LifeCost, cost),
            RangeRaw = Scale(skill.RangeRaw, 10_000 + Value(ItemModifierKind.SkillRangeBasisPoints)),
            AreaIncreasedBasisPoints = skill.AreaIncreasedBasisPoints +
                (SkillDefinitions.Get(skill.SkillId).Tags.HasFlag(SkillTag.Area) ? Value(ItemModifierKind.SkillAreaBasisPoints) : 0),
            CooldownTicks = skill.CooldownTicks <= 0 ? 0 : Math.Max(1, Scale(skill.CooldownTicks,
                100_000_000 / Math.Max(1, 10_000 + Value(ItemModifierKind.IncreasedCooldownRecoveryBasisPoints)))),
            ProjectileCount = projectile ? skill.ProjectileCount + Value(ItemModifierKind.AdditionalProjectile) : skill.ProjectileCount,
            MaximumChains = projectile ? skill.MaximumChains + Value(ItemModifierKind.AdditionalChain) + (Has("鸦群答卷") ? 2 : 0) : skill.MaximumChains,
            PierceCount = projectile ? skill.PierceCount + Value(ItemModifierKind.AdditionalPierce) : skill.PierceCount,
            ProjectileSpeedRawPerSecond = Scale(skill.ProjectileSpeedRawPerSecond, 10_000 + Value(ItemModifierKind.ProjectileSpeedBasisPoints)),
            Returns = skill.Returns || projectile && (Has("鸦群答卷") || Value(ItemModifierKind.ReturnProjectiles) > 0),
        };
    }

    public void BeginAction(string skillId, int lifeCost, int manaCost, bool triggered, VirtueViceState? virtues)
    {
        _action = $"equipment-action:{++_actionSequence}";
        _actionTriggers = [];
        _projectilePrimaryTarget = "";
        _triggered = triggered;
        _copy = false;
        _lifePaid = lifeCost > 0;
        if (!triggered && manaCost > 0 && HasBase("harbor.base.tidal_wand")) _tidalMana.Enqueue((_currentTick, manaCost));
        SkillTag tags = SkillDefinitions.Get(skillId).Tags;
        _actionFallingStar = !triggered && tags.HasFlag(SkillTag.Spell) && _fallingStar;
        if (_actionFallingStar) _fallingStar = false;
        if (lifeCost + manaCost > 0) Gain("节制之印", VirtueViceKind.Temperance, virtues);
    }
    public int ExtraActionChains => _actionFallingStar ? 3 : 0;
    public EquipmentOffenseSnapshot SnapshotOffense(ResourceState hero) =>
        new(hero.Life == hero.MaximumLife, CompanionAlive?.Invoke() == true, _lifePaid, _actionFallingStar, _rekindles);

    public int HitMultiplier(TeamBuild build, ResourceState hero, SkillTag tags, string enemyId,
        int enemyLife, int enemyMaximumLife, bool rareOrBoss, bool boss, bool bleeding,
        int distanceRaw, int nearbyEnemies, int tick, int chainIndex = 0, EquipmentOffenseSnapshot? snapshot = null)
    {
        int multiplier = 10_000;
        void More(int value) => multiplier = Scale(multiplier, 10_000 + value);
        if (HasBase("harbor.base.tidal_wand"))
            multiplier = Scale(multiplier, checked(10_000 + TidalManaBonus()));
        if (HasBase("harbor.base.returning_tide_shield") && tick < _returningShieldUntil &&
            (tags.HasFlag(SkillTag.Attack) || tags.HasFlag(SkillTag.Spell)))
            multiplier = Scale(multiplier, 10_000 + BaseRuleValue("harbor.base.returning_tide_shield"));
        if (HasBase("harbor.base.cablecleaver_axe") && tags.HasFlag(SkillTag.Attack) && enemyLife * 2L <= enemyMaximumLife)
            More(BaseRuleValue("harbor.base.cablecleaver_axe"));
        if (HasBase("harbor.base.sunken_anchor_maul") && tags.HasFlag(SkillTag.Melee) && distanceRaw <= 2_000)
            More(BaseRuleValue("harbor.base.sunken_anchor_maul"));
        if (HasBase("harbor.base.tideskimmer_bow") && tags.HasFlag(SkillTag.Attack) && tags.HasFlag(SkillTag.Projectile) && tick < _movementBuffUntil)
            More(BaseRuleValue("harbor.base.tideskimmer_bow"));
        if (Has("铁月") && tags.HasFlag(SkillTag.Slam) && (snapshot?.FullLife ?? hero.Life == hero.MaximumLife)) More(7_000);
        if (Has("共生兽印") && (snapshot?.CompanionAlive ?? CompanionAlive?.Invoke() == true)) More(3_000);
        if (Has("裂渊獠牙") && tags.HasFlag(SkillTag.Melee) && rareOrBoss) More(5_500);
        if (Has("逆潮之锋") && tags.HasFlag(SkillTag.Melee) && _reverseTideStacks > 0 &&
            !_triggered && _actionTriggers.Add("reverse-tide-consume"))
        {
            More(_reverseTideStacks * 1_750);
            _reverseTideStacks = 0;
        }
        if (Has("灯塔守望") && tags.HasFlag(SkillTag.Projectile))
            More(distanceRaw >= 6_000 ? 3_000 : distanceRaw < 3_000 ? -2_000 : 0);
        if (Has("双潮织手"))
        {
            if (tags.HasFlag(SkillTag.Spell) && tick < _attackBuffUntil) More(2_500);
            if (tags.HasFlag(SkillTag.Attack) && tick < _spellBuffUntil) More(2_500);
        }
        if (Has("复生之种") && (snapshot?.FullLife ?? hero.Life == hero.MaximumLife)) More(2_500);
        if (Has("行刑者之偿") && bleeding && enemyLife * 5L < enemyMaximumLife) More(10_000);
        if (Has("血税契据") && (snapshot?.LifePaid ?? _lifePaid)) More(6_000);
        if (Has("凝滞一刻") && boss && !_firstBossHits.Contains(enemyId)) More(4_000);
        if (Has("统帅之负") && nearbyEnemies == 1) More(4_500);
        if (Has("琉璃地平线") && tags.HasFlag(SkillTag.Attack) && distanceRaw >= 6_000) More(3_500);
        if (Has("深层回音") && tags.HasFlag(SkillTag.Projectile)) More(Math.Min(4, chainIndex) * 1_200);
        if ((snapshot?.FallingStar ?? _actionFallingStar) && tags.HasFlag(SkillTag.Spell)) More(3_500);
        if (tick < _debuffUntil.GetValueOrDefault(enemyId)) More(3_000);
        if (Has("虚空天平") && EqualResistances(build.Sheet)) More(3_500);
        if (Has("沉默铁砧") && tags.HasFlag(SkillTag.Attack))
        {
            int frequency = CombatRules.AttackFrequencyMilliPerSecond(build.Weapon.AttacksPerSecondMilli,
                build.IncreasedAttackSpeedBasisPoints + build.IncreasedActionSpeedBasisPoints);
            More(Math.Clamp((1_500 - frequency) / 100, 0, 10) * 800);
        }
        if ((snapshot?.Rekindles ?? _rekindles) > 0) More((snapshot?.Rekindles ?? _rekindles) * 3_000);
        return multiplier;
    }
    public int BaseCriticalBonus(SkillTag tags, int distanceRaw) => Has("琉璃地平线") && tags.HasFlag(SkillTag.Attack)
        ? Math.Min(6, distanceRaw / 2_000) * 100 : 0;

    public int OnHit(ResourceState hero, SkillTag tags, string enemyId, bool boss, bool critical, int damage,
        VirtueViceState? virtues, int tick = 0)
    {
        if (damage <= 0) return 0;
        bool first = boss && Has("凝滞一刻") && _firstBossHits.Add(enemyId);
        if (_copy) return 0;
        if (!_triggered)
        {
            if (tags.HasFlag(SkillTag.Attack)) _attackBuffUntil = tick + 80;
            if (tags.HasFlag(SkillTag.Spell)) _spellBuffUntil = tick + 80;
            if (HasBase("harbor.base.downstream_quiver") && tags.HasFlag(SkillTag.Attack) && tags.HasFlag(SkillTag.Projectile)) _quiverMovementUntil = tick + 80;
        }
        if (tags.HasFlag(SkillTag.Attack)) _hunt = false;
        if (critical) Gain("傲慢之印", VirtueViceKind.Arrogance, virtues);
        if (!_triggered && critical && tags.HasFlag(SkillTag.Spell) && Has("坠星透镜") && _actionTriggers.Add("falling-star")) _fallingStar = true;
        if (tags.HasFlag(SkillTag.Projectile)) Gain("懒惰之印", VirtueViceKind.Sloth, virtues);
        hero.HealLife(Math.Max(0, Value(ItemModifierKind.LifeOnHit)));
        hero.RestoreMana(Math.Max(0, Value(ItemModifierKind.ManaOnHit)));
        hero.RestoreShield(Math.Max(0, Value(ItemModifierKind.ShieldOnHit)));
        hero.AddLifeLeech(Scale(damage, Value(ItemModifierKind.LifeLeechBasisPoints)));
        hero.AddManaLeech(Scale(damage, Value(ItemModifierKind.ManaLeechBasisPoints)));
        hero.AddShieldLeech(Scale(damage, Value(ItemModifierKind.ShieldLeechBasisPoints)));
        return first ? 20 : 0;
    }
    public void Warcry(int tick, IEnumerable<string> targets, VirtueViceState? virtues)
    {
        Gain("暴怒之印", VirtueViceKind.Rage, virtues);
        if (Has("葬钟")) foreach (string id in targets) _debuffUntil[id] = tick + 120;
    }
    public void FlaskUsed(VirtueViceState? virtues) => Gain("慈悲之印", VirtueViceKind.Mercy, virtues, perAction: false);
    public void Evaded() { if (Has("猎手蚀影")) _hunt = true; }
    public void Blocked(int tick, bool spell)
    {
        if (HasBase("harbor.base.returning_tide_shield")) _returningShieldUntil = tick + 80;
        if (!spell && Has("空洞守卫") && tick >= _suppressionReady) { _suppressionUntil = tick + 40; _suppressionReady = tick + 60; }
    }
    public void Suppressed(int tick, ResourceState hero)
    {
        if (Has("无星祷衣") && tick >= _shieldHealReady) { hero.RestoreShield(hero.MaximumShield * 800 / 10_000); _shieldHealReady = tick + 20; }
    }
    public int IncomingMultiplier(CharacterSheet sheet, EnemyDamageType type, bool hit, int tick)
    {
        int result = 10_000;
        if (hit && Has("终夜守望")) result = Scale(result, 10_000 - _bulwark * 500);
        if (tick < _barkUntil) result = Scale(result, 8_000);
        if (hit && tick < _compassUntil) result = Scale(result, 8_500);
        if (Has("虚空天平") && !EqualResistances(sheet) && type is EnemyDamageType.Fire or EnemyDamageType.Cold or EnemyDamageType.Lightning) result = Scale(result, 8_800);
        if (Has("不归航迹")) result = Scale(result, !hit && _wasMoving ? 7_000 : hit && !_wasMoving ? 11_000 : 10_000);
        return Scale(result, 10_000 - _rekindles * 1_500);
    }

    public void DamageTaken(int damage, bool hit, int tick, VirtueViceState? virtues)
    {
        if (damage <= 0) return;
        Gain("谦逊足印", VirtueViceKind.Humility, virtues, perAction: false, chance: 1_500);
        if (!hit) return;
        if (Has("终夜守望")) _bulwark = Math.Min(5, _bulwark + 1);
        if (Has("荆生树皮")) _barkUntil = tick + 40;
    }
    public bool TryRekindle(ResourceState hero)
    {
        if (hero.IsAlive || !Has("灰烬之心") || _rekindles >= 2) return false;
        _rekindles++;
        hero.SetLifeAndShield(_rekindles == 1 ? 7_500 : 5_000);
        return true;
    }
    public void Killed(EnemyRarity rarity, int tick)
    {
        if (Has("黑潮披挂")) { _blackTide = Math.Min(3, _blackTide + 1); _blackTideUntil = tick + 80; }
        if (Has("折断军旗") && rarity == EnemyRarity.Rare) _bannerUntil = tick + 160;
    }
    private void Gain(string enchantment, VirtueViceKind kind, VirtueViceState? state,
        bool perAction = true, int chance = 1_000)
    {
        int count = Loadout.EnchantmentCount(enchantment);
        if (state is null || count <= 0 || perAction && (_triggered || !_actionTriggers.Add(enchantment))) return;
        if (_random.NextBasisPoints() < Math.Min(10_000, chance * count)) state.Gain(kind);
    }
    private static bool EqualResistances(CharacterSheet s) => s.FireResistanceBasisPoints == s.ColdResistanceBasisPoints &&
        s.ColdResistanceBasisPoints == s.LightningResistanceBasisPoints && s.LightningResistanceBasisPoints == s.VoidResistanceBasisPoints;
    private int TidalManaBonus()
    {
        long total = _tidalMana.Sum(entry => (long)entry.Mana / 10 * BaseRuleValue("harbor.base.tidal_wand"));
        return (int)Math.Min(int.MaxValue - 10_000L, Math.Max(0, total));
    }
    private static int Scale(int value, int multiplier) => (int)Math.Clamp((long)value * multiplier / 10_000, 0, int.MaxValue);
}
