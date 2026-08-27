using System.Text.Json;

namespace GameForWork.Core.Diagnostics;

public enum GameLogLevel
{
    Debug,
    Information,
    Warning,
    Error,
}

public sealed class JsonLineLogger : IDisposable
{
    private const long MaximumBytes = 10L * 1024 * 1024;
    private const int MaximumFiles = 10;
    private readonly object _gate = new();
    private readonly string _directory;
    private readonly string _sessionId = Guid.NewGuid().ToString("N");
    private StreamWriter? _writer;

    public JsonLineLogger(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);
        OpenWriter();
    }

    public string CurrentLogPath { get; private set; } = string.Empty;

    public void Write(
        GameLogLevel level,
        string eventId,
        string subsystem,
        string message,
        IReadOnlyDictionary<string, object?>? properties = null,
        Exception? exception = null)
    {
        lock (_gate)
        {
            RotateIfNeeded();
            var entry = new
            {
                timestamp_utc = DateTimeOffset.UtcNow,
                level = level.ToString(),
                event_id = eventId,
                session_id = _sessionId,
                subsystem,
                message,
                properties = properties ?? new Dictionary<string, object?>(),
                exception = exception?.ToString(),
            };
            string line = JsonSerializer.Serialize(entry);
            _writer!.WriteLine(line);
            if (level == GameLogLevel.Error || exception is not null)
            {
                _writer.Flush();
            }
#if DEBUG
            Console.WriteLine(line);
#endif
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _writer?.Dispose();
            _writer = null;
        }

        GC.SuppressFinalize(this);
    }

    private void RotateIfNeeded()
    {
        if (_writer is null || !File.Exists(CurrentLogPath) || new FileInfo(CurrentLogPath).Length < MaximumBytes)
        {
            return;
        }

        _writer.Dispose();
        OpenWriter();
        string[] logs = Directory.EnumerateFiles(_directory, "game_*.jsonl")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToArray();
        foreach (string log in logs.Skip(MaximumFiles))
        {
            File.Delete(log);
        }
    }

    private void OpenWriter()
    {
        CurrentLogPath = Path.Combine(_directory, $"game_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss_fff}.jsonl");
        _writer = new StreamWriter(new FileStream(CurrentLogPath, FileMode.Append, FileAccess.Write, FileShare.Read))
        {
            AutoFlush = false,
        };
        TrimOldLogs();
    }

    private void TrimOldLogs()
    {
        string[] logs = Directory.EnumerateFiles(_directory, "game_*.jsonl")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToArray();
        foreach (string log in logs.Skip(MaximumFiles))
        {
            File.Delete(log);
        }
    }
}
