using static GameForWork.Core.Skills.LinkedSupportRules;
using GameForWork.Core.Equipment;
using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.SkillCatalog;
using GameForWork.Core.Ascendancies;
using GameForWork.Core.Characters;
using GameForWork.Core.Archetypes;
using GameForWork.Core.Builds;
using GameForWork.Core.Simulation;

namespace GameForWork.Core.Spatial;

public sealed partial class SpatialCombatRunner
{
    // Builds_SKILL_BASE_DATA section 7. Unit attacks have their own offensive inputs.
    private sealed class BattleArmy
    {
        private const string Bone = "archetypes.skill.summon_boneguard", Bow = "archetypes.skill.summon_soulbow",
            Beast = "archetypes.skill.summon_spirit_beast", Turret = "archetypes.skill.forge_turret";
        private readonly List<ArmyUnit> _units = [];
        private readonly EquipmentCombatLoadout _equipment;
        private readonly CombatProfile _ascendancy;
        private readonly Combat.AuraCombatProfile? _auras;
        private readonly NodeCombatRequest _request;
        private int _sequence;
        private int _unusedMinionSlots;
        private int _arrayUntil, _arrayPulse;
        private Point _heroPosition;
        private SkillConfiguration? _arraySkill;
        private readonly Combat.SharedLifePool _sharedPool = new();
        public bool CompanionAlive => _units.Any(unit => unit.Kind == CombatUnitKind.Companion && unit.Life > 0);

        public int RedirectDamage(int damage, bool hit, Point hero, int tick, ICollection<SpatialEvent> events)
        {
            ArmyUnit? companion = _units.FirstOrDefault(unit => unit.Kind == CombatUnitKind.Companion && unit.Life > 0);
            int remaining = damage;
            if (companion is not null)
            {
                int ratio = (_equipment.Has("共生兽印") ? 2_500 : 0) +
                    (hit ? SupportValue(companion.Skill, SupportMechanic.GuardianBeast, 1_500, 3_000) +
                        (_ascendancy.Has("core.ascendancy.beast_keeper.share.core") ? 3_000 : _ascendancy.Has("core.ascendancy.beast_keeper.share.small") ? 1_500 : 0) : 0);
                int redirected = Math.Min(companion.Life, ScaleCombatValue(damage, Math.Min(5_000, ratio)));
                if (redirected > 0)
                {
                    DamageUnit(companion, redirected, tick, events); remaining -= redirected;
                    events.Add(Event(tick, SpatialEventKind.SkillEffect, "hero", companion.Id, redirected,
                        companion.Position, companion.Position, _equipment.Has("共生兽印") ? "equipment:共生兽印|redirect" : "support:guardian-beast|redirect"));
                }
            }
            var guards = hit ? _units.Where(unit => unit.Life > 0 && unit.Kind == CombatUnitKind.Minion &&
                Support(unit.Skill, SupportMechanic.Bodyguard) && InRange(hero, unit.Position, 6_000)).ToArray() : [];
            if (guards.Length == 0) return remaining;
            int transfer = ScaleCombatValue(remaining, guards.Max(unit => SupportValue(unit.Skill, SupportMechanic.Bodyguard, 1_000, 2_000)));
            for (int index = 0; index < guards.Length; index++)
            {
                int portion = Math.Min(guards[index].Life, transfer / guards.Length + (index < transfer % guards.Length ? 1 : 0));
                DamageUnit(guards[index], portion, tick, events); remaining -= portion;
                events.Add(Event(tick, SpatialEventKind.SkillEffect, "hero", guards[index].Id, portion,
                    hero, guards[index].Position, "support:bodyguard|redirect"));
            }
            return remaining;
        }

        private void RebalanceSharedPool()
        {
            if (!_equipment.Has("群炉主脑")) return;
            _sharedPool.Resize(_units.Where(unit => unit.Kind == CombatUnitKind.Construct && unit.Life > 0).Sum(unit => unit.MaximumLife));
            foreach (ArmyUnit unit in _units.Where(unit => unit.Kind == CombatUnitKind.Construct && unit.Life > 0))
                unit.Life = _sharedPool.MemberLife(unit.MaximumLife);
        }

