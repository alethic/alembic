using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Image;

/// <summary>
/// Blurs its input image.
/// </summary>
sealed class Blur : ImageOp
{

    public Blur(OpTraitSet traits, IOp input)
        : base(traits, input)
    {

    }

    public override IOp Copy(OpTraitSet traits, ImmutableArray<IOp> children)
    {
        return new Blur(traits, children[0]);
    }

}
