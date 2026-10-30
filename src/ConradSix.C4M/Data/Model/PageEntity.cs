namespace Parapet.CMS.Engine.Data.Model;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

[Index(nameof(ParentId), nameof(Slug), IsUnique = true)]
[Index(nameof(Path), IsUnique = true)]
internal sealed class PageEntity : EntityBase
{
    private readonly ILazyLoader? _lazyLoader;

    public PageEntity()
    {
    }

#pragma warning disable IDE0051 // Remove unused private members
    private PageEntity(ILazyLoader lazyLoader) => this._lazyLoader = lazyLoader;
#pragma warning restore IDE0051 // Remove unused private members

    public Guid? ParentId { get; set; }

    public Guid? NavigationMenuId { get; set; }

    public bool IsPublished { get; set; }

    public DateTimeOffset? ArchivedAt { get; set; }

    public string Slug { get; set; } = string.Empty;

    public string Path { get; set; } = null!;

    [Timestamp]
    public uint Version { get; set; }

    [ForeignKey(nameof(ParentId))]
    public PageEntity? Parent
    {
        get => this._lazyLoader?.Load(this, ref field)
            ?? throw new InvalidOperationException($"{nameof(this.Parent)} must be loaded before accessing.");
        set;
    }

    [ForeignKey(nameof(NavigationMenuId))]
    public MenuEntity? NavigationMenu
    {
        get => this._lazyLoader?.Load(this, ref field) ?? field;
        set;
    }

    public ICollection<PageEntity> Children =>
        this._lazyLoader?.Load(this, ref field) ?? (field = new HashSet<PageEntity>());

    public ICollection<PageVersionEntity> Versions =>
        this._lazyLoader?.Load(this, ref field) ?? (field = new HashSet<PageVersionEntity>());
}
