namespace ConradSix.C4M.Tests.Endpoints.Page;

using System.Net;
using System.Net.Http.Json;

using AwesomeAssertions;

using ConradSix.C4M.Endpoints.Page;

using Microsoft.AspNetCore.WebUtilities;

[Collection(C4MAppCollection.Name)]
public sealed class GetPageByPathTests(C4MAppFixture fixture) : IAsyncDisposable
{
    [Fact]
    public async Task GetPageByPath_ShouldReturnNotFound_WhenPageDoesNotExist()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();
        var path = "/non-existent-page";

        // Act
        var url = QueryHelpers.AddQueryString("/pages/by-path", new Dictionary<string, string?>
        {
            ["realtorId"] = realtorId.ToString(),
            ["path"] = path,
        });

        var response = await client.GetAsync(url, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetPageByPath_ShouldReturnPage_WhenPageExists()
    {
        // Arrange
        var client = fixture.CreateClient();
        var realtorId = Guid.NewGuid();

        // Create a block type
        var blockType = await TestBlockTypeBuilder
            .CreateTextBlock("simple-text")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Create a page
        var createdPage = await TestCreatePageRequestBuilder
            .Create()
            .WithRealtorId(realtorId)
            .WithSlug("test-page")
            .WithSingleTextBlock(blockType.Id, "Hello World")
            .CreateAsync(client, TestContext.Current.CancellationToken);

        // Act
        var url = QueryHelpers.AddQueryString("/pages/by-path", new Dictionary<string, string?>
        {
            ["realtorId"] = realtorId.ToString(),
            ["path"] = createdPage.PageDetails.Path,
        });

        var response = await client.GetAsync(url, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<GetPageResponse>(TestContext.Current.CancellationToken);
        result.Should().NotBeNull();
        result.PageVersionId.Should().Be(createdPage.PageDetails.PageVersionId);
        result.Slug.Should().Be("test-page");
        result.Path.Should().Be(createdPage.PageDetails.Path);
        result.Blocks.Should().HaveCount(1);
        result.Blocks[0].BlockData.Should().Contain("Hello World");
    }

    public ValueTask DisposeAsync() => fixture.TearDownTestAsync();
}
