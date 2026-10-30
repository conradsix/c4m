namespace Parapet.CMS.Engine.Data.Model;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using Microsoft.EntityFrameworkCore.Infrastructure;

internal sealed class PageVersionEntity : IHasGuidId, IHasCreatedAt
{
    public const int SeoTitleMaxLength = 60;
    public const int SeoDescriptionMaxLength = 160;

    private readonly ILazyLoader? _lazyLoader;

    public PageVersionEntity()
    {
    }

#pragma warning disable IDE0051 // Remove unused private members
    private PageVersionEntity(ILazyLoader lazyLoader) => this._lazyLoader = lazyLoader;
#pragma warning restore IDE0051 // Remove unused private members

    public Guid Id { get; set; }

    public Guid PageId { get; set; }

    public bool? IsCurrent { get; set; }

    [MaxLength(SeoTitleMaxLength)]
    public string? SeoTitle { get; set; }

    [MaxLength(SeoDescriptionMaxLength)]
    public string? SeoDescription { get; set; }

    public Guid? SeoImageId { get; set; }

    public bool RobotsIndex { get; set; } = true;

    public bool RobotsFollow { get; set; } = true;

    [Column(TypeName = "jsonb")]
    public string? ChangeSummaryJson { get; set; }

    // Set by a caller that may retry the write that created this version.
    [MaxLength(200)]
    public string? IdempotencyKey { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    [ForeignKey(nameof(PageId))]
    public PageEntity Page
    {
        get => this._lazyLoader?.Load(this, ref field)
            ?? throw new InvalidOperationException($"{nameof(this.Page)} must be loaded before accessing.");
        set;
    }

    public ICollection<PageBlockEntity> Blocks =>
        this._lazyLoader?.Load(this, ref field)
            ?? (field = new HashSet<PageBlockEntity>());

    public ICollection<PageVersionBlockEntity> VersionBlocks =>
        this._lazyLoader?.Load(this, ref field)
            ?? (field = new HashSet<PageVersionBlockEntity>());
}
