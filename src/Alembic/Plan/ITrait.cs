namespace Alembic.Plan;

/// <summary>
/// A single physical property of an op (a convention, an ordering, etc.). Traits are values:
/// equal traits must be <c>Equals</c>-equal.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTrait")]
public interface ITrait
{

    /// <summary>
    /// The dimension this trait belongs to.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTrait", "getTraitDef()")]
    TraitDef TraitDef { get; }

    /// <summary>
    /// Whether an op carrying this trait also satisfies a requirement for <paramref name="other"/>.
    /// Defaults to equality; traits with a partial order (orderings, distributions) override it.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTrait", "satisfies(RelTrait)")]
    bool Satisfies(ITrait other)
    {
        return this.Equals(other);
    }

    /// <summary>
    /// Registers this trait instance with the planner — an opportunity to add the rules that relate
    /// to it. Typical implementations do nothing.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelTrait", "register(RelOptPlanner)")]
    void Register(IPlanner planner)
    {

    }

}
