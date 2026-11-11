#pragma warning disable SA1402 // File may only contain a single type
namespace ConradSix.C4M.Endpoints.Menu;

using System.Diagnostics;

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
internal class CreateMenu(
    CmsEngineDbContext ctx,
    ICmsChangeHandler changeHandler,
    IMenuService menuService,
    ILogger<CreateMenu> logger)
    : Endpoint<CreateMenuRequest, CreateMenuResponse>
{
    public override void Configure()
    {
        this.Post("/menus");
        this.Description(x => x.ProducesProblemFE(StatusCodes.Status409Conflict));
        this.Summary(s =>
        {
            s.Summary = "Creates a new menu.";
            s.Description = "Adds a new menu with optional menu items to the system.";
            s.Response<CreateMenuResponse>(StatusCodes.Status201Created, "Menu was successfully created.");
            s.Response(StatusCodes.Status400BadRequest, "The request is invalid.");
            s.Response(StatusCodes.Status409Conflict, "A menu with the same name already exists.");
            s.ExampleRequest = new CreateMenuRequest(
                Name: "main-navigation",
                RootItems: [
                    new MenuItem(
                        Title: "Home",
                        ActionTypeId: Guid.Parse("0e1d6f2a-3b4c-4d5e-8f60-718293a4b5c6"),
                        ActionData: /*lang=json,strict*/ """{"pageId": "00000000-0000-0000-0000-000000000000"}""",
                        Children: null),
                ]);
        });
    }

    public override async Task HandleAsync(CreateMenuRequest req, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        await using var tx = await ctx.Database.BeginTransactionAsync(ct);
        await PagePublicationGuard.LockAsync(ctx, ct);

        try
        {
            var menuId = await menuService.CreateMenuAsync(
                req.Name,
                req.RootItems,
                ct);

            await tx.CommitAsync(ct);
            await changeHandler.NavigationChangedAsync();

            await this.Send.CreatedAtAsync<GetMenu>(
                new { menuId },
                new CreateMenuResponse(menuId),
                cancellation: ct);

            logger.CreateMenuCompleted(menuId, req.Name, stopwatch.ElapsedMilliseconds);
        }
        catch (MenuPageReferenceException ex)
        {
            foreach (var error in ex.Errors)
            {
                this.AddError(error);
            }

            await this.Send.ErrorsAsync(statusCode: StatusCodes.Status400BadRequest, ct);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
        {
            this.AddError($"A menu with the name '{req.Name}' already exists.");

            logger.CreateMenuConflict(req.Name);
            await this.Send.ErrorsAsync(statusCode: StatusCodes.Status409Conflict, ct);
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

            logger.CreateMenuFailed(req.Name, ex.Message);
            await this.Send.ErrorsAsync(statusCode: StatusCodes.Status400BadRequest, ct);
        }
        catch (Exception ex)
        {
            logger.EndpointError(ex, nameof(CreateMenu));
            throw;
        }
    }
}

public record CreateMenuRequest(
    string Name,
    IReadOnlyList<MenuItem>? RootItems);

public record CreateMenuResponse(Guid Id);

internal class CreateMenuRequestValidator : Validator<CreateMenuRequest>
{
    public CreateMenuRequestValidator()
    {
        this.RuleFor(r => r.Name)
            .NotEmpty()
            .Matches(@"^[a-zA-Z0-9][a-zA-Z0-9-]*[a-zA-Z0-9]$")
            .WithMessage("Name must contain only alphanumeric characters and hyphens, and cannot start or end with a hyphen");

        // Validate total item count
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
