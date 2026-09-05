using GameForWork.Core.Campaign.Combat;
using GameForWork.Core.Campaign.Items;
using GameForWork.Core.Campaign.World;
using GameForWork.Core.Equipment;
using GameForWork.Core.Simulation;
using GameForWork.Core.Builds;
using GameForWork.Core.SkillCatalog;

namespace GameForWork.Core.Combat;

public sealed record EquippedFlask(FlaskKind Kind, string Id, int Slot, IReadOnlyDictionary<ItemModifierKind, int> Modifiers, int Quality = 0);
public sealed record FlaskActivation(FlaskKind Kind, string Id, int ChargesSpent);
public sealed class FlaskBottle(EquippedFlask input)
{
    public EquippedFlask Input { get; } = input;
    public decimal Charges { get; set; } = input.Kind is FlaskKind.Life or FlaskKind.Mana ? 30 : 40;
    public int MaximumCharges => Input.Kind is FlaskKind.Life or FlaskKind.Mana ? 30 : 40;
    public decimal RemainingRecovery { get; set; }
    public decimal RecoveryPerSecond { get; set; }
    public decimal Remainder { get; set; }
    public int RemainingMilliseconds { get; set; }
    public int DurationMilliseconds { get; set; }
    public decimal TotalNonInstantRecovery { get; set; }
    public bool Echo { get; set; }
    public decimal Overflow { get; set; }
    public bool Active => RemainingMilliseconds > 0;
}

