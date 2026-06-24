using System.Collections.Generic;

namespace Alembic.Util;

/// <summary>
/// Miscellaneous utility functions.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.Util")]
public static class Util
{

    /// <summary>
    /// Whether two lists are the same size and have the same elements, in order, compared by reference.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.Util", "equalShallow(List, List)")]
    public static bool EqualShallow<T>(IReadOnlyList<T> list0, IReadOnlyList<T> list1)
        where T : class
    {
        if (list0.Count != list1.Count)
            return false;

        for (int i = 0; i < list0.Count; i++)
            if (!ReferenceEquals(list0[i], list1[i]))
                return false;

        return true;
    }

    /// <summary>
    /// Returns the first value if it is not null, otherwise the second value.
    /// </summary>
    /// <remarks>
    /// The result may be null only if the second argument is not null. Equivalent to the Elvis operator
    /// (<c>?:</c>) of languages such as Groovy or PHP.
    /// </remarks>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.util.Util", "first(T, T)")]
    public static T? First<T>(T? v0, T? v1)
        where T : class
        => v0 is not null ? v0 : v1;

}
