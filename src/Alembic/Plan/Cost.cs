using System;

namespace Alembic.Plan;

/// <summary>
/// The default cost, defined in terms of a single scalar quantity. Somewhat arbitrarily, it reports zero
/// for both CPU and I/O and uses the scalar only to compare and combine costs; a consumer that wants
/// multiple dimensions can supply its own <see cref="ICost"/>.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCostImpl")]
public sealed class Cost : ICost
{

    /// <summary>
    /// A factory producing <see cref="Cost"/> values.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCostImpl", "FACTORY")]
    public static readonly ICostFactory Factory = new CostFactory();

    readonly double _value;

    /// <summary>
    /// Creates a cost with the given scalar magnitude.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCostImpl", "RelOptCostImpl(double)")]
    public Cost(double value)
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
    public bool IsLessThanOrEqual(ICost other) => _value <= ((Cost)other)._value;

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCostImpl", "isLt(RelOptCost)")]
    public bool IsLessThan(ICost other) => _value < ((Cost)other)._value;

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCostImpl", "plus(RelOptCost)")]
    public ICost Plus(ICost other) => new Cost(_value + ((Cost)other)._value);

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCostImpl", "minus(RelOptCost)")]
    public ICost Minus(ICost other) => new Cost(_value - ((Cost)other)._value);

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCostImpl", "multiplyBy(double)")]
    public ICost MultiplyBy(double factor) => new Cost(_value * factor);

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCostImpl", "divideBy(RelOptCost)")]
    public double DivideBy(ICost other) => _value / ((Cost)other)._value;

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCostImpl", "isEqWithEpsilon(RelOptCost)")]
    public bool IsEqWithEpsilon(ICost other) => Math.Abs(_value - ((Cost)other)._value) < Epsilon;

    const double Epsilon = 1.0e-5;

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCostImpl", "equals(Object)")]
    public override bool Equals(object? obj) => obj is Cost other && _value == other._value;

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCostImpl", "hashCode()")]
    public override int GetHashCode() => _value.GetHashCode();

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCostImpl", "toString()")]
    public override string ToString() => _value.ToString();

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCostImpl.Factory")]
    sealed class CostFactory : ICostFactory
    {

        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCostImpl.Factory", "makeCost(double, double, double)")]
        public ICost MakeCost(double cpu, double io) => new Cost(cpu);

        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCostImpl.Factory", "makeZeroCost()")]
        public ICost MakeZeroCost() => new Cost(0.0);

        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCostImpl.Factory", "makeInfiniteCost()")]
        public ICost MakeInfiniteCost() => new Cost(double.PositiveInfinity);

        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCostImpl.Factory", "makeHugeCost()")]
        public ICost MakeHugeCost() => new Cost(double.MaxValue);

        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.RelOptCostImpl.Factory", "makeTinyCost()")]
        public ICost MakeTinyCost() => new Cost(1.0);

    }

}
