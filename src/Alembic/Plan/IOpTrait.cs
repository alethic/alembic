namespace Alembic.Plan;

/// <summary>
/// A single physical property of an op (a convention, an ordering, etc.). Traits are values:
/// equal traits must be <c>Equals</c>-equal.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTrait")]
public interface IOpTrait
{

    /// <summary>
    /// The dimension this trait belongs to.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTrait", "getTraitDef()")]
    OpTraitDef TraitDef { get; }

    /// <summary>
    /// Whether an op carrying this trait also satisfies a requirement for <paramref name="other"/>.
    /// A trait that satisfies only itself implements this as equality; traits with a partial order
    /// (orderings, distributions) compare more loosely. (Abstract on the interface, as in Calcite.)
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTrait", "satisfies(RelTrait)")]
    bool Satisfies(IOpTrait other);

    /// <summary>
    /// Registers this trait instance with the planner — an opportunity to add the rules that relate
    /// to it. Typical implementations do nothing. (Abstract on the interface, as in Calcite.)
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTrait", "register(RelOptPlanner)")]
    void Register(IOpPlanner planner);

    /// <summary>
    /// Whether this trait is its dimension's default value.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTrait", "isDefault()")]
    bool IsDefault => ReferenceEquals(this, TraitDef.Default);

}
