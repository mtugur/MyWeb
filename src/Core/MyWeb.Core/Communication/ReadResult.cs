using System;

namespace MyWeb.Core.Communication
{
    /// <summary>
    /// Okuma işlemi sonucu: Değer + kalite + zaman + hata metni (varsa).
    /// UI veya üst katmanlar için tek tip dönüş.
    /// </summary>
    public sealed class ReadResult<T>
    {
        public T? Value { get; set; }
        public TagQuality Quality { get; set; } = TagQuality.Bad;
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
        public string? Error { get; set; }
        public bool IsOk => Quality == TagQuality.Good && Error == null;
    }
}
