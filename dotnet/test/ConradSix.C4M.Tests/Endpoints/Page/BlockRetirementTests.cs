namespace ConradSix.C4M.Tests.Endpoints.Page;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using ConradSix.C4M.Data;
using ConradSix.C4M.Data.Model;
using ConradSix.C4M.Endpoints.BlockType;
using ConradSix.C4M.Endpoints.Page;
using ConradSix.C4M.Model;
using ConradSix.C4M.Services;
using ConradSix.C4M.Services.Model;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

[Collection(C4MAppCollection.Name)]
public sealed class BlockRetirementTests(C4MAppFixture fixture) : IAsyncDisposable
{
    public ValueTask DisposeAsync() => fixture.TearDownTestAsync();

    [Theory]
    [InlineData("retired-block")]
    [InlineData("retired-gallery")]
    public async Task RetiredTypes_ShouldRemainReadableAcrossVersions(string name)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        var first = new BlockTypeEntity { Name = name, Version = 1, JsonSchema = "{}" };
        var latest = new BlockTypeEntity { Name = name, Version = 9, JsonSchema = "{}" };
        context.BlockTypes.AddRange(first, latest);
        await context.SaveChangesAsync(ct);
        using var client = fixture.CreateClient();
        var byId = await client.GetFromJsonAsync<CmsTypeDescriptor>($"/block-types/{first.Id}", ct);
        byId!.IsRetired.Should().BeTrue();
        byId.JsonSchema.Should().Be("{}");
        var byName = await client.GetFromJsonAsync<CmsTypeDescriptor>($"/block-types/by-name/{name}", ct);
        byName!.Id.Should().Be(latest.Id);
        byName.IsRetired.Should().BeTrue();
        var listed = await client.GetFromJsonAsync<ListBlockTypesResponse>("/block-types", ct);
        listed!.Items.Should().HaveCount(2).And.OnlyContain(type => type.IsRetired);
    }

    [Theory]
    [InlineData("retired-block")]
    [InlineData("retired-gallery")]
    public async Task NewPages_ShouldRejectRetiredTypesWithoutCreatingPage(string name)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        var type = new BlockTypeEntity { Name = name, Version = 1, JsonSchema = "{}" };
        context.BlockTypes.Add(type);
        await context.SaveChangesAsync(ct);
        using var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/pages",
            new
            {
                slug = "retired-test",
                parentId = (Guid?)null,
                blocks = new[] { new { blockTypeId = type.Id, blockData = "{}" } },
                publish = false,
            },
            ct);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(ct)).Should().Contain("retired");
        (await context.Pages.CountAsync(ct)).Should().Be(0);
        (await context.PageVersions.CountAsync(ct)).Should().Be(0);
    }

    [Theory]
    [InlineData("retired-block")]
    [InlineData("retired-gallery")]
    public async Task Drafts_ShouldRejectReusedRetiredBlocksWhileHistoryRemainsReadable(string name)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        var type = new BlockTypeEntity { Name = name, Version = 1, JsonSchema = "{}" };
        var page = new PageEntity { Slug = "retired-history" };
        var block = new PageBlockEntity { BlockType = type, BlockData = "{}", InstanceId = Guid.NewGuid() };
        var version = new PageVersionEntity { Page = page, IsCurrent = true };
        context.PageVersionBlocks.Add(new PageVersionBlockEntity { PageVersion = version, PageBlock = block, SortOrder = 1 });
        await context.SaveChangesAsync(ct);
        using var client = fixture.CreateClient();
        var historic = await client.GetFromJsonAsync<GetPageResponse>($"/pages/by-version/{version.Id}", ct);
        historic!.Blocks.Should().ContainSingle().Which.BlockName.Should().Be(name);
        var response = await client.PutAsJsonAsync(
            $"/pages/{page.Id}",
            new
            {
                expectedLatestPageVersionId = version.Id,
                newBlocks = Array.Empty<object>(),
                existingBlocks = new[] { new { blockId = block.Id, sortOrder = 0 } },
            },
            ct);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await context.PageVersions.CountAsync(ct)).Should().Be(1);
        (await context.PageVersions.SingleAsync(ct)).IsCurrent.Should().BeTrue();
    }

    [Fact]
    public async Task RetiredPayload_ShouldNotBypassGuardWithAnActiveDescriptor()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        var type = new BlockTypeEntity { Name = "content", Version = 6, JsonSchema = "{}" };
        var page = new PageEntity { Slug = "payload-test" };
        context.AddRange(type, page);
        await context.SaveChangesAsync(ct);
        var service = scope.ServiceProvider.GetRequiredService<IPageVersionServices>();
        var act = () => service.CreatePageVersionAsync(
            page.Id,
            [new PageBlock(null, type.Id, JsonSerializer.Serialize(new { __typename = "retired-block", __version = 5 }))],
            ct);
        await act.Should().ThrowAsync<CmsBlockRetiredException>();
        (await context.PageVersions.CountAsync(ct)).Should().Be(0);
    }
}
