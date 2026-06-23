using System;

using Alembic.Algebra;

namespace Alembic.Plan;

/// <summary>
/// The default implementation of <see cref="IConvention"/> — equal by name, unsealed so a consumer can
/// subclass it to bring its own lowering rules.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.Convention.Impl")]
public class Convention : IConvention
{

    /// <summary>
    /// The default convention carried by an op that has not been assigned one.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.Convention", "NONE")]
    public static readonly Convention None = new Convention("NONE", typeof(IOp));

    readonly string _name;
    readonly Type _interface;

    /// <summary>
    /// Creates a convention with the given name and no op-interface marker (members may be any
    /// <see cref="IOp"/>).
    /// </summary>
    public Convention(string name)
        : this(name, typeof(IOp))
    {

    }

    /// <summary>
    /// Creates a convention with the given name and the op interface its members must implement.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.Convention.Impl", "Impl(String, Class)")]
    public Convention(string name, Type @interface)
    {
        _name = name;
        _interface = @interface;
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.Convention.Impl", "getName()")]
    public string Name => _name;

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.Convention.Impl", "getInterface()")]
    public Type Interface => _interface;

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.Convention.Impl", "getTraitDef()")]
    OpTraitDef IOpTrait.TraitDef => ConventionTraitDef.Instance;

    /// <summary>
    /// A convention satisfies only itself.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.Convention.Impl", "satisfies(RelTrait)")]
    public virtual bool Satisfies(IOpTrait trait)
    {
        return ReferenceEquals(this, trait);
    }

    /// <summary>
    /// Registers the rules that produce this convention with the planner (declared on
    /// <see cref="IOpTrait"/>). The default does nothing; a convention that brings its own lowering rules
    /// overrides this.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.Convention.Impl", "register(RelOptPlanner)")]
    public virtual void Register(IOpPlanner planner)
    {

    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.Convention.Impl", "canConvertConvention(Convention)")]
    public virtual bool CanConvertConvention(IConvention toConvention) => false;

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.Convention.Impl", "useAbstractConvertersForConversion(RelTraitSet, RelTraitSet)")]
    public virtual bool UseAbstractConvertersForConversion(OpTraitSet fromTraits, OpTraitSet toTraits) => false;

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.Convention.Impl", "enforce(RelNode, RelTraitSet)")]
    public virtual IOp? Enforce(IOp input, OpTraitSet required) => null;

    // Calcite's Convention.Impl overrides neither equals nor hashCode: conventions are singletons,
    // compared by reference identity (see Satisfies). Alembic follows suit — no by-name override.

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.Convention.Impl", "toString()")]
    public override string ToString()
    {
        return _name;
    }

}
