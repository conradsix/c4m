#pragma warning disable SA1402 // These entities form one storage model.
namespace ConradSix.C4M.Content;

using Microsoft.EntityFrameworkCore;

internal static class ContentModel
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ContentType>(entity =>
        {
            entity.ToTable("content_types");
            entity.HasKey(type => type.Name);
            entity.Property(type => type.Name).HasMaxLength(100);
        });
        modelBuilder.Entity<ContentSchema>(entity =>
        {
            entity.ToTable("content_schemas");
            entity.HasKey(schema => new { schema.Type, schema.Version });
            entity.HasOne<ContentType>().WithMany().HasForeignKey(schema => schema.Type).OnDelete(DeleteBehavior.Restrict);
            entity.Property(schema => schema.JsonSchema).HasColumnType("jsonb");
        });
        modelBuilder.Entity<ContentRecord>(entity =>
        {
            entity.ToTable("content_records");
            entity.HasKey(record => new { record.Type, record.Key });
            entity.HasOne<ContentType>().WithMany().HasForeignKey(record => record.Type).OnDelete(DeleteBehavior.Restrict);
            entity.Property(record => record.Key).HasMaxLength(120);
            entity.Property(record => record.Version).IsRowVersion();
        });
        modelBuilder.Entity<ContentRevision>(entity =>
        {
            entity.ToTable("content_revisions");
            entity.HasKey(revision => revision.Id);
            entity.Property(revision => revision.Id).ValueGeneratedNever();
            entity.HasOne<ContentRecord>().WithMany().HasForeignKey(revision => new { revision.Type, revision.Key });
            entity.HasOne<ContentSchema>().WithMany().HasForeignKey(revision => new { revision.Type, revision.SchemaVersion }).OnDelete(DeleteBehavior.Restrict);
            entity.Property(revision => revision.Data).HasColumnType("jsonb");
            entity.Property(revision => revision.IdempotencyKey).HasMaxLength(200);
            entity.HasIndex(revision => new { revision.Type, revision.Key, revision.IdempotencyKey }).IsUnique()
                .HasFilter("idempotency_key IS NOT NULL");
            entity.HasIndex(revision => new { revision.Type, revision.Key, revision.CreatedAt });
        });
    }
}

internal sealed class ContentType
{
    public required string Name { get; init; }

    public bool Singleton { get; init; }
}

internal sealed class ContentSchema
{
    public required string Type { get; init; }

    public int Version { get; init; }

    public required string JsonSchema { get; init; }
}

internal sealed class ContentRecord
{
    public required string Type { get; init; }

    public required string Key { get; init; }

    public Guid LatestRevisionId { get; set; }

    public Guid? PublishedRevisionId { get; set; }

    public uint Version { get; set; }
}

internal sealed class ContentRevision
{
    public required string Type { get; init; }

    public required string Key { get; init; }

    public Guid Id { get; init; }

    public int SchemaVersion { get; init; }

    public required string Data { get; init; }

    public string? IdempotencyKey { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}
