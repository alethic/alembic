using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Expression.Physical;

/// <summary>
/// A physical multiplication of two sub-expressions.
/// </summary>
sealed class PhysicalMultiply : BiNode
{

    public PhysicalMultiply(TraitSet traits, INode left, INode right)
        : base(traits, left, right)
    {

    }

    public override INode Copy(TraitSet traits, ImmutableArray<INode> children)
    {
        return new PhysicalMultiply(traits, children[0], children[1]);
    }

}
