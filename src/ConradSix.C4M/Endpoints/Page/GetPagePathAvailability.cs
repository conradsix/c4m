#pragma warning disable SA1402 // File may only contain a single type
namespace ConradSix.C4M.Endpoints.Page;

using System.Diagnostics;

using ConradSix.C4M;
using ConradSix.C4M.Data;

using FastEndpoints;

using FluentValidation;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

[CmsEndpoint(CmsEndpointKind.Editor)]
internal class GetPagePathAvailability(CmsEngineDbContext ctx, CmsPathPolicy pathPolicy, ILogger<GetPagePathAvailability> logger)
    : Endpoint<GetPagePathAvailabilityRequest, GetPagePathAvailabilityResponse>
{
    public override void Configure()
    {
        this.Get("/pages/path-availability");
        this.Summary(s =>
        {
            s.Summary = "Checks whether a page path is available.";
            s.Description = "Resolves the path a page would have with the given parent and slug, and reports whether that path can be used. Pass the ID of a page being edited to exclude it from the check.";
            s.Response<GetPagePathAvailabilityResponse>(StatusCodes.Status200OK, "Path availability was successfully checked.");
            s.Response(StatusCodes.Status400BadRequest, "The slug or IDs were invalid.");
        });
    }

    public override async Task HandleAsync(GetPagePathAvailabilityRequest req, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var response = await this.CheckAsync(req, ct);
            await this.Send.OkAsync(response, ct);
            logger.GetPagePathAvailabilityCompleted(response.Path ?? string.Empty, response.Status.ToString(), stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            logger.EndpointError(ex, nameof(GetPagePathAvailability));
            throw;
        }
    }

    private async Task<GetPagePathAvailabilityResponse> CheckAsync(GetPagePathAvailabilityRequest req, CancellationToken ct)
    {
        var parentPath = string.Empty;
        if (req.ParentId is { } parentId)
        {
            var resolvedParentPath = await this.GetPathAsync(parentId, ct);
            if (resolvedParentPath is null)
            {
                return new GetPagePathAvailabilityResponse(null, PagePathAvailability.ParentNotFound);
            }

            parentPath = resolvedParentPath;
        }

        var path = $"{parentPath}/{req.Slug}";

        if (req.ParentId is not null && req.PageId is { } pageId)
        {
            var pagePath = await this.GetPathAsync(pageId, ct);
            if (req.ParentId == pageId || (pagePath is not null && parentPath.StartsWith(pagePath + "/", StringComparison.Ordinal)))
            {
                return new GetPagePathAvailabilityResponse(path, PagePathAvailability.CircularParent);
            }
        }

        if (pathPolicy.IsReserved(path))
        {
            return new GetPagePathAvailabilityResponse(path, PagePathAvailability.Restricted);
        }

        var conflicts = ctx.Pages.AsNoTracking().Where(page => page.Path == path && page.ArchivedAt == null);
        if (req.PageId is { } excludedPageId)
        {
            conflicts = conflicts.Where(page => page.Id != excludedPageId);
        }

        var status = await conflicts.AnyAsync(ct) ? PagePathAvailability.Taken : PagePathAvailability.Available;
        return new GetPagePathAvailabilityResponse(path, status);
    }

    private Task<string?> GetPathAsync(Guid pageId, CancellationToken ct) => ctx.Pages
        .AsNoTracking()
        .Where(page => page.Id == pageId && page.ArchivedAt == null)
        .Select(page => page.Path)
        .SingleOrDefaultAsync(ct);
}

public sealed record GetPagePathAvailabilityRequest(string Slug, Guid? ParentId = null, Guid? PageId = null);

public sealed record GetPagePathAvailabilityResponse(string? Path, PagePathAvailability Status);

#pragma warning disable SA1201 // Elements should appear in the correct order
public enum PagePathAvailability
{
    Available,
    Taken,
    Restricted,
    ParentNotFound,
    CircularParent,
}

internal sealed class GetPagePathAvailabilityRequestValidator : Validator<GetPagePathAvailabilityRequest>
{
    public GetPagePathAvailabilityRequestValidator()
    {
        this.RuleFor(r => r.Slug).NotEmpty().Slug();
        this.RuleFor(r => r.ParentId).NotEqual(Guid.Empty).When(r => r.ParentId.HasValue);
        this.RuleFor(r => r.PageId).NotEqual(Guid.Empty).When(r => r.PageId.HasValue);
    }
}