        private void DamageUnit(ArmyUnit unit, int damage, int tick, ICollection<SpatialEvent> events)
        {
            if (unit.Life <= 0 || damage <= 0) return;
            if (unit.Kind == CombatUnitKind.Construct && _equipment.Has("群炉主脑"))
            {
                _sharedPool.Damage(damage);
                foreach (ArmyUnit construct in _units.Where(item => item.Kind == CombatUnitKind.Construct && item.Life > 0))
                {
                    construct.Life = _sharedPool.MemberLife(construct.MaximumLife);
                    if (construct.Life == 0) Died(construct);
                }
                if (_sharedPool.Life == 0) _sharedPool.Resize(0);
                return;
            }
            unit.Life = Math.Max(0, unit.Life - damage);
            if (unit.Life == 0) Died(unit);

            void Died(ArmyUnit dead)
            {
                if (dead.Kind == CombatUnitKind.Companion)
                    dead.ReviveAt = tick + Math.Max(1, (160 - Math.Min(20, dead.Skill.Quality) * 2) * 10_000 /
                        Math.Max(1, 10_000 + _equipment.Value(ItemModifierKind.IncreasedCompanionReviveRateBasisPoints)));
                else if (dead.Kind == CombatUnitKind.Minion && !dead.Resummoned && _equipment.Value(ItemModifierKind.MinionAutomaticResummon) > 0)
                { dead.Resummoned = true; dead.ReviveAt = tick + 40; }
                else if (dead.Kind == CombatUnitKind.Construct && _ascendancy.Has("core.ascendancy.idol_forger.rebuild.small"))
                    dead.ReviveAt = tick + (_ascendancy.Has("core.ascendancy.idol_forger.rebuild.core") ? 80 : 160);
                if (dead.Kind == CombatUnitKind.Construct && dead.ReviveAt != int.MaxValue)
                    ApplyRebuildSupport(dead, dead.Skill, tick);
                events.Add(Event(tick, SpatialEventKind.SkillEffect, dead.Id, dead.Id, 0,
                    dead.Position, dead.Position, $"unit:{dead.Kind}|death"));
            }
        }
        public BattleArmy(NodeCombatRequest request, IEnumerable<SkillConfiguration> skills, Point origin)
        {
            _request = request;
            _equipment = request.Build.CombatEquipment ?? EquipmentCombatLoadout.Empty;
            _ascendancy = request.Build.Ascendancy ?? CombatProfile.Empty;
            _auras = request.Auras;
            SkillConfiguration[] stones = skills.Where(s => s.SkillId is Bone or Bow).ToArray();
            int maximum = CombatCaps.Clamp(CombatUnitKind.Minion,
                ClassAscendancyRules.MaximumMinions(_equipment.Value(ItemModifierKind.AdditionalMinionMaximum) +
                    _equipment.Value(ItemModifierKind.AdditionalUnitMaximum), _ascendancy) +
                stones.Select(s => SupportValue(s, SupportMechanic.ExpandedArmy, 1, 2)).DefaultIfEmpty().Max());
            if (_equipment.Has("末页王庭")) { _unusedMinionSlots = Math.Min(8, Math.Max(0, maximum - 1)); maximum = 1; }
            if (stones.Length > 0)
                for (int index = 0; index < maximum; index++) Spawn(stones[index % stones.Length], origin, 0);
            if (skills.FirstOrDefault(s => s.SkillId == Beast) is { } beast) Spawn(beast, origin, 0);
        }
        public static bool Automatic(string id) => id is Bone or Bow or Beast;
        public IReadOnlyList<AllyFrame> Frames() => _units.Where(u => u.Life > 0).Select(u =>
            new AllyFrame(u.Id, u.Position, u.Skill.SkillId is Bone or Beast, u.Skill.SkillId,
                u.Life, u.MaximumLife)).ToArray();
        public bool CanUse(string id) => !Automatic(id) && (id switch
        {
            "archetypes.skill.bone_harvest" => _units.Any(u => u.Life > 0 && u.Kind == CombatUnitKind.Minion),
            "archetypes.skill.rune_array" => _units.Count(u => u.Life > 0 && u.Kind == CombatUnitKind.Construct) >= 2,
            "archetypes.skill.selfdestruct_rebuild" => _units.Any(u => u.Life > 0 && u.Kind == CombatUnitKind.Construct &&
                (u.Life * 2L < u.MaximumLife || _ascendancy.Has("core.ascendancy.idol_forger.detonate.core"))),
            _ => true,
        });

