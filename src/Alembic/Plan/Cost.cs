using System;

namespace Alembic.Plan;

/// <summary>
/// The default cost: a single scalar magnitude. Costs compare and combine on that one number, which is
/// enough for most consumers; a consumer that wants multiple dimensions can supply its own
/// <see cref="ICost"/>.
/// </summary>
public sealed class Cost : ICost
{

    /// <summary>
    /// A cost of zero.
    /// </summary>
    public static readonly Cost Zero = new Cost(0.0);

    /// <summary>
    /// A small positive cost.
    /// </summary>
    public static readonly Cost Tiny = new Cost(1.0);

    /// <summary>
    /// An enormous but finite cost.
    /// </summary>
    public static readonly Cost Huge = new Cost(double.MaxValue);

    /// <summary>
    /// An infinite cost.
    /// </summary>
    public static readonly Cost Infinity = new Cost(double.PositiveInfinity);

    /// <summary>
    /// A factory producing <see cref="Cost"/> values.
    /// </summary>
    public static readonly ICostFactory Factory = new CostFactory();

    readonly double _value;

    /// <summary>
    /// Creates a cost with the given scalar magnitude.
    /// </summary>
    public Cost(double value)
    {
        _value = value;
    }

    /// <summary>
    /// This cost's scalar magnitude.
    /// </summary>
    public double Value => _value;

    /// <inheritdoc />
    public double Cpu => _value;

    /// <inheritdoc />
    public double Io => 0.0;

    /// <inheritdoc />
    public bool IsInfinite => double.IsInfinity(_value);

    /// <inheritdoc />
    public bool IsLessThanOrEqual(ICost other) => _value <= ((Cost)other)._value;

    /// <inheritdoc />
    public bool IsLessThan(ICost other) => _value < ((Cost)other)._value;

    /// <inheritdoc />
    public ICost Plus(ICost other) => new Cost(_value + ((Cost)other)._value);

    /// <inheritdoc />
    public ICost Minus(ICost other) => IsInfinite ? this : new Cost(_value - ((Cost)other)._value);

    /// <inheritdoc />
    public ICost MultiplyBy(double factor) => IsInfinite ? this : new Cost(_value * factor);

    /// <inheritdoc />
    public double DivideBy(ICost other)
    {
        // A single component (the magnitude): the geometric mean reduces to its ratio when both are
        // non-zero and finite, otherwise 1.0.
        var that = ((Cost)other)._value;
        if (_value != 0 && !double.IsInfinity(_value) && that != 0 && !double.IsInfinity(that))
            return _value / that;

        return 1.0;
    }

    /// <inheritdoc />
    public bool IsEqWithEpsilon(ICost other)
    {
        return other is Cost that && (ReferenceEquals(this, that) || Math.Abs(_value - that._value) < Epsilon);
    }

    const double Epsilon = 1.0e-5;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Cost other && _value == other._value;

    /// <inheritdoc />
    public override int GetHashCode() => _value.GetHashCode();

    /// <inheritdoc />
    public override string ToString() => _value.ToString();

    sealed class CostFactory : ICostFactory
    {

        public ICost MakeCost(double cpu, double io) => new Cost(cpu);

        public ICost MakeZeroCost() => Zero;

        public ICost MakeInfiniteCost() => Infinity;

        public ICost MakeHugeCost() => Huge;

        public ICost MakeTinyCost() => Tiny;

    }

}
