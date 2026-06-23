using System;

namespace Alembic.Plan.Volcano;

/// <summary>
/// The cost-based planner's cost: a CPU estimate and an I/O estimate. Costs are compared on CPU and
/// combined by summing both dimensions; a cost is infinite when either dimension is.
/// </summary>
[Provenance("org.apache.calcite.plan.volcano.VolcanoCost")]
public sealed class VolcanoCost : ICost
{

    /// <summary>
    /// A factory producing <see cref="VolcanoCost"/> values.
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.VolcanoCost", "FACTORY")]
    public static readonly ICostFactory Factory = new VolcanoCostFactory();

    static readonly VolcanoCost InfinityCost = new VolcanoCost(double.PositiveInfinity, double.PositiveInfinity);
    static readonly VolcanoCost HugeCost = new VolcanoCost(double.MaxValue, double.MaxValue);
    static readonly VolcanoCost ZeroCost = new VolcanoCost(0.0, 0.0);
    static readonly VolcanoCost TinyCost = new VolcanoCost(1.0, 0.0);

    readonly double _cpu;
    readonly double _io;

    VolcanoCost(double cpu, double io)
    {
        _cpu = cpu;
        _io = io;
    }

    /// <summary>
    /// The estimated CPU usage.
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.VolcanoCost", "getCpu()")]
    public double Cpu => _cpu;

    /// <summary>
    /// The estimated I/O usage.
    /// </summary>
    [Provenance("org.apache.calcite.plan.volcano.VolcanoCost", "getIo()")]
    public double Io => _io;

    /// <inheritdoc />
    [Provenance("org.apache.calcite.plan.volcano.VolcanoCost", "isInfinite()")]
    public bool IsInfinite => double.IsPositiveInfinity(_cpu) || double.IsPositiveInfinity(_io);

    /// <inheritdoc />
    [Provenance("org.apache.calcite.plan.volcano.VolcanoCost", "isLe(RelOptCost)")]
    public bool IsLessThanOrEqual(ICost other) => _cpu <= ((VolcanoCost)other)._cpu;

    /// <inheritdoc />
    [Provenance("org.apache.calcite.plan.volcano.VolcanoCost", "isLt(RelOptCost)")]
    public bool IsLessThan(ICost other) => _cpu < ((VolcanoCost)other)._cpu;

    /// <inheritdoc />
    [Provenance("org.apache.calcite.plan.volcano.VolcanoCost", "plus(RelOptCost)")]
    public ICost Plus(ICost other)
    {
        var that = (VolcanoCost)other;
        if (IsInfinite || that.IsInfinite)
            return InfinityCost;

        return new VolcanoCost(_cpu + that._cpu, _io + that._io);
    }

    /// <inheritdoc />
    [Provenance("org.apache.calcite.plan.volcano.VolcanoCost", "minus(RelOptCost)")]
    public ICost Minus(ICost other)
    {
        if (IsInfinite)
            return this;

        var that = (VolcanoCost)other;
        return new VolcanoCost(_cpu - that._cpu, _io - that._io);
    }

    /// <inheritdoc />
    [Provenance("org.apache.calcite.plan.volcano.VolcanoCost", "multiplyBy(double)")]
    public ICost MultiplyBy(double factor)
    {
        if (this == InfinityCost)
            return this;

        return new VolcanoCost(_cpu * factor, _io * factor);
    }

    /// <inheritdoc />
    [Provenance("org.apache.calcite.plan.volcano.VolcanoCost", "divideBy(RelOptCost)")]
    public double DivideBy(ICost other)
    {
        // The geometric mean of the per-component ratios over the components non-zero and finite in both.
        var that = (VolcanoCost)other;
        double d = 1;
        int n = 0;

        if (_cpu != 0 && !double.IsInfinity(_cpu) && that._cpu != 0 && !double.IsInfinity(that._cpu))
        {
            d *= _cpu / that._cpu;
            n++;
        }

        if (_io != 0 && !double.IsInfinity(_io) && that._io != 0 && !double.IsInfinity(that._io))
        {
            d *= _io / that._io;
            n++;
        }

        return n == 0 ? 1.0 : Math.Pow(d, 1.0 / n);
    }

    /// <inheritdoc />
    [Provenance("org.apache.calcite.plan.volcano.VolcanoCost", "isEqWithEpsilon(RelOptCost)")]
    public bool IsEqWithEpsilon(ICost other)
    {
        return other is VolcanoCost that
            && (ReferenceEquals(this, that) || (Math.Abs(_cpu - that._cpu) < Epsilon && Math.Abs(_io - that._io) < Epsilon));
    }

    const double Epsilon = 1.0e-5;

    /// <inheritdoc />
    [Provenance("org.apache.calcite.plan.volcano.VolcanoCost", "equals(Object)")]
    public override bool Equals(object? obj) => obj is VolcanoCost other && _cpu == other._cpu && _io == other._io;

    /// <inheritdoc />
    [Provenance("org.apache.calcite.plan.volcano.VolcanoCost", "hashCode()")]
    public override int GetHashCode() => HashCode.Combine(_cpu, _io);

    /// <inheritdoc />
    [Provenance("org.apache.calcite.plan.volcano.VolcanoCost", "toString()")]
    public override string ToString() => $"{{{_cpu} cpu, {_io} io}}";

    [Provenance("org.apache.calcite.plan.volcano.VolcanoCost.Factory")]
    sealed class VolcanoCostFactory : ICostFactory
    {

        [Provenance("org.apache.calcite.plan.volcano.VolcanoCost.Factory", "makeCost(double, double, double)")]
        public ICost MakeCost(double cpu, double io) => new VolcanoCost(cpu, io);

        [Provenance("org.apache.calcite.plan.volcano.VolcanoCost.Factory", "makeZeroCost()")]
        public ICost MakeZeroCost() => ZeroCost;

        [Provenance("org.apache.calcite.plan.volcano.VolcanoCost.Factory", "makeInfiniteCost()")]
        public ICost MakeInfiniteCost() => InfinityCost;

        [Provenance("org.apache.calcite.plan.volcano.VolcanoCost.Factory", "makeHugeCost()")]
        public ICost MakeHugeCost() => HugeCost;

        [Provenance("org.apache.calcite.plan.volcano.VolcanoCost.Factory", "makeTinyCost()")]
        public ICost MakeTinyCost() => TinyCost;

    }

}
