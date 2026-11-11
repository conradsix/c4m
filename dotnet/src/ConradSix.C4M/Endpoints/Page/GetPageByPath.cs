#pragma warning disable SA1402 // File may only contain a single type
namespace ConradSix.C4M.Endpoints.Page;

using System.Diagnostics;

using ConradSix.C4M;
using ConradSix.C4M.Services;

using FastEndpoints;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

[CmsEndpoint(CmsEndpointKind.Delivery)]
internal class GetPageByPath(IPageQueryService pageQueryService, ILogger<GetPageByPath> logger)
    : Endpoint<GetPageByPathRequest, GetPageResponse>
{
    public override void Configure()
    {
        this.Get("/pages/by-path");
        this.Description(x => x.ProducesProblemFE(StatusCodes.Status404NotFound));
        this.Summary(s =>
        {
            s.Summary = "Gets a page by path.";
            s.Description = "Retrieves a specific page by path including all related blocks. Returns the current version of the page.";
            s.Response<GetPageResponse>(StatusCodes.Status200OK, "Page was successfully retrieved.");
            s.Response(StatusCodes.Status404NotFound, "The page was not found for the specified path.");
        });
    }

    public override async Task HandleAsync(GetPageByPathRequest req, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var response = await pageQueryService.GetPageByPathAsync(req.Path, ct);
            if (response == null)
            {
                logger.GetPageByPathNotFound(req.Path);
                await this.Send.NotFoundAsync(ct);
                return;
            }

            await this.Send.OkAsync(response, ct);
            logger.GetPageByPathCompleted(req.Path, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            logger.EndpointError(ex, nameof(GetPageByPath));
            throw;
        }
    }
}

public sealed record GetPageByPathRequest(string Path);
