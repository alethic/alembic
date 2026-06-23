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

    static readonly TraitSet Logical = TraitSet.CreateEmpty().Plus(RelationalConventions.Logical);
    static readonly TraitSet Physical = TraitSet.CreateEmpty().Plus(RelationalConventions.Physical);

    [Fact]
    public void Add_converters_lowers_to_the_requested_convention()
    {
        var planner = new HepPlanner(HepProgram.Builder().AddConverters(true).Build());
        var cluster = new Cluster(planner);
        IOpNode root = new LogicalFilter(Logical, new LogicalSource(cluster, Logical, "t"), "x > 5");

        planner.AddRule(new SourceConverter(Physical));
        planner.AddRule(new FilterConverter(Physical));
        planner.AddRule(new ParameterConverter(Physical));
        planner.SetRoot(root);
        planner.ChangeTraits(root, Physical);

        var filter = Assert.IsType<PhysicalFilter>(planner.FindBestPlan());
        Assert.IsType<PhysicalSource>(filter.Child);
    }

    [Fact]
    public void A_non_guaranteed_converter_fires_bottom_up_only_through_trait_matching()
    {
        // The input is already physical, but the filter's converter is non-guaranteed and no parent
        // requests the physical trait, so the raw converter never fires on its own.
        IOpNode Root(Cluster cluster) => new LogicalFilter(Logical, new PhysicalSource(cluster, Physical, "t"), "x > 5");

        // The raw converter alone: doesConverterApply is false (no parent wants physical, not the root
        // request), so the filter stays logical.
        var raw = new HepPlanner(HepProgram.Builder().AddRuleInstance(new UnguaranteedFilterConverter(Physical)).Build());
        raw.SetRoot(Root(new Cluster(raw)));
        Assert.IsType<LogicalFilter>(raw.FindBestPlan());

        // AddConverters(false) also installs a TraitMatchingRule, which fires the converter bottom-up
        // because the single input is already physical.
        var matched = new HepPlanner(HepProgram.Builder().AddConverters(false).Build());
        matched.AddRule(new UnguaranteedFilterConverter(Physical));
        matched.SetRoot(Root(new Cluster(matched)));
        Assert.IsType<PhysicalFilter>(matched.FindBestPlan());
    }

    /// <summary>
    /// A filter converter that declares itself non-guaranteed, so it is applied only bottom-up.
    /// </summary>
    sealed class UnguaranteedFilterConverter : ConverterRule
    {

        readonly TraitSet _physical;

        public UnguaranteedFilterConverter(TraitSet physical)
            : base(RelationalConventions.Logical, RelationalConventions.Physical)
        {
            _physical = physical;
        }

        public override bool IsGuaranteed => false;

        public override IOpNode? Convert(IOpNode op)
        {
            return op is LogicalFilter filter ? new PhysicalFilter(_physical, filter.Input, filter.Predicate) : null;
        }

    }

}
