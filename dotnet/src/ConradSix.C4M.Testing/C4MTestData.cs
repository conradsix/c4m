namespace ConradSix.C4M.Testing;

using ConradSix.C4M.Data;
using ConradSix.C4M.Data.Model;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

// Seeds and clears the engine's tables for tests. It writes below the engine's services, so a test can set up the state
// it needs without the rules a real write goes through. Each call commits on its own.
public sealed class C4MTestData(IServiceScopeFactory scopes)
{
    private string? _resetSql;

    // Empties every engine table.
    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        if (this._resetSql is null)
        {
            var sql = ctx.GetService<ISqlGenerationHelper>();
            var tables = ctx.Model.GetEntityTypes()
                .Select(type => sql.DelimitIdentifier(type.GetTableName()!, type.GetSchema()))
                .Distinct();
            this._resetSql = $"TRUNCATE TABLE {string.Join(", ", tables)} RESTART IDENTITY CASCADE";
        }

#pragma warning disable EF1002 // The statement names only the engine's own tables, quoted by EF's SQL generation helper.
        await ctx.Database.ExecuteSqlRawAsync(this._resetSql, cancellationToken);
#pragma warning restore EF1002
    }

    public async Task<Guid> AddBlockTypeAsync(string name, int version = 1, string jsonSchema = "{}", CancellationToken cancellationToken = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        var type = new BlockTypeEntity { Name = name, Version = version, JsonSchema = jsonSchema };
        ctx.BlockTypes.Add(type);
        await ctx.SaveChangesAsync(cancellationToken);
        return type.Id;
    }

    // Adds a page directly, optionally with an empty current version whose SEO title and description are the slug, so
    // it can be published. The page is published or archived as asked, whether or not the engine would allow it.
    public async Task<C4MTestPage> AddPageAsync(
        string slug,
        Guid? parentId = null,
        bool published = false,
        DateTimeOffset? archivedAt = null,
        bool withVersion = false,
        Guid? id = null,
        CancellationToken cancellationToken = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        var page = new PageEntity { Slug = slug, ParentId = parentId, IsPublished = published, ArchivedAt = archivedAt };
        if (id is { } pageId)
        {
            page.Id = pageId;
        }

        ctx.Pages.Add(page);
        var version = withVersion ? new PageVersionEntity { Page = page, IsCurrent = true, SeoTitle = slug, SeoDescription = slug } : null;
        if (version is not null)
        {
            ctx.PageVersions.Add(version);
        }

        await ctx.SaveChangesAsync(cancellationToken);
        return new C4MTestPage(page.Id, page.Slug, page.Path, version?.Id);
    }

    // Changes a page's slug, and so its path and its descendants' paths.
    public async Task RenamePageAsync(Guid pageId, string slug, CancellationToken cancellationToken = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        var page = await ctx.Pages.SingleAsync(item => item.Id == pageId, cancellationToken);
        page.Slug = slug;
        await ctx.SaveChangesAsync(cancellationToken);
    }

    // Archives or restores a page, unpublishing it when archived.
    public async Task SetArchivedAsync(Guid pageId, DateTimeOffset? archivedAt, CancellationToken cancellationToken = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        var page = await ctx.Pages.SingleAsync(item => item.Id == pageId, cancellationToken);
        page.ArchivedAt = archivedAt;
        page.IsPublished = page.IsPublished && archivedAt is null;
        await ctx.SaveChangesAsync(cancellationToken);
    }

    // Removes a page that has no versions, children or references to it.
    public async Task RemovePageAsync(Guid pageId, CancellationToken cancellationToken = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>().Pages
            .Where(page => page.Id == pageId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<int> CountVersionsAsync(Guid pageId, CancellationToken cancellationToken = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>().PageVersions
            .CountAsync(version => version.PageId == pageId, cancellationToken);
    }

    // Changes a registered block type's schema. The engine never does: this is for tests of a host that has to cope
    // with a registry that changed under it.
    public async Task ReplaceBlockTypeSchemaAsync(string name, int version, string jsonSchema, CancellationToken cancellationToken = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<CmsEngineDbContext>();
        var type = await ctx.BlockTypes.SingleAsync(item => item.Name == name && item.Version == version, cancellationToken);
        type.JsonSchema = jsonSchema;
        await ctx.SaveChangesAsync(cancellationToken);
    }
}
