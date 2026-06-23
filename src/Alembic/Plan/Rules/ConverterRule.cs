using Alembic.Algebra;

namespace Alembic.Plan.Rules;

/// <summary>
/// A rule that converts an op from one trait value to another — most commonly a convention, but any
/// trait (sortedness, distribution, …). The author supplies <see cref="Source"/>, <see cref="Target"/>,
/// and <see cref="Convert"/>; the operand (matching any op carrying the <see cref="Source"/> trait) and
/// the match action are provided here. <see cref="Convert"/> returns the converted op, or null to
/// decline (when it does not handle this op's kind).
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.convert.ConverterRule")]
public abstract class ConverterRule : OpRule
{

    /// <summary>
    /// Initializes the rule with the traits it converts between.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.convert.ConverterRule", "ConverterRule(Class<? extends RelNode>, RelTrait, RelTrait, String)")]
    protected ConverterRule(IOpTrait source, IOpTrait target)
        : base(ConvertOperand<IOp>(source))
    {
        Source = source;
        Target = target;
    }

    /// <summary>
    /// The trait this rule converts from.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.convert.ConverterRule", "getInTrait()")]
    public IOpTrait Source { get; }

    /// <summary>
    /// The trait this rule converts to.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.convert.ConverterRule", "getOutTrait()")]
    public IOpTrait Target { get; }

    /// <summary>
    /// The trait dimension this rule converts on (the dimension of <see cref="Source"/> and
    /// <see cref="Target"/>, which share it).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.convert.ConverterRule", "getTraitDef()")]
    public OpTraitDef TraitDef => Source.TraitDef;

    /// <summary>
    /// Whether this converter always produces its <see cref="Target"/>. A non-guaranteed converter (the
    /// default) is applied bottom-up, via a <see cref="TraitMatchingRule"/>, only where its input is
    /// already in the target form; a converter that always succeeds overrides this to <c>true</c>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.convert.ConverterRule", "isGuaranteed()")]
    public virtual bool IsGuaranteed => false;

    /// <summary>
    /// Converts the op from <see cref="Source"/> to <see cref="Target"/>, or returns null to decline.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.convert.ConverterRule", "convert(RelNode)")]
    public abstract IOp? Convert(IOp op);

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.convert.ConverterRule", "onMatch(RelOptRuleCall)")]
    public override void OnMatch(OpRuleCall call)
    {
        var op = call.Op(0);
        if (op.Traits.Contains(Source))
        {
            var converted = Convert(op);
            if (converted is not null)
                call.TransformTo(converted);
        }
    }

}
