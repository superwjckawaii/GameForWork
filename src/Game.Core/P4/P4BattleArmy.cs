using GameForWork.Core.Equipment;
using GameForWork.Core.P1.Combat;
using GameForWork.Core.P1.Items;
using GameForWork.Core.P17;
using GameForWork.Core.P18;
using GameForWork.Core.P23;
using GameForWork.Core.P24;
using GameForWork.Core.P30;
using GameForWork.Core.Simulation;

namespace GameForWork.Core.P4;

public sealed partial class P4SpatialCombatRunner
{
    // P30_SKILL_BASE_DATA section 7. Unit attacks have their own offensive inputs.
    private sealed class BattleArmy
    {
        private const string Bone = "p24.skill.summon_boneguard", Bow = "p24.skill.summon_soulbow",
            Beast = "p24.skill.summon_spirit_beast", Turret = "p24.skill.forge_turret";
        private readonly List<ArmyUnit> _units = [];
        private readonly EquipmentCombatLoadout _equipment;
        private readonly P18CombatProfile _ascendancy;
        private int _sequence;
        private int _unusedMinionSlots;
        public BattleArmy(P4NodeCombatRequest request, IEnumerable<SkillConfiguration> skills, P4Point origin)
        {
            _equipment = request.Build.CombatEquipment ?? EquipmentCombatLoadout.Empty;
            _ascendancy = request.Build.Ascendancy ?? P18CombatProfile.Empty;
            SkillConfiguration[] stones = skills.Where(s => s.SkillId is Bone or Bow).ToArray();
            int maximum = P24CombatCaps.Clamp(P24CombatUnitKind.Minion,
                P231AscendancyRules.MaximumMinions(_equipment.Value(ItemModifierKind.AdditionalMinionMaximum) +
                    _equipment.Value(ItemModifierKind.AdditionalUnitMaximum), _ascendancy) +
                (stones.Any(s => Support(s, P24SupportMechanic.ExpandedArmy)) ? 1 : 0));
            if (_equipment.Has("末页王庭")) { _unusedMinionSlots = Math.Min(8, Math.Max(0, maximum - 1)); maximum = 1; }
            if (stones.Length > 0)
                for (int index = 0; index < maximum; index++) Spawn(stones[index % stones.Length], origin, 0);
            if (skills.FirstOrDefault(s => s.SkillId == Beast) is { } beast) Spawn(beast, origin, 0);
        }
        public static bool Automatic(string id) => id is Bone or Bow or Beast;
        public IReadOnlyList<P4AllyFrame> Frames() => _units.Where(u => u.Life > 0).Select(u =>
            new P4AllyFrame(u.Id, u.Position, u.Skill.SkillId is Bone or Beast, u.Skill.SkillId,
                u.Life, u.MaximumLife)).ToArray();
        public bool CanUse(string id) => !Automatic(id) && (id != "p24.skill.bone_harvest" ||
            _units.Any(u => u.Life > 0 && u.Kind == P24CombatUnitKind.Minion));

        public bool Execute(SkillConfiguration skill, P4Point origin, IReadOnlyCollection<P4EnemyUnit> enemies,
            Pcg32 random, int tick, ICollection<P4SpatialEvent> events)
        {
            if (skill.SkillId == Turret)
            {
                int maximum = P24CombatCaps.Clamp(P24CombatUnitKind.Construct, 3 +
                    _equipment.Value(ItemModifierKind.AdditionalConstructMaximum) + _equipment.Value(ItemModifierKind.AdditionalUnitMaximum));
                ArmyUnit[] existing = _units.Where(u => u.Kind == P24CombatUnitKind.Construct && u.Life > 0).ToArray();
                if (existing.Length >= maximum && existing.Length > 0) _units.Remove(existing[0]);
                if (maximum > 0) Spawn(skill, origin, tick);
                events.Add(Event(tick, P4SpatialEventKind.SkillEffect, "hero", "", 0, origin, origin, $"skill:{skill.SkillId}|deploy"));
                return true;
            }
            if (skill.SkillId == "p24.skill.bone_harvest")
            {
                int corpses = enemies.Count(e => e.Life <= 0 && !e.CorpseConsumed && InRange(origin, e.Position, 6_000));
                corpses = Math.Min(corpses, 5 + skill.Quality / 10);
                foreach (var corpse in enemies.Where(e => e.Life <= 0 && !e.CorpseConsumed && InRange(origin, e.Position, 6_000)).Take(corpses))
                    corpse.CorpseConsumed = true;
                int multiplier = (int)Math.Round(12_000 * Math.Pow(1.05, skill.Level - 1));
                multiplier = ScaleCombatValue(multiplier, 10_000 + corpses * 1_000);
                foreach (ArmyUnit unit in _units.Where(u => u.Life > 0 && u.Kind == P24CombatUnitKind.Minion))
                    if (SelectTarget(enemies, unit.Position) is { } target)
                        Attack(unit, target, random, tick, events, multiplier);
                return true;
            }
            return Automatic(skill.SkillId);
        }

