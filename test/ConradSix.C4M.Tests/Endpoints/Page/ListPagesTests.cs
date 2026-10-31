namespace ConradSix.C4M.Tests.Endpoints.Page;

using System.Net;
using System.Net.Http.Json;

using AwesomeAssertions;

using ConradSix.C4M.Endpoints.Page;

[Collection(C4MAppCollection.Name)]
public sealed class ListPagesTests(C4MAppFixture fixture) : IAsyncDisposable
{
    [Fact]
    public async Task ListPages_ShouldReturnEmptyResult_WhenNoPages()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        // Act
        var result = await TestListPagesRequestBuilder
            .ForRealtor(realtorId)
            .ExecuteAsync(client, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Total.Should().Be(0);
        result.Limit.Should().Be(3); // Default limit from builder
        result.NextOffset.Should().BeNull();
        result.Pages.Should().BeEmpty();
    }

    [Fact]
    public async Task ListPages_ShouldReturnSinglePage_WhenOnePageExists()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        // Create a block type
        var blockType = await TestBlockTypeBuilder
            .CreateTextBlock("simple-text")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create a single page
        var createdPage = await TestCreatePageRequestBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithSlug("test-page")
            .WithSingleTextBlock(blockType.Id, "Hello World")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act
        var result = await TestListPagesRequestBuilder
            .ForRealtor(realtorId)
            .ExecuteAsync(client, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Total.Should().Be(1);
        result.Limit.Should().Be(3);
        result.NextOffset.Should().BeNull();
        result.Pages.Should().HaveCount(1);

        var page = result.Pages[0];
        page.PageId.Should().Be(createdPage.Id);
        page.ParentPageId.Should().BeNull();
        page.CurrentPageVersionId.Should().Be(createdPage.PageDetails.PageVersionId);
        page.Slug.Should().Be("test-page");
        page.Path.Should().Be("/test-page");
        page.SeoTitle.Should().Be(createdPage.PageDetails.Seo.Title);
        page.VersionCount.Should().Be(1);
        page.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
        page.UpdatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task ListPages_ShouldReturnLatestSeoTitle_WhenPageHasAnUnpublishedVersion()
    {
        var client = fixture.CreateClient();
        var createdPage = await TestCreatePageRequestBuilder
            .Create()
            .WithSlug("seo-title")
            .WithSeo(new("Original title", "Page description", null))
            .CreateAsync(client, TestContext.Current.CancellationToken);

        await TestUpdatePageRequestBuilder
            .Create()
            .WithLatestVersion(createdPage.PageDetails.PageVersionId)
            .WithSeo(new("Latest title", "Page description", null))
            .UpdateAsync(client, createdPage.Id, TestContext.Current.CancellationToken);

        var result = await TestListPagesRequestBuilder
            .ForRealtor(Guid.NewGuid())
            .ExecuteAsync(client, TestContext.Current.CancellationToken);

        var page = result.Pages.Should().ContainSingle().Subject;
        page.CurrentPageVersionId.Should().Be(createdPage.PageDetails.PageVersionId);
        page.SeoTitle.Should().Be("Latest title");
    }

    [Theory]
    [InlineData("  CoAsT  ", 2)]
    [InlineData("retired", 0)]
    [InlineData("%", 0)]
    [InlineData("_", 0)]
    public async Task ListPages_ShouldSearchLatestTitleOrPath_BeforePagination(string search, int expectedTotal)
    {
        var client = fixture.CreateClient();
        var titleMatch = await TestCreatePageRequestBuilder.Create()
            .WithSlug("homes")
            .WithSeo(new("Retired title", "Page description", null))
            .CreateAsync(client, TestContext.Current.CancellationToken);
        await TestUpdatePageRequestBuilder.Create()
            .WithLatestVersion(titleMatch.PageDetails.PageVersionId)
            .WithSeo(new("Coastal homes", "Page description", null))
            .UpdateAsync(client, titleMatch.Id, TestContext.Current.CancellationToken);
        await TestCreatePageRequestBuilder.Create()
            .WithSlug("coast")
            .WithSeo(new("Beach houses", "Page description", null))
            .CreateAsync(client, TestContext.Current.CancellationToken);
        await TestCreatePageRequestBuilder.Create()
            .WithSlug("mountains")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        var result = await TestListPagesRequestBuilder.ForRealtor(Guid.NewGuid())
            .WithSearch(search)
            .WithSort(ListPagesSort.PathAsc)
            .WithLimit(1)
            .ExecuteAsync(client, TestContext.Current.CancellationToken);

        result.Total.Should().Be(expectedTotal);
        result.Pages.Should().HaveCount(Math.Min(expectedTotal, 1));
        if (expectedTotal > 0)
        {
            result.Pages[0].Path.Should().Be("/coast");
            result.NextOffset.Should().Be(1);
            var nextPage = await TestListPagesRequestBuilder.ForRealtor(Guid.NewGuid())
                .WithSearch(search)
                .WithSort(ListPagesSort.PathAsc)
                .WithLimit(1)
                .WithOffset(1)
                .ExecuteAsync(client, TestContext.Current.CancellationToken);
            nextPage.Pages.Should().ContainSingle().Which.PageId.Should().Be(titleMatch.Id);
        }
    }

    [Theory]
    [InlineData(ListPagesSort.SeoTitleAsc, "Alpha homes")]
    [InlineData(ListPagesSort.SeoTitleDesc, "Zulu homes")]
    public async Task ListPages_ShouldSortByLatestSeoTitle(ListPagesSort sort, string firstTitle)
    {
        var client = fixture.CreateClient();
        var page = await TestCreatePageRequestBuilder.Create()
            .WithSlug("first")
            .WithSeo(new("Zulu homes", "Page description", null))
            .CreateAsync(client, TestContext.Current.CancellationToken);
        await TestUpdatePageRequestBuilder.Create()
            .WithLatestVersion(page.PageDetails.PageVersionId)
            .WithSeo(new("Alpha homes", "Page description", null))
            .UpdateAsync(client, page.Id, TestContext.Current.CancellationToken);
        await TestCreatePageRequestBuilder.Create()
            .WithSlug("second")
            .WithSeo(new("Zulu homes", "Page description", null))
            .CreateAsync(client, TestContext.Current.CancellationToken);

        var result = await TestListPagesRequestBuilder.ForRealtor(Guid.NewGuid())
            .WithSort(sort)
            .ExecuteAsync(client, TestContext.Current.CancellationToken);

        result.Pages.Should().HaveCount(2);
        result.Pages[0].SeoTitle.Should().Be(firstTitle);
    }

    [Theory]
    [InlineData(ListPagesSort.PublishedAsc, false)]
    [InlineData(ListPagesSort.PublishedDesc, true)]
    public async Task ListPages_ShouldSortByPublication(ListPagesSort sort, bool firstPublished)
    {
        var client = fixture.CreateClient();
        await TestCreatePageRequestBuilder.Create().WithSlug("published")
            .CreateAsync(client, TestContext.Current.CancellationToken);
        var page = await TestCreatePageRequestBuilder.Create().WithSlug("unpublished")
            .CreateAsync(client, TestContext.Current.CancellationToken);
        var response = await client.PostAsJsonAsync(
            $"/pages/{page.Id}/unpublish",
            new PagePublicationRequest(page.PageDetails.PageVersionId, true),
            TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var result = await TestListPagesRequestBuilder.ForRealtor(Guid.NewGuid())
            .WithSort(sort)
            .ExecuteAsync(client, TestContext.Current.CancellationToken);

        result.Pages.Select(item => item.IsPublished).Should().Equal(firstPublished, !firstPublished);
    }

    [Fact]
    public async Task ListPages_ShouldReturnMultiplePages_WhenMultiplePagesExist()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        // Create a block type
        var blockType = await TestBlockTypeBuilder
            .CreateTextBlock("simple-text")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create multiple pages using the hierarchy builder
        var hierarchy = await TestPageHierarchyBuilder
            .ForRealtor(realtorId)
            .WithDefaultBlockType(blockType.Id)
            .AddRootPage("home")
            .AddRootPage("about")
            .AddRootPage("contact")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act
        var result = await TestListPagesRequestBuilder
            .ForRealtor(realtorId)
            .WithLimit(10) // Ensure we get all pages
            .ExecuteAsync(client, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Total.Should().Be(3);
        result.Limit.Should().Be(10);
        result.NextOffset.Should().BeNull();
        result.Pages.Should().HaveCount(3);

        // Verify all pages are returned and have correct basic structure
        var returnedPageIds = result.Pages.Select(p => p.PageId).ToList();
        returnedPageIds.Should().BeEquivalentTo(hierarchy.AllPageIds);

        // Verify each page has the expected structure
        foreach (var page in result.Pages)
        {
            page.PageId.Should().NotBe(Guid.Empty);
            page.ParentPageId.Should().BeNull(); // All root pages
            page.CurrentPageVersionId.Should().NotBe(Guid.Empty);
            page.Slug.Should().NotBeNullOrEmpty();
            page.Path.Should().StartWith("/");
            page.VersionCount.Should().Be(1);
            page.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
            page.UpdatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
        }
    }

    [Fact]
    public async Task ListPages_ShouldReturnAllPages()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtor1Id = Guid.NewGuid();
        var realtor2Id = Guid.NewGuid();

        // Create a block type
        var blockType = await TestBlockTypeBuilder
            .CreateTextBlock("simple-text")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create pages for realtor 1
        await TestPageHierarchyBuilder
            .ForRealtor(realtor1Id)
            .WithDefaultBlockType(blockType.Id)
            .AddRootPage("realtor1-page1")
            .AddRootPage("realtor1-page2")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create pages for realtor 2
        await TestPageHierarchyBuilder
            .ForRealtor(realtor2Id)
            .WithDefaultBlockType(blockType.Id)
            .AddRootPage("realtor2-page1")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act
        var result = await TestListPagesRequestBuilder
            .ForRealtor(realtor1Id)
            .WithLimit(10)
            .ExecuteAsync(client, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Total.Should().Be(3);
        result.Pages.Should().HaveCount(3);

        result.Pages.Select(page => page.Slug)
            .Should()
            .BeEquivalentTo(["realtor1-page1", "realtor1-page2", "realtor2-page1"]);
    }

    [Fact]
    public async Task ListPages_ShouldReturnCorrectPagination_WhenMultiplePagesSpanMultiplePages()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        var blockType = await TestBlockTypeBuilder
            .CreateTextBlock("simple-text")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create 7 pages to test pagination (with limit of 3, we'll have 3 pages of results)
        await TestPageHierarchyBuilder
            .ForRealtor(realtorId)
            .WithDefaultBlockType(blockType.Id)
            .AddRootPage("page-a")
            .AddRootPage("page-b")
            .AddRootPage("page-c")
            .AddRootPage("page-d")
            .AddRootPage("page-e")
            .AddRootPage("page-f")
            .AddRootPage("page-g")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act - Get first page
        var firstPage = await TestListPagesRequestBuilder
            .ForRealtor(realtorId)
            .WithLimit(3)
            .WithOffset(0)
            .ExecuteAsync(client, TestContext.Current.CancellationToken);

        // Act - Get second page
        var secondPage = await TestListPagesRequestBuilder
            .ForRealtor(realtorId)
            .WithLimit(3)
            .WithOffset(3)
            .ExecuteAsync(client, TestContext.Current.CancellationToken);

        // Act - Get third page
        var thirdPage = await TestListPagesRequestBuilder
            .ForRealtor(realtorId)
            .WithLimit(3)
            .WithOffset(6)
            .ExecuteAsync(client, TestContext.Current.CancellationToken);

        // Assert first page
        firstPage.Should().NotBeNull();
        firstPage.Total.Should().Be(7);
        firstPage.Limit.Should().Be(3);
        firstPage.NextOffset.Should().Be(3);
        firstPage.Pages.Should().HaveCount(3);

        // Assert second page
        secondPage.Should().NotBeNull();
        secondPage.Total.Should().Be(7);
        secondPage.Limit.Should().Be(3);
        secondPage.NextOffset.Should().Be(6);
        secondPage.Pages.Should().HaveCount(3);

        // Assert third page (last page)
        thirdPage.Should().NotBeNull();
        thirdPage.Total.Should().Be(7);
        thirdPage.Limit.Should().Be(3);
        thirdPage.NextOffset.Should().BeNull(); // Last page
        thirdPage.Pages.Should().HaveCount(1); // Only 1 remaining page

        // Verify no duplicate pages across pagination
        var allPageIds = firstPage.Pages.Select(p => p.PageId)
            .Concat(secondPage.Pages.Select(p => p.PageId))
            .Concat(thirdPage.Pages.Select(p => p.PageId))
            .ToList();
        allPageIds.Should().HaveCount(7);
        allPageIds.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task ListPages_ShouldReturnEmptyNextOffset_WhenOnLastPage()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        var blockType = await TestBlockTypeBuilder
            .CreateTextBlock("simple-text")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create exactly 3 pages
        await TestPageHierarchyBuilder
            .ForRealtor(realtorId)
            .WithDefaultBlockType(blockType.Id)
            .AddRootPage("page-1")
            .AddRootPage("page-2")
            .AddRootPage("page-3")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act - Request with limit 3, offset 0 (should get all pages, no next page)
        var result = await TestListPagesRequestBuilder
            .ForRealtor(realtorId)
            .WithLimit(3)
            .WithOffset(0)
            .ExecuteAsync(client, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Total.Should().Be(3);
        result.Limit.Should().Be(3);
        result.NextOffset.Should().BeNull(); // No next page
        result.Pages.Should().HaveCount(3);
    }

    [Fact]
    public async Task ListPages_ShouldSortBySlugDescending_WhenSlugDescSortSpecified()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        var blockType = await TestBlockTypeBuilder
            .CreateTextBlock("simple-text")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        await TestPageHierarchyBuilder
            .ForRealtor(realtorId)
            .WithDefaultBlockType(blockType.Id)
            .AddRootPage("alpha")
            .AddRootPage("beta")
            .AddRootPage("gamma")
            .AddRootPage("delta")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act
        var result = await TestListPagesRequestBuilder
            .ForRealtor(realtorId)
            .SortedBySlug(ascending: false)
            .WithLimit(10)
            .ExecuteAsync(client, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Pages.Should().HaveCount(4);

        var slugs = result.Pages.Select(p => p.Slug).ToList();
        slugs.Should().BeEquivalentTo(["gamma", "delta", "beta", "alpha"], options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task ListPages_ShouldSortByPath_WhenPathSortSpecified()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        var blockType = await TestBlockTypeBuilder
            .CreateTextBlock("simple-text")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        await TestPageHierarchyBuilder
            .ForRealtor(realtorId)
            .WithDefaultBlockType(blockType.Id)
            .AddRootPage("zebra")
            .AddRootPage("apple")
            .AddRootPage("banana")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act - Sort by path ascending
        var resultAsc = await TestListPagesRequestBuilder
            .ForRealtor(realtorId)
            .SortedByPath(ascending: true)
            .WithLimit(10)
            .ExecuteAsync(client, TestContext.Current.CancellationToken);

        // Act - Sort by path descending
        var resultDesc = await TestListPagesRequestBuilder
            .ForRealtor(realtorId)
            .SortedByPath(ascending: false)
            .WithLimit(10)
            .ExecuteAsync(client, TestContext.Current.CancellationToken);

        // Assert ascending
        var pathsAsc = resultAsc.Pages.Select(p => p.Path).ToList();
        pathsAsc.Should().BeEquivalentTo(["/apple", "/banana", "/zebra"], options => options.WithStrictOrdering());

        // Assert descending
        var pathsDesc = resultDesc.Pages.Select(p => p.Path).ToList();
        pathsDesc.Should().BeEquivalentTo(["/zebra", "/banana", "/apple"], options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task ListPages_ShouldSortByPathAsTree_WhenSiblingsShareParentPrefix()
    {
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        await TestPageHierarchyBuilder
            .ForRealtor(realtorId)
            .AddRootPage("resort")
            .AddRootPage("resort-villas")
            .AddRootPage("resorts")
            .AddChildPage("resort", "builders")
            .AddChildPage("builders", "acme")
            .AddChildPage("resort", "watson")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        var resultAsc = await TestListPagesRequestBuilder
            .ForRealtor(realtorId)
            .SortedByPath(ascending: true)
            .WithLimit(10)
            .ExecuteAsync(client, TestContext.Current.CancellationToken);
        var resultDesc = await TestListPagesRequestBuilder
            .ForRealtor(realtorId)
            .SortedByPath(ascending: false)
            .WithLimit(10)
            .ExecuteAsync(client, TestContext.Current.CancellationToken);

        string[] treeOrder =
        [
            "/resort",
            "/resort/builders",
            "/resort/builders/acme",
            "/resort/watson",
            "/resort-villas",
            "/resorts",
        ];
        resultAsc.Pages.Select(p => p.Path).Should().Equal(treeOrder);
        resultDesc.Pages.Select(p => p.Path).Should().Equal(treeOrder.Reverse());
    }

    [Fact]
    public async Task ListPages_ShouldFilterBySlugContains_WhenSlugContainsFilterSpecified()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        var blockType = await TestBlockTypeBuilder
            .CreateTextBlock("simple-text")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        await TestPageHierarchyBuilder
            .ForRealtor(realtorId)
            .WithDefaultBlockType(blockType.Id)
            .AddRootPage("about-us")
            .AddRootPage("contact-info")
            .AddRootPage("about-team")
            .AddRootPage("services")
            .AddRootPage("home")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act
        var result = await TestListPagesRequestBuilder
            .ForRealtor(realtorId)
            .WithSlugContains("about")
            .WithLimit(10)
            .ExecuteAsync(client, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Total.Should().Be(2);
        result.Pages.Should().HaveCount(2);

        var slugs = result.Pages.Select(p => p.Slug).ToList();
        slugs.Should().BeEquivalentTo(["about-team", "about-us"]);
    }

    [Fact]
    public async Task ListPages_ShouldFilterBySlugStartsWith_WhenSlugStartsWithFilterSpecified()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        var blockType = await TestBlockTypeBuilder
            .CreateTextBlock("simple-text")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        await TestPageHierarchyBuilder
            .ForRealtor(realtorId)
            .WithDefaultBlockType(blockType.Id)
            .AddRootPage("product-alpha")
            .AddRootPage("product-beta")
            .AddRootPage("service-gamma")
            .AddRootPage("about-product")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act
        var result = await TestListPagesRequestBuilder
            .ForRealtor(realtorId)
            .WithSlugStartsWith("product")
            .WithLimit(10)
            .ExecuteAsync(client, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Total.Should().Be(2);
        result.Pages.Should().HaveCount(2);

        var slugs = result.Pages.Select(p => p.Slug).ToList();
        slugs.Should().BeEquivalentTo(["product-alpha", "product-beta"]);
    }

    [Fact]
    public async Task ListPages_ShouldFilterBySlugEndsWith_WhenSlugEndsWithFilterSpecified()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        var blockType = await TestBlockTypeBuilder
            .CreateTextBlock("simple-text")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        await TestPageHierarchyBuilder
            .ForRealtor(realtorId)
            .WithDefaultBlockType(blockType.Id)
            .AddRootPage("main-page")
            .AddRootPage("contact-page")
            .AddRootPage("home-section")
            .AddRootPage("about-page")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act
        var result = await TestListPagesRequestBuilder
            .ForRealtor(realtorId)
            .WithSlugEndsWith("page")
            .WithLimit(10)
            .ExecuteAsync(client, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Total.Should().Be(3);
        result.Pages.Should().HaveCount(3);

        var slugs = result.Pages.Select(p => p.Slug).ToList();
        slugs.Should().BeEquivalentTo(["about-page", "contact-page", "main-page"]);
    }

    [Fact]
    public async Task ListPages_ShouldFilterBySlugRegex_WhenSlugRegexFilterSpecified()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        var blockType = await TestBlockTypeBuilder
            .CreateTextBlock("simple-text")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        await TestPageHierarchyBuilder
            .ForRealtor(realtorId)
            .WithDefaultBlockType(blockType.Id)
            .AddRootPage("page-1")
            .AddRootPage("page-2")
            .AddRootPage("page-3")
            .AddRootPage("about")
            .AddRootPage("contact")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act - Use regex to match pages that end with a digit
        var result = await TestListPagesRequestBuilder
            .ForRealtor(realtorId)
            .WithSlugRegex(@".*-\d+$")
            .WithLimit(10)
            .ExecuteAsync(client, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Total.Should().Be(3);
        result.Pages.Should().HaveCount(3);

        var slugs = result.Pages.Select(p => p.Slug).ToList();
        slugs.Should().BeEquivalentTo(["page-1", "page-2", "page-3"]);
    }

    [Fact]
    public async Task ListPages_ShouldFilterByPathContains_WhenPathContainsFilterSpecified()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        var blockType = await TestBlockTypeBuilder
            .CreateTextBlock("simple-text")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        await TestPageHierarchyBuilder
            .ForRealtor(realtorId)
            .WithDefaultBlockType(blockType.Id)
            .AddRootPage("api-docs")
            .AddRootPage("user-api")
            .AddRootPage("documentation")
            .AddRootPage("about")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act
        var result = await TestListPagesRequestBuilder
            .ForRealtor(realtorId)
            .WithPathContains("api")
            .WithLimit(10)
            .ExecuteAsync(client, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Total.Should().Be(2);
        result.Pages.Should().HaveCount(2);

        var paths = result.Pages.Select(p => p.Path).ToList();
        paths.Should().BeEquivalentTo(["/api-docs", "/user-api"]);
    }

    [Fact]
    public async Task ListPages_ShouldFilterByPathRegex_WhenPathRegexFilterSpecified()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        var blockType = await TestBlockTypeBuilder
            .CreateTextBlock("simple-text")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        await TestPageHierarchyBuilder
            .ForRealtor(realtorId)
            .WithDefaultBlockType(blockType.Id)
            .AddRootPage("v1-api")
            .AddRootPage("v2-api")
            .AddRootPage("v3-beta")
            .AddRootPage("about")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act - Use regex to match paths that contain "v" followed by a digit
        var result = await TestListPagesRequestBuilder
            .ForRealtor(realtorId)
            .WithPathRegex(@"/v\d")
            .WithLimit(10)
            .ExecuteAsync(client, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Total.Should().Be(3);
        result.Pages.Should().HaveCount(3);

        var paths = result.Pages.Select(p => p.Path).ToList();
        paths.Should().BeEquivalentTo(["/v1-api", "/v2-api", "/v3-beta"]);
    }

    [Fact]
    public async Task ListPages_ShouldCombineFiltersAndSorting_WhenMultipleParametersSpecified()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        var blockType = await TestBlockTypeBuilder
            .CreateTextBlock("simple-text")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        await TestPageHierarchyBuilder
            .ForRealtor(realtorId)
            .WithDefaultBlockType(blockType.Id)
            .AddRootPage("blog-post-alpha")
            .AddRootPage("blog-post-beta")
            .AddRootPage("news-post-gamma")
            .AddRootPage("blog-article-delta")
            .AddRootPage("about")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act - Filter by slug contains "blog" and sort by slug descending
        var result = await TestListPagesRequestBuilder
            .ForRealtor(realtorId)
            .WithSlugContains("blog")
            .SortedBySlug(ascending: false)
            .WithLimit(10)
            .ExecuteAsync(client, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Total.Should().Be(3);
        result.Pages.Should().HaveCount(3);

        var slugs = result.Pages.Select(p => p.Slug).ToList();
        slugs.Should().BeEquivalentTo(["blog-post-beta", "blog-post-alpha", "blog-article-delta"], options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task ListPages_ShouldReturnEmptyResult_WhenFilterMatchesNoPages()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        var blockType = await TestBlockTypeBuilder
            .CreateTextBlock("simple-text")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        await TestPageHierarchyBuilder
            .ForRealtor(realtorId)
            .WithDefaultBlockType(blockType.Id)
            .AddRootPage("home")
            .AddRootPage("about")
            .AddRootPage("contact")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act
        var result = await TestListPagesRequestBuilder
            .ForRealtor(realtorId)
            .WithSlugContains("nonexistent")
            .WithLimit(10)
            .ExecuteAsync(client, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Total.Should().Be(0);
        result.Pages.Should().BeEmpty();
        result.NextOffset.Should().BeNull();
    }

    public ValueTask DisposeAsync() => fixture.TearDownTestAsync();
}
