#pragma warning disable SA1402 // File may only contain a single type
namespace ConradSix.C4M.Endpoints.Page;

using System.Diagnostics;
using System.Text.RegularExpressions;

using ConradSix.C4M;
using ConradSix.C4M.Services;

using FastEndpoints;

using FluentValidation;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

[CmsEndpoint(CmsEndpointKind.Editor)]
internal class ListPages(IPageQueryService pageQueryService, ICmsPageReader reader, ILogger<ListPages> logger)
    : Endpoint<ListPagesRequest, ListPagesResponse>
{
    public override void Configure()
    {
        this.Get("/pages");
        this.Summary(s =>
        {
            s.Summary = "Lists pages with pagination, sorting, and filtering.";
            s.Description = "Retrieves a paginated list of pages with support for sorting, searching by SEO title or path, and filtering by slug, path, and creation/update dates.";
            s.Response<ListPagesResponse>(StatusCodes.Status200OK, "Pages were successfully retrieved.");
            s.Response(StatusCodes.Status400BadRequest, "The request is invalid.");
        });
    }

    public override async Task HandleAsync(ListPagesRequest req, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            // Links in are worked out across every page, so that sort pages through the whole filtered list here.
            var byLinks = req.Sort is ListPagesSort.LinksInAsc or ListPagesSort.LinksInDesc;
            var listed = await pageQueryService.ListPagesAsync(
                byLinks ? req with { Sort = ListPagesSort.PathAsc, Offset = 0, Limit = int.MaxValue } : req,
                ct);
            var links = await reader.GetLinkCountsAsync(ct);
            var pages = listed.Pages.Select(page => WithLinks(page, links.GetValueOrDefault(page.PageId))).ToList();
            var response = listed with { Pages = pages };
            if (byLinks)
            {
                var ordered = req.Sort == ListPagesSort.LinksInAsc
                    ? pages.OrderBy(LinksIn)
                    : pages.OrderByDescending(LinksIn);
                response = new ListPagesResponse(
                    listed.Total,
                    req.Limit,
                    req.Offset + req.Limit < listed.Total ? req.Offset + req.Limit : null,
                    [.. ordered.ThenBy(page => page.Path, StringComparer.Ordinal).Skip(req.Offset).Take(req.Limit)]);
            }

            await this.Send.OkAsync(response, ct);
            logger.ListPagesCompleted(stopwatch.ElapsedMilliseconds, response.Pages.Count);
        }
        catch (Exception ex)
        {
            logger.EndpointError(ex, nameof(ListPages));
            throw;
        }
    }

    private static int LinksIn(ListPagesResponseItem page) => page.LinksFromPages + page.LinksFromMenus;

    // Archived pages keep no links.
    private static ListPagesResponseItem WithLinks(ListPagesResponseItem page, CmsLinkCounts? links) =>
        links is null || page.ArchivedAt is not null
            ? page
            : page with { LinksFromPages = links.FromPages, LinksFromMenus = links.FromMenus };
}

internal class ListPagesRequestValidator : Validator<ListPagesRequest>
{
    public ListPagesRequestValidator()
    {
        this.RuleFor(r => r.Status).IsInEnum();

        this.RuleFor(r => r.Limit)
            .InclusiveBetween(1, 100);

        this.RuleFor(r => r.Offset)
            .GreaterThanOrEqualTo(0);

        this.RuleFor(r => r.SlugFilter)
            .Must(BeValidRegexWhenRegexType)
            .WithMessage("Invalid regex pattern for slug filter.")
            .When(r => r.SlugFilterType == ListPagesStringFilterType.Regex);

        this.RuleFor(r => r.PathFilter)
            .Must(BeValidRegexWhenRegexType)
            .WithMessage("Invalid regex pattern for path filter.")
            .When(r => r.PathFilterType == ListPagesStringFilterType.Regex);

        this.RuleFor(r => r.CreatedBefore)
            .GreaterThan(r => r.CreatedAfter)
            .WithMessage("CreatedBefore must be after CreatedAfter.")
            .When(r => r.CreatedAfter.HasValue && r.CreatedBefore.HasValue);

        this.RuleFor(r => r.UpdatedBefore)
            .GreaterThan(r => r.UpdatedAfter)
            .WithMessage("UpdatedBefore must be after UpdatedAfter.")
            .When(r => r.UpdatedAfter.HasValue && r.UpdatedBefore.HasValue);
    }

    private static bool BeValidRegexWhenRegexType(ListPagesRequest request, string? filter)
    {
        if (string.IsNullOrEmpty(filter))
        {
            return true;
        }

        try
        {
            _ = new Regex(filter);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}

public record ListPagesRequest(
    int Limit = 20,
    int Offset = 0,
    ListPagesSort Sort = ListPagesSort.SlugAsc,
    string? SlugFilter = null,
    ListPagesStringFilterType SlugFilterType = ListPagesStringFilterType.Contains,
    string? PathFilter = null,
    ListPagesStringFilterType PathFilterType = ListPagesStringFilterType.Contains,
    DateTimeOffset? CreatedAfter = null,
    DateTimeOffset? CreatedBefore = null,
    DateTimeOffset? UpdatedAfter = null,
    DateTimeOffset? UpdatedBefore = null,
    string? Search = null,
    Guid? ParentId = null,
    bool PublishedOnly = false,
    ListPagesStatus Status = ListPagesStatus.NotArchived);

public record ListPagesResponse(
    int Total,
    int Limit,
    int? NextOffset,
    IReadOnlyList<ListPagesResponseItem> Pages);

public record ListPagesResponseItem(
    Guid PageId,
    Guid? ParentPageId,
    Guid? CurrentPageVersionId,
    string Slug,
    string Path,
    int VersionCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsPublished = false,
    string? SeoTitle = null,
    DateTimeOffset? ArchivedAt = null,
    int LinksFromPages = 0,
    int LinksFromMenus = 0);

#pragma warning disable SA1201 // Elements should appear in the correct order
public enum ListPagesSort
#pragma warning restore SA1201 // Elements should appear in the correct order
{
    SlugAsc,
    SlugDesc,
    PathAsc,
    PathDesc,
    CreatedAtAsc,
    CreatedAtDesc,
    UpdatedAtAsc,
    UpdatedAtDesc,
    SeoTitleAsc,
    SeoTitleDesc,
    PublishedAsc,
    PublishedDesc,
    StatusAsc,
    StatusDesc,
    LinksInAsc,
    LinksInDesc,
}

public enum ListPagesStringFilterType
{
    Contains,
    StartsWith,
    EndsWith,
    Regex,
}

public enum ListPagesStatus
{
    NotArchived,
    Published,
    Unpublished,
    Archived,
    All,
}
