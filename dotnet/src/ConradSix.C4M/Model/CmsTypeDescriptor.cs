namespace ConradSix.C4M.Model;

public sealed record CmsTypeDescriptor(
    Guid Id,
    string Name,
    int Version,
    string JsonSchema,
    DateTimeOffset CreatedAt,
    bool IsRetired = false);
