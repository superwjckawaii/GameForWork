using GameForWork.Core.Combat;
using GameForWork.Core.Persistence;
using System.Text.Json;

namespace GameForWork.Tests;

public sealed class ReplayAndPersistenceTests
{
    [Fact]
    public void SnapshotRoundTripPreservesCanonicalHash()
    {
        BattleState state = BattleFactory.Create(123);
        var engine = new BattleEngine();
        engine.Step(state, engine.BuildAutomaticCommands(state));

        BattleState restored = BattleStateCodec.Deserialize(BattleStateCodec.Serialize(state));

        Assert.Equal(BattleStateCodec.Hash(state), BattleStateCodec.Hash(restored));
    }

    [Fact]
    public void SameCommandsReplayToSameHash()
    {
        var runner = new BattleRunner();
        BattleRun original = runner.RunAutomatic(321);
        BattleRun replay = runner.Replay(original.InitialState, original.Commands);
        Assert.Equal(original.FinalHash, replay.FinalHash);
    }

    [Fact]
    public void MutatedCommandChangesFinalHash()
    {
        var runner = new BattleRunner();
        BattleRun original = runner.RunAutomatic(444);
        BattleCommand[] mutated = original.Commands
            .Select((command, index) => index == 0 ? command with { Kind = BattleCommandKind.Wait } : command)
            .ToArray();

        BattleRun replay = runner.Replay(original.InitialState, mutated);

        Assert.NotEqual(original.FinalHash, replay.FinalHash);
    }

    [Fact]
    public void EnvelopeDetectsCorruption()
    {
        byte[] envelope = BinaryEnvelope.Wrap([1, 2, 3]);
        envelope[^1] ^= 0xff;
        Assert.Throws<InvalidDataException>(() => BinaryEnvelope.Unwrap(envelope));
    }

    [Fact]
    public void SqliteRepositoryRoundTripsSnapshotAndDeduplicatesOfflineInterval()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            using (var repository = new SaveRepository(root, 1))
            {
                repository.Initialize();
                byte[] payload = BattleStateCodec.Serialize(BattleFactory.Create(77));
                repository.SaveSnapshot(0, payload);

                Assert.Equal(payload, repository.LoadLatestSnapshot());
                Assert.True(repository.TryCommitOfflineSession("interval-1", 1, 2, 3, "{}"));
                Assert.False(repository.TryCommitOfflineSession("interval-1", 1, 2, 3, "{}"));
                Assert.Equal(SaveRepository.CurrentSchemaVersion, repository.GetSchemaVersion());
                Assert.True(SaveRepository.CheckIntegrity(repository.DatabasePath));
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CampaignSessionJsonUsesSchemaTwoSingletonState()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            using var repository = new SaveRepository(root, 1);
            repository.Initialize();
            Assert.Null(repository.LoadCampaignSessionJson());

            repository.SaveCampaignSessionJson("{\"version\":1}");
            repository.SaveCampaignSessionJson("{\"version\":2}");

            Assert.Equal("{\"version\":2}", repository.LoadCampaignSessionJson());
            Assert.Equal(SaveRepository.CurrentSchemaVersion, repository.GetSchemaVersion());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CorruptDatabaseIsRetainedAndLatestValidBackupIsRestored()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            byte[] expected = BattleStateCodec.Serialize(BattleFactory.Create(88));
            string databasePath;
            using (var repository = new SaveRepository(root, 1))
            {
                repository.Initialize();
                repository.SaveSnapshot(0, expected);
                repository.CreateBackup();
                databasePath = repository.DatabasePath;
            }

            File.WriteAllBytes(databasePath, "corrupt"u8.ToArray());
            using (var recovered = new SaveRepository(root, 1))
            {
                recovered.Initialize();
                Assert.Equal(expected, recovered.LoadLatestSnapshot());
                Assert.Single(Directory.EnumerateFiles(recovered.RecoveryDirectory, "corrupt_*.db"));
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AutomaticBackupSkipsARecentCopy()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            using var repository = new SaveRepository(root, 1);
            repository.Initialize();
            string first = repository.CreateAutomaticBackupIfDue(TimeSpan.FromHours(24))!;

            string? second = repository.CreateAutomaticBackupIfDue(TimeSpan.FromHours(24));

            Assert.True(File.Exists(first));
            Assert.Null(second);
            Assert.Single(Directory.EnumerateFiles(repository.BackupDirectory, "auto_*.db"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void StartupNormalizesRunawayLegacyMapIdsWithoutDroppingMaps()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            string runaway = string.Concat(Enumerable.Repeat("map-", 80)) + "source";
            using (var repository = new SaveRepository(root, 1))
            {
                repository.Initialize();
                repository.SaveCampaignSessionJson(JsonSerializer.Serialize(new
                {
                    World = new
                    {
                        MapInventory = new[]
                        {
                            new { InstanceId = runaway, AreaLevel = 7 },
                            new { InstanceId = "normal-map", AreaLevel = 8 },
                        },
                    },
                }));
            }

            using var reopened = new SaveRepository(root, 1);
            reopened.Initialize();
            using JsonDocument document = JsonDocument.Parse(reopened.LoadCampaignSessionJson()!);
            JsonElement maps = document.RootElement.GetProperty("World").GetProperty("MapInventory");

            Assert.Equal(2, maps.GetArrayLength());
            Assert.StartsWith("legacy-map-", maps[0].GetProperty("InstanceId").GetString());
            Assert.Equal("normal-map", maps[1].GetProperty("InstanceId").GetString());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DeletedSaveCanBeRestoredFromTrashWithoutOverwrite()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            byte[] expected = BattleStateCodec.Serialize(BattleFactory.Create(89));
            using (var repository = new SaveRepository(root, 1))
            {
                repository.Initialize();
                repository.SaveSnapshot(0, expected);
                string trashEntry = repository.MoveToTrash();
                repository.RestoreFromTrash(trashEntry);
                Assert.Equal(expected, repository.LoadLatestSnapshot());
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "GameForWork.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
