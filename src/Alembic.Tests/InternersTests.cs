using Alembic.Util;

using Xunit;

namespace Alembic.Tests;

/// <summary>
/// Exercises the <see cref="Interners"/> port: an interner answers equal-but-distinct samples with one
/// canonical reference, and a weak interner lets a canonical instance be collected once unreferenced.
/// </summary>
public class InternersTests
{

    /// <summary>A reference type whose equality is by value, so distinct instances can be equivalent.</summary>
    sealed class Box
    {

        public Box(int value) => Value = value;

        public int Value { get; }

        public override bool Equals(object? obj) => obj is Box other && other.Value == Value;

        public override int GetHashCode() => Value;

    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Intern_answers_equal_samples_with_one_canonical_reference(bool weak)
    {
        var interner = weak ? Interners.NewWeakInterner<Box>() : Interners.NewStrongInterner<Box>();

        var first = new Box(1);
        var equal = new Box(1);
        var other = new Box(2);

        // The first of an equivalence class is its own canonical...
        Assert.Same(first, interner.Intern(first));
        // ...and an equal-but-distinct sample is answered with that same reference.
        Assert.Same(first, interner.Intern(equal));
        // A different equivalence class interns to itself.
        Assert.Same(other, interner.Intern(other));
        // Interning is idempotent.
        Assert.Same(first, interner.Intern(first));
    }

    [Fact]
    public void Weak_interner_releases_a_canonical_once_unreferenced()
    {
        var interner = Interners.NewWeakInterner<Box>();

        // Intern a canonical and keep only a weak handle to it.
        var canonicalRef = InternAndForget(interner, 1);

        // Force collection of the now-unreferenced canonical.
        for (int i = 0; i < 5 && canonicalRef.IsAlive; i++)
        {
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
        }

        Assert.False(canonicalRef.IsAlive);

        // A fresh equal sample now becomes the canonical (the dead entry was swept / replaced).
        var revived = new Box(1);
        Assert.Same(revived, interner.Intern(revived));
    }

    // Interns a Box(value) and returns only a weak reference, so the strong reference is gone on return.
    static System.WeakReference InternAndForget(IInterner<Box> interner, int value)
    {
        var canonical = interner.Intern(new Box(value));
        return new System.WeakReference(canonical);
    }

}
