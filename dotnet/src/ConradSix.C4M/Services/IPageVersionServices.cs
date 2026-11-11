namespace ConradSix.C4M.Services;

using ConradSix.C4M.Services.Model;

public interface IPageVersionServices
{
    // returns page version id
    Task<Guid> CreatePageVersionAsync(
        Guid pageId,
        IReadOnlyList<PageBlock> blocks,
        CancellationToken cancellationToken);

    Task<Guid> CreatePageVersionAsync(
        Guid pageId,
        IReadOnlyList<PageBlock> blocks,
        PageVersionMetadata metadata,
        CancellationToken cancellationToken);

    // Saves the version in its own transaction; it cannot be called inside a caller's.
    Task<PageVersionWriteResult> CreatePageVersionAsync(
        Guid pageId,
        IReadOnlyList<PageBlock> blocks,
        PageVersionMetadata metadata,
        PageVersionWrite write,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> ValidateForPublicationAsync(
        Guid pageVersionId,
        CancellationToken cancellationToken = default);

    // returns previous page version id
    Task<Guid> SetCurrentPageVersionAsync(
        Guid pageVersionId,
        CancellationToken cancellationToken = default);
}
