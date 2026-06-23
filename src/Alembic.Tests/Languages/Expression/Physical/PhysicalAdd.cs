using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Expression.Physical;

/// <summary>
/// A physical addition of two sub-expressions.
/// </summary>
sealed class PhysicalAdd : BiOp
{

    public PhysicalAdd(OpTraitSet traits, IOp left, IOp right)
        : base(traits, left, right)
    {

    }

    public override IOp Copy(OpTraitSet traits, ImmutableArray<IOp> children)
    {
        return new PhysicalAdd(traits, children[0], children[1]);
    }

}
