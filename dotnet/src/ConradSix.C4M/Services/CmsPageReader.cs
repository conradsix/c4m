#pragma warning disable SA1402 // The read contracts are kept with the reader.
namespace ConradSix.C4M.Services;

using ConradSix.C4M;
using ConradSix.C4M.Data;

using Microsoft.EntityFrameworkCore;

// What a host can read about pages. Nothing here exposes the CMS's tables: hosts read through this and keep page and
// version ids as plain values.
public interface ICmsPageReader
{
    // Any state, archived included.
    Task<CmsPage?> GetPageAsync(Guid pageId, CancellationToken cancellationToken);

    // Archived pages included; unknown ids are left out.
    Task<IReadOnlyDictionary<Guid, CmsPage>> GetPagesAsync(IReadOnlyCollection<Guid> pageIds, CancellationToken cancellationToken);

    // Unarchived pages, or only the published ones, ordered by path.
    Task<IReadOnlyList<CmsPage>> ListPagesAsync(bool publishedOnly, CancellationToken cancellationToken);

    // Each page's current version. Null pageIds means every page.
    Task<IReadOnlyDictionary<Guid, CmsCurrentVersion>> GetCurrentVersionsAsync(IReadOnlyCollection<Guid>? pageIds, CancellationToken cancellationToken);

    Task<CmsVersionHeads> GetVersionHeadsAsync(Guid pageId, CancellationToken cancellationToken);

    Task<CmsVersion?> GetVersionAsync(Guid pageVersionId, CancellationToken cancellationToken);

    // The image shown when another page links to each page, from its current version.
    Task<IReadOnlyDictionary<Guid, Guid>> GetLinkedPageImagesAsync(IReadOnlyCollection<Guid> pageIds, CancellationToken cancellationToken);

    // For each linked page: how many published pages' current versions link to it, and how many menu items do.
    Task<IReadOnlyDictionary<Guid, CmsLinkCounts>> GetLinkCountsAsync(CancellationToken cancellationToken);

    // Null names means every block type.
    Task<IReadOnlyList<CmsBlockType>> GetBlockTypesAsync(IReadOnlyCollection<string>? names, CancellationToken cancellationToken);
}

public sealed record CmsPage(Guid Id, Guid? ParentId, string Slug, string Path, bool IsPublished, DateTimeOffset? ArchivedAt);

public sealed record CmsCurrentVersion(Guid PageId, Guid PageVersionId, string? SeoTitle, bool RobotsIndex, IReadOnlyList<string> BlockTypes);

// The latest version is the newest saved; a page without a current version shows its latest.
public sealed record CmsVersionHeads(Guid? CurrentPageVersionId, Guid? LatestPageVersionId)
{
    public Guid? WorkingPageVersionId => this.CurrentPageVersionId ?? this.LatestPageVersionId;
}

public sealed record CmsVersion(
    Guid Id,
    Guid PageId,
    bool IsCurrent,
    DateTimeOffset CreatedAt,
    string? SeoTitle,
    string? SeoDescription,
    Guid? SeoImageId,
    bool RobotsIndex,
    bool RobotsFollow,
    IReadOnlyList<CmsVersionBlock> Blocks);

// In page order. References are in block order: array items in order, object properties by name.
public sealed record CmsVersionBlock(
    Guid Id,
    Guid InstanceId,
    Guid BlockTypeId,
    string BlockType,
    int BlockTypeVersion,
    string BlockData,
    IReadOnlyList<CmsBlockReference> References);

public sealed record CmsBlockReference(string Kind, Guid TargetId, string JsonPath);

public sealed record CmsLinkCounts(int FromPages, int FromMenus);

public sealed record CmsBlockType(Guid Id, string Name, int Version, string JsonSchema);

internal sealed class CmsPageReader(CmsEngineDbContext ctx, ILinkedPageImageSelector linkedPageImageSelector) : ICmsPageReader
{
    public async Task<CmsPage?> GetPageAsync(Guid pageId, CancellationToken cancellationToken) =>
        (await this.GetPagesAsync([pageId], cancellationToken)).GetValueOrDefault(pageId);

    public async Task<IReadOnlyDictionary<Guid, CmsPage>> GetPagesAsync(IReadOnlyCollection<Guid> pageIds, CancellationToken cancellationToken) =>
        pageIds.Count == 0
            ? []
            : await ctx.Pages.AsNoTracking()
                .Where(page => pageIds.Contains(page.Id))
                .Select(page => new CmsPage(page.Id, page.ParentId, page.Slug, page.Path, page.IsPublished, page.ArchivedAt))
                .ToDictionaryAsync(page => page.Id, cancellationToken);

    public async Task<IReadOnlyList<CmsPage>> ListPagesAsync(bool publishedOnly, CancellationToken cancellationToken) =>
        await ctx.Pages.AsNoTracking()
            .Where(page => page.ArchivedAt == null && (!publishedOnly || page.IsPublished))
            .OrderBy(page => page.Path)
            .Select(page => new CmsPage(page.Id, page.ParentId, page.Slug, page.Path, page.IsPublished, page.ArchivedAt))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, CmsCurrentVersion>> GetCurrentVersionsAsync(
        IReadOnlyCollection<Guid>? pageIds,
        CancellationToken cancellationToken)
    {
        var versions = ctx.PageVersions.AsNoTracking().Where(version => version.IsCurrent == true);
        if (pageIds is not null)
        {
            versions = versions.Where(version => pageIds.Contains(version.PageId));
        }

        return await versions
            .Select(version => new CmsCurrentVersion(
                version.PageId,
                version.Id,
                version.SeoTitle,
                version.RobotsIndex,
                version.VersionBlocks.OrderBy(block => block.SortOrder).Select(block => block.PageBlock.BlockType.Name).ToList()))
            .ToDictionaryAsync(version => version.PageId, cancellationToken);
    }

