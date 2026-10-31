namespace ConradSix.C4M.Tests;

using ConradSix.C4M;

// Picks the first image on the page, standing in for a host's selector.
public sealed class FirstImageSelector : ILinkedPageImageSelector
{
    public Guid? SelectImage(IReadOnlyList<LinkedPageBlock> blocks) =>
        blocks.SelectMany(block => block.Images).Select(image => (Guid?)image.ImageId).FirstOrDefault();
}
