namespace ConradSix.C4M.Data.Model;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

[Index(nameof(ParentId), nameof(SortOrder), IsUnique = true)]
[Index(nameof(MenuId), nameof(SortOrder), IsUnique = true)]
[Index(nameof(ActionTypeId))]
[Index(nameof(CreatedAt))]
internal sealed class MenuItemEntity : IHasGuidId, IHasCreatedAt
{
    private readonly ILazyLoader? _lazyLoader;

    public MenuItemEntity()
    {
    }

#pragma warning disable IDE0051 // Remove unused private members
    private MenuItemEntity(ILazyLoader lazyLoader) => this._lazyLoader = lazyLoader;
#pragma warning restore IDE0051 // Remove unused private members

    [Key]
    public Guid Id { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? MenuId { get; set; }

    public Guid? ParentId { get; set; }

    public Guid ActionTypeId { get; set; }

    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    [Column(TypeName = "jsonb")]
    public string ActionData { get; set; } = string.Empty;

    [ForeignKey(nameof(MenuId))]
    public MenuEntity? Menu
    {
        get => this._lazyLoader?.Load(this, ref field);
        set;
    }

    [ForeignKey(nameof(ParentId))]
    public MenuItemEntity? Parent
    {
        get => this._lazyLoader?.Load(this, ref field);
        set;
    }

    [ForeignKey(nameof(ActionTypeId))]
    public MenuActionTypeEntity ActionType
    {
        get => this._lazyLoader?.Load(this, ref field)
            ?? throw new InvalidOperationException($"{nameof(this.ActionType)} must be loaded before accessing.");
        set;
    }

    public ICollection<MenuItemEntity> Children =>
        this._lazyLoader?.Load(this, ref field) ?? (field = new HashSet<MenuItemEntity>());

    public ICollection<MenuItemPageReferenceEntity> PageReferences =>
        this._lazyLoader?.Load(this, ref field) ?? (field = new HashSet<MenuItemPageReferenceEntity>());
}
