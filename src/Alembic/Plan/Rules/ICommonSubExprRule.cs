namespace Alembic.Plan.Rules;

/// <summary>
/// A marker that a rule (a <see cref="Rule"/>) should be attempted only on common sub-expressions —
/// nodes that more than one parent shares. A
/// <see cref="Alembic.Plan.Hep.HepProgramBuilder.AddCommonRelSubExprInstruction"/> instruction fires
/// such rules, skipping nodes with a single parent.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.CommonRelSubExprRule")]
public interface ICommonSubExprRule
{

}
