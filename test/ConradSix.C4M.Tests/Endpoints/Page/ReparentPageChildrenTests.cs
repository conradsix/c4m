namespace ConradSix.C4M.Tests.Endpoints.Page;

using System.Net;
using System.Net.Http.Json;

using AwesomeAssertions;

using ConradSix.C4M.Data;
using ConradSix.C4M.Data.Model;
using ConradSix.C4M.Endpoints.Page;
using ConradSix.C4M.Testing;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

[Collection(C4MAppCollection.Name)]
public sealed class ReparentPageChildrenTests(C4MAppFixture fixture) : IAsyncDisposable
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reparent_MovesAllActiveChildrenWithSubtrees_PreservingVersionsAndPublication(bool topLevel)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var scope = fixture.Services.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        var source = await PageArchivingTests.CreatePageAsync(ctx, "source", published: true);
        var destination = topLevel ? null : await PageArchivingTests.CreatePageAsync(ctx, "destination", published: true);
        var child = await PageArchivingTests.CreatePageAsync(ctx, "child", source.PageId, published: true);
        var grandchild = await PageArchivingTests.CreatePageAsync(ctx, "grandchild", child.PageId, published: true);
        var sibling = await PageArchivingTests.CreatePageAsync(ctx, "sibling", source.PageId);
        var archivedChild = await PageArchivingTests.CreatePageAsync(ctx, "archived", source.PageId);
        var archivedGrandchild = await PageArchivingTests.CreatePageAsync(ctx, "archived", child.PageId);
        var oldMenu = new MenuEntity { Name = "Old navigation" };
        var newMenu = new MenuEntity { Name = "New navigation" };
        ctx.Menus.AddRange(oldMenu, newMenu);
        source.Page.NavigationMenu = oldMenu;
        destination?.Page.NavigationMenu = newMenu;

        await ctx.SaveChangesAsync(ct);
        using var client = fixture.CreateClient();
        (await client.PostAsync($"/pages/{archivedChild.PageId}/archive", null, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.PostAsync($"/pages/{archivedGrandchild.PageId}/archive", null, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var before = await client.GetFromJsonAsync<GetPageResponse>($"/pages/{grandchild.PageId}", ct);
        before!.EffectiveNavigationMenuId.Should().Be(oldMenu.Id);
        fixture.Changes.Clear();
        var response = await client.PostAsJsonAsync($"/pages/{source.PageId}/reparent-children", new ReparentPageChildrenRequest(destination?.PageId), ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<ReparentPageChildrenResponse>(ct))!.MovedPages.Should().Be(2);
        var prefix = topLevel ? string.Empty : "/destination";
        var after = await client.GetFromJsonAsync<GetPageResponse>($"/pages/{grandchild.PageId}?includeBreadcrumbs=true", ct);
        after!.Path.Should().Be(prefix + "/child/grandchild");
        after.ParentId.Should().Be(child.PageId);
        after.PageVersionId.Should().Be(grandchild.Id);
        after.IsPublished.Should().BeTrue();
        after.EffectiveNavigationMenuId.Should().Be(topLevel ? null : newMenu.Id);
        after.Breadcrumbs!.Select(item => item.Path).Should().NotContain("/source");
        var all = await ctx.Pages.AsNoTracking().ToDictionaryAsync(page => page.Id, ct);
        all[child.PageId].ParentId.Should().Be(destination?.PageId);
        all[sibling.PageId].Path.Should().Be(prefix + "/sibling");
        all[archivedChild.PageId].ParentId.Should().Be(source.PageId);
        all[archivedChild.PageId].Path.Should().Be("/source/archived");
        all[archivedGrandchild.PageId].Path.Should().Be(prefix + "/child/archived");
        all[source.PageId].Path.Should().Be("/source");
        (await ctx.PageVersions.CountAsync(ct)).Should().Be(all.Count);
        var children = await client.GetFromJsonAsync<ListPagesResponse>($"/pages?parentId={source.PageId}", ct);
        children!.Total.Should().Be(0);
        fixture.Changes.Changes.Should().Contain(CmsChange.Content);
        (await client.PostAsJsonAsync($"/pages/{source.PageId}/unpublish", new PagePublicationRequest(source.Id, true), ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.PostAsync($"/pages/{source.PageId}/archive", null, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Theory]
    [InlineData("self", "invalid-parent")]
    [InlineData("descendant", "invalid-parent")]
    [InlineData("missing", "invalid-parent")]
    [InlineData("archived", "invalid-parent")]
    [InlineData("collision", "path-conflict")]
    [InlineData("reserved", "reserved-path")]
    [InlineData("unpublished", "unpublished-ancestors")]
    public async Task Reparent_RejectsInvalidDestinationWithoutMovingAnyChildren(string scenario, string code)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var scope = fixture.Services.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        var source = await PageArchivingTests.CreatePageAsync(ctx, "source", published: true);
        var child = await PageArchivingTests.CreatePageAsync(ctx, scenario == "reserved" ? "reserved" : "child", source.PageId);
        var grandchild = await PageArchivingTests.CreatePageAsync(ctx, "grandchild", child.PageId, published: true);
        var sibling = await PageArchivingTests.CreatePageAsync(ctx, "sibling", source.PageId);
        var destination = await PageArchivingTests.CreatePageAsync(ctx, "destination", published: scenario != "unpublished");
        Guid? targetId = destination.PageId;
        using var client = fixture.CreateClient();
        switch (scenario)
        {
            case "self": targetId = source.PageId; break;
            case "descendant": targetId = grandchild.PageId; break;
            case "missing": targetId = Guid.NewGuid(); break;
            case "reserved": targetId = null; break;
            case "archived":
                destination.Page.IsPublished = false;
                await ctx.SaveChangesAsync(ct);
                (await client.PostAsync($"/pages/{targetId}/archive", null, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
                break;
            case "collision": await PageArchivingTests.CreatePageAsync(ctx, "child", targetId); break;
        }

        fixture.Changes.Clear();
        var response = await client.PostAsJsonAsync($"/pages/{source.PageId}/reparent-children", new ReparentPageChildrenRequest(targetId), ct);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadFromJsonAsync<PageLifecycleConflict>(ct))!.Code.Should().Be(code);
        var all = await ctx.Pages.AsNoTracking().ToDictionaryAsync(page => page.Id, ct);
        all[child.PageId].ParentId.Should().Be(source.PageId);
        all[sibling.PageId].ParentId.Should().Be(source.PageId);
        all[grandchild.PageId].Path.Should().Be($"/source/{child.Page.Slug}/grandchild");
        fixture.Changes.Changes.Should().NotContain(CmsChange.Content);
    }

    [Fact]
    public async Task Reparent_AllowsArchivedPathCollision_AndOnlyMovesChildrenOfTheSpecifiedParent()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var scope = fixture.Services.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        var oldSource = await PageArchivingTests.CreatePageAsync(ctx, "source");
        var oldChild = await PageArchivingTests.CreatePageAsync(ctx, "old", oldSource.PageId);
        var oldDestination = await PageArchivingTests.CreatePageAsync(ctx, "child");
        using var client = fixture.CreateClient();
        foreach (var id in new[] { oldChild.PageId, oldSource.PageId, oldDestination.PageId })
        {
            (await client.PostAsync($"/pages/{id}/archive", null, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        var source = await PageArchivingTests.CreatePageAsync(ctx, "source");
        var child = await PageArchivingTests.CreatePageAsync(ctx, "child", source.PageId);
        fixture.Changes.Clear();
        var response = await client.PostAsJsonAsync($"/pages/{source.PageId}/reparent-children", new ReparentPageChildrenRequest(null), ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<ReparentPageChildrenResponse>(ct))!.MovedPages.Should().Be(1);
        var all = await ctx.Pages.AsNoTracking().ToDictionaryAsync(page => page.Id, ct);
        all[oldChild.PageId].ParentId.Should().Be(oldSource.PageId);
        all[child.PageId].Path.Should().Be("/child");
        (await client.PostAsJsonAsync($"/pages/{oldSource.PageId}/reparent-children", new ReparentPageChildrenRequest(null), ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    public ValueTask DisposeAsync() => fixture.TearDownTestAsync();
}
