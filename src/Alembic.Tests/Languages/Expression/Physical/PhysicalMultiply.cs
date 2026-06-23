using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Expression.Physical;

/// <summary>
/// A physical multiplication of two sub-expressions.
/// </summary>
sealed class PhysicalMultiply : BiOp
{

    public PhysicalMultiply(TraitSet traits, IOpNode left, IOpNode right)
        : base(traits, left, right)
    {

    }

    public override IOpNode Copy(TraitSet traits, ImmutableArray<IOpNode> children)
    {
        return new PhysicalMultiply(traits, children[0], children[1]);
    }

}
