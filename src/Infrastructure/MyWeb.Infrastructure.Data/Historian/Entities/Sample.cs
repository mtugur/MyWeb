using MyWeb.Core.Hist;

namespace MyWeb.Persistence.Historian.Entities
{
    /// <summary>
    /// Zaman-serisi bir ölçüm kaydı (ham örnek).
    /// NOT: DataType enum’unu SQL’de TINYINT olarak saklamayı DbContext’te HasConversion<byte>() ile ayarlayacağız.
    /// Utc alanı DB’de datetime2(3), UTC converter ile zorlanacak.
    /// </summary>
    public class Sample
    {
        /// <summary>Birincil anahtar (identity). PK nonclustered olacak; clustered index (TagId, Utc).</summary>
        public long Id { get; set; }

        /// <summary>Proje bağlamı (çoklu proje desteği).</summary>
        public int ProjectId { get; set; }

        /// <summary>Tag sözlüğündeki benzersiz kimlik.</summary>
        public int TagId { get; set; }

        /// <summary>UTC zaman damgası (ms hassasiyet).</summary>
        public DateTime Utc { get; set; }

        /// <summary>Veri tipi (Bool/Int/Float/String/Date).</summary>
        public DataType DataType { get; set; }

        /// <summary>Sayısal değer (double, SQL: FLOAT(53)).</summary>
        public double? ValueNumeric { get; set; }

        /// <summary>Metin değer (default NVARCHAR(4000); LongString taglerde NVARCHAR(MAX)).</summary>
        public string? ValueText { get; set; }

        /// <summary>Bool değer.</summary>
        public bool? ValueBool { get; set; }

        /// <summary>Kalite kodu (OPC benzeri; 0 = good).</summary>
        public short Quality { get; set; }

        /// <summary>Kaynak (driver/calc/manual vs.).</summary>
        public byte? Source { get; set; }

        /// <summary>Yıl*100 + Ay (hesaplanan, persisted). Sorgu hızlandırma için.</summary>
        public int MonthKey { get; private set; }
    }
}
