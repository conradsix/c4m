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
using ConradSix.C4M.Services.Model;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

[Collection(C4MAppCollection.Name)]
public sealed class UnpublishPageTests(C4MAppFixture fixture) : IAsyncDisposable
{
    [Fact]
    public async Task Unpublish_RetainsHistoryAndDraftAccess_AndCanRepublish()
    {
        using var client = fixture.CreateClient();
        var page = await TestCreatePageRequestBuilder.Create().CreateAsync(client, TestContext.Current.CancellationToken);
        var request = new PagePublicationRequest(page.PageDetails.PageVersionId, true);
        (await client.PostAsJsonAsync($"/pages/{page.Id}/unpublish", request, TestContext.Current.CancellationToken))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.PostAsJsonAsync($"/pages/{page.Id}/unpublish", request with { ExpectedIsPublished = false }, TestContext.Current.CancellationToken))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync($"/pages/{page.Id}", TestContext.Current.CancellationToken)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync("/pages/by-path?path=/test-page", TestContext.Current.CancellationToken)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await TestPageHelper.GetPageAsync(client, page.Id, true, TestContext.Current.CancellationToken)).PageId.Should().Be(page.Id);
        (await TestPageHelper.GetPageVersionAsync(client, request.ExpectedCurrentPageVersionId!.Value, TestContext.Current.CancellationToken)).PageId.Should().Be(page.Id);
        var history = await TestPageHelper.GetPageHistoryAsync(client, page.Id, TestContext.Current.CancellationToken);
        history.Versions.Should().ContainSingle().Which.IsCurrent.Should().BeTrue();
        var list = await client.GetFromJsonAsync<ListPagesResponse>("/pages", TestContext.Current.CancellationToken);
        list!.Pages.Should().ContainSingle().Which.CurrentPageVersionId.Should().Be(request.ExpectedCurrentPageVersionId);
        list.Pages[0].IsPublished.Should().BeFalse();
        (await client.PostAsJsonAsync($"/pages/{page.Id}/publish", request with { ExpectedIsPublished = false }, TestContext.Current.CancellationToken)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Unpublish_ReturnsNotFoundOrStaleVersionConflict()
    {
        using var client = fixture.CreateClient();
        (await client.PostAsJsonAsync($"/pages/{Guid.NewGuid()}/unpublish", new PagePublicationRequest(Guid.NewGuid(), true), TestContext.Current.CancellationToken))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        var page = await TestCreatePageRequestBuilder.Create().CreateAsync(client, TestContext.Current.CancellationToken);
        var response = await client.PostAsJsonAsync($"/pages/{page.Id}/unpublish", new PagePublicationRequest(Guid.NewGuid(), true), TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadFromJsonAsync<UnpublishPageConflict>(TestContext.Current.CancellationToken))!.Code.Should().Be("publication-changed");
        (await TestPageHelper.GetPageAsync(client, page.Id, TestContext.Current.CancellationToken)).PageVersionId.Should().Be(page.PageDetails.PageVersionId);
    }

    [Fact]
    public async Task Unpublish_DeduplicatesLiveReferences_LimitsToThree_AndIgnoresDrafts()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        var target = await CreatePageAsync(ctx, "target");
        for (var index = 0; index < 5; index++)
        {
            var page = await CreatePageAsync(ctx, $"source-{index}");
            await AddLinkedVersionAsync(scope.ServiceProvider, page.PageId, target.PageId, true);
            await AddLinkedVersionAsync(scope.ServiceProvider, page.PageId, null, false);
        }

        var draft = await CreatePageAsync(ctx, "draft", live: false);
        await AddLinkedVersionAsync(scope.ServiceProvider, draft.PageId, target.PageId, false);
        using var client = fixture.CreateClient();
        var conflict = await GetConflictAsync(client, target);
        conflict.TotalPages.Should().Be(5);
        conflict.Pages.Select(page => page.Path).Should().Equal("/source-0", "/source-1", "/source-2");
        conflict.Pages.Should().OnlyContain(page => page.Reason.StartsWith("Link in content-"));
        conflict.TotalMenus.Should().Be(0);
        foreach (var page in conflict.Pages)
        {
            (await TestPageHelper.GetPageAsync(client, page.PageId, TestContext.Current.CancellationToken)).PageVersionId.Should().Be(page.PageVersionId);
        }
    }

    [Fact]
    public async Task Unpublish_IgnoresHistoricalAndSelfReferences_AndRejectsRepublishingBrokenLinks()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        var target = await CreatePageAsync(ctx, "target");
        target = await AddLinkedVersionAsync(scope.ServiceProvider, target.PageId, target.PageId, true);
        var source = await CreatePageAsync(ctx, "source");
        var historical = await AddLinkedVersionAsync(scope.ServiceProvider, source.PageId, target.PageId, true);
        await AddLinkedVersionAsync(scope.ServiceProvider, source.PageId, null, true);
        using var client = fixture.CreateClient();
        (await client.PostAsJsonAsync($"/pages/{target.PageId}/unpublish", new PagePublicationRequest(target.Id, true), TestContext.Current.CancellationToken))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        var result = await TestPageHelper.SetCurrentVersionExpectingAsync(client, historical.Id, HttpStatusCode.BadRequest, TestContext.Current.CancellationToken);
        (await result.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Contain("must be published first");
        await TestPageHelper.SetCurrentVersionAsync(client, target.Id, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Unpublish_BlocksLiveDescendantsOncePerPage_ButAllowsDraftDescendants()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        var target = await CreatePageAsync(ctx, "target");
        var child = await CreatePageAsync(ctx, "child", target.PageId, live: false);
        var grandchild = await CreatePageAsync(ctx, "grandchild", child.PageId);
        await AddLinkedVersionAsync(scope.ServiceProvider, grandchild.PageId, target.PageId, true);
        using var client = fixture.CreateClient();
        var conflict = await GetConflictAsync(client, target);
        conflict.TotalPages.Should().Be(1);
        conflict.Pages.Should().ContainSingle().Which.Reason.Should().Be("Published descendant (breadcrumb link)");
        await ctx.Pages.Where(page => page.Id == grandchild.PageId).ExecuteUpdateAsync(
            update => update.SetProperty(page => page.IsPublished, false), TestContext.Current.CancellationToken);
        (await client.PostAsJsonAsync($"/pages/{target.PageId}/unpublish", new PagePublicationRequest(target.Id, true), TestContext.Current.CancellationToken))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.PostAsJsonAsync($"/pages/{child.PageId}/publish", new PagePublicationRequest(child.Id, false), TestContext.Current.CancellationToken)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Unpublish_BlocksUnusedAndNestedMenus_WithDistinctCountsAndLimitedSamples()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        var target = await CreatePageAsync(ctx, "target");
        for (var index = 0; index < 5; index++)
        {
            await AddMenuAsync(ctx, target.PageId, $"menu-{index}");
        }

        using var client = fixture.CreateClient();
        var conflict = await GetConflictAsync(client, target);
        conflict.TotalPages.Should().Be(0);
        conflict.TotalMenus.Should().Be(5);
        conflict.Menus.Select(menu => menu.Name).Should().Equal("menu-0", "menu-1", "menu-2");
        conflict.Menus.Should().OnlyContain(menu => menu.ItemTitle == "Nested target");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unpublish_SeesReferencesCommittedWhileWaitingForLock(bool menu)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        var target = await CreatePageAsync(ctx, "target");
        await using var transaction = await ctx.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await PagePublicationGuard.LockAsync(ctx, TestContext.Current.CancellationToken);
        using var client = fixture.CreateClient();
        var pending = client.PostAsJsonAsync($"/pages/{target.PageId}/unpublish", new PagePublicationRequest(target.Id, true), TestContext.Current.CancellationToken);
        await WaitForPublicationLockAsync(ctx);
        if (menu)
        {
            await AddMenuAsync(ctx, target.PageId, "concurrent-menu");
        }
        else
        {
            await AddLinkedPageAsync(ctx, target.PageId);
        }

        await transaction.CommitAsync(TestContext.Current.CancellationToken);
        (await pending).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await TestPageHelper.GetPageAsync(client, target.PageId, TestContext.Current.CancellationToken)).PageVersionId.Should().Be(target.Id);
    }

    [Fact]
    public async Task Publish_SeesUnpublishedTargetAfterWaitingForLock()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        var target = await CreatePageAsync(ctx, "target");
        var source = await CreatePageAsync(ctx, "source", live: false);
        var draft = await AddLinkedVersionAsync(scope.ServiceProvider, source.PageId, target.PageId, false);
        await using var transaction = await ctx.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await PagePublicationGuard.LockAsync(ctx, TestContext.Current.CancellationToken);
        using var client = fixture.CreateClient();
        await scope.ServiceProvider.GetRequiredService<IPageVersionServices>().SetCurrentPageVersionAsync(draft.Id, TestContext.Current.CancellationToken);
        var pending = client.PostAsJsonAsync($"/pages/{source.PageId}/publish", new PagePublicationRequest(draft.Id, false), TestContext.Current.CancellationToken);
        await WaitForPublicationLockAsync(ctx);
        target.Page.IsPublished = false;
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
        (await pending).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreatePage_RejectsPublishingBeneathUnpublishedParent_ButAllowsDrafts()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        var parent = await CreatePageAsync(ctx, "offline", live: false);
        using var client = fixture.CreateClient();
        var request = new CreatePageRequest(parent.PageId, "child", [], new("Child", "Description", null), Publish: true);
        var response = await client.PostAsJsonAsync("/pages", request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ctx.Pages.CountAsync(TestContext.Current.CancellationToken)).Should().Be(1);
        (await client.PostAsJsonAsync("/pages", request with { Publish = false }, TestContext.Current.CancellationToken))
            .StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task MovePage_RejectsPuttingLiveDescendantsUnderUnpublishedAncestor()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        var offline = await CreatePageAsync(ctx, "offline", live: false);
        var root = await CreatePageAsync(ctx, "root", live: false);
        await CreatePageAsync(ctx, "child", root.PageId);
        using var client = fixture.CreateClient();
        var page = await TestPageHelper.GetPageAsync(client, root.PageId, true, TestContext.Current.CancellationToken);
        var request = new UpdatePageDetailsRequest(offline.PageId, "root", page.Version);
        var response = await client.PutAsJsonAsync($"/pages/{root.PageId}/details", request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await TestPageHelper.GetPageAsync(client, root.PageId, true, TestContext.Current.CancellationToken)).ParentId.Should().BeNull();
    }

    public async ValueTask DisposeAsync() => await fixture.TearDownTestAsync();

    private static async Task<PageVersionEntity> CreatePageAsync(CmsEngineDbContext ctx, string slug, Guid? parentId = null, bool live = true)
    {
        var page = new PageEntity { Slug = slug, ParentId = parentId, IsPublished = live };
        var version = new PageVersionEntity { Page = page, IsCurrent = true, SeoTitle = slug, SeoDescription = "Test description" };
        ctx.PageVersions.Add(version);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        return version;
    }

    // A live page linking to the target, written in the caller's transaction as a concurrent editor's commit would be.
    private static async Task AddLinkedPageAsync(CmsEngineDbContext ctx, Guid targetId)
    {
        var type = new BlockTypeEntity { Name = $"content-{Guid.NewGuid():N}", JsonSchema = "{}" };
        var page = new PageEntity { Slug = "concurrent-source", IsPublished = true };
        var block = new PageBlockEntity { BlockType = type, BlockData = $$$"""{"link":{"__typename":"page-ref","id":"{{{targetId}}}"}}""" };
        var version = new PageVersionEntity { Page = page, IsCurrent = true, SeoTitle = "Source", SeoDescription = "Test description" };
        ctx.AddRange(
            type,
            page,
            block,
            version,
            new PageVersionBlockEntity { PageVersion = version, PageBlock = block, SortOrder = 1 },
            new PageBlockReferenceEntity { PageBlock = block, Kind = CmsReferenceKinds.Page, TargetId = targetId, JsonPath = "$.link" });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<PageVersionEntity> AddLinkedVersionAsync(IServiceProvider services, Guid pageId, Guid? targetId, bool live)
    {
        var ctx = services.GetRequiredService<CmsEngineDbContext>();
        var type = new BlockTypeEntity { Name = $"content-{Guid.NewGuid():N}", JsonSchema = "{}" };
        ctx.BlockTypes.Add(type);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        var json = targetId is { } id
            ? $$"""{"link":{"__typename":"page-ref","id":"{{id}}"},"text":"[Target](page-ref:{{id}})"}"""
            : "{}";
        var service = services.GetRequiredService<IPageVersionServices>();
        var versionId = await service.CreatePageVersionAsync(
            pageId, [new PageBlock(null, type.Id, json)], new("Title", "Description", null, true, true), TestContext.Current.CancellationToken);
        if (live)
        {
            await service.SetCurrentPageVersionAsync(versionId, TestContext.Current.CancellationToken);
        }

        return await ctx.PageVersions.SingleAsync(version => version.Id == versionId, TestContext.Current.CancellationToken);
    }

    private static async Task AddMenuAsync(CmsEngineDbContext ctx, Guid pageId, string name)
    {
        var menu = new MenuEntity { Name = name };
        var action = new MenuActionTypeEntity { Name = name, JsonSchema = "{}" };
        var root = new MenuItemEntity { Menu = menu, ActionType = action, Title = "Root", ActionData = "{}" };
        var nested = new MenuItemEntity { Parent = root, ActionType = action, Title = "Nested target", ActionData = "{}" };
        var deeper = new MenuItemEntity { Parent = nested, ActionType = action, Title = "Second target", ActionData = "{}" };
        ctx.MenuItemPageReferences.AddRange(
            new MenuItemPageReferenceEntity { MenuItem = nested, ReferencedPageId = pageId },
            new MenuItemPageReferenceEntity { MenuItem = deeper, ReferencedPageId = pageId });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<UnpublishPageConflict> GetConflictAsync(HttpClient client, PageVersionEntity target)
    {
        var response = await client.PostAsJsonAsync($"/pages/{target.PageId}/unpublish", new PagePublicationRequest(target.Id, true), TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var conflict = await response.Content.ReadFromJsonAsync<UnpublishPageConflict>(TestContext.Current.CancellationToken);
        conflict!.Code.Should().Be("page-referenced");
        return conflict;
    }

    private static async Task WaitForPublicationLockAsync(CmsEngineDbContext ctx)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        while (!await ctx.Database.SqlQuery<bool>($"SELECT EXISTS (SELECT 1 FROM pg_locks WHERE locktype = 'advisory' AND classid::int8 = {CmsLocks.PublicationNamespace} AND objid = 0 AND objsubid = 2 AND NOT granted) AS \"Value\"").SingleAsync(timeout.Token))
        {
            await Task.Delay(20, timeout.Token);
        }
    }
}
