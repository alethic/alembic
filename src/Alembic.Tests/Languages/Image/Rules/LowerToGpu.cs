using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;

using Alembic.Algebra.Convert;

namespace Alembic.Tests.Languages.Image.Rules;

/// <summary>
/// Lowers any logical operation to its GPU form by re-stamping its convention.
/// </summary>
sealed class LowerToGpu : ConverterRule
{

    public LowerToGpu()
        : base(ImageConventions.Logical, ImageConventions.Gpu)
    {

    }

    public override bool IsGuaranteed => true;

    public override IOp? Convert(IOp op)
    {
        // A CPU-only operation has no GPU form; the planner reaches its result on the GPU by uploading.
        if (op is IImageOperation image && !image.SupportsGpu)
            return null;

        // Each input must also be in (lowered to) the GPU convention before this op can consume it.
        var lowered = ImmutableArray.CreateBuilder<IOp>(op.Children.Length);
        foreach (var child in op.Children)
            lowered.Add(Convert(child, ImageConventions.Gpu));

        return op.Copy(op.Traits.Replace(ConventionTraitDef.Instance, ImageConventions.Gpu), lowered.MoveToImmutable());
    }

}
