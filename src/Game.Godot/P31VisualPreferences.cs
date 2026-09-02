namespace GameForWork.GodotClient;

public enum P31EffectDensity { Low, Medium, High }
public enum P31DamageNumberMode { Off, Merged, Full }

public static class P31VisualPreferences
{
    public static P31EffectDensity EffectDensity { get; set; } = P31EffectDensity.Medium;
    public static P31DamageNumberMode DamageNumbers { get; set; } = P31DamageNumberMode.Merged;
    public static bool ScreenShake { get; set; }

    public static P31EffectDensity EffectiveDensity(float width) => width < 620
        ? P31EffectDensity.Low
        : EffectDensity;

    public static int EffectLimit(float width) => EffectiveDensity(width) switch
    {
        P31EffectDensity.Low => 8,
        P31EffectDensity.Medium => 18,
        _ => 32,
    };
}
