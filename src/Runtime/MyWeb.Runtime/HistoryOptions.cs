namespace MyWeb.Runtime;

public sealed class HistoryOptions
{
    public bool Enabled { get; set; } = true;
    public int WriteIntervalSeconds { get; set; } = 1;
    public int BatchSize { get; set; } = 100;
    public int MaxQueue { get; set; } = 10000;
}
