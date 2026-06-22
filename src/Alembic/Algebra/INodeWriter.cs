namespace Alembic.Algebra;

/// <summary>
/// Collects a node's identity-bearing terms — its own attributes and its inputs — as a sequence of
/// named values. A node contributes its terms through <see cref="AbstractNode.Explain"/>; the same terms
/// drive the structural <see cref="INode.DeepEquals"/> / <see cref="INode.DeepHashCode"/> contract.
/// A node's explain output doubles as its structural digest.
/// </summary>
public interface INodeWriter
{

    /// <summary>
    /// Adds an attribute term.
    /// </summary>
    INodeWriter Item(string name, object? value);

    /// <summary>
    /// Adds an input (child) term.
    /// </summary>
    INodeWriter Input(string name, INode input);

}
