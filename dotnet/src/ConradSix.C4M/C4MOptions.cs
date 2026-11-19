namespace ConradSix.C4M;

public sealed class C4MOptions
{
    public bool RequirePublishedAncestors { get; set; } = true;

    public int MaxMenuItems { get; set; } = 20;

    public ISet<string> ReservedRootSegments { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public ISet<string> RetiredBlockTypes { get; } = new HashSet<string>(StringComparer.Ordinal);
}
