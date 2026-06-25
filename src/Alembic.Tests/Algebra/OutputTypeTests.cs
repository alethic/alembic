using Alembic.Algebra;
using Alembic.Plan;
using Alembic.Plan.Hep;
using Alembic.Plan.Volcano;

using Alembic.Tests.Languages.Expression.Logical;
using Alembic.Tests.Languages.Relational.Logical;

using Xunit;

namespace Alembic.Tests.Algebra;

/// <summary>
/// The output-type machinery the planner plans around: how an op derives its <see cref="IOp.OutputType"/>
/// and how structural identity folds it in.
/// </summary>
public class OutputTypeTests
{

    static readonly OpTraitSet Traits = OpTraitSet.CreateEmpty();

    static OpCluster NewCluster() => new OpCluster(new VolcanoPlanner());

    [Fact]
    public void Void_is_equivalent_only_to_itself()
    {
        Assert.True(VoidOutputType.Instance.IsEquivalentTo(VoidOutputType.Instance));
        Assert.False(VoidOutputType.Instance.IsEquivalentTo(new Shape(1)));
        Assert.False(new Shape(1).IsEquivalentTo(VoidOutputType.Instance));
    }

    [Fact]
    public void An_op_defaults_to_the_void_output_type()
    {
        var op = new Literal(NewCluster(), Traits, 1);

        Assert.IsType<VoidOutputType>(op.OutputType);
    }

    [Fact]
    public void An_op_derives_its_output_type_once_and_caches_it()
    {
        var shape = new Shape(2);
        var op = new ShapedLeaf(NewCluster(), Traits, shape);

        Assert.Same(shape, op.OutputType);
        Assert.Same(op.OutputType, op.OutputType);
    }

    [Fact]
    public void A_single_input_op_derives_its_output_type_from_its_input()
    {
        var cluster = NewCluster();
        var leaf = new ShapedLeaf(cluster, Traits, new Shape(3));
        var filter = new LogicalFilter(Traits, leaf, "x");

        Assert.True(filter.OutputType.IsEquivalentTo(new Shape(3)));
    }

    [Fact]
    public void A_hep_vertex_derives_its_output_type_from_its_current_op()
    {
        var leaf = new ShapedLeaf(NewCluster(), Traits, new Shape(4));
        var vertex = new HepOpVertex(leaf);

        Assert.True(vertex.OutputType.IsEquivalentTo(new Shape(4)));
    }

    [Fact]
    public void Deep_equality_folds_in_output_type_but_ignores_cosmetic_naming()
    {
        var cluster = NewCluster();
        var op = new ShapedLeaf(cluster, Traits, new Shape(2, "a"), tag: 1);
        var sameShapeOtherLabel = new ShapedLeaf(cluster, Traits, new Shape(2, "b"), tag: 1);
        var widerOutput = new ShapedLeaf(cluster, Traits, new Shape(3, "a"), tag: 1);
        var otherTerm = new ShapedLeaf(cluster, Traits, new Shape(2, "a"), tag: 2);

        // Equivalent output (the cosmetic label is ignored) and equal terms ⇒ structurally equal.
        Assert.True(op.DeepEquals(sameShapeOtherLabel));

        // Output types differ ⇒ not equal, even though every other term matches.
        Assert.False(op.DeepEquals(widerOutput));

        // A term differs ⇒ not equal, even though the output type is the same.
        Assert.False(op.DeepEquals(otherTerm));
    }

}
