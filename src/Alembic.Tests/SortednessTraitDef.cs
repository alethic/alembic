using Alembic.Algebra;
using Alembic.Plan;

using Alembic.Tests.Languages.Relational.Physical;

namespace Alembic.Tests;

/// <summary>
/// The trait dimension for <see cref="Sortedness"/>. It can convert anything to sorted itself, by
/// wrapping the node in a <see cref="PhysicalSort"/> — an alternative to registering a converter rule,
/// exercising the trait-def conversion hooks.
/// </summary>
sealed class SortednessTraitDef : TraitDef<Sortedness>
{

    public static readonly SortednessTraitDef Instance = new SortednessTraitDef();

    SortednessTraitDef()
    {

    }

    public override string Name => "sortedness";

    public override Sortedness Default => Sortedness.Unsorted;

    public override bool CanConvert(IPlanner planner, ITrait fromTrait, ITrait toTrait)
    {
        return toTrait.Equals(Sortedness.Sorted);
    }

    public override INode? Convert(IPlanner planner, INode node, ITrait toTrait, bool allowInfiniteCostConverters)
    {
        if (!toTrait.Equals(Sortedness.Sorted) || ReferenceEquals(node.Traits.Get(Instance), Sortedness.Sorted))
            return null;

        return new PhysicalSort(node.Traits.Plus(Sortedness.Sorted), node);
    }

}
