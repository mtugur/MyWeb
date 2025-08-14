namespace MyWeb.Runtime.History
{
    /// <summary>HistoryWriter çalışma ayarları.</summary>
    public sealed class HistoryWriterOptions
    {
        public bool Enabled { get; set; } = true;
        public int  PollMs  { get; set; } = 1000;
        public int  BatchSize { get; set; } = 500;
        public string? ProjectKey { get; set; }
        public bool UseRandom { get; set; } = true;
    }
}
