namespace ConradSix.C4M;

public sealed class C4MOptions
{
    public ISet<string> ReservedRootSegments { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public ISet<string> RetiredBlockTypes { get; } = new HashSet<string>(StringComparer.Ordinal);
}
