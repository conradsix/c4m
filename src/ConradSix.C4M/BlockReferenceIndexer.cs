#pragma warning disable SA1402 // The indexed reference is kept with the indexer that produces it.
namespace ConradSix.C4M;

using System.Text.Json;

// Runs every registered extractor over a block in one walk: array items in order, object properties by name. Stored
// block data is JSONB, which doesn't keep property order, so the walk can't follow it: a block indexed when it is saved
// and again when it is reindexed must get the same ordinals. Each reference is reported once per path, with the ordinal
// of its first appearance.
internal sealed class BlockReferenceIndexer(IEnumerable<IReferenceExtractor> extractors)
{
    private readonly IReferenceExtractor[] _extractors = [.. extractors];

    public IReadOnlyList<IndexedReference> Index(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var found = new List<ExtractedReference>();
            this.Walk(new ReferenceNode(document.RootElement, "$", null), found);
            return [.. found
                .Select((reference, ordinal) => new IndexedReference(reference.Kind, reference.TargetId, reference.JsonPath, ordinal))
                .DistinctBy(reference => (reference.Kind, reference.TargetId, reference.JsonPath)),];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private void Walk(ReferenceNode node, List<ExtractedReference> found)
    {
        foreach (var extractor in this._extractors)
        {
            found.AddRange(extractor.Extract(node));
        }

        switch (node.Element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in node.Element.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    this.Walk(new ReferenceNode(property.Value, $"{node.JsonPath}.{property.Name}", property.Name), found);
                }

                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in node.Element.EnumerateArray())
                {
                    this.Walk(new ReferenceNode(item, $"{node.JsonPath}[{index++}]", null), found);
                }

                break;
        }
    }
}

internal sealed record IndexedReference(
    string Kind,
    Guid TargetId,
    string JsonPath,
    int Ordinal);
