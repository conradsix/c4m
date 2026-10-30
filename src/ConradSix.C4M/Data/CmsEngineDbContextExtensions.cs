namespace Parapet.CMS.Engine.Data;

using Microsoft.EntityFrameworkCore;

using Parapet.CMS.Engine.Data.Model;

internal static class CmsEngineDbContextExtensions
{
    public static IQueryable<MenuItemEntity> GetMenuItems(this DbSet<MenuItemEntity> menuItems, Guid menuId) =>
        menuItems.FromSql($"SELECT * FROM cms.get_menu_items({menuId})");
}
