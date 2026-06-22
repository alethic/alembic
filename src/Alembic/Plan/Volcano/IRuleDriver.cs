using Alembic.Algebra;

namespace Alembic.Plan.Volcano;

/// <summary>
/// Applies the rule matches a planner discovers, according to some search strategy.
/// </summary>
public interface IRuleDriver
{

    /// <summary>
    /// The queue of pending matches this driver consumes.
    /// </summary>
    RuleQueue Queue { get; }

    /// <summary>
    /// Applies matches until the search is done.
    /// </summary>
    void Drive();

    /// <summary>
    /// Notifies the driver that a node has been added to a subset.
    /// </summary>
    void OnProduce(INode node, NodeSubset subset);

    /// <summary>
    /// Notifies the driver that two sets have been merged.
    /// </summary>
    void OnSetMerged(NodeSet set);

    /// <summary>
    /// Resets the driver.
    /// </summary>
    void Clear();

}
