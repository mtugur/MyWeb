namespace MyWeb.Core.Communication
{
    /// <summary>
    /// Kanal genel zaman aşımı ve tekrar deneme ayarları.
    /// DB yok; şimdilik sabit/DI ile verilebilir.
    /// </summary>
    public sealed class CommunicationOptions
    {
        /// <summary>Her işlem için tekrar sayısı (başlangıç denemesi + bu kadar retry).</summary>
        public int MaxRetryCount { get; set; } = 1;

        /// <summary>Retry aralığı (ms).</summary>
        public int RetryDelayMs { get; set; } = 100;

        /// <summary>Gelecekte per-op timeout uygulanabilir (S7.Net sync çalıştığı için burada saklıyoruz).</summary>
        public int OperationTimeoutMs { get; set; } = 2000;
    }
}
