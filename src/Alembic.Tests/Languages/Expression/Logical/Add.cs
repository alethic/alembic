using System;
using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Expression.Logical;

/// <summary>
/// A logical addition of two sub-expressions.
/// </summary>
sealed class Add : AbstractOp
{

    IOp _left;
    IOp _right;

    public Add(OpTraitSet traits, IOp left, IOp right)
        : base(left.Cluster, traits)
    {
        _left = left;
        _right = right;
    }

    public IOp Left => _left;

    public IOp Right => _right;

    public override ImmutableArray<IOp> Inputs => [_left, _right];

    public override IOpWriter ExplainTerms(IOpWriter writer)
    {
        base.ExplainTerms(writer);
        writer.Input("left", _left);
        writer.Input("right", _right);
        return writer;
    }

    public override void ReplaceInput(int ordinalInParent, IOp p)
    {
        switch (ordinalInParent)
        {
            case 0:
                _left = p;
                break;
            case 1:
                _right = p;
                break;
            default:
                throw new IndexOutOfRangeException("Input " + ordinalInParent);
        }

        RecomputeDigest();
    }

    public override IOp Copy(OpTraitSet traits, ImmutableArray<IOp> children)
    {
        return new Add(traits, children[0], children[1]);
    }

}
