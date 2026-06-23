using System.Text;

using Alembic.Algebra;

namespace Alembic.Plan;

/// <summary>
/// Optimizer utilities. Only the medium-agnostic members are ported; the relational ones (row-type
/// comparison, cast creation, correlation-variable collection, type-equivalence checks) are out of
/// scope here.
/// </summary>
[Provenance("org.apache.calcite.plan.RelOptUtil")]
public static class PlanUtil
{

    /// <summary>
    /// Renders a node and its inputs as an indented plan string: it news up a writer and drives the
    /// node's <see cref="INode.Explain"/>.
    /// </summary>
    [Provenance("org.apache.calcite.plan.RelOptUtil", "toString(RelNode)")]
    public static string ToString(INode node)
    {
        var builder = new StringBuilder();
        node.Explain(new NodeWriterImpl(builder));
        return builder.ToString().TrimEnd();
    }

}
