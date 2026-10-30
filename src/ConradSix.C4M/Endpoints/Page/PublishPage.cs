#pragma warning disable SA1402 // Endpoint contracts are kept with their endpoint.
namespace Parapet.CMS.Engine.Endpoints.Page;

using FastEndpoints;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

using Parapet.CMS.Engine;
using Parapet.CMS.Engine.Data;
using Parapet.CMS.Engine.Services;

[CmsEndpoint(CmsEndpointKind.Editor)]
internal sealed class PublishPage(CmsEngineDbContext ctx, ICmsChangeHandler changeHandler, IPageVersionServices versions) : Endpoint<PagePublicationRequest>
{
    public override void Configure()
    {
        this.Post("/pages/{id}/publish");
        this.Description(builder => builder.Produces(StatusCodes.Status409Conflict));
        this.Summary(summary => summary.Summary = "Publishes the saved Current version of a page.");
    }

    public override async Task HandleAsync(PagePublicationRequest req, CancellationToken ct)
    {
        var pageId = this.Route<Guid>("id");
        await using var transaction = await ctx.Database.BeginTransactionAsync(ct);
        await PagePublicationGuard.LockAsync(ctx, ct);
        var page = await ctx.Pages.SingleOrDefaultAsync(page => page.Id == pageId && page.ArchivedAt == null, ct);
        if (page is null)
        {
            await this.Send.NotFoundAsync(ct);
            return;
        }

        var current = await ctx.PageVersions.Where(version => version.PageId == pageId && version.IsCurrent == true)
            .Select(version => (Guid?)version.Id).SingleOrDefaultAsync(ct);
        if (current != req.ExpectedCurrentPageVersionId || page.IsPublished != req.ExpectedIsPublished)
        {
            await this.Send.StatusCodeAsync(StatusCodes.Status409Conflict, ct);
            return;
        }

        if (current is null)
        {
            this.AddError("Select a Current version before publishing.");
            await this.Send.ErrorsAsync(cancellation: ct);
            return;
        }

        var errors = await versions.ValidateForPublicationAsync(current.Value, ct);
        if (errors.Count > 0)
        {
            foreach (var error in errors)
            {
                this.AddError(error);
            }

            await this.Send.ErrorsAsync(cancellation: ct);
            return;
        }

        page.IsPublished = true;
        await ctx.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        await changeHandler.PageChangedAsync(page.Path);
        await this.Send.NoContentAsync(ct);
    }
}

public sealed record PagePublicationRequest(Guid? ExpectedCurrentPageVersionId, bool ExpectedIsPublished);
