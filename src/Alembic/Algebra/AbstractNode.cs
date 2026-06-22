using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

using Alembic.Plan;

namespace Alembic.Algebra;

/// <summary>
/// Convenience base for <see cref="INode"/> implementations. A node lists its identity-bearing terms
/// in <see cref="ExplainTerms"/> — its own attributes and its inputs — and the base derives
/// <see cref="DeepEquals"/> / <see cref="DeepHashCode"/> from them. Each node keeps one
/// <see cref="INodeDigest"/> that caches its hash.
/// </summary>
public abstract class AbstractNode : INode
{

    readonly InnerNodeDigest _digest;

    /// <summary>
    /// Initializes the node with its cluster, traits, and children.
    /// </summary>
    protected AbstractNode(Cluster cluster, TraitSet traits, ImmutableArray<INode> children)
    {
        Cluster = cluster;
        Traits = traits;
        Children = children;
        _digest = new InnerNodeDigest(this);
    }

    /// <inheritdoc />
    public Cluster Cluster { get; }

    /// <inheritdoc />
    public TraitSet Traits { get; }

    /// <inheritdoc />
    public ImmutableArray<INode> Children { get; }

    /// <summary>
    /// Lists this node's identity-bearing terms. A subclass calls <c>base.ExplainTerms</c>, then adds its
    /// own attributes (<see cref="INodeWriter.Item"/>) and its inputs (<see cref="INodeWriter.Input"/>).
    /// Two nodes of the same type with equal traits and equal terms are structurally equivalent; a node
    /// that omits a term excludes it from that comparison, so inputs must be listed here.
    /// </summary>
    public virtual INodeWriter ExplainTerms(INodeWriter writer)
    {
        return writer;
    }

    /// <inheritdoc />
    public void Explain(INodeWriter writer)
    {
        ExplainTerms(writer).Done(this);
    }

    /// <inheritdoc />
    public abstract INode Copy(TraitSet traits, ImmutableArray<INode> children);

    /// <inheritdoc />
    public virtual ICost ComputeSelfCost(IPlanner planner)
    {
        return planner.CostFactory.MakeTinyCost();
    }

    /// <summary>
    /// This node's kept digest. Returning the same instance lets the planner reuse its cached hash.
    /// </summary>
    public INodeDigest GetDigest()
    {
        return _digest;
    }

    /// <summary>
    /// Discards this node's cached digest, so it is recomputed on next use.
    /// </summary>
    public void RecomputeDigest()
    {
        _digest.Clear();
    }

    /// <inheritdoc />
    public virtual bool DeepEquals(INode? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is null || GetType() != other.GetType()) return false;

        var that = (AbstractNode)other;
        if (!Traits.Equals(that.Traits)) return false;

        var a = DigestItems();
        var b = that.DigestItems();
        if (a.Count != b.Count) return false;

        for (int i = 0; i < a.Count; i++)
        {
            if (!string.Equals(a[i].Name, b[i].Name, StringComparison.Ordinal)) return false;

            var v1 = a[i].Value;
            var v2 = b[i].Value;
            if (v1 is INode n1)
            {
                if (v2 is not INode n2 || !n1.DeepEquals(n2)) return false;
            }
            else if (!Equals(v1, v2))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public virtual int DeepHashCode()
    {
        var h = new HashCode();
        h.Add(GetType());
        h.Add(Traits);
        foreach (var (name, value) in DigestItems())
        {
            h.Add(name);
            h.Add(value is INode node ? node.DeepHashCode() : value);
        }

        return h.ToHashCode();
    }

    List<(string Name, object? Value)> DigestItems()
    {
        var writer = new DigestWriter();
        ExplainTerms(writer);
        return writer.Items;
    }

    /// <summary>
    /// The digest kept by each node: it caches its hash and delegates equality to the node's
    /// <see cref="DeepEquals"/>. Because it is nested it can reach the node's <see cref="ExplainTerms"/> to
    /// render the digest string.
    /// </summary>
    sealed class InnerNodeDigest : INodeDigest
    {

        readonly AbstractNode _node;
        int _hash;

        public InnerNodeDigest(AbstractNode node)
        {
            _node = node;
        }

        public INode Node => _node;

        public void Clear() => _hash = 0;

        public override bool Equals(object? obj)
        {
            return obj is INodeDigest other && _node.DeepEquals(other.Node);
        }

        public override int GetHashCode()
        {
            if (_hash == 0)
            {
                _hash = _node.DeepHashCode();
                if (_hash == 0) _hash = 1;
            }

            return _hash;
        }

        public override string ToString()
        {
            var writer = new DigestWriter();
            _node.Explain(writer);
            return writer.Digest;
        }

    }

    /// <summary>
    /// Collects a node's explain terms and, on <see cref="Done"/>, renders them into the digest string.
    /// The digest analog of Calcite's per-node digest writer (inputs are referenced by type, not
    /// recursed). <see cref="Items"/> also backs the term-by-term <see cref="DeepEquals"/> comparison.
    /// </summary>
    sealed class DigestWriter : INodeWriter
    {

        public List<(string Name, object? Value)> Items { get; } = new List<(string, object?)>();

        /// <summary>
        /// The rendered digest string, available after <see cref="Done"/>.
        /// </summary>
        public string Digest { get; private set; } = "";

        public INodeWriter Item(string name, object? value)
        {
            Items.Add((name, value));
            return this;
        }

        public INodeWriter Done(INode node)
        {
            var sb = new StringBuilder();
            sb.Append(node.GetType().Name).Append('.').Append(node.Traits).Append('(');
            for (int i = 0; i < Items.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(Items[i].Name).Append('=');
                sb.Append(Items[i].Value is INode input ? input.GetType().Name : Items[i].Value);
            }

            sb.Append(')');
            Digest = sb.ToString();
            return this;
        }

    }

}
