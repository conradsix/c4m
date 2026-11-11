#pragma warning disable CA2025 // Pending requests are awaited after committing the transaction blocking them.
namespace ConradSix.C4M.Tests.Endpoints.Menu;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using ConradSix.C4M;
using ConradSix.C4M.Data;
using ConradSix.C4M.Data.Model;
using ConradSix.C4M.Endpoints.Menu;
using ConradSix.C4M.Endpoints.Page;
using ConradSix.C4M.Model;
using ConradSix.C4M.Services;
using ConradSix.C4M.Tests.Endpoints.Page;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

[Collection(C4MAppCollection.Name)]
public sealed class MenuPublicationTests(C4MAppFixture fixture) : IAsyncDisposable
{
    [Theory]
    [InlineData("published", false)]
    [InlineData("published", true)]
    [InlineData("unpublished", false)]
    [InlineData("unpublished", true)]
    [InlineData("archived", false)]
    [InlineData("archived", true)]
    [InlineData("missing", false)]
    [InlineData("missing", true)]
    [InlineData("versionless", false)]
    [InlineData("versionless", true)]
    public async Task MenuSave_RequiresPublishedTargets_AndPreservesTheOldMenuOnFailure(string state, bool update)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var scope = fixture.Services.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        var page = await PageArchivingTests.CreatePageAsync(ctx, "target", published: state is "published" or "versionless");
        if (state == "versionless")
        {
            page.IsCurrent = null;
        }

        if (state == "archived")
        {
            page.Page.ArchivedAt = DateTimeOffset.UtcNow;
        }

