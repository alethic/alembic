namespace Alembic.Algebra;

/// <summary>
/// The trivial output type: the single "no meaningful output" type. A singleton, equivalent only to
/// itself — not an absorbing "matches anything" type. A medium that does not type its outputs leaves
/// every op <see cref="Instance">Void</see>, so all ops share one output type and merge freely. It is
/// the default <see cref="AbstractOp.DeriveOutputType"/> returns.
/// </summary>
[Provenance(ProvenanceSource.Local)]
public sealed class VoidOutputType : IOutputType
{

    /// <summary>
    /// The singleton instance.
    /// </summary>
    [Provenance(ProvenanceSource.Local)]
    public static readonly VoidOutputType Instance = new VoidOutputType();

    VoidOutputType()
    {
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Local)]
    public bool IsEquivalentTo(IOutputType other) => other is VoidOutputType;

}
