using Alembic.Algebra;
using Alembic.Plan;

using Alembic.Tests.Languages.Relational.Logical;
using Alembic.Tests.Languages.Relational.Physical;

namespace Alembic.Tests.Languages.Relational.Rules;

/// <summary>
/// Lowers a logical source by offering two physical scans from a single match — a cheaper index scan
/// and a full source scan — leaving the planner to choose the cheaper. Exercises one rule call
/// registering several equivalents.
/// </summary>
sealed class OfferTwoScans : OpRule
{

    readonly OpTraitSet _physical;

    public OfferTwoScans(OpTraitSet physical)
        : base(Leaf<LogicalSource>())
    {
        _physical = physical;
    }

    public override void OnMatch(OpRuleCall call)
    {
        var source = (LogicalSource)call.Op(0);

        // Register the cheaper option first, so that picking it proves the choice is by cost — not by
        // the order the equivalents were registered.
        call.TransformTo(new PhysicalIndexSource(source.Cluster, _physical, source.Table));
        call.TransformTo(new PhysicalSource(source.Cluster, _physical, source.Table));
    }

}
