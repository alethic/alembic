using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Image;

/// <summary>
/// Thresholds its input image to a binary mask.
/// </summary>
sealed class Threshold : ImageOp
{

    public Threshold(TraitSet traits, INode input)
        : base(traits, input)
    {

    }

    public override INode Copy(TraitSet traits, ImmutableArray<INode> children)
    {
        return new Threshold(traits, children[0]);
    }

}
