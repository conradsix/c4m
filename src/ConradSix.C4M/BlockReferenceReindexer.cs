#pragma warning disable SA1402 // The contract is kept with its implementation.
namespace Parapet.CMS.Engine;

using Microsoft.EntityFrameworkCore;

using Parapet.CMS.Engine.Data;
using Parapet.CMS.Engine.Data.Model;

// Rebuilds every block's references from its data, for when the registered extractors change. Blocks never change, so
// the result depends only on the data and the extractors, and a run can be repeated safely.
public interface IBlockReferenceReindexer
{
    // Progress reports the blocks done so far and the total. Returns the number of blocks reindexed.
    Task<int> ReindexAsync(Func<int, int, CancellationToken, ValueTask>? progress, CancellationToken cancellationToken);
}

internal sealed class BlockReferenceReindexer(CmsEngineDbContext ctx, BlockReferenceIndexer indexer, ICmsChangeHandler changeHandler)
    : IBlockReferenceReindexer
{
    public const int BatchSize = 500;

    public async Task<int> ReindexAsync(
        Func<int, int, CancellationToken, ValueTask>? progress,
        CancellationToken cancellationToken)
    {
        var total = await ctx.PageBlocks.CountAsync(cancellationToken);
        var done = 0;
        Guid? after = null;
        while (true)
        {
            var batch = await ctx.PageBlocks.AsNoTracking()
                .Where(block => after == null || block.Id.CompareTo(after.Value) > 0)
                .OrderBy(block => block.Id)
                .Take(BatchSize)
                .Select(block => new { block.Id, block.BlockData })
                .ToListAsync(cancellationToken);
            if (batch.Count == 0)
            {
                break;
            }

            // The batch's references are replaced in the database, so none of them may still be tracked.
            ctx.ChangeTracker.Clear();
            var ids = batch.Select(block => block.Id).ToArray();
            await using var transaction = await ctx.Database.BeginTransactionAsync(cancellationToken);
            await ctx.PageBlockReferences.Where(reference => ids.Contains(reference.PageBlockId)).ExecuteDeleteAsync(cancellationToken);
            ctx.PageBlockReferences.AddRange(batch.SelectMany(block => indexer.Index(block.BlockData).Select(reference => new PageBlockReferenceEntity
            {
                PageBlockId = block.Id,
                Kind = reference.Kind,
                TargetId = reference.TargetId,
                JsonPath = reference.JsonPath,
                Ordinal = reference.Ordinal,
            })));
            await ctx.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            done += batch.Count;
            after = batch[^1].Id;
            if (progress is not null)
            {
                await progress(done, total, cancellationToken);
            }
        }

        await changeHandler.ContentChangedAsync();
        return done;
    }
}
