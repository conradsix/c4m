#pragma warning disable SA1402 // File may only contain a single type
namespace Parapet.CMS.Engine.Endpoints.BlockType;

using FastEndpoints;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Parapet.CMS.Engine;
using Parapet.CMS.Engine.Data;
using Parapet.CMS.Engine.Model;

[CmsEndpoint(CmsEndpointKind.Editor)]
internal class ListBlockTypes(CmsEngineDbContext ctx, IOptions<CmsEngineOptions> options)
    : Endpoint<ListBlockTypesRequest, ListBlockTypesResponse>
{
    public override void Configure()
    {
        this.Get("/block-types");
        this.Summary(s =>
        {
            s.Summary = "Lists block types with pagination.";
            s.Description = "Retrieves a paginated list of block types grouped by name. All versions of each block type are included together.";
            s.Response<ListBlockTypesResponse>(StatusCodes.Status200OK, "Block types were successfully retrieved.");
            s.Response(StatusCodes.Status400BadRequest, "The request is invalid.");
        });
    }

    public override async Task HandleAsync(ListBlockTypesRequest req, CancellationToken ct)
    {
        var distinctNames = await ctx.BlockTypes
            .Select(bt => bt.Name)
            .Distinct()
            .OrderBy(name => name)
            .Skip(req.Offset)
            .Take(req.Limit)
            .ToListAsync(ct);

        var entities = await ctx.BlockTypes
            .Where(bt => distinctNames.Contains(bt.Name))
            .OrderBy(bt => bt.Name)
            .ThenByDescending(bt => bt.Version)
            .ToListAsync(ct);
        var blockTypes = entities.Select(bt => new CmsTypeDescriptor(
            bt.Id, bt.Name, bt.Version, bt.JsonSchema, bt.CreatedAt, options.Value.RetiredBlockTypes.Contains(bt.Name))).ToArray();

        var total = await ctx.BlockTypes
            .Select(bt => bt.Name)
            .Distinct()
            .CountAsync(ct);

        var nextOffset = req.Offset + req.Limit < total ? req.Offset + req.Limit : (int?)null;

        await this.Send.OkAsync(
            new ListBlockTypesResponse(
                blockTypes,
                total,
                nextOffset,
                req.Limit),
            ct);
    }
}

public record ListBlockTypesRequest(int Offset = 0, int Limit = 50);

public record ListBlockTypesResponse(
    IReadOnlyList<CmsTypeDescriptor> Items,
    int Total,
    int? NextOffset,
    int Limit);
