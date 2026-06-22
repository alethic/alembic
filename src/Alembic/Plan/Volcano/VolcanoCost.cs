using System;

namespace Alembic.Plan.Volcano;

/// <summary>
/// The cost-based planner's cost: a CPU estimate and an I/O estimate. Costs are compared on CPU and
/// combined by summing both dimensions; a cost is infinite when either dimension is.
/// </summary>
public sealed class VolcanoCost : ICost
{

    /// <summary>
    /// A factory producing <see cref="VolcanoCost"/> values.
    /// </summary>
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
    public double Cpu => _cpu;

    /// <summary>
    /// The estimated I/O usage.
    /// </summary>
    public double Io => _io;

    /// <inheritdoc />
    public bool IsInfinite => double.IsPositiveInfinity(_cpu) || double.IsPositiveInfinity(_io);

    /// <inheritdoc />
    public bool IsLessThanOrEqual(ICost other) => _cpu <= ((VolcanoCost)other)._cpu;

    /// <inheritdoc />
    public bool IsLessThan(ICost other) => _cpu < ((VolcanoCost)other)._cpu;

    /// <inheritdoc />
    public ICost Plus(ICost other)
    {
        var that = (VolcanoCost)other;
        if (IsInfinite || that.IsInfinite)
            return InfinityCost;

        return new VolcanoCost(_cpu + that._cpu, _io + that._io);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is VolcanoCost other && _cpu == other._cpu && _io == other._io;

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(_cpu, _io);

    /// <inheritdoc />
    public override string ToString() => $"{{{_cpu} cpu, {_io} io}}";

    sealed class VolcanoCostFactory : ICostFactory
    {

        public ICost MakeCost(double cpu, double io) => new VolcanoCost(cpu, io);

        public ICost MakeZeroCost() => ZeroCost;

        public ICost MakeInfiniteCost() => InfinityCost;

        public ICost MakeHugeCost() => HugeCost;

        public ICost MakeTinyCost() => TinyCost;

    }

}
