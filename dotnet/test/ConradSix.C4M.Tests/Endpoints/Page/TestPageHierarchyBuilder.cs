#pragma warning disable SA1402 // File may only contain a single type
namespace ConradSix.C4M.Tests.Endpoints.Page;

using System.Net.Http;

using ConradSix.C4M.Endpoints.Page;

public class TestPageHierarchyBuilder
{
    private readonly List<HierarchyPageDefinition> _pages = [];
    private Guid _realtorId = Guid.NewGuid();
    private Guid? _defaultBlockTypeId;

    public static TestPageHierarchyBuilder ForRealtor(Guid realtorId) => new TestPageHierarchyBuilder().WithRealtorId(realtorId);

    public TestPageHierarchyBuilder WithRealtorId(Guid realtorId)
    {
        this._realtorId = realtorId;
        return this;
    }

    public TestPageHierarchyBuilder WithDefaultBlockType(Guid blockTypeId)
    {
        this._defaultBlockTypeId = blockTypeId;
        return this;
    }

    public TestPageHierarchyBuilder AddRootPage(string slug, Action<PageDefinitionBuilder>? configure = null)
    {
        var builder = new PageDefinitionBuilder(slug, null, this._defaultBlockTypeId);
        configure?.Invoke(builder);
        this._pages.Add(builder.Build());
        return this;
    }

    public TestPageHierarchyBuilder AddChildPage(string parentSlug, string slug, Action<PageDefinitionBuilder>? configure = null)
    {
        var builder = new PageDefinitionBuilder(slug, parentSlug, this._defaultBlockTypeId);
        configure?.Invoke(builder);
        this._pages.Add(builder.Build());
        return this;
    }

    public TestPageHierarchyBuilder AddPageTree(string rootSlug, params string[] childSlugs)
    {
        this.AddRootPage(rootSlug);
        foreach (var childSlug in childSlugs)
        {
            this.AddChildPage(rootSlug, childSlug);
        }

        return this;
    }

    public TestPageHierarchyBuilder AddDeepHierarchy(string rootSlug, string level1Slug, string level2Slug)
    {
        this.AddRootPage(rootSlug);
        this.AddChildPage(rootSlug, level1Slug);
        this.AddChildPage(level1Slug, level2Slug);
        return this;
    }

    public async Task<TestPageHierarchyResult> CreateAsync(HttpClient client, CancellationToken cancellationToken = default)
    {
        var createdPages = new Dictionary<string, TestPageResult>();
        var pageIdLookup = new Dictionary<string, Guid>();

        // Sort pages by hierarchy level to ensure parents are created before children
        var sortedPages = this.SortByHierarchyLevel();

        foreach (var pageDefinition in sortedPages)
        {
            Guid? parentId = null;
            if (pageDefinition.ParentSlug != null && pageIdLookup.TryGetValue(pageDefinition.ParentSlug, out var foundParentId))
            {
                parentId = foundParentId;
            }

            var builder = TestCreatePageRequestBuilder.Create()
                .WithRealtorId(this._realtorId)
                .WithSlug(pageDefinition.Slug)
                .WithParentId(parentId);

            if (pageDefinition.Blocks.Any())
            {
                builder = builder.WithBlocks(pageDefinition.Blocks);
            }
            else if (pageDefinition.BlockTypeId.HasValue)
            {
                builder = builder.WithSingleTextBlock(pageDefinition.BlockTypeId.Value, pageDefinition.BlockText);
            }

            var result = await builder.CreateAsync(client, cancellationToken);
            createdPages[pageDefinition.Slug] = result;
            pageIdLookup[pageDefinition.Slug] = result.Id;

            // Create additional versions if specified
            foreach (var versionDef in pageDefinition.AdditionalVersions)
            {
                await Task.Delay(10, cancellationToken); // Small delay to ensure different timestamps

                var latestPage = await TestPageHelper.GetPageAsync(client, result.Id, true, cancellationToken);
                var updateBuilder = TestUpdatePageRequestBuilder.Create().WithLatestVersion(latestPage.LatestPageVersionId!.Value);

                if (versionDef.NewBlocks.Any())
                {
                    updateBuilder = updateBuilder.WithNewBlocks(versionDef.NewBlocks);
                }
                else if (versionDef.BlockTypeId.HasValue)
                {
                    updateBuilder = updateBuilder.WithSingleNewTextBlock(versionDef.BlockTypeId.Value, versionDef.BlockText);
                }

                await updateBuilder.UpdateAsync(client, result.Id, cancellationToken);
            }
        }

        return new TestPageHierarchyResult(createdPages, pageIdLookup, this._realtorId);
    }

