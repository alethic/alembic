using System;
using System.Collections.Generic;

namespace Alembic.Util;

/// <summary>
/// An immutable pair of values. Because it implements <see cref="Equals(object)"/> and
/// <see cref="GetHashCode"/> in terms of its members, a pair can be used as a key in a hash table.
/// </summary>
/// <typeparam name="T1">The left value's type.</typeparam>
/// <typeparam name="T2">The right value's type.</typeparam>
public class Pair<T1, T2> : IEquatable<Pair<T1, T2>>
{

    /// <summary>
    /// Creates a pair.
    /// </summary>
    public Pair(T1 left, T2 right)
    {
        Left = left;
        Right = right;
    }

    /// <summary>
    /// The left value.
    /// </summary>
    public T1 Left { get; }

    /// <summary>
    /// The right value.
    /// </summary>
    public T2 Right { get; }

    /// <inheritdoc />
    public bool Equals(Pair<T1, T2>? other)
    {
        return other is not null
            && EqualityComparer<T1>.Default.Equals(Left, other.Left)
            && EqualityComparer<T2>.Default.Equals(Right, other.Right);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as Pair<T1, T2>);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        int leftHash = Left is null ? 0 : Left.GetHashCode();
        int rightHash = Right is null ? 0 : Right.GetHashCode();
        return leftHash ^ rightHash;
    }

    /// <inheritdoc />
    public override string ToString() => "<" + Left + ", " + Right + ">";

}

/// <summary>
/// Factory for <see cref="Pair{T1, T2}"/> that lets the member types be inferred, so callers can write
/// <c>Pair.Of(a, b)</c> instead of naming both type arguments.
/// </summary>
public static class Pair
{

    /// <summary>
    /// Creates a pair, inferring the member types.
    /// </summary>
    public static Pair<T1, T2> Of<T1, T2>(T1 left, T2 right) => new Pair<T1, T2>(left, right);

}
