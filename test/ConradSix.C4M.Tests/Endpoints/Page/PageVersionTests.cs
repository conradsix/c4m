namespace ConradSix.C4M.Tests.Endpoints.Page;

using System.Net;

using AwesomeAssertions;

[Collection(C4MAppCollection.Name)]
public sealed class PageVersionTests(C4MAppFixture fixture) : IAsyncDisposable
{
    [Fact]
    public async Task SetCurrentPageVersion_ShouldRejectDraft_WhenSeoIsIncomplete()
    {
        var client = fixture.CreateClient();
        var blockType = await TestBlockTypeBuilder
            .CreateTextBlock("publication-seo")
            .CreateAsync(client, TestContext.Current.CancellationToken);
        var initialPage = await TestCreatePageRequestBuilder
            .Create()
            .WithSlug("publication-seo")
            .WithSingleTextBlock(blockType.Id, "Published content")
            .CreateAsync(client, TestContext.Current.CancellationToken);
        var draft = await TestUpdatePageRequestBuilder
            .Create()
            .WithSlug("publication-seo")
            .WithLatestVersion(initialPage.PageDetails.LatestPageVersionId!.Value)
            .WithSeo(new(null, null, null))
            .WithSingleNewTextBlock(blockType.Id, "Draft content")
            .UpdateAsync(client, initialPage.Id, TestContext.Current.CancellationToken);

        await TestPageHelper.SetCurrentVersionExpectingAsync(
            client,
            draft.PageDetails.PageVersionId,
            HttpStatusCode.BadRequest,
            TestContext.Current.CancellationToken);

        var currentPage = await TestPageHelper.GetPageAsync(
            client,
            initialPage.Id,
            TestContext.Current.CancellationToken);
        currentPage.PageVersionId.Should().Be(initialPage.PageDetails.PageVersionId);
    }

    [Fact]
    public async Task SetCurrentPageVersion_ShouldSetVersionAsCurrent_WhenValidVersionId()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        var blockType = await TestBlockTypeBuilder
            .CreateTextBlock("text-content")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create initial page
        var initialPage = await TestCreatePageRequestBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithSlug("test-page")
            .WithSingleTextBlock(blockType.Id, "Version 1 content")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create second version
        var secondVersion = await TestUpdatePageRequestBuilder
            .Create()
            .WithSlug("test-page")
            .WithLatestVersion(initialPage.PageDetails.LatestPageVersionId!.Value)
            .WithSingleNewTextBlock(blockType.Id, "Version 2 content")
            .UpdateAsync(client, initialPage.Id, TestContext.Current.CancellationToken);

        // Act - Set second version as current
        var setCurrentResult = await TestPageHelper.SetCurrentVersionAsync(
            client, secondVersion.PageDetails.PageVersionId, TestContext.Current.CancellationToken);

        // Assert
        setCurrentResult.Should().NotBeNull();
        setCurrentResult.PreviousPageVersionId.Should().Be(initialPage.PageDetails.PageVersionId);

        // Verify that getting the page by ID now returns the second version's content
        var currentPageContent = await TestPageHelper.GetPageAsync(
            client, initialPage.Id, TestContext.Current.CancellationToken);

