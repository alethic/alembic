using System.Collections.Generic;

namespace Alembic.Util;

/// <summary>
/// An unbounded, comparator-ordered priority queue — the .NET stand-in for <c>java.util.PriorityQueue</c>,
/// backed by a binary min-heap stored in an array. The least element, as ordered by the supplied comparator,
/// is the head of the queue.
/// </summary>
/// <remarks>
/// Unlike the BCL's <c>PriorityQueue&lt;TElement, TPriority&gt;</c>, this exposes a <see cref="Remove"/> that
/// deletes an arbitrary element and restores the heap (the decrease-key idiom of moving the last element into
/// the hole and sifting). Elements are reference types so that <see cref="Poll"/> and <see cref="Peek"/> can
/// return <c>null</c> on an empty queue, matching the Java contract.
/// </remarks>
/// <typeparam name="E">The element type.</typeparam>
[Provenance(ProvenanceSource.Other, "java.util.PriorityQueue")]
public sealed class PriorityQueue<E>
    where E : class
{

    readonly List<E> _heap = new List<E>();
    readonly IComparer<E> _comparator;

    /// <summary>
    /// Creates an empty queue whose elements are ordered by <paramref name="comparator"/>.
    /// </summary>
    [Provenance(ProvenanceSource.Other, "java.util.PriorityQueue", "PriorityQueue(Comparator)")]
    public PriorityQueue(IComparer<E> comparator)
    {
        _comparator = comparator;
    }

    /// <summary>
    /// The number of elements in the queue.
    /// </summary>
    [Provenance(ProvenanceSource.Other, "java.util.PriorityQueue", "size()")]
    public int Count => _heap.Count;

    /// <summary>
    /// Whether the queue holds no elements.
    /// </summary>
    [Provenance(ProvenanceSource.Other, "java.util.AbstractCollection", "isEmpty()")]
    public bool IsEmpty => _heap.Count == 0;

    /// <summary>
    /// Inserts <paramref name="e"/> into the queue, sifting it up to its ordered position.
    /// </summary>
    [Provenance(ProvenanceSource.Other, "java.util.PriorityQueue", "offer(E)")]
    public bool Offer(E e)
    {
        _heap.Add(e);
        SiftUp(_heap.Count - 1, e);
        return true;
    }

    /// <summary>
    /// Inserts <paramref name="e"/> into the queue. Equivalent to <see cref="Offer"/>.
    /// </summary>
    [Provenance(ProvenanceSource.Other, "java.util.PriorityQueue", "add(E)")]
    public bool Add(E e) => Offer(e);

    /// <summary>
    /// Retrieves, but does not remove, the least element, or <c>null</c> if the queue is empty.
    /// </summary>
    [Provenance(ProvenanceSource.Other, "java.util.PriorityQueue", "peek()")]
    public E? Peek()
    {
        return _heap.Count == 0 ? null : _heap[0];
    }

    /// <summary>
    /// Retrieves and removes the least element, or returns <c>null</c> if the queue is empty.
    /// </summary>
    [Provenance(ProvenanceSource.Other, "java.util.PriorityQueue", "poll()")]
    public E? Poll()
    {
        if (_heap.Count == 0)
            return null;

        var result = _heap[0];
        var last = _heap.Count - 1;
        var x = _heap[last];
        _heap.RemoveAt(last);
        if (last != 0)
            SiftDown(0, x);

        return result;
    }

    /// <summary>
    /// Removes a single occurrence of <paramref name="e"/> (located by the element type's default equality),
    /// restoring the heap, and returns whether it was present.
    /// </summary>
    [Provenance(ProvenanceSource.Other, "java.util.PriorityQueue", "remove(Object)")]
    public bool Remove(E e)
    {
        var i = IndexOf(e);
        if (i == -1)
            return false;

        RemoveAt(i);
        return true;
    }

    [Provenance(ProvenanceSource.Other, "java.util.PriorityQueue", "indexOf(Object)")]
    int IndexOf(E e)
    {
        var cmp = EqualityComparer<E>.Default;
        for (var i = 0; i < _heap.Count; i++)
            if (cmp.Equals(e, _heap[i]))
                return i;

        return -1;
    }

    /// <summary>
    /// Removes the element at index <paramref name="i"/>, moving the last element into the hole and sifting it
    /// down (and, if that left it out of order, back up) to restore the heap invariant.
    /// </summary>
    [Provenance(ProvenanceSource.Other, "java.util.PriorityQueue", "removeAt(int)")]
    void RemoveAt(int i)
    {
        var last = _heap.Count - 1;
        if (last == i)
        {
            _heap.RemoveAt(i);
            return;
        }

        var moved = _heap[last];
        _heap.RemoveAt(last);
        SiftDown(i, moved);
        if (ReferenceEquals(_heap[i], moved))
            SiftUp(i, moved);
    }

    /// <summary>
    /// Sifts <paramref name="x"/> up from <paramref name="k"/> until its parent is no greater than it.
    /// </summary>
    [Provenance(ProvenanceSource.Other, "java.util.PriorityQueue", "siftUpUsingComparator(int, E)")]
    void SiftUp(int k, E x)
    {
        while (k > 0)
        {
            var parent = (k - 1) >> 1;
            var e = _heap[parent];
            if (_comparator.Compare(x, e) >= 0)
                break;

            _heap[k] = e;
            k = parent;
        }

        _heap[k] = x;
    }

    /// <summary>
    /// Sifts <paramref name="x"/> down from <paramref name="k"/> until both children are no less than it.
    /// </summary>
    [Provenance(ProvenanceSource.Other, "java.util.PriorityQueue", "siftDownUsingComparator(int, E)")]
    void SiftDown(int k, E x)
    {
        var size = _heap.Count;
        var half = size >> 1;
        while (k < half)
        {
            var child = (k << 1) + 1;
            var c = _heap[child];
            var right = child + 1;
            if (right < size && _comparator.Compare(c, _heap[right]) > 0)
            {
                child = right;
                c = _heap[child];
            }

            if (_comparator.Compare(x, c) <= 0)
                break;

            _heap[k] = c;
            k = child;
        }

        _heap[k] = x;
    }

}
