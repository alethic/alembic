using Alembic.Algebra;

namespace Alembic.Plan.Rules;

/// <summary>
/// Adapts a converter rule, restricting it to fire only when its single input already carries the
/// converter's target trait. Used with the heuristic planner to minimize converters: an op is
/// converted bottom-up, once its input has been converted.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.convert.TraitMatchingRule")]
public sealed class TraitMatchingRule : OpRule
{

    readonly ConverterRule _converterRule;

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.convert.TraitMatchingRule", "TraitMatchingRule(ConverterRule)")]
    public TraitMatchingRule(ConverterRule converterRule)
        : base(BuildOperand(converterRule))
    {
        _converterRule = converterRule;
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.convert.TraitMatchingRule", "config(ConverterRule, RelBuilderFactory)")]
    public override string Description => "TraitMatchingRule: " + _converterRule.Description;

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.convert.TraitMatchingRule", "onMatch(RelOptRuleCall)")]
    public override void OnMatch(OpRuleCall call)
    {
        var input = call.Op(1);
        if (input.Traits.Contains(_converterRule.Target))
            _converterRule.OnMatch(call);
    }

    // The converter matches any op carrying its source trait; here we additionally require a single
    // input and bind it, so the rule can inspect its traits.
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.convert.TraitMatchingRule", "config(ConverterRule, RelBuilderFactory)")]
    static OpRuleOperand BuildOperand(ConverterRule converterRule)
    {
        var converterOperand = converterRule.Operand;
        return new OpRuleOperand(converterOperand.MatchedClass, converterOperand.Trait, converterOperand.Predicate, RuleOperandChildPolicy.Some, new OpRuleOperand(typeof(IOp), RuleOperandChildPolicy.Any));
    }

}
