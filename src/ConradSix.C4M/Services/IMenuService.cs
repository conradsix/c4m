namespace Parapet.CMS.Engine.Services;

using Parapet.CMS.Engine.Model;

public interface IMenuService
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
