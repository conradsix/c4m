namespace ConradSix.C4M.Testing;

using System.Collections.Concurrent;

// An asset resolver that knows the images a test gives it, and no others.
public sealed class FakeAssetResolver : IAssetResolver
{
    private readonly ConcurrentDictionary<Guid, CmsImageAsset> _images = new();

    // Adds an image with a new id and returns it.
    public CmsImageAsset Add(int width = 1200, int height = 800, string altText = "An image")
    {
        var id = Guid.CreateVersion7();
        var image = new CmsImageAsset(id, width, height, $"https://images.example.com/{id:N}.jpg", altText);
        this._images[id] = image;
        return image;
    }

    public void Add(CmsImageAsset image)
    {
        ArgumentNullException.ThrowIfNull(image);
        this._images[image.Id] = image;
    }

    public void Clear() => this._images.Clear();

    public Task<IReadOnlyDictionary<Guid, CmsImageAsset>> ResolveImagesAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, CmsImageAsset>>(
            ids.Distinct().Where(this._images.ContainsKey).ToDictionary(id => id, id => this._images[id]));
}
