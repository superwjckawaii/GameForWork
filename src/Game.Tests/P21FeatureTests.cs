using GameForWork.Core.P14;
using GameForWork.Core.P21;
using GameForWork.Core.P24;

namespace GameForWork.Tests;

public sealed class P21FeatureTests
{
    [Fact]
    public void AssetContractCoversAllStableContent()
    {
        Assert.Equal(48, P21ArtContract.EnemyIds.Count);
        Assert.Equal(78, P21ArtContract.SkillStoneIds.Count);
        Assert.Equal(31, P21ArtContract.AnimationRanges.Sum(range => range.FrameCount));
        Assert.Equal(P21ArtContract.AnimationColumns,
            P21ArtContract.AnimationRanges.Max(range => range.StartColumn + range.FrameCount));
        Assert.Equal(P21ArtContract.EnemyIds.Count, P21ArtContract.EnemyIds.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(P21ArtContract.SkillStoneIds.Count, P21ArtContract.SkillStoneIds.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void EveryAnimationActionStaysInsideItsDeclaredColumns()
    {
        foreach (P21SpriteAction action in Enum.GetValues<P21SpriteAction>())
        {
            int first = P21ArtContract.AnimationColumn(action, 0);
            int later = P21ArtContract.AnimationColumn(action, 123_456);
            P21AnimationRange range = P21ArtContract.AnimationRanges.Single(candidate => candidate.Action == action);
            Assert.InRange(first, range.StartColumn, range.StartColumn + range.FrameCount - 1);
            Assert.InRange(later, range.StartColumn, range.StartColumn + range.FrameCount - 1);
        }
    }

    [Fact]
    public void BossesAndEnemiesResolveToValidRigs()
    {
        foreach (string enemy in P21ArtContract.EnemyIds)
            Assert.InRange(P21ArtContract.EnemyRig(enemy), 0, P21ArtContract.EnemyBodyRigCount - 1);

        foreach (string boss in P14Bosses.MapBosses.Select(item => item.StableId)
                     .Concat([P14Bosses.Breakthrough.StableId])
                     .Concat(P14Bosses.CitadelStages.Select(item => item.StableId)))
            Assert.InRange(P21ArtContract.BossRig(boss), 0, P21ArtContract.BossRigCount - 1);
    }

    [Fact]
    public void P24SkillStonesUseDeterministicSemanticAtlasRows()
    {
        IEnumerable<string> stones = P24SkillCatalog.Active.Select(item => item.Combat.StoneId)
            .Concat(P24SkillCatalog.Supports.Select(item => item.StoneId));
        foreach (string stableId in stones)
        {
            int first = P21ArtContract.SkillStoneIndex(stableId);
            Assert.InRange(first, 0, 89);
            Assert.Equal(first, P21ArtContract.SkillStoneIndex(stableId));
        }
    }
}
