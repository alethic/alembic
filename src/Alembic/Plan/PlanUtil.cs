using System;
using System.Text;

using Alembic.Algebra;

namespace Alembic.Plan;

/// <summary>
/// Optimizer utilities. Only the medium-agnostic members are ported; the relational ones (cast
/// creation, correlation-variable collection, field manipulation) are out of scope here. The output
/// type comparison itself lives on <see cref="IOutputType.IsEquivalentTo"/>, so Calcite's
/// <c>areRowTypesEqual</c> field walk is not ported separately.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptUtil")]
public static class PlanUtil
{

    /// <summary>
    /// Renders an op and its inputs as an indented plan string: it news up a writer and drives the
    /// op's <see cref="IOp.Explain"/>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptUtil", "toString(RelNode)")]
    public static string? ToString(IOp? op)
    {
        if (op is null)
            return null;

        var builder = new StringBuilder();
        op.Explain(new OpWriterImpl(builder, withIdPrefix: false));
        return builder.ToString();
    }

    /// <summary>
    /// Verifies that an op being added to an equivalence class has the same output type as the class's
    /// existing representative, throwing if not. (Calcite throws an <c>AssertionError</c>; the
    /// field-level type diff in its message is relational, so the message reduces to the two output
    /// types' renderings.)
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptUtil", "verifyTypeEquivalence(RelNode, RelNode, Object)")]
    public static void VerifyTypeEquivalence(IOp originalOp, IOp newOp, object equivalenceClass)
    {
        var expectedOutputType = originalOp.OutputType;
        var actualOutputType = newOp.OutputType;

        // Output types must be equivalent.
        if (expectedOutputType.IsEquivalentTo(actualOutputType))
            return;

        throw new InvalidOperationException(
            "Cannot add expression of different type to set:\n"
            + "set type is " + expectedOutputType
            + "\nexpression type is " + actualOutputType
            + "\nset is " + equivalenceClass
            + "\nexpression is " + ToString(newOp));
    }

}
