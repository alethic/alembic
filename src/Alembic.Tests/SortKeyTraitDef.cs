using Alembic.Plan;

namespace Alembic.Tests;

/// <summary>
/// The trait dimension for <see cref="SortKey"/>.
/// </summary>
sealed class SortKeyTraitDef : OpTraitDef<SortKey>
{

    public static readonly SortKeyTraitDef Instance = new SortKeyTraitDef();

    SortKeyTraitDef()
    {

    }

    public override string Name => "sort-key";

    public override SortKey Default => SortKey.None;

}
