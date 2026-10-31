namespace ConradSix.C4M.Testing;

// A change the engine reported to its host's change handler. Path is set for page changes.
public sealed record CmsChange(CmsChangeKind Kind, string? Path = null)
{
    public static CmsChange Content { get; } = new(CmsChangeKind.Content);

    public static CmsChange Navigation { get; } = new(CmsChangeKind.Navigation);

    public static CmsChange Page(string path) => new(CmsChangeKind.Page, path);
}
