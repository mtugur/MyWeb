using Microsoft.EntityFrameworkCore;
using MyWeb.Persistence.Catalog.Entities;
using MyWeb.Persistence.Common;

namespace MyWeb.Persistence.Catalog
{
    /// <summary>
    /// Proje katalog/snapshot şeması (default schema: catalog).
    /// Tasarım/snapshot verileri burada tutulur.
    /// </summary>
    public class CatalogDbContext : DbContext
    {
        public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options) { }

        public DbSet<Project> Projects => Set<Project>();
        public DbSet<ProjectVersion> ProjectVersions => Set<ProjectVersion>();
        public DbSet<Controller> Controllers => Set<Controller>();
        public DbSet<Tag> Tags => Set<Tag>();
        public DbSet<TagArchiveConfig> TagArchiveConfigs => Set<TagArchiveConfig>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            b.HasDefaultSchema("catalog");

            // Ortak: UTC converter (tarih alanlarına tek tek uyguluyoruz)
            var utc = UtcDateTimeConverter.Instance;

            // Project
            b.Entity<Project>(e =>
            {
                e.ToTable("Projects");
                e.HasKey(x => x.Id);

                e.Property(x => x.Key).IsRequired().HasMaxLength(128);
                e.Property(x => x.Name).IsRequired().HasMaxLength(256);
                e.Property(x => x.Version).IsRequired().HasMaxLength(32);
                e.Property(x => x.MinEngine).IsRequired().HasMaxLength(32);
                e.Property(x => x.CreatedUtc).HasConversion(utc).HasColumnType("datetime2(3)");

                e.HasIndex(x => x.Key).IsUnique();
            });

            // ProjectVersion
            b.Entity<ProjectVersion>(e =>
            {
                e.ToTable("ProjectVersions");
                e.HasKey(x => x.Id);

                e.Property(x => x.Version).IsRequired().HasMaxLength(32);
                e.Property(x => x.AppliedUtc).HasConversion(utc).HasColumnType("datetime2(3)");
                e.Property(x => x.Hash).IsRequired().HasMaxLength(128);

                e.HasOne(x => x.Project)
                 .WithMany(p => p.Versions)
                 .HasForeignKey(x => x.ProjectId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // Controller
            b.Entity<Controller>(e =>
            {
                e.ToTable("Controllers");
                e.HasKey(x => x.Id);

                e.Property(x => x.Name).IsRequired().HasMaxLength(128);
                e.Property(x => x.Type).IsRequired().HasMaxLength(64);
                e.Property(x => x.Address).IsRequired().HasMaxLength(256);
                e.Property(x => x.SettingsJson);

                e.HasOne(x => x.Project)
                 .WithMany(p => p.Controllers)
                 .HasForeignKey(x => x.ProjectId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // Tag
            b.Entity<Tag>(e =>
            {
                e.ToTable("Tags");
                e.HasKey(x => x.Id);

                e.Property(x => x.Name).IsRequired().HasMaxLength(128);
                e.Property(x => x.Path).IsRequired().HasMaxLength(512);

                e.Property(x => x.Unit).HasMaxLength(32);
                e.Property(x => x.Address).HasMaxLength(256);
                e.Property(x => x.DriverRef).HasMaxLength(64);

                // DataType enum -> byte (TINYINT)
                e.Property(x => x.DataType).HasConversion<byte>().HasColumnType("tinyint");

                e.HasIndex(x => new { x.ProjectId, x.Path }).IsUnique();

                e.HasOne(x => x.Project)
                 .WithMany(p => p.Tags)
                 .HasForeignKey(x => x.ProjectId)
                 .OnDelete(DeleteBehavior.Cascade);

                // One-to-one TagArchiveConfig
                e.HasOne(x => x.Archive)
                 .WithOne(a => a.Tag)
                 .HasForeignKey<TagArchiveConfig>(a => a.TagId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // TagArchiveConfig
            b.Entity<TagArchiveConfig>(e =>
            {
                e.ToTable("TagArchiveConfigs");
                e.HasKey(x => x.Id);

                // Enum saklama
                e.Property(x => x.Mode).HasConversion<byte>().HasColumnType("tinyint");

                e.Property(x => x.RollupsJson).HasMaxLength(512);

                // Tekil ilişki
                e.HasIndex(x => x.TagId).IsUnique();
            });
        }
    }
}
