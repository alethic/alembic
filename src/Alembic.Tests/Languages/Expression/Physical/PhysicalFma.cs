using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Expression.Physical;

/// <summary>
/// A physical fused multiply-add — computes <c>a * b + c</c> in one step. It is the result of pushing a
/// <see cref="PhysicalMultiply"/> up into the <see cref="PhysicalAdd"/> above it: a single physical
/// realization that replaces the two-node form. Having three children, it is a plain
/// <see cref="AbstractNode"/> rather than a <see cref="BiNode"/>.
/// </summary>
sealed class PhysicalFma : AbstractNode
{

    public PhysicalFma(TraitSet traits, INode a, INode b, INode c)
        : base(traits, ImmutableArray.Create(a, b, c))
    {

    }

    public INode A => Children[0];

    public INode B => Children[1];

    public INode C => Children[2];

    protected override void Explain(INodeWriter writer)
    {
        writer.Input("a", A);
        writer.Input("b", B);
        writer.Input("c", C);
    }

    public override INode Copy(TraitSet traits, ImmutableArray<INode> children)
    {
        return new PhysicalFma(traits, children[0], children[1], children[2]);
    }

}
