#pragma warning disable SA1402 // File may only contain a single type
namespace ConradSix.C4M.Endpoints.BlockType;

using ConradSix.C4M;
using ConradSix.C4M.Data;
using ConradSix.C4M.Model;

using FastEndpoints;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

[CmsEndpoint(CmsEndpointKind.Editor)]
internal class GetBlockTypeByName(CmsEngineDbContext ctx, IOptions<C4MOptions> options)
    : Endpoint<GetBlockTypeByNameRequest, CmsTypeDescriptor>
{
    public override void Configure()
    {
        this.Get("/block-types/by-name/{name}");
        this.Summary(s =>
        {
            s.Summary = "Gets a block type by name.";
            s.Description = "Retrieves a specific block type from the system by its name. If version is not specified, returns the latest version.";
            s.Response<CmsTypeDescriptor>(StatusCodes.Status200OK, "Block type was successfully retrieved.");
            s.Response(StatusCodes.Status404NotFound, "Block type was not found.");
        });
    }

    public override async Task HandleAsync(GetBlockTypeByNameRequest req, CancellationToken ct)
    {
        var query = ctx.BlockTypes
            .Where(bt => bt.Name == req.Name)
            .AsNoTracking();

        // If version is specified, filter by it; otherwise get the latest version
        var entity = req.Version.HasValue
            ? await query.SingleOrDefaultAsync(bt => bt.Version == req.Version.Value, ct)
            : await query
                .OrderByDescending(bt => bt.Version)
                .FirstOrDefaultAsync(ct);

        if (entity is null)
        {
            await this.Send.NotFoundAsync(ct);
            return;
        }

        await this.Send.OkAsync(
            new CmsTypeDescriptor(
                entity.Id,
                entity.Name,
                entity.Version,
                entity.JsonSchema,
                entity.CreatedAt,
                options.Value.RetiredBlockTypes.Contains(entity.Name)),
            ct);
    }
}

public record GetBlockTypeByNameRequest(string Name, int? Version = null);
