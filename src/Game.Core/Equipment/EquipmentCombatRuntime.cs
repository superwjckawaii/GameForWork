using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.SkillCatalog;
using GameForWork.Core.Builds;
using GameForWork.Core.Skills;
using GameForWork.Core.Simulation;

namespace GameForWork.Core.Equipment;

public sealed record EquipmentActionContext(string Id, bool LifePaid, bool FallingStar, bool Triggered,
    HashSet<string> Triggers, string ProjectilePrimaryTarget = "");

/// <summary>One battle's mutable equipment state. No state is shared between simulations.</summary>
public sealed class EquipmentCombatRuntime(EquipmentCombatLoadout loadout, ulong seed)
{
    private readonly Pcg32 _random = new(seed ^ 0x65717569706d656eUL);
    private readonly HashSet<string> _firstBossHits = [];
    private HashSet<string> _actionTriggers = [];
    private readonly Dictionary<string, int> _debuffUntil = [];
    private int _actionSequence, _blackTide, _blackTideUntil, _marchTicks, _stationaryTicks;
    private int _barkUntil, _compassUntil, _bannerUntil, _suppressionUntil, _suppressionReady, _shieldHealReady;
    private int _bulwark, _rekindles;
    private int _seaReady, _seaUntil;
    private string _cohuntTarget = "";
    private string _projectilePrimaryTarget = "";
    private int _cohuntLayers;
    private bool _hunt, _fallingStar, _actionFallingStar, _lifePaid;
    private string _action = "";
    private bool _triggered;
    public EquipmentActionContext CaptureAction() => new(_action, _lifePaid, _actionFallingStar, _triggered, _actionTriggers, _projectilePrimaryTarget);
    public EquipmentActionContext CreateTriggeredAction(string primaryTarget) =>
        new($"equipment-action:{++_actionSequence}", false, false, true, [], primaryTarget);
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
        }
    }
    public EquipmentCombatLoadout Loadout { get; } = loadout;
    public int Rekindles => _rekindles;
    public int ExternalSkillCostMultiplier { get; set; } = 10_000;
    public Func<int>? NearbyEnemyCount { get; set; }
    public bool Has(string name) => Loadout.Has(name);
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
    public int MovementBonus(int tick) => tick < _blackTideUntil ? _blackTide * 2_000 : 0;
    public int ArmorIncrease(int nearbyEnemies) => (Has("无尽行军") ? Math.Min(10, _marchTicks / 20) * 800 : 0) +
        (Has("统帅之负") ? Math.Min(5, nearbyEnemies) * 1_500 : 0);
    public int SpiritBarrier(CharacterSheet sheet, int tick) => Scale(sheet.SpiritBarrier().Value, tick < _seaUntil ? 20_000 : 10_000);
    public Func<int, int>? RedirectDamage { get; set; }
    public Func<bool>? CompanionAlive { get; set; }

    public int ApplyEnemyDamage(ResourceState hero, int damage, bool hit, int tick, VirtueViceState? virtues, bool blocked = false)
    {
        if (!hero.IsAlive || damage <= 0) return 0;
        damage = RedirectDamage?.Invoke(damage) ?? damage;
        int shieldBefore = hero.Shield;
        int actual = Math.Min(damage, hero.Life + hero.Shield);
        bool shieldBroken = shieldBefore > 0 && damage >= shieldBefore;
        bool lifeLost = !hit && shieldBefore == 0 && actual > 0;
        hero.ApplyDamage(damage, tick);
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
            EnemyDamageType.Fire => sheet.FireResistanceBasisPoints,
            EnemyDamageType.Cold => sheet.ColdResistanceBasisPoints,
            EnemyDamageType.Lightning => sheet.LightningResistanceBasisPoints,
            _ => sheet.VoidResistanceBasisPoints,
        };
        int damage = CombatRules.MitigateByResistance(raw,
            CombatRules.EffectiveResistance(resistance, sheet.ResistanceMaximum(type)));
        if (type == EnemyDamageType.Physical)
            damage = Scale(damage, 10_000 - CombatRules.PhysicalDotArmorReduction(sheet.Armor().Value, Scale(raw, pulsesPerSecond * 10_000)));
        damage = Scale(damage, 10_000 - CombatRules.SpiritBarrierReduction(SpiritBarrier(sheet, tick), Scale(damage, pulsesPerSecond * 10_000)));
        return Scale(damage, IncomingMultiplier(sheet, type, false, tick));
    }

    public void Advance(int tick, ResourceState hero, bool moved)
    {
        if (moved) { _marchTicks++; _stationaryTicks = 0; }
        else if (++_stationaryTicks >= 40) _marchTicks = 0;
        if (tick >= _blackTideUntil) _blackTide = 0;
        if (tick % 20 == 0 && Has("复生之种") && hero.Life * 100L < hero.MaximumLife * 35L)
            hero.HealLife(hero.MaximumLife * 400 / 10_000);
    }
    public void UsedMovementSkill(int tick) { if (Has("界行罗盘")) _compassUntil = tick + 60; }

    public ResolvedSkill Resolve(ResolvedSkill skill)
    {
        bool projectile = SkillDefinitions.Get(skill.SkillId).Tags.HasFlag(SkillTag.Projectile);
        int cost = Scale(Has("怒节同契") ? 12_000 : 10_000, ExternalSkillCostMultiplier);
        return skill with
        {
            ManaCost = Scale(skill.ManaCost, cost), LifeCost = Scale(skill.LifeCost, cost),
            RangeRaw = Scale(skill.RangeRaw, 10_000 + Value(ItemModifierKind.SkillRangeBasisPoints) +
                (SkillDefinitions.Get(skill.SkillId).Tags.HasFlag(SkillTag.Area) ? Value(ItemModifierKind.SkillAreaBasisPoints) : 0)),
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
        _lifePaid = lifeCost > 0;
        SkillTag tags = SkillDefinitions.Get(skillId).Tags;
        _actionFallingStar = !triggered && tags.HasFlag(SkillTag.Spell) && _fallingStar;
        if (_actionFallingStar) _fallingStar = false;
        if (lifeCost + manaCost > 0) Gain("节制之印", VirtueViceKind.Temperance, virtues);
    }
    public int ExtraActionChains => _actionFallingStar ? 3 : 0;

    public int HitMultiplier(TeamBuild build, ResourceState hero, SkillTag tags, string enemyId,
        int enemyLife, int enemyMaximumLife, bool rareOrBoss, bool boss, bool bleeding,
        int distanceRaw, int nearbyEnemies, int tick, int chainIndex = 0)
    {
        int multiplier = 10_000;
        void More(int value) => multiplier = Scale(multiplier, 10_000 + value);
        if (Has("铁月") && tags.HasFlag(SkillTag.Slam) && hero.Life == hero.MaximumLife) More(7_000);
        if (Has("共生兽印") && CompanionAlive?.Invoke() == true) More(3_000);
        if (Has("裂渊獠牙") && tags.HasFlag(SkillTag.Melee) && rareOrBoss) More(5_500);
        if (Has("复生之种") && hero.Life == hero.MaximumLife) More(2_500);
        if (Has("行刑者之偿") && bleeding && enemyLife * 5L < enemyMaximumLife) More(10_000);
        if (Has("血税契据") && _lifePaid) More(6_000);
        if (Has("凝滞一刻") && boss && !_firstBossHits.Contains(enemyId)) More(4_000);
        if (Has("统帅之负") && nearbyEnemies == 1) More(4_500);
        if (Has("琉璃地平线") && tags.HasFlag(SkillTag.Attack) && distanceRaw >= 6_000) More(3_500);
        if (Has("深层回音") && tags.HasFlag(SkillTag.Projectile)) More(Math.Min(4, chainIndex) * 1_200);
        if (_actionFallingStar && tags.HasFlag(SkillTag.Spell)) More(3_500);
        if (tick < _debuffUntil.GetValueOrDefault(enemyId)) More(3_000);
        if (Has("虚空天平") && EqualResistances(build.Sheet)) More(3_500);
        if (Has("沉默铁砧") && tags.HasFlag(SkillTag.Attack))
        {
            int frequency = CombatRules.AttackFrequencyMilliPerSecond(build.Weapon.AttacksPerSecondMilli,
                build.IncreasedAttackSpeedBasisPoints + build.IncreasedActionSpeedBasisPoints);
            More(Math.Clamp((1_500 - frequency) / 100, 0, 10) * 800);
        }
        if (_rekindles > 0) More(_rekindles * 3_000);
        return multiplier;
    }
    public int BaseCriticalBonus(SkillTag tags, int distanceRaw) => Has("琉璃地平线") && tags.HasFlag(SkillTag.Attack)
        ? Math.Min(6, distanceRaw / 2_000) * 100 : 0;

    public int OnHit(ResourceState hero, SkillTag tags, string enemyId, bool boss, bool critical, int damage,
        VirtueViceState? virtues)
    {
        if (damage <= 0) return 0;
        bool first = boss && Has("凝滞一刻") && _firstBossHits.Add(enemyId);
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
        if (state is null || count <= 0 || perAction && !_actionTriggers.Add(enchantment)) return;
        if (_random.NextBasisPoints() < Math.Min(10_000, chance * count)) state.Gain(kind);
    }
    private static bool EqualResistances(CharacterSheet s) => s.FireResistanceBasisPoints == s.ColdResistanceBasisPoints &&
        s.ColdResistanceBasisPoints == s.LightningResistanceBasisPoints && s.LightningResistanceBasisPoints == s.VoidResistanceBasisPoints;
    private static int Scale(int value, int multiplier) => (int)Math.Clamp((long)value * multiplier / 10_000, 0, int.MaxValue);
}
