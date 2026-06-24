using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Expression.Physical;

/// <summary>
/// A physical multiplication of two sub-expressions.
/// </summary>
sealed class PhysicalMultiply : BiOp
{

    public PhysicalMultiply(OpTraitSet traits, IOp left, IOp right)
        : base(left.Cluster, traits, left, right)
    {

    }

    public override IOp Copy(OpTraitSet traits, ImmutableArray<IOp> children)
    {
        return new PhysicalMultiply(traits, children[0], children[1]);
    }

}
