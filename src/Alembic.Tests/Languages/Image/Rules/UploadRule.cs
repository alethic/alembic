using Alembic.Algebra;
using Alembic.Plan;
using Alembic.Plan.Rules;

namespace Alembic.Tests.Languages.Image.Rules;

/// <summary>
/// Bridges a CPU result to the GPU by wrapping it in an <see cref="Upload"/>. It declines an
/// already-downloaded input so the planner never stacks an upload directly on a download.
/// </summary>
sealed class UploadRule : ConverterRule
{

    public UploadRule()
        : base(ImageConventions.Cpu, ImageConventions.Gpu)
    {

    }

    public override INode? Convert(INode node)
    {
        if (node is Download)
            return null;

        return new Upload(node.Traits.Replace(ConventionTraitDef.Instance, ImageConventions.Gpu), node);
    }

}
