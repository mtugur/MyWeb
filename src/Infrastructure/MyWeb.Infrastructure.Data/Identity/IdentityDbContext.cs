using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MyWeb.Infrastructure.Data.Identity;

public class IdentityDb : IdentityDbContext<ApplicationUser, ApplicationRole, string>
{
    public const string Schema = "auth";

    public IdentityDb(DbContextOptions<IdentityDb> options) : base(options) { }

    public DbSet<PermissionSet> PermissionSets => Set<PermissionSet>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserPermissionSet> UserPermissionSets => Set<UserPermissionSet>();
    public DbSet<RolePermissionSet> RolePermissionSets => Set<RolePermissionSet>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // Varsayılan şema
        b.HasDefaultSchema(Schema);

        // Identity tabloları
        b.Entity<ApplicationUser>().ToTable("Users");
        b.Entity<ApplicationRole>().ToTable("Roles");
        b.Entity<Microsoft.AspNetCore.Identity.IdentityUserClaim<string>>().ToTable("UserClaims");
        b.Entity<Microsoft.AspNetCore.Identity.IdentityUserLogin<string>>().ToTable("UserLogins");
        b.Entity<Microsoft.AspNetCore.Identity.IdentityUserToken<string>>().ToTable("UserTokens");
        b.Entity<Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>>().ToTable("RoleClaims");
        b.Entity<Microsoft.AspNetCore.Identity.IdentityUserRole<string>>().ToTable("UserRoles");

        // PermissionSet
        b.Entity<PermissionSet>(e =>
        {
            e.ToTable("PermissionSets");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
        });

        // Permission
        b.Entity<Permission>(e =>
        {
            e.ToTable("Permissions");
            e.HasKey(x => x.Id);
            e.Property(x => x.ScopeId).HasMaxLength(256).IsRequired();

            e.HasOne(x => x.PermissionSet)       // NAVIGASYON ÜZERİNDEN
             .WithMany()
             .HasForeignKey(x => x.PermissionSetId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => new { x.PermissionSetId, x.ScopeType, x.ScopeId, x.Access }).IsUnique();
        });

        // UserPermissionSet
        b.Entity<UserPermissionSet>(e =>
        {
            e.ToTable("UserPermissionSets");
            e.HasKey(x => x.Id);

            e.HasOne(x => x.PermissionSet)       // NAVIGASYON ÜZERİNDEN
             .WithMany()
             .HasForeignKey(x => x.PermissionSetId)
             .OnDelete(DeleteBehavior.Restrict); // Çift cascade FK engellemek için

            e.HasIndex(x => new { x.UserId, x.PermissionSetId }).IsUnique();
        });

        // RolePermissionSet
        b.Entity<RolePermissionSet>(e =>
        {
            e.ToTable("RolePermissionSets");
            e.HasKey(x => x.Id);

            e.HasOne(x => x.PermissionSet)       // NAVIGASYON ÜZERİNDEN
             .WithMany()
             .HasForeignKey(x => x.PermissionSetId)
             .OnDelete(DeleteBehavior.Restrict); // Çift cascade FK engellemek için

            e.HasIndex(x => new { x.RoleId, x.PermissionSetId }).IsUnique();
        });

        // AuditLog
        b.Entity<AuditLog>(e =>
        {
            e.ToTable("AuditLogs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Action).HasMaxLength(200).IsRequired();
            e.Property(x => x.Entity).HasMaxLength(100);
            e.Property(x => x.EntityId).HasMaxLength(200);
            e.HasIndex(x => x.CreatedAt);
        });
    }
}
