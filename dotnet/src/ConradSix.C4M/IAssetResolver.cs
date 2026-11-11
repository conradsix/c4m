#pragma warning disable SA1402 // The asset contract is kept with its resolver.
namespace ConradSix.C4M;

public interface IAssetResolver
{
    Task<IReadOnlyDictionary<Guid, CmsImageAsset>> ResolveImagesAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken);
}

// The default: resolves no images, so responses leave image details out.
public sealed class NullAssetResolver : IAssetResolver
{
    public Task<IReadOnlyDictionary<Guid, CmsImageAsset>> ResolveImagesAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, CmsImageAsset>>(new Dictionary<Guid, CmsImageAsset>());
}

#pragma warning disable CA1054 // URI-like parameters should not be strings
#pragma warning disable CA1056 // URI-like properties should not be strings
public sealed record CmsImageAsset(
    Guid Id,
    int? Width,
    int? Height,
    string Url,
    string AltText);
