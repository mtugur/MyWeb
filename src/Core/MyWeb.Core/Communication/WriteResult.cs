using System;

namespace MyWeb.Core.Communication
{
    /// <summary>
    /// Yazma işlemi sonucu: başarılı mı + kalite (Good/Bad) + hata metni (varsa).
    /// </summary>
    public sealed class WriteResult
    {
        public bool Success { get; set; }
        public TagQuality Quality { get; set; } = TagQuality.Bad;
        public string? Error { get; set; }
    }
}