        public bool Execute(SkillConfiguration skill, Point origin, IReadOnlyCollection<EnemyUnit> enemies,
            Pcg32 random, int tick, ICollection<SpatialEvent> events, ResourceState hero, EnemyUnit primary)
        {
            if (skill.SkillId == "archetypes.skill.beast_shapeshift")
            {
                foreach (ArmyUnit beast in _units.Where(unit => unit.Kind == CombatUnitKind.Companion))
                {
                    string form = skill.Mode.Length > 0 ? skill.Mode : hero.Life * 2L <= hero.MaximumLife ? "守护" :
                        !InRange(origin, primary.Position, 6_000) ? "追猎" : "猛攻";
                    int maximum = ScaleCombatValue(beast.BaseLife, form == "守护" ? 15_000 : 10_000);
                    beast.Life = (int)((long)beast.Life * maximum / beast.MaximumLife);
                    beast.MaximumLife = maximum;
                    beast.Form = form;
                    events.Add(Event(tick, SpatialEventKind.SkillEffect, beast.Id, beast.Id, beast.MaximumLife, beast.Position, beast.Position, $"beast-form:{form}"));
                }
                return true;
            }
            if (skill.SkillId == "archetypes.skill.twin_soul_pincer")
                foreach (var beast in _units.Where(unit => unit.Life > 0 && unit.Kind == CombatUnitKind.Companion))
                    Attack(beast, primary, random, tick, events, ScaleCombatValue(18_000, 10_000 + skill.Quality * 100));
            if (skill.SkillId == "archetypes.skill.rune_array")
            { _arraySkill = skill; _arrayUntil = tick + 160; _arrayPulse = tick + 15; return true; }
            if (skill.SkillId == "archetypes.skill.selfdestruct_rebuild")
            {
                ArmyUnit? construct = _units.Where(unit => unit.Life > 0 && unit.Kind == CombatUnitKind.Construct &&
                    (unit.Life * 2L < unit.MaximumLife || _ascendancy.Has("core.ascendancy.idol_forger.detonate.core")))
                    .OrderBy(unit => (double)unit.Life / unit.MaximumLife).FirstOrDefault();
                if (construct is null) return true;
                int raw = construct.MaximumLife * (_ascendancy.Has("core.ascendancy.idol_forger.detonate.core") ? 10_000 : 5_000) / 10_000;
                foreach (var enemy in enemies.Where(enemy => enemy.Life > 0 && InRange(construct.Position, enemy.Position, 4_000)))
                    DealUnitDamage(construct, enemy, enemy.Boss ? raw / 2 : raw, SkillDamageType.Fire, tick, events, "selfdestruct");
                DamageUnit(construct, construct.Life, tick, events);
                construct.ReviveAt = tick + (_ascendancy.Has("core.ascendancy.idol_forger.rebuild.core") ? 80 : 120) * (100 - Math.Min(20, skill.Quality)) / 100;
                ApplyRebuildSupport(construct, skill, tick);
                return true;
            }
            if (skill.SkillId == Turret)
            {
                int maximum = CombatCaps.Clamp(CombatUnitKind.Construct, 3 +
                    _equipment.Value(ItemModifierKind.AdditionalConstructMaximum) + _equipment.Value(ItemModifierKind.AdditionalUnitMaximum) +
                    (_ascendancy.Has("core.ascendancy.idol_forger.construct.core") ? 3 : _ascendancy.Has("core.ascendancy.idol_forger.construct.small") ? 1 : 0));
                ArmyUnit[] existing = _units.Where(u => u.Kind == CombatUnitKind.Construct && u.Life > 0).ToArray();
                if (existing.Length >= maximum && existing.Length > 0) _units.Remove(existing[0]);
                if (maximum > 0) Spawn(skill, origin, tick);
                events.Add(Event(tick, SpatialEventKind.SkillEffect, "hero", "", 0, origin, origin, $"skill:{skill.SkillId}|deploy"));
                return true;
            }
            if (skill.SkillId == "archetypes.skill.bone_harvest")
            {
                int corpses = enemies.Count(e => e.Life <= 0 && !e.CorpseConsumed && InRange(origin, e.Position, 6_000));
                corpses = Math.Min(corpses, 5 + skill.Quality / 10);
                foreach (var corpse in enemies.Where(e => e.Life <= 0 && !e.CorpseConsumed && InRange(origin, e.Position, 6_000)).Take(corpses))
                    corpse.CorpseConsumed = true;
                int multiplier = (int)Math.Round(12_000 * Math.Pow(1.05, skill.Level - 1));
                multiplier = ScaleCombatValue(multiplier, 10_000 + corpses * 1_000);
                foreach (ArmyUnit unit in _units.Where(u => u.Life > 0 && u.Kind == CombatUnitKind.Minion))
                    if (SelectTarget(enemies, unit.Position) is { } target)
                        Attack(unit, target, random, tick, events, multiplier);
                return true;
            }
            return Automatic(skill.SkillId);
        }

        private void Spawn(SkillConfiguration skill, Point origin, int tick)
        {
            CombatUnitKind kind = skill.SkillId == Beast ? CombatUnitKind.Companion :
                skill.SkillId == Turret ? CombatUnitKind.Construct : CombatUnitKind.Minion;
            int life = skill.SkillId switch { Bone => 180, Bow => 100, Beast => 350, _ => 160 };
            life = (int)Math.Round(life * Math.Pow(1.065, skill.Level - 1));
            int increased = _equipment.Value(kind switch
            {
                CombatUnitKind.Minion => ItemModifierKind.IncreasedMinionLifeBasisPoints,
                CombatUnitKind.Companion => ItemModifierKind.IncreasedCompanionLifeBasisPoints,
                _ => ItemModifierKind.IncreasedConstructLifeBasisPoints,
            }) + (skill.SkillId == Bone ? skill.Quality * 100 : 0);
            increased += _auras?.UnitLifeIncrease ?? 0;
            increased += SupportValue(skill, SupportMechanic.Bodyguard, 4_000, 7_000) + SupportQuality(skill, SupportMechanic.Bodyguard) * 100;
            increased += SupportValue(skill, SupportMechanic.GuardianBeast, 3_000, 5_000) + SupportQuality(skill, SupportMechanic.GuardianBeast) * 100;
            increased += SupportValue(skill, SupportMechanic.ConstructAmplify, 2_500, 4_000) + SupportQuality(skill, SupportMechanic.ConstructAmplify) * 100;
            life = ScaleCombatValue(life, 10_000 + increased);
            if (kind == CombatUnitKind.Minion) life = ScaleCombatValue(life, 10_000 + _unusedMinionSlots * 3_000);
            if (Support(skill, SupportMechanic.MinionAmplify)) life = ScaleCombatValue(life,
                10_000 - QualityOverride(skill, SupportMechanic.MinionAmplify, SupportValue(skill, SupportMechanic.MinionAmplify, 1_500, 1_000), 500));
            if (kind == CombatUnitKind.Companion) life += _equipment.Value(ItemModifierKind.FlatCompanionMaximumLife);
            if (kind == CombatUnitKind.Construct) life = ScaleCombatValue(life, 10_000 + _equipment.Value(ItemModifierKind.MoreConstructLifeBasisPoints));
            _units.Add(new($"army:{++_sequence}", kind, skill, Math.Max(1, life),
                origin with { XRaw = Math.Clamp(origin.XRaw + (_sequence % 5 - 2) * 350, 350, 11_650) }, tick));
            if (kind == CombatUnitKind.Construct) RebalanceSharedPool();
        }

