namespace Parapet.CMS.Engine.Endpoints.Menu;

using FastEndpoints;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

using Parapet.CMS.Engine;
using Parapet.CMS.Engine.Data;
using Parapet.CMS.Engine.Data.Model;

[CmsEndpoint(CmsEndpointKind.Delivery)]
internal class GetDefaultMenu(CmsEngineDbContext ctx) : EndpointWithoutRequest<GetMenuResponse>
{
    public override void Configure()
    {
        this.Get("/menus/default");
        this.Description(description => description.ProducesProblemFE(StatusCodes.Status404NotFound));
        this.Summary(summary =>
        {
            summary.Summary = "Gets the default navigation menu.";
            summary.Description = "Retrieves the menu configured as the site-wide navigation fallback.";
            summary.Response<GetMenuResponse>(StatusCodes.Status200OK, "The default menu was successfully retrieved.");
            summary.Response(StatusCodes.Status404NotFound, "No default menu is configured.");
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var menuId = await ctx.NavigationSettings
            .AsNoTracking()
            .Where(settings => settings.Id == NavigationSettingsEntity.SingletonId)
            .Select(settings => settings.DefaultMenuId)
            .SingleOrDefaultAsync(ct);
        if (!menuId.HasValue)
        {
            await this.Send.NotFoundAsync(ct);
            return;
        }

        var response = await GetMenu.GetResponseAsync(ctx, menuId.Value, ct);
        if (response == null)
        {
            await this.Send.NotFoundAsync(ct);
            return;
        }

        await this.Send.OkAsync(response, ct);
    }
}
