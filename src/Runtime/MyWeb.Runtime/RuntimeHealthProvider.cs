using System;
using System.Threading;
using MyWeb.Core.Runtime.Health;

namespace MyWeb.Runtime;

/// <summary>
/// Thread-safe sağlık bilgisi sağlayıcısı.
/// </summary>
public sealed class RuntimeHealthProvider : IRuntimeHealthProvider
{
    private int _consecutiveErrors;
    private DateTime? _lastGoodSampleUtc;
    private HealthStatus _status = HealthStatus.Healthy;
    private string? _message;
    private readonly object _lock = new();

    public RuntimeHealthSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            return new RuntimeHealthSnapshot
            {
                UtcNow = DateTime.UtcNow,
                Status = _status,
                LastGoodSampleUtc = _lastGoodSampleUtc,
                ConsecutiveErrors = _consecutiveErrors,
                Message = _message
            };
        }
    }

    public void ReportGoodSample()
    {
        lock (_lock)
        {
            _lastGoodSampleUtc = DateTime.UtcNow;
            _consecutiveErrors = 0;
            if (_status != HealthStatus.Healthy)
                _status = HealthStatus.Healthy;
            _message = "OK";
        }
    }

    public void ReportError()
    {
        lock (_lock)
        {
            _consecutiveErrors++;
            if (_consecutiveErrors >= 1 && _status == HealthStatus.Healthy)
                _status = HealthStatus.Degraded;
            _message = $"Errors={_consecutiveErrors}";
        }
    }

    public void ResetErrors()
    {
        lock (_lock)
        {
            _consecutiveErrors = 0;
            _message = "Errors reset";
        }
    }

    public void SetStatus(HealthStatus status, string? message = null)
    {
        lock (_lock)
        {
            _status = status;
            _message = message ?? _message;
        }
    }
}
