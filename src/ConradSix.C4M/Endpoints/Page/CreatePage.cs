#pragma warning disable SA1402 // File may only contain a single type
namespace ConradSix.C4M.Endpoints.Page;

using System.Diagnostics;

using ConradSix.C4M;
using ConradSix.C4M.Data.Model;
using ConradSix.C4M.Services;
using ConradSix.C4M.Services.Model;

using FastEndpoints;

using FluentValidation;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

[CmsEndpoint(CmsEndpointKind.Editor)]
internal class CreatePage(IPageService pages, ILogger<CreatePage> logger)
    : Endpoint<CreatePageRequest, CreatePageResponse>
{
    public override void Configure()
    {
        this.Post("/pages");
        this.Description(x => x.ProducesProblemFE(StatusCodes.Status409Conflict));
        this.Summary(s =>
        {
            s.Summary = "Creates a new page.";
            s.Description = "Creates a new page with the specified content blocks, optionally leaving its initial version as a draft.";
            s.Response<CreatePageResponse>(StatusCodes.Status201Created, "Page was successfully created.");
            s.Response(StatusCodes.Status400BadRequest, "The request is invalid or would create a cycle.");
            s.Response(StatusCodes.Status409Conflict, "A page with the same path already exists.");
            s.ExampleRequest = new CreatePageRequest(
                ParentId: null,
                NavigationMenuId: null,
                Slug: "about-us",
                Blocks: [
                    new CreatePageBlock(
                        BlockTypeId: Guid.Parse("87654321-4321-4321-4321-210987654321"),
                        BlockData: /*lang=json,strict*/ """{"title": "About Us", "content": "Welcome to our website"}"""),
                ],
                Seo: new PageSeoRequest(
                    Title: "About Top Villas Realty",
                    Description: "Learn about the experience behind Top Villas Realty.",
                    SocialImageId: null),
                Publish: true);
        });
    }

    public override async Task HandleAsync(CreatePageRequest req, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var blocks = req.Blocks.Select(b => new PageBlock(
                BlockId: null,
                BlockTypeId: b.BlockTypeId,
                BlockData: b.BlockData,
                InstanceId: b.InstanceId))
                .ToList();
            var result = await pages.CreatePageAsync(
                new NewPage(null, req.ParentId, req.NavigationMenuId, req.Slug, blocks, ToMetadata(req.Seo), req.Publish),
                ct);
            switch (result)
            {
                case PageCreated created:
                    await this.Send.CreatedAtAsync<GetPage>(
                        new { Id = created.PageId },
                        new CreatePageResponse(created.PageId),
                        cancellation: ct);
                    logger.CreatePageEndpointCompleted(created.PageId, created.Path, stopwatch.ElapsedMilliseconds);
                    return;
                case CreatePagePathTaken or CreatePageIdTaken:
                    this.AddError(r => r.Slug, $"A page with the slug '{req.Slug}' already exists at this location.");
                    logger.CreatePageEndpointConflict(req.Slug);
                    await this.Send.ErrorsAsync(statusCode: StatusCodes.Status409Conflict, ct);
                    return;
                case CreatePageParentMissing:
                    this.AddError(r => r.ParentId, "The selected parent page does not exist.");
                    break;
                case CreatePageMenuMissing:
                    this.AddError(r => r.NavigationMenuId, "The selected navigation menu does not exist.");
                    break;
                case CreatePageReserved reserved:
                    this.AddError(r => r.Slug, $"The path '{reserved.Path}' is reserved.");
                    break;
                case CreatePageInvalid invalid:
                    foreach (var error in invalid.Errors)
                    {
                        this.AddError(error);
                    }

                    break;
            }

            await this.Send.ErrorsAsync(cancellation: ct);
        }
        catch (Exception ex)
        {
            logger.EndpointError(ex, nameof(CreatePage));
            throw;
        }
    }

    private static PageVersionMetadata ToMetadata(PageSeoRequest? seo) => new(
        SeoTitle: seo?.Title,
        SeoDescription: seo?.Description,
        SeoImageId: seo?.SocialImageId,
        RobotsIndex: seo?.Index ?? true,
        RobotsFollow: seo?.Follow ?? true);
}

internal class CreatePageRequestValidator : Validator<CreatePageRequest>
{
    public CreatePageRequestValidator()
    {
        this.RuleFor(r => r.Slug)
            .NotEmpty()
            .Slug();

        this.RuleFor(r => r.Blocks)
            .NotNull();

        this.RuleFor(r => r.NavigationMenuId)
            .NotEqual(Guid.Empty)
            .When(r => r.NavigationMenuId.HasValue);

        this.RuleForEach(r => r.Blocks)
            .NewBlock();

        this.When(r => r.Seo is not null, () =>
        {
            this.RuleFor(r => r.Seo!.Title)
                .MaximumLength(PageVersionEntity.SeoTitleMaxLength);

            this.RuleFor(r => r.Seo!.Description)
                .MaximumLength(PageVersionEntity.SeoDescriptionMaxLength);
        });
    }
}

public record CreatePageRequest(
    Guid? ParentId,
    string Slug,
    IReadOnlyList<CreatePageBlock> Blocks,
    PageSeoRequest? Seo = null,
    bool Publish = true,
    Guid? NavigationMenuId = null);

public record CreatePageBlock(
    Guid BlockTypeId,
    string BlockData,
    Guid? InstanceId = null) : INewBlock;

public record CreatePageResponse(Guid Id);
