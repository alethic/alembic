namespace Alembic.Algebra;

/// <summary>
/// Collects a node's identity-bearing terms — its attributes and its inputs — as a node describes itself
/// through <see cref="INode.Explain"/>. Implementations consume the terms differently: one renders an
/// indented plan string (<see cref="NodeWriterImpl"/>), another builds the structural digest that drives
/// <see cref="INode.DeepEquals"/> / <see cref="INode.DeepHashCode"/>. The analog of Calcite's
/// <c>RelWriter</c>.
/// </summary>
public interface INodeWriter
{

    /// <summary>
    /// Adds an attribute term.
    /// </summary>
    INodeWriter Item(string name, object? value);

    /// <summary>
    /// Adds an attribute term only when <paramref name="condition"/> holds.
    /// </summary>
    INodeWriter ItemIf(string name, object? value, bool condition) => condition ? Item(name, value) : this;

    /// <summary>
    /// Adds an input (child) term. As in Calcite, an input is just an item whose value is a node.
    /// </summary>
    INodeWriter Input(string name, INode input) => Item(name, input);

    /// <summary>
    /// Signals that <paramref name="node"/>'s terms are complete, so the writer can emit it.
    /// </summary>
    INodeWriter Done(INode node);

}
