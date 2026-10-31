namespace ConradSix.C4M.Tests.Endpoints.Page;

using AwesomeAssertions;

using ConradSix.C4M;
using ConradSix.C4M.Data;
using ConradSix.C4M.Endpoints.Page;
using ConradSix.C4M.Services;
using ConradSix.C4M.Services.Model;
using ConradSix.C4M.Testing;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

[Collection(C4MAppCollection.Name)]
public sealed class BlockReferenceTests(C4MAppFixture fixture) : IAsyncDisposable
{
    private static readonly Guid Garden = Guid.NewGuid();
    private static readonly Guid Pool = Guid.NewGuid();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CreatePageVersionAsync_ShouldStoreEachPageTargetInTheOrderItFirstAppears()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var (pageId, blockTypeId) = await this.ArrangeAsync();
        var versions = scope.ServiceProvider.GetRequiredService<IPageVersionServices>();

        await versions.CreatePageVersionAsync(
            pageId,
            [
                new PageBlock(null, blockTypeId, ImagesJson(Pool)),
                new PageBlock(null, blockTypeId, ImagesJson(Garden, Pool)),
            ],
            Ct);

        var images = await scope.ServiceProvider.GetRequiredService<IBlockReferenceQuery>()
            .GetCurrentAsync(CmsReferenceKinds.Image, [pageId], Ct);
        images[pageId].Should().Equal(Pool, Garden);

        var page = await scope.ServiceProvider.GetRequiredService<IPageQueryService>().GetPageByIdLatestAsync(pageId, Ct);
        page!.References.Should().Equal(
            new PageReferenceResponse(CmsReferenceKinds.Image, Pool),
            new PageReferenceResponse(CmsReferenceKinds.Image, Garden));
    }

    [Fact]
    public async Task CreatePageVersionAsync_ShouldRejectLinksToPagesThatDoNotExist()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var (pageId, blockTypeId) = await this.ArrangeAsync();
        var missing = Guid.NewGuid();

        var create = () => scope.ServiceProvider.GetRequiredService<IPageVersionServices>().CreatePageVersionAsync(
            pageId,
            [new PageBlock(null, blockTypeId, $$"""{ "link": { "__typename": "page-ref", "id": "{{missing}}" } }""")],
            Ct);

        await create.Should().ThrowAsync<CmsBlockValidationException>().WithMessage($"Linked page {missing} does not exist.");
    }

    [Fact]
    public async Task ReindexAsync_ShouldRebuildReferencesFromBlockData()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        var (pageId, blockTypeId) = await this.ArrangeAsync();
        await scope.ServiceProvider.GetRequiredService<IPageVersionServices>().CreatePageVersionAsync(
            pageId,
            [new PageBlock(null, blockTypeId, ImagesJson(Garden, Pool))],
            Ct);
        var expected = await ctx.PageBlockReferences.AsNoTracking()
            .Select(reference => new { reference.PageBlockId, reference.Kind, reference.TargetId, reference.JsonPath, reference.Ordinal })
            .ToListAsync(Ct);
        await ctx.PageBlockReferences.ExecuteDeleteAsync(Ct);
        var reported = new List<(int Done, int Total)>();

        var blocks = await scope.ServiceProvider.GetRequiredService<IBlockReferenceReindexer>().ReindexAsync(
            (done, total, _) =>
            {
                reported.Add((done, total));
                return ValueTask.CompletedTask;
            },
            Ct);

        blocks.Should().Be(1);
        reported.Should().Equal((1, 1));
        (await ctx.PageBlockReferences.AsNoTracking()
            .Select(reference => new { reference.PageBlockId, reference.Kind, reference.TargetId, reference.JsonPath, reference.Ordinal })
            .ToListAsync(Ct)).Should().BeEquivalentTo(expected);
        fixture.Changes.Changes.Should().Contain(CmsChange.Content);
    }

    [Fact]
    public async Task ReindexAsync_ShouldKeepEachTargetsOrdinal_ThoughStoredDataReordersProperties()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        var (pageId, blockTypeId) = await this.ArrangeAsync();

        // JSONB stores the shorter gallery first, so the block reads back in a different order than it was saved.
        var block = $$"""
            {
              "featuredImage": { "__typename": "image-ref", "id": "{{Garden}}" },
              "gallery": [{ "__typename": "image-ref", "id": "{{Pool}}" }]
            }
            """;
        await scope.ServiceProvider.GetRequiredService<IPageVersionServices>().CreatePageVersionAsync(
            pageId,
            [new PageBlock(null, blockTypeId, block)],
            Ct);
        var images = scope.ServiceProvider.GetRequiredService<IBlockReferenceQuery>();
        (await images.GetCurrentAsync(CmsReferenceKinds.Image, [pageId], Ct))[pageId].Should().Equal(Garden, Pool);

        await scope.ServiceProvider.GetRequiredService<IBlockReferenceReindexer>().ReindexAsync(null, Ct);

        ctx.ChangeTracker.Clear();
        (await images.GetCurrentAsync(CmsReferenceKinds.Image, [pageId], Ct))[pageId].Should().Equal(Garden, Pool);
    }

    [Fact]
    public async Task CreatePageVersionAsync_ShouldRejectALinkToTheNilPage()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var (pageId, blockTypeId) = await this.ArrangeAsync();

        var create = () => scope.ServiceProvider.GetRequiredService<IPageVersionServices>().CreatePageVersionAsync(
            pageId,
            [new PageBlock(null, blockTypeId, $$"""{ "link": { "__typename": "page-ref", "id": "{{Guid.Empty}}" } }""")],
            Ct);

        await create.Should().ThrowAsync<CmsBlockValidationException>().WithMessage($"Linked page {Guid.Empty} does not exist.");
    }

    public async ValueTask DisposeAsync() => await fixture.TearDownTestAsync();

    private static string ImagesJson(params Guid[] images) =>
        $$"""{ "images": [{{string.Join(", ", images.Select(image => $$"""{ "__typename": "image-ref", "id": "{{image}}" }"""))}}] }""";

    private async Task<(Guid PageId, Guid BlockTypeId)> ArrangeAsync()
    {
        var page = await fixture.Data.AddPageAsync($"references-{Guid.NewGuid():N}", cancellationToken: Ct);
        var blockTypeId = await fixture.Data.AddBlockTypeAsync("gallery", cancellationToken: Ct);
        return (page.Id, blockTypeId);
    }
}
