using System;
using System.Collections.Generic;
using System.Threading;

namespace MyWeb.Core.Communication
{
    /// <summary>
    /// Herhangi bir ICommunicationChannel'ı sarar; basit retry uygular.
    /// (Zaman aşımı/yüksek dayanıklılık gerektiğinde devreye alınır.)
    /// </summary>
    public sealed class ResilientCommunicationChannelDecorator : ICommunicationChannel
    {
        private readonly ICommunicationChannel _inner;
        private readonly int _maxRetry;
        private readonly int _delayMs;

        /// <param name="inner">Sarmalanacak asıl kanal</param>
        /// <param name="maxRetry">Başarısızlıkta tekrar sayısı (başlangıç denemesine ek olarak)</param>
        /// <param name="retryDelayMs">Tekrarlar arası bekleme (ms)</param>
        public ResilientCommunicationChannelDecorator(ICommunicationChannel inner, int maxRetry = 1, int retryDelayMs = 100)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _maxRetry = Math.Max(0, maxRetry);
            _delayMs = Math.Max(0, retryDelayMs);
        }

        public bool Connect() => ExecuteWithRetry(_inner.Connect);

        public void Disconnect() => _inner.Disconnect();

        public bool IsConnected => _inner.IsConnected;

        public void AddTag(TagDefinition tag) => _inner.AddTag(tag);

        public bool RemoveTag(string tagName) => _inner.RemoveTag(tagName);

        public T ReadTag<T>(string tagName) => ExecuteWithRetry(() => _inner.ReadTag<T>(tagName));

        public Dictionary<string, object> ReadTags(IEnumerable<string> tagNames) =>
            ExecuteWithRetry(() => _inner.ReadTags(tagNames));

        public bool WriteTag(string tagName, object value) =>
            ExecuteWithRetry(() => _inner.WriteTag(tagName, value));

        public ChannelHealth GetHealth() =>
            ExecuteWithRetry(() => _inner.GetHealth());

        public bool TryReadTag<T>(string tagName, out T value, out string? error)
        {
            // Try-pattern: hata fırlatma yok, yine de  retry faydalı olabilir.
            int attempts = 0;
            while (true)
            {
                if (_inner.TryReadTag(tagName, out value, out error))
                    return true;

                attempts++;
                if (attempts > _maxRetry) return false;
                Thread.Sleep(_delayMs);
            }
        }

        public Dictionary<string, TagValue> ReadTagsWithQuality(IEnumerable<string> tagNames) =>
            ExecuteWithRetry(() => _inner.ReadTagsWithQuality(tagNames));

        public void Dispose()
        {
            try { _inner.Dispose(); } catch { /* yut */ }
        }

        // ----------------- Yardımcı -----------------
        private T ExecuteWithRetry<T>(Func<T> op)
        {
            int attempts = 0;
            Exception? last = null;
            while (true)
            {
                try { return op(); }
                catch (Exception ex)
                {
                    last = ex;
                    attempts++;
                    if (attempts > _maxRetry) throw last;
                    Thread.Sleep(_delayMs);
                }
            }
        }

        private bool ExecuteWithRetry(Func<bool> op)
        {
            return ExecuteWithRetry(() =>
            {
                bool ok = op();
                if (!ok) throw new Exception("Operation returned false.");
                return true;
            });
        }
    }
}
