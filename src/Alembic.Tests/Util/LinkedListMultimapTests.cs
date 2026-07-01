using System.Collections.Generic;
using System.Linq;

using Alembic.Util;

using Xunit;

namespace Alembic.Tests.Util;

/// <summary>
/// Locks in the Guava-like semantics of <see cref="LinkedListMultimap{TKey, TValue}.Get"/>: a live,
/// mutate-through view over a key's values. The planner only reads the view, so these mutate-through
/// paths are otherwise unexercised.
/// </summary>
public class LinkedListMultimapTests
{

    static LinkedListMultimap<string, int> Build(params (string Key, int Value)[] entries)
    {
        var map = new LinkedListMultimap<string, int>();
        foreach (var (key, value) in entries)
            map.Put(key, value);

        return map;
    }

    [Fact]
    public void Get_returns_values_in_per_key_insertion_order()
    {
        var map = Build(("a", 1), ("b", 2), ("a", 3));

        Assert.Equal(new[] { 1, 3 }, map.Get("a"));
        Assert.Equal(new[] { 2 }, map.Get("b"));
    }

    [Fact]
    public void Get_for_absent_key_is_empty()
    {
        var map = Build(("a", 1));
        var view = map.Get("missing");

        Assert.Empty(view);
        Assert.Equal(0, view.Count);
    }

    [Fact]
    public void Get_view_is_live_reflecting_later_puts()
    {
        var map = Build(("a", 1));
        var view = map.Get("a");

        map.Put("a", 2);

        Assert.Equal(new[] { 1, 2 }, view);
    }

    [Fact]
    public void Add_through_view_appends_to_the_multimap()
    {
        var map = Build(("a", 1));
        map.Get("a").Add(2);

        Assert.Equal(new[] { 1, 2 }, map.Get("a"));
    }

    [Fact]
    public void Add_through_view_of_absent_key_creates_the_key()
    {
        var map = new LinkedListMultimap<string, int>();
        map.Get("a").Add(7);

        Assert.Equal(new[] { 7 }, map.Get("a"));
    }

    [Fact]
    public void Insert_through_view_splices_at_the_position()
    {
        var map = Build(("a", 1), ("a", 3));
        map.Get("a").Insert(1, 2);

        Assert.Equal(new[] { 1, 2, 3 }, map.Get("a"));
    }

    [Fact]
    public void Set_through_view_replaces_the_value()
    {
        var map = Build(("a", 1), ("a", 2));
        map.Get("a")[0] = 9;

        Assert.Equal(new[] { 9, 2 }, map.Get("a"));
    }

    [Fact]
    public void RemoveAt_and_Remove_through_view_drop_values()
    {
        var map = Build(("a", 1), ("a", 2), ("a", 3));
        var view = map.Get("a");

        view.RemoveAt(1);
        Assert.Equal(new[] { 1, 3 }, map.Get("a"));

        Assert.True(view.Remove(1));
        Assert.False(view.Remove(42));
        Assert.Equal(new[] { 3 }, map.Get("a"));
    }

    [Fact]
    public void Removing_the_last_value_makes_the_key_absent_again()
    {
        var map = Build(("a", 1));
        map.Get("a").RemoveAt(0);

        Assert.Empty(map.Get("a"));
    }

    [Fact]
    public void Two_views_of_the_same_key_observe_each_others_mutations()
    {
        var map = Build(("a", 1));
        var first = map.Get("a");
        var second = map.Get("a");

        first.Add(2);

        Assert.Equal(new[] { 1, 2 }, second);
    }

    [Fact]
    public void Copying_the_view_snapshots_current_values()
    {
        // The FireRules pattern: new List<>(map.Get(key)) is a defensive copy of the live view.
        var map = Build(("a", 1), ("a", 2));
        var snapshot = new List<int>(map.Get("a"));

        map.Put("a", 3);

        Assert.Equal(new[] { 1, 2 }, snapshot);
        Assert.Equal(new[] { 1, 2, 3 }, map.Get("a"));
    }

    [Fact]
    public void RemoveValuesWhere_prunes_matching_values_across_keys()
    {
        var map = Build(("a", 1), ("b", 2), ("a", 3), ("b", 4));
        map.RemoveValuesWhere(v => v % 2 == 0);

        Assert.Equal(new[] { 1, 3 }, map.Get("a"));
        Assert.Empty(map.Get("b"));
    }

}
