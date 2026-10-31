#pragma warning disable SA1402 // File may only contain a single type
namespace ConradSix.C4M.Tests.Endpoints.Page;

using System.Net;
using System.Net.Http.Json;

using AwesomeAssertions;

using ConradSix.C4M.Endpoints.Page;

public sealed class TestUpdatePageRequestBuilder
{
    private Guid? _parentId;
    private string _slug = "test-page";
    private Guid _expectedLatestPageVersionId;
    private IReadOnlyList<UpdatePageNewBlock> _newBlocks = [];
    private IReadOnlyList<UpdatePageExistingBlock> _existingBlocks = [];
    private PageSeoRequest _seo = new(
        Title: "Test page",
        Description: "A test page description.",
        SocialImageId: null);

    public static TestUpdatePageRequestBuilder Create() => new();

    public TestUpdatePageRequestBuilder WithParentId(Guid? parentId)
    {
        this._parentId = parentId;
        return this;
    }

    public TestUpdatePageRequestBuilder WithSlug(string slug)
    {
        this._slug = slug;
        return this;
    }

    public TestUpdatePageRequestBuilder WithLatestVersion(Guid version)
    {
        this._expectedLatestPageVersionId = version;
        return this;
    }

    public TestUpdatePageRequestBuilder WithSeo(PageSeoRequest seo)
    {
        this._seo = seo;
        return this;
    }

    public TestUpdatePageRequestBuilder WithNewBlocks(IReadOnlyList<UpdatePageNewBlock> newBlocks)
    {
        this._newBlocks = newBlocks;
        return this;
    }

    public TestUpdatePageRequestBuilder WithExistingBlocks(IReadOnlyList<UpdatePageExistingBlock> existingBlocks)
    {
        this._existingBlocks = existingBlocks;
        return this;
    }

    public TestUpdatePageRequestBuilder WithSingleNewTextBlock(Guid blockTypeId, string text, int sortOrder = 0)
    {
        this._newBlocks = [
            new UpdatePageNewBlock(
                BlockTypeId: blockTypeId,
                SortOrder: sortOrder,
                BlockData: TestBlockTypeBuilder.GenerateTextBlockJson(text)),
        ];
        return this;
    }

    public TestUpdatePageRequestBuilder WithSingleNewImageBlock(Guid blockTypeId, string url, string alt, int sortOrder = 0)
    {
        this._newBlocks = [
            new UpdatePageNewBlock(
                BlockTypeId: blockTypeId,
                SortOrder: sortOrder,
                BlockData: TestBlockTypeBuilder.GenerateImageBlockJson(url, alt)),
        ];
        return this;
    }

    public TestUpdatePageRequestBuilder WithSingleNewRichTextBlock(Guid blockTypeId, string html, string? plainText = null, int sortOrder = 0)
    {
        this._newBlocks = [
            new UpdatePageNewBlock(
                BlockTypeId: blockTypeId,
                SortOrder: sortOrder,
                BlockData: TestBlockTypeBuilder.GenerateRichTextBlockJson(html, plainText)),
        ];
        return this;
    }

    public TestUpdatePageRequestBuilder WithSingleNewButtonBlock(Guid blockTypeId, string text, string url, string style = "primary", int sortOrder = 0)
    {
        this._newBlocks = [
            new UpdatePageNewBlock(
                BlockTypeId: blockTypeId,
                SortOrder: sortOrder,
                BlockData: TestBlockTypeBuilder.GenerateButtonBlockJson(text, url, style)),
        ];
        return this;
    }

    public TestUpdatePageRequestBuilder WithSingleNewSectionBlock(Guid blockTypeId, string title, string subtitle = "", int sortOrder = 0)
    {
        this._newBlocks = [
            new UpdatePageNewBlock(
                BlockTypeId: blockTypeId,
                SortOrder: sortOrder,
                BlockData: TestBlockTypeBuilder.GenerateSectionBlockJson(title, subtitle)),
        ];
        return this;
    }

    public TestUpdatePageRequestBuilder WithSingleNewBlock(Guid blockTypeId, string blockData, int sortOrder = 0)
    {
        this._newBlocks = [
            new UpdatePageNewBlock(
                BlockTypeId: blockTypeId,
                SortOrder: sortOrder,
                BlockData: blockData),
        ];
        return this;
    }

    public TestUpdatePageRequestBuilder WithMultipleNewBlocks(params UpdatePageNewBlock[] newBlocks)
    {
        this._newBlocks = [.. newBlocks];
        return this;
    }

