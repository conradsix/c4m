namespace ConradSix.C4M.Tests.Endpoints.Page;

using AwesomeAssertions;

using ConradSix.C4M;
using ConradSix.C4M.Data;
using ConradSix.C4M.Data.Model;
using ConradSix.C4M.Services;
using ConradSix.C4M.Services.Model;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

[Collection(C4MAppCollection.Name)]
public sealed class PageVersionServicesTests(C4MAppFixture fixture) :
    IAsyncDisposable
{
    [Fact]
    public async Task CreatePageVersionAsync_ShouldCreateVersionWithNewBlocks()
    {
        // Arrange
        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        var versionService = scope.ServiceProvider.GetRequiredService<IPageVersionServices>();

        var realtorId = Guid.NewGuid();

        // Create a page
        var page = new PageEntity
        {
            Slug = "test-page",
            ParentId = null,
        };

        context.Pages.Add(page);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Create a block type
        var blockType = new BlockTypeEntity
        {
            Name = "TextBlock",
            Version = 1,
            JsonSchema = "{}",
        };

        context.BlockTypes.Add(blockType);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var blocks = new List<PageBlock>
        {
            new(null, blockType.Id, /*lang=json,strict*/ @"{""text"": ""Hello World""}"),
            new(null, blockType.Id, /*lang=json,strict*/ @"{""text"": ""Second Block""}"),
        };

        // Act
        var versionId = await versionService.CreatePageVersionAsync(
            page.Id,
            blocks,
            TestContext.Current.CancellationToken);

        // Assert
        versionId.Should().NotBe(Guid.Empty);

        var version = await context.PageVersions
            .FirstOrDefaultAsync(pv => pv.Id == versionId, TestContext.Current.CancellationToken);

        version.Should().NotBeNull();
        version.PageId.Should().Be(page.Id);
        version.IsCurrent.Should().BeTrue(); // The first saved version is selected without publishing.

        // Verify blocks were created and linked
        var versionBlocks = await context.PageVersionBlocks
            .Where(pvb => pvb.PageVersionId == versionId)
            .OrderBy(pvb => pvb.SortOrder)
            .ToListAsync(TestContext.Current.CancellationToken);

        versionBlocks.Should().HaveCount(2);
        versionBlocks[0].SortOrder.Should().Be(1);
        versionBlocks[1].SortOrder.Should().Be(2);
    }

    [Fact]
    public async Task CreatePageVersionAsync_ShouldReuseExistingBlocks()
    {
        // Arrange
        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        var versionService = scope.ServiceProvider.GetRequiredService<IPageVersionServices>();

        // Create a page and block type
        var page = new PageEntity { Slug = "test-page", ParentId = null };
        var blockType = new BlockTypeEntity { Name = "TextBlock", Version = 1, JsonSchema = "{}" };

        context.Pages.Add(page);
        context.BlockTypes.Add(blockType);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Create first version with new blocks
        var firstVersionBlocks = new List<PageBlock>
        {
            new(null, blockType.Id, /*lang=json,strict*/ @"{""text"": ""Block 1""}"),
            new(null, blockType.Id, /*lang=json,strict*/ @"{""text"": ""Block 2""}"),
        };

        var firstVersionId = await versionService.CreatePageVersionAsync(
            page.Id, firstVersionBlocks, TestContext.Current.CancellationToken);

        // Get the created block IDs
        var createdBlocks = await context.PageVersionBlocks
            .Where(pvb => pvb.PageVersionId == firstVersionId)
            .Select(pvb => pvb.PageBlockId)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Act - Create second version reusing first block and adding a new one
        var secondVersionBlocks = new List<PageBlock>
        {
            new(createdBlocks[0], blockType.Id, string.Empty), // Reuse existing block
            new(null, blockType.Id, /*lang=json,strict*/ @"{""text"": ""New Block""}"), // New block
        };

        var secondVersionId = await versionService.CreatePageVersionAsync(
            page.Id, secondVersionBlocks, TestContext.Current.CancellationToken);

        // Assert
        var secondVersionBlockLinks = await context.PageVersionBlocks
            .Where(pvb => pvb.PageVersionId == secondVersionId)
            .OrderBy(pvb => pvb.SortOrder)
            .ToListAsync(TestContext.Current.CancellationToken);

        secondVersionBlockLinks.Should().HaveCount(2);
        secondVersionBlockLinks[0].PageBlockId.Should().Be(createdBlocks[0]); // Reused block
        secondVersionBlockLinks[1].PageBlockId.Should().NotBe(createdBlocks[0]); // New block
        secondVersionBlockLinks[1].PageBlockId.Should().NotBe(createdBlocks[1]); // Different from second block in first version
    }

    [Fact]
    public async Task CreatePageVersionAsync_ShouldIndexMarkdownPageReferences()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        var versionService = scope.ServiceProvider.GetRequiredService<IPageVersionServices>();
        var sourcePage = new PageEntity { Slug = "source-page" };
        var targetPage = new PageEntity { Slug = "target-page", IsPublished = true };
        var blockType = new BlockTypeEntity
        {
            Name = "MarkdownBlock",
            Version = 1,
            JsonSchema = "{}",
        };

        context.Pages.AddRange(sourcePage, targetPage);
        context.BlockTypes.Add(blockType);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var blockData = $$"""
        {
          "markdown": "[Target page](page-ref:{{targetPage.Id}})"
        }
        """;
        var versionId = await versionService.CreatePageVersionAsync(
            sourcePage.Id,
            [new PageBlock(null, blockType.Id, blockData)],
            TestContext.Current.CancellationToken);
        var pageBlockId = await context.PageVersionBlocks
            .Where(block => block.PageVersionId == versionId)
            .Select(block => block.PageBlockId)
            .SingleAsync(TestContext.Current.CancellationToken);
        var reference = await context.PageBlockReferences
            .SingleAsync(
                item => item.PageBlockId == pageBlockId && item.Kind == CmsReferenceKinds.Page,
                TestContext.Current.CancellationToken);

        reference.TargetId.Should().Be(targetPage.Id);
        reference.JsonPath.Should().Be("$.markdown");
    }

    [Fact]
    public async Task ValidateForPublicationAsync_ShouldReportUnpublishedFeaturedLinkTargetOnce()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        var versionService = scope.ServiceProvider.GetRequiredService<IPageVersionServices>();
        var sourcePage = new PageEntity { Slug = "featured-source" };
        var targetPage = new PageEntity { Slug = "unpublished-target" };
        var featuredLinksType = new BlockTypeEntity
        {
            Name = "featured-links",
            Version = 1,
            JsonSchema = "{}",
        };

        context.Pages.AddRange(sourcePage, targetPage);
        context.BlockTypes.Add(featuredLinksType);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.PageVersions.Add(new PageVersionEntity { PageId = targetPage.Id, IsCurrent = true });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var blockData = $$"""
        {
          "__typename": "featured-links",
          "__version": 1,
          "items": [{
            "id": "{{Guid.NewGuid()}}",
            "label": "Target page",
            "link": {
              "__typename": "page-link",
              "__version": 1,
              "pageRef": { "__typename": "page-ref", "id": "{{targetPage.Id}}" }
            }
          }]
        }
        """;
        var sourceVersionId = await versionService.CreatePageVersionAsync(
            sourcePage.Id,
            [new PageBlock(null, featuredLinksType.Id, blockData)],
            new PageVersionMetadata(
                SeoTitle: "Source page",
                SeoDescription: "Source page description",
                SeoImageId: null,
                RobotsIndex: true,
                RobotsFollow: true),
            TestContext.Current.CancellationToken);

        var errors = await versionService.ValidateForPublicationAsync(
            sourceVersionId,
            TestContext.Current.CancellationToken);

        errors.Should().ContainSingle()
            .Which.Should().Be("Linked page '/unpublished-target' must be published first.");
    }

    [Fact]
    public async Task SetCurrentPageVersionAsync_ShouldSetVersionAsCurrent()
    {
        // Arrange
        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        var versionService = scope.ServiceProvider.GetRequiredService<IPageVersionServices>();

        var realtorId = Guid.NewGuid();

        // Create a page
        var page = new PageEntity
        {
            Slug = "test-page",
            ParentId = null,
        };

        context.Pages.Add(page);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Create two versions
        var version1 = new PageVersionEntity { PageId = page.Id };
        var version2 = new PageVersionEntity { PageId = page.Id };

        context.PageVersions.AddRange(version1, version2);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Set first version as current
        await versionService.SetCurrentPageVersionAsync(
            version1.Id,
            TestContext.Current.CancellationToken);

        // Act - Set second version as current
        await versionService.SetCurrentPageVersionAsync(
            version2.Id,
            TestContext.Current.CancellationToken);

        // Assert
        await context.Entry(version1).ReloadAsync(TestContext.Current.CancellationToken);
        await context.Entry(version2).ReloadAsync(TestContext.Current.CancellationToken);

        version1.IsCurrent.Should().BeNull();
        version2.IsCurrent.Should().BeTrue();
    }

    [Fact]
    public async Task SetCurrentPageVersionAsync_ShouldThrowWhenVersionNotFound()
    {
        // Arrange
        await using var scope = fixture.Services.CreateAsyncScope();
        var versionService = scope.ServiceProvider.GetRequiredService<IPageVersionServices>();

        var nonExistentVersionId = Guid.NewGuid();

        // Act & Assert
        var action = async () => await versionService.SetCurrentPageVersionAsync(
            nonExistentVersionId,
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Page version with ID {nonExistentVersionId} not found.");
    }

    [Fact]
    public async Task CreatePageVersionAsync_ShouldRejectMetadataThatExceedsStorageLimits()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var versionService = scope.ServiceProvider.GetRequiredService<IPageVersionServices>();
        var metadata = new PageVersionMetadata(
            SeoTitle: "Title",
            SeoDescription: new string('x', PageVersionEntity.SeoDescriptionMaxLength + 1),
            SeoImageId: null,
            RobotsIndex: true,
            RobotsFollow: true);

        var action = async () => await versionService.CreatePageVersionAsync(
            Guid.NewGuid(),
            [],
            metadata,
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("SEO description must not exceed 160 characters; received 161.*");
    }

    [Fact]
    public async Task CreatePageVersionAsync_ShouldRejectABlockThatDoesNotMatchItsTypesSchema()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        var versionService = scope.ServiceProvider.GetRequiredService<IPageVersionServices>();
        var page = new PageEntity { Slug = "schema-checked", ParentId = null };
        var blockType = new BlockTypeEntity
        {
            Name = "versioned-text",
            Version = 1,
            JsonSchema = /*lang=json,strict*/ """{"type": "object", "properties": {"__version": {"const": 1}, "text": {"type": "string"}}, "required": ["__version", "text"]}""",
        };
        context.AddRange(page, blockType);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var action = async () => await versionService.CreatePageVersionAsync(
            page.Id,
            [new(null, blockType.Id, /*lang=json,strict*/ """{"__version": 2, "text": "Hello"}""")],
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<CmsBlockValidationException>()
            .WithMessage("*versioned-text*#/__version*ConstMismatch*");
        (await context.PageVersions.AnyAsync(version => version.PageId == page.Id, TestContext.Current.CancellationToken))
            .Should().BeFalse();
    }

    public ValueTask DisposeAsync() => fixture.TearDownTestAsync();
}
