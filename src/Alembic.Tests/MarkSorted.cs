using Alembic.Algebra;
using Alembic.Plan.Rules;

namespace Alembic.Tests;

/// <summary>
/// Marks any not-yet-sorted node as <see cref="Sortedness.Sorted"/>, proving a custom trait
/// dimension can be read and written through the engine and preserved across rewrites.
/// </summary>
sealed class MarkSorted : Rule
{

    public MarkSorted()
        : base(Any<INode>(node => !ReferenceEquals(node.Traits.Get(SortednessTraitDef.Instance), Sortedness.Sorted)))
    {
    }

    public override void OnMatch(RuleCall call)
    {
        var node = call.Node(0);
        var traits = node.Traits.Replace(SortednessTraitDef.Instance, Sortedness.Sorted);
        call.TransformTo(node.Copy(traits, node.Children));
    }

}
