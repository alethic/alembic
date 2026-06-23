using Alembic.Plan;

using Xunit;

namespace Alembic.Tests;

public class CompositeTraitTests
{

    [Fact]
    public void Stores_and_reads_several_values_on_one_dimension()
    {
        var a = new SortKey("a");
        var b = new SortKey("b");

        var set = OpTraitSet.CreateEmpty().Plus(SortKeyTraitDef.Instance.Default).Replace(SortKeyTraitDef.Instance, new[] { a, b });

        Assert.Equal(new[] { a, b }, set.GetList(SortKeyTraitDef.Instance));
    }

    [Fact]
    public void A_single_value_folds_to_a_plain_trait()
    {
        var a = new SortKey("a");

        var set = OpTraitSet.CreateEmpty().Plus(SortKeyTraitDef.Instance.Default).Replace(SortKeyTraitDef.Instance, new[] { a });

        Assert.Equal(new[] { a }, set.GetList(SortKeyTraitDef.Instance));
        Assert.Equal(a, set.Get(SortKeyTraitDef.Instance));
    }

    [Fact]
    public void A_composite_satisfies_a_requirement_met_by_any_member()
    {
        var a = new SortKey("a");
        var b = new SortKey("b");

        var have = OpTraitSet.CreateEmpty().Plus(SortKeyTraitDef.Instance.Default).Replace(SortKeyTraitDef.Instance, new[] { a, b });
        var need = OpTraitSet.CreateEmpty().Plus(a);

        Assert.True(have.Satisfies(need));
    }

    [Fact]
    public void Simplify_flattens_composite_traits()
    {
        var a = new SortKey("a");
        var b = new SortKey("b");

        var multi = OpTraitSet.CreateEmpty().Plus(SortKeyTraitDef.Instance.Default).Replace(SortKeyTraitDef.Instance, new[] { a, b });
        Assert.False(multi.AllSimple());

        // A many-member composite collapses to the dimension's default; the set is then all-simple.
        var simplified = multi.Simplify();
        Assert.True(simplified.AllSimple());
        Assert.Equal(SortKeyTraitDef.Instance.Default, simplified.Get(SortKeyTraitDef.Instance));

        // A single-member composite is stored as a plain trait, so it is simple already.
        var single = OpTraitSet.CreateEmpty().Plus(SortKeyTraitDef.Instance.Default).Replace(SortKeyTraitDef.Instance, new[] { a });
        Assert.True(single.AllSimple());
    }

    [Fact]
    public void Replace_a_trait_ignores_an_absent_dimension()
    {
        var empty = OpTraitSet.CreateEmpty();

        // The SortKey dimension is absent, so Replace is a no-op and returns the same interned set.
        Assert.Same(empty, empty.Replace(new SortKey("a")));

        // Once present, Replace substitutes the value.
        var present = empty.Plus(new SortKey("a"));
        Assert.Equal(new SortKey("b"), present.Replace(new SortKey("b")).Get(SortKeyTraitDef.Instance));
    }

}
