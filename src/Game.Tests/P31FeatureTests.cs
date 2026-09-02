using GameForWork.Core.P30;
using GameForWork.Core.P31;

namespace GameForWork.Tests;

public sealed class P31FeatureTests
{
    [Fact]
    public void EveryActiveSkillHasOneStableVisualDescriptor()
    {
        Assert.Equal(86, P31VisualCatalog.Skills.Count);
        Assert.Equal(86, P31VisualCatalog.Skills.Select(item => item.SkillId).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(18, P31VisualCatalog.Skills.Count(item => item.Signature));
        Assert.All(P30SkillCatalog.Active, active =>
        {
            P31SkillVisualDescriptor visual = P31VisualCatalog.ForSkill(active.Combat.SkillId);
            Assert.InRange(visual.AtlasCell, 0, 15);
            Assert.InRange(visual.LifetimeMilliseconds, 500, 1_200);
            Assert.True(visual.ScaleBasisPoints > 0);
        });
    }

    [Fact]
    public void EverySupportHasAVisibleMechanicLayer()
    {
        Assert.Equal(98, P31VisualCatalog.Supports.Count);
        Assert.All(P31VisualCatalog.Supports, support => Assert.NotEqual(P31SupportVisualLayer.None, support.Layer));
        Assert.Contains(P31VisualCatalog.Supports, item => item.Layer.HasFlag(P31SupportVisualLayer.ExtraProjectiles));
        Assert.Contains(P31VisualCatalog.Supports, item => item.Layer.HasFlag(P31SupportVisualLayer.CriticalFlash));
        Assert.Contains(P31VisualCatalog.Supports, item => item.Layer.HasFlag(P31SupportVisualLayer.AilmentTrail));
    }

    [Fact]
    public void SkillVisualLookupIsDeterministic()
    {
        foreach (P30ActiveSkillDefinition active in P30SkillCatalog.Active)
            Assert.Same(P31VisualCatalog.ForSkill(active.Combat.SkillId), P31VisualCatalog.ForSkill(active.Combat.SkillId));
    }
}
