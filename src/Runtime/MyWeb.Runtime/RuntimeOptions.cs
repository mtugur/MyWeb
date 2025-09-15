namespace MyWeb.Runtime;

public sealed class RuntimeOptions
{
    public int ReconnectMs { get; set; } = 3000;
    public int HealthDegradedAfterMs { get; set; } = 10000;
    public int HealthUnhealthyAfterMs { get; set; } = 30000;
    public int HeartbeatMs { get; set; } = 1000; // Watchdog tick period (ms)

    // Step-2: TCP probe seçenekleri
    public string PlcIp { get; set; } = "192.168.1.113";
    public int PlcProbePort { get; set; } = 102;         // S7comm
    public int ProbeTimeoutMs { get; set; } = 1000;      // Socket connect timeout
}
