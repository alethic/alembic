using Alembic.Algebra;

using Alembic.Tests.Languages.Relational.Physical;

using Alembic.Plan;

namespace Alembic.Tests.Languages.Relational.Rules;

/// <summary>
/// Push-down: a physical filter directly over a physical source collapses into a single
/// <see cref="PhysicalFilteredSource"/> that applies the predicate itself.
/// </summary>
sealed class PushFilterIntoSource : OpRule
{

    public PushFilterIntoSource()
        : base(Some<PhysicalFilter>(Leaf<PhysicalSource>()))
    {
    }

    public override void OnMatch(OpRuleCall call)
    {
        var filter = (PhysicalFilter)call.Op(0);
        var source = (PhysicalSource)call.Op(1);
        call.TransformTo(new PhysicalFilteredSource(source.Cluster, filter.Traits, source.Table, filter.Predicate));
    }

}
