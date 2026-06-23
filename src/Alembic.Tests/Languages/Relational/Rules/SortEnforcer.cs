using Alembic.Algebra;
using Alembic.Plan.Rules;

using Alembic.Tests.Languages.Relational.Physical;

namespace Alembic.Tests.Languages.Relational.Rules;

/// <summary>
/// Converts an unsorted node into a sorted one by wrapping it in a <see cref="PhysicalSort"/>. The
/// trait it converts is sortedness — not a convention — so this exercises converter rules over an
/// arbitrary trait dimension.
/// </summary>
sealed class SortEnforcer : ConverterRule
{

    public SortEnforcer()
        : base(Sortedness.Unsorted, Sortedness.Sorted)
    {

    }

    public override bool IsGuaranteed => true;

    public override INode? Convert(INode node)
    {
        // The operand also admits already-sorted nodes (sorted satisfies an unsorted requirement),
        // so decline those rather than stacking a redundant sort.
        if (ReferenceEquals(node.Traits.Get(SortednessTraitDef.Instance), Sortedness.Sorted))
            return null;

        var sorted = node.Traits.Plus(Sortedness.Sorted);
        return new PhysicalSort(sorted, node);
    }

}
