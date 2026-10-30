namespace ConradSix.C4M.Data.Model;

using System.ComponentModel.DataAnnotations.Schema;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

[Index(nameof(BlockTypeId))]
internal sealed class PageBlockEntity : IHasGuidId, IHasCreatedAt
{
    private readonly ILazyLoader? _lazyLoader;

    public PageBlockEntity()
    {
    }

#pragma warning disable IDE0051 // Remove unused private members
    private PageBlockEntity(ILazyLoader lazyLoader) => this._lazyLoader = lazyLoader;
#pragma warning restore IDE0051 // Remove unused private members

    public Guid Id { get; set; }

    public Guid InstanceId { get; set; } = Guid.NewGuid();

    public Guid BlockTypeId { get; set; }

    [Column(TypeName = "jsonb")]
    public string BlockData { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    [ForeignKey(nameof(BlockTypeId))]
    public BlockTypeEntity BlockType
    {
        get => this._lazyLoader?.Load(this, ref field)
            ?? throw new InvalidOperationException($"{nameof(this.BlockType)} must be loaded before accessing.");
        set;
    }

    public ICollection<PageVersionEntity> PageVersions =>
        this._lazyLoader?.Load(this, ref field) ?? (field = new HashSet<PageVersionEntity>());

    public ICollection<PageVersionBlockEntity> VersionBlocks =>
        this._lazyLoader?.Load(this, ref field) ?? (field = new HashSet<PageVersionBlockEntity>());

    public ICollection<PageBlockReferenceEntity> References =>
        this._lazyLoader?.Load(this, ref field) ?? (field = new HashSet<PageBlockReferenceEntity>());
}
