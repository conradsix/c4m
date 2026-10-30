#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1204 // Response query helper is kept beside the endpoint handler.
namespace ConradSix.C4M.Endpoints.Menu;

using System.Diagnostics;

using ConradSix.C4M;
using ConradSix.C4M.Data;
using ConradSix.C4M.Data.Model;

using FastEndpoints;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

[CmsEndpoint(CmsEndpointKind.Delivery)]
internal class GetMenu(
    CmsEngineDbContext dbContext,
    ILogger<GetMenu> logger)
    : Endpoint<GetMenuRequest, GetMenuResponse>
{
    public override void Configure()
    {
        this.Get("/menus/{id}");
        this.Description(x => x.ProducesProblemFE(StatusCodes.Status404NotFound));
        this.Summary(s =>
        {
            s.Summary = "Gets a menu by ID.";
            s.Description = "Retrieves a menu with its hierarchical structure of menu items.";
            s.Response<GetMenuResponse>(StatusCodes.Status200OK, "Menu was successfully retrieved.");
            s.Response(StatusCodes.Status404NotFound, "The menu was not found.");
        });
    }

    public override async Task HandleAsync(GetMenuRequest req, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        logger.GetMenuStart(req.Id);

        var response = await GetResponseAsync(dbContext, req.Id, ct);
        if (response == null)
        {
            await this.Send.NotFoundAsync(ct);
            return;
        }

        await this.Send.OkAsync(response, ct);

        logger.GetMenuEnd(req.Id, stopwatch.ElapsedMilliseconds);
    }

    public static async Task<GetMenuResponse?> GetResponseAsync(
        CmsEngineDbContext dbContext,
        Guid menuId,
        CancellationToken ct)
    {
        var menu = await dbContext.Menus
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == menuId, ct);

        if (menu == null)
        {
            return null;
        }

        var menuItems = await dbContext.MenuItems
            .GetMenuItems(menuId)
            .Include(mi => mi.ActionType)
            .Include(mi => mi.PageReferences)
                .ThenInclude(reference => reference.ReferencedPage)
                    .ThenInclude(page => page.Versions.Where(version => version.IsCurrent == true))
            .AsNoTracking()
            .ToDictionaryAsync(mi => mi.Id, ct);

        var rootItems = menuItems.Values
            .Where(mi => mi.MenuId == menuId)
            .OrderBy(mi => mi.SortOrder)
            .Select(rootNode => BuildMenuTree(rootNode, menuItems))
            .ToArray();

        var pages = menuItems.Values
            .SelectMany(item => item.PageReferences)
            .Select(reference => reference.ReferencedPage)
            .Where(page => page.ArchivedAt == null && page.IsPublished && page.Versions.Any(version => version.IsCurrent == true))
            .DistinctBy(page => page.Id)
            .OrderBy(page => page.Path)
            .Select(page => new GetMenuResponsePage(
                page.Id,
                page.Slug,
                page.Path))
            .ToArray();

        return new GetMenuResponse(
            menu.Id,
            menu.Name,
            menu.Version,
            menu.CreatedAt,
            menu.UpdatedAt,
            rootItems,
            pages);
    }

    private static GetMenuRequestMenuItem BuildMenuTree(
        MenuItemEntity item,
        Dictionary<Guid, MenuItemEntity> itemsById)
    {
        var children = itemsById.Values
            .Where(mi => mi.ParentId == item.Id)
            .OrderBy(mi => mi.SortOrder)
            .Select(child => BuildMenuTree(child, itemsById))
            .ToArray();

        return new GetMenuRequestMenuItem(
            item.Id,
            item.Title,
            item.ActionType.Name,
            item.ActionType.Version,
            item.ActionData,
            children);
    }
}

public sealed record GetMenuRequest
{
    public Guid Id { get; init; }
}

public sealed record GetMenuResponse(
    Guid Id,
    string Name,
    uint Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<GetMenuRequestMenuItem> RootItems,
    IReadOnlyList<GetMenuResponsePage> Pages);

public sealed record GetMenuResponsePage(
    Guid Id,
    string Slug,
    string Path);

public sealed record GetMenuRequestMenuItem(
    Guid Id,
    string Title,
    string ActionTypeName,
    int ActionTypeVersion,
    string ActionData,
    IReadOnlyList<GetMenuRequestMenuItem> Children);
