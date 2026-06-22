using Alembic.Algebra;
using Alembic.Plan.Rules;

using Alembic.Tests.Languages.Expression.Logical;

namespace Alembic.Tests.Languages.Expression.Rules;

/// <summary>
/// Constant folding: an addition of two literals collapses to a single literal holding their sum.
/// Exercises a binary operand whose two children are themselves matched.
/// </summary>
sealed class FoldAdd : Rule
{

    public FoldAdd()
        : base(Some<Add>(Leaf<Literal>(), Leaf<Literal>()))
    {
    }

    public override void OnMatch(RuleCall call)
    {
        var add = (Add)call.Node(0);
        var left = (Literal)call.Node(1);
        var right = (Literal)call.Node(2);
        call.TransformTo(new Literal(add.Cluster, add.Traits, left.Value + right.Value));
    }

}
