using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Expression.Logical;

/// <summary>
/// A logical multiplication of two sub-expressions.
/// </summary>
sealed class Multiply : BiNode
{

    public Multiply(TraitSet traits, INode left, INode right)
        : base(traits, left, right)
    {

    }

    public override INode Copy(TraitSet traits, ImmutableArray<INode> children)
    {
        return new Multiply(traits, children[0], children[1]);
    }

}
