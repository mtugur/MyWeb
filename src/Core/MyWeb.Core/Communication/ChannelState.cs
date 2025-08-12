namespace MyWeb.Core.Communication
{
    /// <summary>
    /// Kanalın çalışma/bağlantı durumu.
    /// </summary>
    public enum ChannelState
    {
        Disconnected = 0,
        Connecting = 1,
        Connected = 2,
        Faulted = 3
    }
}
