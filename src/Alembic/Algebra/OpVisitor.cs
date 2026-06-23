namespace Alembic.Algebra;

/// <summary>
/// A depth-first walk over an op tree. Override <see cref="Visit(IOpNode, int, IOpNode?)"/> to act on each
/// op; the default descends into its children. Start a walk with <see cref="Visit(IOpNode)"/>.
/// </summary>
/// <remarks>
/// The shape is deliberately minimal: just an entry point (<see cref="Visit(IOpNode)"/>) and the per-op
/// hook. A rewriting-capable visitor would also carry a mutable root field, a replace-root method, and a
/// separate entry point so it could swap the tree's root out as it walks — but that bolts a rewriting
/// concern onto a traversal; rewriting, if ever needed, belongs in a separate shuttle. The walk reads
/// <see cref="IOpNode.Children"/> directly rather than through a double-dispatch <c>accept</c>.
/// </remarks>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelVisitor")]
public abstract class OpVisitor
{

    /// <summary>
    /// Visits the tree rooted at <paramref name="root"/>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelVisitor", "go(RelNode)")]
    public void Visit(IOpNode root)
    {
        Visit(root, 0, null);
    }

    /// <summary>
    /// Visits <paramref name="op"/> — the input at <paramref name="ordinal"/> of <paramref name="parent"/>,
    /// or the root (ordinal 0, null parent). The default descends into the op's children; override to
    /// act on the op (call <see cref="VisitChildren"/> to continue the descent).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelVisitor", "visit(RelNode, int, RelNode)")]
    public virtual void Visit(IOpNode op, int ordinal, IOpNode? parent)
    {
        VisitChildren(op);
    }

    /// <summary>
    /// Visits each child of <paramref name="op"/> in order.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelNode", "childrenAccept(RelVisitor)")]
    protected void VisitChildren(IOpNode op)
    {
        var children = op.Children;
        for (int i = 0; i < children.Length; i++)
            Visit(children[i], i, op);
    }

}
