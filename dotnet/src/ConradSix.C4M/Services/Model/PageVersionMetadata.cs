namespace ConradSix.C4M.Services.Model;

public sealed record PageVersionMetadata(
    string? SeoTitle,
    string? SeoDescription,
    Guid? SeoImageId,
    bool RobotsIndex,
    bool RobotsFollow)
{
    public static PageVersionMetadata Empty { get; } = new(null, null, null, true, true);
}
