using Alembic.Algebra;
using Alembic.Plan.Rules;

namespace Alembic.Plan.Volcano;

/// <summary>
/// Replaces an <see cref="AbstractConverter"/> with a real conversion built from the registered
/// converter rules. A <see cref="VolcanoPlanner"/> registers one of these automatically.
/// </summary>
public sealed class ExpandConversionRule : IRule
{

    /// <inheritdoc />
    public Operand Operand => new Operand(static node => node is AbstractConverter);

    /// <inheritdoc />
    public void OnMatch(RuleCall call)
    {
        var converter = (AbstractConverter)call.Node(0);
        var planner = ((VolcanoRuleCall)call).Planner;

        var converted = planner.ChangeTraitsUsingConverters(converter.Input, converter.Traits);
        if (converted is not null)
            call.Transform(converted);
    }

}
