namespace ConradSix.C4M.Services.Model;

internal sealed record PageVersionSummarySnapshot(
    PageVersionMetadata Metadata,
    IReadOnlyList<PageVersionSummaryBlock> Blocks);
