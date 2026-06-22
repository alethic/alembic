using System.Collections;
using System.Collections.Generic;

using Alembic.Algebra;

namespace Alembic.Plan.Hep;

/// <summary>
/// Iterates the vertices of a <see cref="HepNodeVertex"/> graph in depth-first order, following each
/// vertex's current node's inputs (which are themselves vertices). Used in large-plan mode, where it
/// can resume from a new vertex (<see cref="ContinueFrom"/>) instead of restarting from the root.
/// </summary>
sealed class HepVertexIterator : IEnumerator<HepNodeVertex>
{

    readonly Stack<HepNodeVertex> _deque = new Stack<HepNodeVertex>();
    readonly HashSet<HepNodeVertex> _visited;
    HepNodeVertex _current = default!;

    internal HepVertexIterator(HepNodeVertex root, HashSet<HepNodeVertex> visited)
    {
        _visited = visited;
        _deque.Push(root);
    }

    /// <summary>
    /// Iterates from <paramref name="root"/>, excluding (and adding to) the vertices in
    /// <paramref name="visited"/>.
    /// </summary>
    public static IEnumerable<HepNodeVertex> Of(HepNodeVertex root, HashSet<HepNodeVertex> visited)
    {
        var iterator = new HepVertexIterator(root, visited);
        while (iterator.MoveNext())
            yield return iterator.Current;
    }

    /// <summary>
    /// Resumes iteration from <paramref name="newVertex"/>, keeping the visited set.
    /// </summary>
    public HepVertexIterator ContinueFrom(HepNodeVertex newVertex)
    {
        _deque.Push(newVertex);
        return this;
    }

    public HepNodeVertex Current => _current;

    object IEnumerator.Current => Current;

    public bool MoveNext()
    {
        if (_deque.Count == 0)
            return false;

        _current = _deque.Pop();

        var current = _current.CurrentNode;
        if (current is SingleNode single)
        {
            var target = (HepNodeVertex)single.Child;
            if (_visited.Add(target))
                _deque.Push(target);
        }
        else
        {
            foreach (var input in current.Children)
            {
                var target = (HepNodeVertex)input;
                if (_visited.Add(target))
                    _deque.Push(target);
            }
        }

        return true;
    }

    public void Reset() => throw new System.NotSupportedException();

    public void Dispose()
    {
    }

}
