using System.Text;

using Alembic.Algebra;

namespace Alembic.Plan;

/// <summary>
/// Optimizer utilities. Only the medium-agnostic members are ported; the relational ones (row-type
/// comparison, cast creation, correlation-variable collection, type-equivalence checks) are out of
/// scope here.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptUtil")]
public static class PlanUtil
{

    /// <summary>
    /// Renders an op and its inputs as an indented plan string: it news up a writer and drives the
    /// op's <see cref="IOpNode.Explain"/>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptUtil", "toString(RelNode)")]
    public static string ToString(IOpNode op)
    {
        var builder = new StringBuilder();
        op.Explain(new OpWriterImpl(builder));
        return builder.ToString().TrimEnd();
    }

}
