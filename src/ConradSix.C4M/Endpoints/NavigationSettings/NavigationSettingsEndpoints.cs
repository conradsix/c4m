#pragma warning disable SA1402 // File may only contain a single type
namespace ConradSix.C4M.Endpoints.NavigationSettings;

using ConradSix.C4M;
using ConradSix.C4M.Data;
using ConradSix.C4M.Data.Model;
using ConradSix.C4M.Model;

using FastEndpoints;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

[CmsEndpoint(CmsEndpointKind.Editor)]
internal class GetNavigationSettings(CmsEngineDbContext ctx)
    : EndpointWithoutRequest<NavigationSettingsResponse>
{
    public override void Configure()
    {
        this.Get("/navigation-settings");
        this.Summary(summary =>
        {
            summary.Summary = "Gets navigation settings.";
            summary.Description = "Retrieves the default navigation and footer menu selections.";
            summary.Response<NavigationSettingsResponse>(
                StatusCodes.Status200OK,
                "Navigation settings were successfully retrieved.");
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var settings = await ctx.NavigationSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == NavigationSettingsEntity.SingletonId, ct);

        await this.Send.OkAsync(
            new NavigationSettingsResponse(
                settings?.DefaultMenuId,
                settings?.FooterMenuId,
                settings?.Version ?? 0),
            ct);
    }
}

[CmsEndpoint(CmsEndpointKind.Editor)]
internal class UpdateNavigationSettings(CmsEngineDbContext ctx, ICmsChangeHandler changeHandler)
    : Endpoint<UpdateNavigationSettingsRequest, NavigationSettingsResponse>
{
    public override void Configure()
    {
        this.Put("/navigation-settings");
        this.Description(description => description
            .ProducesProblemFE(StatusCodes.Status400BadRequest)
            .Produces<ConflictResponse>(StatusCodes.Status409Conflict));
        this.Summary(summary =>
        {
            summary.Summary = "Updates navigation settings.";
            summary.Description = "Changes the default navigation and footer menus immediately.";
            summary.Response<NavigationSettingsResponse>(
                StatusCodes.Status200OK,
                "Navigation settings were successfully updated.");
        });
    }

    public override async Task HandleAsync(UpdateNavigationSettingsRequest req, CancellationToken ct)
    {
        var settings = await ctx.NavigationSettings
            .SingleOrDefaultAsync(item => item.Id == NavigationSettingsEntity.SingletonId, ct);

        if (settings == null)
        {
            if (req.Version != 0)
            {
                await this.SendConflictAsync(req.Version, ct);
                return;
            }

            settings = new NavigationSettingsEntity
            {
                Id = NavigationSettingsEntity.SingletonId,
                DefaultMenuId = req.DefaultMenuId,
                FooterMenuId = req.FooterMenuId,
            };
            ctx.NavigationSettings.Add(settings);
        }
        else
        {
            if (settings.Version != req.Version)
            {
                await this.SendConflictAsync(settings.Version, ct);
                return;
            }

            settings.DefaultMenuId = req.DefaultMenuId;
            settings.FooterMenuId = req.FooterMenuId;
            ctx.Entry(settings).Property(item => item.Version).IsModified = true;
        }

        try
        {
            await ctx.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            ctx.ChangeTracker.Clear();
            var currentVersion = await ctx.NavigationSettings
                .AsNoTracking()
                .Where(item => item.Id == NavigationSettingsEntity.SingletonId)
                .Select(item => (uint?)item.Version)
                .SingleOrDefaultAsync(ct);
            await this.SendConflictAsync(currentVersion ?? 0, ct);
            return;
        }
        catch (DbUpdateException ex) when (ex.IsForeignKeyConstraintViolation())
        {
            if (ex.GetForeignKeyConstraintName()?.Contains("footer_menu", StringComparison.OrdinalIgnoreCase) == true)
            {
                this.AddError(request => request.FooterMenuId, "The selected footer menu does not exist.");
            }
            else
            {
                this.AddError(request => request.DefaultMenuId, "The selected navigation menu does not exist.");
            }

            await this.Send.ErrorsAsync(cancellation: ct);
            return;
        }

        await changeHandler.NavigationChangedAsync();
        await this.Send.OkAsync(
            new NavigationSettingsResponse(settings.DefaultMenuId, settings.FooterMenuId, settings.Version),
            ct);
    }

    private async Task SendConflictAsync(uint version, CancellationToken ct)
    {
        this.HttpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        await this.HttpContext.Response.WriteAsJsonAsync(
            new ConflictResponse(
                NavigationSettingsEntity.SingletonId,
                version,
                DateTimeOffset.UtcNow),
            ct);
    }
}

public sealed record UpdateNavigationSettingsRequest(
    Guid? DefaultMenuId,
    Guid? FooterMenuId,
    uint Version);

public sealed record NavigationSettingsResponse(
    Guid? DefaultMenuId,
    Guid? FooterMenuId,
    uint Version);
