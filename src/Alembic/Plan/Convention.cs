using System;

using Alembic.Algebra;

namespace Alembic.Plan;

/// <summary>
/// The default implementation of <see cref="IConvention"/> — equal by name, unsealed so a consumer can
/// subclass it to bring its own lowering rules.
/// </summary>
[Provenance("org.apache.calcite.plan.Convention.Impl")]
public class Convention : IConvention
{

    /// <summary>
    /// The default convention carried by a node that has not been assigned one.
    /// </summary>
    [Provenance("org.apache.calcite.plan.Convention", "NONE")]
    public static readonly Convention None = new Convention("NONE", typeof(INode));

    readonly string _name;
    readonly Type _interface;

    /// <summary>
    /// Creates a convention with the given name and no node-interface marker (members may be any
    /// <see cref="INode"/>).
    /// </summary>
    public Convention(string name)
        : this(name, typeof(INode))
    {

    }

    /// <summary>
    /// Creates a convention with the given name and the node interface its members must implement.
    /// </summary>
    [Provenance("org.apache.calcite.plan.Convention.Impl", "Impl(String, Class)")]
    public Convention(string name, Type @interface)
    {
        _name = name;
        _interface = @interface;
    }

    /// <inheritdoc />
    [Provenance("org.apache.calcite.plan.Convention.Impl", "getName()")]
    public string Name => _name;

    /// <inheritdoc />
    [Provenance("org.apache.calcite.plan.Convention.Impl", "getInterface()")]
    public Type Interface => _interface;

    [Provenance("org.apache.calcite.plan.Convention.Impl", "getTraitDef()")]
    TraitDef ITrait.TraitDef => ConventionTraitDef.Instance;

    /// <summary>
    /// A convention satisfies only itself.
    /// </summary>
    [Provenance("org.apache.calcite.plan.Convention.Impl", "satisfies(RelTrait)")]
    public virtual bool Satisfies(ITrait trait)
    {
        return Equals(trait);
    }

    /// <summary>
    /// Registers the rules that produce this convention with the planner (declared on
    /// <see cref="ITrait"/>). The default does nothing; a convention that brings its own lowering rules
    /// overrides this.
    /// </summary>
    [Provenance("org.apache.calcite.plan.Convention.Impl", "register(RelOptPlanner)")]
    public virtual void Register(IPlanner planner)
    {

    }

    /// <inheritdoc />
    [Provenance("org.apache.calcite.plan.Convention.Impl", "canConvertConvention(Convention)")]
    public virtual bool CanConvertConvention(IConvention toConvention) => false;

    /// <inheritdoc />
    [Provenance("org.apache.calcite.plan.Convention.Impl", "useAbstractConvertersForConversion(RelTraitSet, RelTraitSet)")]
    public virtual bool UseAbstractConvertersForConversion(TraitSet fromTraits, TraitSet toTraits) => false;

    /// <inheritdoc />
    [Provenance("org.apache.calcite.plan.Convention.Impl", "enforce(RelNode, RelTraitSet)")]
    public virtual INode? Enforce(INode input, TraitSet required) => null;

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is Convention other && string.Equals(_name, other._name, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return _name.GetHashCode();
    }

    /// <inheritdoc />
    [Provenance("org.apache.calcite.plan.Convention.Impl", "toString()")]
    public override string ToString()
    {
        return _name;
    }

}
