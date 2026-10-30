namespace ConradSix.C4M.Services;

using ConradSix.C4M;
using ConradSix.C4M.Data;
using ConradSix.C4M.Data.Model;
using ConradSix.C4M.Exceptions;
using ConradSix.C4M.Model;

using Microsoft.EntityFrameworkCore;

internal class MenuService(CmsEngineDbContext ctx, BlockReferenceIndexer referenceIndexer)
    : IMenuService
{
    public async Task<Guid> CreateMenuAsync(
        string name,
        IReadOnlyList<MenuItem>? rootItems,
        CancellationToken cancellationToken = default)
    {
        await this.ValidatePageReferencesAsync(rootItems, cancellationToken);
        var menu = new MenuEntity
        {
            Name = name,
        };

        ctx.Menus.Add(menu);
        await ctx.SaveChangesAsync(cancellationToken);

        if (rootItems != null)
        {
            for (var i = 0; i < rootItems.Count; i++)
            {
                await this.CreateMenuItemRecursivelyAsync(
                    rootItems[i],
                    menuId: menu.Id,
                    parentId: null,
                    sortOrder: i,
                    cancellationToken);
            }

            await ctx.SaveChangesAsync(cancellationToken);
        }

        return menu.Id;
    }

    public async Task UpdateMenuAsync(
        Guid menuId,
        uint version,
        IReadOnlyList<MenuItem>? rootItems,
        CancellationToken cancellationToken = default)
    {
        var menu = await ctx.Menus.FindAsync([menuId], cancellationToken)
            ?? throw new InvalidOperationException($"Menu with ID {menuId} not found.");

        // Check version for optimistic concurrency
        if (menu.Version != version)
        {
            throw new DbUpdateConcurrencyException($"Menu with ID {menuId} has been modified by another user.");
        }

        await this.ValidatePageReferencesAsync(rootItems, cancellationToken);
        await ctx.MenuItems
            .Where(mi => mi.MenuId == menuId ||
                   ctx.MenuItems.Any(root => root.MenuId == menuId && root.Id == mi.ParentId))
            .ExecuteDeleteAsync(cancellationToken);

        if (rootItems != null)
        {
            for (var i = 0; i < rootItems.Count; i++)
            {
                await this.CreateMenuItemRecursivelyAsync(
                    rootItems[i],
                    menuId: menu.Id,
                    parentId: null,
                    sortOrder: i,
                    cancellationToken);
            }
        }

        ctx.Entry(menu).Property(m => m.Version).IsModified = true;

        await ctx.SaveChangesAsync(cancellationToken);
    }

    private async Task ValidatePageReferencesAsync(IReadOnlyList<MenuItem>? rootItems, CancellationToken ct)
    {
        // The endpoints hold the publication lock throughout validation and persistence.
        var references = new List<(string ItemPath, Guid PageId)>();
        visit(rootItems, string.Empty);
        if (references.Count == 0)
        {
            return;
        }

        var ids = references.Select(reference => reference.PageId).Distinct().ToArray();
        var pages = await ctx.Pages.Where(page => ids.Contains(page.Id))
            .Select(page => new
            {
                page.Id,
                page.Path,
                page.IsPublished,
                page.ArchivedAt,
                HasCurrent = page.Versions.Any(version => version.IsCurrent == true),
            }).ToDictionaryAsync(page => page.Id, ct);
        var errors = new List<string>();
        foreach (var (itemPath, pageId) in references)
        {
            if (!pages.TryGetValue(pageId, out var page))
            {
                errors.Add($"Menu item '{itemPath}' references missing page '{pageId}'. Choose another page or remove the item.");
            }
            else if (page.ArchivedAt.HasValue)
            {
                errors.Add($"Menu item '{itemPath}' references archived page '{page.Path}'. Choose another page or remove the item.");
            }
            else if (!page.IsPublished || !page.HasCurrent)
            {
                errors.Add($"Menu item '{itemPath}' references page '{page.Path}', which must be published with a Current version. Publish it, choose another page, or remove the item.");
            }
        }

        if (errors.Count > 0)
        {
            throw new MenuPageReferenceException(errors);
        }

        void visit(IReadOnlyList<MenuItem>? items, string parentPath)
        {
            foreach (var item in items ?? [])
            {
                var path = string.IsNullOrEmpty(parentPath) ? item.Title : $"{parentPath} → {item.Title}";
                foreach (var id in this.PageIds(item.ActionData))
                {
                    references.Add((path, id));
                }

                visit(item.Children, path);
            }
        }
    }

    private IEnumerable<Guid> PageIds(string actionData) => referenceIndexer.Index(actionData)
        .Where(reference => reference.Kind == CmsReferenceKinds.Page)
        .Select(reference => reference.TargetId)
        .Distinct();

    private async Task<Guid> CreateMenuItemRecursivelyAsync(
        MenuItem request,
        Guid? menuId,
        Guid? parentId,
        int sortOrder,
        CancellationToken cancellationToken)
    {
        var menuItem = new MenuItemEntity
        {
            MenuId = menuId,
            ParentId = parentId,
            Title = request.Title,
            ActionTypeId = request.ActionTypeId,
            ActionData = request.ActionData,
            SortOrder = sortOrder,
        };

        ctx.MenuItems.Add(menuItem);

        // Extract page references from ActionData
        var pageRefs = this.PageIds(request.ActionData);

        foreach (var pageRef in pageRefs)
        {
            var pageReference = new MenuItemPageReferenceEntity
            {
                MenuItem = menuItem,
                ReferencedPageId = pageRef,
            };
            ctx.MenuItemPageReferences.Add(pageReference);
        }

        // Recursively create children using their index as sort order
        if (request.Children != null)
        {
            for (var i = 0; i < request.Children.Count; i++)
            {
                await this.CreateMenuItemRecursivelyAsync(
                    request.Children[i],
                    menuId: null,
                    parentId: menuItem.Id,
                    sortOrder: i,
                    cancellationToken);
            }
        }

        return menuItem.Id;
    }
}