        currentPageContent.PageVersionId.Should().Be(secondVersion.PageDetails.PageVersionId);
        currentPageContent.Blocks[0].BlockData.Should().BeJsonEqual(
            TestBlockTypeBuilder.GenerateTextBlockJson("Version 2 content"));
    }

    [Fact]
    public async Task SetCurrentPageVersion_ShouldUpdatePageCurrentVersion_WhenSetToDifferentVersion()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        var blockType = await TestBlockTypeBuilder
            .CreateTextBlock("text-content")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create initial page (Version A)
        var versionA = await TestCreatePageRequestBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithSlug("test-page")
            .WithSingleTextBlock(blockType.Id, "Version A content")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create Version B
        var versionB = await TestUpdatePageRequestBuilder
            .Create()
            .WithSlug("test-page")
            .WithLatestVersion(versionA.PageDetails.LatestPageVersionId!.Value)
            .WithSingleNewTextBlock(blockType.Id, "Version B content")
            .UpdateAsync(client, versionA.Id, TestContext.Current.CancellationToken);

        // Set Version A as current
        await TestPageHelper.SetCurrentVersionAsync(
            client, versionA.PageDetails.PageVersionId, TestContext.Current.CancellationToken);

        // Verify Version A is current
        var pageWithVersionA = await TestPageHelper.GetPageAsync(
            client, versionA.Id, TestContext.Current.CancellationToken);
        pageWithVersionA.PageVersionId.Should().Be(versionA.PageDetails.PageVersionId);

        // Act - Set Version B as current
        await TestPageHelper.SetCurrentVersionAsync(
            client, versionB.PageDetails.PageVersionId, TestContext.Current.CancellationToken);

        // Assert - Verify that getting the page by ID now returns Version B's content
        var pageWithVersionB = await TestPageHelper.GetPageAsync(
            client, versionA.Id, TestContext.Current.CancellationToken);

        pageWithVersionB.PageVersionId.Should().Be(versionB.PageDetails.PageVersionId);
        pageWithVersionB.Blocks[0].BlockData.Should().BeJsonEqual(
            TestBlockTypeBuilder.GenerateTextBlockJson("Version B content"));

        // Verify that Version A is no longer current but still accessible by version ID
        var versionAById = await TestPageHelper.GetPageVersionAsync(
            client, versionA.PageDetails.PageVersionId, TestContext.Current.CancellationToken);
        versionAById.Blocks[0].BlockData.Should().BeJsonEqual(
            TestBlockTypeBuilder.GenerateTextBlockJson("Version A content"));
    }

    [Fact]
    public async Task SetCurrentPageVersion_ShouldReturnNotFound_WhenVersionIdDoesNotExist()
    {
        // Arrange
        var client = fixture.CreateClient();
        var nonExistentVersionId = Guid.NewGuid();

        // Act & Assert
        await TestPageHelper.SetCurrentVersionExpectingAsync(
            client, nonExistentVersionId, HttpStatusCode.NotFound, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SetCurrentPageVersion_ShouldWorkWithGetPageVersionEndpoint_WhenAccessingSpecificVersion()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        var blockType = await TestBlockTypeBuilder
            .CreateTextBlock("text-content")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create Version A (current)
        var versionA = await TestCreatePageRequestBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithSlug("test-page")
            .WithSingleTextBlock(blockType.Id, "Version A content")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create Version B
        var versionB = await TestUpdatePageRequestBuilder
            .Create()
            .WithSlug("test-page")
            .WithLatestVersion(versionA.PageDetails.LatestPageVersionId!.Value)
            .WithSingleNewTextBlock(blockType.Id, "Version B content")
            .UpdateAsync(client, versionA.Id, TestContext.Current.CancellationToken);

        // Act - Set Version B as current
        await TestPageHelper.SetCurrentVersionAsync(
            client, versionB.PageDetails.PageVersionId, TestContext.Current.CancellationToken);

        // Assert - Verify GET /pages/by-version/{versionA} still returns Version A content
        var versionAContent = await TestPageHelper.GetPageVersionAsync(
            client, versionA.PageDetails.PageVersionId, TestContext.Current.CancellationToken);
        versionAContent.PageVersionId.Should().Be(versionA.PageDetails.PageVersionId);
        versionAContent.Blocks[0].BlockData.Should().BeJsonEqual(
            TestBlockTypeBuilder.GenerateTextBlockJson("Version A content"));

        // Verify GET /pages/{pageId} now returns Version B content
        var currentPageContent = await TestPageHelper.GetPageAsync(
            client, versionA.Id, TestContext.Current.CancellationToken);
        currentPageContent.PageVersionId.Should().Be(versionB.PageDetails.PageVersionId);
        currentPageContent.Blocks[0].BlockData.Should().BeJsonEqual(
            TestBlockTypeBuilder.GenerateTextBlockJson("Version B content"));
    }

    [Fact]
    public async Task SetCurrentPageVersion_ShouldWork_WhenSettingSameVersionAsCurrentTwice()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        var blockType = await TestBlockTypeBuilder
            .CreateTextBlock("text-content")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create Version A
        var versionA = await TestCreatePageRequestBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithSlug("test-page")
            .WithSingleTextBlock(blockType.Id, "Version A content")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        var versionB = await TestUpdatePageRequestBuilder
            .Create()
            .WithSlug("test-page")
            .WithLatestVersion(versionA.PageDetails.LatestPageVersionId!.Value)
            .WithSingleNewTextBlock(blockType.Id, "Version B content")
            .UpdateAsync(client, versionA.Id, TestContext.Current.CancellationToken);

        await TestPageHelper.SetCurrentVersionAsync(
            client, versionB.PageDetails.PageVersionId, TestContext.Current.CancellationToken);

        await TestPageHelper.SetCurrentVersionAsync(
            client, versionA.PageDetails.PageVersionId, TestContext.Current.CancellationToken);

        // Act - Set Version A as current again
        var result = await TestPageHelper.SetCurrentVersionAsync(
            client, versionA.PageDetails.PageVersionId, TestContext.Current.CancellationToken);

        // Assert - Should work without errors and return the same previous version ID
        result.Should().NotBeNull();
        result.PreviousPageVersionId.Should().Be(versionA.PageDetails.PageVersionId);

        // Verify Version A is still current
        var currentPageContent = await TestPageHelper.GetPageAsync(
            client, versionA.Id, TestContext.Current.CancellationToken);
        currentPageContent.PageVersionId.Should().Be(versionA.PageDetails.PageVersionId);
        currentPageContent.Blocks[0].BlockData.Should().BeJsonEqual(
            TestBlockTypeBuilder.GenerateTextBlockJson("Version A content"));
    }

    public ValueTask DisposeAsync() => fixture.TearDownTestAsync();
}
