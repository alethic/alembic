using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Expression.Logical;

/// <summary>
/// A logical addition of two sub-expressions.
/// </summary>
sealed class Add : BiOp
{

    public Add(OpTraitSet traits, IOpNode left, IOpNode right)
        : base(traits, left, right)
    {

    }

    public override IOpNode Copy(OpTraitSet traits, ImmutableArray<IOpNode> children)
    {
        return new Add(traits, children[0], children[1]);
    }

}
