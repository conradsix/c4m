#pragma warning disable SA1402 // The candidate contracts are kept with their selector.
namespace ConradSix.C4M;

// Picks the image shown when another page links to this one.
public interface ILinkedPageImageSelector
{
    Guid? SelectImage(IReadOnlyList<LinkedPageBlock> blocks);
}

public sealed record LinkedPageBlock(
    string BlockType,
    IReadOnlyList<LinkedPageImageReference> Images);

public sealed record LinkedPageImageReference(
    string JsonPath,
    Guid ImageId);

public sealed class NullLinkedPageImageSelector : ILinkedPageImageSelector
{
    public Guid? SelectImage(IReadOnlyList<LinkedPageBlock> blocks) => null;
}
