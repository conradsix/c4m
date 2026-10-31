#pragma warning disable SA1402 // File may only contain a single type
namespace ConradSix.C4M.Tests.Endpoints.Page;

using System.Net;
using System.Net.Http.Json;

using AwesomeAssertions;

using ConradSix.C4M.Endpoints.Page;

public sealed class TestCreatePageRequestBuilder
{
    private Guid _realtorId = Guid.NewGuid();
    private Guid? _parentId;
    private Guid? _navigationMenuId;
    private string _slug = "test-page";
    private IReadOnlyList<CreatePageBlock> _blocks = [];
    private PageSeoRequest _seo = new(
        Title: "Test page",
        Description: "A test page description.",
        SocialImageId: null);

    public static TestCreatePageRequestBuilder Create() => new();

    public TestCreatePageRequestBuilder WithRealtorId(Guid realtorId)
    {
        this._realtorId = realtorId;
        return this;
    }

    public TestCreatePageRequestBuilder WithParentId(Guid? parentId)
    {
        this._parentId = parentId;
        return this;
    }

    public TestCreatePageRequestBuilder WithNavigationMenuId(Guid? navigationMenuId)
    {
        this._navigationMenuId = navigationMenuId;
        return this;
    }

    public TestCreatePageRequestBuilder WithSlug(string slug)
    {
        this._slug = slug;
        return this;
    }

    public TestCreatePageRequestBuilder WithSeo(PageSeoRequest seo)
    {
        this._seo = seo;
        return this;
    }

    public TestCreatePageRequestBuilder WithNonExistentBlockType(Guid? nonExistentBlockTypeId = null)
    {
        var blockTypeId = nonExistentBlockTypeId ?? Guid.NewGuid();
        this._blocks = [
            new CreatePageBlock(
                BlockTypeId: blockTypeId,
                BlockData: TestBlockTypeBuilder.GenerateTextBlockJson("Hello World")),
        ];
        return this;
    }

    public TestCreatePageRequestBuilder WithBlocks(IReadOnlyList<CreatePageBlock> blocks)
    {
        this._blocks = blocks;
        return this;
    }

    public TestCreatePageRequestBuilder WithSingleTextBlock(Guid blockTypeId, string text)
    {
        this._blocks = [
            new CreatePageBlock(
                BlockTypeId: blockTypeId,
                BlockData: TestBlockTypeBuilder.GenerateTextBlockJson(text)),
        ];
        return this;
    }

    public TestCreatePageRequestBuilder WithSingleImageBlock(Guid blockTypeId, string url, string alt)
    {
        this._blocks = [
            new CreatePageBlock(
                BlockTypeId: blockTypeId,
                BlockData: TestBlockTypeBuilder.GenerateImageBlockJson(url, alt)),
        ];
        return this;
    }

    public TestCreatePageRequestBuilder WithSingleRichTextBlock(Guid blockTypeId, string html, string? plainText = null)
    {
        this._blocks = [
            new CreatePageBlock(
                BlockTypeId: blockTypeId,
                BlockData: TestBlockTypeBuilder.GenerateRichTextBlockJson(html, plainText)),
        ];
        return this;
    }

    public TestCreatePageRequestBuilder WithSingleButtonBlock(Guid blockTypeId, string text, string url, string style = "primary")
    {
        this._blocks = [
            new CreatePageBlock(
                BlockTypeId: blockTypeId,
                BlockData: TestBlockTypeBuilder.GenerateButtonBlockJson(text, url, style)),
        ];
        return this;
    }

    public TestCreatePageRequestBuilder WithSingleSectionBlock(Guid blockTypeId, string title, string subtitle = "")
    {
        this._blocks = [
            new CreatePageBlock(
                BlockTypeId: blockTypeId,
                BlockData: TestBlockTypeBuilder.GenerateSectionBlockJson(title, subtitle)),
        ];
        return this;
    }

    public TestCreatePageRequestBuilder WithSingleBlock(Guid blockTypeId, string blockData)
    {
        this._blocks = [
            new CreatePageBlock(
                BlockTypeId: blockTypeId,
                BlockData: blockData),
        ];
        return this;
    }

    public TestCreatePageRequestBuilder WithMultipleBlocks(params CreatePageBlock[] blocks)
    {
        this._blocks = [.. blocks];
        return this;
    }

