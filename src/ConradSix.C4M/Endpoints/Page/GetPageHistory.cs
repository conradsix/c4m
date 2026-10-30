#pragma warning disable SA1402 // File may only contain a single type
namespace Parapet.CMS.Engine.Endpoints.Page;

using FastEndpoints;

using Microsoft.AspNetCore.Http;

using Parapet.CMS.Engine;
using Parapet.CMS.Engine.Services;
using Parapet.CMS.Engine.Services.Model;

[CmsEndpoint(CmsEndpointKind.Editor)]
internal class GetPageHistory(IPageQueryService pageQueryService)
    : Endpoint<GetPageHistoryRequest, GetPageHistoryResponse>
{
    public override void Configure()
    {
        this.Get("/pages/{id}/history");
        this.Description(x => x.ProducesProblemFE(StatusCodes.Status404NotFound));
        this.Summary(s =>
        {
            s.Summary = "Gets page version history by page ID.";
            s.Description = "Retrieves the complete version history for a specific page, including high-level change summaries.";
            s.Response<GetPageHistoryResponse>(StatusCodes.Status200OK, "Page history was successfully retrieved.");
            s.Response(StatusCodes.Status404NotFound, "The page was not found.");
        });
    }

    public override async Task HandleAsync(GetPageHistoryRequest req, CancellationToken ct)
    {
        var pageId = this.Route<Guid?>("id");
        if (pageId == null)
        {
            await this.Send.NotFoundAsync(ct);
            return;
        }

        var response = await pageQueryService.GetPageHistoryAsync(pageId.Value, ct, req.IncludeArchived);

        if (response == null)
        {
            await this.Send.NotFoundAsync(ct);
            return;
        }

        await this.Send.OkAsync(response, ct);
    }
}

public sealed record GetPageHistoryRequest(bool IncludeArchived = false);

public sealed record GetPageHistoryResponse(
    IReadOnlyList<GetPageHistoryResponseItem> Versions,
    PageHistoryMetadata? Page = null);

public sealed record PageHistoryMetadata(
    Guid PageId,
    Guid? ParentId,
    string Slug,
    string Path,
    Guid? NavigationMenuId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ArchivedAt,
    string? NavigationMenuName = null);

public sealed record GetPageHistoryResponseItem(
    Guid PageVersionId,
    bool IsCurrent,
    PageVersionChangeSummary ChangeSummary,
    DateTimeOffset CreatedAt);