        public void Advance(IReadOnlyCollection<EnemyUnit> enemies, Point hero, Pcg32 random, int tick,
            ICollection<SpatialEvent> events)
        {
            _heroPosition = hero;
            if (_arraySkill is { } array && tick < _arrayUntil && tick >= _arrayPulse)
            {
                _arrayPulse = tick + 15;
                ArmyUnit[] constructs = _units.Where(unit => unit.Life > 0 && unit.Kind == CombatUnitKind.Construct).ToArray();
                var edges = new HashSet<(string, string)>();
                foreach (var from in constructs)
                    foreach (var to in constructs.Where(unit => unit != from).OrderBy(unit => Point.DistanceSquared(from.Position, unit.Position)).ThenBy(unit => unit.Id, StringComparer.Ordinal).Take(2))
                        edges.Add(string.CompareOrdinal(from.Id, to.Id) < 0 ? (from.Id, to.Id) : (to.Id, from.Id));
                foreach (var enemy in enemies.Where(enemy => enemy.Life > 0))
                {
                    int count = 0;
                    foreach (var edge in edges.OrderBy(edge => edge.Item1, StringComparer.Ordinal).ThenBy(edge => edge.Item2, StringComparer.Ordinal))
                    {
                        var from = constructs.Single(unit => unit.Id == edge.Item1); var to = constructs.Single(unit => unit.Id == edge.Item2);
                        if (!OnSegment(enemy.Position, from.Position, to.Position, 450 + array.Quality * 450 / 100) || count++ >= 3) continue;
                        int raw = (int)Math.Round((10 + random.NextUInt() % 11) * Math.Pow(1.07, array.Level - 1));
                        DealUnitDamage(from, enemy, raw, SkillDamageType.Lightning, tick, events, "rune-array");
                    }
                }
            }
            foreach (ArmyUnit unit in _units)
            {
                if (unit.Life <= 0)
                {
                    if (tick < unit.ReviveAt) continue;
                    if (unit.Kind == CombatUnitKind.Construct)
                    {
                        unit.MaximumLife = ScaleCombatValue(unit.BaseLife, unit.RebuildLifeMultiplier);
                        unit.RapidRebuildUntil = tick + (unit.RebuildLifeMultiplier < 10_000 ? 60 : 0);
                    }
                    unit.Life = unit.Kind == CombatUnitKind.Companion ? Math.Max(1, unit.MaximumLife / 2) : unit.MaximumLife;
                    unit.ReviveAt = int.MaxValue;
                    unit.RebuiltUntil = tick + 80;
                    if (unit.Kind == CombatUnitKind.Construct) RebalanceSharedPool();
                    events.Add(Event(tick, SpatialEventKind.SkillEffect, unit.Id, unit.Id, unit.Life,
                        unit.Position, unit.Position, $"unit:{unit.Kind}|revive"));
                }
                var candidates = enemies.Where(e => e.Life > 0);
                var command = unit.Kind == CombatUnitKind.Minion ? _request.Buffs?.Command(tick) : null;
                EnemyUnit? target = command is not null && candidates.FirstOrDefault(enemy => enemy.EntityId == command.TargetId) is { } commanded ? commanded :
                    Support(unit.Skill, SupportMechanic.Bodyguard) ? candidates.OrderBy(enemy => Point.DistanceSquared(hero, enemy.Position)).FirstOrDefault() :
                    unit.Kind == CombatUnitKind.Companion && unit.Form == "追猎" ?
                    candidates.OrderBy(e => e.Life * 2L > e.MaximumLife).ThenByDescending(e => Point.DistanceSquared(unit.Position, e.Position)).FirstOrDefault() :
                    unit.Kind == CombatUnitKind.Companion && unit.Form == "守护" ? candidates.OrderBy(e => Point.DistanceSquared(hero, e.Position)).FirstOrDefault() :
                    unit.Skill.SkillId == Bow ? candidates.OrderBy(e => (double)e.Life / e.MaximumLife)
                    .ThenBy(e => Point.DistanceSquared(unit.Position, e.Position)).FirstOrDefault() :
                    candidates.OrderByDescending(e => unit.Kind == CombatUnitKind.Construct &&
                        ClassAscendancyRules.ConstructPrioritizes(e.Rarity, _ascendancy))
                        .ThenBy(e => Point.DistanceSquared(unit.Position, e.Position)).FirstOrDefault();
                if (target is null) continue;
                int range = unit.Skill.SkillId == Bow ? 9_000 : unit.Skill.SkillId == Turret ? 10_000 : 1_700;
                var buff = _request.Buffs?.ForUnit(tick, hero, unit.Position, unit.Kind == CombatUnitKind.Minion) ?? default;
                int movementIncrease = (_auras?.UnitSpeedIncrease ?? 0) + buff.MovementSpeed;
                if (unit.Kind == CombatUnitKind.Companion) movementIncrease += unit.Form == "追猎" ? 5_000 : unit.Form == "猛攻" ? 2_000 : 0;
                if (unit.Kind == CombatUnitKind.Minion) movementIncrease += _equipment.Value(ItemModifierKind.IncreasedMinionSpeedBasisPoints) +
                    SupportValue(unit.Skill, SupportMechanic.SwiftMinions, 3_000, 5_000) + SupportQuality(unit.Skill, SupportMechanic.SwiftMinions) * 50;
                int speed = ScaleCombatValue(4_000, 10_000 + movementIncrease);
                if (unit.Kind != CombatUnitKind.Construct && !InRange(unit.Position, target.Position, range))
                    unit.Position = Point.MoveToward(unit.Position, target.Position, speed / 20);
                if (unit.Skill.SkillId == Bow && InRange(unit.Position, target.Position, 3_000))
                    unit.Position = Point.MoveToward(unit.Position, new Point(
                        Math.Clamp(2 * unit.Position.XRaw - target.Position.XRaw, 350, 11_650),
                        Math.Clamp(2 * unit.Position.YRaw - target.Position.YRaw, 350, 23_650)), speed / 20);
                if (tick < unit.ReadyAt || !InRange(unit.Position, target.Position, range)) continue;
                bool bash = unit.Skill.SkillId == Bone && tick >= unit.BashAt;
                Attack(unit, target, random, tick, events, bash ? 18_000 : 10_000);
                if (unit.Skill.SkillId == Bow && enemies.Where(enemy => enemy != target && enemy.Life > 0)
                    .FirstOrDefault(enemy => OnSegment(enemy.Position, target.Position, new Point(
                        target.Position.XRaw + (target.Position.XRaw - unit.Position.XRaw) * 2,
                        target.Position.YRaw + (target.Position.YRaw - unit.Position.YRaw) * 2), 450)) is { } pierced)
                    Attack(unit, pierced, random, tick, events, 10_000);
                if (bash) { unit.BashAt = tick + 80; target.ArmyTauntId = unit.Id; target.ArmyTauntUntil = tick + 60; }
                int frequency = unit.Skill.SkillId switch { Bone => 1_100, Bow => 1_250, Beast => 1_200, _ => 1_050 };
                int bonus = unit.Kind == CombatUnitKind.Minion ? _equipment.Value(ItemModifierKind.IncreasedMinionSpeedBasisPoints) : 0;
                bonus += _auras?.UnitSpeedIncrease ?? 0;
                bonus += buff.ActionSpeed + SupportValue(unit.Skill, SupportMechanic.SwiftMinions, 3_000, 5_000) + SupportQuality(unit.Skill, SupportMechanic.SwiftMinions) * 50;
                bonus += SupportValue(unit.Skill, SupportMechanic.FerociousBeast, 1_500, 2_500);
                if (tick < unit.RapidRebuildUntil) bonus += 5_000;
                if (unit.Skill.SkillId == Turret) bonus += unit.Skill.Quality * 100;
                if (unit.Kind == CombatUnitKind.Companion && unit.Form == "猛攻") bonus += 2_500;
                if (unit.Kind == CombatUnitKind.Construct && _ascendancy.Has("core.ascendancy.idol_forger.construct.core")) bonus += _units.Count(unit => unit.Life > 0 && unit.Kind == CombatUnitKind.Construct) * 800;
                frequency = Math.Max(1, ScaleCombatValue(frequency, 10_000 + bonus));
                unit.ReadyAt = tick + Math.Max(1, (20_000 + frequency - 1) / frequency);
            }
        }