        private void Spawn(SkillConfiguration skill, P4Point origin, int tick)
        {
            P24CombatUnitKind kind = skill.SkillId == Beast ? P24CombatUnitKind.Companion :
                skill.SkillId == Turret ? P24CombatUnitKind.Construct : P24CombatUnitKind.Minion;
            int life = skill.SkillId switch { Bone => 180, Bow => 100, Beast => 350, _ => 160 };
            life = (int)Math.Round(life * Math.Pow(1.065, skill.Level - 1));
            int increased = _equipment.Value(kind switch
            {
                P24CombatUnitKind.Minion => ItemModifierKind.IncreasedMinionLifeBasisPoints,
                P24CombatUnitKind.Companion => ItemModifierKind.IncreasedCompanionLifeBasisPoints,
                _ => ItemModifierKind.IncreasedConstructLifeBasisPoints,
            }) + (skill.SkillId == Bone ? skill.Quality * 100 : 0);
            life = ScaleCombatValue(life, 10_000 + increased);
            if (kind == P24CombatUnitKind.Minion) life = ScaleCombatValue(life, 10_000 + _unusedMinionSlots * 3_000);
            if (Support(skill, P24SupportMechanic.MinionAmplify)) life = ScaleCombatValue(life, 8_500);
            if (kind == P24CombatUnitKind.Companion) life += _equipment.Value(ItemModifierKind.FlatCompanionMaximumLife);
            _units.Add(new($"army:{++_sequence}", kind, skill, Math.Max(1, life),
                origin with { XRaw = Math.Clamp(origin.XRaw + (_sequence % 5 - 2) * 350, 350, 11_650) }, tick));
        }

        public void Advance(IReadOnlyCollection<P4EnemyUnit> enemies, Pcg32 random, int tick,
            ICollection<P4SpatialEvent> events)
        {
            foreach (ArmyUnit unit in _units)
            {
                if (unit.Life <= 0)
                {
                    if (unit.Kind != P24CombatUnitKind.Companion || tick < unit.ReviveAt) continue;
                    unit.Life = Math.Max(1, unit.MaximumLife / 2);
                    events.Add(Event(tick, P4SpatialEventKind.SkillEffect, unit.Id, unit.Id, unit.Life,
                        unit.Position, unit.Position, "companion:revive"));
                }
                var candidates = enemies.Where(e => e.Life > 0);
                P4EnemyUnit? target = unit.Skill.SkillId == Bow ? candidates.OrderBy(e => (double)e.Life / e.MaximumLife)
                    .ThenBy(e => P4Point.DistanceSquared(unit.Position, e.Position)).FirstOrDefault() :
                    candidates.OrderByDescending(e => unit.Kind == P24CombatUnitKind.Construct &&
                        P231AscendancyRules.ConstructPrioritizes(e.Rarity, _ascendancy))
                        .ThenBy(e => P4Point.DistanceSquared(unit.Position, e.Position)).FirstOrDefault();
                if (target is null) continue;
                int range = unit.Skill.SkillId == Bow ? 9_000 : unit.Skill.SkillId == Turret ? 10_000 : 1_700;
                int speed = 4_000;
                if (unit.Kind == P24CombatUnitKind.Minion) speed = ScaleCombatValue(speed, 10_000 +
                    _equipment.Value(ItemModifierKind.IncreasedMinionSpeedBasisPoints) +
                    (Support(unit.Skill, P24SupportMechanic.SwiftMinions) ? 3_000 : 0));
                if (unit.Kind != P24CombatUnitKind.Construct && !InRange(unit.Position, target.Position, range))
                    unit.Position = P4Point.MoveToward(unit.Position, target.Position, speed / 20);
                if (tick < unit.ReadyAt || !InRange(unit.Position, target.Position, range)) continue;
                bool bash = unit.Skill.SkillId == Bone && tick >= unit.BashAt;
                Attack(unit, target, random, tick, events, bash ? 18_000 : 10_000);
                if (bash) { unit.BashAt = tick + 80; target.ArmyTauntId = unit.Id; target.ArmyTauntUntil = tick + 60; }
                int frequency = unit.Skill.SkillId switch { Bone => 1_100, Bow => 1_250, Beast => 1_200, _ => 1_050 };
                int bonus = unit.Kind == P24CombatUnitKind.Minion ? _equipment.Value(ItemModifierKind.IncreasedMinionSpeedBasisPoints) : 0;
                if (Support(unit.Skill, P24SupportMechanic.SwiftMinions)) bonus += 3_000;
                if (unit.Skill.SkillId == Turret) bonus += unit.Skill.Quality * 100;
                frequency = Math.Max(1, ScaleCombatValue(frequency, 10_000 + bonus));
                unit.ReadyAt = tick + Math.Max(1, (20_000 + frequency - 1) / frequency);
            }
        }

