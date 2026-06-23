using Alembic.Algebra;
using Alembic.Plan;
using Alembic.Plan.Rules;

namespace Alembic.Tests.Languages.Image.Rules;

/// <summary>
/// Lowers any logical operation to its CPU form by re-stamping its convention.
/// </summary>
sealed class LowerToCpu : ConverterRule
{

    public LowerToCpu()
        : base(ImageConventions.Logical, ImageConventions.Cpu)
    {

    }

    public override bool IsGuaranteed => true;

    public override IOp? Convert(IOp op)
    {
        return op.Copy(op.Traits.Replace(ConventionTraitDef.Instance, ImageConventions.Cpu), op.Children);
    }

}