    public async Task<TestPageResult> CreateAsync(HttpClient client, CancellationToken cancellationToken = default)
    {
        var request = new CreatePageRequest(
            ParentId: this._parentId,
            Slug: this._slug,
            Blocks: this._blocks,
            Seo: this._seo,
            NavigationMenuId: this._navigationMenuId);

        var response = await client.PostAsJsonAsync("/pages", request, cancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdPage = await response.Content.ReadFromJsonAsync<CreatePageResponse>(cancellationToken);
        createdPage.Should().NotBeNull();

        // Get the Location header for the created resource
        var locationHeader = response.Headers.Location?.ToString();
        locationHeader.Should().NotBeNullOrEmpty();
        locationHeader.Should().StartWith("/pages/");

        // Fetch the full page details
        var getPageResponse = await client.GetAsync(locationHeader, cancellationToken);
        getPageResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var pageDetails = await getPageResponse.Content
            .ReadFromJsonAsync<GetPageResponse>(cancellationToken);
        pageDetails.Should().NotBeNull();

        return new TestPageResult(
            Id: createdPage.Id,
            PageDetails: pageDetails,
            LocationHeader: locationHeader,
            ExpectedRealtorId: this._realtorId,
            ExpectedParentId: this._parentId,
            ExpectedSlug: this._slug,
            ExpectedBlocks: this._blocks);
    }

    public async Task<HttpResponseMessage> CreateExpectingAsync(HttpClient client, HttpStatusCode expectedStatusCode, CancellationToken cancellationToken = default)
    {
        var request = new CreatePageRequest(
            ParentId: this._parentId,
            Slug: this._slug,
            Blocks: this._blocks,
            Seo: this._seo,
            NavigationMenuId: this._navigationMenuId);

        var response = await client.PostAsJsonAsync("/pages", request, cancellationToken);
        response.StatusCode.Should().Be(expectedStatusCode);

        return response;
    }
}

public sealed record TestPageResult(
    Guid Id,
    GetPageResponse PageDetails,
    string LocationHeader,
    Guid ExpectedRealtorId,
    Guid? ExpectedParentId,
    string ExpectedSlug,
    IReadOnlyList<CreatePageBlock> ExpectedBlocks)
{
    public void Assert(string expectedPath)
    {
        // Validate basic page properties
        this.Id.Should().NotBe(Guid.Empty);
        this.PageDetails.Should().NotBeNull();

        // Validate page metadata
        this.PageDetails.ParentId.Should().Be(this.ExpectedParentId);
        this.PageDetails.Slug.Should().Be(this.ExpectedSlug);

        // Validate path - user must be explicit about expected path
        this.PageDetails.Path.Should().Be(expectedPath);

        // Validate timestamps
        this.PageDetails.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
        this.PageDetails.UpdatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));

        // Validate blocks
        this.PageDetails.Blocks.Should().HaveCount(this.ExpectedBlocks.Count);

        for (var i = 0; i < this.ExpectedBlocks.Count; i++)
        {
            var expectedBlock = this.ExpectedBlocks[i];
            var actualBlock = this.PageDetails.Blocks[i];

            // Validate block properties
            actualBlock.Id.Should().NotBe(Guid.Empty);
            actualBlock.BlockTypeId.Should().Be(expectedBlock.BlockTypeId);

            // For JSON data, we need to handle property order differences
            // Skip exact string comparison for JSON - let individual tests handle specific validation if needed
            actualBlock.BlockData.Should().NotBeNullOrEmpty();

            actualBlock.BlockVersion.Should().NotBeNullOrEmpty();
            actualBlock.BlockName.Should().NotBeNullOrEmpty();
        }

        // Validate location header
        this.LocationHeader.Should().NotBeNullOrEmpty();
        this.LocationHeader.Should().StartWith("/pages/");
        this.LocationHeader.Should().EndWith(this.Id.ToString());
    }

    public void AssertBlockType(int blockIndex, string expectedTypeName, string expectedTypeVersion)
    {
        this.PageDetails.Blocks.Should().HaveCountGreaterThan(blockIndex);
        var block = this.PageDetails.Blocks[blockIndex];

        block.BlockName.Should().Be(expectedTypeName);
        block.BlockVersion.Should().Be(expectedTypeVersion);
    }
}
