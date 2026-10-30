namespace Parapet.CMS.Engine;

// Extra checks a page version must pass before it can be published. Each returned string is one error.
public interface IPublicationRule
{
    Task<IReadOnlyList<string>> ValidateAsync(
        Guid pageId,
        Guid pageVersionId,
        CancellationToken cancellationToken);
}
