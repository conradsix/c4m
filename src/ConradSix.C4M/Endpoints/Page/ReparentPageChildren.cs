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
internal sealed class ReparentPageChildren(CmsEngineDbContext ctx, ICmsChangeHandler changeHandler, CmsPathPolicy pathPolicy, ILogger<ReparentPageChildren> logger)
    : Endpoint<ReparentPageChildrenRequest, ReparentPageChildrenResponse>
{
    public override void Configure()
    {
        this.Post("/pages/{id}/reparent-children");
        this.Description(builder => builder.Produces<PageLifecycleConflict>(StatusCodes.Status409Conflict));
        this.Summary(summary =>
        {
            summary.Summary = "Moves all unarchived direct children and their subtrees to another parent or the top level.";
            summary.Response(StatusCodes.Status404NotFound, "The source page was not found.");
            summary.Response<PageLifecycleConflict>(StatusCodes.Status409Conflict, "The destination or resulting paths are invalid.");
        });
    }

    public override async Task HandleAsync(ReparentPageChildrenRequest req, CancellationToken ct)
    {
        var pageId = this.Route<Guid>("id");
        var stopwatch = Stopwatch.StartNew();
        await using var transaction = await ctx.Database.BeginTransactionAsync(ct);
        await PagePublicationGuard.LockAsync(ctx, ct);
        var source = await ctx.Pages.AsNoTracking().SingleOrDefaultAsync(page => page.Id == pageId && page.ArchivedAt == null, ct);
        if (source is null)
        {
            await this.Send.NotFoundAsync(ct);
            return;
        }

        var destinationPath = string.Empty;
        var ancestorId = req.ParentId;
        while (ancestorId.HasValue)
        {
            if (ancestorId == pageId)
            {
                await this.SendConflictAsync(new("invalid-parent", "Choose a parent outside this page's subtree.", 0, []), ct);
                return;
            }

            var ancestor = await ctx.Pages.AsNoTracking().SingleOrDefaultAsync(page => page.Id == ancestorId && page.ArchivedAt == null, ct);
            if (ancestor is null)
            {
                await this.SendConflictAsync(new("invalid-parent", "The selected parent page does not exist.", 0, []), ct);
                return;
            }

            if (ancestorId == req.ParentId)
            {
                destinationPath = ancestor.Path;
            }

            ancestorId = ancestor.ParentId;
        }

        var children = await ctx.Pages.Where(page => page.ParentId == pageId && page.ArchivedAt == null).OrderBy(page => page.Id).ToArrayAsync(ct);
        var subtreeIds = await ctx.Database.SqlQuery<Guid>($"""
            WITH RECURSIVE subtree AS (
                SELECT id FROM cms.pages WHERE parent_id = {pageId} AND archived_at IS NULL
                UNION ALL
                SELECT child.id FROM cms.pages child JOIN subtree parent ON child.parent_id = parent.id
            )
            SELECT id AS "Value" FROM subtree
            """).ToArrayAsync(ct);
        var activeSubtree = await ctx.Pages.AsNoTracking().Where(page => subtreeIds.Contains(page.Id) && page.ArchivedAt == null)
            .Select(page => new { page.Id, page.Path, page.IsPublished }).ToArrayAsync(ct);
        var proposedPaths = activeSubtree.Select(page => new PageLifecycleConflictPage(page.Id, destinationPath + page.Path[source.Path.Length..])).ToArray();
        var reserved = proposedPaths.Where(page => pathPolicy.IsReserved(page.Path)).ToArray();
        if (reserved.Length > 0)
        {
            await this.SendConflictAsync(new("reserved-path", "These destination paths are reserved.", reserved.Length, [.. reserved.Take(3)]), ct);
            return;
        }

        var paths = proposedPaths.Select(page => page.Path).ToArray();
        var collisions = ctx.Pages.Where(page => page.ArchivedAt == null && paths.Contains(page.Path) && !subtreeIds.Contains(page.Id));
        var collisionCount = await collisions.CountAsync(ct);
        if (collisionCount > 0)
        {
            var pages = await collisions.OrderBy(page => page.Path).Take(3).Select(page => new PageLifecycleConflictPage(page.Id, page.Path)).ToArrayAsync(ct);
            await this.SendConflictAsync(new("path-conflict", "These destination paths are already in use.", collisionCount, pages), ct);
            return;
        }

        if (activeSubtree.Any(page => page.IsPublished))
        {
            var unpublished = await PagePublicationGuard.GetUnpublishedAncestorsAsync(ctx, req.ParentId, ct);
            if (unpublished.Count > 0)
            {
                var blockers = ctx.Pages.Where(page => page.ArchivedAt == null && unpublished.Contains(page.Path));
                var pages = await blockers.OrderBy(page => page.Path).Take(3).Select(page => new PageLifecycleConflictPage(page.Id, page.Path)).ToArrayAsync(ct);
                await this.SendConflictAsync(new("unpublished-ancestors", "Publish the destination ancestors before moving published pages beneath them.", unpublished.Count, pages), ct);
                return;
            }
        }

        foreach (var child in children)
        {
            child.ParentId = req.ParentId;
        }

        await ctx.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        await changeHandler.ContentChangedAsync();
        logger.ReparentPageChildrenEnd(pageId, children.Length, stopwatch.ElapsedMilliseconds);
        await this.Send.OkAsync(new ReparentPageChildrenResponse(children.Length), ct);
    }

    private async Task SendConflictAsync(PageLifecycleConflict conflict, CancellationToken ct)
    {
        this.HttpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        await this.HttpContext.Response.WriteAsJsonAsync(conflict, ct);
    }
}

public sealed record ReparentPageChildrenRequest(Guid? ParentId);

public sealed record ReparentPageChildrenResponse(int MovedPages);
