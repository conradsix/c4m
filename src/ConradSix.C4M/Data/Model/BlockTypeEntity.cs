namespace Parapet.CMS.Engine.Data.Model;

using System.ComponentModel.DataAnnotations.Schema;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

[Index(nameof(Name), nameof(Version), IsUnique = true)]
internal sealed class BlockTypeEntity : IHasGuidId, IHasCreatedAt
{
    private readonly ILazyLoader? _lazyLoader;

    public BlockTypeEntity()
    {
    }

#pragma warning disable IDE0051 // Remove unused private members
    private BlockTypeEntity(ILazyLoader lazyLoader) => this._lazyLoader = lazyLoader;
#pragma warning restore IDE0051 // Remove unused private members

    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Version { get; set; } = 1;

    public DateTimeOffset CreatedAt { get; set; }

    [Column(TypeName = "jsonb")]
    public string JsonSchema { get; set; } = string.Empty;

    public ICollection<PageBlockEntity> Blocks =>
        this._lazyLoader?.Load(this, ref field) ?? (field = new HashSet<PageBlockEntity>());
}
