namespace Alembic.Plan;

/// <summary>
/// A marker for a callback that turns a tree of nodes into an executable plan. Each calling convention
/// typically has its own protocol for walking the tree and a matching implementor.
/// </summary>
public interface INodeImplementor
{
}
