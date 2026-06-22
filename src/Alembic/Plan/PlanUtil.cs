using System.Text;

using Alembic.Algebra;

namespace Alembic.Plan;

/// <summary>
/// Optimizer utilities — the analog of Calcite's <c>RelOptUtil</c>. Only the medium-agnostic members are
/// ported; Calcite's relational ones (row-type comparison, cast creation, correlation-variable
/// collection, type-equivalence checks) are out of scope here.
/// </summary>
public static class PlanUtil
{

    /// <summary>
    /// Renders a node and its inputs as an indented plan string. Calcite's <c>RelOptUtil.toString(rel)</c>:
    /// it news up a writer and drives the node's <see cref="INode.Explain"/>.
    /// </summary>
    public static string ToString(INode node)
    {
        var builder = new StringBuilder();
        node.Explain(new NodeWriterImpl(builder));
        return builder.ToString().TrimEnd();
    }

}
