using System.Text.Json;

namespace GameForWork.Core.Persistence;

public sealed record GameSettings
{
    public bool AlwaysOnTop { get; init; } = true;
    public bool SnapEnabled { get; init; } = true;
    public int OpacityPercent { get; init; } = 100;
    public bool? CloseToTray { get; init; }
    public int StandardX { get; init; } = -1;
    public int StandardY { get; init; } = -1;
    public bool StartMini { get; init; }
}

public sealed class SettingsStore(string path)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public GameSettings Load()
    {
        if (!File.Exists(path))
        {
            return new GameSettings();
        }

        try
        {
            return JsonSerializer.Deserialize<GameSettings>(File.ReadAllText(path), JsonOptions) ?? new GameSettings();
        }
        catch (JsonException)
        {
            string invalidPath = path + $".invalid_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}";
            File.Move(path, invalidPath, overwrite: false);
            return new GameSettings();
        }
    }

    public void Save(GameSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(temporaryPath, path, overwrite: true);
    }
}
