namespace ConradSix.C4M.Tests.Services;

using System.Text.Json;

using AwesomeAssertions;

using ConradSix.C4M;
using ConradSix.C4M.Services;
using ConradSix.C4M.Services.Model;

public sealed class PageVersionChangeSummaryServiceTests
{
    private static readonly TestDescriber Describer = new();

    [Fact]
    public void Summarize_ShouldDescribeInitialVersionWithMeaningfulBlockLabels()
    {
        var current = Snapshot(
            Block(Guid.NewGuid(), "hero", 1, 1, /*lang=json,strict*/ "{\"title\":\"Reunion Resort Homes\"}"),
            Block(Guid.NewGuid(), "faq", 1, 2, /*lang=json,strict*/ "{\"items\":[{\"question\":\"Where is Reunion Resort?\"}]}"));

        var summary = PageVersionChangeSummaryService.Summarize(null, current, Describer);

        summary.Headline.Should().Be("Initial page version");
        summary.Changes.Should().Contain("Added Hero “Reunion Resort Homes”");
        summary.Changes.Should().Contain("Added FAQ “Where is Reunion Resort?”");
    }

    [Fact]
    public void Summarize_ShouldDescribeSeoAndKnownBlockFieldChanges()
    {
        var previous = Snapshot(
            Block(Guid.NewGuid(), "content", 2, 1, /*lang=json,strict*/ "{\"title\":\"Ownership\",\"markdown\":\"Old copy\"}"));
        var current = Snapshot(
            Block(Guid.NewGuid(), "content", 2, 1, /*lang=json,strict*/ "{\"title\":\"Ownership\",\"markdown\":\"New copy\"}")) with
        {
            Metadata = previous.Metadata with { SeoTitle = "A new SEO title" },
        };

        var summary = PageVersionChangeSummaryService.Summarize(previous, current, Describer);

        summary.Headline.Should().Be("Updated page content and search settings");
        summary.Changes.Should().Contain("SEO title updated");
        summary.Changes.Should().Contain("Updated body copy in Content “Ownership”");
    }

    [Fact]
    public void Summarize_ShouldDistinguishAddedRemovedAndReorderedBlocks()
    {
        var heroId = Guid.NewGuid();
        var contentId = Guid.NewGuid();
        var removedId = Guid.NewGuid();
        var previous = Snapshot(
            Block(heroId, "hero", 1, 1, /*lang=json,strict*/ "{\"title\":\"Hero\"}"),
            Block(contentId, "content", 2, 2, /*lang=json,strict*/ "{\"title\":\"Content\",\"markdown\":\"Copy\"}"),
            Block(removedId, "faq", 1, 3, /*lang=json,strict*/ "{\"title\":\"Old FAQ\",\"items\":[]}"));
        var current = Snapshot(
            Block(contentId, "content", 2, 1, /*lang=json,strict*/ "{\"title\":\"Content\",\"markdown\":\"Copy\"}"),
            Block(heroId, "hero", 1, 2, /*lang=json,strict*/ "{\"title\":\"Hero\"}"),
            Block(Guid.NewGuid(), "search-results-block", 2, 3, /*lang=json,strict*/ "{\"title\":\"Homes\"}"));

        var summary = PageVersionChangeSummaryService.Summarize(previous, current, Describer);

        summary.Changes.Should().Contain("Added Property results “Homes”");
        summary.Changes.Should().Contain("Removed FAQ “Old FAQ”");
        summary.Changes.Should().Contain(change =>
            change.StartsWith("Reordered page sections", StringComparison.Ordinal));
    }

    [Fact]
    public void Summarize_ShouldFallBackForUnknownOrMalformedBlocks()
    {
        var previous = Snapshot(Block(Guid.NewGuid(), "promo-grid", 1, 1, "not-json"));
        var current = Snapshot(Block(Guid.NewGuid(), "promo-grid", 1, 1, "still-not-json"));

        var summary = PageVersionChangeSummaryService.Summarize(previous, current, Describer);

        summary.Changes.Should().Contain("Updated Promo grid");
    }

    [Fact]
    public void Summarize_ShouldIdentifyVersionsWithoutVersionedChanges()
    {
        var blockId = Guid.NewGuid();
        var previous = Snapshot(Block(blockId, "hero", 1, 1, /*lang=json,strict*/ "{\"title\":\"Hero\"}"));
        var current = Snapshot(Block(blockId, "hero", 1, 1, /*lang=json,strict*/ "{\"title\":\"Hero\"}"));

        var summary = PageVersionChangeSummaryService.Summarize(previous, current, Describer);

        summary.Headline.Should().Be("No versioned content changes");
        summary.Changes.Should().BeEmpty();
    }

    private static PageVersionSummarySnapshot Snapshot(params PageVersionSummaryBlock[] blocks) => new(
        new("SEO title", "SEO description", null, true, true),
        blocks);

    private static PageVersionSummaryBlock Block(
        Guid id,
        string typeName,
        int typeVersion,
        int sortOrder,
        string blockData) => new(id, typeName, typeVersion, sortOrder, blockData);

    // Wording a host gives its own blocks, standing in for a host's describer.
    private sealed class TestDescriber : BlockChangeDescriber
    {
        public override string DisplayName(string blockType) => blockType switch
        {
            "faq" => "FAQ",
            "search-results-block" => "Property results",
            _ => base.DisplayName(blockType),
        };

        public override string? Label(string blockType, JsonElement block) =>
            blockType == "faq" && GetString(block, "title") is null &&
            block.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array && items.GetArrayLength() > 0
                ? GetString(items[0], "question")
                : base.Label(blockType, block);

        public override IReadOnlyList<string> ChangedFields(string blockType, JsonElement previous, JsonElement current) =>
            blockType == "content" && PropertyChanged(previous, current, "markdown") ? ["body copy"] : [];
    }
}
