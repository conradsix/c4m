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
internal sealed class UnpublishPage(CmsEngineDbContext ctx, ICmsChangeHandler changeHandler, ILogger<UnpublishPage> logger)
    : Endpoint<PagePublicationRequest>
{
    public override void Configure()
    {
        this.Post("/pages/{id}/unpublish");
        this.Description(builder => builder.Produces<UnpublishPageConflict>(StatusCodes.Status409Conflict));
        this.Summary(summary =>
        {
            summary.Summary = "Unpublishes a page unless published pages or menus reference it.";
            summary.Response(StatusCodes.Status204NoContent, "The page is unpublished; its history is retained.");
            summary.Response(StatusCodes.Status404NotFound, "The page was not found.");
            summary.Response<UnpublishPageConflict>(StatusCodes.Status409Conflict, "References block unpublishing, or the publication state changed.");
        });
    }

    public override async Task HandleAsync(PagePublicationRequest req, CancellationToken ct)
    {
        var pageId = this.Route<Guid>("id");
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await using var transaction = await ctx.Database.BeginTransactionAsync(ct);
            await PagePublicationGuard.LockAsync(ctx, ct);
            var page = await ctx.Pages.SingleOrDefaultAsync(page => page.Id == pageId && page.ArchivedAt == null, ct);
            if (page is null)
            {
                await this.Send.NotFoundAsync(ct);
                return;
            }

            var live = await ctx.PageVersions.SingleOrDefaultAsync(version => version.PageId == pageId && version.IsCurrent == true, ct);
            if (live?.Id != req.ExpectedCurrentPageVersionId || page.IsPublished != req.ExpectedIsPublished)
            {
                await this.SendConflictAsync(new("publication-changed", "The page publication state or Current version changed. Refresh before unpublishing.", 0, [], 0, []), ct);
                return;
            }

            if (!page.IsPublished)
            {
                await this.Send.NoContentAsync(ct);
                return;
            }

            var path = page.Path;
            var descendantsPrefix = path + "/";
            var referencingVersions = ctx.PageVersions.Where(version => version.IsCurrent == true && version.Page.IsPublished && version.PageId != pageId &&
                (version.Page.Path.StartsWith(descendantsPrefix) || version.VersionBlocks.Any(block =>
                    block.PageBlock.References.Any(reference => reference.Kind == CmsReferenceKinds.Page && reference.TargetId == pageId))));
            var pageCount = await referencingVersions.CountAsync(ct);
            var pages = await referencingVersions.OrderBy(version => version.Page.Path).ThenBy(version => version.PageId)
                .Take(3)
                .Select(version => new UnpublishPageReference(
                    version.PageId,
                    version.Id,
                    version.Page.Path,
                    version.Page.Path.StartsWith(descendantsPrefix)
                        ? "Published descendant (breadcrumb link)"
                        : "Link in " + version.VersionBlocks
                            .Where(block => block.PageBlock.References.Any(reference => reference.Kind == CmsReferenceKinds.Page && reference.TargetId == pageId))
                            .OrderBy(block => block.SortOrder).ThenBy(block => block.PageBlockId)
                            .Select(block => block.PageBlock.BlockType.Name).First() + " block"))
                .ToArrayAsync(ct);

            var (menuCount, menus) = await PagePublicationGuard.GetReferencingMenusAsync(ctx, pageId, ct);
            if (pageCount > 0 || menuCount > 0)
            {
                await this.SendConflictAsync(new("page-referenced", "Remove these references before unpublishing.", pageCount, pages, menuCount, menus), ct);
                return;
            }

            page.IsPublished = false;
            await ctx.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            // Any published page's related links may point here.
            await changeHandler.ContentChangedAsync();
            logger.UnpublishPageEnd(pageId, stopwatch.ElapsedMilliseconds);
            await this.Send.NoContentAsync(ct);
        }
        catch (Exception ex)
        {
            logger.EndpointError(ex, nameof(UnpublishPage));
            throw;
        }
    }

    private async Task SendConflictAsync(UnpublishPageConflict conflict, CancellationToken ct)
    {
        this.HttpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        await this.HttpContext.Response.WriteAsJsonAsync(conflict, ct);
    }
}

public sealed record UnpublishPageConflict(
    string Code,
    string Message,
    int TotalPages,
    IReadOnlyList<UnpublishPageReference> Pages,
    int TotalMenus,
    IReadOnlyList<UnpublishMenuReference> Menus);

public sealed record UnpublishPageReference(Guid PageId, Guid PageVersionId, string Path, string Reason);

public sealed record UnpublishMenuReference(Guid MenuId, string Name, string ItemTitle);