    public TestUpdatePageRequestBuilder WithSingleExistingBlock(Guid blockId, int sortOrder = 0)
    {
        this._existingBlocks = [
            new UpdatePageExistingBlock(
                BlockId: blockId,
                SortOrder: sortOrder),
        ];
        return this;
    }

    public TestUpdatePageRequestBuilder WithMultipleExistingBlocks(params UpdatePageExistingBlock[] existingBlocks)
    {
        this._existingBlocks = [.. existingBlocks];
        return this;
    }

    public TestUpdatePageRequestBuilder AddNewBlock(Guid blockTypeId, int sortOrder, string blockData)
    {
        var newBlock = new UpdatePageNewBlock(blockTypeId, sortOrder, blockData);
        this._newBlocks = [.. this._newBlocks, newBlock];
        return this;
    }

    public TestUpdatePageRequestBuilder AddExistingBlock(Guid blockId, int sortOrder)
    {
        var existingBlock = new UpdatePageExistingBlock(blockId, sortOrder);
        this._existingBlocks = [.. this._existingBlocks, existingBlock];
        return this;
    }

    public UpdatePageRequest Build() => new(
        ExpectedLatestPageVersionId: this._expectedLatestPageVersionId,
        NewBlocks: this._newBlocks,
        ExistingBlocks: this._existingBlocks,
        Seo: this._seo);

    public async Task<TestUpdatePageResult> UpdateAsync(HttpClient client, Guid pageId, CancellationToken cancellationToken = default)
    {
        var request = this.Build();

        var response = await client.PutAsJsonAsync($"/pages/{pageId}", request, cancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var updatedPage = await response.Content.ReadFromJsonAsync<UpdatePageResponse>(cancellationToken);
        updatedPage.Should().NotBeNull();
        updatedPage.PageVersionId.Should().NotBeEmpty();

        // Get the Location header for the created resource
        var locationHeader = response.Headers.Location?.ToString();
        locationHeader.Should().NotBeNullOrEmpty();
        locationHeader.Should().StartWith("/pages/by-version/");

        // Fetch the full page details
        var getPageByVersionResponse = await client.GetAsync(locationHeader, cancellationToken);
        getPageByVersionResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var pageDetails = await getPageByVersionResponse.Content
            .ReadFromJsonAsync<GetPageResponse>(cancellationToken);
        pageDetails.Should().NotBeNull();

        return new TestUpdatePageResult(
            PageDetails: pageDetails,
            ExpectedParentId: this._parentId,
            ExpectedSlug: this._slug,
            ExpectedNewBlocks: this._newBlocks,
            ExpectedExistingBlocks: this._existingBlocks);
    }

    public async Task<HttpResponseMessage> UpdateExpectingAsync(HttpClient client, Guid pageId, HttpStatusCode expectedStatusCode, CancellationToken cancellationToken = default)
    {
        var request = this.Build();

        var response = await client.PutAsJsonAsync($"/pages/{pageId}", request, cancellationToken);
        response.StatusCode.Should().Be(expectedStatusCode);

        return response;
    }
}

public sealed record TestUpdatePageResult(
    GetPageResponse PageDetails,
    Guid? ExpectedParentId,
    string ExpectedSlug,
    IReadOnlyList<UpdatePageNewBlock> ExpectedNewBlocks,
    IReadOnlyList<UpdatePageExistingBlock> ExpectedExistingBlocks)
{
    public void Assert(string expectedPath)
    {
        // Validate basic page properties
        this.PageDetails.Should().NotBeNull();

        // Validate page metadata
        this.PageDetails.ParentId.Should().Be(this.ExpectedParentId);
        this.PageDetails.Slug.Should().Be(this.ExpectedSlug);
        this.PageDetails.Path.Should().Be(expectedPath);

        // Validate timestamps
        this.PageDetails.UpdatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));

        // Validate total block count
        var expectedTotalBlocks = this.ExpectedNewBlocks.Count + this.ExpectedExistingBlocks.Count;
        this.PageDetails.Blocks.Should().HaveCount(expectedTotalBlocks);
    }

    public void AssertBlock(
        int blockIndex,
        (string Name, string Version) expectedType,
        string expectedBlockData)
    {
        this.PageDetails.Blocks.Should().HaveCountGreaterThan(blockIndex);
        var block = this.PageDetails.Blocks[blockIndex];

        block.BlockName.Should().Be(expectedType.Name);
        block.BlockVersion.Should().Be(expectedType.Version);
        block.BlockData.Should().BeJsonEqual(expectedBlockData);
    }

    public void AssertBlockNotPresent(Guid blockId) =>
        this.PageDetails.Blocks.Should().NotContain(b => b.Id == blockId);
}
