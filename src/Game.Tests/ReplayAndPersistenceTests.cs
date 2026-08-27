using GameForWork.Core.Combat;
using GameForWork.Core.Persistence;

namespace GameForWork.Tests;

public sealed class ReplayAndPersistenceTests
{
    [Fact]
    public void SnapshotRoundTripPreservesCanonicalHash()
    {
        BattleState state = P0BattleFactory.Create(123);
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
                byte[] payload = BattleStateCodec.Serialize(P0BattleFactory.Create(77));
                repository.SaveSnapshot(0, payload);

                Assert.Equal(payload, repository.LoadLatestSnapshot());
                Assert.True(repository.TryCommitOfflineSession("interval-1", 1, 2, 3, "{}"));
                Assert.False(repository.TryCommitOfflineSession("interval-1", 1, 2, 3, "{}"));
                Assert.True(SaveRepository.CheckIntegrity(repository.DatabasePath));
            }
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
            byte[] expected = BattleStateCodec.Serialize(P0BattleFactory.Create(88));
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
    public void DeletedSaveCanBeRestoredFromTrashWithoutOverwrite()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            byte[] expected = BattleStateCodec.Serialize(P0BattleFactory.Create(89));
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
