using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Expression.Logical;

/// <summary>
/// A logical addition of two sub-expressions.
/// </summary>
sealed class Add : BiNode
{

    public Add(TraitSet traits, INode left, INode right)
        : base(traits, left, right)
    {

    }

    public override INode Copy(TraitSet traits, ImmutableArray<INode> children)
    {
        return new Add(traits, children[0], children[1]);
    }

}
