using Alembic.Util;

namespace Alembic.Plan;

/// <summary>
/// Allows optional configuration to be passed to a planner without a fixed constructor signature: the
/// planner unwraps whatever it understands (e.g. a <see cref="System.Threading.CancellationToken"/>).
/// Like Calcite's <c>Context</c>, it adds nothing of its own — it is a typed bag, an <see cref="IWrapper"/>.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.Context")]
public interface IContext : IWrapper
{
}
