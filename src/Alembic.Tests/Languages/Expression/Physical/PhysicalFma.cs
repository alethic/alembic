using System;
using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Expression.Physical;

/// <summary>
/// A physical fused multiply-add — computes <c>a * b + c</c> in one step. It is the result of pushing a
/// <see cref="PhysicalMultiply"/> up into the <see cref="PhysicalAdd"/> above it: a single physical
/// realization that replaces the two-op form. Having three children, it is a plain
/// <see cref="AbstractOp"/> rather than a <see cref="BiOp"/>, and stores and replaces them itself.
/// </summary>
sealed class PhysicalFma : AbstractOp
{

    IOp _a;
    IOp _b;
    IOp _c;

    public PhysicalFma(OpTraitSet traits, IOp a, IOp b, IOp c)
        : base(a.Cluster, traits)
    {
        _a = a;
        _b = b;
        _c = c;
    }

    public IOp A => _a;

    public IOp B => _b;

    public IOp C => _c;

    public override ImmutableArray<IOp> Children => ImmutableArray.Create(_a, _b, _c);

    public override IOpWriter ExplainTerms(IOpWriter writer)
    {
        base.ExplainTerms(writer);
        writer.Input("a", A);
        writer.Input("b", B);
        writer.Input("c", C);
        return writer;
    }

    public override IOp Copy(OpTraitSet traits, ImmutableArray<IOp> children)
    {
        return new PhysicalFma(traits, children[0], children[1], children[2]);
    }

    public override void ReplaceInput(int ordinalInParent, IOp p)
    {
        switch (ordinalInParent)
        {
            case 0:
                _a = p;
                break;
            case 1:
                _b = p;
                break;
            case 2:
                _c = p;
                break;
            default:
                throw new IndexOutOfRangeException("Input " + ordinalInParent);
        }

        RecomputeDigest();
    }

}
