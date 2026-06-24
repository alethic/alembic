using System;

namespace Alembic.Plan;

/// <summary>
/// The default cost, defined in terms of a single scalar quantity. Somewhat arbitrarily, it reports zero
/// for both CPU and I/O and uses the scalar only to compare and combine costs; a consumer that wants
/// multiple dimensions can supply its own <see cref="IOpCost"/>.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCostImpl")]
public class OpCost : IOpCost
{

    /// <summary>
    /// A factory producing <see cref="OpCost"/> values.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCostImpl", "FACTORY")]
    public static readonly IOpCostFactory Factory = new CostFactory();

    readonly double _value;

    /// <summary>
    /// Creates a cost with the given scalar magnitude.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCostImpl", "RelOptCostImpl(double)")]
    public OpCost(double value)
    {
        _value = value;
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCostImpl", "getCpu()")]
    public double Cpu => 0;

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCostImpl", "getIo()")]
    public double Io => 0;

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCostImpl", "isInfinite()")]
    public bool IsInfinite => double.IsInfinity(_value);

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCostImpl", "isLe(RelOptCost)")]
    public bool IsLessThanOrEqual(IOpCost other) => _value <= ((OpCost)other)._value;

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCostImpl", "isLt(RelOptCost)")]
    public bool IsLessThan(IOpCost other) => _value < ((OpCost)other)._value;

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCostImpl", "plus(RelOptCost)")]
    public IOpCost Plus(IOpCost other) => new OpCost(_value + ((OpCost)other)._value);

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCostImpl", "minus(RelOptCost)")]
    public IOpCost Minus(IOpCost other) => new OpCost(_value - ((OpCost)other)._value);

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCostImpl", "multiplyBy(double)")]
    public IOpCost MultiplyBy(double factor) => new OpCost(_value * factor);

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCostImpl", "divideBy(RelOptCost)")]
    public double DivideBy(IOpCost other) => _value / ((OpCost)other)._value;

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCostImpl", "isEqWithEpsilon(RelOptCost)")]
    public bool IsEqWithEpsilon(IOpCost other) => Math.Abs(_value - ((OpCost)other)._value) < Epsilon;

    const double Epsilon = 1.0e-5;

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCostImpl", "equals(Object)")]
    public override bool Equals(object? obj) => obj is OpCost other && _value == other._value;

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCostImpl", "hashCode()")]
    public override int GetHashCode() => _value.GetHashCode();

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCostImpl", "toString()")]
    public override string ToString() => IOpCost.ToString(_value);

    /// <summary>
    /// The factory for <see cref="OpCost"/> instances.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCostImpl.Factory")]
    sealed class CostFactory : IOpCostFactory
    {

        /// <inheritdoc/>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCostImpl.Factory", "makeCost(double, double, double)")]
        public IOpCost MakeCost(double cpu, double io) => new OpCost(cpu);

        /// <inheritdoc/>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCostImpl.Factory", "makeZeroCost()")]
        public IOpCost MakeZeroCost() => new OpCost(0.0);

        /// <inheritdoc/>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCostImpl.Factory", "makeInfiniteCost()")]
        public IOpCost MakeInfiniteCost() => new OpCost(double.PositiveInfinity);

        /// <inheritdoc/>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCostImpl.Factory", "makeHugeCost()")]
        public IOpCost MakeHugeCost() => new OpCost(double.MaxValue);

        /// <inheritdoc/>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCostImpl.Factory", "makeTinyCost()")]
        public IOpCost MakeTinyCost() => new OpCost(1.0);

    }

}
