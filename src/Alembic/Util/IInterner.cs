namespace Alembic.Util;

/// <summary>
/// Provides equivalent, canonical instances for objects of type <typeparamref name="T"/>: two arguments
/// that are equal (by the interner's equivalence) are guaranteed to be answered with the one same
/// reference, so callers can subsequently compare interned instances by reference.
/// </summary>
/// <typeparam name="T">The interned reference type.</typeparam>
[Provenance(ProvenanceSource.Other, "com.google.common.collect.Interner")]
public interface IInterner<T>
    where T : class
{

    /// <summary>
    /// Returns the canonical instance equal to <paramref name="sample"/>: the one already interned, or
    /// <paramref name="sample"/> itself if it is the first of its equivalence class. Equal samples always
    /// yield the same returned reference.
    /// </summary>
    [Provenance(ProvenanceSource.Other, "com.google.common.collect.Interner", "intern(E)")]
    T Intern(T sample);

}
