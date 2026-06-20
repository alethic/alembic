namespace Alembic.Plan.Traits;

/// <summary>
/// A trait dimension — the set of mutually exclusive values a node may carry along one axis
/// (e.g. convention). Defs are singletons and are registered with a <see cref="TraitContext"/>,
/// which assigns each an ordinal.
/// </summary>
public interface ITraitDef
{

    /// <summary>
    /// A stable name for this dimension.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The value a node carries on this dimension when none is specified.
    /// </summary>
    ITrait Default { get; }

}
