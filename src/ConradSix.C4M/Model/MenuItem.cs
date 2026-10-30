namespace ConradSix.C4M.Model;

public record MenuItem(
    string Title,
    Guid ActionTypeId,
    string ActionData,
    IReadOnlyList<MenuItem>? Children);
