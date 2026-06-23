using Alembic.Algebra;

namespace Alembic.Plan.Rules;

/// <summary>
/// Adapts a converter rule, restricting it to fire only when its single input already carries the
/// converter's target trait. Used with the heuristic planner to minimize converters: a node is
/// converted bottom-up, once its input has been converted.
/// </summary>
[Provenance("org.apache.calcite.rel.convert.TraitMatchingRule")]
public sealed class TraitMatchingRule : Rule
{

    readonly ConverterRule _converterRule;

    [Provenance("org.apache.calcite.rel.convert.TraitMatchingRule", "TraitMatchingRule(ConverterRule)")]
    public TraitMatchingRule(ConverterRule converterRule)
        : base(BuildOperand(converterRule))
    {
        _converterRule = converterRule;
    }

    /// <inheritdoc />
    [Provenance("org.apache.calcite.rel.convert.TraitMatchingRule", "config(ConverterRule, RelBuilderFactory)")]
    public override string Description => "TraitMatchingRule: " + _converterRule.Description;

    /// <inheritdoc />
    [Provenance("org.apache.calcite.rel.convert.TraitMatchingRule", "onMatch(RelOptRuleCall)")]
    public override void OnMatch(RuleCall call)
    {
        var input = call.Node(1);
        if (input.Traits.Contains(_converterRule.Target))
            _converterRule.OnMatch(call);
    }

    // The converter matches any node carrying its source trait; here we additionally require a single
    // input and bind it, so the rule can inspect its traits.
    [Provenance("org.apache.calcite.rel.convert.TraitMatchingRule", "config(ConverterRule, RelBuilderFactory)")]
    static RuleOperand BuildOperand(ConverterRule converterRule)
    {
        var converterOperand = converterRule.Operand;
        return new RuleOperand(converterOperand.MatchedClass, converterOperand.Trait, converterOperand.Predicate, RuleOperandChildPolicy.Some, new RuleOperand(typeof(INode), RuleOperandChildPolicy.Any));
    }

}
