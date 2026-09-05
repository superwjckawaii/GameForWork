namespace GameForWork.Core.Combat;

/// <summary>A single cooldown and independent, finite barrier pools for the party.</summary>
public sealed class TeamProtectionState(bool enabled)
{
    private sealed record Member(int Maximum, bool Alive, bool LowLife, int Barrier = 0);
    private readonly Dictionary<string, Member> _members = [];
    private int _ready, _expires;
    public int Barrier(string id, int tick) => tick < _expires && _members.TryGetValue(id, out var member) ? member.Barrier : 0;
    public void Remove(string id) => _members.Remove(id);
    public bool Update(string id, int life, int maximumLife, int maximumShield, bool affected, int tick)
    {
        if (!enabled) return false;
        bool low = life > 0 && life * 2L <= maximumLife;
        _members.TryGetValue(id, out var previous);
        _members[id] = new(maximumLife + maximumShield, life > 0, low, previous?.Barrier ?? 0);
        if (!affected || !low || previous?.LowLife == true || tick < _ready) return false;
        _ready = tick + 200;
        _expires = tick + 80;
        foreach (string key in _members.Keys.ToArray())
        {
            var member = _members[key];
            _members[key] = member with { Barrier = member.Alive ? member.Maximum / 5 : 0 };
        }
        return true;
    }
    public int Absorb(string id, int damage, bool hit, int tick)
    {
        int barrier = Barrier(id, tick);
        if (damage <= 0 || barrier <= 0) return damage;
        if (hit) damage = (int)((long)damage * 8_000 / 10_000);
        int absorbed = Math.Min(barrier, damage);
        _members[id] = _members[id] with { Barrier = barrier - absorbed };
        return damage - absorbed;
    }
}
