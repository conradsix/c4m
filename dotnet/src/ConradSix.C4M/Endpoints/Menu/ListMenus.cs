#pragma warning disable SA1402 // File may only contain a single type
namespace ConradSix.C4M.Endpoints.Menu;

using System.Diagnostics;

using ConradSix.C4M;
using ConradSix.C4M.Data;

using FastEndpoints;

using FluentValidation;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

[CmsEndpoint(CmsEndpointKind.Editor)]
internal class ListMenus(
    CmsEngineDbContext dbContext,
    ILogger<ListMenus> logger)
    : Endpoint<ListMenusRequest, ListMenusResponse>
{
    public override void Configure()
    {
        this.Get("/menus");
        this.Summary(s =>
        {
            s.Summary = "Lists menus.";
            s.Description = "Retrieves a paginated list of menus.";
            s.Response<ListMenusResponse>(StatusCodes.Status200OK, "Menus were successfully retrieved.");
        });
    }

    public override async Task HandleAsync(ListMenusRequest req, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        logger.ListMenusStart(req.Limit, req.Offset);

        var query = dbContext.Menus
            .AsNoTracking()
            .OrderBy(m => m.CreatedAt);

        var totalCount = await query.CountAsync(ct);

        var menus = await query
            .Skip(req.Offset)
            .Take(req.Limit)
            .Select(m => new ListMenusMenuItem(
                m.Id,
                m.Name,
                m.CreatedAt,
                m.UpdatedAt))
            .ToArrayAsync(ct);

        var response = new ListMenusResponse(
            menus,
            totalCount,
            req.Limit,
            req.Offset);

        await this.Send.OkAsync(response, ct);

        logger.ListMenusEnd(menus.Length, stopwatch.ElapsedMilliseconds);
    }
}

public sealed record ListMenusRequest(
    int Limit = 20,
    int Offset = 0);

public sealed record ListMenusResponse(
    IReadOnlyList<ListMenusMenuItem> Items,
    int TotalCount,
    int Limit,
    int Offset);

public sealed record ListMenusMenuItem(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

internal sealed class ListMenusRequestValidator : Validator<ListMenusRequest>
{
    public ListMenusRequestValidator()
    {
        this.RuleFor(x => x.Limit)
            .InclusiveBetween(1, 100)
            .WithMessage("Limit must be between 1 and 100");

        this.RuleFor(x => x.Offset)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Offset must be greater than or equal to 0");
    }
}
