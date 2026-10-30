namespace ConradSix.C4M;

using System.Text.Json;
using System.Text.RegularExpressions;

// References written as {"__typename": "<kind>", "id": "<guid>"} objects, and page-ref:<guid> links in markdown.
public sealed partial class TypedReferenceExtractor : IReferenceExtractor
{
    public IEnumerable<ExtractedReference> Extract(ReferenceNode node) => node.Element.ValueKind switch
    {
        JsonValueKind.Object => TryParse(node.Element, out var kind, out var id)
            ? [new ExtractedReference(kind, id, node.JsonPath)]
            : [],
        JsonValueKind.String => MarkdownPageRefs(node.Element.GetString(), node.JsonPath),
        _ => [],
    };

    private static IEnumerable<ExtractedReference> MarkdownPageRefs(string? value, string jsonPath)
    {
        if (string.IsNullOrEmpty(value))
        {
            yield break;
        }

        HashSet<Guid> pageIds = [];
        foreach (Match match in MarkdownPageRefRegex().Matches(value))
        {
            if (Guid.TryParseExact(match.Groups["id"].Value, "D", out var pageId) && pageIds.Add(pageId))
            {
                yield return new ExtractedReference(CmsReferenceKinds.Page, pageId, jsonPath);
            }
        }
    }

    private static bool TryParse(JsonElement element, out string kind, out Guid id)
    {
        kind = string.Empty;
        id = Guid.Empty;
        if (element.GetPropertyCount() != 2 ||
            !element.TryGetProperty("__typename", out var typeElement) || typeElement.ValueKind != JsonValueKind.String ||
            !element.TryGetProperty("id", out var idElement) || idElement.ValueKind != JsonValueKind.String ||
            !idElement.TryGetGuid(out id))
        {
            return false;
        }

        kind = typeElement.GetString()!;
        return kind.Length > 0;
    }

    [GeneratedRegex(
        @"page-ref:(?<id>[0-9a-f]{8}-(?:[0-9a-f]{4}-){3}[0-9a-f]{12})(?![0-9a-f-])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MarkdownPageRefRegex();
}