    private List<HierarchyPageDefinition> SortByHierarchyLevel()
    {
        var result = new List<HierarchyPageDefinition>();
        var remaining = new Queue<HierarchyPageDefinition>(this._pages);
        var addedSlugs = new HashSet<string>();

        while (remaining.Count > 0)
        {
            var page = remaining.Dequeue();

            // If this is a root page or its parent has already been added, add it now
            if (page.ParentSlug == null || addedSlugs.Contains(page.ParentSlug))
            {
                result.Add(page);
                addedSlugs.Add(page.Slug);
            }
            else
            {
                // Re-queue for later processing
                remaining.Enqueue(page);
            }
        }

        return result;
    }
}

public class PageDefinitionBuilder
{
    private readonly string _slug;
    private readonly string? _parentSlug;
    private readonly List<PageVersionDefinition> _additionalVersions = [];
    private readonly Guid? _defaultBlockTypeId;
    private IReadOnlyList<CreatePageBlock> _blocks = [];
    private Guid? _blockTypeId;
    private string _blockText = "Default content";

    internal PageDefinitionBuilder(string slug, string? parentSlug, Guid? defaultBlockTypeId)
    {
        this._slug = slug;
        this._parentSlug = parentSlug;
        this._defaultBlockTypeId = defaultBlockTypeId;
        this._blockTypeId = defaultBlockTypeId;
    }

    public PageDefinitionBuilder WithTextBlock(Guid blockTypeId, string text)
    {
        this._blockTypeId = blockTypeId;
        this._blockText = text;
        this._blocks = [];
        return this;
    }

    public PageDefinitionBuilder WithBlocks(IReadOnlyList<CreatePageBlock> blocks)
    {
        this._blocks = blocks;
        this._blockTypeId = null;
        return this;
    }

    public PageDefinitionBuilder AddVersion(Action<PageVersionDefinitionBuilder>? configure = null)
    {
        var builder = new PageVersionDefinitionBuilder(this._defaultBlockTypeId);
        configure?.Invoke(builder);
        this._additionalVersions.Add(builder.Build());
        return this;
    }

    public PageDefinitionBuilder AddVersionWithText(Guid blockTypeId, string text) =>
        this.AddVersion(v => v.WithTextBlock(blockTypeId, text));

    internal HierarchyPageDefinition Build() => new(
        this._slug,
        this._parentSlug,
        this._blocks,
        this._blockTypeId,
        this._blockText,
        this._additionalVersions);
}

public class PageVersionDefinitionBuilder
{
    private IReadOnlyList<UpdatePageNewBlock> _newBlocks = [];
    private Guid? _blockTypeId;
    private string _blockText = "Updated content";

    internal PageVersionDefinitionBuilder(Guid? defaultBlockTypeId) => this._blockTypeId = defaultBlockTypeId;

    public PageVersionDefinitionBuilder WithTextBlock(Guid blockTypeId, string text)
    {
        this._blockTypeId = blockTypeId;
        this._blockText = text;
        this._newBlocks = [];
        return this;
    }

    public PageVersionDefinitionBuilder WithNewBlocks(IReadOnlyList<UpdatePageNewBlock> blocks)
    {
        this._newBlocks = blocks;
        this._blockTypeId = null;
        return this;
    }

    public PageVersionDefinition Build() => new(
        this._newBlocks,
        this._blockTypeId,
        this._blockText);
}

public sealed record HierarchyPageDefinition(
    string Slug,
    string? ParentSlug,
    IReadOnlyList<CreatePageBlock> Blocks,
    Guid? BlockTypeId,
    string BlockText,
    IReadOnlyList<PageVersionDefinition> AdditionalVersions);

public sealed record PageVersionDefinition(
    IReadOnlyList<UpdatePageNewBlock> NewBlocks,
    Guid? BlockTypeId,
    string BlockText);

public class TestPageHierarchyResult
{
    private readonly Dictionary<string, TestPageResult> _pages;
    private readonly Dictionary<string, Guid> _pageIds;

    internal TestPageHierarchyResult(
        Dictionary<string, TestPageResult> pages,
        Dictionary<string, Guid> pageIds,
        Guid realtorId)
    {
        this._pages = pages;
        this._pageIds = pageIds;
        this.RealtorId = realtorId;
    }

    public Guid RealtorId { get; }

    public int PageCount => this._pages.Count;

    public IReadOnlyList<TestPageResult> AllPages => [.. this._pages.Values];

    public IReadOnlyList<Guid> AllPageIds => [.. this._pageIds.Values];

    public TestPageResult GetPage(string slug) => this._pages[slug];

    public Guid GetPageId(string slug) => this._pageIds[slug];

    public IReadOnlyList<TestPageResult> GetRootPages() =>
        [.. this._pages.Values.Where(p => p.ExpectedParentId == null)];

    public IReadOnlyList<TestPageResult> GetChildPages(string parentSlug)
    {
        var parentId = this.GetPageId(parentSlug);
        return [.. this._pages.Values.Where(p => p.ExpectedParentId == parentId)];
    }

    public bool HasPage(string slug) => this._pages.ContainsKey(slug);
}
