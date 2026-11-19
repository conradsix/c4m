namespace ConradSix.C4M.Tests.Endpoints;

using System.Net;
using System.Net.Http.Json;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

[Collection(C4MAppCollection.Name)]
public sealed class PublishedAncestorsTests(C4MAppFixture fixture) : IAsyncLifetime
{
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync() => fixture.TearDownTestAsync();

    [Fact]
    public async Task PublishDescendant_OptionalAncestors_PreservesIndependentPublication()
    {
        using var client = fixture.CreateClient();
        var ct = TestContext.Current.CancellationToken;
        var options = fixture.Services.GetRequiredService<IOptions<C4MOptions>>().Value;
        var original = options.RequirePublishedAncestors;
        options.RequirePublishedAncestors = false;
        try
        {
            var parent = await fixture.Data.AddPageAsync("parent", withVersion: true, cancellationToken: ct);
            var child = await fixture.Data.AddPageAsync("child", parentId: parent.Id, withVersion: true, cancellationToken: ct);

            using var publish = await client.PostAsJsonAsync(
                $"/pages/{child.Id}/publish",
                new
                {
                    ExpectedIsPublished = false,
                    ExpectedCurrentPageVersionId = child.CurrentVersionId,
                },
                ct);
            publish.EnsureSuccessStatusCode();

            using var publicParent = await client.GetAsync("/pages/by-path?path=/parent", ct);
            publicParent.StatusCode.Should().Be(HttpStatusCode.NotFound);
            using var publicChild = await client.GetAsync("/pages/by-path?path=/parent/child", ct);
            publicChild.EnsureSuccessStatusCode();

            using var publishedParent = await client.PostAsJsonAsync(
                $"/pages/{parent.Id}/publish",
                new
                {
                    ExpectedIsPublished = false,
                    ExpectedCurrentPageVersionId = parent.CurrentVersionId,
                },
                ct);
            publishedParent.EnsureSuccessStatusCode();

            using var unpublish = await client.PostAsJsonAsync(
                $"/pages/{parent.Id}/unpublish",
                new
                {
                    ExpectedIsPublished = true,
                    ExpectedCurrentPageVersionId = parent.CurrentVersionId,
                },
                ct);
            unpublish.EnsureSuccessStatusCode();
        }
        finally
        {
            options.RequirePublishedAncestors = original;
        }
    }
}
