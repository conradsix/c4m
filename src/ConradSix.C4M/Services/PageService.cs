#pragma warning disable SA1402 // The page creation contracts are kept with the service.
namespace Parapet.CMS.Engine.Services;

using Microsoft.EntityFrameworkCore;

using Parapet.CMS.Engine;
using Parapet.CMS.Engine.Data;
using Parapet.CMS.Engine.Data.Model;
using Parapet.CMS.Engine.Services.Model;

public interface IPageService
{
    // Creates a page and its first version in one transaction of its own. A caller that chooses the page's id can
    // repeat the call after a failure of its own and gets the same page back.
    Task<CreatePageResult> CreatePageAsync(NewPage page, CancellationToken cancellationToken = default);
}

// IdempotencyKey keys the page's first version, as PageVersionWrite does for later ones: a caller writing a version
// with the same key afterwards finds that one.
public sealed record NewPage(
    Guid? PageId,
    Guid? ParentId,
    Guid? NavigationMenuId,
    string Slug,
    IReadOnlyList<PageBlock> Blocks,
    PageVersionMetadata Metadata,
    bool Publish,
    string? IdempotencyKey = null);

public abstract record CreatePageResult;

public sealed record PageCreated(Guid PageId, Guid PageVersionId, string Path, bool Replayed) : CreatePageResult;

public sealed record CreatePageParentMissing : CreatePageResult;

public sealed record CreatePageMenuMissing : CreatePageResult;

// Another page already has the slug under the same parent.
public sealed record CreatePagePathTaken : CreatePageResult;

public sealed record CreatePageReserved(string Path) : CreatePageResult;

// The blocks were rejected, or the page couldn't be published.
public sealed record CreatePageInvalid(IReadOnlyList<string> Errors) : CreatePageResult;

// A different page already has the chosen id.
public sealed record CreatePageIdTaken : CreatePageResult;

internal sealed class PageService(
    CmsEngineDbContext ctx,
    PageVersionServices versions,
    CmsPathPolicy pathPolicy,
    ICmsChangeHandler changeHandler) : IPageService
{
    public async Task<CreatePageResult> CreatePageAsync(NewPage page, CancellationToken cancellationToken = default)
    {
        CmsTransactions.EnsureNoCallerTransaction(ctx.Database);
        try
        {
            var (result, published) = await this.CreateAsync(page, cancellationToken);
            switch (result)
            {
                case PageCreated created when published:
                    await changeHandler.PageChangedAsync(created.Path);
                    break;
                case PageCreated:
                    break;
                default:
                    CmsTransactions.ForgetUnsaved(ctx.ChangeTracker);
                    break;
            }

            return result;
        }
        catch
        {
            CmsTransactions.ForgetUnsaved(ctx.ChangeTracker);
            throw;
        }
    }

    // The result, and whether a published page was created, which the change handler needs to hear about.
    private async Task<(CreatePageResult Result, bool Published)> CreateAsync(NewPage page, CancellationToken cancellationToken)
    {
        await using var transaction = await ctx.Database.BeginTransactionAsync(cancellationToken);
        await CmsLocks.PublicationAsync(ctx.Database, cancellationToken);
        if (page.PageId is { } chosenId && await ctx.Pages.AsNoTracking()
            .Where(existing => existing.Id == chosenId)
            .Select(existing => new
            {
                existing.ParentId,
                existing.Slug,
                existing.Path,
                existing.ArchivedAt,
                Current = existing.Versions.Where(version => version.IsCurrent == true).Select(version => (Guid?)version.Id).FirstOrDefault(),
            })
            .SingleOrDefaultAsync(cancellationToken) is { } existing)
        {
            return (existing.ParentId == page.ParentId && existing.Slug == page.Slug && existing.ArchivedAt is null && existing.Current is { } versionId
                ? new PageCreated(chosenId, versionId, existing.Path, Replayed: true)
                : new CreatePageIdTaken(), false);
        }

        if (page.ParentId.HasValue && !await ctx.Pages.AnyAsync(parent => parent.Id == page.ParentId && parent.ArchivedAt == null, cancellationToken))
        {
            return (new CreatePageParentMissing(), false);
        }

        var entity = new PageEntity
        {
            ParentId = page.ParentId,
            NavigationMenuId = page.NavigationMenuId,
            Slug = page.Slug,
        };
        if (page.PageId is { } id)
        {
            entity.Id = id;
        }

        ctx.Pages.Add(entity);
        try
        {
            await ctx.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
        {
            return (new CreatePagePathTaken(), false);
        }
        catch (DbUpdateException ex) when (ex.IsForeignKeyConstraintViolation())
        {
            return (ex.GetForeignKeyConstraintName()?.Contains("navigation_menu", StringComparison.OrdinalIgnoreCase) == true
                ? new CreatePageMenuMissing()
                : new CreatePageParentMissing(), false);
        }

        if (pathPolicy.IsReserved(entity.Path))
        {
            return (new CreatePageReserved(entity.Path), false);
        }

        await CmsLocks.PageAsync(ctx.Database, entity.Id, cancellationToken);
        Guid pageVersionId;
        try
        {
            pageVersionId = await versions.AppendAsync(entity.Id, page.Blocks, page.Metadata, page.IdempotencyKey, cancellationToken);
        }
        catch (ArgumentException ex) when (ex is CmsBlockRetiredException or CmsBlockValidationException)
        {
            return (new CreatePageInvalid([ex.Message]), false);
        }

        if (page.Publish)
        {
            var errors = await versions.ValidateForPublicationAsync(pageVersionId, cancellationToken);
            if (errors.Count > 0)
            {
                return (new CreatePageInvalid(errors), false);
            }

            entity.IsPublished = true;
            await ctx.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return (new PageCreated(entity.Id, pageVersionId, entity.Path, Replayed: false), page.Publish);
    }
}