public sealed class FlaskRack
{
    private readonly EquipmentCombatLoadout _equipment;
    private readonly TeamBuild _build;
    private readonly List<FlaskBottle> _bottles;
    public IReadOnlyList<FlaskBottle> Bottles => _bottles;
    public FlaskRack(TeamBuild build)
    {
        _build = build;
        _equipment = build.CombatEquipment ?? EquipmentCombatLoadout.Empty;
        IEnumerable<EquippedFlask> entries = _equipment.Flasks ?? (build.Flasks ?? (build.LifeFlask is not null ? [FlaskKind.Life] : []))
            .Select((kind, slot) => new EquippedFlask(kind, $"flask:{slot}", slot, new Dictionary<ItemModifierKind, int>()));
        _bottles = entries.OrderBy(flask => flask.Slot).Take(5).Select(flask => new FlaskBottle(flask)).ToList();
    }
    public bool Active(FlaskKind kind) => _bottles.Any(bottle => bottle.Input.Kind == kind && bottle.Active);
    public int UtilityEffect(FlaskKind kind) => _bottles.Where(bottle => bottle.Input.Kind == kind && bottle.Active)
        .Select(bottle => CombatRules.ApplyIncreased(kind == FlaskKind.Resistance ? 2_500 : 3_000,
            Effect(bottle))).DefaultIfEmpty().Max();
    private int Value(FlaskBottle bottle, ItemModifierKind kind) => bottle.Input.Modifiers.GetValueOrDefault(kind) +
        (kind is ItemModifierKind.FlaskRepeatEffect or ItemModifierKind.FlaskOverflowCharges or ItemModifierKind.FlaskCleanseBleedPoison or
            ItemModifierKind.FlaskCleanseElementalAilments or ItemModifierKind.FlaskCleanseCurses ? 0 : _equipment.Value(kind)) +
        (kind == ItemModifierKind.IncreasedFlaskRecoveryAmountBasisPoints ? bottle.Input.Quality * 100 : 0);
    private int Effect(FlaskBottle bottle) => Value(bottle, ItemModifierKind.MoreFlaskEffectBasisPoints) + (_equipment.Has("余烬锁链") ? 3_000 : 0);
    private int Cost(FlaskBottle bottle) => Math.Max(1, (int)decimal.Ceiling((bottle.Input.Kind is FlaskKind.Life or FlaskKind.Mana ? 10 : 20) *
        Math.Max(0, 10_000 + Value(bottle, ItemModifierKind.IncreasedFlaskChargesPerUseBasisPoints)) / 10_000m));
    public int UnusedUses => _bottles.Sum(bottle => (int)decimal.Floor((bottle.Charges + bottle.Overflow) / Cost(bottle)));
    public int Buff(ItemModifierKind kind) => _bottles.Where(bottle => bottle.Active).Select(bottle =>
        CombatRules.ApplyIncreased(Value(bottle, kind), Effect(bottle))).Sum();
    public void Fill() { foreach (var bottle in _bottles) bottle.Charges = bottle.MaximumCharges; }
    public void GainCharges(int amount)
    {
        foreach (var bottle in _bottles)
        {
            decimal total = bottle.Charges + amount * Math.Max(0, 10_000 + Value(bottle, ItemModifierKind.IncreasedFlaskChargeGainBasisPoints)) / 10_000m;
            if (Value(bottle, ItemModifierKind.FlaskOverflowCharges) > 0)
                bottle.Overflow = Math.Min(Cost(bottle), bottle.Overflow + Math.Max(0, total - bottle.MaximumCharges));
            bottle.Charges = Math.Min(bottle.MaximumCharges, total);
        }
    }
    public FlaskActivation? TryUse(FlaskKind kind, ResourceState hero, Pcg32 random, int thresholdBasisPoints = 0, int recoveryMultiplier = 10_000)
    {
        if (!hero.IsAlive) return null;
        int current = kind == FlaskKind.Mana ? hero.Mana : hero.Life;
        int maximum = kind == FlaskKind.Mana ? hero.AvailableMaximumMana : hero.MaximumLife;
        if (kind is FlaskKind.Life or FlaskKind.Mana)
        {
            if (current >= maximum) return null;
            decimal pending = _bottles.Where(bottle => bottle.Input.Kind == kind && bottle.Active).Sum(bottle => bottle.RemainingRecovery);
            if (thresholdBasisPoints > 0 && (current + pending) * 10_000 >= maximum * (long)thresholdBasisPoints) return null;
        }
        else if (Active(kind)) return null;
        foreach (var bottle in _bottles.Where(bottle => bottle.Input.Kind == kind).OrderByDescending(bottle => bottle.Charges / bottle.MaximumCharges).ThenBy(bottle => bottle.Input.Slot))
        {
            int cost = Cost(bottle);
            if (bottle.Charges + bottle.Overflow < cost) continue;
            decimal total = 0;
            if (kind is FlaskKind.Life or FlaskKind.Mana)
            {
                total = (kind == FlaskKind.Life ? hero.MaximumLife * .5m : hero.MaximumMana * .3m) *
                    Math.Max(0, 10_000 + Value(bottle, ItemModifierKind.IncreasedFlaskRecoveryAmountBasisPoints)) / 10_000 * Math.Max(0, recoveryMultiplier) / 10_000 *
                    Math.Max(0, 10_000 + Effect(bottle)) / 10_000;
                if (kind == FlaskKind.Life)
                {
                    int effect = _build.IncreasedLifeFlaskEffectBasisPoints;
                    if (_build.Ascendancy?.Has(GameForWork.Core.Ascendancies.WarriorNodeIds.BloodLowLifeSmall) == true && hero.Life * 2L <= hero.MaximumLife) effect += 2_500;
                    total *= Math.Max(0, 10_000 + effect) / 10_000m;
                }
            }
            int side = kind == FlaskKind.Life ? Value(bottle, ItemModifierKind.FlaskLifeRemovedFromManaBasisPoints) :
                kind == FlaskKind.Mana ? Value(bottle, ItemModifierKind.FlaskManaRemovedFromLifeBasisPoints) : 0;
            int sideCost = (int)Math.Min(int.MaxValue, decimal.Ceiling(total * Math.Max(0, side) / 10_000));
            if (kind == FlaskKind.Life && hero.Mana < sideCost || kind == FlaskKind.Mana && hero.Life <= sideCost) continue;
            if (sideCost > 0)
            {
                if (kind == FlaskKind.Life) hero.TryPayMana(sideCost);
                else hero.TryPayLifeCost(sideCost);
            }
            int consumed = random.NextBasisPoints() < Value(bottle, ItemModifierKind.FlaskNoChargeConsumptionChanceBasisPoints) ? 0 : cost;
            decimal ordinary = Math.Min(bottle.Charges, consumed);
            bottle.Charges -= ordinary; bottle.Overflow -= consumed - ordinary;
            bottle.RemainingMilliseconds = kind is FlaskKind.Life or FlaskKind.Mana ?
                Math.Max(50, (int)(3_000m * 10_000 / Math.Max(1, 10_000 + Value(bottle, ItemModifierKind.IncreasedFlaskRecoveryRateBasisPoints)) *
                    10_000 / Math.Max(1, 10_000 + hero.Sheet.IncreasedRecoveryRateBasisPoints))) :
                CombatRules.ApplyIncreased(5_000, Value(bottle, ItemModifierKind.IncreasedFlaskDurationBasisPoints));
            bottle.DurationMilliseconds = bottle.RemainingMilliseconds;
            bottle.Echo = false;
            if (kind is FlaskKind.Life or FlaskKind.Mana)
            {
                decimal instant = total * Math.Clamp(Value(bottle, ItemModifierKind.InstantFlaskRecoveryPortionBasisPoints), 0, 10_000) / 10_000;
                Restore(kind, hero, (int)instant);
                bottle.RemainingRecovery = total - instant;
                bottle.TotalNonInstantRecovery = bottle.RemainingRecovery;
                bottle.RecoveryPerSecond = bottle.RemainingRecovery * 1_000 / bottle.RemainingMilliseconds;
                bottle.Remainder = 0;
            }
            if (_equipment.Has("余烬锁链"))
            {
                bottle.RemainingMilliseconds = bottle.DurationMilliseconds = Math.Max(1, bottle.DurationMilliseconds * 3 / 4);
                bottle.RemainingRecovery *= .75m;
                bottle.TotalNonInstantRecovery = bottle.RemainingRecovery;
            }
            if (Value(bottle, ItemModifierKind.FlaskCleanseBleedPoison) > 0)
                hero.HarmfulStatus.Cleanse(80, Ailment.Bleed, Ailment.Poison);
            if (Value(bottle, ItemModifierKind.FlaskCleanseElementalAilments) > 0)
                hero.HarmfulStatus.Cleanse(80, Ailment.Ignite, Ailment.Chill, Ailment.Freeze, Ailment.Shock, Ailment.Paralysis);
            if (Value(bottle, ItemModifierKind.FlaskCleanseCurses) > 0) hero.HarmfulStatus.CleanseCurses(80);
            return new(kind, bottle.Input.Id, consumed);
        }
        return null;
    }
    public IReadOnlyList<(FlaskKind Kind, int Amount)> Advance(ResourceState hero, int milliseconds, int recoveryMultiplier = 10_000)
    {
        var restored = new List<(FlaskKind, int)>();
        foreach (var bottle in _bottles.Where(bottle => bottle.Active))
        {
            int elapsed = Math.Min(milliseconds, bottle.RemainingMilliseconds);
            bottle.RemainingMilliseconds -= elapsed;
            if (bottle.Input.Kind is not (FlaskKind.Life or FlaskKind.Mana)) { Repeat(bottle); continue; }
            bool full = bottle.Input.Kind == FlaskKind.Life ? hero.Life >= hero.MaximumLife : hero.Mana >= hero.AvailableMaximumMana;
            if (full && !(bottle.Input.Kind == FlaskKind.Mana && Value(bottle, ItemModifierKind.FlaskDoesNotEndAtFullMana) > 0))
            { bottle.RemainingMilliseconds = 0; bottle.RemainingRecovery = 0; continue; }
            bool delayed = Value(bottle, ItemModifierKind.FlaskRecoveryAtEnd) > 0;
            decimal portion = delayed ? bottle.RemainingMilliseconds == 0 ? bottle.RemainingRecovery : 0 : Math.Min(bottle.RemainingRecovery, bottle.RecoveryPerSecond * elapsed / 1_000);
            bottle.RemainingRecovery -= portion;
            bottle.Remainder += portion * Math.Max(0, recoveryMultiplier) / 10_000;
            int whole = (int)Math.Min(int.MaxValue, decimal.Floor(bottle.Remainder)); bottle.Remainder -= whole;
            int actual = Restore(bottle.Input.Kind, hero, whole);
            if (actual > 0) restored.Add((bottle.Input.Kind, actual));
            Repeat(bottle);
        }
        return restored;
    }
    private void Repeat(FlaskBottle bottle)
    {
        if (bottle.RemainingMilliseconds > 0 || bottle.Echo || Value(bottle, ItemModifierKind.FlaskRepeatEffect) <= 0) return;
        bottle.Echo = true;
        bottle.RemainingMilliseconds = bottle.DurationMilliseconds;
        bottle.RemainingRecovery = bottle.TotalNonInstantRecovery;
    }
    public void EndEncounter()
    {
        foreach (var bottle in _bottles) { bottle.RemainingMilliseconds = 0; bottle.RemainingRecovery = 0; bottle.Remainder = 0; }
    }
    private static int Restore(FlaskKind kind, ResourceState hero, int amount) => hero.IsAlive ? kind == FlaskKind.Life ? hero.HealLife(amount) : hero.RestoreMana(amount) : 0;
}