        private void Attack(ArmyUnit unit, P4EnemyUnit enemy, Pcg32 random, int tick,
            ICollection<P4SpatialEvent> events, int multiplier)
        {
            int accuracy = (unit.Skill.SkillId switch { Bone => 180, Bow => 220, Beast => 250, _ => 240 }) + (unit.Skill.Level - 1) * 25;
            if (random.NextBasisPoints() >= DamageRules.HitChance(accuracy, enemy.Scaled.Evasion, false).Value) return;
            (int min, int max) = unit.Skill.SkillId switch { Bone => (12, 18), Bow => (10, 28), Beast => (24, 36), _ => (16, 24) };
            double growth = Math.Pow(1.06, unit.Skill.Level - 1);
            min = (int)Math.Round(min * growth); max = (int)Math.Round(max * growth);
            int raw = min + (int)(random.NextUInt() % (uint)(max - min + 1));
            int increased = _equipment.Value(unit.Kind switch
            {
                P24CombatUnitKind.Minion => ItemModifierKind.IncreasedMinionDamageBasisPoints,
                P24CombatUnitKind.Companion => ItemModifierKind.IncreasedCompanionDamageBasisPoints,
                _ => ItemModifierKind.IncreasedConstructDamageBasisPoints,
            });
            if (unit.Kind == P24CombatUnitKind.Minion) increased += P231AscendancyRules.IncreasedMinionDamageBasisPoints(
                _units.Count(u => u.Life > 0 && u.Kind == unit.Kind), _ascendancy);
            raw = ScaleCombatValue(raw, 10_000 + increased);
            if (unit.Kind == P24CombatUnitKind.Minion) raw = ScaleCombatValue(raw, 10_000 + _unusedMinionSlots * 3_000);
            if (Support(unit.Skill, P24SupportMechanic.MinionAmplify) ||
                Support(unit.Skill, P24SupportMechanic.ConstructAmplify)) raw = ScaleCombatValue(raw, 13_000);
            if (Support(unit.Skill, P24SupportMechanic.ExpandedArmy)) raw = ScaleCombatValue(raw, 8_500);
            raw = ScaleCombatValue(raw, multiplier);
            if (unit.Kind == P24CombatUnitKind.Construct) raw = ScaleCombatValue(raw, P231AscendancyRules.ConstructDamageMultiplier(enemy.Rarity, _ascendancy));
            if (random.NextBasisPoints() < (unit.Kind == P24CombatUnitKind.Companion ? 600 : 500)) raw = ScaleCombatValue(raw, 15_000);
            P17DamageType type = unit.Skill.SkillId == Bow ? P17DamageType.Void : P17DamageType.Physical;
            int damage = P17DamageRules.Resolve(raw, type, SkillSupport.None, enemy.Scaled.Armor,
                enemy.Scaled.FireResistanceBasisPoints, enemy.Scaled.ColdResistanceBasisPoints,
                enemy.Scaled.LightningResistanceBasisPoints, enemy.Scaled.VoidResistanceBasisPoints).Total;
            damage = Math.Min(enemy.Life, damage);
            enemy.Life -= damage;
            events.Add(Event(tick, P4SpatialEventKind.SkillEffect, unit.Id, enemy.EntityId, damage,
                unit.Position, enemy.Position, $"skill:{unit.Skill.SkillId}|unit:{unit.Kind}"));
            if (enemy.Life == 0) events.Add(Event(tick, P4SpatialEventKind.EnemyDefeated, unit.Id,
                enemy.EntityId, 0, unit.Position, enemy.Position, enemy.Profile.StableId));
        }

