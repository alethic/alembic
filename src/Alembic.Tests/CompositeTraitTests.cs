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

        var set = TraitSet.CreateEmpty().Replace(SortKeyTraitDef.Instance, new[] { a, b });

        Assert.Equal(new[] { a, b }, set.GetList(SortKeyTraitDef.Instance));
    }

    [Fact]
    public void A_single_value_folds_to_a_plain_trait()
    {
        var a = new SortKey("a");

        var set = TraitSet.CreateEmpty().Replace(SortKeyTraitDef.Instance, new[] { a });

        Assert.Equal(new[] { a }, set.GetList(SortKeyTraitDef.Instance));
        Assert.Equal(a, set.Get(SortKeyTraitDef.Instance));
    }

    [Fact]
    public void A_composite_satisfies_a_requirement_met_by_any_member()
    {
        var a = new SortKey("a");
        var b = new SortKey("b");

        var have = TraitSet.CreateEmpty().Replace(SortKeyTraitDef.Instance, new[] { a, b });
        var need = TraitSet.CreateEmpty().Plus(a);

        Assert.True(have.Satisfies(need));
    }

}
