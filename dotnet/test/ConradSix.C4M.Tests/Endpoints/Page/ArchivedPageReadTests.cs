namespace ConradSix.C4M.Tests.Endpoints.Page;

using System.Net;
using System.Net.Http.Json;

using AwesomeAssertions;

using ConradSix.C4M.Data;
using ConradSix.C4M.Data.Model;
using ConradSix.C4M.Endpoints.Page;
using ConradSix.C4M.Services;
using ConradSix.C4M.Services.Model;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

[Collection(C4MAppCollection.Name)]
public sealed class ArchivedPageReadTests(C4MAppFixture fixture) : IAsyncDisposable
{
    [Theory]
    [InlineData(ListPagesStatus.NotArchived, 2)]
    [InlineData(ListPagesStatus.Published, 1)]
    [InlineData(ListPagesStatus.Unpublished, 1)]
    [InlineData(ListPagesStatus.Archived, 2)]
    [InlineData(ListPagesStatus.All, 4)]
    public async Task StatusFilters_CombineWithSearchParentAndPagination(ListPagesStatus status, int expected)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var scope = fixture.Services.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        var parent = await PageArchivingTests.CreatePageAsync(ctx, "parent");
        await PageArchivingTests.CreatePageAsync(ctx, "live", parent.PageId, published: true);
        await PageArchivingTests.CreatePageAsync(ctx, "draft", parent.PageId);
        var archived = await PageArchivingTests.CreatePageAsync(ctx, "old", parent.PageId);
        archived.Page.ArchivedAt = DateTimeOffset.UtcNow;
        var empty = new PageEntity { Slug = "empty", ParentId = parent.PageId, ArchivedAt = DateTimeOffset.UtcNow };
        ctx.Pages.Add(empty);
        await ctx.SaveChangesAsync(ct);
        using var client = fixture.CreateClient();
        var query = $"/pages?status={status}&parentId={parent.PageId}&pathFilter=parent&search=parent&limit=1&sort=StatusAsc";
        var first = (await client.GetFromJsonAsync<ListPagesResponse>(query, ct))!;
        first.Total.Should().Be(expected);
        first.Pages.Should().HaveCount(1);
        first.NextOffset.Should().Be(expected > 1 ? 1 : null);
        var ids = new HashSet<Guid>(first.Pages.Select(page => page.PageId));
        for (var offset = 1; offset < expected; offset++)
        {
            var next = (await client.GetFromJsonAsync<ListPagesResponse>($"{query}&offset={offset}", ct))!;
            next.Total.Should().Be(expected);
            ids.Add(next.Pages.Single().PageId).Should().BeTrue();
        }

