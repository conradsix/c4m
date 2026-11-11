namespace ConradSix.C4M.Services;

using ConradSix.C4M.Endpoints.Page;

internal interface IPageQueryService
{
    Task<GetPageResponse?> GetPageByIdCurrentAsync(Guid pageId, CancellationToken ct);

    Task<GetPageResponse?> GetPageByIdLatestAsync(Guid pageId, CancellationToken ct, bool includeArchived = false);

    Task<GetPageResponse?> GetPageByVersionIdAsync(Guid pageVersionId, CancellationToken ct, bool includeArchived = false);

    Task<GetPageResponse?> GetPageByPathAsync(string path, CancellationToken ct);

    Task<string?> GetPagePathByIdAsync(Guid pageId, CancellationToken ct);

    Task<ListPagesResponse> ListPagesAsync(ListPagesRequest request, CancellationToken ct);

    Task<GetPageHistoryResponse?> GetPageHistoryAsync(Guid pageId, CancellationToken ct, bool includeArchived = false);

    Task<IReadOnlyList<PageBreadcrumbItem>> GetPageBreadcrumbsAsync(Guid pageId, CancellationToken ct, bool includeArchived = false);
}
