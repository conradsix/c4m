#pragma warning disable CA2025 // Concurrent requests are awaited after committing the transaction that deliberately blocks them.
namespace ConradSix.C4M.Tests.Endpoints.Page;

using System.Net;
using System.Net.Http.Json;

using AwesomeAssertions;

using ConradSix.C4M;
using ConradSix.C4M.Data;
using ConradSix.C4M.Data.Model;
using ConradSix.C4M.Endpoints.Page;
using ConradSix.C4M.Services;
using ConradSix.C4M.Testing;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

[Collection(C4MAppCollection.Name)]
public sealed class PageArchivingTests(C4MAppFixture fixture) : IAsyncDisposable
{
    [Fact]
    public async Task Archive_RetainsContentAndReferences_ButHidesAllReadsAndRejectsWrites()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var scope = fixture.Services.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        var page = await CreatePageAsync(ctx, "archived");
        var source = await CreatePageAsync(ctx, "source");
        var type = new BlockTypeEntity { Name = "text", JsonSchema = "{}" };
        ctx.BlockTypes.Add(type);
        await ctx.SaveChangesAsync(ct);
        var versions = scope.ServiceProvider.GetRequiredService<IPageVersionServices>();
        await versions.CreatePageVersionAsync(source.PageId, [new(null, type.Id, $$"""{"text":"[Old](page-ref:{{page.PageId}})"}""")], ct);
        await versions.CreatePageVersionAsync(page.PageId, [new(null, type.Id, /*lang=json,strict*/ "{\"text\":\"Retained\"}")], ct);
        using var client = fixture.CreateClient();
        (await client.PostAsync($"/pages/{page.PageId}/archive", null, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.PostAsync($"/pages/{page.PageId}/archive", null, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        foreach (var url in new[]
        {
            $"/pages/{page.PageId}", $"/pages/{page.PageId}?latest=true", $"/pages/by-version/{page.Id}",
            $"/pages/{page.PageId}/history", $"/pages/{page.PageId}/path", "/pages/by-path?path=/archived",
        })
        {
            (await client.GetAsync(url, ct)).StatusCode.Should().Be(HttpStatusCode.NotFound, url);
        }

        (await client.PutAsJsonAsync($"/pages/{page.PageId}", new UpdatePageRequest(page.Id, [], []), ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.PutAsJsonAsync($"/pages/{page.PageId}/details", new UpdatePageDetailsRequest(null, "changed", page.Page.Version), ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.PutAsJsonAsync($"/pages/by-version/{page.Id}", new PagePublicationRequest(page.Id, false), ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.PostAsJsonAsync($"/pages/{page.PageId}/publish", new PagePublicationRequest(page.Id, false), ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.PostAsync($"/pages/{Guid.NewGuid()}/archive", null, ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        foreach (var query in new[] { "?limit=1", "?search=archived", "?pathFilter=archived", "?slugFilter=archived" })
        {
            var list = await client.GetFromJsonAsync<ListPagesResponse>("/pages" + query, ct);
            list!.Pages.Should().NotContain(item => item.PageId == page.PageId);
            list.Total.Should().Be(query == "?limit=1" ? 1 : 0);
            list.NextOffset.Should().BeNull();
        }

        await ctx.Entry(page.Page).ReloadAsync(ct);
        page.Page.ArchivedAt.Should().NotBeNull();
        (await ctx.PageVersions.CountAsync(version => version.PageId == page.PageId, ct)).Should().Be(2);
        (await ctx.PageBlocks.CountAsync(ct)).Should().Be(2);
        (await ctx.PageBlockReferences.SingleAsync(reference => reference.Kind == CmsReferenceKinds.Page, ct)).TargetId.Should().Be(page.PageId);
        fixture.Changes.Changes.Should().Contain(CmsChange.Content);

        var replacement = await CreatePageAsync(ctx, "archived", published: true);
        var latest = await client.GetFromJsonAsync<GetPageResponse>($"/pages/{source.PageId}?latest=true", ct);
        latest!.Blocks.Single().Pages.Single().Id.Should().Be(page.PageId);
        latest.Blocks.Single().Pages.Single().Id.Should().NotBe(replacement.PageId);
        (await versions.ValidateForPublicationAsync(latest.PageVersionId, ct)).Should().Contain(error => error.Contains("must be published first"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => versions.CreatePageVersionAsync(page.PageId, [], ct));
    }

    [Fact]
    public async Task Archive_BlocksPublishedPagesAndActiveChildren_ButAllowsArchivedChildren()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var scope = fixture.Services.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        var page = await CreatePageAsync(ctx, "parent", published: true);
        using var client = fixture.CreateClient();
        var published = await client.PostAsync($"/pages/{page.PageId}/archive", null, ct);
        published.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await published.Content.ReadFromJsonAsync<PageLifecycleConflict>(ct))!.Code.Should().Be("page-published");
        page.Page.IsPublished = false;
        await ctx.SaveChangesAsync(ct);
        var children = new List<PageVersionEntity>();
        for (var i = 0; i < 4; i++)
        {
            children.Add(await CreatePageAsync(ctx, $"child-{i}", page.PageId));
        }

        var blocked = await client.PostAsync($"/pages/{page.PageId}/archive", null, ct);
        blocked.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var conflict = await blocked.Content.ReadFromJsonAsync<PageLifecycleConflict>(ct);
        conflict!.Code.Should().Be("page-has-children");
        conflict.TotalPages.Should().Be(4);
        conflict.Pages.Should().HaveCount(3);
        foreach (var child in children)
        {
            (await client.PostAsync($"/pages/{child.PageId}/archive", null, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        (await client.PostAsync($"/pages/{page.PageId}/archive", null, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Archive_ReleasesAddress_ForNewPages(bool nested)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var scope = fixture.Services.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        var parent = nested ? await CreatePageAsync(ctx, "parent", published: true) : null;
        var page = await CreatePageAsync(ctx, "reusable", parent?.PageId);
        using var client = fixture.CreateClient();
        var path = page.Page.Path;
        var request = new CreatePageRequest(parent?.PageId, "reusable", [], new("Replacement", "Description", null), Publish: false);
        (await client.PostAsJsonAsync("/pages", request, ct)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        for (var i = 0; i < 2; i++)
        {
            (await client.PostAsync($"/pages/{page.PageId}/archive", null, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
            var availability = await client.GetFromJsonAsync<GetPagePathAvailabilityResponse>($"/pages/path-availability?slug=reusable" + (parent is null ? string.Empty : $"&parentId={parent.PageId}"), ct);
            availability!.Status.Should().Be(PagePathAvailability.Available);
            var created = await client.PostAsJsonAsync("/pages", request, ct);
            created.StatusCode.Should().Be(HttpStatusCode.Created);
            var id = (await created.Content.ReadFromJsonAsync<CreatePageResponse>(ct))!.Id;
            page = await ctx.PageVersions.Include(version => version.Page).SingleAsync(version => version.PageId == id, ct);
        }

        (await ctx.Pages.CountAsync(item => item.Path == path, ct)).Should().Be(3);
        (await ctx.Pages.CountAsync(item => item.Path == path && item.ArchivedAt == null, ct)).Should().Be(1);
        (await client.PostAsJsonAsync($"/pages/{page.PageId}/publish", new PagePublicationRequest(page.Id, false), ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetFromJsonAsync<GetPageResponse>("/pages/by-path?path=" + path, ct))!.PageId.Should().Be(page.PageId);
    }

    [Fact]
    public async Task ReusedAncestorPath_DoesNotBlockPublishingItsNewChildren()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var scope = fixture.Services.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        var old = await CreatePageAsync(ctx, "parent");
        using var client = fixture.CreateClient();
        (await client.PostAsync($"/pages/{old.PageId}/archive", null, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var replacement = await CreatePageAsync(ctx, "parent", published: true);
        var child = await CreatePageAsync(ctx, "child", replacement.PageId);
        (await client.PostAsJsonAsync($"/pages/{child.PageId}/publish", new PagePublicationRequest(child.Id, false), ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var request = new CreatePageRequest(old.PageId, "forbidden", [], null, Publish: false);
        (await client.PostAsJsonAsync("/pages", request, ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ctx.Entry(child.Page).ReloadAsync(ct);
        (await client.PutAsJsonAsync($"/pages/{child.PageId}/details", new UpdatePageDetailsRequest(old.PageId, "child", child.Page.Version), ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var availability = await client.GetFromJsonAsync<GetPagePathAvailabilityResponse>($"/pages/path-availability?slug=child&parentId={old.PageId}", ct);
        availability!.Status.Should().Be(PagePathAvailability.ParentNotFound);
    }

    [Theory]
    [InlineData("publish", HttpStatusCode.NotFound)]
    [InlineData("draft", HttpStatusCode.NotFound)]
    [InlineData("create", HttpStatusCode.BadRequest)]
    [InlineData("reparent", HttpStatusCode.Conflict)]
    public async Task Writes_RecheckArchiveStateAfterWaitingForLock(string operation, HttpStatusCode expected)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var scope = fixture.Services.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        var page = await CreatePageAsync(ctx, "target");
        var source = await CreatePageAsync(ctx, "source");
        await CreatePageAsync(ctx, "child", source.PageId);
        await using var transaction = await ctx.Database.BeginTransactionAsync(ct);
        await PagePublicationGuard.LockAsync(ctx, ct);
        await CmsLocks.PageAsync(ctx.Database, page.PageId, ct);
        using var client = fixture.CreateClient();
        var pending = operation switch
        {
            "publish" => client.PostAsJsonAsync($"/pages/{page.PageId}/publish", new PagePublicationRequest(page.Id, false), ct),
            "draft" => client.PutAsJsonAsync($"/pages/{page.PageId}", new UpdatePageRequest(page.Id, [], []), ct),
            "create" => client.PostAsJsonAsync("/pages", new CreatePageRequest(page.PageId, "child", [], null, Publish: false), ct),
            _ => client.PostAsJsonAsync($"/pages/{source.PageId}/reparent-children", new ReparentPageChildrenRequest(page.PageId), ct),
        };
        await WaitForLockAsync(ctx, ct);
        page.Page.ArchivedAt = DateTimeOffset.UtcNow;
        await ctx.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        (await pending).StatusCode.Should().Be(expected);
        (await ctx.PageVersions.CountAsync(version => version.PageId == page.PageId, ct)).Should().Be(1);
        (await ctx.Pages.CountAsync(item => item.ParentId == page.PageId, ct)).Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Archive_SeesChildrenOrPublicationCommittedWhileWaiting(bool publish)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var scope = fixture.Services.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        var page = await CreatePageAsync(ctx, "target");
        await using var transaction = await ctx.Database.BeginTransactionAsync(ct);
        await PagePublicationGuard.LockAsync(ctx, ct);
        using var client = fixture.CreateClient();
        var pending = client.PostAsync($"/pages/{page.PageId}/archive", null, ct);
        await WaitForLockAsync(ctx, ct);
        if (publish)
        {
            page.Page.IsPublished = true;
            await ctx.SaveChangesAsync(ct);
        }
        else
        {
            await CreatePageAsync(ctx, "child", page.PageId);
        }

        await transaction.CommitAsync(ct);
        (await pending).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ctx.Pages.AsNoTracking().SingleAsync(item => item.Id == page.PageId, ct)).ArchivedAt.Should().BeNull();
    }

    public ValueTask DisposeAsync() => fixture.TearDownTestAsync();

    internal static async Task<PageVersionEntity> CreatePageAsync(CmsEngineDbContext ctx, string slug, Guid? parentId = null, bool published = false)
    {
        var page = new PageEntity { Slug = slug, ParentId = parentId, IsPublished = published };
        var version = new PageVersionEntity { Page = page, IsCurrent = true, SeoTitle = slug, SeoDescription = "Description" };
        ctx.PageVersions.Add(version);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return version;
    }

    private static async Task WaitForLockAsync(CmsEngineDbContext ctx, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        while (!await ctx.Database.SqlQuery<bool>($"SELECT EXISTS (SELECT 1 FROM pg_locks WHERE locktype = 'advisory' AND NOT granted) AS \"Value\"").SingleAsync(timeout.Token))
        {
            await Task.Delay(20, timeout.Token);
        }
    }
}
