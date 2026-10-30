namespace Parapet.CMS.Engine.Data.Model;

using System.ComponentModel.DataAnnotations.Schema;

using Microsoft.EntityFrameworkCore.Infrastructure;

internal sealed class PageVersionBlockEntity : IHasCreatedAt
{
    private readonly ILazyLoader? _lazyLoader;

    public PageVersionBlockEntity()
    {
    }

#pragma warning disable IDE0051 // Remove unused private members
    private PageVersionBlockEntity(ILazyLoader lazyLoader) => this._lazyLoader = lazyLoader;
#pragma warning restore IDE0051 // Remove unused private members

    public Guid PageVersionId { get; set; }

    public Guid PageBlockId { get; set; }

    public int SortOrder { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    [ForeignKey(nameof(PageVersionId))]
    public PageVersionEntity PageVersion
    {
        get => this._lazyLoader?.Load(this, ref field)
            ?? throw new InvalidOperationException($"{nameof(this.PageVersion)} must be loaded before accessing.");
        set;
    }

    [ForeignKey(nameof(PageBlockId))]
    public PageBlockEntity PageBlock
    {
        get => this._lazyLoader?.Load(this, ref field)
            ?? throw new InvalidOperationException($"{nameof(this.PageBlock)} must be loaded before accessing.");
        set;
    }
}
