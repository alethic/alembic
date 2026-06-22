using Alembic.Algebra;
using Alembic.Plan;
using Alembic.Plan.Volcano;

using Alembic.Tests.Languages.Image;
using Alembic.Tests.Languages.Image.Rules;

using Xunit;

namespace Alembic.Tests;

public class ImagePlanningTests
{

    [Fact]
    public void Runs_the_pipeline_on_the_gpu_and_downloads_once()
    {
        var logical = TraitSet.CreateEmpty().Plus(ImageConventions.Logical);
        var cpu = TraitSet.CreateEmpty().Plus(ImageConventions.Cpu);

        // threshold(grayscale(blur(load))) — the result is wanted on the CPU.
        INode root = new Threshold(logical, new Grayscale(logical, new Blur(logical, new Load(logical, "img.png"))));

        var best = Plan(root, cpu);

        // Cheapest: every operation on the GPU (cost 1 each = 4), then a single download (5) = 9,
        // versus running the pipeline on the CPU (10 each).
        var download = Assert.IsType<Download>(best);
        AssertAllGpu(download.Input);

        var threshold = Assert.IsType<Threshold>(download.Input);
        var grayscale = Assert.IsType<Grayscale>(threshold.Input);
        var blur = Assert.IsType<Blur>(grayscale.Input);
        var load = Assert.IsType<Load>(blur.Input);
        Assert.Equal("img.png", load.Source);
    }

    [Fact]
    public void Keeps_the_pipeline_on_the_gpu_when_gpu_output_is_requested()
    {
        var logical = TraitSet.CreateEmpty().Plus(ImageConventions.Logical);
        var gpu = TraitSet.CreateEmpty().Plus(ImageConventions.Gpu);

        INode root = new Threshold(logical, new Grayscale(logical, new Blur(logical, new Load(logical, "img.png"))));

        var best = Plan(root, gpu);

        // No consumer on the CPU, so no transfer is inserted — the whole pipeline stays on the GPU.
        Assert.IsType<Threshold>(best);
        AssertAllGpu(best);
    }

    static INode Plan(INode root, TraitSet required)
    {
        var planner = new VolcanoPlanner();
        planner.AddRule(new LowerToCpu());
        planner.AddRule(new LowerToGpu());
        planner.AddRule(new DownloadRule());
        planner.AddRule(new UploadRule());
        planner.SetRoot(root);
        planner.ChangeTraits(root, required);
        return planner.FindBestPlan();
    }

    static void AssertAllGpu(INode node)
    {
        Assert.Equal(ImageConventions.Gpu, node.Convention);

        foreach (var child in node.Children)
            AssertAllGpu(child);
    }

}
