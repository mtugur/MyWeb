namespace MyWeb.Infrastructure.Data.Identity;

// MVP ŞEMA: Tag/Area/Project bazlı izin kümeleri
public class PermissionSet
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum PermissionScopeType
{
    Tag = 0,
    Area = 1,
    Project = 2
}

public enum PermissionAccess
{
    Read = 0,
    Write = 1,
    Admin = 2
}

public class Permission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PermissionSetId { get; set; }
    public PermissionSet PermissionSet { get; set; } = default!;

    public PermissionScopeType ScopeType { get; set; }
    public string ScopeId { get; set; } = default!; // TagId/AreaId/ProjectId string olarak tutulur
    public PermissionAccess Access { get; set; }
}

public class UserPermissionSet
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = default!;
    public PermissionSet PermissionSet { get; set; } = default!;
    public Guid PermissionSetId { get; set; }
}

public class RolePermissionSet
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RoleId { get; set; } = default!;
    public PermissionSet PermissionSet { get; set; } = default!;
    public Guid PermissionSetId { get; set; }
}