        var all = (await client.GetFromJsonAsync<ListPagesResponse>($"/pages?status={status}&parentId={parent.PageId}", ct))!;
        all.Pages.Should().OnlyContain(page => status == ListPagesStatus.All ||
            (status == ListPagesStatus.Archived ? page.ArchivedAt.HasValue : !page.ArchivedAt.HasValue));
    }

    [Fact]
    public async Task StatusSorts_AreStable_AndDefaultConsumersExcludeArchives()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var scope = fixture.Services.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        var archived = await PageArchivingTests.CreatePageAsync(ctx, "reused");
        archived.Page.ArchivedAt = DateTimeOffset.UtcNow;
        await ctx.SaveChangesAsync(ct);
        var published = await PageArchivingTests.CreatePageAsync(ctx, "reused", published: true);
        var unpublished = await PageArchivingTests.CreatePageAsync(ctx, "draft");
        var secondArchive = await PageArchivingTests.CreatePageAsync(ctx, "old");
        secondArchive.Page.ArchivedAt = DateTimeOffset.UtcNow;
        await ctx.SaveChangesAsync(ct);
        using var client = fixture.CreateClient();
        var asc = (await client.GetFromJsonAsync<ListPagesResponse>("/pages?status=All&sort=StatusAsc", ct))!;
        asc.Pages.Select(page => page.PageId).Should().Equal(published.PageId, unpublished.PageId, archived.PageId, secondArchive.PageId);
        var desc = (await client.GetFromJsonAsync<ListPagesResponse>("/pages?status=All&sort=StatusDesc", ct))!;
        desc.Pages.Select(page => page.PageId).Should().Equal(archived.PageId, secondArchive.PageId, unpublished.PageId, published.PageId);
        var normal = (await client.GetFromJsonAsync<ListPagesResponse>("/pages", ct))!;
        normal.Total.Should().Be(2);
        normal.Pages.Should().OnlyContain(page => page.ArchivedAt == null);
        var menu = (await client.GetFromJsonAsync<ListPagesResponse>("/pages?status=All&publishedOnly=true", ct))!;
        menu.Pages.Should().ContainSingle().Which.PageId.Should().Be(published.PageId);
        (await client.GetFromJsonAsync<ListPagesResponse>("/pages?status=Archived&publishedOnly=true", ct))!.Total.Should().Be(0);
        (await client.GetAsync("/pages?status=999", ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ArchivedReads_RequireOptIn_RetainHistory_AndNeverExposePublicContentOrEnableWrites()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var scope = fixture.Services.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        var page = await PageArchivingTests.CreatePageAsync(ctx, "archive");
        var versions = scope.ServiceProvider.GetRequiredService<IPageVersionServices>();
        var latestId = await versions.CreatePageVersionAsync(page.PageId, [], new PageVersionMetadata("Newest title", "Description", null, true, true), ct);
        using var client = fixture.CreateClient();
        (await client.PostAsync($"/pages/{page.PageId}/archive", null, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        foreach (var path in new[]
        {
            $"/pages/{page.PageId}?latest=true", $"/pages/by-version/{page.Id}", $"/pages/{page.PageId}/history",
            $"/pages/{page.PageId}?includeArchived=true", "/pages/by-path?path=/archive&includeArchived=true",
            $"/pages/{page.PageId}/path?includeArchived=true",
        })
        {
            (await client.GetAsync(path, ct)).StatusCode.Should().Be(HttpStatusCode.NotFound, path);
        }

        var latest = (await client.GetFromJsonAsync<GetPageResponse>($"/pages/{page.PageId}?latest=true&includeArchived=true", ct))!;
        latest.PageVersionId.Should().Be(latestId);
        latest.Seo.Title.Should().Be("Newest title");
        latest.ArchivedAt.Should().NotBeNull();
        var old = (await client.GetFromJsonAsync<GetPageResponse>($"/pages/by-version/{page.Id}?includeArchived=true&includeBreadcrumbs=true", ct))!;
        old.PageId.Should().Be(page.PageId);
        old.PageVersionId.Should().Be(page.Id);
        old.ArchivedAt.Should().Be(latest.ArchivedAt);
        old.Breadcrumbs!.Should().ContainSingle().Which.PageId.Should().Be(page.PageId);
        var history = (await client.GetFromJsonAsync<GetPageHistoryResponse>($"/pages/{page.PageId}/history?includeArchived=true", ct))!;
        history.Versions.Select(version => version.PageVersionId).Should().Equal(latestId, page.Id);
        history.Page!.ArchivedAt.Should().Be(latest.ArchivedAt);
        history.Page.Path.Should().Be("/archive");
        (await client.PutAsJsonAsync($"/pages/{page.PageId}?includeArchived=true", new UpdatePageRequest(latestId, [], []), ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.PostAsJsonAsync($"/pages/{page.PageId}/publish?includeArchived=true", new PagePublicationRequest(page.Id, false), ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.PutAsJsonAsync($"/pages/by-version/{latestId}?includeArchived=true", new PagePublicationRequest(page.Id, false), ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ctx.PageVersions.CountAsync(version => version.PageId == page.PageId, ct)).Should().Be(2);
        (await client.GetAsync($"/pages/by-version/{Guid.NewGuid()}?includeArchived=true", ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task HistoryMetadata_AllowsVersionlessArchives_AndUnarchivedPagesRemainReadable()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var scope = fixture.Services.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        var menu = new MenuEntity { Name = "Archived navigation" };
        var empty = new PageEntity { Slug = "empty", ArchivedAt = DateTimeOffset.UtcNow, NavigationMenu = menu };
        ctx.Pages.Add(empty);
        await ctx.SaveChangesAsync(ct);
        var live = await PageArchivingTests.CreatePageAsync(ctx, "live", published: true);
        using var client = fixture.CreateClient();
        var history = (await client.GetFromJsonAsync<GetPageHistoryResponse>($"/pages/{empty.Id}/history?includeArchived=true", ct))!;
        history.Versions.Should().BeEmpty();
        history.Page!.PageId.Should().Be(empty.Id);
        history.Page.Slug.Should().Be("empty");
        history.Page.NavigationMenuName.Should().Be(menu.Name);
        var list = (await client.GetFromJsonAsync<ListPagesResponse>("/pages?status=Archived&sort=UpdatedAtDesc&updatedAfter=2000-01-01T00:00:00Z", ct))!;
        list.Pages.Should().ContainSingle().Which.PageId.Should().Be(empty.Id);
        list.Pages[0].UpdatedAt.Should().Be(history.Page.CreatedAt);
        (await client.GetFromJsonAsync<GetPageResponse>($"/pages/{live.PageId}?includeArchived=true", ct))!.PageVersionId.Should().Be(live.Id);
        (await client.GetFromJsonAsync<GetPageHistoryResponse>($"/pages/{live.PageId}/history", ct))!.Page!.ArchivedAt.Should().BeNull();
        (await client.GetAsync($"/pages/{Guid.NewGuid()}/history?includeArchived=true", ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    public ValueTask DisposeAsync() => fixture.TearDownTestAsync();
}
