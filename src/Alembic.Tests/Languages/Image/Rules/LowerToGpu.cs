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

    public override INode? Convert(INode node)
    {
        // A CPU-only operation has no GPU form; the planner reaches its result on the GPU by uploading.
        if (node is IImageOperation op && !op.SupportsGpu)
            return null;

        return node.Copy(node.Traits.Replace(ConventionTraitDef.Instance, ImageConventions.Gpu), node.Children);
    }

}
