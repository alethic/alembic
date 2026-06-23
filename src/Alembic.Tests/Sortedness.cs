using Alembic.Plan;

namespace Alembic.Tests;

/// <summary>
/// A toy ordering trait with a real partial order: sorted data satisfies an unsorted requirement,
/// but not the other way around.
/// </summary>
sealed class Sortedness : ITrait
{

    public static readonly Sortedness Unsorted = new Sortedness();

    public static readonly Sortedness Sorted = new Sortedness();

    Sortedness()
    {

    }

    TraitDef ITrait.TraitDef => SortednessTraitDef.Instance;

    public bool Satisfies(ITrait other)
    {
        if (ReferenceEquals(this, other))
            return true;

        return ReferenceEquals(this, Sorted) && ReferenceEquals(other, Unsorted);
    }

}
