using Alembic.Algebra;
using Alembic.Plan;
using Alembic.Plan.Hep;
using Alembic.Plan.Rules;

using Alembic.Tests.Languages.Relational;
using Alembic.Tests.Languages.Relational.Logical;
using Alembic.Tests.Languages.Relational.Physical;
using Alembic.Tests.Languages.Relational.Rules;

using Xunit;

namespace Alembic.Tests.Holistic;

/// <summary>
/// Exercises the converter-program instructions: <c>AddConverters</c> (guaranteed converters, applied
/// where needed) and the bottom-up <see cref="TraitMatchingRule"/> for non-guaranteed converters.
/// </summary>
public class ConverterProgramTests
{

    static readonly OpTraitSet Logical = OpTraitSet.CreateEmpty().Plus(RelationalConventions.Logical);
    static readonly OpTraitSet Physical = OpTraitSet.CreateEmpty().Plus(RelationalConventions.Physical);

    [Fact]
    public void Add_converters_lowers_to_the_requested_convention()
    {
        var planner = new HepPlanner(HepProgram.Builder().AddConverters(true).Build());
        var cluster = new OpCluster(planner);
        IOp root = new LogicalFilter(Logical, new LogicalSource(cluster, Logical, "t"), "x > 5");

        planner.AddRule(new SourceConverter(Physical));
        planner.AddRule(new FilterConverter(Physical));
        planner.AddRule(new ParameterConverter(Physical));
        planner.SetRoot(root);
        planner.ChangeTraits(root, Physical);

        var filter = Assert.IsType<PhysicalFilter>(planner.FindBestPlan());
        Assert.IsType<PhysicalSource>(filter.Child);
    }

    [Fact]
    public void A_non_guaranteed_converter_fires_when_forced_or_bottom_up_via_trait_matching()
    {
        IOp Root(OpCluster cluster) => new LogicalFilter(Logical, new PhysicalSource(cluster, Physical, "t"), "x > 5");

        // AddRuleInstance applies the rule with forceConversions = true, and Calcite skips the
        // doesConverterApply gate for a force-converted non-guaranteed converter, so it fires directly.
        var forced = new HepPlanner(HepProgram.Builder().AddRuleInstance(new UnguaranteedFilterConverter(Physical)).Build());
        forced.SetRoot(Root(new OpCluster(forced)));
        Assert.IsType<PhysicalFilter>(forced.FindBestPlan());

        // AddConverters(false) installs a TraitMatchingRule, which fires the converter bottom-up
        // because the single input is already physical.
        var matched = new HepPlanner(HepProgram.Builder().AddConverters(false).Build());
        matched.AddRule(new UnguaranteedFilterConverter(Physical));
        matched.SetRoot(Root(new OpCluster(matched)));
        Assert.IsType<PhysicalFilter>(matched.FindBestPlan());
    }

    /// <summary>
    /// A filter converter that declares itself non-guaranteed, so it is applied only bottom-up.
    /// </summary>
    sealed class UnguaranteedFilterConverter : ConverterRule
    {

        readonly OpTraitSet _physical;

        public UnguaranteedFilterConverter(OpTraitSet physical)
            : base(RelationalConventions.Logical, RelationalConventions.Physical)
        {
            _physical = physical;
        }

        public override bool IsGuaranteed => false;

        public override IOp? Convert(IOp op)
        {
            return op is LogicalFilter filter ? new PhysicalFilter(_physical, filter.Input, filter.Predicate) : null;
        }

    }

}
