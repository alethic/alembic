namespace Alembic.Algebra;

/// <summary>
/// A node's structural digest — the value the planner compares and hashes nodes by, since a node's own
/// <c>Equals</c>/<c>GetHashCode</c> stay as reference identity. Two digests are equal exactly when
/// their nodes are <see cref="INode.DeepEquals"/>.
/// </summary>
public interface INodeDigest
{

    /// <summary>
    /// The node this digest represents.
    /// </summary>
    INode Node { get; }

    /// <summary>
    /// Resets any cached state (e.g. the cached hash), so it is recomputed on next use.
    /// </summary>
    void Clear();

}
