using MyWeb.Core.Hist;

namespace MyWeb.Persistence.Catalog.Entities
{
    /// <summary>
    /// Tag (değişken) tanımı: adres, tip, ölçek ve arşiv konfig referansı.
    /// </summary>
    public class Tag
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }

        /// <summary>İnsan okunur ad.</summary>
        public string Name { get; set; } = null!;

        /// <summary>Hiyerarşik yol (benzersiz): örn. "Area1/Mill/Speed".</summary>
        public string Path { get; set; } = null!;

        /// <summary>Veri tipi (SQL'de byte olarak saklanır).</summary>
        public DataType DataType { get; set; }

        /// <summary>Birim (örn. "kg/h").</summary>
        public string? Unit { get; set; }

        /// <summary>Yazılımda değeri ölçeklemek için çarpan.</summary>
        public double? Scale { get; set; }

        /// <summary>Yazılımda değeri düzeltmek için offset.</summary>
        public double? Offset { get; set; }

        /// <summary>Sürücü adresi (örn. DB1.DBW0, ns=2;s=Tag1).</summary>
        public string? Address { get; set; }

        /// <summary>Bu tag'i sağlayan sürücü/modül referansı (örn. "Siemens@2").</summary>
        public string? DriverRef { get; set; }

        /// <summary>String değerler 4000 char'ı aşacaksa NVARCHAR(MAX) kullan.</summary>
        public bool LongString { get; set; }

        // Navs
        public Project Project { get; set; } = null!;
        public TagArchiveConfig? Archive { get; set; }
    }
}
