using System.Collections.Immutable;
using System.Diagnostics;

using Alembic.Algebra;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Relational.Logical;

/// <summary>
/// A logical filter over a single relational input.
/// </summary>
sealed class LogicalFilter : AbstractOp
{

    IOp _input;
    readonly string _predicate;

    public LogicalFilter(OpTraitSet traits, IOp input, string predicate)
        : base(input.Cluster, traits)
    {
        _input = input;
        _predicate = predicate;
    }

    public IOp Input => _input;

    public string Predicate => _predicate;

    public override ImmutableArray<IOp> Inputs => [_input];

    protected override IOutputType DeriveOutputType() => _input.OutputType;

    public override IOpWriter ExplainTerms(IOpWriter writer)
    {
        base.ExplainTerms(writer);
        writer.Input("input", _input);
        writer.Item("predicate", _predicate);
        return writer;
    }

    public override void ReplaceInput(int ordinalInParent, IOp p)
    {
        Debug.Assert(ordinalInParent == 0);
        _input = p;
        RecomputeDigest();
    }

    public override IOp Copy(OpTraitSet traits, ImmutableArray<IOp> children)
    {
        return new LogicalFilter(traits, children[0], _predicate);
    }

}
