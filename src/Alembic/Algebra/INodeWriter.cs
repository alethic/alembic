namespace Alembic.Algebra;

/// <summary>
/// Collects a node's identity-bearing terms — its attributes and its inputs — as a node describes itself
/// through <see cref="INode.Explain"/>. Implementations consume the terms differently: one renders an
/// indented plan string (<see cref="NodeWriterImpl"/>), another builds the structural digest that drives
/// <see cref="INode.DeepEquals"/> / <see cref="INode.DeepHashCode"/>.
/// </summary>
[Provenance("org.apache.calcite.rel.RelWriter")]
public interface INodeWriter
{

    /// <summary>
    /// Adds an attribute term.
    /// </summary>
    [Provenance("org.apache.calcite.rel.RelWriter", "item(String, Object)")]
    INodeWriter Item(string name, object? value);

    /// <summary>
    /// Adds an attribute term only when <paramref name="condition"/> holds.
    /// </summary>
    [Provenance("org.apache.calcite.rel.RelWriter", "itemIf(String, Object, boolean)")]
    INodeWriter ItemIf(string name, object? value, bool condition) => condition ? Item(name, value) : this;

    /// <summary>
    /// Adds an input (child) term — an item whose value is a node.
    /// </summary>
    [Provenance("org.apache.calcite.rel.RelWriter", "input(String, RelNode)")]
    INodeWriter Input(string name, INode input) => Item(name, input);

    /// <summary>
    /// Signals that <paramref name="node"/>'s terms are complete, so the writer can emit it.
    /// </summary>
    [Provenance("org.apache.calcite.rel.RelWriter", "done(RelNode)")]
    INodeWriter Done(INode node);

}
