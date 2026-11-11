#pragma warning disable SA1402 // File may only contain a single type
namespace ConradSix.C4M.Endpoints.Page;

using ConradSix.C4M;
using ConradSix.C4M.Services;

using FastEndpoints;

using Microsoft.AspNetCore.Http;

[CmsEndpoint(CmsEndpointKind.Editor)]
internal class GetPagePath(IPageQueryService pageQueryService)
    : EndpointWithoutRequest<GetPagePathResponse>
{
    public override void Configure()
    {
        this.Get("/pages/{id}/path");
        this.Description(x => x.ProducesProblemFE(StatusCodes.Status404NotFound));
        this.Summary(s =>
        {
            s.Summary = "Gets a page path by ID.";
            s.Description = "Retrieves just the path of a specific page by its unique identifier. This is a performance-optimized endpoint that returns only the path without loading the full page data.";
            s.Response<GetPagePathResponse>(StatusCodes.Status200OK, "Page path was successfully retrieved.");
            s.Response(StatusCodes.Status404NotFound, "The page was not found.");
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var pageId = this.Route<Guid?>("id");
        if (pageId == null)
        {
            await this.Send.NotFoundAsync(ct);
            return;
        }

        var path = await pageQueryService.GetPagePathByIdAsync(pageId.Value, ct);

        if (path == null)
        {
            await this.Send.NotFoundAsync(ct);
            return;
        }

        await this.Send.OkAsync(new GetPagePathResponse(path), ct);
    }
}

public sealed record GetPagePathResponse(string Path);
