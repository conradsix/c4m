namespace Parapet.CMS.Engine.Services;

using Microsoft.EntityFrameworkCore;

using Parapet.CMS.Engine;
using Parapet.CMS.Engine.Data;
using Parapet.CMS.Engine.Endpoints.Page;

internal static class PagePublicationGuard
{
    public static Task LockAsync(CmsEngineDbContext ctx, CancellationToken ct) => CmsLocks.PublicationAsync(ctx.Database, ct);

    public static async Task<(int Total, IReadOnlyList<UnpublishMenuReference> Menus)> GetReferencingMenusAsync(
        CmsEngineDbContext ctx,
        Guid pageId,
        CancellationToken ct)
    {
        var referencingMenus = ctx.Database.SqlQuery<UnpublishMenuReference>($"""
            WITH RECURSIVE references_to_page AS (
                SELECT item.id, item.parent_id, item.menu_id, item.title, item.id AS referenced_item_id
                FROM cms.menu_items item
                JOIN cms.menu_item_page_references reference ON reference.menu_item_id = item.id
                WHERE reference.referenced_page_id = {pageId}
                UNION ALL
                SELECT parent.id, parent.parent_id, parent.menu_id, child.title, child.referenced_item_id
                FROM cms.menu_items parent
                JOIN references_to_page child ON child.parent_id = parent.id
            )
            SELECT DISTINCT ON (menu.id) menu.id AS menu_id, menu.name AS name, reference.title AS item_title
            FROM references_to_page reference
            JOIN cms.menus menu ON menu.id = reference.menu_id
            ORDER BY menu.id, reference.title, reference.referenced_item_id
            """);
        var menuCount = await referencingMenus.CountAsync(ct);
        var menus = await referencingMenus.OrderBy(menu => menu.Name).ThenBy(menu => menu.MenuId).Take(3).ToArrayAsync(ct);
        return (menuCount, menus);
    }

    public static async Task<IReadOnlyList<string>> GetUnpublishedAncestorsAsync(
        CmsEngineDbContext ctx,
        Guid? parentId,
        CancellationToken ct)
    {
        var paths = new List<string>();
        while (parentId.HasValue)
        {
            var parent = await ctx.Pages.Where(page => page.Id == parentId)
                .Select(page => new
                {
                    page.ParentId,
                    page.Path,
                    page.ArchivedAt,
                    page.IsPublished,
                    HasCurrent = page.Versions.Any(version => version.IsCurrent == true),
                }).SingleOrDefaultAsync(ct);
            if (parent is null)
            {
                break;
            }

            if (parent.ArchivedAt.HasValue || !parent.IsPublished || !parent.HasCurrent)
            {
                paths.Add(parent.Path);
            }

            parentId = parent.ParentId;
        }

        paths.Sort(StringComparer.Ordinal);
        return paths;
    }
}
