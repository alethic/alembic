using Alembic.Algebra;

namespace Alembic.Tests;

/// <summary>
/// A toy <see cref="IOutputType"/> for tests: an output of a given <see cref="Width"/>, with a cosmetic
/// <see cref="Label"/> that equivalence ignores — the stand-in for a field name, mirroring Calcite's
/// <c>equalsSansFieldNames</c>. Two shapes are equivalent iff their widths match.
/// </summary>
sealed class Shape : IOutputType
{

    public Shape(int width, string label = "")
    {
        Width = width;
        Label = label;
    }

    public int Width { get; }

    public string Label { get; }

    public bool IsEquivalentTo(IOutputType other) => other is Shape s && s.Width == Width;

    public override string ToString() => "Shape(" + Width + ":" + Label + ")";

}
