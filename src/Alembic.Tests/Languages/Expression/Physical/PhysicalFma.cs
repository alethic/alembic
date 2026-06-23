using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Expression.Physical;

/// <summary>
/// A physical fused multiply-add — computes <c>a * b + c</c> in one step. It is the result of pushing a
/// <see cref="PhysicalMultiply"/> up into the <see cref="PhysicalAdd"/> above it: a single physical
/// realization that replaces the two-op form. Having three children, it is a plain
/// <see cref="AbstractOp"/> rather than a <see cref="BiOp"/>.
/// </summary>
sealed class PhysicalFma : AbstractOp
{

    public PhysicalFma(TraitSet traits, IOpNode a, IOpNode b, IOpNode c)
        : base(a.Cluster, traits, ImmutableArray.Create(a, b, c))
    {

    }

    public IOpNode A => Children[0];

    public IOpNode B => Children[1];

    public IOpNode C => Children[2];

    public override IOpWriter ExplainTerms(IOpWriter writer)
    {
        base.ExplainTerms(writer);
        writer.Input("a", A);
        writer.Input("b", B);
        writer.Input("c", C);
        return writer;
    }

    public override IOpNode Copy(TraitSet traits, ImmutableArray<IOpNode> children)
    {
        return new PhysicalFma(traits, children[0], children[1], children[2]);
    }

}
