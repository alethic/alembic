using Alembic.Algebra;
using Alembic.Plan;

using Alembic.Tests.Languages.Relational.Physical;

namespace Alembic.Tests;

/// <summary>
/// The trait dimension for <see cref="Sortedness"/>. It can convert anything to sorted itself, by
/// wrapping the op in a <see cref="PhysicalSort"/> — an alternative to registering a converter rule,
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

    public override IOpNode? Convert(IPlanner planner, IOpNode op, ITrait toTrait, bool allowInfiniteCostConverters)
    {
        if (!toTrait.Equals(Sortedness.Sorted) || ReferenceEquals(op.Traits.Get(Instance), Sortedness.Sorted))
            return null;

        return new PhysicalSort(op.Traits.Plus(Sortedness.Sorted), op);
    }

}
