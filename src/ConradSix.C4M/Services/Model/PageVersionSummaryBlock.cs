namespace Parapet.CMS.Engine.Services.Model;

internal sealed record PageVersionSummaryBlock(
    Guid Id,
    string TypeName,
    int TypeVersion,
    int SortOrder,
    string BlockData);
