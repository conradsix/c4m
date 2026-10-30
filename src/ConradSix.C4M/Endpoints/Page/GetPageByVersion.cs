#pragma warning disable SA1402 // File may only contain a single type
namespace ConradSix.C4M.Endpoints.Page;

using ConradSix.C4M;
using ConradSix.C4M.Services;

using FastEndpoints;

using Microsoft.AspNetCore.Http;

[CmsEndpoint(CmsEndpointKind.Editor)]
internal class GetPageByVersion(IPageQueryService pageQueryService)
    : Endpoint<GetPageByVersionRequest, GetPageResponse>
{
    public override void Configure()
    {
        this.Get("/pages/by-version/{id}");
        this.Description(x => x.ProducesProblemFE(StatusCodes.Status404NotFound));
        this.Summary(s =>
        {
            s.Summary = "Gets a page by version.";
            s.Description = "Retrieves a specific page by its version identifier including all related blocks.";
            s.Response<GetPageResponse>(StatusCodes.Status200OK, "Page was successfully retrieved.");
            s.Response(StatusCodes.Status404NotFound, "The page was not found or the specified version does not exist.");
        });
    }

    public override async Task HandleAsync(GetPageByVersionRequest req, CancellationToken ct)
    {
        var pageVersionId = this.Route<Guid>("id");

        var response = await pageQueryService.GetPageByVersionIdAsync(pageVersionId, ct, req.IncludeArchived);
        if (response == null)
        {
            await this.Send.NotFoundAsync(ct);
            return;
        }

        if (req.IncludeBreadcrumbs)
        {
            var breadcrumbs = await pageQueryService.GetPageBreadcrumbsAsync(response.PageId, ct, req.IncludeArchived);
            response = response with { Breadcrumbs = breadcrumbs };
        }

        await this.Send.OkAsync(response, ct);
    }
}

public sealed record GetPageByVersionRequest(bool IncludeBreadcrumbs = false, bool IncludeArchived = false);
