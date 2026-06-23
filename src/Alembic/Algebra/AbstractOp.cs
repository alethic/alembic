using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text;

using Alembic.Plan;

namespace Alembic.Algebra;

/// <summary>
/// Convenience base for <see cref="IOp"/> implementations. An op lists its identity-bearing terms
/// in <see cref="ExplainTerms"/> — its own attributes and its inputs — and the base derives
/// <see cref="DeepEquals"/> / <see cref="DeepHashCode"/> from them. Each op keeps one
/// <see cref="IOpDigest"/> that caches its hash.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.AbstractRelNode")]
public abstract class AbstractOp : IOp
{

    readonly InnerOpDigest _digest;

    /// <summary>
    /// Initializes the op with its cluster, traits, and children.
    /// </summary>
    // The source of op ids, handed out in creation order. Atomic, as in Calcite's NEXT_ID.
    static int _nextId;

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.AbstractRelNode", "AbstractRelNode(RelOptCluster, RelTraitSet)")]
    protected AbstractOp(OpCluster cluster, OpTraitSet traits)
    {
        Cluster = cluster;
        Traits = traits;
        Id = System.Threading.Interlocked.Increment(ref _nextId) - 1;
        _digest = new InnerOpDigest(this);
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.AbstractRelNode", "getId()")]
    public int Id { get; }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.AbstractRelNode", "getCluster()")]
    public OpCluster Cluster { get; }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.AbstractRelNode", "getTraitSet()")]
    public OpTraitSet Traits { get; }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.AbstractRelNode", "getInputs()")]
    public virtual ImmutableArray<IOp> Children => ImmutableArray<IOp>.Empty;

    /// <summary>
    /// Lists this op's identity-bearing terms. A subclass calls <c>base.ExplainTerms</c>, then adds its
    /// own attributes (<see cref="IOpWriter.Item"/>) and its inputs (<see cref="IOpWriter.Input"/>).
    /// Two ops of the same type with equal traits and equal terms are structurally equivalent; an op
    /// that omits a term excludes it from that comparison, so inputs must be listed here.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.AbstractRelNode", "explainTerms(RelWriter)")]
    public virtual IOpWriter ExplainTerms(IOpWriter writer)
    {
        return writer;
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.AbstractRelNode", "explain(RelWriter)")]
    public void Explain(IOpWriter writer)
    {
        ExplainTerms(writer).Done(this);
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.AbstractRelNode", "childrenAccept(RelVisitor)")]
    public void ChildrenAccept(OpVisitor visitor)
    {
        var inputs = Children;
        for (int i = 0; i < inputs.Length; i++)
            visitor.Visit(inputs[i], i, this);
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.AbstractRelNode", "copy(RelTraitSet, List<RelNode>)")]
    public abstract IOp Copy(OpTraitSet traits, ImmutableArray<IOp> children);

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.AbstractRelNode", "replaceInput(int, RelNode)")]
    public virtual void ReplaceInput(int ordinalInParent, IOp p)
    {
        throw new NotSupportedException("replaceInput called on " + this);
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.AbstractRelNode", "computeSelfCost(RelOptPlanner, RelMetadataQuery)")]
    public virtual IOpCost ComputeSelfCost(IOpPlanner planner)
    {
        return planner.CostFactory.MakeTinyCost();
    }

    /// <summary>
    /// This op's kept digest. Returning the same instance lets the planner reuse its cached hash.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.AbstractRelNode", "getRelDigest()")]
    public IOpDigest GetDigest()
    {
        return _digest;
    }

    /// <summary>
    /// Discards this op's cached digest, so it is recomputed on next use.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.AbstractRelNode", "recomputeDigest()")]
    public void RecomputeDigest()
    {
        _digest.Clear();
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.AbstractRelNode", "onRegister(RelOptPlanner)")]
    public virtual IOp OnRegister(IOpPlanner planner)
    {
        var oldInputs = Children;
        var inputs = ImmutableArray.CreateBuilder<IOp>(oldInputs.Length);
        var changed = false;
        foreach (var input in oldInputs)
        {
            var registered = planner.EnsureRegistered(input, null);
            if (!ReferenceEquals(registered, input))
                changed = true;

            inputs.Add(registered);
        }

        IOp r = this;
        if (changed)
            r = Copy(Traits, inputs.MoveToImmutable());

        r.RecomputeDigest();
        return r;
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.AbstractRelNode", "deepEquals(Object)")]
    public virtual bool DeepEquals(IOp? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is null || GetType() != other.GetType()) return false;

        var that = (AbstractOp)other;
        if (!Traits.Equals(that.Traits)) return false;

        var a = DigestItems();
        var b = that.DigestItems();
        if (a.Count != b.Count) return false;

        for (int i = 0; i < a.Count; i++)
        {
            var v1 = a[i].Value;
            var v2 = b[i].Value;
            if (v1 is IOp n1)
            {
                // Calcite compares op-valued items by value only (deepEquals) — the term name is not part
                // of the comparison for inputs.
                if (v2 is not IOp n2 || !n1.DeepEquals(n2)) return false;
            }
            else
            {
                // Non-op items compare as a whole (name, value) entry, per Calcite's Map.Entry.equals.
                if (!string.Equals(a[i].Name, b[i].Name, StringComparison.Ordinal)) return false;
                if (!Equals(v1, v2)) return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.AbstractRelNode", "deepHashCode()")]
    public virtual int DeepHashCode()
    {
        // Calcite folds the trait set and the term *values* only — not the op type, not the term names —
        // with a fixed 31-based accumulation: 31 + traitSet.hashCode(), then result*31 + valueHash.
        int result = 31 + Traits.GetHashCode();
        foreach (var (_, value) in DigestItems())
        {
            int h = value is null ? 0 : value is IOp op ? op.DeepHashCode() : value.GetHashCode();
            result = result * 31 + h;
        }

        return result;
    }

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.AbstractRelNode", "getDigestItems()")]
    List<(string Name, object? Value)> DigestItems()
    {
        var writer = new DigestWriter();
        ExplainTerms(writer);
        return writer.Items;
    }

    /// <summary>
    /// The digest kept by each op: it caches its hash and delegates equality to the op's
    /// <see cref="DeepEquals"/>. Because it is nested it can reach the op's <see cref="ExplainTerms"/> to
    /// render the digest string.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.AbstractRelNode.InnerRelDigest")]
    sealed class InnerOpDigest : IOpDigest
    {

        readonly AbstractOp _op;
        int _hash;

        public InnerOpDigest(AbstractOp op)
        {
            _op = op;
        }

        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.AbstractRelNode.InnerRelDigest", "getRel()")]
        public IOp Op => _op;

        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.AbstractRelNode.InnerRelDigest", "clear()")]
        public void Clear() => _hash = 0;

        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.AbstractRelNode.InnerRelDigest", "equals(Object)")]
        public override bool Equals(object? obj)
        {
            return obj is IOpDigest other && _op.DeepEquals(other.Op);
        }

        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.AbstractRelNode.InnerRelDigest", "hashCode()")]
        public override int GetHashCode()
        {
            if (_hash == 0)
            {
                _hash = _op.DeepHashCode();
                if (_hash == 0) _hash = 1;
            }

            return _hash;
        }

        public override string ToString()
        {
            var writer = new DigestWriter();
            _op.Explain(writer);
            return writer.Digest;
        }

    }

    /// <summary>
    /// Collects an op's explain terms and, on <see cref="Done"/>, renders them into the digest string
    /// (inputs are referenced by type, not recursed). <see cref="Items"/> also backs the term-by-term
    /// <see cref="DeepEquals"/> comparison.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.AbstractRelNode.RelDigestWriter")]
    sealed class DigestWriter : IOpWriter
    {

        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.AbstractRelNode.RelDigestWriter", "attrs")]
        internal readonly List<(string Name, object? Value)> Items = new List<(string, object?)>();

        /// <summary>
        /// The rendered digest string, available after <see cref="Done"/>.
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.AbstractRelNode.RelDigestWriter", "digest")]
        internal string Digest = "";

        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.AbstractRelNode.RelDigestWriter", "item(String, Object)")]
        public IOpWriter Item(string name, object? value)
        {
            // We can't rely on value-based hashCode/equals for an array, so stringify it (per-instance,
            // matching Calcite's `"" + value`, i.e. Object.toString = type@identityHash).
            if (value is Array)
                value = value.GetType().Name + "@" + RuntimeHelpers.GetHashCode(value).ToString("x");

            Items.Add((name, value));
            return this;
        }

        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.AbstractRelNode.RelDigestWriter", "done(RelNode)")]
        public IOpWriter Done(IOp op)
        {
            var sb = new StringBuilder();
            sb.Append(op.GetType().Name).Append('.').Append(op.Traits).Append('(');
            for (int i = 0; i < Items.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(Items[i].Name).Append('=');
                if (Items[i].Value is IOp input)
                    sb.Append(input.GetType().Name).Append('#').Append(input.Id);
                else
                    sb.Append(Items[i].Value);
            }

            sb.Append(')');
            Digest = sb.ToString();
            return this;
        }

    }

}
