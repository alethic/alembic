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

    public override bool IsGuaranteed => true;

    public override IOpNode? Convert(IOpNode op)
    {
        if (op is Download)
            return null;

        return new Upload(op.Traits.Replace(ConventionTraitDef.Instance, ImageConventions.Gpu), op);
    }

}
