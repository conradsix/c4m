namespace ConradSix.C4M.Services.Model;

public sealed record PageVersionChangeSummary(
    int SchemaVersion,
    string Headline,
    IReadOnlyList<string> Changes)
{
    public const int CurrentSchemaVersion = 1;

    public static PageVersionChangeSummary Unavailable { get; } = new(
        CurrentSchemaVersion,
        "Summary not generated",
        []);
}
