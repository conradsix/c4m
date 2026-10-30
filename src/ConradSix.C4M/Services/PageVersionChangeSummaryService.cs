namespace ConradSix.C4M.Services;

#pragma warning disable SA1204 // Static summary helpers are kept after the service operations.

using System.Text.Json;
using System.Text.RegularExpressions;

using ConradSix.C4M;
using ConradSix.C4M.Data;
using ConradSix.C4M.Data.Model;
using ConradSix.C4M.Services.Model;

using Microsoft.EntityFrameworkCore;

internal sealed partial class PageVersionChangeSummaryService(CmsEngineDbContext ctx, IBlockChangeDescriber describer)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<string> CreateForNewVersionAsync(
        Guid pageId,
        PageVersionMetadata metadata,
        IReadOnlyList<PageVersionBlockEntity> versionBlocks,
        CancellationToken cancellationToken)
    {
        var previousEntity = await ctx.PageVersions
            .AsNoTracking()
            .Where(version => version.PageId == pageId)
            .Include(version => version.VersionBlocks)
                .ThenInclude(versionBlock => versionBlock.PageBlock)
                    .ThenInclude(block => block.BlockType)
            .OrderByDescending(version => version.CreatedAt)
            .ThenByDescending(version => version.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var previous = previousEntity is null ? null : ToSnapshot(previousEntity);
        var current = await this.ToSnapshotAsync(metadata, versionBlocks, cancellationToken);
        return Serialize(Summarize(previous, current, describer));
    }

    public static PageVersionChangeSummary Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return PageVersionChangeSummary.Unavailable;
        }

        try
        {
            return JsonSerializer.Deserialize<PageVersionChangeSummary>(json, JsonOptions)
                ?? PageVersionChangeSummary.Unavailable;
        }
        catch (JsonException)
        {
            return PageVersionChangeSummary.Unavailable;
        }
    }

    internal static PageVersionChangeSummary Summarize(
        PageVersionSummarySnapshot? previous,
        PageVersionSummarySnapshot current,
        IBlockChangeDescriber describer)
    {
        if (previous is null)
        {
            return new(
                PageVersionChangeSummary.CurrentSchemaVersion,
                "Initial page version",
                [.. current.Blocks
                    .OrderBy(block => block.SortOrder)
                    .Select(block => $"Added {DescribeBlock(block, describer)}"),
                ]);
        }

        var changes = new List<string>();
        AddMetadataChanges(previous.Metadata, current.Metadata, changes);
        var metadataChanged = changes.Count > 0;

        var previousById = previous.Blocks
            .Where(block => block.Id != Guid.Empty)
            .ToDictionary(block => block.Id);
        var currentById = current.Blocks
            .Where(block => block.Id != Guid.Empty)
            .ToDictionary(block => block.Id);
        var pairs = new List<BlockPair>();

        foreach (var block in current.Blocks)
        {
            if (block.Id != Guid.Empty && previousById.TryGetValue(block.Id, out var oldBlock))
            {
                pairs.Add(new(oldBlock, block, pairs.Count));
            }
        }

        var previousRemaining = previous.Blocks
            .Where(block => block.Id == Guid.Empty || !currentById.ContainsKey(block.Id))
            .ToList();
        var currentRemaining = current.Blocks
            .Where(block => block.Id == Guid.Empty || !previousById.ContainsKey(block.Id))
            .ToList();

        var unmatchedTypeGroups = previousRemaining
            .Select(TypeKey)
            .Concat(currentRemaining.Select(TypeKey))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        foreach (var typeGroup in unmatchedTypeGroups)
        {
            var oldBlocks = previousRemaining
                .Where(block => TypeKey(block) == typeGroup)
                .OrderBy(block => block.SortOrder)
                .ToArray();
            var newBlocks = currentRemaining
                .Where(block => TypeKey(block) == typeGroup)
                .OrderBy(block => block.SortOrder)
                .ToArray();
            var pairCount = Math.Min(oldBlocks.Length, newBlocks.Length);
            for (var index = 0; index < pairCount; index++)
            {
                pairs.Add(new(oldBlocks[index], newBlocks[index], pairs.Count));
                previousRemaining.Remove(oldBlocks[index]);
                currentRemaining.Remove(newBlocks[index]);
            }
        }

        foreach (var block in currentRemaining.OrderBy(block => block.SortOrder))
        {
            changes.Add($"Added {DescribeBlock(block, describer)}");
        }

        foreach (var block in previousRemaining.OrderBy(block => block.SortOrder))
        {
            changes.Add($"Removed {DescribeBlock(block, describer)}");
        }

        foreach (var pair in pairs.Where(pair =>
            pair.Previous.Id != pair.Current.Id || pair.Previous.BlockData != pair.Current.BlockData))
        {
            changes.Add(DescribeUpdatedBlock(pair.Previous, pair.Current, describer));
        }

        var previousOrder = pairs.OrderBy(pair => pair.Previous.SortOrder).Select(pair => pair.Key);
        var currentOrder = pairs.OrderBy(pair => pair.Current.SortOrder).Select(pair => pair.Key);
        if (pairs.Count > 1 && !previousOrder.SequenceEqual(currentOrder))
        {
            var moved = pairs
                .Where(pair => pair.Previous.SortOrder != pair.Current.SortOrder)
                .Select(pair => DescribeBlock(pair.Current, describer))
                .Distinct(StringComparer.Ordinal)
                .Take(3)
                .ToArray();
            changes.Add(moved.Length == 0
                ? "Reordered page sections"
                : $"Reordered page sections including {string.Join(", ", moved)}");
        }

        var blockChanged = changes.Count > (metadataChanged ? MetadataChangeCount(previous.Metadata, current.Metadata) : 0);
        var headline = changes.Count == 0
            ? "No versioned content changes"
            : metadataChanged && blockChanged
                ? "Updated page content and search settings"
                : metadataChanged
                    ? "Updated search and social settings"
                    : changes.Count == 1 && changes[0].StartsWith("Updated ", StringComparison.Ordinal)
                        ? changes[0]
                        : "Changed page content";

        return new(PageVersionChangeSummary.CurrentSchemaVersion, headline, changes);
    }

    private async Task<PageVersionSummarySnapshot> ToSnapshotAsync(
        PageVersionMetadata metadata,
        IReadOnlyList<PageVersionBlockEntity> versionBlocks,
        CancellationToken cancellationToken)
    {
        var typeIds = versionBlocks.Select(item => item.PageBlock.BlockTypeId).Distinct().ToArray();
        var blockTypes = await ctx.BlockTypes
            .Where(type => typeIds.Contains(type.Id))
            .ToDictionaryAsync(type => type.Id, cancellationToken);
        var blocks = versionBlocks.Select(item =>
        {
            var block = item.PageBlock;
            var type = blockTypes[block.BlockTypeId];
            return new PageVersionSummaryBlock(
                block.Id,
                type.Name,
                type.Version,
                item.SortOrder,
                block.BlockData);
        }).ToArray();
        return new(metadata, blocks);
    }

    private static PageVersionSummarySnapshot ToSnapshot(PageVersionEntity version) => new(
        new(
            version.SeoTitle,
            version.SeoDescription,
            version.SeoImageId,
            version.RobotsIndex,
            version.RobotsFollow),
        [.. version.VersionBlocks
            .OrderBy(item => item.SortOrder)
            .Select(item => new PageVersionSummaryBlock(
                item.PageBlockId,
                item.PageBlock.BlockType.Name,
                item.PageBlock.BlockType.Version,
                item.SortOrder,
                item.PageBlock.BlockData)),
        ]);

    private static string Serialize(PageVersionChangeSummary summary) =>
        JsonSerializer.Serialize(summary, JsonOptions);

    private static void AddMetadataChanges(
        PageVersionMetadata previous,
        PageVersionMetadata current,
        List<string> changes)
    {
        if (previous.SeoTitle != current.SeoTitle)
        {
            changes.Add("SEO title updated");
        }

        if (previous.SeoDescription != current.SeoDescription)
        {
            changes.Add("SEO description updated");
        }

        if (previous.SeoImageId != current.SeoImageId)
        {
            changes.Add("Social image changed");
        }

        if (previous.RobotsIndex != current.RobotsIndex)
        {
            changes.Add(current.RobotsIndex ? "Search indexing enabled" : "Search indexing disabled");
        }

        if (previous.RobotsFollow != current.RobotsFollow)
        {
            changes.Add(current.RobotsFollow ? "Link following enabled" : "Link following disabled");
        }
    }

    private static int MetadataChangeCount(PageVersionMetadata previous, PageVersionMetadata current) =>
        Convert.ToInt32(previous.SeoTitle != current.SeoTitle) +
        Convert.ToInt32(previous.SeoDescription != current.SeoDescription) +
        Convert.ToInt32(previous.SeoImageId != current.SeoImageId) +
        Convert.ToInt32(previous.RobotsIndex != current.RobotsIndex) +
        Convert.ToInt32(previous.RobotsFollow != current.RobotsFollow);

    private static string DescribeUpdatedBlock(
        PageVersionSummaryBlock previous,
        PageVersionSummaryBlock current,
        IBlockChangeDescriber describer)
    {
        var fields = ChangedFields(previous, current, describer);
        return fields.Count == 0
            ? $"Updated {DescribeBlock(current, describer)}"
            : $"Updated {string.Join(", ", fields)} in {DescribeBlock(current, describer)}";
    }

    private static IReadOnlyList<string> ChangedFields(
        PageVersionSummaryBlock previous,
        PageVersionSummaryBlock current,
        IBlockChangeDescriber describer)
    {
        try
        {
            using var previousDocument = JsonDocument.Parse(previous.BlockData);
            using var currentDocument = JsonDocument.Parse(current.BlockData);
            return describer.ChangedFields(current.TypeName, previousDocument.RootElement, currentDocument.RootElement);
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string DescribeBlock(PageVersionSummaryBlock block, IBlockChangeDescriber describer)
    {
        var type = describer.DisplayName(block.TypeName);
        var label = GetBlockLabel(block, describer);
        return string.IsNullOrWhiteSpace(label) ? type : $"{type} “{label}”";
    }

    private static string GetBlockLabel(PageVersionSummaryBlock block, IBlockChangeDescriber describer)
    {
        try
        {
            using var document = JsonDocument.Parse(block.BlockData);
            var label = describer.Label(block.TypeName, document.RootElement);
            return Truncate(NormalizeText(label ?? string.Empty), 80);
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static string TypeKey(PageVersionSummaryBlock block) =>
        $"{block.TypeName}:{block.TypeVersion}";

    private static string NormalizeText(string value) =>
        WhitespaceRegex().Replace(MarkdownRegex().Replace(value, " "), " ").Trim();

    private static string Truncate(string value, int maximumLength) => value.Length <= maximumLength
        ? value
        : $"{value[..(maximumLength - 1)].TrimEnd()}…";

    [GeneratedRegex(@"[#*_>`~\[\](){}]+", RegexOptions.CultureInvariant)]
    private static partial Regex MarkdownRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    private sealed record BlockPair(
        PageVersionSummaryBlock Previous,
        PageVersionSummaryBlock Current,
        int Key);
}
