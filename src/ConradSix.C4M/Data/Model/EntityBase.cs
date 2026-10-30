namespace ConradSix.C4M.Data.Model;

using System.ComponentModel.DataAnnotations;

using Microsoft.EntityFrameworkCore;

[Index(nameof(CreatedAt))]
[Index(nameof(UpdatedAt))]
internal abstract class EntityBase : IHasGuidId, IHasTimestamps
{
    [Key]
    public Guid Id { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
