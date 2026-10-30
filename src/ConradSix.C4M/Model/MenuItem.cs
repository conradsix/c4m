namespace Parapet.CMS.Engine.Model;

public record MenuItem(
    string Title,
    Guid ActionTypeId,
    string ActionData,
    IReadOnlyList<MenuItem>? Children);
