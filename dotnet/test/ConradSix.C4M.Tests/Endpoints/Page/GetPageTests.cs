namespace ConradSix.C4M.Tests.Endpoints.Page;

using AwesomeAssertions;

[Collection(C4MAppCollection.Name)]
public sealed class GetPageTests(C4MAppFixture fixture) : IAsyncDisposable
{
    [Fact]
    public async Task GetPage_ShouldReturnLatestVersion_WhenLatestParameterIsTrue()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        var blockType = await TestBlockTypeBuilder
            .CreateTextBlock("text-content")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create initial page (this will be the current version)
        var initialPage = await TestCreatePageRequestBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithSlug("test-page")
            .WithSingleTextBlock(blockType.Id, "Current version content")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create a second version (this will be the latest version but not current)
        var latestVersion = await TestUpdatePageRequestBuilder
            .Create()
            .WithSlug("test-page")
            .WithLatestVersion(initialPage.PageDetails.LatestPageVersionId!.Value)
            .WithSingleNewTextBlock(blockType.Id, "Latest version content")
            .UpdateAsync(client, initialPage.Id, TestContext.Current.CancellationToken);

        // At this point, latestVersion is the most recent but initialPage is still current

        // Act - Get page with Latest=true (should return the latest version)
        var pageWithLatest = await TestPageHelper.GetPageAsync(
            client, initialPage.Id, latest: true, TestContext.Current.CancellationToken);

        // Act - Get page with Latest=false (should return the current version)
        var pageWithCurrent = await TestPageHelper.GetPageAsync(
            client, initialPage.Id, latest: false, TestContext.Current.CancellationToken);

        // Assert - Latest=true should return the latest version
        pageWithLatest.PageVersionId.Should().Be(latestVersion.PageDetails.PageVersionId);
        pageWithLatest.Blocks[0].BlockData.Should().BeJsonEqual(
            TestBlockTypeBuilder.GenerateTextBlockJson("Latest version content"));

        // Assert - Latest=false should return the current version
        pageWithCurrent.PageVersionId.Should().Be(initialPage.PageDetails.PageVersionId);
        pageWithCurrent.Blocks[0].BlockData.Should().BeJsonEqual(
            TestBlockTypeBuilder.GenerateTextBlockJson("Current version content"));
    }

    [Fact]
    public async Task GetPage_ShouldReturnSameVersion_WhenLatestParameterIsTrueAndCurrentVersionIsLatest()
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

        // Set the second version as current (making it both current and latest)
        await TestPageHelper.SetCurrentVersionAsync(
            client, secondVersion.PageDetails.PageVersionId, TestContext.Current.CancellationToken);

        // Act - Get page with Latest=true
        var pageWithLatest = await TestPageHelper.GetPageAsync(
            client, initialPage.Id, latest: true, TestContext.Current.CancellationToken);

        // Act - Get page with Latest=false (default)
        var pageWithCurrent = await TestPageHelper.GetPageAsync(
            client, initialPage.Id, latest: false, TestContext.Current.CancellationToken);

        // Assert - Both should return the same version since current == latest
        pageWithLatest.PageVersionId.Should().Be(secondVersion.PageDetails.PageVersionId);
        pageWithCurrent.PageVersionId.Should().Be(secondVersion.PageDetails.PageVersionId);

        pageWithLatest.Blocks[0].BlockData.Should().BeJsonEqual(
            TestBlockTypeBuilder.GenerateTextBlockJson("Version 2 content"));
        pageWithCurrent.Blocks[0].BlockData.Should().BeJsonEqual(
            TestBlockTypeBuilder.GenerateTextBlockJson("Version 2 content"));
    }

    [Fact]
    public async Task GetPage_ShouldReturnLatestVersion_WhenMultipleVersionsExistAndLatestParameterIsTrue()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        var blockType = await TestBlockTypeBuilder
            .CreateTextBlock("text-content")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create initial page
        var version1 = await TestCreatePageRequestBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithSlug("test-page")
            .WithSingleTextBlock(blockType.Id, "Version 1 content")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create second version
        var version2 = await TestUpdatePageRequestBuilder
            .Create()
            .WithSlug("test-page")
            .WithLatestVersion(version1.PageDetails.LatestPageVersionId!.Value)
            .WithSingleNewTextBlock(blockType.Id, "Version 2 content")
            .UpdateAsync(client, version1.Id, TestContext.Current.CancellationToken);

        // Create third version (this will be the latest)
        var version3 = await TestUpdatePageRequestBuilder
            .Create()
            .WithSlug("test-page")
            .WithLatestVersion(version2.PageDetails.LatestPageVersionId!.Value)
            .WithSingleNewTextBlock(blockType.Id, "Version 3 content")
            .UpdateAsync(client, version1.Id, TestContext.Current.CancellationToken);

        // Set version 2 as current (so current != latest)
        await TestPageHelper.SetCurrentVersionAsync(
            client, version2.PageDetails.PageVersionId, TestContext.Current.CancellationToken);

        // Act - Get page with Latest=true
        var pageWithLatest = await TestPageHelper.GetPageAsync(
            client, version1.Id, latest: true, TestContext.Current.CancellationToken);

        // Act - Get page with Latest=false
        var pageWithCurrent = await TestPageHelper.GetPageAsync(
            client, version1.Id, latest: false, TestContext.Current.CancellationToken);

        // Assert - Latest=true should return version 3 (the latest)
        pageWithLatest.PageVersionId.Should().Be(version3.PageDetails.PageVersionId);
        pageWithLatest.Blocks[0].BlockData.Should().BeJsonEqual(
            TestBlockTypeBuilder.GenerateTextBlockJson("Version 3 content"));

        // Assert - Latest=false should return version 2 (the current)
        pageWithCurrent.PageVersionId.Should().Be(version2.PageDetails.PageVersionId);
        pageWithCurrent.Blocks[0].BlockData.Should().BeJsonEqual(
            TestBlockTypeBuilder.GenerateTextBlockJson("Version 2 content"));
    }

    public ValueTask DisposeAsync() => fixture.TearDownTestAsync();
}
