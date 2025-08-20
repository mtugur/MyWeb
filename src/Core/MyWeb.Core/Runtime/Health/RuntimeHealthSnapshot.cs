using System;

namespace MyWeb.Core.Runtime.Health;

/// <summary>
/// Runtime sağlık durumunun anlık özet verisi.
/// </summary>
public sealed class RuntimeHealthSnapshot
{
    public DateTime UtcNow { get; init; } = DateTime.UtcNow;

    /// <summary>Genel sağlık.</summary>
    public HealthStatus Status { get; init; } = HealthStatus.Healthy;

    /// <summary>Son başarılı veri alma (okuma) zamanı (UTC). Opsiyonel.</summary>
    public DateTime? LastGoodSampleUtc { get; init; }

    /// <summary>Ardışık hata sayısı.</summary>
    public int ConsecutiveErrors { get; init; }

    /// <summary>Bilgi amaçlı mesaj.</summary>
    public string? Message { get; init; }
}
