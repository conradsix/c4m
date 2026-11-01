#pragma warning disable SA1402 // File may only contain a single type
namespace ConradSix.C4M.Endpoints.Page;

using System.Diagnostics;

using ConradSix.C4M;
using ConradSix.C4M.Services;

using FastEndpoints;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

[CmsEndpoint(CmsEndpointKind.Editor)]
internal class GetPage(IPageQueryService pageQueryService, ILogger<GetPage> logger)
    : Endpoint<GetPageRequest, GetPageResponse>
{
    public override void Configure()
    {
        this.Get("/pages/{id}");
        this.Description(x => x.ProducesProblemFE(StatusCodes.Status404NotFound));
        this.Summary(s =>
        {
            s.Summary = "Gets a page by ID.";
            s.Description = "Retrieves a specific page by its unique identifier including all related blocks. Optionally specify a version ID to get a specific version, otherwise returns the current version.";
            s.Response<GetPageResponse>(StatusCodes.Status200OK, "Page was successfully retrieved.");
            s.Response(StatusCodes.Status404NotFound, "The page was not found or the specified version does not exist.");
        });
    }

    public override async Task HandleAsync(GetPageRequest req, CancellationToken ct)
    {
        var pageId = this.Route<Guid?>("id");
        var stopwatch = Stopwatch.StartNew();
        try
        {
            if (pageId == null)
            {
                await this.Send.NotFoundAsync(ct);
                return;
            }

            var response = req.Latest
                ? await pageQueryService.GetPageByIdLatestAsync(pageId.Value, ct, req.IncludeArchived)
                : await pageQueryService.GetPageByIdCurrentAsync(pageId.Value, ct);

            if (response == null)
            {
                logger.GetPageEndpointNotFound(pageId.Value);
                await this.Send.NotFoundAsync(ct);
                return;
            }

            if (req.IncludeBreadcrumbs)
            {
                var breadcrumbs = await pageQueryService.GetPageBreadcrumbsAsync(pageId.Value, ct, req.Latest && req.IncludeArchived);
                response = response with { Breadcrumbs = breadcrumbs };
            }

            await this.Send.OkAsync(response, ct);
            logger.GetPageEndpointCompleted(pageId.Value, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            logger.EndpointError(ex, nameof(GetPage));
            throw;
        }
    }
}

public sealed record GetPageRequest(bool Latest = false, bool IncludeBreadcrumbs = false, bool IncludeArchived = false);

public sealed record PageBreadcrumbItem(
    Guid PageId,
    string Slug,
    string Path);

public sealed record GetPageResponse(
    Guid PageId,
    Guid PageVersionId,
    Guid? ParentId,
    Guid? NavigationMenuId,
    Guid? EffectiveNavigationMenuId,
    string Slug,
    string Path,
    uint Version,
    PageSeoResponse Seo,
    IReadOnlyList<GetPageResponseBlock> Blocks,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<PageBreadcrumbItem>? Breadcrumbs = null,
    bool IsPublished = false,
    Guid? CurrentPageVersionId = null,
    Guid? LatestPageVersionId = null,
    DateTimeOffset? ArchivedAt = null,
    IReadOnlyList<PageReferenceResponse>? References = null);

// Something the version's blocks point at, in the order it first appears on the page.
public sealed record PageReferenceResponse(string Kind, Guid TargetId);

public sealed record PageSeoResponse(
    string? Title,
    string? Description,
    GetPageResponseBlockImage? SocialImage,
    bool Index,
    bool Follow);

public sealed record PageSeoRequest(
    string? Title,
    string? Description,
    Guid? SocialImageId,
    bool Index = true,
    bool Follow = true);

public sealed record GetPageResponseBlockImage(
    Guid Id,
    int Width,
    int Height,
#pragma warning disable CA1054 // URI-like parameters should not be strings
#pragma warning disable CA1056 // URI-like properties should not be strings
    string ImageUrl, // Direct Images service master URL.
#pragma warning restore CA1056 // URI-like properties should not be strings
#pragma warning restore CA1054 // URI-like parameters should not be strings
    string AltText);

public sealed record GetPageResponseBlockPage(
    Guid Id,
    string Slug,
    string Path,
    string Title,
    GetPageResponseBlockImage? HeroImage);

public sealed record GetPageResponseBlock(
    Guid Id,
    Guid BlockTypeId,
    string BlockName,
    string BlockVersion,
    string BlockData,
    IReadOnlyList<GetPageResponseBlockImage> Images,
    IReadOnlyList<GetPageResponseBlockPage> Pages,
    Guid InstanceId = default);
