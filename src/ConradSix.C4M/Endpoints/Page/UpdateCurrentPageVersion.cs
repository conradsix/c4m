#pragma warning disable SA1402 // File may only contain a single type
namespace Parapet.CMS.Engine.Endpoints.Page;

using FastEndpoints;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

using Parapet.CMS.Engine;
using Parapet.CMS.Engine.Data;
using Parapet.CMS.Engine.Services;

[CmsEndpoint(CmsEndpointKind.Editor)]
internal class UpdateCurrentPageVersion(IPageVersionServices pageVersionServices, CmsEngineDbContext ctx, ICmsChangeHandler changeHandler)
    : Endpoint<PagePublicationRequest, UpdateCurrentPageVersionResponse>
{
    public override void Configure()
    {
        this.Put("/pages/by-version/{id}");
        this.Description(x => x.ProducesProblemFE(StatusCodes.Status404NotFound));
        this.Summary(s =>
        {
            s.Summary = "Sets a page version as current.";
            s.Description = "Sets the specified page version as the current version and unsets the previous current version.";
            s.Response<UpdateCurrentPageVersionResponse>(StatusCodes.Status200OK, "Page version was successfully set as current.");
            s.Response(StatusCodes.Status404NotFound, "The page version was not found.");
        });
    }

    public override async Task HandleAsync(PagePublicationRequest req, CancellationToken ct)
    {
        var pageVersionId = this.Route<Guid>("id");

        try
        {
            await using var transaction = await ctx.Database.BeginTransactionAsync(ct);
            await PagePublicationGuard.LockAsync(ctx, ct);
            var pageId = await ctx.PageVersions.Where(version => version.Id == pageVersionId && version.Page.ArchivedAt == null)
                .Select(version => (Guid?)version.PageId).SingleOrDefaultAsync(ct);
            if (pageId is null)
            {
                await this.Send.NotFoundAsync(ct);
                return;
            }

            var page = await ctx.Pages.SingleAsync(page => page.Id == pageId, ct);
            var current = await ctx.PageVersions.Where(version => version.PageId == pageId && version.IsCurrent == true)
                .Select(version => (Guid?)version.Id).SingleOrDefaultAsync(ct);
            if (current != req.ExpectedCurrentPageVersionId || page.IsPublished != req.ExpectedIsPublished)
            {
                await this.Send.StatusCodeAsync(StatusCodes.Status409Conflict, ct);
                return;
            }

            var publicationErrors = page.IsPublished
                ? await pageVersionServices.ValidateForPublicationAsync(pageVersionId, ct)
                : [];
            if (publicationErrors.Count > 0)
            {
                foreach (var publicationError in publicationErrors)
                {
                    this.AddError(publicationError);
                }

                await this.Send.ErrorsAsync(cancellation: ct);
                return;
            }

            var previousPageVersionId = await pageVersionServices.SetCurrentPageVersionAsync(pageVersionId, ct);
            await transaction.CommitAsync(ct);
            if (page.IsPublished)
            {
                await changeHandler.PageChangedAsync(page.Path);
            }

            await this.Send.OkAsync(new UpdateCurrentPageVersionResponse(previousPageVersionId), ct);
        }
        catch (InvalidOperationException)
        {
            await this.Send.NotFoundAsync(ct);
        }
    }
}

public sealed record UpdateCurrentPageVersionResponse(Guid PreviousPageVersionId);
