using Alembic.Algebra;
using Alembic.Plan;
using Alembic.Plan.Rules;

namespace Alembic.Tests.Languages.Image.Rules;

/// <summary>
/// Bridges a GPU result to the CPU by wrapping it in a <see cref="Download"/>. It declines an
/// already-uploaded input so the planner never stacks a download directly on an upload.
/// </summary>
sealed class DownloadRule : ConverterRule
{

    public DownloadRule()
        : base(ImageConventions.Gpu, ImageConventions.Cpu)
    {

    }

    public override INode? Convert(INode node)
    {
        if (node is Upload)
            return null;

        return new Download(node.Traits.Replace(ConventionTraitDef.Instance, ImageConventions.Cpu), node);
    }

}
