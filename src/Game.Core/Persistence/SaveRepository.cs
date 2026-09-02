using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace GameForWork.Core.Persistence;

public sealed class SaveRepository : IDisposable
{
    public const int CurrentSchemaVersion = 4;
    private const int AutoBackupLimit = 5;
    private const int RecoveryLimit = 10;
    private readonly object _backupSync = new();
    private readonly string _slotDirectory;
    private readonly string _databasePath;
    private SqliteConnection? _connection;

    public SaveRepository(string savesRoot, int slot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(savesRoot);
        if (slot is < 1 or > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(slot), "Save slots range from 1 through 3.");
        }

        _slotDirectory = Path.Combine(savesRoot, $"slot_{slot:00}");
        _databasePath = Path.Combine(_slotDirectory, "save.db");
    }

    public string DatabasePath => _databasePath;
    public string BackupDirectory => Path.Combine(_slotDirectory, "backups");
    public string RecoveryDirectory => Path.Combine(_slotDirectory, "recovery");
    public string LegacyRecoveryDirectory => Path.Combine(RecoveryDirectory, "legacy");
    public string TrashDirectory => Path.Combine(_slotDirectory, "trash");

    public void Initialize()
    {
        Directory.CreateDirectory(_slotDirectory);
        Directory.CreateDirectory(BackupDirectory);
        Directory.CreateDirectory(RecoveryDirectory);
        Directory.CreateDirectory(LegacyRecoveryDirectory);
        Directory.CreateDirectory(TrashDirectory);
        EnsureManifest();
        PurgeExpiredTrash();

        if (File.Exists(_databasePath) && !CheckStartupHealth(_databasePath))
        {
            RecoverCorruptDatabase();
        }

        OpenConnection();
        ApplyMigrations();
        NormalizeOversizedMapIds();
    }

    public void SaveSnapshot(int tick, ReadOnlySpan<byte> payload)
    {
        SqliteConnection connection = RequireConnection();
        byte[] envelope = BinaryEnvelope.Wrap(payload);
        using SqliteTransaction transaction = connection.BeginTransaction();
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO snapshots(created_utc_ms, tick, format_version, payload) VALUES ($time, $tick, $version, $payload);";
        command.Parameters.AddWithValue("$time", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue("$tick", tick);
        command.Parameters.AddWithValue("$version", BinaryEnvelope.Version);
        command.Parameters.AddWithValue("$payload", envelope);
        command.ExecuteNonQuery();

        using SqliteCommand meta = connection.CreateCommand();
        meta.Transaction = transaction;
        meta.CommandText = "UPDATE save_meta SET last_observed_utc_ms = $time WHERE id = 1;";
        meta.Parameters.AddWithValue("$time", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        meta.ExecuteNonQuery();
        transaction.Commit();
    }

    public byte[]? LoadLatestSnapshot()
    {
        using SqliteCommand command = RequireConnection().CreateCommand();
        command.CommandText = "SELECT payload FROM snapshots ORDER BY id DESC LIMIT 1;";
        object? value = command.ExecuteScalar();
        return value is byte[] envelope ? BinaryEnvelope.Unwrap(envelope) : null;
    }

    public bool TryCommitOfflineSession(
        string intervalId,
        long startUtcMs,
        long endUtcMs,
        int battles,
        string resultJson)
    {
        using SqliteTransaction transaction = RequireConnection().BeginTransaction();
        using SqliteCommand command = RequireConnection().CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT OR IGNORE INTO offline_sessions(interval_id, start_utc_ms, end_utc_ms, battles, result_json)
            VALUES ($id, $start, $end, $battles, $result);
            """;
        command.Parameters.AddWithValue("$id", intervalId);
        command.Parameters.AddWithValue("$start", startUtcMs);
        command.Parameters.AddWithValue("$end", endUtcMs);
        command.Parameters.AddWithValue("$battles", battles);
        command.Parameters.AddWithValue("$result", resultJson);
        bool inserted = command.ExecuteNonQuery() == 1;
        using SqliteCommand meta = RequireConnection().CreateCommand();
        meta.Transaction = transaction;
        meta.CommandText = "UPDATE save_meta SET last_observed_utc_ms = $end WHERE id = 1;";
        meta.Parameters.AddWithValue("$end", endUtcMs);
        meta.ExecuteNonQuery();
        transaction.Commit();
        return inserted;
    }

    public long GetLastObservedUtcMs()
    {
        using SqliteCommand command = RequireConnection().CreateCommand();
        command.CommandText = "SELECT last_observed_utc_ms FROM save_meta WHERE id = 1;";
        object? value = command.ExecuteScalar();
        return value is long timestamp
            ? timestamp
            : throw new InvalidDataException("Save metadata does not contain a last-observed timestamp.");
    }

    public int GetSchemaVersion()
    {
        using SqliteCommand command = RequireConnection().CreateCommand();
        command.CommandText = "SELECT schema_version FROM save_meta WHERE id = 1;";
        return Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    public string? LoadP1SessionJson()
    {
        using SqliteCommand command = RequireConnection().CreateCommand();
        command.CommandText = "SELECT state_json FROM p1_state WHERE id = 1;";
        return command.ExecuteScalar() as string;
    }

    public void SaveP1SessionJson(string stateJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateJson);
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        using SqliteTransaction transaction = RequireConnection().BeginTransaction();
        using SqliteCommand command = RequireConnection().CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO p1_state(id, updated_utc_ms, state_json) VALUES (1, $now, $json)
            ON CONFLICT(id) DO UPDATE SET updated_utc_ms = excluded.updated_utc_ms, state_json = excluded.state_json;
            UPDATE save_meta SET last_observed_utc_ms = $now WHERE id = 1;
            """;
        command.Parameters.AddWithValue("$now", now);
        command.Parameters.AddWithValue("$json", stateJson);
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    public string CreateBackup(bool manual = false)
    {
        lock (_backupSync)
        {
            return CreateBackupCore(manual);
        }
    }

    public string? CreateAutomaticBackupIfDue(TimeSpan minimumInterval)
    {
        if (minimumInterval < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumInterval));
        }

        lock (_backupSync)
        {
            string? latest = Directory.EnumerateFiles(BackupDirectory, "auto_*.db")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
            if (latest is not null && DateTime.UtcNow - File.GetLastWriteTimeUtc(latest) < minimumInterval)
            {
                return null;
            }

            return CreateBackupCore(manual: false);
        }
    }

    private string CreateBackupCore(bool manual)
    {
        if (!File.Exists(_databasePath))
        {
            throw new InvalidOperationException("The save database does not exist.");
        }

        Directory.CreateDirectory(BackupDirectory);
        string kind = manual ? "manual" : "auto";
        string path = Path.Combine(BackupDirectory, $"{kind}_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss_fff}.db");
        string temporaryPath = path + ".tmp";
        try
        {
            var sourceBuilder = new SqliteConnectionStringBuilder
            {
                DataSource = _databasePath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false,
            };
            var destinationBuilder = new SqliteConnectionStringBuilder
            {
                DataSource = temporaryPath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = false,
            };
            using var source = new SqliteConnection(sourceBuilder.ToString());
            using var destination = new SqliteConnection(destinationBuilder.ToString());
            source.Open();
            destination.Open();
            source.BackupDatabase(destination);
            destination.Close();
            File.Move(temporaryPath, path, overwrite: false);
        }
        catch
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            throw;
        }

        if (!manual) TrimFiles(BackupDirectory, "auto_*.db", AutoBackupLimit);

        return path;
    }

    public string MoveToTrash()
    {
        _connection?.Dispose();
        _connection = null;
        string entry = Path.Combine(TrashDirectory, $"deleted_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss_fff}");
        Directory.CreateDirectory(entry);
        foreach (string suffix in new[] { string.Empty, "-wal", "-shm" })
        {
            string source = _databasePath + suffix;
            if (File.Exists(source))
            {
                File.Move(source, Path.Combine(entry, "save.db" + suffix));
            }
        }

        return entry;
    }

    public string ArchiveLegacyAndReset()
    {
        if (_connection is not null)
        {
            using SqliteCommand checkpoint = _connection.CreateCommand();
            checkpoint.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            checkpoint.ExecuteNonQuery();
        }
        _connection?.Dispose();
        _connection = null;
        Directory.CreateDirectory(LegacyRecoveryDirectory);
        string archived = Path.Combine(LegacyRecoveryDirectory,
            $"legacy_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss_fff}.db");
        foreach (string suffix in new[] { string.Empty, "-wal", "-shm" })
        {
            string source = _databasePath + suffix;
            if (File.Exists(source)) File.Move(source, archived + suffix);
        }
        TrimDatabaseFamilies(LegacyRecoveryDirectory, "legacy_*.db", RecoveryLimit);
        OpenConnection();
        ApplyMigrations();
        return archived;
    }

    public void RestoreFromTrash(string entryDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entryDirectory);
        string fullEntry = Path.GetFullPath(entryDirectory);
        string fullTrash = Path.GetFullPath(TrashDirectory) + Path.DirectorySeparatorChar;
        if (!fullEntry.StartsWith(fullTrash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Trash restore path is outside this save slot.");
        }

        string source = Path.Combine(fullEntry, "save.db");
        if (!File.Exists(source) || !CheckIntegrity(source))
        {
            throw new InvalidDataException("Trash entry does not contain a valid save database.");
        }

        if (File.Exists(_databasePath))
        {
            throw new IOException("A current save already exists; it will not be overwritten by trash restore.");
        }

        File.Copy(source, _databasePath, overwrite: false);
        OpenConnection();
    }

    public bool RestoreLatestValidBackup()
    {
        string? backup = Directory.EnumerateFiles(BackupDirectory, "*.db")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault(CheckIntegrity);
        if (backup is null)
        {
            return false;
        }

        _connection?.Dispose();
        _connection = null;
        File.Copy(backup, _databasePath, overwrite: true);
        OpenConnection();
        return true;
    }

    public void Dispose()
    {
        _connection?.Dispose();
        _connection = null;
        GC.SuppressFinalize(this);
    }

    public static bool CheckIntegrity(string databasePath)
    {
        try
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false,
            };
            using var connection = new SqliteConnection(builder.ToString());
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "PRAGMA integrity_check;";
            return string.Equals(command.ExecuteScalar()?.ToString(), "ok", StringComparison.OrdinalIgnoreCase);
        }
        catch (SqliteException)
        {
            return false;
        }
    }

    private static bool CheckStartupHealth(string databasePath)
    {
        try
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false,
            };
            using var connection = new SqliteConnection(builder.ToString());
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT schema_version FROM save_meta WHERE id = 1;";
            return command.ExecuteScalar() is not null;
        }
        catch (SqliteException)
        {
            return false;
        }
    }

    private void RecoverCorruptDatabase()
    {
        string recoveryBase = Path.Combine(RecoveryDirectory, $"corrupt_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss_fff}.db");
        foreach (string suffix in new[] { string.Empty, "-wal", "-shm" })
        {
            string source = _databasePath + suffix;
            if (File.Exists(source))
            {
                File.Move(source, recoveryBase + suffix);
            }
        }

        TrimRecoveryFiles();
        string? validBackup = Directory.EnumerateFiles(BackupDirectory, "*.db")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault(CheckIntegrity);
        if (validBackup is null)
        {
            throw new InvalidDataException($"Save database is corrupt and no valid backup exists. Recovery copy: {recoveryBase}");
        }

        File.Copy(validBackup, _databasePath, overwrite: false);
    }

    private void OpenConnection()
    {
        _connection?.Dispose();
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = false,
        };
        _connection = new SqliteConnection(builder.ToString());
        _connection.Open();
        foreach (string pragma in new[]
        {
            "PRAGMA journal_mode=WAL;",
            "PRAGMA synchronous=FULL;",
            "PRAGMA foreign_keys=ON;",
            "PRAGMA busy_timeout=5000;",
        })
        {
            using SqliteCommand command = _connection.CreateCommand();
            command.CommandText = pragma;
            command.ExecuteNonQuery();
        }
    }

    private void ApplyMigrations()
    {
        SqliteConnection connection = RequireConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS schema_migrations(version INTEGER PRIMARY KEY, applied_utc_ms INTEGER NOT NULL);
            CREATE TABLE IF NOT EXISTS save_meta(
                id INTEGER PRIMARY KEY CHECK(id = 1),
                schema_version INTEGER NOT NULL,
                created_utc_ms INTEGER NOT NULL,
                last_observed_utc_ms INTEGER NOT NULL
            );
            CREATE TABLE IF NOT EXISTS snapshots(
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                created_utc_ms INTEGER NOT NULL,
                tick INTEGER NOT NULL,
                format_version INTEGER NOT NULL,
                payload BLOB NOT NULL
            );
            CREATE TABLE IF NOT EXISTS battle_runs(
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                seed TEXT NOT NULL,
                outcome INTEGER NOT NULL,
                final_hash TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS battle_commands(
                run_id INTEGER NOT NULL REFERENCES battle_runs(id) ON DELETE CASCADE,
                ordinal INTEGER NOT NULL,
                payload BLOB NOT NULL,
                PRIMARY KEY(run_id, ordinal)
            );
            CREATE TABLE IF NOT EXISTS battle_events(
                run_id INTEGER NOT NULL REFERENCES battle_runs(id) ON DELETE CASCADE,
                ordinal INTEGER NOT NULL,
                payload BLOB NOT NULL,
                PRIMARY KEY(run_id, ordinal)
            );
            CREATE TABLE IF NOT EXISTS offline_sessions(
                interval_id TEXT PRIMARY KEY,
                start_utc_ms INTEGER NOT NULL,
                end_utc_ms INTEGER NOT NULL,
                battles INTEGER NOT NULL,
                result_json TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS characters(
                stable_id TEXT PRIMARY KEY,
                kind INTEGER NOT NULL,
                name TEXT NOT NULL,
                state_json TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS equipped_items(
                character_id TEXT NOT NULL REFERENCES characters(stable_id) ON DELETE CASCADE,
                slot INTEGER NOT NULL,
                instance_id TEXT NOT NULL,
                item_json TEXT NOT NULL,
                PRIMARY KEY(character_id, slot)
            );
            CREATE TABLE IF NOT EXISTS storage_items(
                instance_id TEXT PRIMARY KEY,
                item_json TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS town_state(
                id INTEGER PRIMARY KEY CHECK(id = 1),
                state_json TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS map_queues(
                team INTEGER NOT NULL,
                ordinal INTEGER NOT NULL,
                map_json TEXT NOT NULL,
                PRIMARY KEY(team, ordinal)
            );
            CREATE TABLE IF NOT EXISTS p1_state(
                id INTEGER PRIMARY KEY CHECK(id = 1),
                updated_utc_ms INTEGER NOT NULL,
                state_json TEXT NOT NULL
            );
            INSERT OR IGNORE INTO schema_migrations(version, applied_utc_ms) VALUES (1, $now);
            INSERT OR IGNORE INTO schema_migrations(version, applied_utc_ms) VALUES (2, $now);
            INSERT OR IGNORE INTO schema_migrations(version, applied_utc_ms) VALUES (3, $now);
            INSERT OR IGNORE INTO schema_migrations(version, applied_utc_ms) VALUES (4, $now);
            INSERT OR IGNORE INTO save_meta(id, schema_version, created_utc_ms, last_observed_utc_ms)
            VALUES (1, $schema, $now, $now);
            UPDATE save_meta SET schema_version = $schema WHERE id = 1 AND schema_version < $schema;
            """;
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue("$schema", CurrentSchemaVersion);
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    private void NormalizeOversizedMapIds()
    {
        SqliteConnection connection = RequireConnection();
        using (SqliteCommand validJson = connection.CreateCommand())
        {
            validJson.CommandText = "SELECT COALESCE((SELECT json_valid(state_json) FROM p1_state WHERE id = 1), 0);";
            if (Convert.ToInt32(validJson.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) == 0)
            {
                return;
            }
        }

        using SqliteCommand countCommand = connection.CreateCommand();
        countCommand.CommandText = """
            SELECT COUNT(*)
            FROM json_each(json_extract((SELECT state_json FROM p1_state WHERE id = 1), '$.World.MapInventory'))
            WHERE length(CAST(json_extract(value, '$.InstanceId') AS TEXT)) > 128;
            """;
        int oversized = Convert.ToInt32(countCommand.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
        if (oversized == 0)
        {
            return;
        }

        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        using SqliteTransaction transaction = connection.BeginTransaction();
        using SqliteCommand normalize = connection.CreateCommand();
        normalize.Transaction = transaction;
        normalize.CommandText = """
            WITH normalized(maps) AS (
                SELECT json_group_array(json(
                    CASE
                        WHEN length(CAST(json_extract(value, '$.InstanceId') AS TEXT)) > 128
                        THEN json_set(value, '$.InstanceId',
                            'legacy-map-' || $token || '-' || printf('%06d', CAST(key AS INTEGER)))
                        ELSE value
                    END))
                FROM json_each(json_extract((SELECT state_json FROM p1_state WHERE id = 1), '$.World.MapInventory'))
            )
            UPDATE p1_state
            SET state_json = json_set(state_json, '$.World.MapInventory', json((SELECT maps FROM normalized))),
                updated_utc_ms = $now
            WHERE id = 1;
            """;
        normalize.Parameters.AddWithValue("$token", now.ToString("x", System.Globalization.CultureInfo.InvariantCulture));
        normalize.Parameters.AddWithValue("$now", now);
        normalize.ExecuteNonQuery();
        transaction.Commit();
    }

    private SqliteConnection RequireConnection() =>
        _connection ?? throw new InvalidOperationException("SaveRepository.Initialize must be called first.");

    private void EnsureManifest()
    {
        string path = Path.Combine(_slotDirectory, "manifest.json");
        if (File.Exists(path))
        {
            return;
        }

        string temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(new
        {
            format_version = 1,
            created_utc_ms = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            database = "save.db",
        }, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporaryPath, path, overwrite: false);
    }

    private void PurgeExpiredTrash()
    {
        DateTime threshold = DateTime.UtcNow.AddDays(-7);
        foreach (string entry in Directory.EnumerateDirectories(TrashDirectory))
        {
            if (Directory.GetLastWriteTimeUtc(entry) < threshold)
            {
                Directory.Delete(entry, recursive: true);
            }
        }
    }

    private static void TrimFiles(string directory, string pattern, int maximum)
    {
        string[] files = Directory.EnumerateFiles(directory, pattern)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToArray();
        foreach (string file in files.Skip(maximum))
        {
            File.Delete(file);
        }
    }

    private void TrimRecoveryFiles()
    {
        TrimDatabaseFamilies(RecoveryDirectory, "corrupt_*.db", RecoveryLimit);
    }

    private static void TrimDatabaseFamilies(string directory, string pattern, int maximum)
    {
        string[] databases = Directory.EnumerateFiles(directory, pattern)
            .OrderByDescending(File.GetLastWriteTimeUtc).ToArray();
        foreach (string database in databases.Skip(maximum))
        {
            foreach (string suffix in new[] { string.Empty, "-wal", "-shm" })
            {
                string path = database + suffix;
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
    }
}
