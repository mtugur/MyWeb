namespace MyWeb.Core.Runtime.Health;

/// <summary>
/// Basit çalışma sağlık durumu.
/// </summary>
public enum HealthStatus
{
    Healthy = 0,
    Degraded = 1,
    Unhealthy = 2
}
