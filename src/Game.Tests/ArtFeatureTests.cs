using GameForWork.Core.Content;
using GameForWork.Core.Art;
using GameForWork.Core.Archetypes;

namespace GameForWork.Tests;

public sealed class ArtFeatureTests
{
    [Fact]
    public void AssetContractCoversAllStableContent()
    {
        Assert.Equal(80, ArtContract.EnemyIds.Count);
        Assert.Equal(78, ArtContract.SkillStoneIds.Count);
        Assert.Equal(31, ArtContract.AnimationRanges.Sum(range => range.FrameCount));
        Assert.Equal(ArtContract.AnimationColumns,
            ArtContract.AnimationRanges.Max(range => range.StartColumn + range.FrameCount));
        Assert.Equal(ArtContract.EnemyIds.Count, ArtContract.EnemyIds.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(ArtContract.SkillStoneIds.Count, ArtContract.SkillStoneIds.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void EveryAnimationActionStaysInsideItsDeclaredColumns()
    {
        foreach (SpriteAction action in Enum.GetValues<SpriteAction>())
        {
            int first = ArtContract.AnimationColumn(action, 0);
            int later = ArtContract.AnimationColumn(action, 123_456);
            AnimationRange range = ArtContract.AnimationRanges.Single(candidate => candidate.Action == action);
            Assert.InRange(first, range.StartColumn, range.StartColumn + range.FrameCount - 1);
            Assert.InRange(later, range.StartColumn, range.StartColumn + range.FrameCount - 1);
        }
    }

    [Fact]
    public void BossesAndEnemiesResolveToValidRigs()
    {
        foreach (string enemy in ArtContract.EnemyIds)
            Assert.InRange(ArtContract.EnemyRig(enemy), 0, ArtContract.EnemyBodyRigCount - 1);

        foreach (string boss in Bosses.MapBosses.Select(item => item.StableId)
                     .Concat([Bosses.Breakthrough.StableId])
                     .Concat(Bosses.CitadelStages.Select(item => item.StableId)))
            Assert.InRange(ArtContract.BossRig(boss), 0, ArtContract.BossRigCount - 1);
    }

    [Fact]
    public void ArchetypesSkillStonesUseDeterministicSemanticAtlasRows()
    {
        IEnumerable<string> stones = ArchetypeSkillDefinitions.Active.Select(item => item.Combat.StoneId)
            .Concat(ArchetypeSkillDefinitions.Supports.Select(item => item.StoneId));
        foreach (string stableId in stones)
        {
            int first = ArtContract.SkillStoneIndex(stableId);
            Assert.InRange(first, 0, GameForWork.Core.Equipment.SkillStoneArt.StableIds.Count - 1);
            Assert.Equal(first, ArtContract.SkillStoneIndex(stableId));
        }
    }
}
