using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Image;

/// <summary>
/// Thresholds its input image to a binary mask.
/// </summary>
sealed class Threshold : ImageOp
{

    public Threshold(OpTraitSet traits, IOp input)
        : base(traits, input)
    {

    }

    public override IOp Copy(OpTraitSet traits, ImmutableArray<IOp> children)
    {
        return new Threshold(traits, children[0]);
    }

}
