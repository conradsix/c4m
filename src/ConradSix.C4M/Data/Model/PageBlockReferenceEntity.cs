namespace Parapet.CMS.Engine.Data.Model;

using System.ComponentModel.DataAnnotations.Schema;

using Microsoft.EntityFrameworkCore.Infrastructure;

// Something a block's data points at, such as a page, an image or a search area. Blocks never change, so neither do their
// references; the ordinal is where the reference first appears in the block.
internal sealed class PageBlockReferenceEntity : IHasCreatedAt
{
    private readonly ILazyLoader? _lazyLoader;

    public PageBlockReferenceEntity()
    {
    }

#pragma warning disable IDE0051 // Remove unused private members
    private PageBlockReferenceEntity(ILazyLoader lazyLoader) => this._lazyLoader = lazyLoader;
#pragma warning restore IDE0051 // Remove unused private members

    public Guid PageBlockId { get; set; }

    public string Kind { get; set; } = string.Empty;

    public Guid TargetId { get; set; }

    public string JsonPath { get; set; } = string.Empty;

    public int Ordinal { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    [ForeignKey(nameof(PageBlockId))]
    public PageBlockEntity PageBlock
    {
        get => this._lazyLoader?.Load(this, ref field)
            ?? throw new InvalidOperationException($"{nameof(this.PageBlock)} must be loaded before accessing.");
        set;
    }
}
