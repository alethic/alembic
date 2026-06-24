using System.Collections;
using System.Collections.Generic;

using Alembic.Algebra;

namespace Alembic.Plan.Hep;

/// <summary>
/// Iterates the vertices of a <see cref="HepOpVertex"/> graph in depth-first order, following each
/// vertex's current op's inputs (which are themselves vertices). Used in large-plan mode, where it
/// can resume from a new vertex (<see cref="ContinueFrom"/>) instead of restarting from the root.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepVertexIterator")]
sealed class HepVertexIterator : IEnumerator<HepOpVertex>
{

    readonly Stack<HepOpVertex> _deque = new Stack<HepOpVertex>();
    readonly HashSet<HepOpVertex> _visited;
    HepOpVertex _current = default!;

    /// <summary>
    /// Creates an iterator from <paramref name="root"/>, sharing the <paramref name="visited"/> set.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepVertexIterator", "HepVertexIterator(V, Set<Integer>)")]
    internal HepVertexIterator(HepOpVertex root, HashSet<HepOpVertex> visited)
    {
        _visited = visited;
        _deque.Push(root);
    }

    /// <summary>
    /// Iterates from <paramref name="root"/>, excluding (and adding to) the vertices in
    /// <paramref name="visited"/>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepVertexIterator", "of(V, Set<Integer>)")]
    public static IEnumerable<HepOpVertex> Of(HepOpVertex root, HashSet<HepOpVertex> visited)
    {
        var iterator = new HepVertexIterator(root, visited);
        while (iterator.MoveNext())
            yield return iterator.Current;
    }

    /// <summary>
    /// Resumes iteration from <paramref name="newVertex"/>, keeping the visited set.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepVertexIterator", "continueFrom(V)")]
    public HepVertexIterator ContinueFrom(HepOpVertex newVertex)
    {
        _deque.Push(newVertex);
        return this;
    }

    /// <inheritdoc/>
    public HepOpVertex Current => _current;

    object IEnumerator.Current => Current;

    /// <inheritdoc/>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepVertexIterator", "next()")]
    public bool MoveNext()
    {
        if (_deque.Count == 0)
            return false;

        _current = _deque.Pop();

        var current = _current.CurrentOp;
        if (current is SingleOp single)
        {
            var target = (HepOpVertex)single.Child;
            if (_visited.Add(target))
                _deque.Push(target);
        }
        else
        {
            foreach (var input in current.Children)
            {
                var target = (HepOpVertex)input;
                if (_visited.Add(target))
                    _deque.Push(target);
            }
        }

        return true;
    }

    /// <inheritdoc/>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepVertexIterator", "remove()")]
    public void Reset() => throw new System.NotSupportedException();

    /// <inheritdoc/>
    public void Dispose()
    {
    }

}
