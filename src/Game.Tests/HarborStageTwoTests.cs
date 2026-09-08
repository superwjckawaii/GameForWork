using GameForWork.Core.Harbor;
namespace GameForWork.Tests;
public sealed class HarborStageTwoTests
{
 [Fact] public void FeesAndDifficultyAreFixed() { Assert.Equal(1000, HarborConfig.For(HarborDifficulty.First).Fee); Assert.Equal(3000, HarborConfig.For(HarborDifficulty.Second).Fee); Assert.Equal(8000, HarborConfig.For(HarborDifficulty.Third).Fee); }
 [Fact] public void SuccessfulRouteCreatesOneChestAndClaimIsAtomic() { var a=HarborAction.Start("a",1,HarborDifficulty.First,7,1000); a.Step(HarborCommand.Engage,1,0,0); a.Step(HarborCommand.Engage,1,0,100); a.Step(HarborCommand.Extract,1,0,200); Assert.Equal(HarborStatus.Succeeded,a.Snapshot.Status); var c=a.Snapshot.Chest!; Assert.Equal(3,c.Candidates.Count); Assert.NotNull(a.Claim(1)); Assert.Null(a.Claim(2)); }
 [Fact] public void FailureAndCancelNeverCreateChest() { var a=HarborAction.Start("a",1,HarborDifficulty.First,7,1000); a.Step(HarborCommand.Advance,1,200,0); Assert.Equal(HarborStatus.Failed,a.Snapshot.Status); Assert.Null(a.Snapshot.Chest); var b=HarborAction.Start("b",1,HarborDifficulty.First,8,1000); Assert.True(b.Cancel()); Assert.Null(b.Snapshot.Chest); }
 [Fact] public void SnapshotRestoreKeepsDeterministicEvents() { var a=HarborAction.Start("a",1,HarborDifficulty.First,9,1000); a.Step(HarborCommand.Engage,2,0,10); var b=HarborAction.Restore(a.Capture()); a.Step(HarborCommand.Engage,2,0,20); b.Step(HarborCommand.Engage,2,0,20); Assert.Equal(a.Capture().Status,b.Capture().Status); Assert.Equal(a.Capture().Region,b.Capture().Region); Assert.Equal(a.Events,b.Events); }
}

