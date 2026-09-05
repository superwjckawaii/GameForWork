namespace GameForWork.Core.Ascendancies;

public enum PhantomReplayMode { Sequential, Focus, Reverse }
public enum ConstructModule { Firepower, Guardian, LongRange, Stabilizer, ExplosiveCore, Reforge }

public sealed record CombatConfiguration(PhantomReplayMode PhantomMode = PhantomReplayMode.Sequential,
    IReadOnlyList<ConstructModule>? Modules = null)
{
    private static readonly ConstructModule[] DefaultModules = [ConstructModule.Firepower, ConstructModule.Guardian];
    public bool Has(ConstructModule module) => (Modules ?? DefaultModules).Contains(module);
    public bool Valid => Enum.IsDefined(PhantomMode) && (Modules is null ||
        Modules.Count == 2 && Modules.Distinct().Count() == Modules.Count && Modules.All(Enum.IsDefined));
    public CombatConfiguration Snapshot() => this with { Modules = Modules?.ToArray() };
}
