using System;
using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;

namespace Alembic.Benchmarks;

/// <summary>
/// A leaf op carrying a string-valued term (<c>name</c>) and a value-typed term (<c>index</c>). The
/// value-typed term exercises the boxing that happens when terms are stored as <c>object?</c>.
/// </summary>
sealed class Var : AbstractOp
{

    readonly string _name;
    readonly int _index;

    public Var(OpCluster cluster, OpTraitSet traits, string name, int index)
        : base(cluster, traits)
    {
        _name = name;
        _index = index;
    }

    public override IOpWriter ExplainTerms(IOpWriter writer)
    {
        base.ExplainTerms(writer);
        writer.Item("name", _name);
        writer.Item("index", _index);
        return writer;
    }

    public override IOp Copy(OpTraitSet traits, ImmutableArray<IOp> children)
        => new Var(Cluster, traits, _name, _index);

}

/// <summary>
/// A binary op with two input terms, used to build deep expression trees.
/// </summary>
sealed class Bin : AbstractOp
{

    IOp _left;
    IOp _right;

    public Bin(OpTraitSet traits, IOp left, IOp right)
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
            case 0: _left = p; break;
            case 1: _right = p; break;
            default: throw new IndexOutOfRangeException("Input " + ordinalInParent);
        }

        RecomputeDigest();
    }

    public override IOp Copy(OpTraitSet traits, ImmutableArray<IOp> children)
        => new Bin(traits, children[0], children[1]);

}
