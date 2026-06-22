namespace Alembic.Algebra;

/// <summary>
/// Display helpers for plans.
/// </summary>
public static class NodePlan
{

    /// <summary>
    /// Renders the node and its inputs as an indented plan tree, for display and debugging.
    /// </summary>
    public static string ToPlanString(this INode node)
    {
        return node is AbstractNode abstractNode ? abstractNode.RenderPlan() : node.GetType().Name;
    }

}
