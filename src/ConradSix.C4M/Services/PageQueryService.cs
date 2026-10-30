namespace Parapet.CMS.Engine.Services;

#pragma warning disable SA1204 // Response mapping helpers are kept beside the query mapper.

using System.Globalization;
using System.Linq.Expressions;
using System.Text.RegularExpressions;

using Microsoft.EntityFrameworkCore;

using Parapet.CMS.Engine;
using Parapet.CMS.Engine.Data;
using Parapet.CMS.Engine.Data.Model;
using Parapet.CMS.Engine.Endpoints.Page;

internal class PageQueryService(
    CmsEngineDbContext ctx,
    IAssetResolver assetResolver,
    ILinkedPageImageSelector linkedPageImageSelector) : IPageQueryService
{
    public async Task<GetPageResponse?> GetPageByIdCurrentAsync(Guid pageId, CancellationToken ct)
    {
        var pageVersion = await this
            .GetQuery()
            .SingleOrDefaultAsync(pv => pv.PageId == pageId && pv.IsCurrent == true && pv.Page.IsPublished, ct);

        return await this.MapToResponseAsync(pageVersion, ct);
    }

    public async Task<GetPageResponse?> GetPageByIdLatestAsync(Guid pageId, CancellationToken ct, bool includeArchived = false)
    {
        var pageVersion = await this
            .GetQuery(includeArchived)
            .OrderByDescending(pv => pv.CreatedAt).ThenByDescending(pv => pv.Id)
            .FirstOrDefaultAsync(pv => pv.PageId == pageId, ct);

        return await this.MapToResponseAsync(pageVersion, ct);
    }

    public async Task<GetPageResponse?> GetPageByVersionIdAsync(Guid pageVersionId, CancellationToken ct, bool includeArchived = false)
    {
        var pageVersion = await this
            .GetQuery(includeArchived)
            .SingleOrDefaultAsync(pv => pv.Id == pageVersionId, ct);

        return await this.MapToResponseAsync(pageVersion, ct);
    }

    public async Task<GetPageResponse?> GetPageByPathAsync(string path, CancellationToken ct)
    {
        var pageVersion = await this
            .GetQuery()
            .SingleOrDefaultAsync(pv => pv.Page.Path == path && pv.IsCurrent == true && pv.Page.IsPublished, ct);

        return await this.MapToResponseAsync(pageVersion, ct);
    }

    public async Task<string?> GetPagePathByIdAsync(Guid pageId, CancellationToken ct) => await ctx.Pages
        .Where(p => p.Id == pageId && p.ArchivedAt == null)
        .Select(p => p.Path)
        .SingleOrDefaultAsync(ct);

    public async Task<ListPagesResponse> ListPagesAsync(ListPagesRequest request, CancellationToken ct)
    {
        var query = request.Status switch
        {
            ListPagesStatus.Published => ctx.Pages.Where(p => p.ArchivedAt == null && p.IsPublished),
            ListPagesStatus.Unpublished => ctx.Pages.Where(p => p.ArchivedAt == null && !p.IsPublished),
            ListPagesStatus.Archived => ctx.Pages.Where(p => p.ArchivedAt != null),
            ListPagesStatus.All => ctx.Pages.AsQueryable(),
            _ => ctx.Pages.Where(p => p.ArchivedAt == null),
        };
        if (request.PublishedOnly)
        {
            query = query.Where(p => p.ArchivedAt == null && p.IsPublished && p.Versions.Any(version => version.IsCurrent == true));
        }

        if (request.ParentId.HasValue)
        {
            query = query.Where(p => p.ParentId == request.ParentId);
        }

        // Apply string filters
        if (!string.IsNullOrEmpty(request.SlugFilter))
        {
            query = ApplyStringFilter(query, p => p.Slug, request.SlugFilter, request.SlugFilterType);
        }

        if (!string.IsNullOrEmpty(request.PathFilter))
        {
            query = ApplyStringFilter(query, p => p.Path, request.PathFilter, request.PathFilterType);
        }

        // Apply date filters
        if (request.CreatedAfter.HasValue)
        {
            query = query.Where(p => p.CreatedAt >= request.CreatedAfter.Value);
        }

        if (request.CreatedBefore.HasValue)
        {
            query = query.Where(p => p.CreatedAt <= request.CreatedBefore.Value);
        }

        if (request.UpdatedAfter.HasValue || request.UpdatedBefore.HasValue)
        {
            query = query.Where(p =>
                (!request.UpdatedAfter.HasValue || (p.Versions.Max(v => (DateTimeOffset?)v.CreatedAt) ?? p.CreatedAt) >= request.UpdatedAfter.Value) &&
                (!request.UpdatedBefore.HasValue || (p.Versions.Max(v => (DateTimeOffset?)v.CreatedAt) ?? p.CreatedAt) <= request.UpdatedBefore.Value));
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = "%" + request.Search.Trim()
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("%", "\\%", StringComparison.Ordinal)
                .Replace("_", "\\_", StringComparison.Ordinal) + "%";
            query = query.Where(p => EF.Functions.ILike(p.Path, search, "\\") ||
                EF.Functions.ILike(
                    p.Versions
                        .OrderByDescending(v => v.CreatedAt)
                        .ThenByDescending(v => v.Id)
                        .Select(v => v.SeoTitle)
                        .FirstOrDefault() ?? string.Empty,
                    search,
                    "\\"));
        }

        // Apply sorting. Paths compare segment by segment so children follow their parent, not siblings like /parent-other.
        var orderedQuery = request.Sort switch
        {
            ListPagesSort.SlugAsc => query.OrderBy(p => p.Slug),
            ListPagesSort.SlugDesc => query.OrderByDescending(p => p.Slug),
            ListPagesSort.PathAsc => query.OrderBy(p => EF.Functions.Collate(p.Path.Replace("/", "\u0001"), "C")),
            ListPagesSort.PathDesc => query.OrderByDescending(p => EF.Functions.Collate(p.Path.Replace("/", "\u0001"), "C")),
            ListPagesSort.CreatedAtAsc => query.OrderBy(p => p.CreatedAt),
            ListPagesSort.CreatedAtDesc => query.OrderByDescending(p => p.CreatedAt),
            ListPagesSort.UpdatedAtAsc => query.OrderBy(p => p.Versions.Max(v => (DateTimeOffset?)v.CreatedAt) ?? p.CreatedAt),
            ListPagesSort.UpdatedAtDesc => query.OrderByDescending(p => p.Versions.Max(v => (DateTimeOffset?)v.CreatedAt) ?? p.CreatedAt),
            ListPagesSort.SeoTitleAsc => query.OrderBy(p => p.Versions
                .OrderByDescending(v => v.CreatedAt).ThenByDescending(v => v.Id)
                .Select(v => v.SeoTitle).FirstOrDefault() ?? string.Empty),
            ListPagesSort.SeoTitleDesc => query.OrderByDescending(p => p.Versions
                .OrderByDescending(v => v.CreatedAt).ThenByDescending(v => v.Id)
                .Select(v => v.SeoTitle).FirstOrDefault() ?? string.Empty),
            ListPagesSort.StatusAsc => query.OrderBy(p => p.ArchivedAt != null ? 2 : p.IsPublished ? 0 : 1),
            ListPagesSort.StatusDesc => query.OrderByDescending(p => p.ArchivedAt != null ? 2 : p.IsPublished ? 0 : 1),
            ListPagesSort.PublishedAsc => query.OrderBy(p => p.IsPublished),
            ListPagesSort.PublishedDesc => query.OrderByDescending(p => p.IsPublished),
            _ => query.OrderBy(p => p.Slug),
        };

        // Ensure consistent secondary sorting
        var finalQuery = orderedQuery.ThenBy(p => p.Id);

        // Get total count before pagination
        var total = await finalQuery.CountAsync(ct);

        // Apply pagination and project to response
        var pages = await finalQuery
            .Skip(request.Offset)
            .Take(request.Limit)
            .Select(p => new
            {
                p.Id,
                p.ParentId,
                p.Slug,
                p.Path,
                p.CreatedAt,
                p.IsPublished,
                p.ArchivedAt,
                SeoTitle = p.Versions
                    .OrderByDescending(v => v.CreatedAt)
                    .ThenByDescending(v => v.Id)
                    .Select(v => v.SeoTitle)
                    .FirstOrDefault(),
                CurrentVersionId = p.Versions
                    .Where(v => v.IsCurrent == true)
                    .Select(v => (Guid?)v.Id)
                    .SingleOrDefault(),
                VersionCount = p.Versions.Count(),
                UpdatedAt = p.Versions.Max(v => (DateTimeOffset?)v.CreatedAt) ?? p.CreatedAt,
            })
            .ToListAsync(ct);

        var responseItems = pages.Select(p => new ListPagesResponseItem(
            p.Id,
            p.ParentId,
            p.CurrentVersionId,
            p.Slug,
            p.Path,
            p.VersionCount,
            p.CreatedAt,
            p.UpdatedAt,
            p.IsPublished,
            p.SeoTitle,
            p.ArchivedAt)).ToList();

        var nextOffset = request.Offset + request.Limit < total
            ? request.Offset + request.Limit
            : default(int?);

        return new ListPagesResponse(
            total,
            request.Limit,
            nextOffset,
            responseItems);
    }

    public async Task<GetPageHistoryResponse?> GetPageHistoryAsync(Guid pageId, CancellationToken ct, bool includeArchived = false)
    {
        var page = await ctx.Pages
            .Where(p => p.Id == pageId && (includeArchived || p.ArchivedAt == null))
            .Select(p => new PageHistoryMetadata(p.Id, p.ParentId, p.Slug, p.Path, p.NavigationMenuId, p.CreatedAt, p.ArchivedAt, p.NavigationMenu != null ? p.NavigationMenu.Name : null))
            .SingleOrDefaultAsync(ct);

        if (page is null)
        {
            return null;
        }

        var versionData = await ctx.PageVersions
            .Where(pv => pv.PageId == pageId)
            .Select(pv => new
            {
                pv.Id,
                pv.IsCurrent,
                pv.CreatedAt,
                pv.ChangeSummaryJson,
            })
            .OrderByDescending(v => v.CreatedAt)
            .ThenByDescending(v => v.Id)
            .ToListAsync(ct);

        var versions = versionData.Select(v => new GetPageHistoryResponseItem(
            v.Id,
            v.IsCurrent == true,
            PageVersionChangeSummaryService.Deserialize(v.ChangeSummaryJson),
            v.CreatedAt)).ToList();

        return new GetPageHistoryResponse(versions, page);
    }

    public async Task<IReadOnlyList<PageBreadcrumbItem>> GetPageBreadcrumbsAsync(Guid pageId, CancellationToken ct, bool includeArchived = false)
    {
        var breadcrumbs = new List<PageBreadcrumbItem>();
        var currentPageId = (Guid?)pageId;

        while (currentPageId.HasValue)
        {
            var page = await ctx.Pages
                .Where(p => p.Id == currentPageId.Value && (includeArchived || p.ArchivedAt == null))
                .Select(p => new { p.Id, p.Slug, p.Path, p.ParentId })
                .SingleOrDefaultAsync(ct);

            if (page == null)
            {
                break;
            }

            breadcrumbs.Insert(0, new PageBreadcrumbItem(page.Id, page.Slug, page.Path));
            currentPageId = page.ParentId;
        }

        return breadcrumbs;
    }

    private static IQueryable<PageEntity> ApplyStringFilter(
        IQueryable<PageEntity> query,
        Expression<Func<PageEntity, string>> propertySelector,
        string filterValue,
        ListPagesStringFilterType filterType) =>
        filterType switch
        {
            ListPagesStringFilterType.Contains => query.Where(BuildContainsExpression(propertySelector, filterValue)),
            ListPagesStringFilterType.StartsWith => query.Where(BuildStartsWithExpression(propertySelector, filterValue)),
            ListPagesStringFilterType.EndsWith => query.Where(BuildEndsWithExpression(propertySelector, filterValue)),
            ListPagesStringFilterType.Regex => query.Where(BuildRegexExpression(propertySelector, filterValue)),
            _ => query,
        };

    private static Expression<Func<PageEntity, bool>> BuildContainsExpression(
        Expression<Func<PageEntity, string>> propertySelector,
        string filterValue)
    {
        var parameter = propertySelector.Parameters[0];
        var property = propertySelector.Body;
        var containsMethod = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;
        var filterConstant = Expression.Constant(filterValue);
        var containsCall = Expression.Call(property, containsMethod, filterConstant);
        return Expression.Lambda<Func<PageEntity, bool>>(containsCall, parameter);
    }

    private static Expression<Func<PageEntity, bool>> BuildStartsWithExpression(
        Expression<Func<PageEntity, string>> propertySelector,
        string filterValue)
    {
        var parameter = propertySelector.Parameters[0];
        var property = propertySelector.Body;
        var startsWithMethod = typeof(string).GetMethod(nameof(string.StartsWith), [typeof(string)])!;
        var filterConstant = Expression.Constant(filterValue);
        var startsWithCall = Expression.Call(property, startsWithMethod, filterConstant);
        return Expression.Lambda<Func<PageEntity, bool>>(startsWithCall, parameter);
    }

    private static Expression<Func<PageEntity, bool>> BuildEndsWithExpression(
        Expression<Func<PageEntity, string>> propertySelector,
        string filterValue)
    {
        var parameter = propertySelector.Parameters[0];
        var property = propertySelector.Body;
        var endsWithMethod = typeof(string).GetMethod(nameof(string.EndsWith), [typeof(string)])!;
        var filterConstant = Expression.Constant(filterValue);
        var endsWithCall = Expression.Call(property, endsWithMethod, filterConstant);
        return Expression.Lambda<Func<PageEntity, bool>>(endsWithCall, parameter);
    }

    private static Expression<Func<PageEntity, bool>> BuildRegexExpression(
        Expression<Func<PageEntity, string>> propertySelector,
        string pattern)
    {
        var parameter = propertySelector.Parameters[0];
        var property = propertySelector.Body;
        var regexType = typeof(Regex);
        var isMatchMethod = regexType.GetMethod(nameof(Regex.IsMatch), [typeof(string), typeof(string)])!;
        var patternConstant = Expression.Constant(pattern);
        var isMatchCall = Expression.Call(isMatchMethod, property, patternConstant);
        return Expression.Lambda<Func<PageEntity, bool>>(isMatchCall, parameter);
    }

    private async Task<GetPageResponse?> MapToResponseAsync(
        PageVersionEntity? pageVersion,
        CancellationToken cancellationToken)
    {
        if (pageVersion is null)
        {
            return null;
        }

        var referencedPageIds = pageVersion.VersionBlocks
            .SelectMany(versionBlock => versionBlock.PageBlock.References)
            .Where(reference => reference.Kind == CmsReferenceKinds.Page)
            .Select(reference => reference.TargetId)
            .Distinct()
            .ToArray();
        var linkedPages = referencedPageIds.Length == 0
            ? []
            : await ctx.Pages
                .AsNoTracking()
                .Where(linkedPage => referencedPageIds.Contains(linkedPage.Id))
                .ToDictionaryAsync(linkedPage => linkedPage.Id, cancellationToken);
        IReadOnlyList<PageVersionEntity> referencedPageVersions = referencedPageIds.Length == 0
            ? []
            : await ctx.PageVersions
                .AsNoTracking()
                .AsSplitQuery()
                .Where(version => referencedPageIds.Contains(version.PageId) && version.IsCurrent == true && version.Page.IsPublished)
                .Include(version => version.VersionBlocks)
                    .ThenInclude(versionBlock => versionBlock.PageBlock)
                        .ThenInclude(block => block.BlockType)
                .Include(version => version.VersionBlocks)
                    .ThenInclude(versionBlock => versionBlock.PageBlock)
                        .ThenInclude(block => block.References)
                .ToListAsync(cancellationToken);
        var referencedPageHeroImageIds = referencedPageVersions
            .Select(version => new
            {
                version.PageId,
                ImageId = linkedPageImageSelector.SelectImage(ToLinkedPageBlocks(version)),
            })
            .Where(pageHero => pageHero.ImageId.HasValue)
            .ToDictionary(pageHero => pageHero.PageId, pageHero => pageHero.ImageId!.Value);
        var imageIds = pageVersion.VersionBlocks
            .SelectMany(versionBlock => versionBlock.PageBlock.References)
            .Where(reference => reference.Kind == CmsReferenceKinds.Image)
            .Select(reference => reference.TargetId)
            .Concat(referencedPageHeroImageIds.Values)
            .Append(pageVersion.SeoImageId ?? Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();
        var images = await assetResolver.ResolveImagesAsync(imageIds, cancellationToken);
        var page = pageVersion.Page;
        var effectiveNavigationMenuId = await this.ResolveNavigationMenuIdAsync(page, cancellationToken);
        var blocks = pageVersion.VersionBlocks
            .OrderBy(vb => vb.SortOrder)
            .Select(bv => bv.PageBlock)
            .Select(block => new GetPageResponseBlock(
                Id: block.Id,
                InstanceId: block.InstanceId,
                BlockTypeId: block.BlockType.Id,
                BlockName: block.BlockType.Name,
                BlockVersion: block.BlockType.Version.ToString(CultureInfo.InvariantCulture),
                BlockData: block.BlockData,
                Images: [.. block.References
                    .Where(reference => reference.Kind == CmsReferenceKinds.Image && images.ContainsKey(reference.TargetId))
                    .OrderBy(reference => reference.Ordinal)
                    .Select(reference => MapImage(images[reference.TargetId])),],
                Pages: [.. block.References
                    .Where(reference => reference.Kind == CmsReferenceKinds.Page && linkedPages.ContainsKey(reference.TargetId))
                    .OrderBy(reference => reference.Ordinal)
                    .Select(reference => linkedPages[reference.TargetId])
                    .Select(linkedPage => new GetPageResponseBlockPage(
                        Id: linkedPage.Id,
                        Slug: linkedPage.Slug,
                        Path: linkedPage.Path,
                        Title: linkedPage.Slug,
                        HeroImage: referencedPageHeroImageIds.TryGetValue(linkedPage.Id, out var heroImageId) && images.TryGetValue(heroImageId, out var heroImage)
                            ? MapImage(heroImage)
                            : null)),]))
            .ToArray();

        return new GetPageResponse(
            PageId: page.Id,
            PageVersionId: pageVersion.Id,
            ParentId: page.ParentId,
            NavigationMenuId: page.NavigationMenuId,
            EffectiveNavigationMenuId: effectiveNavigationMenuId,
            Slug: page.Slug,
            Path: page.Path,
            Version: page.Version,
            Seo: new PageSeoResponse(
                Title: pageVersion.SeoTitle,
                Description: pageVersion.SeoDescription,
                SocialImage: pageVersion.SeoImageId is { } seoImageId && images.TryGetValue(seoImageId, out var seoImage)
                    ? MapImage(seoImage)
                    : null,
                Index: pageVersion.RobotsIndex,
                Follow: pageVersion.RobotsFollow),
            Blocks: blocks,
            CreatedAt: page.CreatedAt,
            UpdatedAt: pageVersion.CreatedAt,
            IsPublished: page.IsPublished,
            ArchivedAt: page.ArchivedAt,
            References: [.. pageVersion.VersionBlocks
                .OrderBy(versionBlock => versionBlock.SortOrder)
                .SelectMany(versionBlock => versionBlock.PageBlock.References.OrderBy(reference => reference.Ordinal))
                .Select(reference => new PageReferenceResponse(reference.Kind, reference.TargetId))
                .Distinct(),],
            CurrentPageVersionId: await ctx.PageVersions.Where(version => version.PageId == page.Id && version.IsCurrent == true).Select(version => (Guid?)version.Id).SingleOrDefaultAsync(cancellationToken),
            LatestPageVersionId: await ctx.PageVersions.Where(version => version.PageId == page.Id).OrderByDescending(version => version.CreatedAt).ThenByDescending(version => version.Id).Select(version => (Guid?)version.Id).FirstOrDefaultAsync(cancellationToken));
    }

    private static LinkedPageBlock[] ToLinkedPageBlocks(PageVersionEntity version) => [.. version.VersionBlocks
        .OrderBy(versionBlock => versionBlock.SortOrder)
        .Select(versionBlock => new LinkedPageBlock(versionBlock.PageBlock.BlockType.Name, LinkedPageImages(versionBlock.PageBlock))),];

    private static LinkedPageImageReference[] LinkedPageImages(PageBlockEntity block) => [.. block.References
        .Where(reference => reference.Kind == CmsReferenceKinds.Image)
        .OrderBy(reference => reference.Ordinal)
        .Select(reference => new LinkedPageImageReference(reference.JsonPath, reference.TargetId)),];

    private static GetPageResponseBlockImage MapImage(CmsImageAsset image) =>
        new(
            Id: image.Id,
            Width: image.Width ?? 0,
            Height: image.Height ?? 0,
            ImageUrl: image.Url,
            AltText: image.AltText);

    private async Task<Guid?> ResolveNavigationMenuIdAsync(
        PageEntity page,
        CancellationToken cancellationToken)
    {
        if (page.NavigationMenuId.HasValue)
        {
            return page.NavigationMenuId;
        }

        var parentId = page.ParentId;
        while (parentId.HasValue)
        {
            var parent = await ctx.Pages
                .AsNoTracking()
                .Where(candidate => candidate.Id == parentId.Value)
                .Select(candidate => new
                {
                    candidate.ParentId,
                    candidate.NavigationMenuId,
                })
                .SingleOrDefaultAsync(cancellationToken);
            if (parent == null)
            {
                return null;
            }

            if (parent.NavigationMenuId.HasValue)
            {
                return parent.NavigationMenuId;
            }

            parentId = parent.ParentId;
        }

        return null;
    }

    private IQueryable<PageVersionEntity> GetQuery(bool includeArchived = false) => ctx.PageVersions
        .Where(pv => includeArchived || pv.Page.ArchivedAt == null)
        .Include(pv => pv.Page)
        .Include(pv => pv.VersionBlocks)
            .ThenInclude(vb => vb.PageBlock)
                .ThenInclude(pb => pb.BlockType)
        .Include(pv => pv.VersionBlocks)
            .ThenInclude(vb => vb.PageBlock)
                .ThenInclude(pb => pb.References);
}
