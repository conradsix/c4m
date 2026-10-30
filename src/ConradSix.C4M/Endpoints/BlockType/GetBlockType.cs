#pragma warning disable SA1402 // File may only contain a single type
namespace Parapet.CMS.Engine.Endpoints.BlockType;

using System.Diagnostics;

using FastEndpoints;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Parapet.CMS.Engine;
using Parapet.CMS.Engine.Data;
using Parapet.CMS.Engine.Model;

[CmsEndpoint(CmsEndpointKind.Editor)]
internal class GetBlockType(CmsEngineDbContext ctx, IOptions<CmsEngineOptions> options, ILogger<GetBlockType> logger)
    : Endpoint<GetBlockTypeRequest, CmsTypeDescriptor>
{
    public override void Configure()
    {
        this.Get("/block-types/{id}");
        this.Summary(s =>
        {
            s.Summary = "Gets a block type by ID.";
            s.Description = "Retrieves a specific block type from the system.";
            s.Response<CmsTypeDescriptor>(StatusCodes.Status200OK, "Block type was successfully retrieved.");
            s.Response(StatusCodes.Status404NotFound, "Block type was not found.");
        });
    }

    public override async Task HandleAsync(GetBlockTypeRequest req, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var entity = await ctx.BlockTypes
                .AsNoTracking()
                .SingleOrDefaultAsync(bt => bt.Id == req.Id, ct);

            if (entity is null)
            {
                logger.GetBlockTypeNotFound(req.Id);
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

            logger.GetBlockTypeCompleted(req.Id, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            logger.EndpointError(ex, nameof(GetBlockType));
            throw;
        }
    }
}

public record GetBlockTypeRequest(Guid Id);
