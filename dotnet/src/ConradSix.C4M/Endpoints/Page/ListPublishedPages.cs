#pragma warning disable SA1402 // File may only contain a single type
namespace ConradSix.C4M.Endpoints.Page;

using System.Diagnostics;

using ConradSix.C4M;
using ConradSix.C4M.Services;

using FastEndpoints;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

[CmsEndpoint(CmsEndpointKind.Delivery)]
internal class ListPublishedPages(IPageQueryService pageQueryService, ILogger<ListPublishedPages> logger)
    : EndpointWithoutRequest<ListPublishedPagesResponse>
{
    public override void Configure()
    {
        this.Get("/pages/published");
        this.Summary(s =>
        {
            s.Summary = "Lists every published page.";
            s.Description = "Retrieves the path, SEO title and robots index setting of every published page's current version in path order, for sitemaps and page directories.";
            s.Response<ListPublishedPagesResponse>(StatusCodes.Status200OK, "Published pages were successfully retrieved.");
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var response = await pageQueryService.ListPublishedPagesAsync(ct);
            await this.Send.OkAsync(response, ct);
            logger.ListPublishedPagesCompleted(stopwatch.ElapsedMilliseconds, response.Pages.Count);
        }
        catch (Exception ex)
        {
            logger.EndpointError(ex, nameof(ListPublishedPages));
            throw;
        }
    }
}

public sealed record ListPublishedPagesResponse(IReadOnlyList<PublishedPageItem> Pages);

// UpdatedAt is when the current version was saved, as GetPageByPath reports it.
public sealed record PublishedPageItem(
    Guid PageId,
    Guid? ParentPageId,
    string Path,
    string? SeoTitle,
    bool RobotsIndex,
    DateTimeOffset UpdatedAt);
