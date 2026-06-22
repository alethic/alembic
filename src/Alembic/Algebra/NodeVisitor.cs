namespace Alembic.Algebra;

/// <summary>
/// A depth-first walk over a node tree. Override <see cref="Visit(INode, int, INode?)"/> to act on each
/// node; the default descends into its children. Start a walk with <see cref="Visit(INode)"/>.
/// </summary>
/// <remarks>
/// This is Alembic's analog of Calcite's <c>RelVisitor</c>, with a simpler shape. Calcite's version
/// carries a mutable <c>root</c> field, a <c>replaceRoot</c> method, and a <c>go</c> entry point so that
/// a visitor can swap the tree's root out as it walks — a rewriting concern bolted onto a traversal. A
/// pure traversal needs none of that: there is just an entry point (<see cref="Visit(INode)"/>) and the
/// per-node hook. Rewriting, if ever needed, belongs in a separate shuttle. The walk reads
/// <see cref="INode.Children"/> directly rather than through a double-dispatch <c>accept</c>.
/// </remarks>
public abstract class NodeVisitor
{

    /// <summary>
    /// Visits the tree rooted at <paramref name="root"/>.
    /// </summary>
    public void Visit(INode root)
    {
        Visit(root, 0, null);
    }

    /// <summary>
    /// Visits <paramref name="node"/> — the input at <paramref name="ordinal"/> of <paramref name="parent"/>,
    /// or the root (ordinal 0, null parent). The default descends into the node's children; override to
    /// act on the node (call <see cref="VisitChildren"/> to continue the descent).
    /// </summary>
    public virtual void Visit(INode node, int ordinal, INode? parent)
    {
        VisitChildren(node);
    }

    /// <summary>
    /// Visits each child of <paramref name="node"/> in order. (Calcite's <c>childrenAccept</c>.)
    /// </summary>
    protected void VisitChildren(INode node)
    {
        var children = node.Children;
        for (int i = 0; i < children.Length; i++)
            Visit(children[i], i, node);
    }

}
