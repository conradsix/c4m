namespace Parapet.CMS.Engine.Services.Model;

public sealed record PageVersionMetadata(
    string? SeoTitle,
    string? SeoDescription,
    Guid? SeoImageId,
    bool RobotsIndex,
    bool RobotsFollow)
{
    public static PageVersionMetadata Empty { get; } = new(null, null, null, true, true);
}
