#pragma warning disable SA1402 // The asset contract is kept with its resolver.
namespace Parapet.CMS.Engine;

public interface IAssetResolver
{
    Task<IReadOnlyDictionary<Guid, CmsImageAsset>> ResolveImagesAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken);
}

#pragma warning disable CA1054 // URI-like parameters should not be strings
#pragma warning disable CA1056 // URI-like properties should not be strings
public sealed record CmsImageAsset(
    Guid Id,
    int? Width,
    int? Height,
    string Url,
    string AltText);
