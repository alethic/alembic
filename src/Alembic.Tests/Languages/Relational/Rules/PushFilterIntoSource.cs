using Alembic.Algebra;
using Alembic.Plan.Rules;

using Alembic.Tests.Languages.Relational.Physical;

namespace Alembic.Tests.Languages.Relational.Rules;

/// <summary>
/// Push-down: a physical filter directly over a physical source collapses into a single
/// <see cref="PhysicalFilteredSource"/> that applies the predicate itself.
/// </summary>
sealed class PushFilterIntoSource : IRule
{

    public Operand Operand => Operand.Of<PhysicalFilter>(Operand.Of<PhysicalSource>());

    public void OnMatch(RuleCall call)
    {
        var filter = (PhysicalFilter)call.Node(0);
        var source = (PhysicalSource)call.Node(1);
        call.Transform(new PhysicalFilteredSource(filter.Traits, source.Table, filter.Predicate));
    }

}
