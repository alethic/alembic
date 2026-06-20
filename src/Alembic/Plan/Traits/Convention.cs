using System;

namespace Alembic.Plan.Traits;

/// <summary>
/// A calling convention — the "family" a node belongs to (logical, or some physical backend).
/// Converter rules move nodes between conventions; a fully lowered plan is one whose nodes are
/// all in a target convention.
/// </summary>
public sealed class Convention : ITrait
{

    /// <summary>
    /// The default convention carried by a node that has not been assigned one.
    /// </summary>
    public static readonly Convention None = new Convention("NONE");

    readonly string _name;

    /// <summary>
    /// Creates a convention with the given name. Conventions are equal by name.
    /// </summary>
    public Convention(string name)
    {
        _name = name;
    }

    /// <summary>
    /// This convention's name.
    /// </summary>
    public string Name => _name;

    ITraitDef ITrait.Def => ConventionTraitDef.Instance;

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
    public override string ToString()
    {
        return _name;
    }

}
