using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan.Rules;
using Alembic.Plan.Traits;

namespace Alembic.Tests;

/// <summary>
/// Two toy conventions used by the lowering tests.
/// </summary>
static class Conventions
{

    public static readonly Convention Logical = new Convention("LOGICAL");

    public static readonly Convention Physical = new Convention("PHYSICAL");

}

/// <summary>
/// A logical leaf naming some source.
/// </summary>
sealed class LogicalSource : NodeBase
{

    readonly string _table;

    public LogicalSource(TraitSet traits, string table)
        : base(traits, ImmutableArray<INode>.Empty)
    {
        _table = table;
    }

    public string Table => _table;

    protected override object Signature => _table;

    public override INode Copy(TraitSet traits, ImmutableArray<INode> children)
    {
        return new LogicalSource(traits, _table);
    }

}

/// <summary>
/// A logical filter over a single child.
/// </summary>
sealed class LogicalFilter : NodeBase
{

    readonly string _predicate;

    public LogicalFilter(TraitSet traits, INode source, string predicate)
        : base(traits, ImmutableArray.Create(source))
    {
        _predicate = predicate;
    }

    public INode Source => Children[0];

    public string Predicate => _predicate;

    protected override object Signature => _predicate;

    public override INode Copy(TraitSet traits, ImmutableArray<INode> children)
    {
        return new LogicalFilter(traits, children[0], _predicate);
    }

}

/// <summary>
/// The physical counterpart of <see cref="LogicalSource"/>.
/// </summary>
sealed class PhysicalSource : NodeBase
{

    readonly string _table;

    public PhysicalSource(TraitSet traits, string table)
        : base(traits, ImmutableArray<INode>.Empty)
    {
        _table = table;
    }

    public string Table => _table;

    protected override object Signature => _table;

    public override INode Copy(TraitSet traits, ImmutableArray<INode> children)
    {
        return new PhysicalSource(traits, _table);
    }

}

/// <summary>
/// The physical counterpart of <see cref="LogicalFilter"/>.
/// </summary>
sealed class PhysicalFilter : NodeBase
{

    readonly string _predicate;

    public PhysicalFilter(TraitSet traits, INode source, string predicate)
        : base(traits, ImmutableArray.Create(source))
    {
        _predicate = predicate;
    }

    public INode Source => Children[0];

    public string Predicate => _predicate;

    protected override object Signature => _predicate;

    public override INode Copy(TraitSet traits, ImmutableArray<INode> children)
    {
        return new PhysicalFilter(traits, children[0], _predicate);
    }

}

/// <summary>
/// Lowers <see cref="LogicalSource"/> to <see cref="PhysicalSource"/>.
/// </summary>
sealed class SourceConverter : IConverterRule
{

    readonly TraitSet _physical;

    public SourceConverter(TraitSet physical)
    {
        _physical = physical;
    }

    public Operand Operand => Operand.Of<LogicalSource>();

    public Convention In => Conventions.Logical;

    public Convention Out => Conventions.Physical;

    public INode? Convert(INode node)
    {
        var source = (LogicalSource)node;
        return new PhysicalSource(_physical, source.Table);
    }

}

/// <summary>
/// Lowers <see cref="LogicalFilter"/> to <see cref="PhysicalFilter"/>.
/// </summary>
sealed class FilterConverter : IConverterRule
{

    readonly TraitSet _physical;

    public FilterConverter(TraitSet physical)
    {
        _physical = physical;
    }

    public Operand Operand => Operand.Of<LogicalFilter>();

    public Convention In => Conventions.Logical;

    public Convention Out => Conventions.Physical;

    public INode? Convert(INode node)
    {
        var filter = (LogicalFilter)node;
        return new PhysicalFilter(_physical, filter.Source, filter.Predicate);
    }

}
