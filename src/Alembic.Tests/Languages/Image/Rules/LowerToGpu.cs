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
        return node.Copy(node.Traits.Replace(ConventionTraitDef.Instance, ImageConventions.Gpu), node.Children);
    }

}
