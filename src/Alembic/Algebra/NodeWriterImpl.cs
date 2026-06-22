using System.Collections.Generic;
using System.Text;

namespace Alembic.Algebra;

/// <summary>
/// The default <see cref="INodeWriter"/>: renders a node and its inputs as an indented plan tree. Each
/// node contributes its terms (via <see cref="INode.Explain"/>); <see cref="Done"/> emits the node's line
/// — its type, traits, and attributes — and then recurses into its inputs, one indent level deeper. The
/// analog of Calcite's <c>RelWriterImpl</c>.
/// </summary>
public sealed class NodeWriterImpl : INodeWriter
{

    readonly StringBuilder _builder;
    readonly List<(string Name, object? Value)> _values = new List<(string, object?)>();
    int _spaces;

    /// <summary>
    /// Creates a writer that appends to <paramref name="builder"/>.
    /// </summary>
    public NodeWriterImpl(StringBuilder builder)
    {
        _builder = builder;
    }

    /// <inheritdoc />
    public INodeWriter Item(string name, object? value)
    {
        _values.Add((name, value));
        return this;
    }

    /// <inheritdoc />
    public INodeWriter Done(INode node)
    {
        var values = _values.ToArray();
        _values.Clear();
        Explain(node, values);
        return this;
    }

    void Explain(INode node, (string Name, object? Value)[] values)
    {
        _builder.Append(' ', _spaces);
        _builder.Append(node.GetType().Name).Append(' ').Append(node.Traits);

        int attributes = 0;
        foreach (var (name, value) in values)
        {
            // Inputs are items whose value is a node; they are recursed into below, not printed inline.
            if (value is INode)
                continue;

            _builder.Append(attributes++ == 0 ? " (" : ", ").Append(name).Append('=').Append(value);
        }

        if (attributes > 0)
            _builder.Append(')');

        _builder.Append('\n');

        _spaces += 2;
        foreach (var input in node.Children)
            input.Explain(this);
        _spaces -= 2;
    }

}
