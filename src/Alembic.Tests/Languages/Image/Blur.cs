using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Image;

/// <summary>
/// Blurs its input image.
/// </summary>
sealed class Blur : ImageOp
{

    public Blur(TraitSet traits, INode input)
        : base(traits, input)
    {

    }

    public override INode Copy(TraitSet traits, ImmutableArray<INode> children)
    {
        return new Blur(traits, children[0]);
    }

}
