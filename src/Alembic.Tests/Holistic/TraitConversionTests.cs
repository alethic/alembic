using Alembic.Algebra;
using Alembic.Plan;
using Alembic.Plan.Volcano;

using Alembic.Tests.Languages.Image;
using Alembic.Tests.Languages.Image.Rules;
using Alembic.Tests.Languages.Relational;
using Alembic.Tests.Languages.Relational.Physical;

using Xunit;
using Xunit.Abstractions;

namespace Alembic.Tests.Holistic;

public class TraitConversionTests
{

    readonly ITestOutputHelper _output;

    public TraitConversionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void A_trait_def_hook_enforces_a_trait_without_a_converter_rule()
    {
        var unsorted = TraitSet.CreateEmpty().Plus(RelationalConventions.Physical).Plus(Sortedness.Unsorted);
        var sorted = TraitSet.CreateEmpty().Plus(RelationalConventions.Physical).Plus(Sortedness.Sorted);

        // No converter rule for sortedness is registered; the planner must reach the sorted form
        // through SortednessTraitDef's own conversion hook, which wraps the input in a PhysicalSort.
        var planner = new VolcanoPlanner();
        var cluster = new Cluster(planner);
        INode root = new PhysicalSource(cluster, unsorted, "t");

        planner.AddTraitDef(SortednessTraitDef.Instance);
        planner.SetRoot(root);
        planner.SetRoot(planner.ChangeTraits(root, sorted));
        var best = planner.FindBestPlan();
        _output.WriteLine(PlanUtil.ToString(best));

        var sort = Assert.IsType<PhysicalSort>(best);
        Assert.Equal("t", Assert.IsType<PhysicalSource>(sort.Input).Table);
    }

    [Fact]
    public void A_multi_step_chain_reaches_a_convention_through_an_intermediate()
    {
        var logical = TraitSet.CreateEmpty().Plus(ImageConventions.Logical);
        var gpu = TraitSet.CreateEmpty().Plus(ImageConventions.Gpu);

        // Only logical->CPU and CPU->GPU converters are registered (no direct logical->GPU). Reaching
        // the GPU therefore requires chaining the two: load on the CPU, then upload.
        var planner = new VolcanoPlanner();
        var cluster = new Cluster(planner);
        INode root = new Load(cluster, logical, "x.png");

        planner.AddRule(new LowerToCpu());
        planner.AddRule(new UploadRule());
        planner.SetRoot(root);
        planner.SetRoot(planner.ChangeTraits(root, gpu));
        var best = planner.FindBestPlan();
        _output.WriteLine(PlanUtil.ToString(best));

        var upload = Assert.IsType<Upload>(best);
        Assert.Equal("x.png", Assert.IsType<Load>(upload.Input).Source);
    }

}
