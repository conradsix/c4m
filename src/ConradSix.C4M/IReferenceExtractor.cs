#pragma warning disable SA1402 // The node and reference contracts are kept with their extractor.
namespace Parapet.CMS.Engine;

using System.Text.Json;

// Finds references in block data. The engine walks each block once, array items in order and object properties by
// name, and offers every value to every extractor; an extractor reports only what it sees at that node, not beneath it.
public interface IReferenceExtractor
{
    IEnumerable<ExtractedReference> Extract(ReferenceNode node);
}

// PropertyName is the name the value sits under, or null for the root and array items.
public readonly record struct ReferenceNode(
    JsonElement Element,
    string JsonPath,
    string? PropertyName);

public sealed record ExtractedReference(
    string Kind,
    Guid TargetId,
    string JsonPath);

public static class CmsReferenceKinds
{
    public const string Page = "page-ref";

    public const string Image = "image-ref";
}
