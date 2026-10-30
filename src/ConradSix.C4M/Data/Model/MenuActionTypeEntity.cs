namespace Parapet.CMS.Engine.Data.Model;

using System.ComponentModel.DataAnnotations.Schema;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

[Index(nameof(Name), nameof(Version), IsUnique = true)]
internal sealed class MenuActionTypeEntity : IHasGuidId, IHasCreatedAt
{
    private readonly ILazyLoader? _lazyLoader;

    public MenuActionTypeEntity()
    {
    }

#pragma warning disable IDE0051 // Remove unused private members
    private MenuActionTypeEntity(ILazyLoader lazyLoader) => this._lazyLoader = lazyLoader;
#pragma warning restore IDE0051 // Remove unused private members

    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Version { get; set; } = 1;

    public DateTimeOffset CreatedAt { get; set; }

    [Column(TypeName = "jsonb")]
    public string JsonSchema { get; set; } = string.Empty;

    public ICollection<MenuItemEntity> MenuItems =>
        this._lazyLoader?.Load(this, ref field) ?? (field = new HashSet<MenuItemEntity>());
}
