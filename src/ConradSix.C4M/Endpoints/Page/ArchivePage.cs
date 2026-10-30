#pragma warning disable SA1402 // Endpoint contracts are kept with their endpoint.
namespace Parapet.CMS.Engine.Endpoints.Page;

using System.Diagnostics;

using FastEndpoints;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Parapet.CMS.Engine;
using Parapet.CMS.Engine.Data;
using Parapet.CMS.Engine.Services;

[CmsEndpoint(CmsEndpointKind.Editor)]
internal sealed class ArchivePage(CmsEngineDbContext ctx, ICmsChangeHandler changeHandler, ILogger<ArchivePage> logger)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        this.Post("/pages/{id}/archive");
        this.Description(builder => builder.Produces<PageLifecycleConflict>(StatusCodes.Status409Conflict));
        this.Summary(summary =>
        {
            summary.Summary = "Archives an unpublished page with no unarchived children, retaining its content and history.";
            summary.Response(StatusCodes.Status204NoContent, "The page is archived.");
            summary.Response(StatusCodes.Status404NotFound, "The page was not found.");
            summary.Response<PageLifecycleConflict>(StatusCodes.Status409Conflict, "The page is published, has unarchived children, or is referenced by menus.");
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var pageId = this.Route<Guid>("id");
        var stopwatch = Stopwatch.StartNew();
        await using var transaction = await ctx.Database.BeginTransactionAsync(ct);
        await PagePublicationGuard.LockAsync(ctx, ct);
        await CmsLocks.PageAsync(ctx.Database, pageId, ct);
        var page = await ctx.Pages.SingleOrDefaultAsync(page => page.Id == pageId, ct);
        if (page is null)
        {
            await this.Send.NotFoundAsync(ct);
            return;
        }

        if (page.ArchivedAt.HasValue)
        {
            await this.Send.NoContentAsync(ct);
            return;
        }

        if (page.IsPublished)
        {
            await this.SendConflictAsync(new("page-published", "Unpublish this page before archiving.", 0, []), ct);
            return;
        }

        var children = ctx.Pages.Where(child => child.ParentId == pageId && child.ArchivedAt == null);
        var count = await children.CountAsync(ct);
        if (count > 0)
        {
            var pages = await children.OrderBy(child => child.Path).Take(3)
                .Select(child => new PageLifecycleConflictPage(child.Id, child.Path)).ToArrayAsync(ct);
            await this.SendConflictAsync(new("page-has-children", "Reparent or archive the child pages first.", count, pages), ct);
            return;
        }

        var (menuCount, menus) = await PagePublicationGuard.GetReferencingMenusAsync(ctx, pageId, ct);
        if (menuCount > 0)
        {
            await this.SendConflictAsync(new("page-referenced", "Remove these menu references before archiving.", 0, [], menuCount, menus), ct);
            return;
        }

        page.ArchivedAt = DateTimeOffset.UtcNow;
        await ctx.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        await changeHandler.ContentChangedAsync();
        logger.ArchivePageEnd(pageId, stopwatch.ElapsedMilliseconds);
        await this.Send.NoContentAsync(ct);
    }

    private async Task SendConflictAsync(PageLifecycleConflict conflict, CancellationToken ct)
    {
        this.HttpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        await this.HttpContext.Response.WriteAsJsonAsync(conflict, ct);
    }
}

public sealed record PageLifecycleConflict(
    string Code,
    string Message,
    int TotalPages,
    IReadOnlyList<PageLifecycleConflictPage> Pages,
    int TotalMenus = 0,
    IReadOnlyList<UnpublishMenuReference>? Menus = null);

public sealed record PageLifecycleConflictPage(Guid PageId, string Path);
