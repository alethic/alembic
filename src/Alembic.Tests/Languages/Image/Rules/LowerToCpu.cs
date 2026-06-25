using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;

using Alembic.Algebra.Convert;

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
        // Each input must also be in (lowered to) the CPU convention before this op can consume it.
        var lowered = ImmutableArray.CreateBuilder<IOp>(op.Inputs.Length);
        foreach (var child in op.Inputs)
            lowered.Add(Convert(child, ImageConventions.Cpu));

        return op.Copy(op.Traits.Replace(ConventionTraitDef.Instance, ImageConventions.Cpu), lowered.MoveToImmutable());
    }

}
