namespace GameForWork.Core.Combat;

public sealed record CurseInstance(string Id, int Effect, int SecondaryEffect, int Expires, long Sequence);
public sealed class CurseState
{
    private readonly Dictionary<string, CurseInstance> _active = [];
    private long _sequence;
    public IReadOnlyCollection<CurseInstance> Active(int tick) => _active.Values.Where(curse => curse.Expires > tick).ToArray();
    public int Effect(string id, int tick) => _active.TryGetValue(id, out var curse) && curse.Expires > tick ? curse.Effect : 0;
    public int Secondary(string id, int tick) => _active.TryGetValue(id, out var curse) && curse.Expires > tick ? curse.SecondaryEffect : 0;
    public void Remove(string id) => _active.Remove(id);
    public void Apply(string id, int effect, int secondary, int expires, int maximum, int tick)
    {
        foreach (var expired in _active.Values.Where(curse => curse.Expires <= tick).ToArray()) _active.Remove(expired.Id);
        if (maximum <= 0) return;
        if (!_active.ContainsKey(id))
            while (_active.Count >= maximum) _active.Remove(_active.Values.MinBy(curse => curse.Sequence)!.Id);
        _active[id] = new(id, effect, secondary, expires, ++_sequence);
    }
}
