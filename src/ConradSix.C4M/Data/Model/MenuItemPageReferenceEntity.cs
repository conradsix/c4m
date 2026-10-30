namespace Parapet.CMS.Engine.Data.Model;

using System.ComponentModel.DataAnnotations.Schema;

using Microsoft.EntityFrameworkCore.Infrastructure;

internal sealed class MenuItemPageReferenceEntity
{
    private readonly ILazyLoader? _lazyLoader;

    public MenuItemPageReferenceEntity()
    {
    }

#pragma warning disable IDE0051 // Remove unused private members
    private MenuItemPageReferenceEntity(ILazyLoader lazyLoader) => this._lazyLoader = lazyLoader;
#pragma warning restore IDE0051 // Remove unused private members

    public Guid MenuItemId { get; set; }

    public Guid ReferencedPageId { get; set; }

    [ForeignKey(nameof(MenuItemId))]
    public MenuItemEntity MenuItem
    {
        get => this._lazyLoader?.Load(this, ref field)
            ?? throw new InvalidOperationException($"{nameof(this.MenuItem)} must be loaded before accessing.");
        set;
    }

    [ForeignKey(nameof(ReferencedPageId))]
    public PageEntity ReferencedPage
    {
        get => this._lazyLoader?.Load(this, ref field)
            ?? throw new InvalidOperationException($"{nameof(this.ReferencedPage)} must be loaded before accessing.");
        set;
    }
}
