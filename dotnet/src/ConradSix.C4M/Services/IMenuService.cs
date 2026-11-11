namespace ConradSix.C4M.Services;

using ConradSix.C4M.Model;

internal interface IMenuService
{
    Task<Guid> CreateMenuAsync(
        string name,
        IReadOnlyList<MenuItem>? rootItems,
        CancellationToken cancellationToken = default);

    Task UpdateMenuAsync(
        Guid menuId,
        uint version,
        IReadOnlyList<MenuItem>? rootItems,
        CancellationToken cancellationToken = default);
}
