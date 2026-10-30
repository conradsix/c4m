// spell-checker: ignore mipr

namespace ConradSix.C4M.Data;

using ConradSix.C4M.Data.Model;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations;

using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

// The engine's own tables, in their own schema. Hosts never see this context: they use the engine's services.
internal sealed class CmsEngineDbContext(DbContextOptions<CmsEngineDbContext> options)
    : DbContext(options)
{
    public const string Schema = "cms";

    public DbSet<PageEntity> Pages { get; set; } = null!;

    public DbSet<BlockTypeEntity> BlockTypes { get; set; } = null!;

    public DbSet<PageBlockEntity> PageBlocks { get; set; } = null!;

    public DbSet<PageVersionEntity> PageVersions { get; set; } = null!;

    public DbSet<PageVersionBlockEntity> PageVersionBlocks { get; set; } = null!;

    public DbSet<PageBlockReferenceEntity> PageBlockReferences { get; set; } = null!;

    public DbSet<MenuEntity> Menus { get; set; } = null!;

    public DbSet<MenuItemEntity> MenuItems { get; set; } = null!;

    public DbSet<MenuActionTypeEntity> MenuActionTypes { get; set; } = null!;

    public DbSet<MenuItemPageReferenceEntity> MenuItemPageReferences { get; set; } = null!;

    public DbSet<NavigationSettingsEntity> NavigationSettings { get; set; } = null!;

    // Runtime and design time both configure the context here, so they agree on the schema and its history table.
    public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder options, NpgsqlDataSource dataSource) =>
        Conventions(options.UseNpgsql(dataSource, Npgsql));

    public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder options, string connectionString) =>
        Conventions(options.UseNpgsql(connectionString, Npgsql));

    public override int SaveChanges()
    {
        this.Stamp();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        this.Stamp();
        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<BlockTypeEntity>(e =>
        {
            e.HasIndex(bt => bt.JsonSchema)
                .HasMethod("gin");
        });

        modelBuilder.Entity<PageBlockEntity>(e =>
        {
            e.HasIndex(pb => pb.BlockData)
                .HasMethod("gin");
        });

        modelBuilder.Entity<PageEntity>(e =>
        {
            e.HasIndex(p => p.Path).IsUnique().HasFilter("archived_at IS NULL");
            e.HasIndex(p => new { p.ParentId, p.Slug }).IsUnique().HasFilter("archived_at IS NULL");
            e.ToTable(t => t.HasCheckConstraint("ck_pages_archived_unpublished", "archived_at IS NULL OR NOT is_published"));

            e.Property(p => p.Path)
                .ValueGeneratedOnAddOrUpdate();

            e.HasOne(p => p.NavigationMenu)
                .WithMany()
                .HasForeignKey(p => p.NavigationMenuId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PageVersionEntity>(e =>
        {
            e.HasIndex(pv => new { pv.PageId, pv.IsCurrent })
                .IsUnique()
                .HasFilter("is_current = true");
            e.HasIndex(pv => new { pv.PageId, pv.IdempotencyKey })
                .IsUnique()
                .HasFilter("idempotency_key IS NOT NULL");
        });

        modelBuilder.Entity<PageVersionEntity>()
            .HasMany(pv => pv.Blocks)
            .WithMany(pb => pb.PageVersions)
            .UsingEntity<PageVersionBlockEntity>(
                j => j
                    .HasOne(pvb => pvb.PageBlock)
                    .WithMany(pb => pb.VersionBlocks)
                    .HasForeignKey(pvb => pvb.PageBlockId),
                j => j
                    .HasOne(pvb => pvb.PageVersion)
                    .WithMany(pv => pv.VersionBlocks)
                    .HasForeignKey(pvb => pvb.PageVersionId),
                j =>
                {
                    j.HasKey(pvb => new { pvb.PageVersionId, pvb.PageBlockId });
                    j.HasIndex(pvb => new { pvb.PageVersionId, pvb.SortOrder }).IsUnique();
                });

        modelBuilder.Entity<PageBlockReferenceEntity>(e =>
        {
            e.HasKey(reference => new { reference.PageBlockId, reference.Kind, reference.TargetId, reference.JsonPath });
            e.HasIndex(reference => new { reference.Kind, reference.TargetId });
            e.HasOne(reference => reference.PageBlock)
                .WithMany(block => block.References)
                .HasForeignKey(reference => reference.PageBlockId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MenuActionTypeEntity>(e =>
        {
            e.HasIndex(mat => mat.JsonSchema)
                .HasMethod("gin");
        });

        modelBuilder.Entity<MenuItemEntity>(e =>
        {
            e.HasOne(mi => mi.Menu)
                .WithMany(m => m.RootItems)
                .HasForeignKey(mi => mi.MenuId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(mi => mi.Parent)
                .WithMany(mi => mi.Children)
                .HasForeignKey(mi => mi.ParentId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(mi => mi.ActionType)
                .WithMany(mat => mat.MenuItems)
                .HasForeignKey(mi => mi.ActionTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(mi => mi.ActionData)
                .HasMethod("gin");

            e.ToTable(t => t.HasCheckConstraint(
                "ck_menu_item_menu_xor_parent",
                "((menu_id IS NOT NULL AND parent_id IS NULL) OR (menu_id IS NULL AND parent_id IS NOT NULL))"));
        });

        modelBuilder.Entity<MenuItemPageReferenceEntity>(e =>
        {
            e.HasKey(mipr => new { mipr.MenuItemId, mipr.ReferencedPageId });
            e.HasIndex(mipr => mipr.ReferencedPageId);

            e.HasOne(mipr => mipr.MenuItem)
                .WithMany(mi => mi.PageReferences)
                .HasForeignKey(mipr => mipr.MenuItemId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(mipr => mipr.ReferencedPage)
                .WithMany()
                .HasForeignKey(mipr => mipr.ReferencedPageId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<NavigationSettingsEntity>(e =>
        {
            e.HasOne(settings => settings.DefaultMenu)
                .WithMany()
                .HasForeignKey(settings => settings.DefaultMenuId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(settings => settings.FooterMenu)
                .WithMany()
                .HasForeignKey(settings => settings.FooterMenuId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void Npgsql(NpgsqlDbContextOptionsBuilder npgsql) => npgsql
        .MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schema)
        .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);

    private static DbContextOptionsBuilder Conventions(DbContextOptionsBuilder options) => options
        .UseSnakeCaseNamingConvention()
        .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ContextInitialized))
        .EnableDetailedErrors();

    private void Stamp()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in this.ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity is IHasGuidId { Id: var id } withId && id == Guid.Empty)
                {
                    withId.Id = Guid.NewGuid();
                }

                if (entry.Entity is IHasCreatedAt created)
                {
                    created.CreatedAt = now;
                }
            }

            if (entry.State is EntityState.Added or EntityState.Modified && entry.Entity is IHasUpdatedAt updated)
            {
                updated.UpdatedAt = now;
            }
        }
    }
}
