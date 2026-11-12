namespace ConradSix.C4M.Tests.Endpoints.Page;

using System.Net;
using System.Net.Http.Json;

using AwesomeAssertions;

using ConradSix.C4M.Endpoints.Page;

[Collection(C4MAppCollection.Name)]
public sealed class ListPublishedPagesTests(C4MAppFixture fixture) : IAsyncDisposable
{
    [Fact]
    public async Task ListPublishedPages_ShouldReturnEachPublishedPagesCurrentVersionInPathOrder()
    {
        using var client = fixture.CreateClient();
        var ct = TestContext.Current.CancellationToken;
        var parent = await TestCreatePageRequestBuilder.Create()
            .WithSlug("bravo")
            .WithSeo(new("Bravo homes", "Page description", null))
            .CreateAsync(client, ct);
        var child = await TestCreatePageRequestBuilder.Create()
            .WithSlug("condos")
            .WithParentId(parent.Id)
            .WithSeo(new("Bravo condos", "Page description", null, Index: false))
            .CreateAsync(client, ct);
        var first = await TestCreatePageRequestBuilder.Create()
            .WithSlug("alpha")
            .WithSeo(new("Alpha homes", "Page description", null))
            .CreateAsync(client, ct);
        var unpublished = await TestCreatePageRequestBuilder.Create()
            .WithSlug("charlie")
            .CreateAsync(client, ct);
        (await client.PostAsJsonAsync(
            $"/pages/{unpublished.Id}/unpublish",
            new PagePublicationRequest(unpublished.PageDetails.PageVersionId, true),
            ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await client.GetFromJsonAsync<ListPublishedPagesResponse>("/pages/published", ct);

        response!.Pages.Select(page => (page.PageId, page.ParentPageId, page.Path, page.SeoTitle, page.RobotsIndex)).Should().Equal(
            (first.Id, null, "/alpha", "Alpha homes", true),
            (parent.Id, null, "/bravo", "Bravo homes", true),
            (child.Id, parent.Id, "/bravo/condos", "Bravo condos", false));
        var served = await client.GetFromJsonAsync<GetPageResponse>("/pages/by-path?path=/bravo", ct);
        response.Pages[1].UpdatedAt.Should().Be(served!.UpdatedAt);
    }

    public ValueTask DisposeAsync() => fixture.TearDownTestAsync();
}
