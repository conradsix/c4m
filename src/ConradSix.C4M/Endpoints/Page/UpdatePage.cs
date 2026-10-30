#pragma warning disable SA1402 // Endpoint contracts are kept with their endpoint.
namespace Parapet.CMS.Engine.Endpoints.Page;

using System.Diagnostics;

using FastEndpoints;

using FluentValidation;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using Parapet.CMS.Engine;
using Parapet.CMS.Engine.Data.Model;
using Parapet.CMS.Engine.Services;
using Parapet.CMS.Engine.Services.Model;

[CmsEndpoint(CmsEndpointKind.Editor)]
internal class UpdatePage(IPageVersionServices pageVersionServices, ILogger<UpdatePage> logger)
    : Endpoint<UpdatePageRequest, UpdatePageResponse>
{
    public override void Configure()
    {
        this.Put("/pages/{id}");
        this.Description(x => x.ProducesProblemFE(StatusCodes.Status404NotFound).Produces<PageDraftConflict>(StatusCodes.Status409Conflict));
        this.Summary(s => s.Summary = "Creates a content and SEO draft without changing page details or publication.");
    }

    public override async Task HandleAsync(UpdatePageRequest req, CancellationToken ct)
    {
        var pageId = this.Route<Guid>("id");
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var sortedBlocks = new List<(PageBlock Block, int SortOrder)>();

            foreach (var newBlock in req.NewBlocks)
            {
                sortedBlocks.Add((
                    new(
                        BlockId: null,
                        BlockTypeId: newBlock.BlockTypeId,
                        BlockData: newBlock.BlockData,
                        InstanceId: newBlock.InstanceId),
                    newBlock.SortOrder));
            }

            foreach (var existingBlock in req.ExistingBlocks)
            {
                sortedBlocks.Add((
                    new(
                        BlockId: existingBlock.BlockId,
                        BlockTypeId: null,
                        BlockData: null),
                    existingBlock.SortOrder));
            }

            var orderedBlocks = sortedBlocks
                .OrderBy(x => x.SortOrder)
                .Select(x => x.Block)
                .ToArray();

            var result = await pageVersionServices.CreatePageVersionAsync(
                pageId,
                orderedBlocks,
                ToMetadata(req.Seo),
                new PageVersionWrite(ExpectedLatestPageVersionId: req.ExpectedLatestPageVersionId),
                ct);
            if (result is PageVersionPageMissing)
            {
                await this.Send.NotFoundAsync(ct);
                return;
            }

            if (result is PageVersionConflict conflict)
            {
                this.HttpContext.Response.StatusCode = StatusCodes.Status409Conflict;
                await this.HttpContext.Response.WriteAsJsonAsync(new PageDraftConflict(conflict.LatestPageVersionId), ct);
                return;
            }

            var pageVersionId = ((PageVersionSaved)result).PageVersionId;
            await this.Send.CreatedAtAsync<GetPageByVersion>(
                new { Id = pageVersionId },
                new UpdatePageResponse(pageVersionId),
                cancellation: ct);
            logger.UpdatePageEndpointCompleted(pageId, stopwatch.ElapsedMilliseconds);
        }
        catch (ArgumentException ex) when (ex is CmsBlockRetiredException or CmsBlockValidationException)
        {
            this.AddError(ex.Message);
            await this.Send.ErrorsAsync(cancellation: ct);
        }
        catch (Exception ex)
        {
            logger.EndpointError(ex, nameof(UpdatePage));
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

internal class UpdatePageRequestValidator : Validator<UpdatePageRequest>
{
    public UpdatePageRequestValidator()
    {
        this.RuleFor(r => r.ExpectedLatestPageVersionId).NotEmpty();

        this.RuleFor(r => r.NewBlocks)
            .NotNull();

        this.RuleFor(r => r.ExistingBlocks)
            .NotNull();

        this.When(r => r.Seo is not null, () =>
        {
            this.RuleFor(r => r.Seo!.Title)
                .MaximumLength(PageVersionEntity.SeoTitleMaxLength);

            this.RuleFor(r => r.Seo!.Description)
                .MaximumLength(PageVersionEntity.SeoDescriptionMaxLength);
        });

        this.RuleForEach(r => r.NewBlocks)
            .SetValidator(new UpdatePageNewBlockValidator());

        this.RuleForEach(r => r.ExistingBlocks)
            .SetValidator(new UpdatePageExistingBlockValidator());

        this.RuleFor(r => r)
            .Custom((request, context) =>
            {
                // Check for duplicate sort orders
                var allSortOrders = new List<int>();
                allSortOrders.AddRange(request.NewBlocks.Select(b => b.SortOrder));
                allSortOrders.AddRange(request.ExistingBlocks.Select(b => b.SortOrder));

                var duplicates = allSortOrders.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key);
                if (duplicates.Any())
                {
                    context.AddFailure("SortOrder", $"Duplicate sort orders found: {string.Join(", ", duplicates)}");
                }
            });
    }
}

internal class UpdatePageNewBlockValidator : Validator<UpdatePageNewBlock>
{
    public UpdatePageNewBlockValidator()
    {
        this.RuleFor(b => b.SortOrder)
            .GreaterThanOrEqualTo(0);

        this.RuleFor(b => b)
            .NewBlock();
    }
}

internal class UpdatePageExistingBlockValidator : Validator<UpdatePageExistingBlock>
{
    public UpdatePageExistingBlockValidator()
    {
        this.RuleFor(b => b.SortOrder)
            .GreaterThanOrEqualTo(0);

        this.RuleFor(b => b)
            .ExistingBlock();
    }
}

public sealed record UpdatePageRequest(
    Guid ExpectedLatestPageVersionId,
    IReadOnlyList<UpdatePageNewBlock> NewBlocks,
    IReadOnlyList<UpdatePageExistingBlock> ExistingBlocks,
    PageSeoRequest? Seo = null);

public sealed record PageDraftConflict(Guid? LatestPageVersionId);

public record UpdatePageNewBlock(
    Guid BlockTypeId,
    int SortOrder,
    string BlockData,
    Guid? InstanceId = null) : INewBlock;

public record UpdatePageExistingBlock(
    Guid BlockId,
    int SortOrder) : IExistingBlock;

public record UpdatePageResponse(Guid PageVersionId);
