namespace Alembic.Algebra;

/// <summary>
/// Collects an op's identity-bearing terms — its attributes and its inputs — as an op describes itself
/// through <see cref="IOpNode.Explain"/>. Implementations consume the terms differently: one renders an
/// indented plan string (<see cref="OpWriterImpl"/>), another builds the structural digest that drives
/// <see cref="IOpNode.DeepEquals"/> / <see cref="IOpNode.DeepHashCode"/>.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelWriter")]
public interface IOpWriter
{

    /// <summary>
    /// Adds an attribute term.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelWriter", "item(String, Object)")]
    IOpWriter Item(string name, object? value);

    /// <summary>
    /// Adds an attribute term only when <paramref name="condition"/> holds.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelWriter", "itemIf(String, Object, boolean)")]
    IOpWriter ItemIf(string name, object? value, bool condition) => condition ? Item(name, value) : this;

    /// <summary>
    /// Adds an input (child) term — an item whose value is an op.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelWriter", "input(String, RelNode)")]
    IOpWriter Input(string name, IOpNode input) => Item(name, input);

    /// <summary>
    /// Signals that <paramref name="op"/>'s terms are complete, so the writer can emit it.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelWriter", "done(RelNode)")]
    IOpWriter Done(IOpNode op);

}