        var action = await CreateActionAsync(ctx);
        var pageId = state == "missing" ? Guid.NewGuid() : page.PageId;
        using var client = fixture.CreateClient();
        var original = new MenuItem("Original", action.Id, "{}", null);
        var created = await client.PostAsJsonAsync("/menus", new CreateMenuRequest("original-menu", [original]), ct);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var menuId = (await created.Content.ReadFromJsonAsync<CreateMenuResponse>(ct))!.Id;
        var before = (await client.GetFromJsonAsync<GetMenuResponse>($"/menus/{menuId}", ct))!;
        var items = new[] { new MenuItem("Parent", action.Id, "{}", [Link(action.Id, pageId), Link(action.Id, pageId) with { Title = "Second" }]) };
        var response = update
            ? await client.PutAsJsonAsync($"/menus/{menuId}", new UpdateMenuRequest(menuId, before.Version, items), ct)
            : await client.PostAsJsonAsync("/menus", new CreateMenuRequest("new-menu", items), ct);
        if (state == "published")
        {
            response.StatusCode.Should().Be(update ? HttpStatusCode.NoContent : HttpStatusCode.Created);
            (await ctx.MenuItemPageReferences.CountAsync(ct)).Should().Be(2, "repeated references within each item's JSON are deduplicated");
            return;
        }

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadAsStringAsync(ct);
        error.Should().Contain("Parent").And.Contain("Destination");
        error.Should().Contain(state == "missing" ? pageId.ToString() : "/target");
        var after = (await client.GetFromJsonAsync<GetMenuResponse>($"/menus/{menuId}", ct))!;
        after.Version.Should().Be(before.Version);
        after.RootItems.Should().ContainSingle().Which.Title.Should().Be("Original");
        (await ctx.Menus.CountAsync(ct)).Should().Be(1);
    }

    [Fact]
    public async Task ExistingInvalidReferences_RemainEditable_ButNeverResolveToReusedAddresses()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var scope = fixture.Services.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        var archived = await PageArchivingTests.CreatePageAsync(ctx, "reused");
        archived.Page.ArchivedAt = DateTimeOffset.UtcNow;
        await ctx.SaveChangesAsync(ct);
        var replacement = await PageArchivingTests.CreatePageAsync(ctx, "reused", published: true);
        var unpublished = await PageArchivingTests.CreatePageAsync(ctx, "offline");
        var versionless = await PageArchivingTests.CreatePageAsync(ctx, "versionless", published: true);
        versionless.IsCurrent = null;
        var action = await CreateActionAsync(ctx);
        var menu = await CreateLegacyMenuAsync(ctx, action.Id, archived.PageId, "legacy-menu", replacement.PageId);
        var offlineItem = new MenuItemEntity { Menu = menu, Title = "Offline", ActionTypeId = action.Id, ActionData = Link(action.Id, unpublished.PageId).ActionData, SortOrder = 1 };
        var versionlessItem = new MenuItemEntity { Menu = menu, Title = "Versionless", ActionTypeId = action.Id, ActionData = Link(action.Id, versionless.PageId).ActionData, SortOrder = 2 };
        ctx.MenuItemPageReferences.AddRange(
            new MenuItemPageReferenceEntity { MenuItem = offlineItem, ReferencedPageId = unpublished.PageId },
            new MenuItemPageReferenceEntity { MenuItem = versionlessItem, ReferencedPageId = versionless.PageId });
        ctx.NavigationSettings.Add(new NavigationSettingsEntity { Id = NavigationSettingsEntity.SingletonId, DefaultMenuId = menu.Id });
        await ctx.SaveChangesAsync(ct);
        using var client = fixture.CreateClient();
        foreach (var url in new[] { $"/menus/{menu.Id}", "/menus/default" })
        {
            var result = (await client.GetFromJsonAsync<GetMenuResponse>(url, ct))!;
            result.RootItems.Should().HaveCount(3);
            result.RootItems[0].ActionData.Should().Contain(archived.PageId.ToString());
            result.RootItems[0].Children.Should().ContainSingle();
            result.Pages.Should().ContainSingle().Which.Id.Should().Be(replacement.PageId);
            result.Pages[0].Path.Should().Be("/reused");
        }

        var before = (await client.GetFromJsonAsync<GetMenuResponse>($"/menus/{menu.Id}", ct))!;
        (await client.PutAsJsonAsync($"/menus/{menu.Id}", new UpdateMenuRequest(menu.Id, before.Version, [Link(action.Id, archived.PageId)]), ct))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PutAsJsonAsync($"/menus/{menu.Id}", new UpdateMenuRequest(menu.Id, before.Version, [Link(action.Id, replacement.PageId)]), ct))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ctx.MenuItemPageReferences.ToArrayAsync(ct)).Should().ContainSingle().Which.ReferencedPageId.Should().Be(replacement.PageId);
    }

    [Fact]
    public async Task Archive_BlocksUnusedAndNestedMenus_DeduplicatesSamples_AndAllowsRemoval()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var scope = fixture.Services.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        var page = await PageArchivingTests.CreatePageAsync(ctx, "offline");
        var action = await CreateActionAsync(ctx);
        for (var i = 0; i < 5; i++)
        {
            await CreateLegacyMenuAsync(ctx, action.Id, page.PageId, $"menu-{i}", page.PageId);
        }

        using var client = fixture.CreateClient();
        var response = await client.PostAsync($"/pages/{page.PageId}/archive", null, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var conflict = (await response.Content.ReadFromJsonAsync<PageLifecycleConflict>(ct))!;
        conflict.Code.Should().Be("page-referenced");
        conflict.TotalMenus.Should().Be(5);
        conflict.Menus!.Select(menu => menu.Name).Should().Equal("menu-0", "menu-1", "menu-2");
        foreach (var id in await ctx.Menus.Select(menu => menu.Id).ToArrayAsync(ct))
        {
            var menu = (await client.GetFromJsonAsync<GetMenuResponse>($"/menus/{id}", ct))!;
            (await client.PutAsJsonAsync($"/menus/{id}", new UpdateMenuRequest(id, menu.Version, []), ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        (await client.PostAsync($"/pages/{page.PageId}/archive", null, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task PublishedOnly_FilterRunsBeforePagination_AndDoesNotChangeOtherPickers()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var scope = fixture.Services.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        var parent = await PageArchivingTests.CreatePageAsync(ctx, "parent", published: true);
        await PageArchivingTests.CreatePageAsync(ctx, "a-offline", parent.PageId);
        var archived = await PageArchivingTests.CreatePageAsync(ctx, "b-archived", parent.PageId);
        archived.Page.ArchivedAt = DateTimeOffset.UtcNow;
        var versionless = await PageArchivingTests.CreatePageAsync(ctx, "c-versionless", parent.PageId, published: true);
        versionless.IsCurrent = null;
        var live = await PageArchivingTests.CreatePageAsync(ctx, "d-published", parent.PageId, published: true);
        using var client = fixture.CreateClient();
        var query = $"/pages?parentId={parent.PageId}&limit=1&pathFilter=parent&search=parent";
        var filtered = (await client.GetFromJsonAsync<ListPagesResponse>(query + "&publishedOnly=true", ct))!;
        filtered.Total.Should().Be(1);
        filtered.NextOffset.Should().BeNull();
        filtered.Pages.Should().ContainSingle().Which.PageId.Should().Be(live.PageId);
        (await client.GetFromJsonAsync<ListPagesResponse>(query, ct))!.Total.Should().Be(3);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task MenuSave_RechecksTargetsAfterWaitingForLifecycleChanges(bool archive, bool update)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var scope = fixture.Services.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        var page = await PageArchivingTests.CreatePageAsync(ctx, "target", published: true);
        var action = await CreateActionAsync(ctx);
        var menu = new MenuEntity { Name = "existing-menu" };
        ctx.Menus.Add(menu);
        await ctx.SaveChangesAsync(ct);
        await using var transaction = await ctx.Database.BeginTransactionAsync(ct);
        await PagePublicationGuard.LockAsync(ctx, ct);
        using var client = fixture.CreateClient();
        var items = new[] { Link(action.Id, page.PageId) };
        var pending = update
            ? client.PutAsJsonAsync($"/menus/{menu.Id}", new UpdateMenuRequest(menu.Id, menu.Version, items), ct)
            : client.PostAsJsonAsync("/menus", new CreateMenuRequest("pending-menu", items), ct);
        await WaitForLockAsync(ctx, ct);
        page.Page.IsPublished = false;
        page.Page.ArchivedAt = archive ? DateTimeOffset.UtcNow : null;
        await ctx.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        (await pending).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ctx.MenuItemPageReferences.CountAsync(ct)).Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LifecycleChange_SeesMenuReferencesCommittedWhileWaiting(bool archive)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var scope = fixture.Services.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        var page = await PageArchivingTests.CreatePageAsync(ctx, "target", published: !archive);
        var action = await CreateActionAsync(ctx);
        await using var transaction = await ctx.Database.BeginTransactionAsync(ct);
        await PagePublicationGuard.LockAsync(ctx, ct);
        using var client = fixture.CreateClient();
        var pending = archive
            ? client.PostAsync($"/pages/{page.PageId}/archive", null, ct)
            : client.PostAsJsonAsync($"/pages/{page.PageId}/unpublish", new PagePublicationRequest(page.Id, true), ct);
        await WaitForLockAsync(ctx, ct);
        await CreateLegacyMenuAsync(ctx, action.Id, page.PageId, "concurrent-menu", page.PageId);
        await transaction.CommitAsync(ct);
        (await pending).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    public ValueTask DisposeAsync() => fixture.TearDownTestAsync();

    private static MenuItem Link(Guid actionId, Guid pageId) => new(
        "Destination",
        actionId,
        JsonSerializer.Serialize(new { pageRef = new { __typename = "page-ref", id = pageId }, repeated = new { __typename = "page-ref", id = pageId } }),
        null);

    private static async Task<MenuActionTypeEntity> CreateActionAsync(CmsEngineDbContext ctx)
    {
        var action = new MenuActionTypeEntity { Name = "page-link", JsonSchema = "{}" };
        ctx.MenuActionTypes.Add(action);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return action;
    }

    private static async Task<MenuEntity> CreateLegacyMenuAsync(CmsEngineDbContext ctx, Guid actionId, Guid pageId, string name, Guid childPageId)
    {
        var menu = new MenuEntity { Name = name };
        var root = new MenuItemEntity { Menu = menu, Title = "Root", ActionTypeId = actionId, ActionData = Link(actionId, pageId).ActionData };
        var child = new MenuItemEntity { Parent = root, Title = "Child", ActionTypeId = actionId, ActionData = Link(actionId, childPageId).ActionData };
        ctx.MenuItemPageReferences.AddRange(
            new MenuItemPageReferenceEntity { MenuItem = root, ReferencedPageId = pageId },
            new MenuItemPageReferenceEntity { MenuItem = child, ReferencedPageId = childPageId });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return menu;
    }

    private static async Task WaitForLockAsync(CmsEngineDbContext ctx, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        while (!await ctx.Database.SqlQuery<bool>($"SELECT EXISTS (SELECT 1 FROM pg_locks WHERE locktype = 'advisory' AND classid::int8 = {CmsLocks.PublicationNamespace} AND objid = 0 AND objsubid = 2 AND NOT granted) AS \"Value\"").SingleAsync(timeout.Token))
        {
            await Task.Delay(20, timeout.Token);
        }
    }
}