        private void Attack(ArmyUnit unit, EnemyUnit enemy, Pcg32 random, int tick,
            ICollection<SpatialEvent> events, int multiplier)
        {
            int accuracy = (unit.Skill.SkillId switch { Bone => 180, Bow => 220, Beast => 250, _ => 240 }) + (unit.Skill.Level - 1) * 25;
            if (random.NextBasisPoints() >= DamageRules.HitChance(accuracy, enemy.Scaled.Evasion, false).Value) return;
            (int min, int max) = unit.Skill.SkillId switch { Bone => (12, 18), Bow => (10, 28), Beast => (24, 36), _ => (16, 24) };
            double growth = Math.Pow(1.06, unit.Skill.Level - 1);
            min = (int)Math.Round(min * growth); max = (int)Math.Round(max * growth);
            int raw = min + (int)(random.NextUInt() % (uint)(max - min + 1));
            int increased = _equipment.Value(unit.Kind switch
            {
                CombatUnitKind.Minion => ItemModifierKind.IncreasedMinionDamageBasisPoints,
                CombatUnitKind.Companion => ItemModifierKind.IncreasedCompanionDamageBasisPoints,
                _ => ItemModifierKind.IncreasedConstructDamageBasisPoints,
            });
            if (unit.Kind == CombatUnitKind.Minion) increased += ClassAscendancyRules.IncreasedMinionDamageBasisPoints(
                _units.Count(u => u.Life > 0 && u.Kind == unit.Kind), _ascendancy);
            increased += _request.Buffs?.ForUnit(tick, _heroPosition, unit.Position, unit.Kind == CombatUnitKind.Minion).DamageIncrease ?? 0;
            raw = ScaleCombatValue(raw, 10_000 + increased);
            if (unit.Kind == CombatUnitKind.Minion) raw = ScaleCombatValue(raw, 10_000 + _unusedMinionSlots * 3_000);
            raw = ScaleCombatValue(raw, 10_000 + SupportValue(unit.Skill, SupportMechanic.MinionAmplify, 3_000, 5_500));
            raw = ScaleCombatValue(raw, 10_000 + SupportValue(unit.Skill, SupportMechanic.ConstructAmplify, 3_000, 5_500));
            raw = ScaleCombatValue(raw, 10_000 + SupportValue(unit.Skill, SupportMechanic.SwiftMinions, 1_500, 2_500));
            if (Support(unit.Skill, SupportMechanic.ExpandedArmy)) raw = ScaleCombatValue(raw, 10_000 -
                QualityOverride(unit.Skill, SupportMechanic.ExpandedArmy, SupportValue(unit.Skill, SupportMechanic.ExpandedArmy, 2_000, 1_500), 1_000));
            if (Support(unit.Skill, SupportMechanic.FerociousBeast))
            {
                raw = ScaleCombatValue(raw, 10_000 + SupportValue(unit.Skill, SupportMechanic.FerociousBeast, 3_500, 6_000));
                raw = ScaleCombatValue(raw, 10_000 + SupportQuality(unit.Skill, SupportMechanic.FerociousBeast) * 50);
            }
            if (Support(unit.Skill, SupportMechanic.GuardianBeast)) raw = ScaleCombatValue(raw, 8_000);
            if (unit.Kind == CombatUnitKind.Minion && _request.Buffs?.Command(tick) is { } command && command.TargetId == enemy.EntityId)
            {
                raw = ScaleCombatValue(raw, 10_000 + command.MoreDamage);
                if (_ascendancy.Has("core.ascendancy.soul_shepherd.command.core") && enemy.Rarity is EnemyRarity.Rare or EnemyRarity.Boss)
                    raw = ScaleCombatValue(raw, 15_000);
            }
            raw = ScaleCombatValue(raw, multiplier);
            raw = ScaleCombatValue(raw, _auras?.UnitDamageMultiplier ?? 10_000);
            if (unit.Kind == CombatUnitKind.Companion && _equipment.Has("共生兽印") && CompanionAlive)
                raw = ScaleCombatValue(raw, 13_000);
            if (unit.Kind == CombatUnitKind.Companion)
            {
                raw = ScaleCombatValue(raw, 10_000 + _equipment.Value(ItemModifierKind.MoreCompanionDamageBasisPoints));
                raw = ScaleCombatValue(raw, unit.Form == "猛攻" ? 13_500 : unit.Form == "守护" ? 7_500 : enemy.Life * 2L <= enemy.MaximumLife ? 13_000 : 10_000);
            }
            if (unit.Kind == CombatUnitKind.Construct)
            {
                raw = ScaleCombatValue(raw, 10_000 + _equipment.Value(ItemModifierKind.MoreConstructDamageBasisPoints));
                if (tick < unit.RebuiltUntil && _ascendancy.Has("core.ascendancy.idol_forger.rebuild.core")) raw = ScaleCombatValue(raw, 15_000);
            }
            if (unit.Kind == CombatUnitKind.Construct) raw = ScaleCombatValue(raw, ClassAscendancyRules.ConstructDamageMultiplier(enemy.Rarity, _ascendancy));
            if (random.NextBasisPoints() < (unit.Kind == CombatUnitKind.Companion ? unit.Form == "追猎" ? 1_200 : 600 : 500)) raw = ScaleCombatValue(raw, 15_000);
            SkillDamageType type = unit.Skill.SkillId == Bow ? SkillDamageType.Void : SkillDamageType.Physical;
            DealUnitDamage(unit, enemy, raw, type, tick, events, "attack");
        }
        private void DealUnitDamage(ArmyUnit unit, EnemyUnit enemy, int raw, SkillDamageType type, int tick, ICollection<SpatialEvent> events, string action)
        {
            raw = ScaleCombatValue(raw, 10_000 + enemy.ShockEffect);
            raw = ScaleCombatValue(raw, 10_000 + enemy.Curses.Effect("archetypes.skill.death_mark", tick));
            if (type == SkillDamageType.Void) raw = ScaleCombatValue(raw, CombatRules.WitherMultiplier(enemy.Ailments.Stack(Ailment.Wither, tick)));
            int damage = DamagePacketRules.Resolve(raw, type, SkillSupport.None, CombatRules.ArmorAfterBreak(enemy.Scaled.Armor, enemy.ArmorBreakStacks),
                EnemyResistance(enemy, _request, SkillDamageType.Fire, penetrate: false), EnemyResistance(enemy, _request, SkillDamageType.Cold, penetrate: false),
                EnemyResistance(enemy, _request, SkillDamageType.Lightning, penetrate: false), EnemyResistance(enemy, _request, SkillDamageType.Void, penetrate: false),
                enemy.Scaled.PhysicalResistanceBasisPoints + _request.EnemyPhysicalReductionBasisPoints).Total;
            damage = Math.Min(enemy.Life, damage);
            enemy.Life -= damage;
            events.Add(Event(tick, SpatialEventKind.SkillEffect, unit.Id, enemy.EntityId, damage,
                unit.Position, enemy.Position, $"skill:{unit.Skill.SkillId}|unit:{unit.Kind}|{action}"));
            if (enemy.Life == 0) events.Add(Event(tick, SpatialEventKind.EnemyDefeated, unit.Id,
                enemy.EntityId, 0, unit.Position, enemy.Position, enemy.Profile.StableId));
        }

