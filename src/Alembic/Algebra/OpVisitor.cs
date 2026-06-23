namespace Alembic.Algebra;

/// <summary>
/// A visitor that walks an op tree depth-first. Override <see cref="Visit(IOp, int, IOp?)"/> to act on
/// each op; the default descends into its children (via <see cref="IOp.ChildrenAccept"/>). Start a walk
/// with <see cref="Go(IOp)"/>, which records the root and returns it — a visitor may swap the root out
/// mid-walk with <see cref="ReplaceRoot"/>.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelVisitor")]
public abstract class OpVisitor
{

    IOp? _root;

    /// <summary>
    /// Visits <paramref name="node"/> — the input at <paramref name="ordinal"/> of <paramref name="parent"/>,
    /// or the root (ordinal 0, null parent). The default descends into the op's children; override to act
    /// on the op.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelVisitor", "visit(RelNode, int, RelNode)")]
    public virtual void Visit(IOp node, int ordinal, IOp? parent)
    {
        node.ChildrenAccept(this);
    }

    /// <summary>
    /// Replaces the root recorded for the current walk — for a visitor that rewrites the tree as it goes.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelVisitor", "replaceRoot(RelNode)")]
    public void ReplaceRoot(IOp? node)
    {
        _root = node;
    }

    /// <summary>
    /// Walks the tree rooted at <paramref name="p"/>, returning the (possibly replaced) root.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.RelVisitor", "go(RelNode)")]
    public IOp? Go(IOp p)
    {
        _root = p;
        Visit(p, 0, null);
        return _root;
    }

}
