namespace ConradSix.C4M.Data;

using ConradSix.C4M.Data.Model;

using Microsoft.EntityFrameworkCore;

internal static class CmsEngineDbContextExtensions
{
    public static IQueryable<MenuItemEntity> GetMenuItems(this DbSet<MenuItemEntity> menuItems, Guid menuId) =>
        menuItems.FromSql($"SELECT * FROM cms.get_menu_items({menuId})");
}