        // Single target attacks may target a closer unit, or an active taunting boneguard.
        public bool ReceiveEnemyAction(P4EnemyUnit enemy, EnemySkillProfile skill, P4Point hero,
            P4NodeCombatRequest request, Pcg32 random, int tick, ICollection<P4SpatialEvent> events)
        {
            if (skill.Area || skill.Kind is not (EnemySkillKind.BasicStrike or EnemySkillKind.ArcaneBolt or EnemySkillKind.Volley)) return false;
            ArmyUnit? unit = _units.Where(u => u.Life > 0).OrderByDescending(u =>
                tick < enemy.ArmyTauntUntil && u.Id == enemy.ArmyTauntId)
                .ThenBy(u => P4Point.DistanceSquared(u.Position, enemy.Position)).FirstOrDefault();
            if (unit is null || !(tick < enemy.ArmyTauntUntil && unit.Id == enemy.ArmyTauntId) &&
                P4Point.DistanceSquared(unit.Position, enemy.Position) >= P4Point.DistanceSquared(hero, enemy.Position)) return false;
            int range = Math.Max(enemy.Profile.AttackRangeRaw, skill.RangeRaw);
            if (!InRange(enemy.Position, unit.Position, range))
                enemy.Position = P4Point.MoveToward(enemy.Position, unit.Position, Math.Max(1,
                    ScaleCombatValue(enemy.Profile.MovementSpeedRawPerSecond, request.EnemySpeedBasisPoints) / 20));
            if (tick < enemy.NextActionTick || !InRange(enemy.Position, unit.Position, range)) return true;
            int raw = enemy.Scaled.MinimumPhysicalDamage + (int)(random.NextUInt() % (uint)Math.Max(1,
                enemy.Scaled.MaximumPhysicalDamage - enemy.Scaled.MinimumPhysicalDamage + 1));
            raw = ScaleCombatValue(raw, skill.DamageMultiplierBasisPoints);
            raw = ScaleCombatValue(raw, request.EnemyDamageBasisPoints);
            bool blocked = unit.Skill.SkillId == Bone && random.NextBasisPoints() < (skill.IsSpell ? 1_500 : 3_000);
            int damage = blocked ? 0 : raw;
            unit.Life = Math.Max(0, unit.Life - damage);
            if (unit.Life == 0 && unit.Kind == P24CombatUnitKind.Companion)
                unit.ReviveAt = tick + Math.Max(1, (160 - Math.Min(20, unit.Skill.Quality) * 2) * 10_000 /
                    Math.Max(1, 10_000 + _equipment.Value(ItemModifierKind.IncreasedCompanionReviveRateBasisPoints)));
            events.Add(Event(tick, P4SpatialEventKind.EnemyAttack, enemy.EntityId, unit.Id, damage,
                enemy.Position, unit.Position, $"{skill.DisplayName}|unit:{unit.Kind}|{(blocked ? "block" : "hit")}"));
            int frequency = Math.Max(1, ScaleCombatValue(enemy.Scaled.AttacksPerSecondMilli, request.EnemySpeedBasisPoints));
            enemy.NextActionTick = tick + Math.Max(8, (20_000 + frequency - 1) / frequency);
            enemy.ActionSequence++;
            return true;
        }
        private static bool Support(SkillConfiguration skill, P24SupportMechanic support)
        {
            string id = P30SkillCatalog.SupportFor(support).StoneId;
            return skill.ExtendedSupports.Contains(support) || skill.ExtendedP30Supports.Contains(id) ||
                skill.ExtendedP30SupportLinks.Any(link => link.StoneId == id);
        }
        private sealed class ArmyUnit(string id, P24CombatUnitKind kind, SkillConfiguration skill, int life, P4Point position, int tick)
        {
            public string Id { get; } = id;
            public P24CombatUnitKind Kind { get; } = kind;
            public SkillConfiguration Skill { get; } = skill;
            public int MaximumLife { get; } = life;
            public int Life { get; set; } = life;
            public P4Point Position { get; set; } = position;
            public int ReadyAt { get; set; } = tick;
            public int BashAt { get; set; } = tick;
            public int ReviveAt { get; set; } = int.MaxValue;
        }
    }
}
