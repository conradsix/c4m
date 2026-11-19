#pragma warning disable SA1402 // The endpoint and its contracts are kept together.
namespace ConradSix.C4M.Content;

using ConradSix.C4M.Data;

using FastEndpoints;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

internal sealed record ListPublishedBlocksRequest(string BlockType, int Offset = 0, int Limit = 100);

internal sealed record PublishedBlockItem(
    Guid PageId,
    Guid? ParentId,
    string Path,
    Guid PageVersionId,
    string? SeoTitle,
    string? SeoDescription,
    Guid? SeoImageId,
    bool RobotsIndex,
    DateTimeOffset UpdatedAt,
    int Version,
    string Data);

[CmsEndpoint(CmsEndpointKind.Delivery)]
internal sealed class ListPublishedBlocks(CmsEngineDbContext ctx)
    : Endpoint<ListPublishedBlocksRequest, ContentListResponse<PublishedBlockItem>>
{
    public override void Configure() => this.Get("/published-blocks/{blockType}");

    public override async Task HandleAsync(ListPublishedBlocksRequest req, CancellationToken ct)
    {
        if (req.Offset < 0 || req.Limit is < 1 or > 500)
        {
            this.AddError("Offset must be non-negative, and limit must be between 1 and 500.");
            await this.Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct);
            return;
        }

        var rows = await ctx.PageVersionBlocks.AsNoTracking()
            .Where(block => block.PageVersion.IsCurrent == true
                && block.PageVersion.Page.IsPublished
                && block.PageVersion.Page.ArchivedAt == null
                && block.PageBlock.BlockType.Name == req.BlockType)
            .OrderBy(block => block.PageVersion.Page.Path)
            .ThenBy(block => block.SortOrder)
            .Skip(req.Offset)
            .Take(req.Limit)
            .Select(block => new PublishedBlockItem(
                block.PageVersion.PageId,
                block.PageVersion.Page.ParentId,
                block.PageVersion.Page.Path,
                block.PageVersionId,
                block.PageVersion.SeoTitle,
                block.PageVersion.SeoDescription,
                block.PageVersion.SeoImageId,
                block.PageVersion.RobotsIndex,
                block.PageVersion.CreatedAt,
                block.PageBlock.BlockType.Version,
                block.PageBlock.BlockData))
            .ToArrayAsync(ct);
        await this.Send.OkAsync(new ContentListResponse<PublishedBlockItem>(rows), ct);
    }
}
