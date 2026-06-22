using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Image;

/// <summary>
/// Converts its input image to grayscale.
/// </summary>
sealed class Grayscale : ImageOp
{

    public Grayscale(TraitSet traits, INode input)
        : base(traits, input)
    {

    }

    public override INode Copy(TraitSet traits, ImmutableArray<INode> children)
    {
        return new Grayscale(traits, children[0]);
    }

}
