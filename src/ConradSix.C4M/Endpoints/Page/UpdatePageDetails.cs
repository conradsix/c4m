#pragma warning disable SA1402 // File may only contain a single type
namespace ConradSix.C4M.Endpoints.Page;

using System.Diagnostics;

using ConradSix.C4M;
using ConradSix.C4M.Data;
using ConradSix.C4M.Model;
using ConradSix.C4M.Services;

using FastEndpoints;

using FluentValidation;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

[CmsEndpoint(CmsEndpointKind.Editor)]
internal class UpdatePageDetails(CmsEngineDbContext ctx, ICmsChangeHandler changeHandler, CmsPathPolicy pathPolicy, IPageQueryService pageQueryService, ILogger<UpdatePageDetails> logger)
    : Endpoint<UpdatePageDetailsRequest, GetPageResponse>
{
    public override void Configure()
    {
        this.Put("/pages/{id}/details");
        this.Description(x => x.ProducesProblemFE(StatusCodes.Status404NotFound).Produces<ConflictResponse>(StatusCodes.Status409Conflict));
        this.Summary(s => s.Summary = "Updates unversioned page details without creating a content version.");
    }

    public override async Task HandleAsync(UpdatePageDetailsRequest req, CancellationToken ct)
    {
        var pageId = this.Route<Guid>("id");
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await using var tx = await ctx.Database.BeginTransactionAsync(ct);
            await PagePublicationGuard.LockAsync(ctx, ct);

            var page = await ctx.Pages.SingleOrDefaultAsync(page => page.Id == pageId && page.ArchivedAt == null, ct);
            if (page == null)
            {
                await this.Send.NotFoundAsync(ct);
                return;
            }

            // Check version match for optimistic concurrency
            if (page.Version != req.Version)
            {
                // Return conflict with current page data
                var conflictResponse = new ConflictResponse(
                    page.Id,
                    page.Version,
                    page.UpdatedAt);

                this.HttpContext.Response.StatusCode = StatusCodes.Status409Conflict;
                await this.HttpContext.Response.WriteAsJsonAsync(conflictResponse, ct);
                return;
            }

            if (req.ParentId.HasValue && !await ctx.Pages.AnyAsync(parent => parent.Id == req.ParentId && parent.ArchivedAt == null, ct))
            {
                this.AddError(r => r.ParentId, "The selected parent page does not exist.");
                await this.Send.ErrorsAsync(cancellation: ct);
                return;
            }

            if (page.ParentId != req.ParentId)
            {
                var descendantPrefix = page.Path + "/";
                var hasLivePages = await ctx.PageVersions.AnyAsync(
                    version => version.IsCurrent == true && version.Page.IsPublished &&
                        (version.PageId == pageId || version.Page.Path.StartsWith(descendantPrefix)),
                    ct);
                if (hasLivePages)
                {
                    var unpublishedAncestors = await PagePublicationGuard.GetUnpublishedAncestorsAsync(ctx, req.ParentId, ct);
                    if (unpublishedAncestors.Count > 0)
                    {
                        this.AddError(r => r.ParentId, $"Publish the ancestor pages before moving published pages beneath them: {string.Join(", ", unpublishedAncestors)}.");
                        await this.Send.ErrorsAsync(cancellation: ct);
                        return;
                    }
                }

                page.ParentId = req.ParentId;
            }

            if (page.NavigationMenuId != req.NavigationMenuId)
            {
                page.NavigationMenuId = req.NavigationMenuId;
            }

            if (page.Slug != req.Slug)
            {
                page.Slug = req.Slug;
            }

            ctx.Entry(page).Property(p => p.Version).IsModified = true;

            try
            {
                await ctx.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.IsCycleDetected())
            {
                this.AddError(r => r.ParentId, "Moving this page would create a circular reference in the page hierarchy.");
                await this.Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct);
                return;
            }
            catch (DbUpdateConcurrencyException)
            {
                // Another user has updated the page
                var currentPage = await ctx.Pages.AsNoTracking().SingleOrDefaultAsync(current => current.Id == pageId, ct);
                if (currentPage == null)
                {
                    await this.Send.NotFoundAsync(ct);
                    return;
                }

                var conflictResponse = new ConflictResponse(
                    currentPage.Id,
                    currentPage.Version,
                    currentPage.UpdatedAt);

                this.HttpContext.Response.StatusCode = StatusCodes.Status409Conflict;
                await this.HttpContext.Response.WriteAsJsonAsync(conflictResponse, ct);
                return;
            }
            catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
            {
                var constraintName = ex.GetUniqueConstraintName();

                if (constraintName?.Contains("path", StringComparison.OrdinalIgnoreCase) == true)
                {
                    this.AddError(r => r.Slug, $"A page with the slug '{req.Slug}' already exists at this location.");
                }
                else if (constraintName?.Contains("slug", StringComparison.OrdinalIgnoreCase) == true)
                {
                    this.AddError(r => r.Slug, $"A page with the slug '{req.Slug}' already exists at this location.");
                }
                else
                {
                    this.AddError("A page with the same path or slug already exists.");
                }

                await this.Send.ErrorsAsync(statusCode: StatusCodes.Status409Conflict, ct);
                return;
            }
            catch (DbUpdateException ex) when (ex.IsForeignKeyConstraintViolation())
            {
                var constraintName = ex.GetForeignKeyConstraintName();
                if (constraintName?.Contains("navigation_menu", StringComparison.OrdinalIgnoreCase) == true)
                {
                    this.AddError(r => r.NavigationMenuId, "The selected navigation menu does not exist.");
                }
                else
                {
                    this.AddError(r => r.ParentId, "The selected parent page does not exist.");
                }

                await this.Send.ErrorsAsync(cancellation: ct);
                return;
            }

            if (pathPolicy.IsReserved(page.Path))
            {
                this.AddError(r => r.Slug, $"The path '{page.Path}' is reserved.");
                await this.Send.ErrorsAsync(cancellation: ct);
                return;
            }

            await tx.CommitAsync(ct);

            // A published page's path and title can appear in every other page's links.
            if (page.IsPublished)
            {
                await changeHandler.ContentChangedAsync();
            }

            await ctx.Entry(page).ReloadAsync(ct);
            var response = await pageQueryService.GetPageByIdLatestAsync(pageId, ct);
            await this.Send.OkAsync(response!, ct);
            logger.UpdatePageDetailsEnd(pageId, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            logger.EndpointError(ex, nameof(UpdatePageDetails));
            throw;
        }
    }
}

public sealed record UpdatePageDetailsRequest(
    Guid? ParentId,
    string Slug,
    uint Version,
    Guid? NavigationMenuId = null);

internal sealed class UpdatePageDetailsRequestValidator : Validator<UpdatePageDetailsRequest>
{
    public UpdatePageDetailsRequestValidator()
    {
        this.RuleFor(r => r.Slug).NotEmpty().Slug();
        this.RuleFor(r => r.NavigationMenuId).NotEqual(Guid.Empty).When(r => r.NavigationMenuId.HasValue);
    }
}
