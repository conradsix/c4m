#pragma warning disable SA1402 // File may only contain a single type
namespace ConradSix.C4M.Endpoints.Menu;

using System.Diagnostics;
using System.Globalization;

using ConradSix.C4M;
using ConradSix.C4M.Data;
using ConradSix.C4M.Exceptions;
using ConradSix.C4M.Model;
using ConradSix.C4M.Services;
using ConradSix.C4M.Validation;

using FastEndpoints;

using FluentValidation;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

[CmsEndpoint(CmsEndpointKind.Editor)]
internal class UpdateMenu(
    CmsEngineDbContext ctx,
    ICmsChangeHandler changeHandler,
    IMenuService menuService,
    ILogger<UpdateMenu> logger)
    : Endpoint<UpdateMenuRequest>
{
    public override void Configure()
    {
        this.Put("/menus/{menuId}");
        this.Description(x => x
            .ProducesProblemFE(StatusCodes.Status404NotFound)
            .ProducesProblemFE(StatusCodes.Status409Conflict));
        this.Summary(s =>
        {
            s.Summary = "Updates an existing menu.";
            s.Description = "Replaces all menu items with the provided tree structure.";
            s.Response(StatusCodes.Status204NoContent, "Menu was successfully updated.");
            s.Response(StatusCodes.Status400BadRequest, "The request is invalid.");
            s.Response(StatusCodes.Status404NotFound, "Menu not found.");
            s.Response<ConflictResponse>(StatusCodes.Status409Conflict, "Version conflict detected. The menu has been modified by another user.");
            s.ResponseExamples[StatusCodes.Status409Conflict] = new ConflictResponse(
                Id: Guid.Parse("660f9a5d-82c4-4b8e-9f3a-123456789012"),
                Version: 15,
                UpdatedAt: DateTimeOffset.Parse("2024-03-15T14:45:00Z", CultureInfo.InvariantCulture));
            s.ExampleRequest = new UpdateMenuRequest(
                MenuId: Guid.Parse("1f2e3d4c-5b6a-4798-8a7b-6c5d4e3f2a1b"),
                Version: 1,
                RootItems: [
                    new MenuItem(
                        Title: "Home",
                        ActionTypeId: Guid.Parse("0e1d6f2a-3b4c-4d5e-8f60-718293a4b5c6"),
                        ActionData: /*lang=json,strict*/ """{"pageId": "00000000-0000-0000-0000-000000000000"}""",
                        Children: [
                            new MenuItem(
                                Title: "About",
                                ActionTypeId: Guid.Parse("0e1d6f2a-3b4c-4d5e-8f60-718293a4b5c6"),
                                ActionData: /*lang=json,strict*/ """{"pageId": "00000000-0000-0000-0000-000000000001"}""",
                                Children: null),
                        ]),
                ]);
        });
    }

    public override async Task HandleAsync(UpdateMenuRequest req, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        await using var tx = await ctx.Database.BeginTransactionAsync(ct);
        await PagePublicationGuard.LockAsync(ctx, ct);

        try
        {
            await menuService.UpdateMenuAsync(req.MenuId, req.Version, req.RootItems, ct);
            await tx.CommitAsync(ct);
            await changeHandler.NavigationChangedAsync();

            await this.Send.NoContentAsync(ct);

            logger.UpdateMenuCompleted(req.MenuId, stopwatch.ElapsedMilliseconds);
        }
        catch (MenuPageReferenceException ex)
        {
            foreach (var error in ex.Errors)
            {
                this.AddError(error);
            }

            await this.Send.ErrorsAsync(statusCode: StatusCodes.Status400BadRequest, ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another user has updated the menu
            var currentMenu = await ctx.Menus.FindAsync([req.MenuId], ct);
            if (currentMenu == null)
            {
                logger.UpdateMenuNotFound(req.MenuId);
                await this.Send.NotFoundAsync(ct);
                return;
            }

            var conflictResponse = new ConflictResponse(
                currentMenu.Id,
                currentMenu.Version,
                currentMenu.UpdatedAt);

            logger.UpdateMenuFailed(req.MenuId, "Concurrency conflict");
            this.HttpContext.Response.StatusCode = StatusCodes.Status409Conflict;
            await this.HttpContext.Response.WriteAsJsonAsync(conflictResponse, ct);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.Ordinal))
        {
            logger.UpdateMenuNotFound(req.MenuId);
            this.AddError($"Menu with ID '{req.MenuId}' not found.");
            await this.Send.ErrorsAsync(statusCode: StatusCodes.Status404NotFound, ct);
        }
        catch (DbUpdateException ex) when (ex.IsForeignKeyConstraintViolation())
        {
            var constraintName = ex.GetForeignKeyConstraintName();

            if (constraintName?.Contains("page", StringComparison.OrdinalIgnoreCase) == true)
            {
                this.AddError("One or more referenced pages do not exist.");
            }
            else
            {
                this.AddError("Referenced resource does not exist.");
            }

            logger.UpdateMenuFailed(req.MenuId, ex.Message);
            await this.Send.ErrorsAsync(statusCode: StatusCodes.Status400BadRequest, ct);
        }
        catch (Exception ex)
        {
            logger.EndpointError(ex, nameof(UpdateMenu));
            throw;
        }
    }
}

public record UpdateMenuRequest(
    Guid MenuId,
    uint Version,
    IReadOnlyList<MenuItem>? RootItems);

internal class UpdateMenuRequestValidator : Validator<UpdateMenuRequest>
{
    public UpdateMenuRequestValidator()
    {
        this.RuleFor(r => r.Version)
            .NotNull()
            .WithMessage("Version is required for concurrency checking");
        this.RuleFor(r => r.RootItems)
            .Must(items => CountMenuItems(items) <= MenuConstants.MaxMenuItems)
            .WithMessage($"Menu cannot contain more than {MenuConstants.MaxMenuItems} items total")
            .When(r => r.RootItems != null && r.RootItems.Count > 0);

        this.RuleForEach(r => r.RootItems)
            .SetValidator(new MenuItemValidator())
            .When(r => r.RootItems != null && r.RootItems.Count > 0);
    }

    private static int CountMenuItems(IReadOnlyList<MenuItem>? items)
    {
        if (items == null)
        {
            return 0;
        }

        var count = items.Count;
        foreach (var item in items)
        {
            count += CountMenuItems(item.Children);
        }

        return count;
    }
}
