namespace Parapet.CMS.Engine;

public sealed class CmsEngineOptions
{
    public ISet<string> ReservedRootSegments { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public ISet<string> RetiredBlockTypes { get; } = new HashSet<string>(StringComparer.Ordinal);
}
