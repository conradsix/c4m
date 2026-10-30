namespace Parapet.CMS.Engine.Endpoints.Menu;

using FastEndpoints;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

using Parapet.CMS.Engine;
using Parapet.CMS.Engine.Data;
using Parapet.CMS.Engine.Data.Model;

[CmsEndpoint(CmsEndpointKind.Delivery)]
internal class GetFooterMenu(CmsEngineDbContext ctx) : EndpointWithoutRequest<GetMenuResponse>
{
    public override void Configure()
    {
        this.Get("/menus/footer");
        this.Description(description => description.ProducesProblemFE(StatusCodes.Status404NotFound));
        this.Summary(summary =>
        {
            summary.Summary = "Gets the footer menu.";
            summary.Description = "Retrieves the menu configured for the site-wide footer.";
            summary.Response<GetMenuResponse>(StatusCodes.Status200OK, "The footer menu was successfully retrieved.");
            summary.Response(StatusCodes.Status404NotFound, "No footer menu is configured.");
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var menuId = await ctx.NavigationSettings
            .AsNoTracking()
            .Where(settings => settings.Id == NavigationSettingsEntity.SingletonId)
            .Select(settings => settings.FooterMenuId)
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
