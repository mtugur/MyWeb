namespace MyWeb.Infrastructure.Data.Identity;

public class AuditLog
{
    public long Id { get; set; }
    public string? UserId { get; set; }
    public string Action { get; set; } = default!;      // e.g., "User.Create", "Role.Assign"
    public string? Entity { get; set; }                 // e.g., "User","Role","PermissionSet"
    public string? EntityId { get; set; }
    public string? DataBefore { get; set; }             // PII maskesi sana bağlı
    public string? DataAfter { get; set; }
    public string? Ip { get; set; }
    public string? UserAgent { get; set; }
    public string? CorrelationId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
