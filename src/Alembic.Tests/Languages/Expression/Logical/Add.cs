using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Expression.Logical;

/// <summary>
/// A logical addition of two sub-expressions.
/// </summary>
sealed class Add : BiOp
{

    public Add(OpTraitSet traits, IOp left, IOp right)
        : base(left.Cluster, traits, left, right)
    {

    }

    public override IOp Copy(OpTraitSet traits, ImmutableArray<IOp> children)
    {
        return new Add(traits, children[0], children[1]);
    }

}
