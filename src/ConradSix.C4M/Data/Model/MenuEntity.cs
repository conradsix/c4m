namespace ConradSix.C4M.Data.Model;

using System.ComponentModel.DataAnnotations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

[Index(nameof(Name), IsUnique = true)]
internal sealed class MenuEntity : EntityBase
{
    private readonly ILazyLoader? _lazyLoader;

    public MenuEntity()
    {
    }

#pragma warning disable IDE0051 // Remove unused private members
    private MenuEntity(ILazyLoader lazyLoader) => this._lazyLoader = lazyLoader;
#pragma warning restore IDE0051 // Remove unused private members

    public string Name { get; set; } = string.Empty;

    [Timestamp]
    public uint Version { get; set; }

    public ICollection<MenuItemEntity> RootItems =>
        this._lazyLoader?.Load(this, ref field) ?? (field = new HashSet<MenuItemEntity>());
}
