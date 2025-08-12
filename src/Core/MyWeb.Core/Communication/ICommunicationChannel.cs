using System;
using System.Collections.Generic;

namespace MyWeb.Core.Communication
{
    /// <summary>
    /// Tüm haberleşme sürücülerinin uyması gereken ortak arayüz.
    /// OPC UA, ModbusTCP, Siemens S7 vb. tüm kanallar aynı sözleşme ile kullanılabilir.
    /// </summary>
    public interface ICommunicationChannel : IDisposable
    {
        bool Connect();
        void Disconnect();
        bool IsConnected { get; }

        void AddTag(TagDefinition tag);
        bool RemoveTag(string tagName);

        T ReadTag<T>(string tagName);
        Dictionary<string, object> ReadTags(IEnumerable<string> tagNames);
        bool WriteTag(string tagName, object value);

        ChannelHealth GetHealth();
        bool TryReadTag<T>(string tagName, out T value, out string? error);
        Dictionary<string, TagValue> ReadTagsWithQuality(IEnumerable<string> tagNames);
    }

    /// <summary>
    /// Kanal sağlık bilgisi – izleme/log/diagnostics için.
    /// </summary>
    public sealed class ChannelHealth
    {
        public bool IsConnected { get; set; }
        public DateTimeOffset LastOkUtc { get; set; }
        public string? LastErrorMessage { get; set; }

        // --- Eklenen alanlar ---
        public DateTimeOffset? StartTimeUtc { get; set; }       // İlk başarılı bağlanma zamanı
        public double UptimeSeconds { get; set; }               // İlk başarılı bağlanmadan beri geçen süre (sn)
        public long ReconnectCount { get; set; }                // Başlangıçtan beri yeniden bağlanma sayısı
        public DateTimeOffset? LastReconnectUtc { get; set; }   // Son reconnect zamanı
    }

    /// <summary>
    /// Değer + Kalite + Zaman damgası (UI, trend, alarm için standart paket).
    /// </summary>
    public sealed class TagValue
    {
        public object? Value { get; set; }
        public string Quality { get; set; } = "Good"; // "Good" | "Bad" | "Uncertain"
        public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
    }
}
