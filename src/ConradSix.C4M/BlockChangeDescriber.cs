#pragma warning disable SA1402 // The default describer is kept with its contract.
namespace Parapet.CMS.Engine;

using System.Text.Json;

// Wording for page version change summaries.
public interface IBlockChangeDescriber
{
    string DisplayName(string blockType);

    string? Label(string blockType, JsonElement block);

    IReadOnlyList<string> ChangedFields(string blockType, JsonElement previous, JsonElement current);
}

public class BlockChangeDescriber : IBlockChangeDescriber
{
    public virtual string DisplayName(string blockType)
    {
        var normalized = blockType.Replace('-', ' ').Replace('_', ' ').Trim();
        return string.IsNullOrEmpty(normalized)
            ? "Block"
            : char.ToUpperInvariant(normalized[0]) + normalized[1..];
    }

    public virtual string? Label(string blockType, JsonElement block) =>
        GetString(block, "title") is { } title && !string.IsNullOrWhiteSpace(title)
            ? title
            : GetString(block, "markdown") ?? GetString(block, "content");

    public virtual IReadOnlyList<string> ChangedFields(string blockType, JsonElement previous, JsonElement current) => [];

    protected static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    protected static bool PropertyChanged(JsonElement previous, JsonElement current, string property)
    {
        var hasPrevious = previous.TryGetProperty(property, out var oldValue);
        var hasCurrent = current.TryGetProperty(property, out var newValue);
        return hasPrevious != hasCurrent ||
            (hasPrevious && hasCurrent && !JsonElement.DeepEquals(oldValue, newValue));
    }
}
