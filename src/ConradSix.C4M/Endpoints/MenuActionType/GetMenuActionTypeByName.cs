#pragma warning disable SA1402 // File may only contain a single type
namespace ConradSix.C4M.Endpoints.MenuActionType;

using ConradSix.C4M;
using ConradSix.C4M.Data;
using ConradSix.C4M.Model;

using FastEndpoints;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

[CmsEndpoint(CmsEndpointKind.Editor)]
internal class GetMenuActionTypeByName(CmsEngineDbContext ctx)
    : Endpoint<GetMenuActionTypeByNameRequest, CmsTypeDescriptor>
{
    public override void Configure()
    {
        this.Get("/menu-action-types/by-name/{name}");
        this.Summary(s =>
        {
            s.Summary = "Gets a menu action type by name.";
            s.Description = "Retrieves a specific menu action type from the system by its name. If version is not specified, returns the latest version.";
            s.Response<CmsTypeDescriptor>(StatusCodes.Status200OK, "Menu action type was successfully retrieved.");
            s.Response(StatusCodes.Status404NotFound, "Menu action type was not found.");
        });
    }

    public override async Task HandleAsync(GetMenuActionTypeByNameRequest req, CancellationToken ct)
    {
        var query = ctx.MenuActionTypes
            .Where(mat => mat.Name == req.Name)
            .AsNoTracking();

        // If version is specified, filter by it; otherwise get the latest version
        var entity = req.Version.HasValue
            ? await query.SingleOrDefaultAsync(mat => mat.Version == req.Version.Value, ct)
            : await query
                .OrderByDescending(mat => mat.Version)
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
                entity.CreatedAt),
            ct);
    }
}

public record GetMenuActionTypeByNameRequest(string Name, int? Version = null);
