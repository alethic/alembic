namespace Alembic.Algebra;

/// <summary>
/// Collects an op's identity-bearing terms — its attributes and its inputs — as an op describes itself
/// through <see cref="IOp.Explain"/>. Implementations consume the terms differently: one renders an
/// indented plan string (<see cref="OpWriterImpl"/>), another builds the structural digest that drives
/// <see cref="IOp.DeepEquals"/> / <see cref="IOp.DeepHashCode"/>.
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
    /// Adds an attribute term whose value is typed, avoiding a box for value-typed <typeparamref name="T"/>.
    /// The default routes to <see cref="Item(string, object?)"/> (boxing as before); a writer that only
    /// needs the value's hash — e.g. the one behind <see cref="IOp.DeepHashCode"/> — overrides this to
    /// consume <paramref name="value"/> without boxing. (Alembic addition: Calcite's <c>RelWriter.item</c>
    /// takes a bare <c>Object</c>.)
    /// </summary>
    IOpWriter Item<T>(string name, T value) => Item(name, (object?)value);

    /// <summary>
    /// Adds an attribute term only when <paramref name="condition"/> holds.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelWriter", "itemIf(String, Object, boolean)")]
    IOpWriter ItemIf(string name, object? value, bool condition) => condition ? Item(name, value) : this;

    /// <summary>
    /// Adds an input (child) term — an item whose value is an op.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelWriter", "input(String, RelNode)")]
    IOpWriter Input(string name, IOp input) => Item(name, input);

    /// <summary>
    /// Signals that <paramref name="op"/>'s terms are complete, so the writer can emit it.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelWriter", "done(RelNode)")]
    IOpWriter Done(IOp op);

    /// <summary>
    /// Whether the writer renders nested values rather than flattening them. Defaults to <c>false</c>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelWriter", "nest()")]
    bool Nest => false;

    /// <summary>
    /// Whether the writer expands each op's detail when printing. Defaults to <c>false</c>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelWriter", "expand()")]
    bool Expand => false;

}
