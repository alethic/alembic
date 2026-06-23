using System.Collections.Immutable;

using Alembic.Algebra;
using Alembic.Plan;

namespace Alembic.Tests.Languages.Image;

/// <summary>
/// Fills a region of the image from its surroundings. Inpainting has no GPU kernel, so it is CPU-only:
/// a GPU pipeline must download before it and upload after, which is what produces a plan that crosses
/// to the CPU mid-stream and back to the GPU.
/// </summary>
sealed class Inpaint : ImageOp
{

    public Inpaint(OpTraitSet traits, IOp input)
        : base(traits, input)
    {

    }

    public override bool SupportsGpu => false;

    public override IOp Copy(OpTraitSet traits, ImmutableArray<IOp> children)
    {
        return new Inpaint(traits, children[0]);
    }

}
