using Alembic.Algebra;
using Alembic.Plan;
using Alembic.Plan.Rules;

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

        return op.Copy(op.Traits.Replace(ConventionTraitDef.Instance, ImageConventions.Gpu), op.Children);
    }

}