    public async Task<CmsVersionHeads> GetVersionHeadsAsync(Guid pageId, CancellationToken cancellationToken)
    {
        var versions = ctx.PageVersions.AsNoTracking().Where(version => version.PageId == pageId);
        return new CmsVersionHeads(
            await versions.Where(version => version.IsCurrent == true).Select(version => (Guid?)version.Id).SingleOrDefaultAsync(cancellationToken),
            await versions.OrderByDescending(version => version.CreatedAt).ThenByDescending(version => version.Id)
                .Select(version => (Guid?)version.Id).FirstOrDefaultAsync(cancellationToken));
    }

    public async Task<CmsVersion?> GetVersionAsync(Guid pageVersionId, CancellationToken cancellationToken)
    {
        var version = await ctx.PageVersions.AsNoTracking()
            .Where(version => version.Id == pageVersionId)
            .Select(version => new
            {
                version.Id,
                version.PageId,
                IsCurrent = version.IsCurrent == true,
                version.CreatedAt,
                version.SeoTitle,
                version.SeoDescription,
                version.SeoImageId,
                version.RobotsIndex,
                version.RobotsFollow,
                Blocks = version.VersionBlocks.OrderBy(versionBlock => versionBlock.SortOrder).Select(versionBlock => new
                {
                    versionBlock.PageBlock.Id,
                    versionBlock.PageBlock.InstanceId,
                    versionBlock.PageBlock.BlockTypeId,
                    versionBlock.PageBlock.BlockType.Name,
                    versionBlock.PageBlock.BlockType.Version,
                    versionBlock.PageBlock.BlockData,
                    References = versionBlock.PageBlock.References.OrderBy(reference => reference.Ordinal)
                        .Select(reference => new CmsBlockReference(reference.Kind, reference.TargetId, reference.JsonPath)).ToList(),
                }).ToList(),
            })
            .SingleOrDefaultAsync(cancellationToken);
        return version is null
            ? null
            : new CmsVersion(
                version.Id,
                version.PageId,
                version.IsCurrent,
                version.CreatedAt,
                version.SeoTitle,
                version.SeoDescription,
                version.SeoImageId,
                version.RobotsIndex,
                version.RobotsFollow,
                [.. version.Blocks.Select(block => new CmsVersionBlock(block.Id, block.InstanceId, block.BlockTypeId, block.Name, block.Version, block.BlockData, block.References))]);
    }

    public async Task<IReadOnlyDictionary<Guid, Guid>> GetLinkedPageImagesAsync(IReadOnlyCollection<Guid> pageIds, CancellationToken cancellationToken)
    {
        if (pageIds.Count == 0)
        {
            return new Dictionary<Guid, Guid>();
        }

        var blocks = await ctx.PageVersionBlocks.AsNoTracking()
            .Where(versionBlock => pageIds.Contains(versionBlock.PageVersion.PageId) && versionBlock.PageVersion.IsCurrent == true)
            .Select(versionBlock => new
            {
                versionBlock.PageVersion.PageId,
                versionBlock.SortOrder,
                versionBlock.PageBlock.BlockType.Name,
                Images = versionBlock.PageBlock.References.Where(reference => reference.Kind == CmsReferenceKinds.Image)
                    .OrderBy(reference => reference.Ordinal)
                    .Select(reference => new LinkedPageImageReference(reference.JsonPath, reference.TargetId)).ToList(),
            })
            .ToListAsync(cancellationToken);
        return blocks
            .GroupBy(block => block.PageId)
            .Select(page => (PageId: page.Key, ImageId: linkedPageImageSelector.SelectImage(
                [.. page.OrderBy(block => block.SortOrder).Select(block => new LinkedPageBlock(block.Name, block.Images))])))
            .Where(page => page.ImageId.HasValue)
            .ToDictionary(page => page.PageId, page => page.ImageId!.Value);
    }

    public async Task<IReadOnlyDictionary<Guid, CmsLinkCounts>> GetLinkCountsAsync(CancellationToken cancellationToken)
    {
        var publishedPages = ctx.Pages.Where(page => page.IsPublished && page.ArchivedAt == null).Select(page => page.Id);
        var fromPages = await ctx.PageVersionBlocks.AsNoTracking()
            .Where(versionBlock => versionBlock.PageVersion.IsCurrent == true && publishedPages.Contains(versionBlock.PageVersion.PageId))
            .SelectMany(
                versionBlock => versionBlock.PageBlock.References.Where(reference => reference.Kind == CmsReferenceKinds.Page),
                (versionBlock, reference) => new { Source = versionBlock.PageVersion.PageId, Target = reference.TargetId })
            .Where(link => link.Source != link.Target)
            .Distinct()
            .GroupBy(link => link.Target)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.Key, group => group.Count, cancellationToken);
        var fromMenus = await ctx.MenuItemPageReferences.AsNoTracking()
            .GroupBy(reference => reference.ReferencedPageId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.Key, group => group.Count, cancellationToken);
        return fromPages.Keys.Union(fromMenus.Keys).ToDictionary(
            pageId => pageId,
            pageId => new CmsLinkCounts(fromPages.GetValueOrDefault(pageId), fromMenus.GetValueOrDefault(pageId)));
    }

    public async Task<IReadOnlyList<CmsBlockType>> GetBlockTypesAsync(IReadOnlyCollection<string>? names, CancellationToken cancellationToken) =>
        await ctx.BlockTypes.AsNoTracking()
            .Where(type => names == null || names.Contains(type.Name))
            .Select(type => new CmsBlockType(type.Id, type.Name, type.Version, type.JsonSchema))
            .ToListAsync(cancellationToken);
}
