using GameForWork.Core.Campaign.Progression;
using GameForWork.Core.Endgame;
using GameForWork.Core.Ascendancies;
using GameForWork.Core.Builds;
using GameForWork.Core.Presentation;

namespace GameForWork.Tests;

public sealed class PresentationFeatureTests
{
    [Fact]
    public void EveryActiveSkillHasOneStableVisualDescriptor()
    {
        Assert.Equal(86, VisualCatalog.Skills.Count);
        Assert.Equal(86, VisualCatalog.Skills.Select(item => item.SkillId).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(18, VisualCatalog.Skills.Count(item => item.Signature));
        Assert.All(ActiveSkillCatalog.Active, active =>
        {
            SkillVisualDescriptor visual = VisualCatalog.ForSkill(active.Combat.SkillId);
            Assert.InRange(visual.AtlasCell, 0, 15);
            Assert.InRange(visual.LifetimeMilliseconds, 500, 1_200);
            Assert.True(visual.ScaleBasisPoints > 0);
        });
    }

    [Fact]
    public void EverySupportHasAVisibleMechanicLayer()
    {
        Assert.Equal(98, VisualCatalog.Supports.Count);
        Assert.All(VisualCatalog.Supports, support => Assert.NotEqual(SupportVisualLayer.None, support.Layer));
        Assert.Contains(VisualCatalog.Supports, item => item.Layer.HasFlag(SupportVisualLayer.ExtraProjectiles));
        Assert.Contains(VisualCatalog.Supports, item => item.Layer.HasFlag(SupportVisualLayer.CriticalFlash));
        Assert.Contains(VisualCatalog.Supports, item => item.Layer.HasFlag(SupportVisualLayer.AilmentTrail));
    }

    [Fact]
    public void SkillVisualLookupIsDeterministic()
    {
        foreach (ActiveSkillDefinition active in ActiveSkillCatalog.Active)
            Assert.Same(VisualCatalog.ForSkill(active.Combat.SkillId), VisualCatalog.ForSkill(active.Combat.SkillId));
    }

    [Fact]
    public void BakedTreePixelsAndRuntimeWorldPositionsUseTheSameProjection()
    {
        AssertProjection(PassiveTree.Nodes.Select(node => (node.X, node.Y)),
            PassiveTree.LayoutExtent, TreeProjection.PassiveSourceSize);
        AssertProjection(AtlasTree.Nodes.Select(node => (node.X, node.Y)),
            AtlasTree.LayoutExtent, TreeProjection.AtlasSourceSize);
        AssertProjection(WarriorAscendancyCatalog.Nodes.Select(node => ((float)node.X, (float)node.Y)),
            240f, TreeProjection.AscendancySourceSize);
    }

    [Fact]
    public void EveryAscendancyBackdropIsDrivenByOneVerticalSixCoreHexagon()
    {
        foreach (Ascendancy ascendancy in Enum.GetValues<Ascendancy>().Where(value => value != Ascendancy.None))
        {
            AscendancyNode[] cores = WarriorAscendancyCatalog.For(ascendancy)
                .Where(node => node.Kind == NodeKind.Core).ToArray();
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
            Assert.InRange(TreeProjection.Normalize(x, extent), -1f, 1f);
            Assert.InRange(TreeProjection.Normalize(y, extent), -1f, 1f);
            float pixelX = TreeProjection.SourcePixel(x, extent, sourceSize);
            float pixelY = TreeProjection.SourcePixel(y, extent, sourceSize);
            foreach (float zoom in new[] { .11f, .22f, .82f, 1.5f })
            {
                ProjectedPoint direct = TreeProjection.WorldToScreen(x, y, 317.25f, 211.75f, zoom);
                ProjectedPoint baked = TreeProjection.SourcePixelToScreen(pixelX, pixelY, sourceSize,
                    317.25f, 211.75f, extent, zoom);
                Assert.InRange(Math.Abs(direct.X - baked.X), 0, .001f);
                Assert.InRange(Math.Abs(direct.Y - baked.Y), 0, .001f);
            }
        }
    }
}
