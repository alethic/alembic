using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Image;

/// <summary>
/// Thresholds its input image to a binary mask.
/// </summary>
sealed class Threshold : ImageOp
{

    public Threshold(OpTraitSet traits, IOpNode input)
        : base(traits, input)
    {

    }

    public override IOpNode Copy(OpTraitSet traits, ImmutableArray<IOpNode> children)
    {
        return new Threshold(traits, children[0]);
    }

}
