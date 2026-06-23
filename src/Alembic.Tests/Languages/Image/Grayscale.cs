using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Image;

/// <summary>
/// Converts its input image to grayscale.
/// </summary>
sealed class Grayscale : ImageOp
{

    public Grayscale(TraitSet traits, IOpNode input)
        : base(traits, input)
    {

    }

    public override IOpNode Copy(TraitSet traits, ImmutableArray<IOpNode> children)
    {
        return new Grayscale(traits, children[0]);
    }

}
