namespace Alembic.Tests.Languages.Image;

/// <summary>
/// An image operation that declares whether it has a GPU implementation. CPU-only operations (file IO,
/// inpainting, and the like) have no GPU kernel, so the planner must download before them and upload
/// after — which is what forces plans that cross between CPU and GPU and back.
/// </summary>
interface IImageOperation
{

    /// <summary>
    /// Whether this operation can run on the GPU.
    /// </summary>
    bool SupportsGpu { get; }

}
