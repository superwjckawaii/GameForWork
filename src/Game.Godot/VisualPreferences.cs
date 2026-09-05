namespace GameForWork.GodotClient;

public enum EffectDensity { Low, Medium, High }
public enum DamageNumberMode { Off, Merged, Full }

public static class VisualPreferences
{
    public static EffectDensity EffectDensity { get; set; } = EffectDensity.Medium;
    public static DamageNumberMode DamageNumbers { get; set; } = DamageNumberMode.Merged;
    public static bool ScreenShake { get; set; }

    public static EffectDensity EffectiveDensity(float width) => width < 620
        ? EffectDensity.Low
        : EffectDensity;

    public static int EffectLimit(float width) => EffectiveDensity(width) switch
    {
        EffectDensity.Low => 8,
        EffectDensity.Medium => 18,
        _ => 32,
    };
}
