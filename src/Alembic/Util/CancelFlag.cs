using System.Threading;

namespace Alembic.Util;

/// <summary>
/// A flag for cooperatively cancelling a long-running plan (e.g. on a timeout). A caller requests
/// cancellation, typically from another thread; the planner observes it via
/// <see cref="AbstractOpPlanner.CheckCancel"/> as it fires rules. Port of Calcite's
/// <c>org.apache.calcite.util.CancelFlag</c>, which wraps an <c>AtomicBoolean</c>.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.CancelFlag")]
public sealed class CancelFlag
{

    int _flag;

    /// <summary>
    /// Whether cancellation has been requested.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.CancelFlag", "isCancelRequested()")]
    public bool IsCancelRequested => Volatile.Read(ref _flag) != 0;

    /// <summary>
    /// Requests cancellation.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.CancelFlag", "requestCancel()")]
    public void RequestCancel() => Interlocked.Exchange(ref _flag, 1);

    /// <summary>
    /// Clears a prior cancellation request, so the flag can be reused.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.CancelFlag", "clearCancel()")]
    public void ClearCancel() => Interlocked.Exchange(ref _flag, 0);

}
