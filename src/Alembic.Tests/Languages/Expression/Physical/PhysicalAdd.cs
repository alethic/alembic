using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Expression.Physical;

/// <summary>
/// A physical addition of two sub-expressions.
/// </summary>
sealed class PhysicalAdd : BiNode
{

    public PhysicalAdd(TraitSet traits, INode left, INode right)
        : base(traits, left, right)
    {

    }

    public override INode Copy(TraitSet traits, ImmutableArray<INode> children)
    {
        return new PhysicalAdd(traits, children[0], children[1]);
    }

}
