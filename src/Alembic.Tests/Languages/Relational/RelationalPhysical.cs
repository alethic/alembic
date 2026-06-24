using Alembic.Plan;

using Alembic.Tests.Languages.Relational.Rules;

namespace Alembic.Tests.Languages.Relational;

/// <summary>
/// The physical convention of the relational language, which contributes the converter rules that
/// produce it — overriding the convention's register hook.
/// </summary>
sealed class RelationalPhysical : Convention
{

    public RelationalPhysical()
        : base("REL-PHYSICAL")
    {

    }

    public override void Register(IOpPlanner planner)
    {
        var physical = planner.EmptyTraitSet.Plus(this);
        planner.AddRule(new SourceConverter(physical));
        planner.AddRule(new FilterConverter(physical));
        planner.AddRule(new ParameterConverter(physical));
    }

}
