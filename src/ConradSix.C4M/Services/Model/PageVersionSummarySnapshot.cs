namespace Parapet.CMS.Engine.Services.Model;

internal sealed record PageVersionSummarySnapshot(
    PageVersionMetadata Metadata,
    IReadOnlyList<PageVersionSummaryBlock> Blocks);
