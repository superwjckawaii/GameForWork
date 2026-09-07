using GameForWork.Core.Builds;
using GameForWork.Core.Campaign.Progression;

namespace GameForWork.Core.Campaign.Combat;

public sealed partial class ResourceState
{
    private readonly PassiveModifiers _passives;
    private readonly int _manaDamageShare;
    private readonly Queue<(int Tick, int Life)> _skillLifePayments = [];
    private int _resourceTick, _paidActionCount;
    private long _refundMana, _channelMana;
    private string? _paidChannel;
    private int _paidChannelTick, _channelPaymentMultiplier = 10_000;
    public int LastSkillPaymentMultiplier { get; private set; } = 10_000;
    public int LastSkillManaPaid { get; private set; }
    public int LastSkillLifePaid { get; private set; }
    private bool Mastery(string group, int option) => MasteryRuntime.Has(_passives, group, option);

    private int ShieldManaCost(int mana, SkillTag tags) => tags.HasFlag(SkillTag.Spell) && Mastery("能量护盾", 6) ? mana / 2 : 0;

    public bool CanPaySkillCost(int life, int mana, SkillTag tags, bool allowOvercharge = true, int extraShield = 0, bool waiveMana = false)
    {
        if (!IsAlive || life < 0 || mana < 0) return false;
        int shield = life > 0 ? 0 : ShieldManaCost(mana, tags);
        int available = allowOvercharge && tags.HasFlag(SkillTag.Spell) ? AvailableSpellMana : Mana;
        return Shield >= (long)shield + extraShield && (life > 0 ? Life > life : (waiveMana || available >= mana - shield));
    }

    /// <summary>Checks every resource before payment; only successful self casts advance cast-based masteries.</summary>
    public bool TryPaySkillCost(string skillId, int life, int mana, bool selfCast = true, bool allowOvercharge = true, int extraShield = 0, bool waiveMana = false)
    {
        SkillTag tags = SkillDefinitions.Get(skillId).Tags;
        LastSpellFullyFunded = false;
        LastSkillPaymentMultiplier = 10_000;
        LastSkillManaPaid = LastSkillLifePaid = 0;
        if (!CanPaySkillCost(life, mana, tags, allowOvercharge, extraShield, waiveMana)) return false;
        bool eligible = selfCast && (tags & (SkillTag.Attack | SkillTag.Spell)) != 0 &&
            (tags & (SkillTag.Trigger | SkillTag.Counter | SkillTag.Reservation)) == 0;
        bool channel = eligible && tags.HasFlag(SkillTag.Channelling);
        bool continuation = channel && _paidChannel == skillId && _resourceTick - _paidChannelTick <= 5;
        int multiplier = continuation ? _channelPaymentMultiplier :
            eligible && Mastery("法力", 1) && Mana * 10L > MaximumMana * 8L ? 14_000 : 10_000;
        if (eligible && !continuation) multiplier = (int)((long)multiplier * ConsumeEvadeCharge() / 10_000);
        int manaBefore = Mana;
        if (life > 0)
        {
            Life -= life;
            LastSkillLifePaid = life;
            if (Mastery("生命", 4)) _skillLifePayments.Enqueue((_resourceTick, life));
        }
        else
        {
            int shield = ShieldManaCost(mana, tags);
            Shield -= shield;
            if (!waiveMana)
            {
                if (tags.HasFlag(SkillTag.Spell)) TryPaySpellMana(mana - shield, allowOvercharge);
                else Mana -= mana;
            }
            if (shield > 0) LastSpellFullyFunded = false;
        }
        LastSkillManaPaid = manaBefore - Mana;
        LastSkillPaymentMultiplier = multiplier;
        if (!eligible) return true;
        if (!continuation) FinishPaidChannel();
        if (channel)
        {
            _paidChannel = skillId;
            _paidChannelTick = _resourceTick;
            _channelPaymentMultiplier = multiplier;
            _channelMana += LastSkillManaPaid;
        }
        else CompletePaidAction(LastSkillManaPaid);
        return true;
    }

    private void CompletePaidAction(long mana)
    {
        if (!Mastery("法力", 5)) return;
        _refundMana += mana;
        if (++_paidActionCount < 3) return;
        RestoreMana((int)Math.Min(int.MaxValue, _refundMana / 5));
        _paidActionCount = 0;
        _refundMana = 0;
    }

    private void FinishPaidChannel()
    {
        if (_paidChannel is null) return;
        CompletePaidAction(_channelMana);
        _paidChannel = null;
        _channelMana = 0;
    }

    private void AdvanceSkillPayments(int tick)
    {
        _resourceTick = tick;
        if (_paidChannel is not null && tick - _paidChannelTick > 5) FinishPaidChannel();
        while (_skillLifePayments.TryPeek(out var payment) && tick - payment.Tick >= 80) _skillLifePayments.Dequeue();
    }

    public bool HasRecentSkillLifePayment => Mastery("生命", 4) &&
        _skillLifePayments.Sum(payment => (long)payment.Life) * 20 >= MaximumLife;
}
