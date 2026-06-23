using Alembic.Algebra;
using Alembic.Plan;
using Alembic.Plan.Volcano;

using Alembic.Tests.Languages.Image;
using Alembic.Tests.Languages.Image.Rules;

using Xunit;
using Xunit.Abstractions;

namespace Alembic.Tests.Holistic;

public class ImagePlanningTests
{

    readonly ITestOutputHelper _output;

    public ImagePlanningTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Uploads_runs_the_pipeline_on_the_gpu_and_downloads_once()
    {
        // load is CPU-only; the three transforms are far cheaper on the GPU. With a CPU result wanted,
        // the cheapest plan uploads after the load, runs the transforms on the GPU, and downloads once:
        // 10 (load) + 5 (up) + 1 + 1 + 1 + 5 (down) = 23, versus 40 all on the CPU.
        var planner = MakePlanner();
        var cluster = new Cluster(planner);
        IOpNode root = Threshold(Grayscale(Blur(Load(cluster, "photo.png"))));

        var best = Plan(planner, root, Cpu);

        var download = Assert.IsType<Download>(best);
        var threshold = Assert.IsType<Threshold>(download.Input);
        var grayscale = Assert.IsType<Grayscale>(threshold.Input);
        var blur = Assert.IsType<Blur>(grayscale.Input);
        var upload = Assert.IsType<Upload>(blur.Input);
        var load = Assert.IsType<Load>(upload.Input);

        Assert.Equal("photo.png", load.Source);
        Assert.Equal(ImageConventions.Gpu, threshold.Traits.Convention);
        Assert.Equal(ImageConventions.Gpu, blur.Traits.Convention);
        Assert.Equal(ImageConventions.Cpu, load.Traits.Convention);
        Assert.Equal(1, Count<Upload>(best));
        Assert.Equal(1, Count<Download>(best));
    }

    [Fact]
    public void Skips_the_final_download_when_gpu_output_is_requested()
    {
        var planner = MakePlanner();
        var cluster = new Cluster(planner);
        IOpNode root = Threshold(Grayscale(Blur(Load(cluster, "photo.png"))));

        var best = Plan(planner, root, Gpu);

        // No CPU consumer, so the trailing download is gone — only the leading upload remains.
        var threshold = Assert.IsType<Threshold>(best);
        Assert.Equal(ImageConventions.Gpu, threshold.Traits.Convention);
        Assert.Equal(1, Count<Upload>(best));
        Assert.Equal(0, Count<Download>(best));
    }

    [Fact]
    public void Crosses_back_to_the_cpu_for_a_cpu_only_op_then_returns_to_the_gpu()
    {
        // A CPU-only inpaint sits between two GPU-worthy runs (blur+grayscale before it, blur+threshold
        // after). The planner uploads for the first run, downloads to inpaint, uploads again for the
        // second run, and downloads the result: CPU -> GPU -> CPU -> GPU -> CPU.
        var planner = MakePlanner();
        var cluster = new Cluster(planner);
        IOpNode root = Threshold(Blur(Inpaint(Grayscale(Blur(Load(cluster, "photo.png"))))));

        var best = Plan(planner, root, Cpu);

        Assert.IsType<Download>(best);
        Assert.Equal(2, Count<Upload>(best));
        Assert.Equal(2, Count<Download>(best));
        Assert.Equal(1, Count<Inpaint>(best));
        Assert.Equal(ImageConventions.Cpu, First<Inpaint>(best).Traits.Convention);
        Assert.Equal(ImageConventions.Cpu, First<Load>(best).Traits.Convention);
    }

    [Fact]
    public void Does_not_cross_to_the_gpu_for_a_single_op_when_transfers_cost_more()
    {
        // One lone GPU-worthy blur between two CPU-only inpaints. Round-tripping it (5 + 1 + 5 = 11)
        // costs more than just running it on the CPU (10), so the planner keeps everything on the CPU
        // and inserts no transfers at all.
        var planner = MakePlanner();
        var cluster = new Cluster(planner);
        IOpNode root = Inpaint(Blur(Inpaint(Load(cluster, "photo.png"))));

        var best = Plan(planner, root, Cpu);

        Assert.Equal(0, Count<Upload>(best));
        Assert.Equal(0, Count<Download>(best));
        AssertAllCpu(best);
    }

    static IOpNode Load(Cluster cluster, string source) => new Load(cluster, Logical, source);
    static IOpNode Blur(IOpNode input) => new Blur(Logical, input);
    static IOpNode Grayscale(IOpNode input) => new Grayscale(Logical, input);
    static IOpNode Threshold(IOpNode input) => new Threshold(Logical, input);
    static IOpNode Inpaint(IOpNode input) => new Inpaint(Logical, input);

    static readonly TraitSet Logical = TraitSet.CreateEmpty().Plus(ImageConventions.Logical);
    static readonly TraitSet Cpu = TraitSet.CreateEmpty().Plus(ImageConventions.Cpu);
    static readonly TraitSet Gpu = TraitSet.CreateEmpty().Plus(ImageConventions.Gpu);

    static VolcanoPlanner MakePlanner()
    {
        var planner = new VolcanoPlanner();
        planner.AddRule(new LowerToCpu());
        planner.AddRule(new LowerToGpu());
        planner.AddRule(new DownloadRule());
        planner.AddRule(new UploadRule());
        return planner;
    }

    IOpNode Plan(VolcanoPlanner planner, IOpNode root, TraitSet required)
    {
        planner.SetRoot(root);
        planner.SetRoot(planner.ChangeTraits(root, required));
        var best = planner.FindBestPlan();
        _output.WriteLine(PlanUtil.ToString(best));
        return best;
    }

    static int Count<T>(IOpNode op) where T : IOpNode
    {
        var count = op is T ? 1 : 0;
        foreach (var child in op.Children)
            count += Count<T>(child);

        return count;
    }

    static T First<T>(IOpNode op) where T : IOpNode
    {
        if (op is T match)
            return match;

        foreach (var child in op.Children)
            if (TryFirst<T>(child, out var found))
                return found;

        throw new Xunit.Sdk.XunitException($"No {typeof(T).Name} found in the plan.");
    }

    static bool TryFirst<T>(IOpNode op, out T found) where T : IOpNode
    {
        if (op is T match)
        {
            found = match;
            return true;
        }

        foreach (var child in op.Children)
            if (TryFirst<T>(child, out found))
                return true;

        found = default!;
        return false;
    }

    static void AssertAllCpu(IOpNode op)
    {
        Assert.Equal(ImageConventions.Cpu, op.Convention);

        foreach (var child in op.Children)
            AssertAllCpu(child);
    }

}
