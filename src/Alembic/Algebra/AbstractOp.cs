using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

using Alembic.Algebra.Metadata;
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

    // Lazily derived and cached, as in Calcite's AbstractRelNode.rowType (MonotonicNonNull).
    IOutputType? _outputType;

    // The source of op ids, handed out in creation order. Atomic, as in Calcite's NEXT_ID.
    static int _nextId;

    /// <summary>
    /// Initializes the op with its <paramref name="cluster"/> and <paramref name="traits"/>, assigning it
    /// the next op id.
    /// </summary>
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
    public virtual ImmutableArray<IOp> Inputs => ImmutableArray<IOp>.Empty;

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.AbstractRelNode", "getConvention()")]
    public virtual IConvention? Convention => Traits.Convention;

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.AbstractRelNode", "getRowType()")]
    public IOutputType OutputType => _outputType ??= DeriveOutputType();

    /// <summary>
    /// Derives this op's <see cref="OutputType"/>. The base returns <see cref="VoidOutputType"/> — the
    /// trivial "no meaningful output" type — for ops that attach no meaning to their output; a subclass
    /// overrides to describe what it produces. (Calcite's base throws <c>UnsupportedOperationException</c>;
    /// Alembic defaults to <c>Void</c> so an output-agnostic medium need not implement it.)
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.AbstractRelNode", "deriveRowType()")]
    protected virtual IOutputType DeriveOutputType() => VoidOutputType.Instance;

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
    public virtual void Explain(IOpWriter writer)
    {
        ExplainTerms(writer).Done(this);
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.AbstractRelNode", "childrenAccept(RelVisitor)")]
    public void ChildrenAccept(OpVisitor visitor)
    {
        var inputs = Inputs;
        for (int i = 0; i < inputs.Length; i++)
            visitor.Visit(inputs[i], i, this);
    }

    /// <inheritdoc />
    /// <remarks>
    /// An op with zero inputs need not override this: empty set equals empty set, so the unchanged case
    /// returns <c>this</c>. Any op that can actually be copied must override it.
    /// </remarks>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.AbstractRelNode", "copy(RelTraitSet, List<RelNode>)")]
    public virtual IOp Copy(OpTraitSet traits, ImmutableArray<IOp> children)
    {
        if (System.Linq.Enumerable.SequenceEqual(Inputs, children) && ReferenceEquals(traits, Traits))
            return this;

        throw new InvalidOperationException("Op should override Copy. Class=[" + GetType()
            + "]; traits=[" + Traits + "]; desired traits=[" + traits + "]");
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.AbstractRelNode", "replaceInput(int, RelNode)")]
    public virtual void ReplaceInput(int ordinalInParent, IOp p)
    {
        throw new NotSupportedException("replaceInput called on " + this);
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.AbstractRelNode", "computeSelfCost(RelOptPlanner, RelMetadataQuery)")]
    public virtual IOpCost? ComputeSelfCost(IOpPlanner planner, OpMetadataQuery mq)
    {
        return planner.CostFactory.MakeTinyCost();
    }

    /// <summary>
    /// This op's kept digest. Returning the same instance lets the planner reuse its cached hash.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.AbstractRelNode", "getRelDigest()")]
    public IOpDigest GetOpDigest()
    {
        return _digest;
    }

    /// <summary>
    /// This op's digest in string form: the object digest's rendering.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.AbstractRelNode", "getDigest()")]
    public virtual string GetDigest() => GetOpDigest().ToString() ?? "";

    /// <summary>
    /// Discards this op's cached digest, so it is recomputed on next use.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.AbstractRelNode", "recomputeDigest()")]
    public void RecomputeDigest()
    {
        _digest.Clear();
    }

    /// <summary>
    /// This op's string form: <c>"rel#" + id + ':' + getDigest()</c>, where the digest string is
    /// <see cref="GetDigest"/>'s rendering.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.AbstractRelNode", "toString()")]
    public override string ToString()
    {
        return "op#" + Id + ":" + GetDigest();
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.AbstractRelNode", "onRegister(RelOptPlanner)")]
    public virtual IOp OnRegister(IOpPlanner planner)
    {
        var oldInputs = Inputs;
        var builder = ImmutableArray.CreateBuilder<IOp>(oldInputs.Length);
        foreach (var input in oldInputs)
        {
            var e = planner.EnsureRegistered(input, null);
            Debug.Assert(ReferenceEquals(e, input) || input.OutputType.IsEquivalentTo(e.OutputType));
            builder.Add(e);
        }

        var inputs = builder.MoveToImmutable();

        IOp r = this;
        if (!Alembic.Util.Util.EqualShallow(oldInputs, inputs))
            r = Copy(Traits, inputs);

        r.RecomputeDigest();
        System.Diagnostics.Debug.Assert(r.IsValid(Alembic.Util.Litmus.Throw, IOp.IContext.Empty));
        return r;
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.AbstractRelNode", "isValid(Litmus, Context)")]
    public virtual bool IsValid(Alembic.Util.Litmus litmus, IOp.IContext context) => litmus.Succeed();

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.AbstractRelNode", "register(RelOptPlanner)")]
    public virtual void Register(IOpPlanner planner)
    {
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.AbstractRelNode", "deepEquals(Object)")]
    public virtual bool DeepEquals(IOp? other)
    {
        if (ReferenceEquals(this, other))
            return true;
        if (other is null || GetType() != other.GetType())
            return false;

        var that = (AbstractOp)other;
        if (!Traits.Equals(that.Traits))
            return false;
        if (!OutputType.IsEquivalentTo(that.OutputType))
            return false;

        // The item buffers are pooled and returned below; the planner probes its digest maps constantly
        // and every hit calls this to confirm the key, so allocating two lists per call dominated planning.
        var wa = DigestItems();
        var wb = that.DigestItems();
        try
        {
            var a = wa.Items;
            var b = wb.Items;
            if (a.Count != b.Count)
                return false;

            for (int i = 0; i < a.Count; i++)
            {
                var v1 = a[i].Value;
                var v2 = b[i].Value;
                if (v1 is IOp n1)
                {
                    // Calcite compares op-valued items by value only (deepEquals) — the term name is not
                    // part of the comparison for inputs.
                    if (v2 is not IOp n2 || !n1.DeepEquals(n2))
                        return false;
                }
                else
                {
                    // Non-op items compare as a whole (name, value) entry, per Calcite's Map.Entry.equals.
                    if (!string.Equals(a[i].Name, b[i].Name, StringComparison.Ordinal))
                        return false;
                    if (!Equals(v1, v2))
                        return false;
                }
            }

            return true;
        }
        finally
        {
            DigestWriter.Return(wa);
            DigestWriter.Return(wb);
        }
    }

    /// <inheritdoc />
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.AbstractRelNode", "deepHashCode()")]
    public virtual int DeepHashCode()
    {
        // Calcite folds the trait set and the term *values* only — not the op type, not the term names —
        // with a fixed 31-based accumulation: 31 + traitSet.hashCode(), then result*31 + valueHash. The
        // fold streams through a pooled HashWriter so it neither materializes the item list nor boxes
        // value-typed terms; the result is identical to folding over DigestItems().
        var writer = HashWriter.Rent(31 + Traits.GetHashCode());
        ExplainTerms(writer);
        int result = writer.Hash;
        HashWriter.Return(writer);
        return result;
    }

    /// <summary>
    /// Returns a pooled DigestWriter (not a bare list as Calcite's getDigestItems does) so DeepEquals can
    /// return the buffer for reuse; the caller must DigestWriter.Return it. Its Items are the digest items.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.AbstractRelNode", "getDigestItems()")]
    DigestWriter DigestItems()
    {
        var writer = DigestWriter.Rent();
        ExplainTerms(writer);
        return writer;
    }

    /// <summary>
    /// Normalizes a term value for digesting: an array can't be compared or hashed by value, so it is
    /// stringified per-instance (matching Calcite's <c>"" + value</c>, i.e. <c>Object.toString</c> =
    /// <c>type@identityHash</c>). Every other value passes through unchanged. Shared so the hashing path
    /// (<see cref="HashWriter"/>) and the equality/rendering path (<see cref="DigestWriter"/>) agree.
    /// </summary>
    static object? NormalizeItemValue(object? value)
        => value is Array
            ? value.GetType().Name + "@" + RuntimeHelpers.GetHashCode(value).ToString("x")
            : value;

    /// <summary>
    /// An <see cref="IOpWriter"/> that folds an op's terms straight into a running hash — the 31-based
    /// accumulation Calcite's <c>deepHashCode()</c> uses — without materializing the item list or boxing
    /// value-typed terms. Instances are pooled per thread (<see cref="Rent"/>/<see cref="Return"/>) so a
    /// hash computation allocates nothing in steady state; the per-thread free list also covers the
    /// re-entrancy of op-valued terms, which recurse into a nested <see cref="DeepHashCode"/>.
    /// </summary>
    sealed class HashWriter : IOpWriter
    {

        [ThreadStatic]
        static Stack<HashWriter>? _pool;

        /// <summary>The running hash; seeded by <see cref="Rent"/>, read back after <see cref="ExplainTerms"/>.</summary>
        internal int Hash;

        /// <summary>Rents a writer seeded with <paramref name="seed"/> (<c>31 + traits.hashCode()</c>).</summary>
        internal static HashWriter Rent(int seed)
        {
            var pool = _pool;
            var writer = pool is not null && pool.Count > 0 ? pool.Pop() : new HashWriter();
            writer.Hash = seed;
            return writer;
        }

        /// <summary>Returns a writer to the per-thread free list for reuse.</summary>
        internal static void Return(HashWriter writer)
            => (_pool ??= new Stack<HashWriter>()).Push(writer);

        void Fold(int h) => Hash = Hash * 31 + h;

        /// <inheritdoc/>
        public IOpWriter Item(string name, object? value)
        {
            if (value is null)
                Fold(0);
            else if (value is IOp op)
                Fold(op.DeepHashCode());
            else
                Fold(NormalizeItemValue(value)!.GetHashCode());

            return this;
        }

        /// <inheritdoc/>
        public IOpWriter Item<T>(string name, T value)
        {
            if (value is null)
                Fold(0);
            else if (value is IOp op)
                Fold(op.DeepHashCode());
            else if (value is Array)
                Fold(NormalizeItemValue(value)!.GetHashCode());
            else
                // EqualityComparer<T>.Default.GetHashCode matches value.GetHashCode() for the boxed value,
                // so the fold is identical to the boxing path — just without the box for value types.
                Fold(EqualityComparer<T>.Default.GetHashCode(value));

            return this;
        }

        /// <inheritdoc/>
        public IOpWriter Done(IOp op) => this;

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

        /// <summary>
        /// Creates the digest kept by <paramref name="op"/>.
        /// </summary>
        public InnerOpDigest(AbstractOp op)
        {
            _op = op;
        }

        /// <inheritdoc/>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.AbstractRelNode.InnerRelDigest", "getRel()")]
        public IOp Op => _op;

        /// <inheritdoc/>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.AbstractRelNode.InnerRelDigest", "clear()")]
        public void Clear() => _hash = 0;

        /// <inheritdoc/>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.AbstractRelNode.InnerRelDigest", "equals(Object)")]
        public override bool Equals(object? obj)
        {
            return obj is IOpDigest other && _op.DeepEquals(other.Op);
        }

        /// <inheritdoc/>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.AbstractRelNode.InnerRelDigest", "hashCode()")]
        public override int GetHashCode()
        {
            if (_hash == 0)
            {
                _hash = _op.DeepHashCode();
                if (_hash == 0)
                    _hash = 1;
            }

            return _hash;
        }

        /// <inheritdoc/>
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

        [ThreadStatic]
        static Stack<DigestWriter>? _pool;

        /// <summary>
        /// Rents a cleared writer for collecting an op's digest items (see <see cref="DigestItems"/>).
        /// Pooled per thread — with re-entrant returns to cover the recursion of op-valued terms — so the
        /// item-collection <see cref="DeepEquals"/> depends on allocates nothing in steady state.
        /// </summary>
        internal static DigestWriter Rent()
        {
            var pool = _pool;
            var writer = pool is not null && pool.Count > 0 ? pool.Pop() : new DigestWriter();
            writer.Items.Clear();
            return writer;
        }

        /// <summary>Returns a writer to the per-thread free list for reuse.</summary>
        internal static void Return(DigestWriter writer)
            => (_pool ??= new Stack<DigestWriter>()).Push(writer);

        /// <inheritdoc/>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.AbstractRelNode.RelDigestWriter", "item(String, Object)")]
        public IOpWriter Item(string name, object? value)
        {
            Items.Add((name, NormalizeItemValue(value)));
            return this;
        }

        /// <inheritdoc/>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.rel.AbstractRelNode.RelDigestWriter", "done(RelNode)")]
        public IOpWriter Done(IOp op)
        {
            var sb = new StringBuilder();
            sb.Append(op.GetType().Name).Append('.').Append(op.Traits).Append('(');
            for (int i = 0; i < Items.Count; i++)
            {
                if (i > 0)
                    sb.Append(',');
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
