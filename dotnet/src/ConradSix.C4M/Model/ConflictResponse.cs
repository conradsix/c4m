namespace ConradSix.C4M.Model;

public record ConflictResponse(
    Guid Id,
    uint Version,
    DateTimeOffset UpdatedAt);
