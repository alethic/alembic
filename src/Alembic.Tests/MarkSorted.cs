using Alembic.Algebra;
using Alembic.Plan.Rules;

namespace Alembic.Tests;

/// <summary>
/// Marks any not-yet-sorted op as <see cref="Sortedness.Sorted"/>, proving a custom trait
/// dimension can be read and written through the engine and preserved across rewrites.
/// </summary>
sealed class MarkSorted : Rule
{

    public MarkSorted()
        : base(Any<IOpNode>(op => !ReferenceEquals(op.Traits.Get(SortednessTraitDef.Instance), Sortedness.Sorted)))
    {
    }

    public override void OnMatch(RuleCall call)
    {
        var op = call.Op(0);
        var traits = op.Traits.Replace(SortednessTraitDef.Instance, Sortedness.Sorted);
        call.TransformTo(op.Copy(traits, op.Children));
    }

}
