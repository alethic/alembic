using Alembic.Plan;

using Alembic.Tests.Languages.Expression.Rules;

namespace Alembic.Tests.Languages.Expression;

/// <summary>
/// The physical convention of the expression language, which contributes the converter rules that
/// lower each logical op into its physical counterpart.
/// </summary>
sealed class ExpressionPhysical : Convention
{

    public ExpressionPhysical()
        : base("EXPR-PHYSICAL")
    {

    }

    public override void Register(IOpPlanner planner)
    {
        var physical = planner.EmptyTraitSet.Plus(this);
        planner.AddRule(new LiteralConverter(physical));
        planner.AddRule(new VariableConverter(physical));
        planner.AddRule(new AddConverter(physical));
        planner.AddRule(new MultiplyConverter(physical));
    }

}
