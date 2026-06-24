using System;

namespace Alembic.Plan.Volcano;

/// <summary>
/// Indicates that planning timed out. This is not an error; the operation can be retried. A rule driver
/// catches it and returns the best plan found so far.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoTimeoutException")]
public sealed class VolcanoTimeoutException : Exception
{

    /// <summary>
    /// Creates the exception with a default message.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.volcano.VolcanoTimeoutException", "VolcanoTimeoutException()")]
    public VolcanoTimeoutException()
        : base("Volcano timeout")
    {
    }

}
