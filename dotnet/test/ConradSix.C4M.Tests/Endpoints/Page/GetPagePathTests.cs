namespace ConradSix.C4M.Tests.Endpoints.Page;

using System.Net;

using AwesomeAssertions;

[Collection(C4MAppCollection.Name)]
public sealed class GetPagePathTests(C4MAppFixture fixture) : IAsyncDisposable
{
    [Fact]
    public async Task GetPagePath_ShouldReturnPagePath_WhenPageExists()
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
            .WithSingleTextBlock(blockType.Id, "Test content")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act
        var response = await TestPageHelper.GetPagePathAsync(
            client, page.Id, TestContext.Current.CancellationToken);

        // Assert
        response.Path.Should().Be(page.PageDetails.Path);
    }

    [Fact]
    public async Task GetPagePath_ShouldReturnNotFound_WhenPageDoesNotExist()
    {
        // Arrange
        var client = fixture.CreateClient();
        var nonExistentPageId = Guid.NewGuid();

        // Act & Assert
        await TestPageHelper.GetPagePathExpectingAsync(
            client, nonExistentPageId, HttpStatusCode.NotFound, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetPagePath_ShouldReturnCorrectPath_ForNestedPage()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        var blockType = await TestBlockTypeBuilder
            .CreateTextBlock("text-content")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create parent page
        var parentPage = await TestCreatePageRequestBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithSlug("parent")
            .WithSingleTextBlock(blockType.Id, "Parent content")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create child page
        var childPage = await TestCreatePageRequestBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithParentId(parentPage.Id)
            .WithSlug("child")
            .WithSingleTextBlock(blockType.Id, "Child content")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act
        var response = await TestPageHelper.GetPagePathAsync(
            client, childPage.Id, TestContext.Current.CancellationToken);

        // Assert
        response.Path.Should().Be(childPage.PageDetails.Path);
        response.Path.Should().StartWith("/parent/");
        response.Path.Should().EndWith("/child");
    }

    public ValueTask DisposeAsync() => fixture.TearDownTestAsync();
}
