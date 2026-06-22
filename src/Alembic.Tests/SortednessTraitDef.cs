using Alembic.Plan;

namespace Alembic.Tests;

/// <summary>
/// The trait dimension for <see cref="Sortedness"/>.
/// </summary>
sealed class SortednessTraitDef : TraitDef<Sortedness>
{

    public static readonly SortednessTraitDef Instance = new SortednessTraitDef();

    SortednessTraitDef()
    {

    }

    public override string Name => "sortedness";

    public override Sortedness Default => Sortedness.Unsorted;

}
