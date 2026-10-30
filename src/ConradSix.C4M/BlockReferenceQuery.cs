#pragma warning disable SA1402 // The query contract is kept with its implementation.
namespace ConradSix.C4M;

using ConradSix.C4M.Data;

using Microsoft.EntityFrameworkCore;

public interface IBlockReferenceQuery
{
    // Each page's distinct targets of one kind in its current version, in the order they first appear on the page.
    // Pages without any are left out. Null pageIds means every page.
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> GetCurrentAsync(
        string kind,
        IReadOnlyCollection<Guid>? pageIds,
        CancellationToken cancellationToken);
}

internal sealed class BlockReferenceQuery(CmsEngineDbContext ctx) : IBlockReferenceQuery
{
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> GetCurrentAsync(
        string kind,
        IReadOnlyCollection<Guid>? pageIds,
        CancellationToken cancellationToken)
    {
        var versionBlocks = ctx.PageVersionBlocks.Where(versionBlock => versionBlock.PageVersion.IsCurrent == true);
        if (pageIds is not null)
        {
            versionBlocks = versionBlocks.Where(versionBlock => pageIds.Contains(versionBlock.PageVersion.PageId));
        }

        var rows = await versionBlocks
            .SelectMany(
                versionBlock => versionBlock.PageBlock.References.Where(reference => reference.Kind == kind),
                (versionBlock, reference) => new
                {
                    versionBlock.PageVersion.PageId,
                    versionBlock.SortOrder,
                    reference.Ordinal,
                    reference.TargetId,
                })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.PageId)
            .ToDictionary(
                page => page.Key,
                page => (IReadOnlyList<Guid>)[.. page
                    .OrderBy(row => row.SortOrder)
                    .ThenBy(row => row.Ordinal)
                    .Select(row => row.TargetId)
                    .Distinct(),]);
    }
}
