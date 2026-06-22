using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

using Alembic.Plan;

namespace Alembic.Algebra;

/// <summary>
/// Convenience base for <see cref="INode"/> implementations. A node lists its identity-bearing terms
/// in <see cref="Explain"/> — its own attributes and its inputs — and the base derives
/// <see cref="DeepEquals"/> / <see cref="DeepHashCode"/> from them. Each node keeps one
/// <see cref="INodeDigest"/> that caches its hash.
/// </summary>
public abstract class AbstractNode : INode
{

    readonly InnerNodeDigest _digest;

    /// <summary>
    /// Initializes the node with its traits and children.
    /// </summary>
    protected AbstractNode(TraitSet traits, ImmutableArray<INode> children)
    {
        Traits = traits;
        Children = children;
        _digest = new InnerNodeDigest(this);
    }

    /// <inheritdoc />
    public TraitSet Traits { get; }

    /// <inheritdoc />
    public ImmutableArray<INode> Children { get; }

    /// <summary>
    /// Lists this node's identity-bearing terms. A subclass calls <c>base.Explain</c>, then adds its
    /// own attributes (<see cref="INodeWriter.Item"/>) and its inputs (<see cref="INodeWriter.Input"/>).
    /// Two nodes of the same type with equal traits and equal terms are structurally equivalent; a node
    /// that omits a term excludes it from that comparison, so inputs must be listed here.
    /// </summary>
    protected virtual void Explain(INodeWriter writer)
    {

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
    /// Renders this node and its inputs as an indented plan tree — each line is a node's type, traits,
    /// and attributes, with inputs nested beneath. For display and debugging.
    /// </summary>
    internal string RenderPlan()
    {
        var sb = new StringBuilder();
        Render(this, null, sb, 0);
        return sb.ToString().TrimEnd();
    }

    static void Render(INode node, string? label, StringBuilder sb, int depth)
    {
        var writer = new RenderWriter();
        if (node is AbstractNode self)
            self.Explain(writer);

        sb.Append(' ', depth * 2);
        if (label is not null)
            sb.Append(label).Append(": ");

        sb.Append(node.GetType().Name).Append(' ').Append(node.Traits);
        if (writer.Items.Count > 0)
        {
            sb.Append(" (");
            for (int i = 0; i < writer.Items.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(writer.Items[i].Name).Append('=').Append(writer.Items[i].Value);
            }

            sb.Append(')');
        }

        sb.Append('\n');

        foreach (var (name, child) in writer.Inputs)
            Render(child, name, sb, depth + 1);
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
        Explain(writer);
        return writer.Items;
    }

    /// <summary>
    /// The digest kept by each node: it caches its hash and delegates equality to the node's
    /// <see cref="DeepEquals"/>. Because it is nested it can reach the node's <see cref="Explain"/> to
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
            return writer.Done(_node);
        }

    }

    /// <summary>
    /// Collects a node's explain terms. <see cref="Done"/> renders them into the digest string.
    /// </summary>
    sealed class DigestWriter : INodeWriter
    {

        public List<(string Name, object? Value)> Items { get; } = new List<(string, object?)>();

        public INodeWriter Item(string name, object? value)
        {
            Items.Add((name, value));
            return this;
        }

        public INodeWriter Input(string name, INode input)
        {
            Items.Add((name, input));
            return this;
        }

        public string Done(INode node)
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
            return sb.ToString();
        }

    }

    /// <summary>
    /// Collects a node's attributes and inputs for the indented plan rendering.
    /// </summary>
    sealed class RenderWriter : INodeWriter
    {

        public List<(string Name, object? Value)> Items { get; } = new List<(string, object?)>();

        public List<(string Name, INode Child)> Inputs { get; } = new List<(string, INode)>();

        public INodeWriter Item(string name, object? value)
        {
            Items.Add((name, value));
            return this;
        }

        public INodeWriter Input(string name, INode input)
        {
            Inputs.Add((name, input));
            return this;
        }

    }

}
