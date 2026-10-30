namespace Parapet.CMS.Engine.Model;

public record ConflictResponse(
    Guid Id,
    uint Version,
    DateTimeOffset UpdatedAt);
