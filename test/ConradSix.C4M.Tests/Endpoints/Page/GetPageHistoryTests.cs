namespace ConradSix.C4M.Tests.Endpoints.Page;

using System.Net;

using AwesomeAssertions;

[Collection(C4MAppCollection.Name)]
public sealed class GetPageHistoryTests(C4MAppFixture fixture) : IAsyncDisposable
{
    [Fact]
    public async Task GetPageHistory_ShouldReturnSingleVersion_WhenPageHasOneVersion()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        var blockType = await TestBlockTypeBuilder
            .CreateTextBlock("text-content")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        var page = await TestCreatePageRequestBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithSlug("test-page")
            .WithSingleTextBlock(blockType.Id, "Initial content")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act
        var history = await TestPageHelper.GetPageHistoryAsync(
            client, page.Id, TestContext.Current.CancellationToken);

        // Assert
        history.Versions.Should().HaveCount(1);
        var version = history.Versions[0];
        version.PageVersionId.Should().Be(page.PageDetails.PageVersionId);
        version.IsCurrent.Should().BeTrue();
        version.ChangeSummary.Headline.Should().Be("Initial page version");
        version.ChangeSummary.Changes.Should().ContainSingle(change => change.Contains("Text content", StringComparison.Ordinal));
        version.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task GetPageHistory_ShouldReturnMultipleVersions_OrderedByCreatedAtDesc()
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

        // Create third version
        var version3 = await TestUpdatePageRequestBuilder
            .Create()
            .WithSlug("test-page")
            .WithLatestVersion(version2.PageDetails.LatestPageVersionId!.Value)
            .WithSingleNewTextBlock(blockType.Id, "Version 3 content")
            .UpdateAsync(client, version1.Id, TestContext.Current.CancellationToken);

        // Act
        var history = await TestPageHelper.GetPageHistoryAsync(
            client, version1.Id, TestContext.Current.CancellationToken);

        // Assert
        history.Versions.Should().HaveCount(3);

        // Should be ordered by CreatedAt descending (newest first)
        history.Versions[0].PageVersionId.Should().Be(version3.PageDetails.PageVersionId);
        history.Versions[1].PageVersionId.Should().Be(version2.PageDetails.PageVersionId);
        history.Versions[2].PageVersionId.Should().Be(version1.PageDetails.PageVersionId);

        // Only the first version should be current (since we haven't changed current)
        history.Versions[0].IsCurrent.Should().BeFalse();
        history.Versions[1].IsCurrent.Should().BeFalse();
        history.Versions[2].IsCurrent.Should().BeTrue();

        history.Versions.Should().AllSatisfy(version =>
            version.ChangeSummary.Headline.Should().NotBe("Summary not generated"));

        // Timestamps should be in descending order
        history.Versions[0].CreatedAt.Should().BeAfter(history.Versions[1].CreatedAt);
        history.Versions[1].CreatedAt.Should().BeAfter(history.Versions[2].CreatedAt);
    }

    [Fact]
    public async Task GetPageHistory_ShouldShowCorrectCurrentVersion_WhenCurrentVersionIsChanged()
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

        // Set version 2 as current
        await TestPageHelper.SetCurrentVersionAsync(
            client, version2.PageDetails.PageVersionId, TestContext.Current.CancellationToken);

        // Act
        var history = await TestPageHelper.GetPageHistoryAsync(
            client, version1.Id, TestContext.Current.CancellationToken);

        // Assert
        history.Versions.Should().HaveCount(2);

        var currentVersion = history.Versions.Single(v => v.IsCurrent);
        var nonCurrentVersion = history.Versions.Single(v => !v.IsCurrent);

        currentVersion.PageVersionId.Should().Be(version2.PageDetails.PageVersionId);
        nonCurrentVersion.PageVersionId.Should().Be(version1.PageDetails.PageVersionId);
    }

    [Fact]
    public async Task GetPageHistory_ShouldDescribeAddedBlocks_WhenVersionsHaveDifferentBlocks()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        var blockType = await TestBlockTypeBuilder
            .CreateTextBlock("text-content")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create initial page with 1 block
        var version1 = await TestCreatePageRequestBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithSlug("test-page")
            .WithSingleTextBlock(blockType.Id, "Version 1 content")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create second version with 2 blocks
        var version2 = await TestUpdatePageRequestBuilder
            .Create()
            .WithSlug("test-page")
            .WithLatestVersion(version1.PageDetails.LatestPageVersionId!.Value)
            .AddNewBlock(blockType.Id, 0, TestBlockTypeBuilder.GenerateTextBlockJson("Block 1 content"))
            .AddNewBlock(blockType.Id, 1, TestBlockTypeBuilder.GenerateTextBlockJson("Block 2 content"))
            .UpdateAsync(client, version1.Id, TestContext.Current.CancellationToken);

        // Act
        var history = await TestPageHelper.GetPageHistoryAsync(
            client, version1.Id, TestContext.Current.CancellationToken);

        // Assert
        history.Versions.Should().HaveCount(2);

        var version2History = history.Versions.Single(v => v.PageVersionId == version2.PageDetails.PageVersionId);

        version2History.ChangeSummary.Changes.Should().Contain(change =>
            change.StartsWith("Added Text content", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetPageHistory_ShouldReturn404_WhenPageDoesNotExist()
    {
        // Arrange
        var client = fixture.CreateClient();
        var nonExistentPageId = Guid.NewGuid();

        // Act & Assert
        await TestPageHelper.GetPageHistoryExpectingAsync(
            client, nonExistentPageId, HttpStatusCode.NotFound, TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync() => fixture.TearDownTestAsync();
}
