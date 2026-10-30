#pragma warning disable SA1402 // File may only contain a single type
namespace ConradSix.C4M.Endpoints.MenuActionType;

using System.Diagnostics;

using ConradSix.C4M;
using ConradSix.C4M.Data;
using ConradSix.C4M.Model;

using FastEndpoints;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

[CmsEndpoint(CmsEndpointKind.Editor)]
internal class GetMenuActionType(
    CmsEngineDbContext dbContext,
    ILogger<GetMenuActionType> logger)
    : Endpoint<GetMenuActionTypeRequest, CmsTypeDescriptor>
{
    public override void Configure()
    {
        this.Get("/menu-action-types/{id}");
        this.Description(x => x.ProducesProblemFE(StatusCodes.Status404NotFound));
        this.Summary(s =>
        {
            s.Summary = "Gets a menu action type by ID.";
            s.Description = "Retrieves a menu action type including its JSON schema for validation.";
            s.Response<CmsTypeDescriptor>(StatusCodes.Status200OK, "Menu action type was successfully retrieved.");
            s.Response(StatusCodes.Status404NotFound, "The menu action type was not found.");
        });
    }

    public override async Task HandleAsync(GetMenuActionTypeRequest req, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        logger.GetMenuActionTypeStart(req.Id);

        var actionType = await dbContext.MenuActionTypes
            .AsNoTracking()
            .Where(mat => mat.Id == req.Id)
            .Select(mat => new CmsTypeDescriptor(
                mat.Id,
                mat.Name,
                mat.Version,
                mat.JsonSchema,
                mat.CreatedAt))
            .SingleOrDefaultAsync(ct);

        if (actionType == null)
        {
            await this.Send.NotFoundAsync(ct);
            return;
        }

        await this.Send.OkAsync(actionType, ct);

        logger.GetMenuActionTypeEnd(req.Id, stopwatch.ElapsedMilliseconds);
    }
}

public sealed record GetMenuActionTypeRequest(Guid Id);
