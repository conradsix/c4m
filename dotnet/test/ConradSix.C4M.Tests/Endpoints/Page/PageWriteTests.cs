namespace ConradSix.C4M.Tests.Endpoints.Page;

using System.Transactions;

using AwesomeAssertions;

using ConradSix.C4M.Data;
using ConradSix.C4M.Data.Model;
using ConradSix.C4M.Services;
using ConradSix.C4M.Services.Model;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

// The CMS's own writes: each commits by itself, and a caller that chose ids or keys can repeat them safely.
[Collection(C4MAppCollection.Name)]
public sealed class PageWriteTests(C4MAppFixture fixture) : IAsyncDisposable
{
    private static readonly PageVersionMetadata Seo = new("Title", "Description", null, true, true);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CreatePageAsync_ShouldReturnTheSamePage_WhenRepeatedWithTheChosenId()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var pages = scope.ServiceProvider.GetRequiredService<IPageService>();
        var blockTypeId = await BlockTypeAsync(scope);
        var page = new NewPage(Guid.NewGuid(), null, null, "repeatable", [Block(blockTypeId)], Seo, Publish: false);

        var first = (PageCreated)await pages.CreatePageAsync(page, Ct);
        var second = (PageCreated)await pages.CreatePageAsync(page, Ct);

        first.Replayed.Should().BeFalse();
        second.Should().Be(first with { Replayed = true });
        var ctx = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        (await ctx.Pages.CountAsync(item => item.Slug == "repeatable", Ct)).Should().Be(1);
        (await ctx.PageVersions.CountAsync(version => version.PageId == first.PageId, Ct)).Should().Be(1);
        (await pages.CreatePageAsync(page with { Slug = "another" }, Ct)).Should().BeOfType<CreatePageIdTaken>();
    }

    [Fact]
    public async Task CreatePageAsync_ShouldDescribeWhyAPageCannotBeCreated()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var pages = scope.ServiceProvider.GetRequiredService<IPageService>();
        var blockTypeId = await BlockTypeAsync(scope);
        NewPage newPage(string slug, Guid? parentId = null, bool publish = false, PageVersionMetadata? seo = null) =>
            new(null, parentId, null, slug, [Block(blockTypeId)], seo ?? Seo, publish);
        await pages.CreatePageAsync(newPage("taken"), Ct);

        (await pages.CreatePageAsync(newPage("taken"), Ct)).Should().BeOfType<CreatePagePathTaken>();
        (await pages.CreatePageAsync(newPage("orphan", Guid.NewGuid()), Ct)).Should().BeOfType<CreatePageParentMissing>();
        (await pages.CreatePageAsync(newPage("api"), Ct)).Should().Be(new CreatePageReserved("/api"));
        var unpublishable = await pages.CreatePageAsync(newPage("untitled", publish: true, seo: PageVersionMetadata.Empty), Ct);
        unpublishable.Should().BeOfType<CreatePageInvalid>()
            .Which.Errors.Should().Contain("SEO title is required before publishing.");
        var ctx = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        (await ctx.Pages.Select(page => page.Slug).ToArrayAsync(Ct)).Should().Equal("taken");
    }

    [Fact]
    public async Task Writes_ShouldRefuseToRunInsideAnAmbientTransaction()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var blockTypeId = await BlockTypeAsync(scope);
        using var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

        var create = () => scope.ServiceProvider.GetRequiredService<IPageService>()
            .CreatePageAsync(new NewPage(null, null, null, "inside", [Block(blockTypeId)], Seo, Publish: false), Ct);

        await create.Should().ThrowAsync<InvalidOperationException>().WithMessage("CMS operations commit in their own transaction*");
    }

    [Fact]
    public async Task Writes_ShouldCommitApartFromTheCallersTransaction()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var blockTypeId = await BlockTypeAsync(scope);
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(Ct);
        PageCreated created;
        await using (await connection.BeginTransactionAsync(Ct))
        {
            created = (PageCreated)await scope.ServiceProvider.GetRequiredService<IPageService>()
                .CreatePageAsync(new NewPage(null, null, null, "apart", [Block(blockTypeId)], Seo, Publish: false), Ct);
        }

        (await scope.ServiceProvider.GetRequiredService<ICmsPageReader>().GetPageAsync(created.PageId, Ct)).Should().NotBeNull();
    }

    [Fact]
    public async Task CreatePageVersionAsync_ShouldReplayAKeyedWrite_AndRejectAStaleOne()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var versions = scope.ServiceProvider.GetRequiredService<IPageVersionServices>();
        var blockTypeId = await BlockTypeAsync(scope);
        var page = (PageCreated)await scope.ServiceProvider.GetRequiredService<IPageService>()
            .CreatePageAsync(new NewPage(null, null, null, "versions", [Block(blockTypeId)], Seo, Publish: false), Ct);
        var write = new PageVersionWrite(page.PageVersionId, "caller:once");

        var saved = (PageVersionSaved)await versions.CreatePageVersionAsync(page.PageId, [Block(blockTypeId)], Seo, write, Ct);
        var repeated = await versions.CreatePageVersionAsync(page.PageId, [Block(blockTypeId)], Seo, write, Ct);
        var stale = await versions.CreatePageVersionAsync(page.PageId, [Block(blockTypeId)], Seo, write with { IdempotencyKey = null }, Ct);
        var missing = await versions.CreatePageVersionAsync(Guid.NewGuid(), [Block(blockTypeId)], Seo, PageVersionWrite.Unconditional, Ct);

        repeated.Should().Be(saved with { Replayed = true });
        stale.Should().Be(new PageVersionConflict(saved.PageVersionId));
        missing.Should().BeOfType<PageVersionPageMissing>();
        var ctx = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        (await ctx.PageVersions.CountAsync(version => version.PageId == page.PageId, Ct)).Should().Be(2);
    }

    public async ValueTask DisposeAsync() => await fixture.TearDownTestAsync();

    private static PageBlock Block(Guid blockTypeId) => new(null, blockTypeId, /*lang=json,strict*/ """{"text":"Copy"}""");

    private static async Task<Guid> BlockTypeAsync(AsyncServiceScope scope)
    {
        var ctx = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        var type = new BlockTypeEntity { Name = $"text-{Guid.NewGuid():N}", JsonSchema = "{}" };
        ctx.BlockTypes.Add(type);
        await ctx.SaveChangesAsync(Ct);
        return type.Id;
    }
}
