using System.Collections.Generic;
using System.Text;

namespace Alembic.Algebra;

/// <summary>
/// The default <see cref="IOpWriter"/>: renders an op and its inputs as an indented plan tree. Each
/// op contributes its terms (via <see cref="IOp.Explain"/>); <see cref="Done"/> emits the op's line
/// — its type, traits, and attributes — and then recurses into its inputs, one indent level deeper.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.externalize.RelWriterImpl")]
public class OpWriterImpl : IOpWriter
{

    readonly StringBuilder _builder;
    readonly bool _withIdPrefix;
    readonly List<(string Name, object? Value)> _values = new List<(string, object?)>();
    int _spaces;

    /// <summary>
    /// Creates a writer that appends to <paramref name="builder"/>, prefixing each op with its id.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.externalize.RelWriterImpl", "RelWriterImpl(PrintWriter)")]
    public OpWriterImpl(StringBuilder builder)
        : this(builder, true)
    {
    }

    /// <summary>
    /// Creates a writer that appends to <paramref name="builder"/>, prefixing each op with its id only
    /// when <paramref name="withIdPrefix"/> is set. (Calcite also threads a <c>SqlExplainLevel</c> detail
    /// level through this ctor; Alembic always renders at the attribute level.)
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.externalize.RelWriterImpl", "RelWriterImpl(PrintWriter, SqlExplainLevel, boolean)")]
    public OpWriterImpl(StringBuilder builder, bool withIdPrefix)
    {
        _builder = builder;
        _withIdPrefix = withIdPrefix;
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.externalize.RelWriterImpl", "item(String, Object)")]
    public IOpWriter Item(string name, object? value)
    {
        _values.Add((name, value));
        return this;
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.externalize.RelWriterImpl", "done(RelNode)")]
    public IOpWriter Done(IOp op)
    {
        var values = _values.ToArray();
        _values.Clear();
        Explain(op, values);
        return this;
    }

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.externalize.RelWriterImpl", "explain_(RelNode, List<Pair<String, Object>>)")]
    void Explain(IOp op, (string Name, object? Value)[] values)
    {
        _builder.Append(' ', _spaces);

        // Calcite prints the id prefix only when withIdPrefix is set, then getRelTypeName() (the simple
        // class name). It does not print the trait set.
        if (_withIdPrefix)
            _builder.Append(op.Id).Append(':');

        _builder.Append(op.GetType().Name);

        int attributes = 0;
        foreach (var (name, value) in values)
        {
            // Inputs are items whose value is an op; they are recursed into below, not printed inline.
            if (value is IOp)
                continue;

            _builder.Append(attributes++ == 0 ? "(" : ", ").Append(name).Append("=[").Append(value).Append(']');
        }

        if (attributes > 0)
            _builder.Append(')');

        _builder.Append('\n');

        _spaces += 2;
        foreach (var input in op.Children)
            input.Explain(this);
        _spaces -= 2;
    }

}
