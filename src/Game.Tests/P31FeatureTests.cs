using GameForWork.Core.P1.Progression;
using GameForWork.Core.P10;
using GameForWork.Core.P18;
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

    [Fact]
    public void BakedTreePixelsAndRuntimeWorldPositionsUseTheSameProjection()
    {
        AssertProjection(P1PassiveTree.Nodes.Select(node => (node.X, node.Y)),
            P1PassiveTree.LayoutExtent, P31TreeProjection.PassiveSourceSize);
        AssertProjection(P10AtlasTree.Nodes.Select(node => (node.X, node.Y)),
            P10AtlasTree.LayoutExtent, P31TreeProjection.AtlasSourceSize);
        AssertProjection(P18AscendancyCatalog.Nodes.Select(node => ((float)node.X, (float)node.Y)),
            240f, P31TreeProjection.AscendancySourceSize);
    }

    [Fact]
    public void EveryAscendancyBackdropIsDrivenByOneVerticalSixCoreHexagon()
    {
        foreach (P18Ascendancy ascendancy in Enum.GetValues<P18Ascendancy>().Where(value => value != P18Ascendancy.None))
        {
            P18AscendancyNode[] cores = P18AscendancyCatalog.For(ascendancy)
                .Where(node => node.Kind == P18NodeKind.Core).ToArray();
            Assert.Equal(6, cores.Length);
            Assert.Equal(190, cores.Max(node => Math.Abs(node.Y)));
            Assert.Equal(165, cores.Max(node => Math.Abs(node.X)));
            Assert.Contains(cores, node => node.X == 0 && node.Y == -190);
            Assert.Contains(cores, node => node.X == 0 && node.Y == 190);
        }
    }

    private static void AssertProjection(IEnumerable<(float X, float Y)> nodes, float extent, int sourceSize)
    {
        foreach ((float x, float y) in nodes)
        {
            Assert.InRange(P31TreeProjection.Normalize(x, extent), -1f, 1f);
            Assert.InRange(P31TreeProjection.Normalize(y, extent), -1f, 1f);
            float pixelX = P31TreeProjection.SourcePixel(x, extent, sourceSize);
            float pixelY = P31TreeProjection.SourcePixel(y, extent, sourceSize);
            foreach (float zoom in new[] { .11f, .22f, .82f, 1.5f })
            {
                P31ProjectedPoint direct = P31TreeProjection.WorldToScreen(x, y, 317.25f, 211.75f, zoom);
                P31ProjectedPoint baked = P31TreeProjection.SourcePixelToScreen(pixelX, pixelY, sourceSize,
                    317.25f, 211.75f, extent, zoom);
                Assert.InRange(Math.Abs(direct.X - baked.X), 0, .001f);
                Assert.InRange(Math.Abs(direct.Y - baked.Y), 0, .001f);
            }
        }
    }
}
