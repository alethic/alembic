using Alembic.Plan;

namespace Alembic.Tests.Languages.Relational;

/// <summary>
/// The two conventions of the toy relational language.
/// </summary>
static class RelationalConventions
{

    public static readonly Convention Logical = new Convention("REL-LOGICAL");

    public static readonly Convention Physical = new RelationalPhysical();

}
