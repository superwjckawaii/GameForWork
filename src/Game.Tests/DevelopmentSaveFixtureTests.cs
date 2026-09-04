using System.Text.Json;
using GameForWork.Core.P1;
using GameForWork.Core.Persistence;

namespace GameForWork.Tests;

public sealed class DevelopmentSaveFixtureTests
{
    [Fact]
    public void SlotOneFixtureLoadsThroughCurrentRuntime()
    {
        string fixtureDirectory = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "development-save-slot-01");
        string temporaryRoot = Path.Combine(
            Path.GetTempPath(),
            "GameForWork.Tests",
            Guid.NewGuid().ToString("N"));
        string temporarySlot = Path.Combine(temporaryRoot, "slot_01");

        Directory.CreateDirectory(temporarySlot);
        try
        {
            File.Copy(
                Path.Combine(fixtureDirectory, "save.db"),
                Path.Combine(temporarySlot, "save.db"));
            File.Copy(
                Path.Combine(fixtureDirectory, "manifest.json"),
                Path.Combine(temporarySlot, "manifest.json"));

            using var repository = new SaveRepository(temporaryRoot, 1);
            repository.Initialize();

            Assert.True(SaveRepository.CheckIntegrity(repository.DatabasePath));
            Assert.Equal(SaveRepository.CurrentSchemaVersion, repository.GetSchemaVersion());

            string json = Assert.IsType<string>(repository.LoadP1SessionJson());
            P1GameSessionSnapshot snapshot = Assert.IsType<P1GameSessionSnapshot>(
                JsonSerializer.Deserialize<P1GameSessionSnapshot>(json));
            P1GameSession restored = P1GameSession.Restore(snapshot);

            Assert.Equal(P1GameSession.CurrentFormatVersion, snapshot.FormatVersion);
            Assert.Equal(snapshot.Seed, restored.Seed);
        }
        finally
        {
            Directory.Delete(temporaryRoot, recursive: true);
        }
    }
}
