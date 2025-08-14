using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MyWeb.Persistence.Common;
using MyWeb.Persistence.Historian.Entities;

namespace MyWeb.Persistence.Historian
{
    /// <summary>
    /// Historian (zaman-serisi) şeması (default schema: hist).
    /// </summary>
    public class HistorianDbContext : DbContext
    {
        public HistorianDbContext(DbContextOptions<HistorianDbContext> options) : base(options) { }

        public DbSet<Sample> Samples => Set<Sample>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            b.HasDefaultSchema("hist");

            var utc = UtcDateTimeConverter.Instance;

            b.Entity<Sample>(ConfigureSample(utc));
        }

        private static Action<EntityTypeBuilder<Sample>> ConfigureSample(ValueConverter<DateTime, DateTime> utc)
            => e =>
            {
                e.ToTable("Samples");

                // PK nonclustered; clustered index'i (TagId, Utc)'a vereceğiz
                e.HasKey(x => x.Id).IsClustered(false);

                e.Property(x => x.ProjectId).IsRequired();
                e.Property(x => x.TagId).IsRequired();

                e.Property(x => x.Utc)
                    .HasConversion(utc)
                    .HasColumnType("datetime2(3)")
                    .IsRequired();

                // enum -> byte (TINYINT)
                e.Property(x => x.DataType)
                    .HasConversion<byte>()
                    .HasColumnType("tinyint")
                    .IsRequired();

                // DOUBLE (FLOAT(53))
                e.Property(x => x.ValueNumeric)
                    .HasColumnType("float"); // 53 bit

                // Metin için evrensel NVARCHAR(MAX) (kısa metinler çoğunlukla satır içi kalır)
                e.Property(x => x.ValueText)
                    .HasColumnType("nvarchar(max)");

                e.Property(x => x.ValueBool)
                    .HasColumnType("bit");

                e.Property(x => x.Quality)
                    .HasColumnType("smallint")
                    .IsRequired();

                e.Property(x => x.Source)
                    .HasColumnType("tinyint");

                // YEAR*100 + MONTH computed persisted
                e.Property(x => x.MonthKey)
                 .HasComputedColumnSql("(YEAR([Utc])*100 + MONTH([Utc]))", stored: true);

                // Clustered index: en kritik sorgu paterni (TagId, zaman aralığı)
                e.HasIndex(x => new { x.TagId, x.Utc }).IsClustered();

                // Nonclustered: MonthKey (include ile sık kullanılan kolonlar)
                e.HasIndex(x => x.MonthKey)
                 .IncludeProperties(p => new { p.TagId, p.Utc, p.ValueNumeric, p.Quality });
            };
    }
}
