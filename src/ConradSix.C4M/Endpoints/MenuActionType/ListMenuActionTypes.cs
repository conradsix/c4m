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
internal class ListMenuActionTypes(
    CmsEngineDbContext dbContext,
    ILogger<ListMenuActionTypes> logger)
    : EndpointWithoutRequest<ListMenuActionTypesResponse>
{
    public override void Configure()
    {
        this.Get("/menu-action-types");
        this.Summary(s =>
        {
            s.Summary = "Lists all menu action types.";
            s.Description = "Retrieves all available menu action types with their schemas.";
            s.Response<ListMenuActionTypesResponse>(StatusCodes.Status200OK, "Menu action types were successfully retrieved.");
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        logger.ListMenuActionTypesStart();

        var actionTypes = await dbContext.MenuActionTypes
            .AsNoTracking()
            .OrderBy(mat => mat.Name)
            .ThenBy(mat => mat.Version)
            .Select(mat => new CmsTypeDescriptor(
                mat.Id,
                mat.Name,
                mat.Version,
                mat.JsonSchema,
                mat.CreatedAt))
            .ToArrayAsync(ct);

        var response = new ListMenuActionTypesResponse(actionTypes);

        await this.Send.OkAsync(response, ct);

        logger.ListMenuActionTypesEnd(actionTypes.Length, stopwatch.ElapsedMilliseconds);
    }
}

public sealed record ListMenuActionTypesResponse(
    IReadOnlyList<CmsTypeDescriptor> Items);