        // Single target attacks may target a closer unit, or an active taunting boneguard.
        public bool ReceiveEnemyAction(EnemyUnit enemy, EnemySkillProfile skill, Point hero,
            NodeCombatRequest request, Pcg32 random, int tick, ICollection<SpatialEvent> events)
        {
            if (skill.Area || skill.Kind is not (EnemySkillKind.BasicStrike or EnemySkillKind.ArcaneBolt or EnemySkillKind.Volley)) return false;
            ArmyUnit? unit = _units.Where(u => u.Life > 0).OrderByDescending(u =>
                tick < enemy.ArmyTauntUntil && u.Id == enemy.ArmyTauntId)
                .ThenBy(u => Point.DistanceSquared(u.Position, enemy.Position)).FirstOrDefault();
            if (unit is null || !(tick < enemy.ArmyTauntUntil && unit.Id == enemy.ArmyTauntId) &&
                Point.DistanceSquared(unit.Position, enemy.Position) >= Point.DistanceSquared(hero, enemy.Position)) return false;
            int range = Math.Max(enemy.Profile.AttackRangeRaw, skill.RangeRaw);
            if (!InRange(enemy.Position, unit.Position, range))
                enemy.Position = Point.MoveToward(enemy.Position, unit.Position, Math.Max(1,
                    ScaleCombatValue(enemy.Profile.MovementSpeedRawPerSecond, request.EnemySpeedBasisPoints) / 20));
            if (tick < enemy.NextActionTick || !InRange(enemy.Position, unit.Position, range)) return true;
            int raw = enemy.Scaled.MinimumPhysicalDamage + (int)(random.NextUInt() % (uint)Math.Max(1,
                enemy.Scaled.MaximumPhysicalDamage - enemy.Scaled.MinimumPhysicalDamage + 1));
            raw = ScaleCombatValue(raw, skill.DamageMultiplierBasisPoints);
            raw = ScaleCombatValue(raw, request.EnemyDamageBasisPoints);
            int damage = MitigateUnitHit(unit, enemy, skill, raw, random, tick);
            DamageUnit(unit, damage, tick, events);
            events.Add(Event(tick, SpatialEventKind.EnemyAttack, enemy.EntityId, unit.Id, damage,
                enemy.Position, unit.Position, $"{skill.DisplayName}|unit:{unit.Kind}|{(damage == 0 ? "avoided" : "hit")}"));
            int frequency = Math.Max(1, ScaleCombatValue(enemy.Scaled.AttacksPerSecondMilli, request.EnemySpeedBasisPoints));
            enemy.NextActionTick = tick + Math.Max(8, (20_000 + frequency - 1) / frequency);
            enemy.ActionSequence++;
            return true;
        }
        private int MitigateUnitHit(ArmyUnit unit, EnemyUnit enemy, EnemySkillProfile skill, int raw, Pcg32 random, int tick)
        {
            if (!skill.IsSpell && random.NextBasisPoints() >= CombatRules.HitChance(enemy.Profile.Accuracy, unit.Evasion)) return 0;
            int block = unit.Skill.SkillId == Bone ? skill.IsSpell ? 1_500 : 3_000 : 0;
            if (random.NextBasisPoints() < block) return 0;
            int damage = raw;
            if (skill.DamageType == EnemyDamageType.Physical) damage = ScaleCombatValue(damage, 10_000 - CombatRules.ArmorReduction(unit.Armor, damage));
            damage = CombatRules.MitigateByResistance(damage, UnitResistance(unit, skill.DamageType, tick));
            if (skill.IsSpell && random.NextBasisPoints() < unit.Suppression) damage = CombatRules.SuppressedDamage(damage);
            if (unit.Kind == CombatUnitKind.Companion && unit.Form == "守护") damage = ScaleCombatValue(damage, 8_000);
            if (Support(unit.Skill, SupportMechanic.FerociousBeast)) damage = ScaleCombatValue(damage, 12_000);
            if (unit.Kind == CombatUnitKind.Construct && tick < unit.RebuiltUntil && _ascendancy.Has("core.ascendancy.idol_forger.rebuild.core")) damage = ScaleCombatValue(damage, 5_000);
            return damage;
        }
        public void ReceiveArea(EnemyUnit enemy, EnemySkillProfile skill, Point center, int radius, int raw,
            Pcg32 random, int tick, ICollection<SpatialEvent> events, bool damageOverTime = false)
        {
            foreach (var unit in _units.Where(unit => unit.Life > 0 && InRange(unit.Position, center, radius)).ToArray())
            {
                int damage = damageOverTime ? MitigateUnitDot(unit, raw, skill.DamageType, 2, tick) : MitigateUnitHit(unit, enemy, skill, raw, random, tick);
                DamageUnit(unit, damage, tick, events);
                events.Add(Event(tick, SpatialEventKind.EnemyAttack, enemy.EntityId, unit.Id, damage, center, unit.Position,
                    $"{skill.DisplayName}|unit:{unit.Kind}|area:{radius}|dot:{damageOverTime}"));
            }
        }
        public void ReceiveHazard(EnemyHazard hazard, int tick, ICollection<SpatialEvent> events)
        {
            foreach (var unit in _units.Where(unit => unit.Life > 0 && InRange(unit.Position, hazard.Position, hazard.Radius)).ToArray())
            {
                int damage = MitigateUnitDot(unit, hazard.Damage, hazard.DamageType, 2, tick);
                DamageUnit(unit, damage, tick, events);
                events.Add(Event(tick, SpatialEventKind.EnemyAttack, hazard.Source, unit.Id, damage, hazard.Position, unit.Position, "持续危险地面|unit"));
            }
        }
        private int UnitResistance(ArmyUnit unit, EnemyDamageType type, int tick) => type == EnemyDamageType.Physical ? 0 :
            Math.Clamp(unit.Resistance + (_request.Buffs?.ForUnit(tick, _heroPosition, unit.Position).Resistance ?? 0), -50_000, 7_500);
        private int MitigateUnitDot(ArmyUnit unit, int raw, EnemyDamageType type, int frequency, int tick)
        {
            int damage = CombatRules.MitigateByResistance(raw, UnitResistance(unit, type, tick));
            if (type == EnemyDamageType.Physical) damage = ScaleCombatValue(damage,
                10_000 - CombatRules.PhysicalDotArmorReduction(unit.Armor, raw * frequency));
            if (Support(unit.Skill, SupportMechanic.FerociousBeast)) damage = ScaleCombatValue(damage, 12_000);
            return damage;
        }
        private static void ApplyRebuildSupport(ArmyUnit unit, SkillConfiguration config, int tick)
        {
            unit.RebuildLifeMultiplier = 10_000;
            if (!Support(config, SupportMechanic.RapidRebuild)) return;
            int delay = ScaleCombatValue(unit.ReviveAt - tick, 10_000 - SupportValue(config, SupportMechanic.RapidRebuild, 5_000, 7_000));
            delay = ScaleCombatValue(delay, 10_000 - SupportQuality(config, SupportMechanic.RapidRebuild) * 50);
            unit.ReviveAt = tick + Math.Max(1, delay);
            unit.RebuildLifeMultiplier = 10_000 - SupportValue(config, SupportMechanic.RapidRebuild, 2_000, 1_000);
        }
        private sealed class ArmyUnit(string id, CombatUnitKind kind, SkillConfiguration skill, int life, Point position, int tick)
        {
            public string Id { get; } = id;
            public CombatUnitKind Kind { get; } = kind;
            public SkillConfiguration Skill { get; } = skill;
            public int BaseLife { get; } = life;
            public int MaximumLife { get; set; } = life;
            public string Form { get; set; } = "猛攻";
            public int RebuiltUntil { get; set; }
            public int RapidRebuildUntil { get; set; }
            public int RebuildLifeMultiplier { get; set; } = 10_000;
            public int Armor { get; set; }
            public int Evasion { get; set; }
            public int Resistance { get; set; }
            public int Suppression { get; set; }
            public int Life { get; set; } = life;
            public Point Position { get; set; } = position;
            public int ReadyAt { get; set; } = tick;
            public int BashAt { get; set; } = tick;
            public int ReviveAt { get; set; } = int.MaxValue;
            public bool Resummoned { get; set; }
        }
    }
}
